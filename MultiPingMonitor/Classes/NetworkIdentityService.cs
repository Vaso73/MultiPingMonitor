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

        // Public IP / metadata lookup endpoints.
        // Isolated here so they can be changed in one place.
        internal const string LookupUrl          = "https://ipinfo.io/json";
        internal const string PublicIpFallbackUrl = "https://api.ipify.org";

        private const int WanPollIntervalMs  = 60_000;   // 60 seconds
        private const int LanPollIntervalMs  = 120_000;  // 2 minutes
        private const int BurstIntervalMs    = 10_000;   // 10 seconds between burst ticks
        private const int BurstDurationMs    = 60_000;   // 60 seconds of burst
        private const int DebounceMs         = 2_000;    // 2-second debounce after network change
        private const int PublicIpTimeoutMs  = 5_000;    // 5-second timeout for public-IP phase
        private const int MetaTimeoutMs      = 5_000;    // 5-second timeout for metadata phase

        // ── Internals ────────────────────────────────────────────────────────────

        // Per-request timeouts are enforced with CancellationToken; the HttpClient
        // timeout is a backstop only (covers both phases + overhead).
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
        /// Phase 1 of WAN refresh: quickly fetches the public egress IP from a
        /// lightweight plain-text endpoint (api.ipify.org).  Returns the IP string
        /// on success, or an empty string on timeout / failure.
        /// </summary>
        private async Task<string> FetchPublicIpAsync()
        {
            if (_disposed) return string.Empty;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                cts.CancelAfter(PublicIpTimeoutMs);

                var text = (await _http.GetStringAsync(PublicIpFallbackUrl, cts.Token)
                    .ConfigureAwait(false)).Trim();

                // Validate that the response is a bare IP address.
                return System.Net.IPAddress.TryParse(text, out _) ? text : string.Empty;
            }
            catch (Exception ex) when (IsTransientHttpException(ex))
            {
                System.Diagnostics.Trace.WriteLine(
                    $"NetworkIdentityService: PublicIP lookup failed: {ex.GetType().Name}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Phase 2 of WAN refresh: fetches full metadata (IP, country, ASN, provider)
        /// from ipinfo.io.  On timeout or failure, existing values are silently kept
        /// so the IP shown after phase 1 is never lost.
        /// </summary>
        private async Task FetchMetaAsync()
        {
            if (_disposed) return;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                cts.CancelAfter(MetaTimeoutMs);

                var json = await _http.GetStringAsync(LookupUrl, cts.Token).ConfigureAwait(false);
                ParseAndApplyWanResult(json);
                // Notify immediately so country / ASN / provider appear in the UI
                // while IsRefreshing is still true (before the finally-block reset).
                OnStateChanged();
            }
            catch (Exception ex) when (IsTransientHttpException(ex))
            {
                // Silently keep existing values on transient failure.
                System.Diagnostics.Trace.WriteLine(
                    $"NetworkIdentityService: Meta lookup failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool IsTransientHttpException(Exception ex) =>
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                or JsonException or System.IO.IOException;

        /// <summary>
        /// Parses the JSON response from the lookup endpoint and updates public-IP state.
        /// Extracted to a separate internal method so it can be unit-tested without I/O.
        /// Expected JSON shape (ipinfo.io): { "ip":"...", "country":"...", "org":"AS12345 Name" }
        /// </summary>
        internal void ParseAndApplyWanResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var newIp      = GetStringProp(root, "ip");
            var newCountry = GetStringProp(root, "country");
            var org        = GetStringProp(root, "org");

            // Parse "org" field into ASN token and provider name.
            // ipinfo.io format: "AS12345 WATEL" → Asn="AS12345", Provider="WATEL"
            ParseOrgField(org, out string newAsn, out string newProvider);

            PublicIp    = newIp;
            CountryCode = newCountry;
            Asn         = newAsn;
            Provider    = newProvider;
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
