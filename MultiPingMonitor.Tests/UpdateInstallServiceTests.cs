using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class UpdateInstallServiceTests
    {
        private static string SolutionRoot()
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir != null && !File.Exists(Path.Combine(dir, "MultiPingMonitor.sln")))
                dir = Directory.GetParent(dir)?.FullName;
            return dir ?? Directory.GetCurrentDirectory();
        }

        private static string SourcePath(params string[] parts) =>
            Path.Combine(SolutionRoot(), Path.Combine(parts));

        [Fact]
        public void UpdateInstallService_UsesBackendDownloadTokenAndZipEndpoint()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Classes",
                    "UpdateInstallService.cs"));

            Assert.Contains("/v1/update/download-token", source);
            Assert.Contains("DownloadUrl", source);
            Assert.Contains("Authorization", source);
            Assert.Contains("Bearer", source);
        }

        [Fact]
        public void UpdateInstallService_ValidatesCanonicalSingleExeZip()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Classes",
                    "UpdateInstallService.cs"));

            Assert.Contains("ZipArchive", source);
            Assert.Contains("archive.Entries.Count != 1", source);
            Assert.Contains("MultiPingMonitor.exe", source);
            Assert.Contains("AssetSize", source);
            Assert.Contains("Sha256", source);
        }

        [Fact]
        public void UpdateInstallService_UsesTemporarySameExeHelperAndTempOnlySwap()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Classes",
                    "UpdateInstallService.cs"));

            Assert.Contains("--mpm-apply-update", source);
            Assert.Contains("File.Copy(currentExePath, helperPath, true)", source);
            Assert.Contains("ApplyStagedExecutableWithTempSwap", source);
            Assert.Contains("CleanupUpdateRootLater(updateRoot)", source);
            Assert.Contains("ExeName + \".old\"", source);
            Assert.Contains("Path.GetTempPath()", source);
            Assert.DoesNotContain("Path.Combine(appDirectory, \"backup\")", source);
            Assert.DoesNotContain("CopyDirectoryExcludingBackup", source);
            Assert.Contains("Process.Start", source);
        }

        [Fact]
        public void UpdateInstallService_DoesNotInstallPersistentHelperInAppDirectory()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Classes",
                    "UpdateInstallService.cs"));

            Assert.Contains("Path.Combine(updateRoot, \"MultiPingMonitor-update-helper.exe\")", source);
            Assert.DoesNotContain("Path.Combine(appDirectory, \"MultiPingMonitor-update-helper.exe\")", source);
        }

        [Fact]
        public void AppStartup_HandlesUpdateHelperBeforeCreatingMainWindow()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "App.xaml.cs"));

            int helperIndex =
                source.IndexOf("UpdateInstallService.HelperModeArgument", StringComparison.Ordinal);
            int mainWindowIndex =
                source.IndexOf("new UI.MainWindow", StringComparison.Ordinal);

            Assert.True(helperIndex > 0);
            Assert.True(mainWindowIndex > helperIndex);
            Assert.Contains("RunApplyUpdateHelper(args)", source);
        }

        [Fact]
        public void UpdateAvailableWindow_UsesRealUpdateInstallerInsteadOfPlaceholder()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "UpdateAvailableWindow.xaml.cs"));

            Assert.Contains("new UpdateInstallService()", source);
            Assert.Contains("InstallAsync(", source);
            Assert.Contains("Application.Current.Shutdown()", source);
        }

        [Theory]
        [InlineData("About_UpdateConfirmTitle")]
        [InlineData("About_UpdateConfirmMessage")]
        [InlineData("About_StatusPreparingUpdate")]
        [InlineData("About_StatusUpdateRestarting")]
        [InlineData("About_StatusInstallFailed")]
        public void Resources_ContainUpdateInstallKeysInEnglishAndSlovak(
            string key)
        {
            string en =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Properties",
                    "Strings.resx"));
            string sk =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Properties",
                    "Strings.sk-SK.resx"));

            Assert.Contains($"name=\"{key}\"", en);
            Assert.Contains($"name=\"{key}\"", sk);
        }


        [Fact]
        public void UpdateInstallService_DoesNotUseAssemblyLocationInSingleFileFallback()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Classes",
                    "UpdateInstallService.cs"));

            Assert.Contains("Environment.ProcessPath", source);
            Assert.Contains("AppContext.BaseDirectory", source);
            Assert.DoesNotContain("Assembly.GetEntryAssembly", source);
            Assert.DoesNotContain("Assembly.GetExecutingAssembly", source);
            Assert.DoesNotContain(".Location", source);
        }


        [Fact]
        public void UpdateInstallService_WritesPostRestartSuccessMarkerBeforeStartingHelper()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Classes",
                    "UpdateInstallService.cs"));

            int markerIndex =
                source.IndexOf("WritePendingSuccessMarker(manifest)", StringComparison.Ordinal);
            int helperStartIndex =
                source.IndexOf("Process helper = Process.Start(startInfo)", StringComparison.Ordinal);

            Assert.True(markerIndex > 0);
            Assert.True(helperStartIndex > markerIndex);
            Assert.Contains("ConsumeCompletedUpdateSuccessVersion", source);
            Assert.Contains("post-update-success.json", source);
        }

        [Fact]
        public void MainWindow_ShowsCompletedUpdateSuccessNotificationOnStartup()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "MainWindow.xaml.cs"));

            Assert.Contains("ShowCompletedUpdateSuccessIfAny", source);
            Assert.Contains("ConsumeCompletedUpdateSuccessVersion", source);
            Assert.Contains("UpdateSuccess_TrayMessage", source);
            Assert.Contains("DialogWindow.InfoWindow", source);
        }

        [Fact]
        public void Resources_UsePreciseUpdateInstallFailureAndSuccessMessages()
        {
            string en =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Properties",
                    "Strings.resx"));
            string sk =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "Properties",
                    "Strings.sk-SK.resx"));

            Assert.Contains("The application was not changed", en);
            Assert.Contains("Aplikácia nebola zmenená", sk);
            Assert.Contains("UpdateSuccess_TrayTitle", en);
            Assert.Contains("UpdateSuccess_TrayTitle", sk);
            Assert.Contains("UpdateSuccess_TrayMessage", en);
            Assert.Contains("UpdateSuccess_TrayMessage", sk);
            Assert.DoesNotContain("The current application folder will be backed up", en);
            Assert.DoesNotContain("Aktuálny priečinok aplikácie sa pred výmenou zálohuje", sk);
        }

        [Fact]
        public void Project_DoesNotKeepLegacyNetFrameworkAppConfig()
        {
            Assert.False(
                File.Exists(SourcePath("MultiPingMonitor", "app.config")),
                "Legacy app.config creates MultiPingMonitor.dll.config in single-file publish output.");
        }

        [Fact]
        public void WorkflowDocs_RecordOneStepGithubRules()
        {
            string workflow =
                File.ReadAllText(SourcePath("docs", "GITHUB_WORKFLOW.md"));
            string agents =
                File.ReadAllText(SourcePath("AGENTS.md"));

            Assert.Contains("one complete CLI workflow", workflow);
            Assert.Contains("Do not paste very large interactive heredocs directly into zsh.", workflow);
            Assert.Contains("branch + PR + scope check + merge + sync main", workflow);
            Assert.Contains("docs/GITHUB_WORKFLOW.md", agents);
        }
    }
}
