#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MultiPingMonitor.Classes
{
    public sealed class SponsorProSession
    {
        public string SessionId { get; set; }
        public string SessionToken { get; set; }
        public string GithubLogin { get; set; }
        public string SponsorAccount { get; set; }
        public string SponsorTier { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }

        public bool IsUsable =>
            !string.IsNullOrWhiteSpace(SessionId)
            && !string.IsNullOrWhiteSpace(SessionToken)
            && ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(5);
    }

    public sealed class SponsorProSessionStore
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        private readonly string _filePath;

        public SponsorProSessionStore()
            : this(null)
        {
        }

        public SponsorProSessionStore(string filePath)
        {
            _filePath =
                filePath
                ?? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "MultiPingMonitor",
                    "SponsorProSession.dat");
        }

        public string FilePath => _filePath;

        public SponsorProSession Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return null;

                byte[] protectedBytes = File.ReadAllBytes(_filePath);
                byte[] jsonBytes =
                    ProtectedData.Unprotect(
                        protectedBytes,
                        null,
                        DataProtectionScope.CurrentUser);

                return JsonSerializer.Deserialize<SponsorProSession>(
                    Encoding.UTF8.GetString(jsonBytes),
                    JsonOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"SponsorProSessionStore.Load: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        public void Save(SponsorProSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(session);
            byte[] protectedBytes =
                ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(json),
                    null,
                    DataProtectionScope.CurrentUser);

            File.WriteAllBytes(_filePath, protectedBytes);
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"SponsorProSessionStore.Clear: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public sealed class SponsorProAuthStartResult
    {
        public string AuthSessionId { get; set; }
        public string PollToken { get; set; }
        public string LoginUrl { get; set; }
        public string PollUrl { get; set; }
        public int ExpiresIn { get; set; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(AuthSessionId)
            && !string.IsNullOrWhiteSpace(PollToken)
            && !string.IsNullOrWhiteSpace(LoginUrl)
            && !string.IsNullOrWhiteSpace(PollUrl);
    }

    public sealed class SponsorProLoginResult
    {
        private SponsorProLoginResult(
            bool success,
            SponsorProSession session,
            string githubLogin,
            string sponsorStatus,
            string error)
        {
            Success = success;
            Session = session;
            GithubLogin = githubLogin;
            SponsorStatus = sponsorStatus;
            Error = error;
        }

        public bool Success { get; }
        public SponsorProSession Session { get; }
        public string GithubLogin { get; }
        public string SponsorStatus { get; }
        public string Error { get; }

        public static SponsorProLoginResult SignedIn(
            SponsorProSession session,
            string sponsorStatus) =>
            new SponsorProLoginResult(
                true,
                session,
                session?.GithubLogin,
                sponsorStatus,
                null);

        public static SponsorProLoginResult Failed(
            string githubLogin,
            string sponsorStatus,
            string error) =>
            new SponsorProLoginResult(
                false,
                null,
                githubLogin,
                sponsorStatus,
                error);
    }

    public sealed class SponsorProAuthService : IDisposable
    {
        public const string DefaultBaseUrl = "https://updates.watel.cloud";
        public const string GitHubStartPath = "/v1/auth/github/start";
        public const string GitHubPollPath = "/v1/auth/github/poll";

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        private readonly HttpClient _httpClient;
        private readonly Uri _baseUri;
        private readonly bool _ownsHttpClient;

        public SponsorProAuthService()
            : this(CreateDefaultHttpClient(), new Uri(DefaultBaseUrl), true)
        {
        }

        public SponsorProAuthService(HttpClient httpClient, Uri baseUri = null)
            : this(httpClient, baseUri ?? new Uri(DefaultBaseUrl), false)
        {
        }

        private SponsorProAuthService(
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

        public async Task<SponsorProAuthStartResult> StartGitHubLoginAsync(
            CancellationToken cancellationToken = default)
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    BuildUri(GitHubStartPath));

            request.Content =
                new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "application/json");

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

            SponsorProAuthStartResult result =
                JsonSerializer.Deserialize<SponsorProAuthStartResult>(
                    json,
                    JsonOptions);

            if (result == null || !result.IsValid)
                throw new InvalidOperationException(
                    "GitHub login response is invalid.");

            return result;
        }

        public async Task<SponsorProLoginResult> PollUntilAuthenticatedAsync(
            SponsorProAuthStartResult start,
            CancellationToken cancellationToken = default)
        {
            if (start == null)
                throw new ArgumentNullException(nameof(start));
            if (!start.IsValid)
                throw new ArgumentException(
                    "GitHub login session is invalid.",
                    nameof(start));

            DateTimeOffset deadline =
                DateTimeOffset.UtcNow.AddSeconds(
                    Math.Max(30, start.ExpiresIn > 0 ? start.ExpiresIn : 300));

            while (DateTimeOffset.UtcNow < deadline)
            {
                SponsorProPollResponse poll =
                    await PollOnceAsync(start, cancellationToken)
                        .ConfigureAwait(false);

                if (string.Equals(
                        poll.Status,
                        "github_authenticated",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (poll.SessionTokenIssued
                        && poll.MpmSession != null
                        && !string.IsNullOrWhiteSpace(
                            poll.MpmSession.SessionToken))
                    {
                        return SponsorProLoginResult.SignedIn(
                            new SponsorProSession
                            {
                                SessionId = poll.MpmSession.SessionId,
                                SessionToken = poll.MpmSession.SessionToken,
                                GithubLogin = poll.GithubLogin,
                                SponsorAccount = poll.SponsorAccount,
                                SponsorTier =
                                    poll.SponsorTier
                                    ?? poll.MpmSession.SponsorTier,
                                ExpiresAtUtc =
                                    DateTimeOffset.UtcNow.AddSeconds(
                                        Math.Max(
                                            60,
                                            poll.MpmSession.TtlSeconds > 0
                                                ? poll.MpmSession.TtlSeconds
                                                : 2592000))
                            },
                            poll.SponsorStatus);
                    }

                    return SponsorProLoginResult.Failed(
                        poll.GithubLogin,
                        poll.SponsorStatus,
                        poll.Error ?? "Sponsor Pro entitlement is not active.");
                }

                if (string.Equals(
                        poll.Status,
                        "error",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        poll.Status,
                        "github_auth_failed",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return SponsorProLoginResult.Failed(
                        poll.GithubLogin,
                        poll.SponsorStatus,
                        poll.Error ?? "GitHub login failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken)
                    .ConfigureAwait(false);
            }

            return SponsorProLoginResult.Failed(
                null,
                null,
                "GitHub login timed out.");
        }

        private async Task<SponsorProPollResponse> PollOnceAsync(
            SponsorProAuthStartResult start,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                authSessionId = start.AuthSessionId,
                pollToken = start.PollToken
            };

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    BuildUri(start.PollUrl));

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

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

            SponsorProPollResponse poll =
                JsonSerializer.Deserialize<SponsorProPollResponse>(
                    json,
                    JsonOptions);

            return poll ?? new SponsorProPollResponse
            {
                Status = "error",
                Error = "Empty GitHub login poll response."
            };
        }

        private Uri BuildUri(string relativeOrAbsolute)
        {
            if (Uri.TryCreate(
                    relativeOrAbsolute,
                    UriKind.Absolute,
                    out Uri absolute))
            {
                return absolute;
            }

            return new Uri(_baseUri, relativeOrAbsolute);
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MultiPingMonitor-SponsorProAuth/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd(
                "application/json");

            return client;
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
        }

        private sealed class SponsorProPollResponse
        {
            public string Status { get; set; }
            public string Error { get; set; }
            public string GithubLogin { get; set; }
            public string SponsorAccount { get; set; }
            public string SponsorStatus { get; set; }
            public string SponsorTier { get; set; }
            public bool SessionTokenIssued { get; set; }
            public SponsorProMpmSession MpmSession { get; set; }
        }

        private sealed class SponsorProMpmSession
        {
            public string SessionId { get; set; }
            public string SessionToken { get; set; }
            public string TokenType { get; set; }
            public string SponsorTier { get; set; }
            public int TtlSeconds { get; set; }
        }
    }
}
