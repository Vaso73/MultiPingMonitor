#nullable enable
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Diagnostic helpers for the network-identity CLI modes.
    ///
    /// --network-identity-lookup  : query all configured public-IP and metadata
    ///     providers; write compact JSON to stdout; exit without starting the WPF UI.
    ///
    /// --network-identity-diagnose : run in-process lookup AND spawn a child process
    ///     running --network-identity-lookup; compare both results; write JSON with
    ///     both result blocks and a summary indicating whether they differ.
    ///
    /// These modes are temporary diagnostic tools whose purpose is to prove whether
    /// a fresh child process sees the correct WAN IP after a VPN toggle while the
    /// already-running UI process does not.  No WAN refresh behaviour is changed here.
    /// </summary>
    internal static class NetworkIdentityDiagnostics
    {
        // Per-provider timeout used by the diagnostic HTTP client (milliseconds).
        // Intentionally slightly longer than the service's PerProviderTimeoutMs (2 500 ms)
        // so the diagnostic has its own budget independent of the service constants.
        private const int DiagPerProviderTimeoutMs  = 8_000;

        // Maximum time to wait for the child process before killing it (milliseconds).
        private const int ChildProcessTimeoutMs     = 30_000;
        // ── Public-facing entry points ────────────────────────────────────────────

        /// <summary>
        /// Runs the full public-IP + metadata lookup using a fresh HttpClient.
        /// Returns compact JSON suitable for writing to stdout.
        /// </summary>
        internal static async Task<string> RunLookupJsonAsync()
        {
            using var http = BuildDiagHttpClient();
            return await RunLookupJsonAsync(http).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs the in-process lookup AND spawns a child process with
        /// <c>--network-identity-lookup</c>; compares both results.
        /// Returns compact JSON with both result blocks and a summary.
        /// </summary>
        internal static async Task<string> RunDiagnoseJsonAsync(string exePath)
        {
            var timestamp = DateTime.UtcNow;
            var processId = Process.GetCurrentProcess().Id;
            var localIp   = NetworkIdentityService.GetPreferredLocalIp();

            // 1. In-process lookup.
            string?    inProcessIp    = null;
            string?    inProcessError = null;
            JsonNode?  inProcessNode  = null;
            try
            {
                var inProcessJson = await RunLookupJsonAsync().ConfigureAwait(false);
                inProcessIp   = ParseSelectedIp(inProcessJson);
                inProcessNode = JsonNode.Parse(inProcessJson);
            }
            catch (Exception ex)
            {
                inProcessError = ex.GetType().Name + ": " + ex.Message;
            }

            // 2. Child-process lookup.
            string?   childIp       = null;
            string?   childError    = null;
            string?   childStderr   = null;
            JsonNode? childNode     = null;
            int       childExitCode = -1;
            try
            {
                var psi = new ProcessStartInfo(exePath, "--network-identity-lookup")
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("Process.Start returned null");
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                if (!proc.WaitForExit(ChildProcessTimeoutMs))
                {
                    try { proc.Kill(); } catch { }
                    childError = "child-process-timeout";
                }
                else
                {
                    childExitCode = proc.ExitCode;
                }

                var childJson = await stdoutTask.ConfigureAwait(false);
                childStderr   = await stderrTask.ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(childJson))
                {
                    childIp   = ParseSelectedIp(childJson);
                    childNode = JsonNode.Parse(childJson);
                }
            }
            catch (Exception ex)
            {
                childError = ex.GetType().Name + ": " + ex.Message;
            }

            bool differ = !string.Equals(inProcessIp, childIp, StringComparison.Ordinal);

            // Build result JSON.
            var result    = new JsonObject();
            result["timestamp"] = timestamp.ToString("o");
            result["processId"] = processId;
            result["localIp"]   = localIp.Length > 0 ? localIp : null;

            var inProcessObj = new JsonObject();
            inProcessObj["selectedPublicIp"] = inProcessIp;
            inProcessObj["result"]           = inProcessNode;
            if (inProcessError != null)
                inProcessObj["error"] = inProcessError;
            result["inProcess"] = inProcessObj;

            var childObj = new JsonObject();
            childObj["selectedPublicIp"] = childIp;
            childObj["result"]           = childNode;
            childObj["exitCode"]         = childExitCode;
            if (childStderr != null && childStderr.Length > 0)
                childObj["stderr"] = childStderr;
            if (childError != null)
                childObj["error"] = childError;
            result["childProcess"] = childObj;

            var summary = new JsonObject();
            summary["differ"]      = differ;
            summary["inProcessIp"] = inProcessIp;
            summary["childIp"]     = childIp;
            result["summary"] = summary;

            return result.ToJsonString();
        }

        // ── Testable core ─────────────────────────────────────────────────────────

        /// <summary>
        /// Runs the full public-IP + metadata lookup using the supplied <paramref name="http"/>.
        /// Internal so unit tests can inject a stub <see cref="HttpClient"/>.
        /// </summary>
        internal static async Task<string> RunLookupJsonAsync(HttpClient http)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var timestamp = DateTime.UtcNow;
            var processId = Process.GetCurrentProcess().Id;
            var localIp   = NetworkIdentityService.GetPreferredLocalIp();

            // Query all public-IP providers individually and record each result.
            var ipProviders = new JsonArray();
            string selectedIp = string.Empty;

            foreach (var url in NetworkIdentityService.PublicIpProviders)
            {
                if (cts.IsCancellationRequested) break;

                var (val, err) = await TryFetchAsync(http, url, cts.Token).ConfigureAwait(false);
                bool valid = !string.IsNullOrEmpty(val)
                             && System.Net.IPAddress.TryParse(val, out _);

                if (valid && selectedIp.Length == 0)
                    selectedIp = val!;

                string? errorField = err;
                if (errorField == null && !valid && val != null && val.Length > 0)
                    errorField = "invalid-response";

                var entry = new JsonObject();
                entry["url"]   = url;
                entry["ip"]    = valid ? val : null;
                entry["error"] = errorField;
                ipProviders.Add(entry);
            }

            // Query metadata providers only when we have an IP.
            var metaProviders = new JsonArray();
            string countryCode = string.Empty;
            string asn         = string.Empty;
            string provider    = string.Empty;

            if (selectedIp.Length > 0)
            {
                foreach (var urlTemplate in NetworkIdentityService.MetaProviders)
                {
                    if (cts.IsCancellationRequested) break;

                    var url = urlTemplate.Replace("{ip}", selectedIp);
                    var (json, err) = await TryFetchAsync(http, url, cts.Token)
                        .ConfigureAwait(false);

                    bool parsed     = false;
                    string? parsedCc   = null;
                    string? parsedAsn  = null;
                    string? parsedProv = null;

                    if (!string.IsNullOrEmpty(json)
                        && NetworkIdentityService.TryParseMetaJson(
                               json, out var cc, out var a, out var p))
                    {
                        parsed = true;
                        parsedCc = cc.Length > 0 ? cc : null;
                        parsedAsn  = a.Length > 0 ? a : null;
                        parsedProv = p.Length > 0 ? p : null;

                        if (countryCode.Length == 0 && cc.Length > 0)
                        {
                            countryCode = cc;
                            asn      = a;
                            provider = p;
                        }
                    }

                    var entry = new JsonObject();
                    entry["url"]         = url;
                    entry["parsed"]      = parsed;
                    entry["countryCode"] = parsedCc;
                    entry["asn"]         = parsedAsn;
                    entry["provider"]    = parsedProv;
                    entry["error"]       = err;
                    metaProviders.Add(entry);
                }
            }

            var result = new JsonObject();
            result["timestamp"]       = timestamp.ToString("o");
            result["processId"]       = processId;
            result["localIp"]         = localIp.Length > 0 ? localIp : null;
            result["ipProviders"]     = ipProviders;
            result["selectedPublicIp"] = selectedIp.Length > 0 ? selectedIp : null;
            result["metaProviders"]   = metaProviders;
            result["countryCode"]     = countryCode.Length > 0 ? countryCode : null;
            result["asn"]             = asn.Length > 0 ? asn : null;
            result["provider"]        = provider.Length > 0 ? provider : null;

            return result.ToJsonString();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts the <c>selectedPublicIp</c> string from a lookup-JSON document.
        /// Internal so tests can call it directly.
        /// </summary>
        internal static string? ParseSelectedIp(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("selectedPublicIp", out var el))
                    return el.ValueKind == JsonValueKind.Null ? null : el.GetString();
            }
            catch { }
            return null;
        }

        private static async Task<(string? value, string? error)> TryFetchAsync(
            HttpClient http, string url, CancellationToken phaseCt)
        {
            try
            {
                using var providerCts =
                    CancellationTokenSource.CreateLinkedTokenSource(phaseCt);
                providerCts.CancelAfter(TimeSpan.FromMilliseconds(DiagPerProviderTimeoutMs));

                using var req  = new HttpRequestMessage(HttpMethod.Get, url);
                using var resp = await http
                    .SendAsync(req, providerCts.Token)
                    .ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                    return (null, $"http-{(int)resp.StatusCode}");

                var body = await resp.Content
                    .ReadAsStringAsync()
                    .ConfigureAwait(false);
                return (body.Trim(), null);
            }
            catch (OperationCanceledException)
            {
                return (null, "timeout");
            }
            catch (Exception ex)
            {
                return (null, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static HttpClient BuildDiagHttpClient()
        {
            var handler = new System.Net.Http.SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.Zero,
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12),
            };
            client.DefaultRequestHeaders.CacheControl =
                new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache  = true,
                    NoStore  = true,
                };
            return client;
        }
    }
}
