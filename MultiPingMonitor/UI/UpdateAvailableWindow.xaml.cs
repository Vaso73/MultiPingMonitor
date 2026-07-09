using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using MultiPingMonitor.Classes;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    public partial class UpdateAvailableWindow : Window
    {
        private readonly Version _currentVersion;
        private readonly Version _latestVersion;
        private readonly UpdateManifest _manifest;
        private readonly SponsorProSession _sponsorProSession;
        private CancellationTokenSource _installCancellation;

        public bool HelperStarted { get; private set; }

        public UpdateAvailableWindow(
            Version currentVersion,
            Version latestVersion,
            UpdateManifest manifest,
            SponsorProSession sponsorProSession)
        {
            InitializeComponent();

            _currentVersion = currentVersion ?? new Version(0, 0, 0);
            _latestVersion = latestVersion ?? new Version(0, 0, 0);
            _manifest = manifest;
            _sponsorProSession = sponsorProSession;

            Title = Text("UpdateAvailable_Title", "Update Available");
            HeaderText.Text = Text("UpdateAvailable_Header", "Update available");
            SubHeaderText.Text = string.Format(
                CultureInfo.CurrentCulture,
                Text(
                    "About_StatusUpdateAvailable",
                    "Version {0} is available."),
                FormatVersion(_latestVersion));

            CurrentVersionLabel.Text = Text("UpdateAvailable_CurrentVersion", "Current:");
            CurrentVersionText.Text = FormatVersion(_currentVersion);
            LatestVersionLabel.Text = Text("UpdateAvailable_LatestVersion", "Latest:");
            LatestVersionText.Text = FormatVersion(_latestVersion);
            ChannelLabel.Text = Text("UpdateAvailable_Channel", "Channel:");
            ChannelText.Text = string.IsNullOrWhiteSpace(_manifest?.Channel)
                ? "Sponsor Pro"
                : _manifest.Channel;
            SizeLabel.Text = Text("UpdateAvailable_Size", "Package:");
            SizeText.Text = FormatBytes(_manifest?.AssetSize ?? 0);
            DetailsLabelText.Text =
                Text("UpdateAvailable_DetailsLabel", "Details");

            DetailsText.Text =
                Text(
                    "UpdateAvailable_DetailsFallback",
                    "This update will be downloaded and installed directly from the Sponsor Pro update service. Detailed release notes are not available from the update endpoint yet.");

            StatusText.Text =
                Text("UpdateAvailable_StatusReady", "Ready to install.");

            UpdateButton.Content =
                Text("UpdateAvailable_UpdateButton", "Update");
            LaterButton.Content =
                Text("UpdateAvailable_LaterButton", "Not now");
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manifest == null || _latestVersion == null)
            {
                Close();
                return;
            }

            if (_sponsorProSession == null || !_sponsorProSession.IsUsable)
            {
                DialogWindow.InfoWindow(
                    Text(
                        "UpdateAvailable_StatusSignInRequired",
                        "Sign in with GitHub before installing Sponsor Pro updates."),
                    this).ShowDialog();
                return;
            }

            _installCancellation?.Cancel();
            _installCancellation?.Dispose();
            _installCancellation = new CancellationTokenSource();

            SetInstallingState(true);
            StatusText.Text =
                Text(
                    "UpdateAvailable_StatusPreparing",
                    "Preparing update installation...");

            try
            {
                using var service = new UpdateInstallService();

                UpdateInstallResult result =
                    await service.InstallAsync(
                        _manifest,
                        _sponsorProSession,
                        _installCancellation.Token);

                if (result.Status == UpdateInstallStatus.HelperStarted)
                {
                    HelperStarted = true;
                    StatusText.Text =
                        Text(
                            "UpdateAvailable_StatusRestarting",
                            "Update is ready. MultiPingMonitor will restart now.");
                    Application.Current.Shutdown();
                    return;
                }

                if (result.Status == UpdateInstallStatus.AuthenticationRequired)
                {
                    DialogWindow.InfoWindow(
                        Text(
                            "UpdateAvailable_StatusSignInRequired",
                            "Sign in with GitHub before installing Sponsor Pro updates."),
                        this).ShowDialog();
                    return;
                }

                StatusText.Text =
                    Text(
                        "UpdateAvailable_StatusFailed",
                        "The update could not be installed. The application was not changed. Try again or download the latest version manually.");
            }
            catch (OperationCanceledException)
            {
                // Window closed or a newer install attempt replaced this one.
            }
            finally
            {
                if (!HelperStarted)
                    SetInstallingState(false);
            }
        }

        private void SetInstallingState(bool installing)
        {
            UpdateButton.IsEnabled = !installing;
            LaterButton.IsEnabled = !installing;
            InstallProgress.Visibility =
                installing ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _installCancellation?.Cancel();
            _installCancellation?.Dispose();
            _installCancellation = null;
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
            if (version == null)
                return string.Empty;

            return version.Build >= 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
                return "—";

            double mib = bytes / 1024d / 1024d;
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.0} MB",
                mib);
        }
    }
}
