using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class UpdateUxDialogTests
    {
        [Fact]
        public void AboutWindow_UpdateInstallButton_UsesUpdateAvailableWindow()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "AboutWindow.xaml.cs"));
            string body = SliceFrom(source, "private void InstallUpdateButton_Click", 2600);

            Assert.Contains("new UpdateAvailableWindow", body);
            Assert.Contains("_availableUpdateManifest", body);
            Assert.Contains("_sponsorProSession", body);
            Assert.Contains("updateWindow.ShowDialog()", body);
            Assert.DoesNotContain("MessageBox.Show", body);
            Assert.DoesNotContain("MessageBoxButton.YesNo", body);
        }

        [Fact]
        public void MainWindow_UpdateSuccess_UsesThemedDialogWindow()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));
            string body = SliceFrom(source, "private void ShowCompletedUpdateSuccessIfAny()", 2200);

            Assert.Contains("DialogWindow.InfoWindow", body);
            Assert.Contains("ConfigureOwnedDialog(dialog)", body);
            Assert.Contains("dialog.ShowDialog()", body);
            Assert.DoesNotContain("NotifyIcon.ShowBalloonTip", body);
            Assert.DoesNotContain("MessageBox.Show", body);
        }

        [Fact]
        public void MainWindow_AutomaticUpdate_UsesUpdateAvailableWindow()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));
            string body = SliceFrom(source, "private void ShowAutomaticUpdateAvailableWindow", 2600);

            Assert.Contains("UpdateAvailableWindow", body);
            Assert.Contains("SponsorProSessionStore", body);
            Assert.Contains("ConfigureOwnedDialog(updateWindow)", body);
            Assert.Contains("updateWindow.Show()", body);
            Assert.Contains("ShowPendingAutomaticUpdateOrAboutWindow", source);
            Assert.DoesNotContain("NotifyIcon.ShowBalloonTip", body);
        }

        [Fact]
        public void UpdateAvailableWindow_HasDirectUpdateButtonAndProgressState()
        {
            string xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "UpdateAvailableWindow.xaml"));
            string code = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "UpdateAvailableWindow.xaml.cs"));

            Assert.Contains("UpdateButton", xaml);
            Assert.Contains("Click=\"UpdateButton_Click\"", xaml);
            Assert.Contains("UpdateAvailable_UpdateButton", code);
            Assert.Contains("UpdateInstallService", code);
            Assert.Contains("InstallAsync", code);
            Assert.Contains("Application.Current.Shutdown()", code);
            Assert.Contains("Height=\"460\"", xaml);
            Assert.Contains("MinHeight=\"440\"", xaml);
            Assert.Contains("DetailsLabelText", xaml);
            Assert.Contains("UpdateAvailable_DetailsLabel", code);
            Assert.Contains("MinHeight=\"86\"", xaml);
            Assert.Contains("CanContentScroll=\"False\"", xaml);
            Assert.Contains("InstallProgress", xaml);
            Assert.Contains("IsIndeterminate=\"True\"", xaml);
            Assert.Contains("Visibility=\"Collapsed\"", xaml);
            Assert.Contains("UpdateButton.IsEnabled = !installing", code);
            Assert.Contains("LaterButton.IsEnabled = !installing", code);
            Assert.Contains("installing ? Visibility.Visible : Visibility.Collapsed", code);
            Assert.Contains("UpdateAvailable_StatusPreparing", code);
            Assert.DoesNotContain("DownloadButton", xaml);
        }

        private static string SliceFrom(string source, string marker, int maxLength)
        {
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Marker not found: {marker}");

            int length = Math.Min(maxLength, source.Length - start);
            return source.Substring(start, length);
        }

        private static string SourcePath(params string[] parts)
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, Path.Combine(parts));
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "Could not locate source file.",
                Path.Combine(parts));
        }
    }
}
