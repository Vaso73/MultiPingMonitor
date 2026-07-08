#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
                string backupRoot =
                    Path.Combine(appDirectory, "backup");
                string backupDirectory =
                    Path.Combine(
                        backupRoot,
                        "update-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

                Directory.CreateDirectory(backupDirectory);
                CopyDirectoryExcludingBackup(
                    appDirectory,
                    backupDirectory,
                    backupRoot);

                CopyWithRetry(stagedExePath, targetExePath);

                Process.Start(
                    new ProcessStartInfo(targetExePath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = appDirectory
                    });

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

        private static void CopyDirectoryExcludingBackup(
            string sourceDirectory,
            string destinationDirectory,
            string backupRoot)
        {
            string normalizedBackupRoot =
                Path.GetFullPath(backupRoot)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            foreach (string directory in Directory.EnumerateDirectories(
                         sourceDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                string fullDirectory =
                    Path.GetFullPath(directory)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (fullDirectory.StartsWith(
                        normalizedBackupRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
            }

            foreach (string file in Directory.EnumerateFiles(
                         sourceDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                string fullFile = Path.GetFullPath(file);
                if (fullFile.StartsWith(
                        normalizedBackupRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = Path.GetRelativePath(sourceDirectory, file);
                string destination = Path.Combine(destinationDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
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
    }
}
