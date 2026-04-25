#nullable enable
using System;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Explicit state of the WAN public-IP lookup phase.
    /// </summary>
    public enum WanLookupState
    {
        /// <summary>No lookup has been started yet (initial state).</summary>
        NotStarted,
        /// <summary>A lookup is actively in progress.</summary>
        Loading,
        /// <summary>The last lookup completed and PublicIp contains a valid IP.</summary>
        Succeeded,
        /// <summary>The last lookup failed and no PublicIp is available.</summary>
        Failed,
    }

    /// <summary>
    /// Background service that continuously monitors and exposes local/public
    /// network identity: LAN IP, WAN IP, country code, ASN, and provider.
    ///
    /// Refresh schedule:
    ///   - Immediate on Start().
    ///   - Normal WAN poll every 60 seconds.
    ///   - LAN poll every 2 minutes (in addition to event-driven updates).
    ///   - After a network change event: 2-second debounce, then immediate refresh,
    ///     then burst mode (every 10 seconds for 60 seconds), then return to normal.
    ///
    /// All state changes are raised on StateChanged (may fire on any thread).
    /// The caller is responsible for dispatching UI updates to the UI thread.
    /// Dispose() unsubscribes all network events and stops all timers.
    /// </summary>
    internal sealed class NetworkIdentityService : IDisposable
    {
        // Public state

        public string LocalIp      { get; private set; } = string.Empty;
        public string PublicIp     { get; private set; } = string.Empty;
        public string CountryCode  { get; private set; } = string.Empty;
        public string Asn          { get; private set; } = string.Empty;
        public string Provider     { get; private set; } = string.Empty;
        public DateTime? LastRefresh { get; private set; }
        public bool IsRefreshing   { get; private set; }

        /// <summary>
        /// Explicit state of the WAN public-IP lookup phase.
        /// UI should render loading only when this is <see cref="WanLookupState.Loading"/>;
        /// it must not rely solely on <see cref="IsRefreshing"/>.
        /// </summary>
        public WanLookupState WanState { get; private set; } = WanLookupState.NotStarted;

        /// <summary>
        /// Raised whenever any public state property changes.
        /// May be raised from a thread-pool thread.
        /// </summary>
        public event EventHandler? StateChanged;

        // Configuration constants

        // Public IP providers: plain-text endpoints returning just the IP address.
        // Tried in order; first valid IPv4 response wins.
        internal static readonly string[] PublicIpProviders = new[]
        {
            "https://api.ipify.org",
            "https://ipv4.icanhazip.com",
            "https://checkip.amazonaws.com",
        };

        // Metadata providers: tried in order; first successful parse wins.
        internal static readonly string[] MetaProviders = new[]
        {
            "https://free.freeipapi.com/api/json/{ip}",
            "https://api.seeip.org/geoip/{ip}",
            "http://ip-api.com/json/{ip}?fields=status,countryCode,isp,as,query",
        };

        private const int WanPollIntervalMs          = 60_000;
        private const int LanPollIntervalMs          = 120_000;
        private const int BurstIntervalMs            = 10_000;
        private const int BurstDurationMs            = 60_000;
        private const int DebounceMs                 = 2_000;
        private const int PublicIpTimeoutMs          = 5_000;
        private const int MetaTimeoutMs              = 5_000;
        private const int PerProviderTimeoutMs       = 2_500;
        // Extra buffers so Task.WhenAny hard timeouts fire after the CancellationToken
        // timeouts, acting only as a final safety net if HttpClient ignores the token.
        private const int PhaseHardTimeoutBufferMs   = 2_000;
        private const int ProviderHardTimeoutBufferMs = 1_000;

        // Internals

        // Per-instance HttpClient.
        // Production constructor creates one backed by a SocketsHttpHandler; see ReplaceHttpClientInternal.
        // Test constructor creates one backed by a stub HttpMessageHandler that must not be recreated.
        // ALL reads and writes to _http must be performed under _httpLock.
        private HttpClient _http;
        private readonly object _httpLock = new object();

        // True in the production constructor: manual/network-change refreshes are allowed to
        // recreate the HttpClient so OS-level DNS/socket state never survives VPN route changes.
        // False in the test constructor: the injected handler must not be recreated.
        private readonly bool _allowHttpClientRecreation;

        // Whether this instance subscribed to NetworkChange events.
        // The test constructor skips the subscription so tests run on any OS.
        private readonly bool _subscribedNetworkEvents;

        private System.Timers.Timer? _wanTimer;
        private System.Timers.Timer? _lanTimer;
        private System.Timers.Timer? _burstTimer;
        private System.Timers.Timer? _debounceTimer;
        private int _burstTicksLeft;

        // Guards against overlapping refreshes (0 = free, 1 = busy).
        private int _refreshBusy;

        // Set to 1 when a refresh is requested while _refreshBusy == 1.
        // The running refresh's finally block fires exactly one follow-up when it completes,
        // so RequestRefresh() and network-change events are never silently dropped.
        private volatile int _pendingRefresh;

        // Set to true by RequestRefresh() and network-change debounce to indicate that the
        // next refresh must recreate the HttpClient before making any requests.
        // This defeats OS-level DNS caching and socket/route state that survives VPN changes
        // even when PooledConnectionLifetime=Zero is in use.
        private volatile bool _forceRecreateHttp;

        // Master cancellation token: cancelled on Dispose to abort in-flight requests.
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly object _timerLock = new object();
        private bool _disposed;

        // Constructor / lifecycle

        public NetworkIdentityService()
        {
            _allowHttpClientRecreation = true;
            // Initialise _http via the shared helper so the same creation logic is used
            // both here and during forced-refresh recreation.
            _http = BuildFreshHttpClient();
            _subscribedNetworkEvents = true;
            NetworkChange.NetworkAddressChanged     += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        }

        /// <summary>
        /// Constructor for unit testing. Accepts a stub HttpMessageHandler and
        /// does NOT subscribe to NetworkChange events so tests run on any OS.
        /// HttpClient recreation is disabled because the injected handler must not be replaced.
        /// </summary>
        internal NetworkIdentityService(HttpMessageHandler testHandler)
        {
            _http = new HttpClient(testHandler) { Timeout = TimeSpan.FromMilliseconds(12_000) };
            _allowHttpClientRecreation = false;
            _subscribedNetworkEvents = false;
        }

        /// <summary>
        /// Performs an immediate first refresh and starts background timers.
        /// Safe to call multiple times: stops existing timers before starting new ones
        /// so calling Start() again never leaks orphaned timer instances.
        /// </summary>
        public void Start()
        {
            // Stop and dispose any previously-started timers.
            lock (_timerLock)
            {
                _wanTimer?.Stop(); _wanTimer?.Dispose(); _wanTimer = null;
                _lanTimer?.Stop(); _lanTimer?.Dispose(); _lanTimer = null;
            }

            _ = RefreshAllAsync();

            lock (_timerLock)
            {
                _wanTimer = new System.Timers.Timer(WanPollIntervalMs) { AutoReset = true };
                _wanTimer.Elapsed += (s, e) => _ = RefreshAllAsync();
                _wanTimer.Start();

                _lanTimer = new System.Timers.Timer(LanPollIntervalMs) { AutoReset = true };
                _lanTimer.Elapsed += (s, e) => RefreshLocalIpAndNotify();
                _lanTimer.Start();
            }
        }

        // Network change handling

        private void OnNetworkChanged(object? sender, EventArgs e)
        {
            if (_disposed) return;

            lock (_timerLock)
            {
                _debounceTimer?.Stop();
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Timers.Timer(DebounceMs) { AutoReset = false };
                _debounceTimer.Elapsed += (s, ev) =>
                {
                    // Mark pending so the request is not dropped if a refresh is already running.
                    // Also flag for HttpClient recreation: the VPN route has changed.
                    _forceRecreateHttp = true;
                    Interlocked.Exchange(ref _pendingRefresh, 1);
                    _ = RefreshAllAsync();
                    StartBurstMode();
                };
                _debounceTimer.Start();
            }
        }

        private void StartBurstMode()
        {
            if (_disposed) return;

            lock (_timerLock)
            {
                _burstTimer?.Stop();
                _burstTimer?.Dispose();
                _burstTicksLeft = BurstDurationMs / BurstIntervalMs;

                _burstTimer = new System.Timers.Timer(BurstIntervalMs) { AutoReset = true };
                _burstTimer.Elapsed += (s, ev) =>
                {
                    int remaining = Interlocked.Decrement(ref _burstTicksLeft);
                    if (remaining <= 0)
                    {
                        lock (_timerLock)
                        {
                            _burstTimer?.Stop();
                        }
                    }
                    _ = RefreshAllAsync();
                };
                _burstTimer.Start();
            }
        }

        /// <summary>
        /// Starts an immediate refresh on demand (e.g. manual refresh button).
        /// If a refresh is already running, queues exactly one follow-up so the
        /// request is never silently dropped.
        /// </summary>
        public void RequestRefresh()
        {
            if (_disposed) return;
            // Recreate the HttpClient on the next lookup so stale OS-level DNS/socket state
            // bound to the old VPN route is not reused.
            _forceRecreateHttp = true;
            // Mark a pending refresh so the request is not lost if _refreshBusy is currently 1.
            // RefreshAllAsync clears this flag at its very start; the finally block fires a
            // follow-up if the flag was set while the previous refresh was still running.
            Interlocked.Exchange(ref _pendingRefresh, 1);
            _ = RefreshAllAsync();
        }

        // Refresh logic

        /// <summary>
        /// Runs one complete LAN + WAN refresh cycle.
        /// Internal visibility allows unit tests to await the full cycle directly.
        /// </summary>
        internal async Task RefreshAllAsync()
        {
            if (_disposed) return;

            if (Interlocked.CompareExchange(ref _refreshBusy, 1, 0) != 0)
                return;

            // This refresh is now the "pending" one — consume the flag so the finally block
            // only fires a follow-up if another request arrives during this run.
            Interlocked.Exchange(ref _pendingRefresh, 0);

            // If a forced refresh was requested (manual button or network-change), swap out
            // the HttpClient so a fresh TCP connection and DNS resolution are used.
            // This prevents stale OS-level socket/route state from surviving VPN changes,
            // which persists even when PooledConnectionLifetime=Zero is configured.
            if (_forceRecreateHttp)
            {
                _forceRecreateHttp = false;
                ReplaceHttpClientInternal();
            }

            System.Diagnostics.Debug.WriteLine("NetworkIdentityService: RefreshAllAsync start");

            try
            {
                IsRefreshing = true;
                WanState     = WanLookupState.Loading;
                OnStateChanged();

                System.Diagnostics.Debug.WriteLine("NetworkIdentityService: WanState → Loading");

                // 1. LAN (fast, no network required).
                var localIp = GetPreferredLocalIp();
                if (localIp != LocalIp)
                {
                    LocalIp = localIp;
                    OnStateChanged();
                }

                // 2a. Fast public-IP lookup: update UI as soon as IP is known.
                //     Hard timeout via Task.WhenAny ensures we always proceed within bounds,
                //     even if HttpClient does not immediately honour the CancellationToken.
                System.Diagnostics.Debug.WriteLine("NetworkIdentityService: WAN phase start");

                var publicIpTask = FetchPublicIpAsync();
                using (var hardCts = new CancellationTokenSource())
                {
                    var hardTimeout = Task.Delay(PublicIpTimeoutMs + PhaseHardTimeoutBufferMs, hardCts.Token);
                    var phaseWinner = await Task.WhenAny(publicIpTask, hardTimeout).ConfigureAwait(false);
                    hardCts.Cancel(); // stop the delay timer early

                    string publicIp = (phaseWinner == publicIpTask && publicIpTask.IsCompletedSuccessfully)
                        ? publicIpTask.Result
                        : string.Empty;

                    if (!string.IsNullOrEmpty(publicIp))
                    {
                        // When the public IP changes, clear stale metadata immediately so the
                        // UI never shows the country/ASN that belonged to the old IP address.
                        if (!string.Equals(publicIp, PublicIp, StringComparison.Ordinal)
                            && !string.IsNullOrEmpty(PublicIp))
                        {
                            CountryCode = string.Empty;
                            Asn         = string.Empty;
                            Provider    = string.Empty;
                            System.Diagnostics.Debug.WriteLine(
                                $"NetworkIdentityService: IP changed {PublicIp} → {publicIp}, stale metadata cleared");
                        }
                        PublicIp = publicIp;
                        WanState = WanLookupState.Succeeded;
                        OnStateChanged(); // immediate UI update — do not wait for metadata
                        System.Diagnostics.Debug.WriteLine($"NetworkIdentityService: WanState → Succeeded, IP={publicIp}");
                    }
                    else
                    {
                        // All providers failed or timed out.
                        // If a previous IP is cached, keep showing it (Succeeded).
                        // Otherwise mark as Failed so the UI exits the loading state.
                        if (!string.IsNullOrEmpty(PublicIp))
                        {
                            WanState = WanLookupState.Succeeded;
                            System.Diagnostics.Debug.WriteLine(
                                $"NetworkIdentityService: WAN phase failed — keeping cached IP={PublicIp}");
                        }
                        else
                        {
                            WanState = WanLookupState.Failed;
                            System.Diagnostics.Debug.WriteLine(
                                "NetworkIdentityService: WAN phase failed — WanState → Failed");
                        }
                        OnStateChanged();
                    }
                }

                // 2b. Metadata lookup (country, ASN, provider) — only when we have an IP.
                if (!string.IsNullOrEmpty(PublicIp))
                {
                    System.Diagnostics.Debug.WriteLine("NetworkIdentityService: metadata start");
                    await FetchMetaAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine("NetworkIdentityService: metadata done");
                }

                if (!string.IsNullOrEmpty(PublicIp))
                    LastRefresh = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // Ensure WAN state exits Loading on any exception (including OperationCanceledException
                // from master CTS, which was previously excluded by the when-filter).
                if (WanState == WanLookupState.Loading)
                {
                    WanState = string.IsNullOrEmpty(PublicIp)
                        ? WanLookupState.Failed
                        : WanLookupState.Succeeded;
                }
                if (ex is OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"NetworkIdentityService: Refresh cancelled: {ex.Message}");
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"NetworkIdentityService: Unexpected refresh error: {ex.Message}");
                }
            }
            finally
            {
                // Safety net: WanState must NEVER remain Loading after RefreshAllAsync exits,
                // regardless of how the method exits (success, cancellation, or exception).
                if (WanState == WanLookupState.Loading)
                {
                    WanState = string.IsNullOrEmpty(PublicIp)
                        ? WanLookupState.Failed
                        : WanLookupState.Succeeded;
                }
                IsRefreshing = false;
                Interlocked.Exchange(ref _refreshBusy, 0);
                OnStateChanged();
                System.Diagnostics.Debug.WriteLine("NetworkIdentityService: IsRefreshing=false, RefreshAllAsync done");

                // If a refresh was requested while we were running (e.g. manual button press
                // or network-change event), honour it now with a fresh lookup.
                if (Interlocked.Exchange(ref _pendingRefresh, 0) == 1 && !_disposed)
                    _ = RefreshAllAsync();
            }
        }

        private async Task<string> FetchPublicIpAsync()
        {
            if (_disposed) return string.Empty;

            using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            phaseCts.CancelAfter(PublicIpTimeoutMs);

            foreach (var url in PublicIpProviders)
            {
                if (phaseCts.IsCancellationRequested) break;

                System.Diagnostics.Debug.WriteLine($"NetworkIdentityService: WAN provider attempt: {url}");
                var text = await TryFetchStringAsync(url, phaseCts.Token).ConfigureAwait(false);
                // Trim() removes trailing newlines from icanhazip/checkip responses.
                // IPAddress.TryParse confirms it is a bare IPv4 address, not HTML/JSON.
                if (System.Net.IPAddress.TryParse(text, out _))
                {
                    System.Diagnostics.Debug.WriteLine($"NetworkIdentityService: WAN provider success: {url} → {text}");
                    return text;
                }

                System.Diagnostics.Debug.WriteLine($"NetworkIdentityService: WAN provider failed/timeout: {url}");
            }

            return string.Empty;
        }

        private async Task FetchMetaAsync()
        {
            if (_disposed || string.IsNullOrEmpty(PublicIp)) return;

            using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            phaseCts.CancelAfter(MetaTimeoutMs);

            var ip = PublicIp;

            // Best partial result seen so far: country is known but ASN/provider is not yet.
            string partialCountry = string.Empty;

            foreach (var urlTemplate in MetaProviders)
            {
                if (phaseCts.IsCancellationRequested) break;

                var url  = urlTemplate.Replace("{ip}", ip);
                System.Diagnostics.Debug.WriteLine($"NetworkIdentityService: metadata attempt: {url}");
                var json = await TryFetchStringAsync(url, phaseCts.Token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(json)) continue;

                if (!TryParseMetaJson(json, out var country, out var parsedAsn, out var parsedProvider))
                    continue;

                bool hasAsn = !string.IsNullOrEmpty(parsedAsn) || !string.IsNullOrEmpty(parsedProvider);

                if (!string.IsNullOrEmpty(country) && hasAsn)
                {
                    // Full result: country AND ASN/provider → use immediately and stop.
                    CountryCode = country;
                    Asn         = parsedAsn;
                    Provider    = parsedProvider;
                    System.Diagnostics.Debug.WriteLine(
                        $"NetworkIdentityService: metadata success (full): CC={country}, ASN={parsedAsn}");
                    OnStateChanged();
                    return;
                }

                // Partial result: has country but no ASN/provider.
                // Remember it and keep trying richer providers.
                // First-partial-wins: we only capture the first country-only result; if
                // multiple providers return different country codes, the first one is kept
                // so the displayed country is stable and not changed by later partial hits.
                if (!string.IsNullOrEmpty(country) && string.IsNullOrEmpty(partialCountry))
                {
                    partialCountry = country;
                    System.Diagnostics.Debug.WriteLine(
                        $"NetworkIdentityService: metadata partial (country-only): CC={country}, continuing...");
                }
            }

            // All providers exhausted (or phase timed out).
            // Apply the best partial result we found (country-only), if any.
            if (!string.IsNullOrEmpty(partialCountry))
            {
                CountryCode = partialCountry;
                // Asn and Provider remain empty — that is the correct fallback.
                System.Diagnostics.Debug.WriteLine(
                    $"NetworkIdentityService: metadata fallback (country-only): CC={partialCountry}");
                OnStateChanged();
                return;
            }

            System.Diagnostics.Debug.WriteLine("NetworkIdentityService: metadata failed — no provider succeeded");
        }

        private async Task<string> TryFetchStringAsync(string url, CancellationToken phaseToken)
        {
            try
            {
                using var providerCts = CancellationTokenSource.CreateLinkedTokenSource(phaseToken);
                providerCts.CancelAfter(PerProviderTimeoutMs);

                // Snapshot the current client under lock so that a concurrent Dispose()
                // call cannot null/replace it between the null-check and use.
                HttpClient http;
                lock (_httpLock) { http = _http; }

                var fetchTask = http.GetStringAsync(url, providerCts.Token);

                // Hard-timeout fallback: proceed even if GetStringAsync does not honour
                // the CancellationToken promptly (e.g. OS-level TCP keep-alive behaviour).
                using var hardCts = new CancellationTokenSource();
                var hardTimeout = Task.Delay(PerProviderTimeoutMs + ProviderHardTimeoutBufferMs, hardCts.Token);
                var winner = await Task.WhenAny(fetchTask, hardTimeout).ConfigureAwait(false);
                hardCts.Cancel(); // clean up the delay task early

                if (winner != fetchTask || fetchTask.IsFaulted || fetchTask.IsCanceled)
                    return string.Empty;

                return fetchTask.Result.Trim();
            }
            catch (Exception ex) when (IsTransientHttpException(ex))
            {
                System.Diagnostics.Trace.WriteLine(
                    $"NetworkIdentityService: Fetch from {url} failed: {ex.GetType().Name}: {ex.Message}");
                return string.Empty;
            }
        }

        private static bool IsTransientHttpException(Exception ex) =>
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                or JsonException or System.IO.IOException or ObjectDisposedException;

        /// <summary>
        /// Unified JSON parser for metadata responses from any supported provider.
        /// Handles:
        ///   freeipapi.com:  { "countryCode":"SK", "asnNumber":5578, "asnOrganisation":"X" }
        ///   seeip.org:      { "country_code":"SK", "organization":"AS5578 X" }
        ///   ip-api.com:     { "status":"success", "countryCode":"SK", "as":"AS5578 X" }
        /// Returns true when at least a country code or ASN was extracted.
        /// </summary>
        internal static bool TryParseMetaJson(
            string json,
            out string countryCode,
            out string asn,
            out string provider)
        {
            countryCode = asn = provider = string.Empty;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                using var doc  = JsonDocument.Parse(json);
                var        root = doc.RootElement;

                if (root.TryGetProperty("status", out var statusEl)
                    && statusEl.ValueKind == JsonValueKind.String
                    && !string.Equals(statusEl.GetString(), "success",
                           StringComparison.OrdinalIgnoreCase))
                    return false;

                countryCode = GetStringProp(root, "countryCode");
                if (string.IsNullOrEmpty(countryCode))
                    countryCode = GetStringProp(root, "country_code");

                var orgStr = GetStringProp(root, "org");
                if (string.IsNullOrEmpty(orgStr)) orgStr = GetStringProp(root, "organization");
                if (string.IsNullOrEmpty(orgStr)) orgStr = GetStringProp(root, "as");

                if (!string.IsNullOrEmpty(orgStr))
                {
                    ParseOrgField(orgStr, out asn, out provider);
                }
                else
                {
                    if (root.TryGetProperty("asnNumber", out var asnNumEl)
                        && asnNumEl.TryGetInt32(out int asnNum) && asnNum > 0)
                        asn = "AS" + asnNum;

                    provider = GetStringProp(root, "asnOrganisation");
                    if (string.IsNullOrEmpty(provider))
                        provider = GetStringProp(root, "isp");
                }

                return !string.IsNullOrEmpty(countryCode) || !string.IsNullOrEmpty(asn);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private void RefreshLocalIpAndNotify()
        {
            if (_disposed) return;
            var ip = GetPreferredLocalIp();
            if (ip != LocalIp)
            {
                LocalIp = ip;
                OnStateChanged();
            }
        }

        // Static helpers

        internal static string GetPreferredLocalIp()
        {
            try
            {
                using var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram,
                    System.Net.Sockets.ProtocolType.Udp);
                socket.Connect("8.8.8.8", 53);
                var ep = socket.LocalEndPoint as System.Net.IPEndPoint;
                var ip = ep?.Address?.ToString();
                if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0" && !ip.StartsWith("169.254."))
                    return ip;
            }
            catch { }

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                            continue;
                        var ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254.")) continue;
                        if (System.Net.IPAddress.IsLoopback(ua.Address)) continue;
                        return ip;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        internal static string BuildFooterText(
            string localIp,
            string publicIp,
            string countryCode,
            string asn,
            string provider,
            DateTime? lastRefresh,
            bool isRefreshing,
            string loadingText,
            string updatedLabel)
        {
            bool noDataYet = string.IsNullOrEmpty(localIp) && string.IsNullOrEmpty(publicIp);
            if (isRefreshing && noDataYet)
                return $"LAN: {loadingText} | WAN: {loadingText}";

            var lan = string.IsNullOrEmpty(localIp) ? "\u2014" : localIp;

            string wan;
            if (isRefreshing && string.IsNullOrEmpty(publicIp))
            {
                wan = loadingText;
            }
            else if (string.IsNullOrEmpty(publicIp))
            {
                wan = "\u2014";
            }
            else
            {
                wan = publicIp;
                if (!string.IsNullOrEmpty(asn) || !string.IsNullOrEmpty(provider))
                {
                    var parts = new System.Text.StringBuilder();
                    if (!string.IsNullOrEmpty(asn))     parts.Append(asn);
                    if (!string.IsNullOrEmpty(provider)) { if (parts.Length > 0) parts.Append(' '); parts.Append(provider); }
                    wan = wan + " \u00b7 " + parts;
                }
            }

            var countryPrefix = string.IsNullOrEmpty(countryCode) ? string.Empty : countryCode + " ";

            return $"LAN: {lan} | {countryPrefix}WAN: {wan}{(lastRefresh.HasValue ? $" | {updatedLabel} {lastRefresh.Value:HH:mm}" : string.Empty)}";
        }

        private static string GetStringProp(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var prop)
                ? (prop.GetString() ?? string.Empty)
                : string.Empty;
        }

        internal static void ParseOrgField(string? org, out string asn, out string provider)
        {
            asn      = string.Empty;
            provider = string.Empty;
            if (string.IsNullOrEmpty(org)) return;

            int spaceIdx = org.IndexOf(' ');
            if (spaceIdx > 0 && org.Length > 2
                && (org[0] == 'A' || org[0] == 'a')
                && (org[1] == 'S' || org[1] == 's'))
            {
                asn      = org[..spaceIdx];
                provider = org[(spaceIdx + 1)..];
            }
            else
            {
                provider = org;
            }
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        // HTTP client helpers

        /// <summary>
        /// Creates a brand-new <see cref="HttpClient"/> backed by a fresh
        /// <see cref="System.Net.Http.SocketsHttpHandler"/> with zero connection pool lifetime
        /// and no-cache headers. Called once at construction and again on each forced refresh.
        /// </summary>
        private static HttpClient BuildFreshHttpClient()
        {
            var socketsHandler = new System.Net.Http.SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.Zero,
            };
            var client = new HttpClient(socketsHandler) { Timeout = TimeSpan.FromMilliseconds(12_000) };
            client.DefaultRequestHeaders.CacheControl =
                new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };
            return client;
        }

        /// <summary>
        /// Replaces the current <see cref="HttpClient"/> with a fresh instance.
        /// Must only be called while <c>_refreshBusy == 1</c> (i.e. from inside
        /// <see cref="RefreshAllAsync"/>) so no concurrent request is using the old client.
        /// No-ops when <see cref="_allowHttpClientRecreation"/> is false (test constructor).
        /// </summary>
        private void ReplaceHttpClientInternal()
        {
            if (!_allowHttpClientRecreation) return;

            HttpClient? oldClient;
            lock (_httpLock)
            {
                oldClient = _http;
                _http = BuildFreshHttpClient();
            }

            // Dispose the old client after replacing it; any in-flight request on it will
            // fail gracefully (ObjectDisposedException is caught in TryFetchStringAsync).
            try { oldClient?.Dispose(); } catch { }
            System.Diagnostics.Debug.WriteLine("NetworkIdentityService: HttpClient recreated (forced refresh)");
        }

        // IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_subscribedNetworkEvents)
            {
                NetworkChange.NetworkAddressChanged     -= OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            }

            try { _cts.Cancel(); } catch { }
            _cts.Dispose();
            lock (_httpLock) { try { _http?.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"NetworkIdentityService: HttpClient dispose failed: {ex.GetType().Name}: {ex.Message}"); } }

            lock (_timerLock)
            {
                _wanTimer?.Stop();      _wanTimer?.Dispose();      _wanTimer      = null;
                _lanTimer?.Stop();      _lanTimer?.Dispose();      _lanTimer      = null;
                _burstTimer?.Stop();    _burstTimer?.Dispose();    _burstTimer    = null;
                _debounceTimer?.Stop(); _debounceTimer?.Dispose(); _debounceTimer = null;
            }
        }
    }
}
