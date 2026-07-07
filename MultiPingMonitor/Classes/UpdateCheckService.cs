#nullable disable
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MultiPingMonitor.Classes
{
    public enum UpdateCheckStatus
    {
        UpToDate,
        UpdateAvailable,
        InvalidManifest,
        Error
    }

    public sealed class UpdateManifest
    {
        public int SchemaVersion { get; set; }
        public string Channel { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseTag { get; set; }
        public string AssetName { get; set; }
        public long AssetSize { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class UpdateCheckResult
    {
        private UpdateCheckResult(
            UpdateCheckStatus status,
            Version latestVersion,
            string error)
        {
            Status = status;
            LatestVersion = latestVersion;
            Error = error;
        }

        public UpdateCheckStatus Status { get; }
        public Version LatestVersion { get; }
        public string Error { get; }

        public static UpdateCheckResult UpToDate(Version latestVersion) =>
            new UpdateCheckResult(UpdateCheckStatus.UpToDate, latestVersion, null);

        public static UpdateCheckResult UpdateAvailable(Version latestVersion) =>
            new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, latestVersion, null);

        public static UpdateCheckResult InvalidManifest(string error) =>
            new UpdateCheckResult(UpdateCheckStatus.InvalidManifest, null, error);

        public static UpdateCheckResult Failed(string error) =>
            new UpdateCheckResult(UpdateCheckStatus.Error, null, error);
    }

    public sealed class UpdateCheckService : IDisposable
    {
        public const string SponsorProChannel = "sponsor-pro";
        public const int SupportedSchemaVersion = 1;
        public const string DefaultManifestUrl =
            "https://raw.githubusercontent.com/Vaso73/MultiPingMonitor/main/updates/sponsor-pro.json";

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        private readonly HttpClient _httpClient;
        private readonly Uri _manifestUri;
        private readonly bool _ownsHttpClient;

        public UpdateCheckService()
            : this(CreateDefaultHttpClient(), new Uri(DefaultManifestUrl), true)
        {
        }

        public UpdateCheckService(HttpClient httpClient, Uri manifestUri = null)
            : this(httpClient, manifestUri ?? new Uri(DefaultManifestUrl), false)
        {
        }

        private UpdateCheckService(
            HttpClient httpClient,
            Uri manifestUri,
            bool ownsHttpClient)
        {
            _httpClient =
                httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _manifestUri =
                manifestUri ?? throw new ArgumentNullException(nameof(manifestUri));
            _ownsHttpClient = ownsHttpClient;
        }

        public async Task<UpdateCheckResult> CheckAsync(
            Version currentVersion,
            CancellationToken cancellationToken = default)
        {
            if (currentVersion == null)
                throw new ArgumentNullException(nameof(currentVersion));

            try
            {
                using var request =
                    new HttpRequestMessage(HttpMethod.Get, _manifestUri);

                request.Headers.CacheControl =
                    new System.Net.Http.Headers.CacheControlHeaderValue
                    {
                        NoCache = true,
                        NoStore = true
                    };

                using HttpResponseMessage response =
                    await _httpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken)
                        .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                string json =
                    await response.Content
                        .ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);

                UpdateManifest manifest =
                    JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);

                if (manifest == null)
                    return UpdateCheckResult.InvalidManifest("Manifest is empty.");

                if (manifest.SchemaVersion != SupportedSchemaVersion)
                {
                    return UpdateCheckResult.InvalidManifest(
                        "Unsupported schema version.");
                }

                if (!string.Equals(
                        manifest.Channel,
                        SponsorProChannel,
                        StringComparison.Ordinal))
                {
                    return UpdateCheckResult.InvalidManifest(
                        "Unexpected update channel.");
                }

                if (!Version.TryParse(
                        manifest.LatestVersion,
                        out Version latestVersion))
                {
                    return UpdateCheckResult.InvalidManifest(
                        "Invalid latestVersion.");
                }

                Version normalizedCurrent = Normalize(currentVersion);
                Version normalizedLatest = Normalize(latestVersion);

                return normalizedLatest > normalizedCurrent
                    ? UpdateCheckResult.UpdateAvailable(latestVersion)
                    : UpdateCheckResult.UpToDate(latestVersion);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"UpdateCheckService: {ex.GetType().Name}: {ex.Message}");

                return UpdateCheckResult.Failed(ex.Message);
            }
        }

        private static Version Normalize(Version version)
        {
            int build = version.Build < 0 ? 0 : version.Build;
            return new Version(version.Major, version.Minor, build);
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MultiPingMonitor-UpdateCheck/1.0");

            client.DefaultRequestHeaders.Accept.ParseAdd(
                "application/json");

            return client;
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
        }
    }
}
