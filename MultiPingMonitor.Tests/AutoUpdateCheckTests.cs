using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class AutoUpdateCheckTests
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
        public void ApplicationOptions_DefinesAutomaticUpdateCheckDefaults()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "ApplicationOptions.cs"));

            Assert.Contains("public enum AutomaticUpdateFrequency", source);
            Assert.Contains("OnStartup = 0", source);
            Assert.Contains("Daily = 1", source);
            Assert.Contains("Weekly = 2", source);
            Assert.Contains("Monthly = 3", source);
            Assert.Contains("AutomaticUpdateCheckEnabled { get; set; } = true", source);
            Assert.Contains("AutomaticUpdateFrequency.Daily", source);
            Assert.Contains("LastAutomaticUpdateCheckAt", source);
        }

        [Fact]
        public void Configuration_PersistsAutomaticUpdateCheckOptions()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "Configuration.cs"));

            Assert.Contains("AutomaticUpdateCheckEnabled", source);
            Assert.Contains("AutomaticUpdateCheckFrequency", source);
            Assert.Contains("LastAutomaticUpdateCheckAt", source);
            Assert.Contains("DateTime.TryParse", source);
            Assert.Contains("ApplicationOptions.LastAutomaticUpdateCheckAt", source);
        }

        [Fact]
        public void OptionsWindow_ExposesAutomaticUpdateCheckControls()
        {
            string xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "OptionsWindow.xaml"));
            string code = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "OptionsWindow.xaml.cs"));

            Assert.Contains("AutomaticUpdateCheckEnabled", xaml);
            Assert.Contains("AutomaticUpdateCheckFrequency", xaml);
            Assert.Contains("LastAutomaticUpdateCheck", xaml);
            Assert.Contains("PopulateUpdateOptions", code);
            Assert.Contains("SaveUpdateOptions", code);
            Assert.Contains("Options_AutoUpdateFrequency_Daily", code);
        }

        [Fact]
        public void MainWindow_ChecksUpdatesInBackgroundAndOnlyShowsTrayBalloon()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("StartAutomaticUpdateCheckIfDue", source);
            Assert.Contains("RunAutomaticUpdateCheckAsync", source);
            Assert.Contains("new UpdateCheckService()", source);
            Assert.Contains("GetCurrentVersionForAutomaticUpdateCheck", source);
            Assert.Contains("service.CheckAsync(", source);
            Assert.Contains("ShowAutomaticUpdateAvailableBalloon", source);
            Assert.Contains("NotifyIcon.ShowBalloonTip", source);
            Assert.Contains("NotifyIcon.BalloonTipClicked", source);
            Assert.DoesNotContain("new UpdateInstallService()", source);
        }

        [Fact]
        public void AutoUpdateFrequencyDueLogic_IsDailyWeeklyMonthlyAndStartup()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("AutomaticUpdateFrequency.OnStartup", source);
            Assert.Contains("AutomaticUpdateFrequency.Daily", source);
            Assert.Contains("AutomaticUpdateFrequency.Weekly", source);
            Assert.Contains("AutomaticUpdateFrequency.Monthly", source);
            Assert.Contains("last.Date < now.Date", source);
            Assert.Contains("TotalDays >= 7", source);
            Assert.Contains("TotalDays >= 30", source);
        }

        [Theory]
        [InlineData("Options_Header_Updates")]
        [InlineData("Options_AutomaticUpdateCheck")]
        [InlineData("Options_AutomaticUpdateFrequency")]
        [InlineData("Options_AutoUpdateFrequency_OnStartup")]
        [InlineData("Options_AutoUpdateFrequency_Daily")]
        [InlineData("Options_AutoUpdateFrequency_Weekly")]
        [InlineData("Options_AutoUpdateFrequency_Monthly")]
        [InlineData("Options_LastAutomaticUpdateCheck")]
        [InlineData("Options_LastAutomaticUpdateCheckNever")]
        [InlineData("AutoUpdate_TrayTitle")]
        [InlineData("AutoUpdate_TrayUpdateAvailable")]
        public void Resources_ContainAutomaticUpdateKeysInEnglishAndSlovak(string key)
        {
            Assert.Contains(
                XDocument.Load(SourcePath("MultiPingMonitor", "Properties", "Strings.resx"))
                    .Root!.Elements("data"),
                e => (string?)e.Attribute("name") == key);
            Assert.Contains(
                XDocument.Load(SourcePath("MultiPingMonitor", "Properties", "Strings.sk-SK.resx"))
                    .Root!.Elements("data"),
                e => (string?)e.Attribute("name") == key);
        }
    }
}
