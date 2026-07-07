using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using MultiPingMonitor.Classes;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    public partial class AboutWindow : Window
    {
        private const string SponsorProReleasesUrl =
            "https://github.com/Vaso73-Software/Sponsor-Pro-Releases/releases";

        private readonly Version _currentVersion;
        private CancellationTokenSource _checkCancellation;

        public AboutWindow()
        {
            InitializeComponent();

            _currentVersion =
                Assembly.GetEntryAssembly()?.GetName().Version ??
                Assembly.GetExecutingAssembly().GetName().Version ??
                new Version(0, 0, 0);

            Title = Text("About_Title", "About MultiPingMonitor");
            VersionLabelText.Text =
                Text("About_VersionLabel", "Version");
            VersionValueText.Text =
                FormatVersion(_currentVersion);
            EditionValueText.Text =
                $"{Text("About_EditionLabel", "Edition")}: " +
                Text("About_EditionSponsorPro", "Sponsor Pro");

            CheckForUpdatesButton.Content =
                Text("About_CheckForUpdates", "Check for updates");
            OpenReleasesButton.Content =
                Text("About_OpenReleases", "Open Sponsor Pro releases");
            CloseButton.Content =
                Text("About_Close", "Close");
            StatusText.Text =
                Text(
                    "About_StatusIdle",
                    "Use Check for updates to compare this installation with the latest Sponsor Pro version.");
        }

        private async void CheckForUpdatesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _checkCancellation?.Cancel();
            _checkCancellation?.Dispose();
            _checkCancellation = new CancellationTokenSource();

            SetCheckingState(true);
            OpenReleasesButton.Visibility = Visibility.Collapsed;
            StatusText.Text =
                Text("About_StatusChecking", "Checking for updates...");

            try
            {
                using var service = new UpdateCheckService();

                UpdateCheckResult result =
                    await service.CheckAsync(
                        _currentVersion,
                        _checkCancellation.Token);

                switch (result.Status)
                {
                    case UpdateCheckStatus.UpdateAvailable:
                        StatusText.Text = string.Format(
                            CultureInfo.CurrentCulture,
                            Text(
                                "About_StatusUpdateAvailable",
                                "Version {0} is available."),
                            FormatVersion(result.LatestVersion));
                        OpenReleasesButton.Visibility =
                            Visibility.Visible;
                        break;

                    case UpdateCheckStatus.UpToDate:
                        StatusText.Text =
                            Text(
                                "About_StatusUpToDate",
                                "You are using the latest Sponsor Pro version.");
                        break;

                    case UpdateCheckStatus.InvalidManifest:
                        StatusText.Text =
                            Text(
                                "About_StatusInvalidResponse",
                                "The update information returned by the server is invalid.");
                        break;

                    default:
                        StatusText.Text =
                            Text(
                                "About_StatusCheckFailed",
                                "The update check could not be completed. Check your internet connection and try again.");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Window closed or a newer manual check replaced this one.
            }
            finally
            {
                SetCheckingState(false);
            }
        }

        private void SetCheckingState(bool checking)
        {
            CheckForUpdatesButton.IsEnabled = !checking;
            CheckProgress.Visibility =
                checking ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenReleasesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo(SponsorProReleasesUrl)
                    {
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"AboutWindow: cannot open releases page: {ex.GetType().Name}: {ex.Message}");

                StatusText.Text =
                    Text(
                        "About_StatusCheckFailed",
                        "The update check could not be completed. Check your internet connection and try again.");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _checkCancellation?.Cancel();
            _checkCancellation?.Dispose();
            _checkCancellation = null;
            base.OnClosed(e);
        }

        private static string Text(string key, string fallback)
        {
            return Strings.ResourceManager.GetString(
                       key,
                       CultureInfo.CurrentUICulture)
                   ?? fallback;
        }

        private static string FormatVersion(Version version)
        {
            int build = version.Build < 0 ? 0 : version.Build;
            return $"{version.Major}.{version.Minor}.{build}";
        }
    }
}
