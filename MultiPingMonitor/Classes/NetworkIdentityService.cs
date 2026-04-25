using System;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace MultiPingMonitor.Classes
{
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
        /// Raised whenever any public state property changes.
        /// May be raised from a thread-pool thread.
        /// </summary>
        public event EventHandler StateChanged;

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

        private const int WanPollIntervalMs    = 60_000;
        private const int LanPollIntervalMs    = 120_000;
        private const int BurstIntervalMs      = 10_000;
        private const int BurstDurationMs      = 60_000;
        private const int DebounceMs           = 2_000;
        private const int PublicIpTimeoutMs    = 5_000;
        private const int MetaTimeoutMs        = 5_000;
        private const int PerProviderTimeoutMs = 2_500;

        // Internals

        // Per-instance HttpClient. Production constructor creates a plain client;
        // test constructor creates one backed by a stub HttpMessageHandler.
        // Disposed in Dispose() in both cases.
        private readonly HttpClient _http;

        // Whether this instance subscribed to NetworkChange events.
        // The test constructor skips the subscription so tests run on any OS.
        private readonly bool _subscribedNetworkEvents;

        private System.Timers.Timer _wanTimer;
        private System.Timers.Timer _lanTimer;
        private System.Timers.Timer _burstTimer;
        private System.Timers.Timer _debounceTimer;
        private int _burstTicksLeft;

        // Guards against overlapping refreshes (0 = free, 1 = busy).
        private int _refreshBusy;

        // Master cancellation token: cancelled on Dispose to abort in-flight requests.
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly object _timerLock = new object();
        private bool _disposed;

        // Constructor / lifecycle

        public NetworkIdentityService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(12_000) };
            _subscribedNetworkEvents = true;
            NetworkChange.NetworkAddressChanged     += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        }

        /// <summary>
        /// Constructor for unit testing. Accepts a stub HttpMessageHandler and
        /// does NOT subscribe to NetworkChange events so tests run on any OS.
        /// </summary>
        internal NetworkIdentityService(HttpMessageHandler testHandler)
        {
            _http = new HttpClient(testHandler) { Timeout = TimeSpan.FromMilliseconds(12_000) };
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

        private void OnNetworkChanged(object sender, EventArgs e)
        {
            if (_disposed) return;

            lock (_timerLock)
            {
                _debounceTimer?.Stop();
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Timers.Timer(DebounceMs) { AutoReset = false };
                _debounceTimer.Elapsed += (s, ev) =>
                {
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
        /// The internal overlap guard silently ignores concurrent calls.
        /// </summary>
        public void RequestRefresh()
        {
            if (_disposed) return;
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

            try
            {
                IsRefreshing = true;
                OnStateChanged();

                // 1. LAN (fast, no network required).
                var localIp = GetPreferredLocalIp();
                if (localIp != LocalIp)
                {
                    LocalIp = localIp;
                    OnStateChanged();
                }

                // 2a. Fast public-IP lookup: update UI as soon as IP is known.
                var publicIp = await FetchPublicIpAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(publicIp) && publicIp != PublicIp)
                {
                    PublicIp = publicIp;
                    OnStateChanged();
                }

                // 2b. Metadata lookup (country, ASN, provider).
                await FetchMetaAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(PublicIp))
                    LastRefresh = DateTime.UtcNow;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                System.Diagnostics.Trace.WriteLine(
                    $"NetworkIdentityService: Unexpected refresh error: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
                Interlocked.Exchange(ref _refreshBusy, 0);
                OnStateChanged();
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

                var text = await TryFetchStringAsync(url, phaseCts.Token).ConfigureAwait(false);
                // Trim() removes trailing newlines from icanhazip/checkip responses.
                // IPAddress.TryParse confirms it is a bare IPv4 address, not HTML/JSON.
                if (System.Net.IPAddress.TryParse(text, out _))
                    return text;
            }

            return string.Empty;
        }

        private async Task FetchMetaAsync()
        {
            if (_disposed || string.IsNullOrEmpty(PublicIp)) return;

            using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            phaseCts.CancelAfter(MetaTimeoutMs);

            var ip = PublicIp;
            foreach (var urlTemplate in MetaProviders)
            {
                if (phaseCts.IsCancellationRequested) break;

                var url  = urlTemplate.Replace("{ip}", ip);
                var json = await TryFetchStringAsync(url, phaseCts.Token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(json)) continue;

                if (TryParseMetaJson(json, out var country, out var parsedAsn, out var parsedProvider))
                {
                    CountryCode = country;
                    Asn         = parsedAsn;
                    Provider    = parsedProvider;
                    OnStateChanged();
                    return;
                }
            }
        }

        private async Task<string> TryFetchStringAsync(string url, CancellationToken phaseToken)
        {
            try
            {
                using var providerCts = CancellationTokenSource.CreateLinkedTokenSource(phaseToken);
                providerCts.CancelAfter(PerProviderTimeoutMs);

                return (await _http.GetStringAsync(url, providerCts.Token)
                    .ConfigureAwait(false)).Trim();
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
                or JsonException or System.IO.IOException;

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

        internal static void ParseOrgField(string org, out string asn, out string provider)
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
            try { _http?.Dispose(); } catch { }

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
