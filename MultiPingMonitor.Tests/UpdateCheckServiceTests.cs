using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MultiPingMonitor.Classes;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class UpdateCheckServiceTests
    {
        [Fact]
        public async Task CheckAsync_NewerRemoteVersion_ReturnsUpdateAvailable()
        {
            using var client = ClientWithJson(Manifest("1.0.18"));
            using var service =
                new UpdateCheckService(
                    client,
                    new Uri("https://example.test/update.json"));

            UpdateCheckResult result =
                await service.CheckAsync(new Version(1, 0, 17, 0));

            Assert.Equal(
                UpdateCheckStatus.UpdateAvailable,
                result.Status);
            Assert.Equal(
                new Version(1, 0, 18),
                result.LatestVersion);
        }

        [Theory]
        [InlineData("1.0.17")]
        [InlineData("1.0.16")]
        public async Task CheckAsync_SameOrOlderRemoteVersion_ReturnsUpToDate(
            string remoteVersion)
        {
            using var client =
                ClientWithJson(Manifest(remoteVersion));
            using var service =
                new UpdateCheckService(
                    client,
                    new Uri("https://example.test/update.json"));

            UpdateCheckResult result =
                await service.CheckAsync(new Version(1, 0, 17, 0));

            Assert.Equal(
                UpdateCheckStatus.UpToDate,
                result.Status);
        }

        [Fact]
        public async Task CheckAsync_UnsupportedSchema_ReturnsInvalidManifest()
        {
            const string json =
                "{\"schemaVersion\":2,\"channel\":\"sponsor-pro\",\"latestVersion\":\"1.0.18\"}";

            using var client = ClientWithJson(json);
            using var service =
                new UpdateCheckService(
                    client,
                    new Uri("https://example.test/update.json"));

            UpdateCheckResult result =
                await service.CheckAsync(new Version(1, 0, 17));

            Assert.Equal(
                UpdateCheckStatus.InvalidManifest,
                result.Status);
        }

        [Fact]
        public async Task CheckAsync_InvalidVersion_ReturnsInvalidManifest()
        {
            const string json =
                "{\"schemaVersion\":1,\"channel\":\"sponsor-pro\",\"latestVersion\":\"invalid\"}";

            using var client = ClientWithJson(json);
            using var service =
                new UpdateCheckService(
                    client,
                    new Uri("https://example.test/update.json"));

            UpdateCheckResult result =
                await service.CheckAsync(new Version(1, 0, 17));

            Assert.Equal(
                UpdateCheckStatus.InvalidManifest,
                result.Status);
        }

        [Fact]
        public async Task CheckAsync_HttpFailure_ReturnsError()
        {
            using var client =
                new HttpClient(
                    new StubHandler(
                        new HttpResponseMessage(
                            HttpStatusCode.InternalServerError)));

            using var service =
                new UpdateCheckService(
                    client,
                    new Uri("https://example.test/update.json"));

            UpdateCheckResult result =
                await service.CheckAsync(new Version(1, 0, 17));

            Assert.Equal(
                UpdateCheckStatus.Error,
                result.Status);
        }

        [Theory]
        [InlineData("Menu_About")]
        [InlineData("About_Title")]
        [InlineData("About_CheckForUpdates")]
        [InlineData("About_OpenReleases")]
        [InlineData("About_StatusUpToDate")]
        [InlineData("About_StatusUpdateAvailable")]
        [InlineData("About_StatusCheckFailed")]
        public void Resources_ContainUpdateKeysInEnglishAndSlovak(
            string key)
        {
            Assert.True(
                HasResource(DefaultResxPath(), key),
                $"Missing EN resource key: {key}");

            Assert.True(
                HasResource(SkSkResxPath(), key),
                $"Missing SK resource key: {key}");
        }

        [Fact]
        public void MainWindow_ContainsLocalizedAboutMenu()
        {
            string source =
                File.ReadAllText(
                    Path.Combine(
                        SolutionRoot(),
                        "MultiPingMonitor",
                        "UI",
                        "MainWindow.xaml"));

            Assert.Contains("Strings.Menu_About", source);
            Assert.Contains("AboutMenu_Click", source);
        }

        [Fact]
        public void AboutWindow_UsesUpdateCheckService()
        {
            string source =
                File.ReadAllText(
                    Path.Combine(
                        SolutionRoot(),
                        "MultiPingMonitor",
                        "UI",
                        "AboutWindow.xaml.cs"));

            Assert.Contains("new UpdateCheckService()", source);
            Assert.Contains("CheckAsync", source);
            Assert.Contains("InstallUpdateButton", source);
            Assert.Contains("InstallUpdateButton_Click", source);
            Assert.Contains("UpdateCheckStatus.UpdateAvailable", source);
        }

        private static string Manifest(string version)
        {
            return
                "{\"schemaVersion\":1," +
                "\"channel\":\"sponsor-pro\"," +
                "\"latestVersion\":\"" + version + "\"," +
                "\"releaseTag\":\"multipingmonitor/v" + version + "\"," +
                "\"assetName\":\"MultiPingMonitor.zip\"," +
                "\"assetSize\":1," +
                "\"sha256\":\"00\"}";
        }

        private static HttpClient ClientWithJson(string json)
        {
            return new HttpClient(
                new StubHandler(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                    }));
        }

        private static bool HasResource(string path, string key)
        {
            XDocument doc = XDocument.Load(path);

            return doc.Root?
                .Elements("data")
                .Any(
                    element =>
                        string.Equals(
                            (string?)element.Attribute("name"),
                            key,
                            StringComparison.Ordinal))
                == true;
        }

        private static string DefaultResxPath()
        {
            return Path.Combine(
                SolutionRoot(),
                "MultiPingMonitor",
                "Properties",
                "Strings.resx");
        }

        private static string SkSkResxPath()
        {
            return Path.Combine(
                SolutionRoot(),
                "MultiPingMonitor",
                "Properties",
                "Strings.sk-SK.resx");
        }

        private static string SolutionRoot()
        {
            DirectoryInfo? directory =
                new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(
                        Path.Combine(
                            directory.FullName,
                            "MultiPingMonitor.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "MultiPingMonitor solution root was not found.");
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public StubHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }
    }
}
