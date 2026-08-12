#nullable disable
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MultiPingMonitor.Classes
{
    public sealed class GitHubAvatarService : IDisposable
    {
        public const int MaximumAvatarBytes = 1_048_576;
        private static readonly Uri ApiBaseUri =
            new Uri("https://api.github.com/");
        private static readonly Regex LoginPattern = new Regex(
            "^(?!-)(?!.*--)[A-Za-z0-9-]{1,39}(?<!-)$",
            RegexOptions.CultureInvariant);

        private readonly HttpClient _client;
        private readonly bool _ownsClient;

        public GitHubAvatarService()
            : this(CreateClient(), true) { }

        public GitHubAvatarService(HttpClient client)
            : this(client, false) { }

        private GitHubAvatarService(HttpClient client, bool ownsClient)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _ownsClient = ownsClient;
        }

        public async Task<byte[]> DownloadAsync(
            string githubLogin,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(githubLogin)
                || !LoginPattern.IsMatch(githubLogin))
                return null;

            try
            {
                using var profileRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(
                        ApiBaseUri,
                        $"users/{Uri.EscapeDataString(githubLogin)}"));
                profileRequest.Headers.Accept.Add(
                    new MediaTypeWithQualityHeaderValue(
                        "application/vnd.github+json"));
                profileRequest.Headers.Add(
                    "X-GitHub-Api-Version",
                    "2022-11-28");

                using HttpResponseMessage profileResponse =
                    await _client.SendAsync(
                        profileRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);
                if (!profileResponse.IsSuccessStatusCode
                    || profileResponse.Content.Headers.ContentLength
                        > MaximumAvatarBytes)
                    return null;

                byte[] profileBytes = await ReadBoundedAsync(
                    profileResponse,
                    MaximumAvatarBytes,
                    cancellationToken).ConfigureAwait(false);
                using JsonDocument profile =
                    JsonDocument.Parse(profileBytes);
                if (!profile.RootElement.TryGetProperty(
                        "avatar_url",
                        out JsonElement avatarValue)
                    || avatarValue.ValueKind != JsonValueKind.String
                    || !TryValidateAvatarUri(
                        avatarValue.GetString(),
                        out Uri avatarUri))
                    return null;

                using var avatarRequest =
                    new HttpRequestMessage(HttpMethod.Get, avatarUri);
                avatarRequest.Headers.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("image/png"));
                avatarRequest.Headers.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("image/jpeg"));
                using HttpResponseMessage avatarResponse =
                    await _client.SendAsync(
                        avatarRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);
                long? contentLength =
                    avatarResponse.Content.Headers.ContentLength;
                string mediaType =
                    avatarResponse.Content.Headers.ContentType?.MediaType;
                if (!avatarResponse.IsSuccessStatusCode
                    || contentLength > MaximumAvatarBytes
                    || contentLength <= 0
                    || (mediaType != "image/png" && mediaType != "image/jpeg"))
                    return null;

                byte[] avatarBytes = await ReadBoundedAsync(
                    avatarResponse,
                    MaximumAvatarBytes,
                    cancellationToken).ConfigureAwait(false);
                return avatarBytes.Length == 0 ? null : avatarBytes;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException) { return null; }
            catch (JsonException) { return null; }
            catch (InvalidDataException) { return null; }
        }

        private static bool TryValidateAvatarUri(
            string value,
            out Uri avatarUri)
        {
            bool valid = Uri.TryCreate(value, UriKind.Absolute, out Uri parsed)
                && parsed.Scheme == Uri.UriSchemeHttps
                && parsed.Host.Equals(
                    "avatars.githubusercontent.com",
                    StringComparison.OrdinalIgnoreCase)
                && parsed.IsDefaultPort
                && string.IsNullOrEmpty(parsed.UserInfo)
                && string.IsNullOrEmpty(parsed.Fragment);
            avatarUri = valid ? parsed : null;
            return valid;
        }

        private static async Task<byte[]> ReadBoundedAsync(
            HttpResponseMessage response,
            int maximumBytes,
            CancellationToken cancellationToken)
        {
            using Stream input =
                await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[32_768];
            while (true)
            {
                int read = await input.ReadAsync(
                    buffer,
                    0,
                    buffer.Length,
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (output.Length + read > maximumBytes)
                    throw new InvalidDataException("response_too_large");
                await output.WriteAsync(
                    buffer,
                    0,
                    read,
                    cancellationToken).ConfigureAwait(false);
            }
            return output.ToArray();
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            })
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MultiPingMonitor-SponsorPro/1.0");
            return client;
        }

        public void Dispose()
        {
            if (_ownsClient)
                _client.Dispose();
        }
    }
}
