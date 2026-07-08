using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class SponsorProLoginRegistrationTests
    {
        [Fact]
        public void AboutWindow_HasGitHubSponsorProLoginButton()
        {
            string xaml = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "AboutWindow.xaml"));

            Assert.Contains("SponsorProLoginButton", xaml);
            Assert.Contains("SponsorProLoginButton_Click", xaml);
        }

        [Fact]
        public void AboutWindow_UsesSponsorProAuthServiceAndSessionStore()
        {
            string source = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "AboutWindow.xaml.cs"));

            Assert.Contains("SponsorProAuthService", source);
            Assert.Contains("SponsorProSessionStore", source);
            Assert.Contains("StartGitHubLoginAsync", source);
            Assert.Contains("PollUntilAuthenticatedAsync", source);
            Assert.Contains("ProcessStartInfo(start.LoginUrl)", source);
        }


        [Fact]
        public void AboutWindow_HidesGitHubLoginButtonWhenSessionIsUsable()
        {
            string source = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "AboutWindow.xaml.cs"));

            Assert.Contains("SponsorProLoginButton.Visibility =", source);
            Assert.Contains("Visibility.Collapsed", source);
            Assert.Contains("Visibility.Visible", source);
            Assert.DoesNotContain("About_SponsorProSignInAgain", source);
        }


        [Fact]
        public void AboutMenus_ExistInMainCompactAndTrayWithSharedIcon()
        {
            string xaml = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "MainWindow.xaml"));
            string source = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "MainWindow.xaml.cs"));

            Assert.Contains("geom.menu.about", xaml);
            Assert.Contains("x:Name=\"AboutMenu\"", xaml);
            Assert.Contains("compactAboutItem", source);
            Assert.Contains("Strings.Menu_About", source);
            Assert.Contains("geom.menu.about", source);
            Assert.Contains("MakeItem(Strings.Menu_About", source);
            Assert.Contains("\"geom.menu.about\"", source);
            Assert.Contains("size * 0.82", source);
        }

        [Fact]
        public void AboutWindow_UsesAccountSectionAndUpdateFooter()
        {
            string xaml = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "AboutWindow.xaml"));
            string source = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "AboutWindow.xaml.cs"));

            Assert.Contains("AccountPanel", xaml);
            Assert.Contains("AccountGitHubIcon", xaml);
            Assert.Contains("geom.account.github", xaml);
            Assert.Contains("Fill=\"#FF000000\"", xaml);
            Assert.Contains("Canvas Width=\"16\"", xaml);
            Assert.Contains("AccountTitleText", xaml);
            Assert.Contains("AccountStatusText", xaml);
            Assert.Contains("InstallUpdateButton", xaml);
            Assert.Contains("About_AccountSignedInTitle", source);
            Assert.Contains("About_AccountSponsorActive", source);
            Assert.Contains("InstallUpdateButton.IsEnabled = false", source);
        }


        [Fact]
        public void AboutWindow_GuardsOwnerWhenOpenedFromTrayBeforeMainWindowShown()
        {
            string source = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "UI",
                "MainWindow.xaml.cs"));

            Assert.Contains("if (IsLoaded)", source);
            Assert.Contains("aboutWindow.Owner = this", source);
            Assert.Contains("WindowStartupLocation.CenterOwner", source);
            Assert.Contains("WindowStartupLocation.CenterScreen", source);
        }

        [Fact]
        public void SponsorProAuthService_UsesBackendOAuthEndpoints()
        {
            string source = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "Classes",
                "SponsorProAuthService.cs"));

            Assert.Contains("https://updates.watel.cloud", source);
            Assert.Contains("/v1/auth/github/start", source);
            Assert.Contains("/v1/auth/github/poll", source);
            Assert.Contains("mpmSession", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sessionToken", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SponsorProSessionStore_UsesDpapiCurrentUser()
        {
            string source = File.ReadAllText(SourcePath(
                "MultiPingMonitor",
                "Classes",
                "SponsorProAuthService.cs"));

            Assert.Contains("ProtectedData.Protect", source);
            Assert.Contains("ProtectedData.Unprotect", source);
            Assert.Contains("DataProtectionScope.CurrentUser", source);
            Assert.Contains("SponsorProSession.dat", source);
        }

        [Theory]
        [InlineData("About_SponsorProLogin")]
        [InlineData("About_SponsorProSignInAgain")]
        [InlineData("About_SponsorProLoginStarting")]
        [InlineData("About_SponsorProLoginWaiting")]
        [InlineData("About_SponsorProSignedInStatus")]
        [InlineData("About_SponsorProLoginFailed")]
        [InlineData("About_SponsorProLoginError")]
        public void Resources_ContainSponsorProLoginKeysInEnglishAndSlovak(
            string key)
        {
            Assert.True(HasResource(DefaultResxPath(), key),
                $"Missing EN resource key: {key}");
            Assert.True(HasResource(SkSkResxPath(), key),
                $"Missing SK resource key: {key}");
        }

        private static bool HasResource(string path, string key)
        {
            XDocument doc = XDocument.Load(path);

            return doc.Root?
                .Elements("data")
                .Any(element =>
                    string.Equals(
                        (string?)element.Attribute("name"),
                        key,
                        StringComparison.Ordinal))
                == true;
        }

        private static string SourcePath(params string[] parts)
        {
            return Path.Combine(new[] { SolutionRoot() }.Concat(parts).ToArray());
        }

        private static string DefaultResxPath()
        {
            return SourcePath(
                "MultiPingMonitor",
                "Properties",
                "Strings.resx");
        }

        private static string SkSkResxPath()
        {
            return SourcePath(
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
    }
}
