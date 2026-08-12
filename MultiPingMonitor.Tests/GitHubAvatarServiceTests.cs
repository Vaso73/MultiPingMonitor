using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MultiPingMonitor.Classes;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class GitHubAvatarServiceTests
    {
        [Fact]
        public async Task DownloadAsync_UsesOfficialProfileAvatarUrl()
        {
            var requests = new List<Uri>();
            using var service = new GitHubAvatarService(
                new HttpClient(new DelegateHandler(request =>
                {
                    Uri requestUri = request.RequestUri
                        ?? throw new InvalidOperationException("Missing request URI.");
                    requests.Add(requestUri);
                    if (requestUri.Host == "api.github.com")
                        return JsonResponse(
                            "{\"avatar_url\":\"https://avatars.githubusercontent.com/u/583231?v=4\"}");
                    return ImageResponse(new byte[] { 1, 2, 3, 4 });
                })));

            byte[] result = await service.DownloadAsync("octocat");

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, result);
            Assert.Equal(
                "https://api.github.com/users/octocat",
                requests[0].AbsoluteUri);
            Assert.Equal("avatars.githubusercontent.com", requests[1].Host);
        }

        [Theory]
        [InlineData("bad login")]
        [InlineData("-leading")]
        [InlineData("trailing-")]
        [InlineData("double--dash")]
        public async Task DownloadAsync_RejectsInvalidLoginWithoutRequest(
            string login)
        {
            int requests = 0;
            using var service = new GitHubAvatarService(
                new HttpClient(new DelegateHandler(_ =>
                {
                    requests++;
                    return JsonResponse("{}");
                })));

            Assert.Null(await service.DownloadAsync(login));
            Assert.Equal(0, requests);
        }

        [Fact]
        public async Task DownloadAsync_RejectsAvatarOutsideGitHubHost()
        {
            int requests = 0;
            using var service = new GitHubAvatarService(
                new HttpClient(new DelegateHandler(_ =>
                {
                    requests++;
                    return JsonResponse(
                        "{\"avatar_url\":\"https://example.com/avatar.png\"}");
                })));

            Assert.Null(await service.DownloadAsync("octocat"));
            Assert.Equal(1, requests);
        }

        [Fact]
        public async Task DownloadAsync_RejectsOversizedImage()
        {
            using var service = new GitHubAvatarService(
                new HttpClient(new DelegateHandler(request =>
                {
                    if (request.RequestUri?.Host == "api.github.com")
                        return JsonResponse(
                            "{\"avatar_url\":\"https://avatars.githubusercontent.com/u/583231?v=4\"}");
                    return ImageResponse(
                        new byte[GitHubAvatarService.MaximumAvatarBytes + 1]);
                })));

            Assert.Null(await service.DownloadAsync("octocat"));
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage ImageResponse(byte[] bytes)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return response;
        }

        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public DelegateHandler(
                Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }
    }
}
