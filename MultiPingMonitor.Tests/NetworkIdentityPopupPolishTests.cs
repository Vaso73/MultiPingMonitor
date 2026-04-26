using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class NetworkIdentityPopupPolishTests
    {
        private static string SolutionRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "MultiPingMonitor.sln")))
                    return dir;

                var parent = Directory.GetParent(dir);
                if (parent == null)
                    break;

                dir = parent.FullName;
            }

            throw new DirectoryNotFoundException("Could not locate solution root.");
        }

        private static string SourcePath(params string[] parts)
        {
            var all = new string[parts.Length + 1];
            all[0] = SolutionRoot();
            Array.Copy(parts, 0, all, 1, parts.Length);
            return Path.Combine(all);
        }

        [Fact]
        public void StatusChangeLog_HasCompactPopupFields()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "StatusChangeLog.cs"));

            Assert.Contains("public string PopupTitle", source);
            Assert.Contains("public string PopupDetailPrimary", source);
            Assert.Contains("public string PopupDetailSecondary", source);
            Assert.Contains("public bool HasPopupDetail", source);
            Assert.Contains("public string PopupTitleOrAddress", source);
            Assert.Contains("public string PopupStatusText", source);
            Assert.Contains("public string PopupSecondaryText", source);
        }

        [Fact]
        public void MainWindow_NetworkIdentityPopupUsesCompactOldNewIpText()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("PopupTitle = $\"{label} zmenená\"", source);
            Assert.Contains("PopupDetailPrimary = $\"Aktuálna: {currentIp}\"", source);
            Assert.Contains("PopupDetailSecondary = $\"Predtým: {previousIp}\"", source);
            Assert.Contains("CustomStatusText = $\"bola zmenená, aktuálna IP je {currentIp} (predtým {previousIp})\"", source);
        }

        [Fact]
        public void PopupNotificationWindow_UsesWrappedCompactNetworkIdentityLayout()
        {
            var xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "PopupNotificationWindow.xaml"));
            var code = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "PopupNotificationWindow.xaml.cs"));

            Assert.Contains("PopupTitleOrAddress", xaml);
            Assert.Contains("PopupStatusText", xaml);
            Assert.Contains("PopupSecondaryText", xaml);
            Assert.Contains("TextWrapping=\"Wrap\"", xaml);
            Assert.Contains("MaxWidth=\"760\"", xaml);
            Assert.Contains("Math.Min(Width, 820)", code);
            Assert.Contains("Height = 112;", code);
        }

        [Fact]
        public void NetworkIdentityAudioAlert_HasOptionsAndDefaultSound()
        {
            var options = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "ApplicationOptions.cs"));
            var constants = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "Constants.cs"));

            Assert.Contains("IsAudioNetworkIdentityAlertEnabled", options);
            Assert.Contains("AudioNetworkIdentityFilePath", options);
            Assert.Contains("DefaultAudioNetworkIdentityFilePath", constants);
            Assert.Contains("Windows Notify System Generic.wav", constants);
        }

        [Fact]
        public void NetworkIdentityAudioAlert_IsPersistedInConfiguration()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "Configuration.cs"));

            Assert.Contains("Node(\"IsAudioNetworkIdentityAlertEnabled\"", source);
            Assert.Contains("Node(\"AudioNetworkIdentityFilePath\"", source);
            Assert.Contains("options.TryGetValue(\"IsAudioNetworkIdentityAlertEnabled\"", source);
            Assert.Contains("options.TryGetValue(\"AudioNetworkIdentityFilePath\"", source);
        }

        [Fact]
        public void OptionsWindow_HasNetworkIdentityAudioControls()
        {
            var xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "OptionsWindow.xaml"));
            var code = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "OptionsWindow.xaml.cs"));

            Assert.Contains("IsAudioNetworkIdentityAlertEnabled", xaml);
            Assert.Contains("AudioNetworkIdentityFilePath", xaml);
            Assert.Contains("Options_EnableAudioNetworkIdentity", xaml);
            Assert.Contains("Tooltip_AudioNetworkIdentity", xaml);
            Assert.Contains("AudioNetworkIdentityBrowse_Click", code);
            Assert.Contains("AudioNetworkIdentityPlay_Click", code);
            Assert.Contains("IsAudioNetworkIdentityAlertEnabled_Click", code);
            Assert.Contains("ApplicationOptions.IsAudioNetworkIdentityAlertEnabled = true", code);
        }

        [Fact]
        public void MainWindow_PlaysNetworkIdentitySoundWhenEnabled()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("PlayNetworkIdentityIpChangedSound", source);
            Assert.Contains("ApplicationOptions.IsAudioNetworkIdentityAlertEnabled", source);
            Assert.Contains("ApplicationOptions.AudioNetworkIdentityFilePath", source);
            Assert.Contains("Constants.DefaultAudioNetworkIdentityFilePath", source);
            Assert.Contains("new SoundPlayer(audioPath)", source);
        }

        [Fact]
        public void Resources_ContainNetworkIdentityAudioLabels()
        {
            foreach (var file in new[] { "Strings.resx", "Strings.sk-SK.resx" })
            {
                var source = File.ReadAllText(SourcePath("MultiPingMonitor", "Properties", file));

                Assert.Contains("Options_EnableAudioNetworkIdentity", source);
                Assert.Contains("Tooltip_AudioNetworkIdentity", source);
            }

            var designer = File.ReadAllText(SourcePath("MultiPingMonitor", "Properties", "Strings.Designer.cs"));
            Assert.Contains("Options_EnableAudioNetworkIdentity", designer);
            Assert.Contains("Tooltip_AudioNetworkIdentity", designer);
        }
    }
}