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
            UpdateManifest manifest,
            string error)
        {
            Status = status;
            LatestVersion = latestVersion;
            Manifest = manifest;
            Error = error;
        }

        public UpdateCheckStatus Status { get; }
        public Version LatestVersion { get; }
        public UpdateManifest Manifest { get; }
        public string Error { get; }

        public static UpdateCheckResult UpToDate(
            Version latestVersion,
            UpdateManifest manifest) =>
            new UpdateCheckResult(
                UpdateCheckStatus.UpToDate,
                latestVersion,
                manifest,
                null);

        public static UpdateCheckResult UpdateAvailable(
            Version latestVersion,
            UpdateManifest manifest) =>
            new UpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                latestVersion,
                manifest,
                null);

        public static UpdateCheckResult InvalidManifest(string error) =>
            new UpdateCheckResult(
                UpdateCheckStatus.InvalidManifest,
                null,
                null,
                error);

        public static UpdateCheckResult Failed(string error) =>
            new UpdateCheckResult(UpdateCheckStatus.Error, null, null, error);
    }

    public sealed class UpdateCheckService : IDisposable
    {
        public const string SponsorProChannel = "sponsor-pro";
        public const int SupportedSchemaVersion = 1;
        public const string RequiredAssetName = "MultiPingMonitor.zip";
        public const string DefaultManifestUrl =
            "https://updates.watel.cloud/v1/update/latest";

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

                UpdateManifest manifest = ParseManifest(json);

                if (manifest == null)
                    return UpdateCheckResult.InvalidManifest("Manifest is empty.");

                string validationError = ValidateManifest(manifest);
                if (validationError != null)
                    return UpdateCheckResult.InvalidManifest(validationError);

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
                    ? UpdateCheckResult.UpdateAvailable(latestVersion, manifest)
                    : UpdateCheckResult.UpToDate(latestVersion, manifest);
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

        private static UpdateManifest ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("asset", out JsonElement asset))
            {
                return new UpdateManifest
                {
                    SchemaVersion = SupportedSchemaVersion,
                    Channel = GetString(root, "channel"),
                    LatestVersion = GetString(root, "latestVersion"),
                    ReleaseTag = GetString(root, "tagName"),
                    AssetName = GetString(asset, "name"),
                    AssetSize = GetInt64(asset, "size"),
                    Sha256 = GetString(asset, "sha256")
                };
            }

            return JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
        }

        public static string ValidateManifest(UpdateManifest manifest)
        {
            if (manifest == null)
                return "Manifest is empty.";

            if (manifest.SchemaVersion != SupportedSchemaVersion)
                return "Unsupported schema version.";

            if (!string.Equals(
                    manifest.Channel,
                    SponsorProChannel,
                    StringComparison.Ordinal))
            {
                return "Unexpected update channel.";
            }

            if (!Version.TryParse(manifest.LatestVersion, out Version latest))
                return "Invalid latestVersion.";

            string expectedTag = $"multipingmonitor/v{Normalize(latest)}";
            if (!string.Equals(
                    manifest.ReleaseTag,
                    expectedTag,
                    StringComparison.Ordinal))
            {
                return "Unexpected releaseTag.";
            }

            if (!string.Equals(
                    manifest.AssetName,
                    RequiredAssetName,
                    StringComparison.Ordinal))
            {
                return "Unexpected assetName.";
            }

            if (manifest.AssetSize <= 0)
                return "Invalid assetSize.";

            if (!IsSha256(manifest.Sha256))
                return "Invalid sha256.";

            return null;
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : null;
        }

        private static long GetInt64(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
                return 0;

            if (property.ValueKind == JsonValueKind.Number
                && property.TryGetInt64(out long value))
            {
                return value;
            }

            return 0;
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                return false;

            foreach (char ch in value)
            {
                bool hex =
                    (ch >= '0' && ch <= '9')
                    || (ch >= 'a' && ch <= 'f')
                    || (ch >= 'A' && ch <= 'F');
                if (!hex)
                    return false;
            }

            return true;
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
