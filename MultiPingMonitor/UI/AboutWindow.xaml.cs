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
        private readonly Version _currentVersion;
        private readonly SponsorProSessionStore _sponsorProSessionStore;
        private SponsorProSession _sponsorProSession;
        private Version _availableVersion;
        private CancellationTokenSource _checkCancellation;
        private CancellationTokenSource _authCancellation;

        public AboutWindow()
        {
            InitializeComponent();

            _sponsorProSessionStore = new SponsorProSessionStore();
            _sponsorProSession = _sponsorProSessionStore.Load();

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

            SponsorProLoginButton.Content =
                Text("About_SponsorProLogin", "Sign in with GitHub");
            CheckForUpdatesButton.Content =
                Text("About_CheckForUpdates", "Check for updates");
            InstallUpdateButton.Content =
                Text("About_Update", "Update");
            CloseButton.Content =
                Text("About_Close", "Close");

            RefreshSponsorProStatus();
            ResetUpdateState();
        }

        private void RefreshSponsorProStatus()
        {
            if (_sponsorProSession != null && _sponsorProSession.IsUsable)
            {
                string login =
                    string.IsNullOrWhiteSpace(_sponsorProSession.GithubLogin)
                        ? "GitHub"
                        : _sponsorProSession.GithubLogin;

                AccountTitleText.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Text("About_AccountSignedInTitle", "GitHub: {0}"),
                    login);
                AccountStatusText.Text =
                    Text("About_AccountSponsorActive", "Sponsor Pro active");
                SponsorProLoginButton.Visibility =
                    Visibility.Collapsed;
                return;
            }

            AccountTitleText.Text =
                Text(
                    "About_AccountNotConnectedTitle",
                    "GitHub account not connected");
            AccountStatusText.Text =
                Text(
                    "About_AccountNotConnectedStatus",
                    "Sign in to enable Sponsor Pro updates.");
            SponsorProLoginButton.Visibility =
                Visibility.Visible;
            SponsorProLoginButton.Content =
                Text("About_SponsorProLogin", "Sign in with GitHub");
        }

        private void ResetUpdateState()
        {
            _availableVersion = null;
            InstallUpdateButton.IsEnabled = false;
            StatusText.Text =
                Text(
                    "About_StatusIdle",
                    "Check for updates to compare this installation with the latest Sponsor Pro version.");
        }

        private async void SponsorProLoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _authCancellation?.Cancel();
            _authCancellation?.Dispose();
            _authCancellation = new CancellationTokenSource();

            SetAuthState(true);
            StatusText.Text =
                Text(
                    "About_SponsorProLoginStarting",
                    "Opening GitHub sign-in...");

            try
            {
                using var service = new SponsorProAuthService();
                SponsorProAuthStartResult start =
                    await service.StartGitHubLoginAsync(
                        _authCancellation.Token);

                Process.Start(
                    new ProcessStartInfo(start.LoginUrl)
                    {
                        UseShellExecute = true
                    });

                StatusText.Text =
                    Text(
                        "About_SponsorProLoginWaiting",
                        "Complete GitHub sign-in in your browser. This window will update automatically.");

                SponsorProLoginResult result =
                    await service.PollUntilAuthenticatedAsync(
                        start,
                        _authCancellation.Token);

                if (result.Success && result.Session != null)
                {
                    _sponsorProSession = result.Session;
                    _sponsorProSessionStore.Save(result.Session);
                    RefreshSponsorProStatus();
                    ResetUpdateState();
                    return;
                }

                StatusText.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Text(
                        "About_SponsorProLoginFailed",
                        "GitHub sign-in completed, but Sponsor Pro access was not confirmed. {0}"),
                    result.Error ?? string.Empty);
            }
            catch (OperationCanceledException)
            {
                // Window closed or a newer login attempt replaced this one.
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"AboutWindow Sponsor Pro login: {ex.GetType().Name}: {ex.Message}");
                StatusText.Text =
                    Text(
                        "About_SponsorProLoginError",
                        "GitHub sign-in could not be completed. Check your internet connection and try again.");
            }
            finally
            {
                SetAuthState(false);
                RefreshSponsorProStatus();
            }
        }

        private async void CheckForUpdatesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_sponsorProSession == null || !_sponsorProSession.IsUsable)
            {
                StatusText.Text =
                    Text(
                        "About_StatusSignInRequired",
                        "Sign in with GitHub before checking Sponsor Pro updates.");
                RefreshSponsorProStatus();
                return;
            }

            _checkCancellation?.Cancel();
            _checkCancellation?.Dispose();
            _checkCancellation = new CancellationTokenSource();

            SetCheckingState(true);
            _availableVersion = null;
            InstallUpdateButton.IsEnabled = false;
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
                        _availableVersion = result.LatestVersion;
                        InstallUpdateButton.IsEnabled = true;
                        StatusText.Text = string.Format(
                            CultureInfo.CurrentCulture,
                            Text(
                                "About_StatusUpdateAvailable",
                                "Version {0} is available."),
                            FormatVersion(result.LatestVersion));
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

        private void InstallUpdateButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_availableVersion == null)
            {
                InstallUpdateButton.IsEnabled = false;
                return;
            }

            StatusText.Text =
                Text(
                    "About_UpdateInstallNotReady",
                    "Update installation will be enabled in the next updater step.");
        }

        private void SetCheckingState(bool checking)
        {
            CheckForUpdatesButton.IsEnabled = !checking;
            SponsorProLoginButton.IsEnabled = !checking;
            InstallUpdateButton.IsEnabled = !checking && _availableVersion != null;
            CheckProgress.Visibility =
                checking ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetAuthState(bool authenticating)
        {
            SponsorProLoginButton.IsEnabled = !authenticating;
            CheckForUpdatesButton.IsEnabled = !authenticating;
            InstallUpdateButton.IsEnabled = !authenticating && _availableVersion != null;
            CheckProgress.Visibility =
                authenticating ? Visibility.Visible : Visibility.Collapsed;
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
            _authCancellation?.Cancel();
            _authCancellation?.Dispose();
            _authCancellation = null;
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
