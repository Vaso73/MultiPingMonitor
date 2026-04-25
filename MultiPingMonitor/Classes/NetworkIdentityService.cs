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
    ///   – Immediate on Start().
    ///   – Normal WAN poll every 60 seconds.
    ///   – LAN poll every 2 minutes (in addition to event-driven updates).
    ///   – After a network change event: 2-second debounce, then immediate refresh,
    ///     then burst mode (every 10 seconds for 60 seconds), then return to normal.
    ///
    /// All state changes are raised on StateChanged (may fire on any thread).
    /// The caller is responsible for dispatching UI updates to the UI thread.
    /// Dispose() unsubscribes all network events and stops all timers.
    /// </summary>
    internal sealed class NetworkIdentityService : IDisposable
    {
        // ── Public state ─────────────────────────────────────────────────────────

        public string LocalIp      { get; private set; } = string.Empty;
        public string PublicIp     { get; private set; } = string.Empty;
        public string CountryCode  { get; private set; } = string.Empty;
        public string Asn          { get; private set; } = string.Empty;
        public string Provider     { get; private set; } = string.Empty;
        public DateTime? LastRefresh { get; private set; }
        public bool IsRefreshing   { get; private set; }

        /// <summary>
        /// Raised whenever any public state property changes.
        /// May be raised from a thread-pool thread – dispatch to UI thread as needed.
        /// </summary>
        public event EventHandler StateChanged;

        // ── Configuration constants ───────────────────────────────────────────────

        // Public IP providers – plain-text endpoints that return just the IP address.
        // Tried in order; the first valid IPv4 response wins.
        internal static readonly string[] PublicIpProviders = new[]
        {
            "https://api.ipify.org",
            "https://ipv4.icanhazip.com",
            "https://checkip.amazonaws.com",
        };

        // Metadata providers – tried in order; the first successful parse wins.
        // Each URL template uses {ip} as a placeholder for the resolved public IP.
        internal static readonly string[] MetaProviders = new[]
        {
            "https://free.freeipapi.com/api/json/{ip}",
            "https://api.seeip.org/geoip/{ip}",
            "http://ip-api.com/json/{ip}?fields=status,countryCode,isp,as,query",
        };

        private const int WanPollIntervalMs      = 60_000;   // 60 seconds
        private const int LanPollIntervalMs      = 120_000;  // 2 minutes
        private const int BurstIntervalMs        = 10_000;   // 10 seconds between burst ticks
        private const int BurstDurationMs        = 60_000;   // 60 seconds of burst
        private const int DebounceMs             = 2_000;    // 2-second debounce after network change
        private const int PublicIpTimeoutMs      = 5_000;    // overall 5-second cap for the public-IP phase
        private const int MetaTimeoutMs          = 5_000;    // overall 5-second cap for the metadata phase
        private const int PerProviderTimeoutMs   = 2_500;    // per-provider attempt cap (≤ phase cap)

        // ── Internals ────────────────────────────────────────────────────────────

        // Per-request timeouts are enforced with CancellationTokens.
        // The HttpClient.Timeout is a hard backstop covering both phases.
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(12_000)
        };

        private System.Timers.Timer _wanTimer;
        private System.Timers.Timer _lanTimer;
        private System.Timers.Timer _burstTimer;
        private System.Timers.Timer _debounceTimer;
        private int _burstTicksLeft;

        // Guards against overlapping refreshes (0 = free, 1 = busy).
        private int _refreshBusy;

        // Master cancellation token – cancelled on Dispose to abort in-flight requests.
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly object _timerLock = new object();
        private bool _disposed;

        // ── Constructor / lifecycle ───────────────────────────────────────────────

        public NetworkIdentityService()
        {
            NetworkChange.NetworkAddressChanged     += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        }

        /// <summary>
        /// Performs an immediate first refresh and starts all background timers.
        /// </summary>
        public void Start()
        {
            // Kick off an immediate first refresh (fire-and-forget, errors swallowed).
            _ = RefreshAllAsync();

            // Normal WAN poll (60 s).
            _wanTimer = new System.Timers.Timer(WanPollIntervalMs) { AutoReset = true };
            _wanTimer.Elapsed += (s, e) => _ = RefreshAllAsync();
            _wanTimer.Start();

            // LAN poll (2 min).
            _lanTimer = new System.Timers.Timer(LanPollIntervalMs) { AutoReset = true };
            _lanTimer.Elapsed += (s, e) => RefreshLocalIpAndNotify();
            _lanTimer.Start();
        }

        // ── Network change handling ───────────────────────────────────────────────

        private void OnNetworkChanged(object sender, EventArgs e)
        {
            if (_disposed) return;

            lock (_timerLock)
            {
                // Restart debounce timer: wait 2 s after the last event before acting.
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
                _burstTicksLeft = BurstDurationMs / BurstIntervalMs; // 6 ticks

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
        /// Starts an immediate refresh on demand (e.g., from a manual refresh button).
        /// The internal overlap guard ensures a second call while a refresh is already
        /// running is silently ignored.
        /// </summary>
        public void RequestRefresh()
        {
            if (_disposed) return;
            _ = RefreshAllAsync();
        }

        // ── Refresh logic ─────────────────────────────────────────────────────────

        private async Task RefreshAllAsync()
        {
            if (_disposed) return;

            // Prevent overlapping refreshes.
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

                // 2a. Fast public-IP lookup — update UI as soon as the IP is known,
                //     without waiting for country / ASN / provider metadata.
                var publicIp = await FetchPublicIpAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(publicIp) && publicIp != PublicIp)
                {
                    PublicIp = publicIp;
                    OnStateChanged();
                }

                // 2b. Metadata lookup (country, ASN, provider).
                await FetchMetaAsync().ConfigureAwait(false);

                // Record completion time only when we obtained at least a public IP.
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

        /// <summary>
        /// Phase 1 of WAN refresh: tries each public-IP provider in order and returns
        /// the first valid IPv4 address received.  The overall phase is capped at
        /// PublicIpTimeoutMs; each individual attempt is capped at PerProviderTimeoutMs.
        /// Returns empty string if all providers fail or time out.
        /// </summary>
        private async Task<string> FetchPublicIpAsync()
        {
            if (_disposed) return string.Empty;

            using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            phaseCts.CancelAfter(PublicIpTimeoutMs);

            foreach (var url in PublicIpProviders)
            {
                if (phaseCts.IsCancellationRequested) break;

                var text = await TryFetchStringAsync(url, phaseCts.Token).ConfigureAwait(false);
                if (System.Net.IPAddress.TryParse(text, out _))
                    return text;
            }

            return string.Empty;
        }

        /// <summary>
        /// Phase 2 of WAN refresh: tries each metadata provider in order and applies
        /// the first successful parse (country code, ASN, provider name).  The overall
        /// phase is capped at MetaTimeoutMs.  Metadata failure never removes the public
        /// IP resolved in phase 1 — it silently keeps existing values.
        /// </summary>
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
                    // Notify immediately so metadata appears mid-refresh.
                    OnStateChanged();
                    return;
                }
            }

            // All metadata providers failed — public IP is still shown; keep existing metadata.
        }

        /// <summary>
        /// Shared helper: fetches a URL as a trimmed string within the given cancellation
        /// token, also applying a PerProviderTimeoutMs sub-deadline.  Returns empty string
        /// on any transient HTTP error or timeout.
        /// </summary>
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
        /// Handles the three supported schemas:
        ///   – freeipapi.com:  { "countryCode":"SK", "asnNumber":5578, "asnOrganisation":"…" }
        ///   – seeip.org:      { "country_code":"SK", "organization":"AS5578 …" }
        ///   – ip-api.com:     { "status":"success", "countryCode":"SK", "as":"AS5578 …" }
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

                // ip-api.com signals failure via a "status" field.
                if (root.TryGetProperty("status", out var statusEl)
                    && statusEl.ValueKind == JsonValueKind.String
                    && !string.Equals(statusEl.GetString(), "success",
                           StringComparison.OrdinalIgnoreCase))
                    return false;

                // Country code: "countryCode" (freeipapi, ip-api) or "country_code" (seeip).
                countryCode = GetStringProp(root, "countryCode");
                if (string.IsNullOrEmpty(countryCode))
                    countryCode = GetStringProp(root, "country_code");

                // Org string in AS12345 format: "org" (ipinfo), "organization" (seeip), "as" (ip-api).
                var orgStr = GetStringProp(root, "org");
                if (string.IsNullOrEmpty(orgStr)) orgStr = GetStringProp(root, "organization");
                if (string.IsNullOrEmpty(orgStr)) orgStr = GetStringProp(root, "as");

                if (!string.IsNullOrEmpty(orgStr))
                {
                    ParseOrgField(orgStr, out asn, out provider);
                }
                else
                {
                    // freeipapi.com: integer asnNumber + asnOrganisation string.
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

        // ── Static helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the preferred local IPv4 address by probing the default route.
        /// Falls back to enumerating active non-loopback interfaces.
        /// Ignores APIPA (169.254.x.x) and loopback addresses.
        /// Returns empty string when no suitable address is found.
        /// </summary>
        internal static string GetPreferredLocalIp()
        {
            // UDP connect trick: creates no real traffic but asks the OS which
            // local interface it would use for the given remote address.
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
            catch
            {
                // Intentionally swallowed – fall through to interface enumeration.
            }

            // Fallback: walk all active IPv4 unicast addresses.
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                            continue;
                        var ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254."))
                            continue;
                        if (System.Net.IPAddress.IsLoopback(ua.Address))
                            continue;
                        return ip;
                    }
                }
            }
            catch
            {
                // Intentionally swallowed.
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds the single-line footer display text from the current service state.
        /// Separated as a static helper so it can be tested without I/O.
        /// </summary>
        /// <param name="localIp">Current LAN IP (empty = "—").</param>
        /// <param name="publicIp">Current WAN IP (empty = "—").</param>
        /// <param name="countryCode">Two-letter country code (may be empty).</param>
        /// <param name="asn">ASN token, e.g. "AS12345" (may be empty).</param>
        /// <param name="provider">Provider name (may be empty).</param>
        /// <param name="lastRefresh">Last successful refresh time (null = never).</param>
        /// <param name="isRefreshing">True while a refresh is in progress.</param>
        /// <param name="loadingText">Localized placeholder shown while first data is loading.</param>
        /// <param name="updatedLabel">Localized short label for "updated at", e.g. "upd." or "akt."</param>
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
            // While loading with no data yet, show a minimal indicator.
            bool noDataYet = string.IsNullOrEmpty(localIp) && string.IsNullOrEmpty(publicIp);
            if (isRefreshing && noDataYet)
                return $"LAN: {loadingText} | WAN: {loadingText}";

            // LAN segment.
            var lan = string.IsNullOrEmpty(localIp) ? "—" : localIp;

            // WAN segment.
            string wan;
            if (isRefreshing && string.IsNullOrEmpty(publicIp))
            {
                wan = loadingText;
            }
            else if (string.IsNullOrEmpty(publicIp))
            {
                wan = "—";
            }
            else
            {
                wan = publicIp;
                // Append ASN and provider if available.
                var asnPart = string.Empty;
                if (!string.IsNullOrEmpty(asn) || !string.IsNullOrEmpty(provider))
                {
                    var parts = new System.Text.StringBuilder();
                    if (!string.IsNullOrEmpty(asn))     parts.Append(asn);
                    if (!string.IsNullOrEmpty(provider)) { if (parts.Length > 0) parts.Append(' '); parts.Append(provider); }
                    asnPart = " · " + parts;
                }
                wan = wan + asnPart;
            }

            // Country badge prefix (before "WAN:").
            var countryPrefix = string.IsNullOrEmpty(countryCode) ? string.Empty : countryCode + " ";

            return $"LAN: {lan} | {countryPrefix}WAN: {wan}{(lastRefresh.HasValue ? $" | {updatedLabel} {lastRefresh.Value:HH:mm}" : string.Empty)}";
        }

        // ── Private utilities ─────────────────────────────────────────────────────

        private static string GetStringProp(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var prop)
                ? (prop.GetString() ?? string.Empty)
                : string.Empty;
        }

        /// <summary>
        /// Splits an ipinfo.io "org" string ("AS12345 Provider Name") into
        /// ASN token and provider name.  Returns empty strings for both on failure.
        /// </summary>
        internal static void ParseOrgField(string org, out string asn, out string provider)
        {
            asn      = string.Empty;
            provider = string.Empty;

            if (string.IsNullOrEmpty(org)) return;

            int spaceIdx = org.IndexOf(' ');
            if (spaceIdx > 0
                && org.Length > 2
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

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Unsubscribe network events first so no new timer restarts can occur.
            NetworkChange.NetworkAddressChanged     -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;

            // Cancel any in-flight HTTP request.
            try { _cts.Cancel(); } catch { }
            _cts.Dispose();

            lock (_timerLock)
            {
                _wanTimer?.Stop();     _wanTimer?.Dispose();     _wanTimer     = null;
                _lanTimer?.Stop();     _lanTimer?.Dispose();     _lanTimer     = null;
                _burstTimer?.Stop();   _burstTimer?.Dispose();   _burstTimer   = null;
                _debounceTimer?.Stop(); _debounceTimer?.Dispose(); _debounceTimer = null;
            }
        }
    }
}
