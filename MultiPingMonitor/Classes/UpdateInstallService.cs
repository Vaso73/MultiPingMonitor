#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MultiPingMonitor.Classes
{
    public enum UpdateInstallStatus
    {
        HelperStarted,
        AuthenticationRequired,
        InvalidManifest,
        DownloadFailed,
        InvalidPackage,
        Error
    }

    public sealed class UpdateInstallResult
    {
        private UpdateInstallResult(
            UpdateInstallStatus status,
            string error)
        {
            Status = status;
            Error = error;
        }

        public UpdateInstallStatus Status { get; }
        public string Error { get; }

        public static UpdateInstallResult HelperStarted() =>
            new UpdateInstallResult(UpdateInstallStatus.HelperStarted, null);

        public static UpdateInstallResult Failed(
            UpdateInstallStatus status,
            string error) =>
            new UpdateInstallResult(status, error);
    }

    public sealed class UpdateInstallService : IDisposable
    {
        public const string DefaultBaseUrl = "https://updates.watel.cloud";
        public const string DownloadTokenPath = "/v1/update/download-token";
        public const string HelperModeArgument = "--mpm-apply-update";
        private const string ExeName = "MultiPingMonitor.exe";

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        private readonly HttpClient _httpClient;
        private readonly Uri _baseUri;
        private readonly bool _ownsHttpClient;

        public UpdateInstallService()
            : this(CreateDefaultHttpClient(), new Uri(DefaultBaseUrl), true)
        {
        }

        public UpdateInstallService(HttpClient httpClient, Uri baseUri = null)
            : this(httpClient, baseUri ?? new Uri(DefaultBaseUrl), false)
        {
        }

        private UpdateInstallService(
            HttpClient httpClient,
            Uri baseUri,
            bool ownsHttpClient)
        {
            _httpClient =
                httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUri =
                baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            _ownsHttpClient = ownsHttpClient;
        }

        public async Task<UpdateInstallResult> InstallAsync(
            UpdateManifest manifest,
            SponsorProSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null || !session.IsUsable)
            {
                return UpdateInstallResult.Failed(
                    UpdateInstallStatus.AuthenticationRequired,
                    "Sponsor Pro session is missing or expired.");
            }

            string manifestError = UpdateCheckService.ValidateManifest(manifest);
            if (manifestError != null)
            {
                return UpdateInstallResult.Failed(
                    UpdateInstallStatus.InvalidManifest,
                    manifestError);
            }

            try
            {
                string downloadUrl =
                    await RequestDownloadUrlAsync(session, cancellationToken)
                        .ConfigureAwait(false);

                byte[] zipBytes =
                    await DownloadZipAsync(downloadUrl, cancellationToken)
                        .ConfigureAwait(false);

                string updateRoot = CreateUpdateRoot(manifest);
                string zipPath = Path.Combine(updateRoot, manifest.AssetName);
                string stagedExePath = Path.Combine(updateRoot, ExeName);

                File.WriteAllBytes(zipPath, zipBytes);
                ValidateAndExtractPackage(zipBytes, manifest, stagedExePath);

                string currentExePath = ResolveCurrentExecutablePath();
                if (!File.Exists(currentExePath))
                    throw new InvalidOperationException(
                        "Current executable path could not be resolved.");

                if (!string.Equals(
                        Path.GetFileName(currentExePath),
                        ExeName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Current executable is not MultiPingMonitor.exe.");
                }

                string helperPath =
                    Path.Combine(updateRoot, "MultiPingMonitor-update-helper.exe");
                File.Copy(currentExePath, helperPath, true);

                using Process currentProcess = Process.GetCurrentProcess();
                var startInfo = new ProcessStartInfo(helperPath)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(currentExePath)
                };

                startInfo.ArgumentList.Add(HelperModeArgument);
                startInfo.ArgumentList.Add(currentExePath);
                startInfo.ArgumentList.Add(stagedExePath);
                startInfo.ArgumentList.Add(
                    currentProcess.Id.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));

                WritePendingSuccessMarker(manifest);

                Process helper = Process.Start(startInfo);
                if (helper == null)
                    throw new InvalidOperationException(
                        "Update helper could not be started.");

                return UpdateInstallResult.HelperStarted();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (InvalidDataException ex)
            {
                Debug.WriteLine(
                    $"UpdateInstallService package: {ex.GetType().Name}: {ex.Message}");
                return UpdateInstallResult.Failed(
                    UpdateInstallStatus.InvalidPackage,
                    ex.Message);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(
                    $"UpdateInstallService download: {ex.GetType().Name}: {ex.Message}");
                return UpdateInstallResult.Failed(
                    UpdateInstallStatus.DownloadFailed,
                    ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"UpdateInstallService: {ex.GetType().Name}: {ex.Message}");
                return UpdateInstallResult.Failed(
                    UpdateInstallStatus.Error,
                    ex.Message);
            }
        }

        public static int RunApplyUpdateHelper(string[] args)
        {
            try
            {
                if (args == null || args.Length < 5)
                    return 2;

                string targetExePath = Path.GetFullPath(args[2]);
                string stagedExePath = Path.GetFullPath(args[3]);

                if (!int.TryParse(args[4], out int parentProcessId))
                    return 3;

                if (!string.Equals(
                        Path.GetFileName(targetExePath),
                        ExeName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return 4;
                }

                if (!File.Exists(stagedExePath))
                    return 5;

                WaitForParentExit(parentProcessId);

                string appDirectory =
                    Path.GetDirectoryName(targetExePath)
                    ?? AppContext.BaseDirectory;
                string updateRoot =
                    Path.GetDirectoryName(stagedExePath)
                    ?? Path.GetTempPath();

                ApplyStagedExecutableWithTempSwap(
                    stagedExePath,
                    targetExePath,
                    updateRoot);

                Process.Start(
                    new ProcessStartInfo(targetExePath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = appDirectory
                    });

                CleanupUpdateRootLater(updateRoot);

                  return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(Path.GetTempPath(), "MultiPingMonitor-update.log"),
                        DateTime.UtcNow.ToString("O")
                        + " "
                        + ex.GetType().Name
                        + ": "
                        + ex.Message
                        + Environment.NewLine);
                }
                catch { }

                return 1;
            }
        }

        public static void WritePendingSuccessMarker(UpdateManifest manifest)
        {
            try
            {
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.LatestVersion))
                    return;

                string markerPath = GetUpdateSuccessMarkerPath();
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));

                var marker = new PendingUpdateSuccessMarker
                {
                    LatestVersion = manifest.LatestVersion,
                    WrittenAtUtc = DateTime.UtcNow
                };

                File.WriteAllText(
                    markerPath,
                    JsonSerializer.Serialize(marker, JsonOptions),
                    Encoding.UTF8);
            }
            catch
            {
                // Success UX marker is non-critical; never block update installation.
            }
        }

        public static string ConsumeCompletedUpdateSuccessVersion(Version currentVersion)
        {
            string markerPath = GetUpdateSuccessMarkerPath();

            try
            {
                if (!File.Exists(markerPath))
                    return null;

                string json = File.ReadAllText(markerPath, Encoding.UTF8);
                PendingUpdateSuccessMarker marker =
                    JsonSerializer.Deserialize<PendingUpdateSuccessMarker>(
                        json,
                        JsonOptions);

                if (marker == null
                    || string.IsNullOrWhiteSpace(marker.LatestVersion)
                    || !Version.TryParse(marker.LatestVersion, out Version targetVersion))
                {
                    DeleteUpdateSuccessMarker(markerPath);
                    return null;
                }

                if (marker.WrittenAtUtc != default
                    && DateTime.UtcNow - marker.WrittenAtUtc > TimeSpan.FromDays(7))
                {
                    DeleteUpdateSuccessMarker(markerPath);
                    return null;
                }

                if (currentVersion != null
                    && NormalizeVersion(currentVersion).CompareTo(NormalizeVersion(targetVersion)) >= 0)
                {
                    string completedVersion = marker.LatestVersion;
                    DeleteUpdateSuccessMarker(markerPath);
                    return completedVersion;
                }
            }
            catch
            {
                DeleteUpdateSuccessMarker(markerPath);
            }

            return null;
        }

        private static Version NormalizeVersion(Version version)
        {
            return new Version(
                version.Major,
                version.Minor,
                Math.Max(version.Build, 0),
                Math.Max(version.Revision, 0));
        }

        private static string GetUpdateSuccessMarkerPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "MultiPingMonitor",
                "post-update-success.json");
        }

        private static void DeleteUpdateSuccessMarker(string markerPath)
        {
            try
            {
                if (File.Exists(markerPath))
                    File.Delete(markerPath);
            }
            catch { }

            CleanupEmptyUpdateTempDirectories();
        }

        private static void CleanupEmptyUpdateTempDirectories()
        {
            try
            {
                string root =
                    Path.Combine(
                        Path.GetTempPath(),
                        "MultiPingMonitor");

                string updatesRoot = Path.Combine(root, "updates");

                if (Directory.Exists(updatesRoot)
                    && !Directory.EnumerateFileSystemEntries(updatesRoot).Any())
                {
                    Directory.Delete(updatesRoot);
                }

                if (Directory.Exists(root)
                    && !Directory.EnumerateFileSystemEntries(root).Any())
                {
                    Directory.Delete(root);
                }
            }
            catch { }
        }

        private async Task<string> RequestDownloadUrlAsync(
            SponsorProSession session,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                sessionId = session.SessionId,
                sessionToken = session.SessionToken
            };

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    new Uri(_baseUri, DownloadTokenPath));

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", session.SessionToken);
            request.Content =
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

            string json =
                await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Download token request failed: {(int)response.StatusCode}");

            DownloadTokenResponse result =
                JsonSerializer.Deserialize<DownloadTokenResponse>(
                    json,
                    JsonOptions);

            if (result == null
                || !string.Equals(
                    result.Status,
                    "ok",
                    StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(result.DownloadUrl))
            {
                throw new InvalidOperationException(
                    "Download token response is invalid.");
            }

            return result.DownloadUrl;
        }

        private async Task<byte[]> DownloadZipAsync(
            string downloadUrl,
            CancellationToken cancellationToken)
        {
            Uri uri = Uri.TryCreate(downloadUrl, UriKind.Absolute, out Uri absolute)
                ? absolute
                : new Uri(_baseUri, downloadUrl);

            using var request =
                new HttpRequestMessage(HttpMethod.Get, uri);

            using HttpResponseMessage response =
                await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        private static void ValidateAndExtractPackage(
            byte[] zipBytes,
            UpdateManifest manifest,
            string stagedExePath)
        {
            if (zipBytes == null || zipBytes.Length == 0)
                throw new InvalidDataException("Update package is empty.");

            if (zipBytes.LongLength != manifest.AssetSize)
                throw new InvalidDataException("Update package size mismatch.");

            string actualSha;
            using (SHA256 sha256 = SHA256.Create())
            {
                actualSha =
                    BitConverter.ToString(
                            sha256.ComputeHash(zipBytes))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
            }

            if (!string.Equals(
                    actualSha,
                    manifest.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Update package SHA-256 mismatch.");
            }

            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            if (archive.Entries.Count != 1
                || !string.Equals(
                    archive.Entries[0].FullName,
                    ExeName,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Update package must contain exactly MultiPingMonitor.exe.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(stagedExePath));
            using Stream input = archive.Entries[0].Open();
            using FileStream output =
                new FileStream(
                    stagedExePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);
            input.CopyTo(output);

            if (!File.Exists(stagedExePath)
                || new FileInfo(stagedExePath).Length <= 0)
            {
                throw new InvalidDataException(
                    "Extracted update executable is invalid.");
            }
        }

        private static string CreateUpdateRoot(UpdateManifest manifest)
        {
            string version = string.IsNullOrWhiteSpace(manifest.LatestVersion)
                ? "unknown"
                : manifest.LatestVersion;

            string root =
                Path.Combine(
                    Path.GetTempPath(),
                    "MultiPingMonitor",
                    "updates",
                    version + "-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(root);
            return root;
        }

        private static string ResolveCurrentExecutablePath()
        {
            string processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath)
                && File.Exists(processPath))
            {
                return processPath;
            }

            try
            {
                string modulePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(modulePath)
                    && File.Exists(modulePath))
                {
                    return modulePath;
                }
            }
            catch { }

            return Path.Combine(AppContext.BaseDirectory, ExeName);
        }

        private static void WaitForParentExit(int parentProcessId)
        {
            try
            {
                using Process parent = Process.GetProcessById(parentProcessId);
                parent.WaitForExit(60000);
            }
            catch
            {
                // Parent process is already gone or inaccessible.
            }
        }

        private static void ApplyStagedExecutableWithTempSwap(
            string stagedExePath,
            string targetExePath,
            string updateRoot)
        {
            Directory.CreateDirectory(updateRoot);

            string swapOldPath =
                Path.Combine(updateRoot, ExeName + ".old");

            DeleteFileWithRetry(swapOldPath);
            MoveWithRetry(targetExePath, swapOldPath);

            try
            {
                CopyWithRetry(stagedExePath, targetExePath);
                DeleteFileWithRetry(swapOldPath);
            }
            catch
            {
                try
                {
                    DeleteFileWithRetry(targetExePath);
                    if (File.Exists(swapOldPath))
                    {
                        MoveWithRetry(swapOldPath, targetExePath);
                    }
                }
                catch { }

                throw;
            }
        }

        private static void CopyWithRetry(
            string sourcePath,
            string destinationPath)
        {
            Exception last = null;

            for (int attempt = 0; attempt < 80; attempt++)
            {
                try
                {
                    File.Copy(sourcePath, destinationPath, true);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Thread.Sleep(250);
                }
            }

            throw new IOException(
                "Could not replace the running executable.",
                last);
        }

        private static void MoveWithRetry(
            string sourcePath,
            string destinationPath)
        {
            Exception last = null;

            for (int attempt = 0; attempt < 80; attempt++)
            {
                try
                {
                    File.Move(sourcePath, destinationPath, true);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Thread.Sleep(250);
                }
            }

            throw new IOException(
                "Could not move executable during update.",
                last);
        }

        private static void DeleteFileWithRetry(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            Exception last = null;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Thread.Sleep(150);
                }
            }

            if (File.Exists(path))
            {
                throw new IOException(
                    "Could not remove temporary update file.",
                    last);
            }
        }

        private static void CleanupUpdateRootLater(string updateRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(updateRoot))
                    return;

                string fullUpdateRoot = Path.GetFullPath(updateRoot);
                string allowedRoot =
                    Path.GetFullPath(
                        Path.Combine(
                            Path.GetTempPath(),
                            "MultiPingMonitor",
                            "updates"));

                if (!fullUpdateRoot.StartsWith(
                        allowedRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string safePath =
                    fullUpdateRoot.Replace("\"", string.Empty);

                Process.Start(
                    new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Arguments =
                            "/C timeout /T 2 /NOBREAK > NUL & rmdir /S /Q \""
                            + safePath
                            + "\""
                    });
            }
            catch { }
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MultiPingMonitor-UpdateInstall/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd(
                "application/json");

            return client;
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
        }

        private sealed class DownloadTokenResponse
        {
            public string Status { get; set; }
            public string Error { get; set; }
            public string DownloadToken { get; set; }
            public string DownloadUrl { get; set; }
            public int ExpiresIn { get; set; }
        }

        private sealed class PendingUpdateSuccessMarker
        {
            public string LatestVersion { get; set; }
            public DateTime WrittenAtUtc { get; set; }
        }
    }
}
