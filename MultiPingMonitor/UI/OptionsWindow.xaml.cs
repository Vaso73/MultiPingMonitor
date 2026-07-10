using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class OptionsWindow : Window
    {
        // Store accepted preview values so they can be reverted if the user cancels.
        private string _originalTheme;
        private string _originalVisualStyle;
        private ApplicationOptions.DisplayMode _originalDisplayMode;
        private ApplicationOptions.CompactSourceMode _originalCompactSource;
        private string _originalLanguageCode;

        private static string Text(string key, string fallback)
        {
            var value = Properties.Strings.ResourceManager.GetString(key);
            return string.IsNullOrEmpty(value) || string.Equals(value, key, StringComparison.Ordinal)
                ? fallback
                : value;
        }

        private MainWindow HostMainWindow =>
            Owner as MainWindow ?? Application.Current?.MainWindow as MainWindow;

        public OptionsWindow()
        {
            InitializeComponent();
            RefreshTitleBarChromeLocalization();
            WindowPlacementService.Attach(this, "OptionsWindow");

            // Remember the current theme so we can revert if the user cancels.
            _originalTheme = ApplicationOptions.Theme;
            // Remember the current visual style so we can revert if the user cancels.
            _originalVisualStyle = ApplicationOptions.VisualStyle;
            // Remember the current display mode so we can revert if the user cancels.
            _originalDisplayMode = ApplicationOptions.CurrentDisplayMode;
            // Remember the current compact source mode so we can revert if the user cancels.
            _originalCompactSource = ApplicationOptions.CompactSource;
            _originalLanguageCode = ApplicationOptions.LanguageCode;

            PopulateGeneralOptions();
            PopulateUpdateOptions();
            PopulateNotificationOptions();
            PopulateEmailAlertOptions();
            PopulateAudioAlertOptions();
            PopulateLogOutputOptions();
            PopulateAdvancedOptions();
            PopulateDisplayOptions();
            PopulateLayoutOptions();
            RefreshLocalizedText(null, LanguageRuntimeService.CaptureResourceSnapshot());
            Loaded += (_, _) => VisualStyleManager.ApplyNativeWindowCorners(this);
        }

        private bool? ShowError(string message, TabItem tabItem, Control control, bool isWarning = false)
        {
            // Switch to specified tab.
            tabItem?.Focus();

            // Show warning or error?
            DialogWindow errorWindow;
            if (isWarning == true)
            {
                errorWindow = DialogWindow.WarningWindow(message, Properties.Strings.DialogButton_Save);
            }
            else
            {
                errorWindow = DialogWindow.ErrorWindow(message);
            }

            // Display dialog and capture result.
            errorWindow.Owner = this;
            var result = errorWindow.ShowDialog();

            // Set focus to specified control.
            control?.Focus();

            return result;
        }

        private void PopulateGeneralOptions()
        {
            string pingIntervalUnits;
            int pingIntervalDivisor;
            int pingInterval = ApplicationOptions.PingInterval;
            int pingTimeout = ApplicationOptions.PingTimeout;

            if (ApplicationOptions.PingInterval >= 3600000 && ApplicationOptions.PingInterval % 3600000 == 0)
            {
                pingIntervalUnits = "hours";
                pingIntervalDivisor = 3600000;
            }
            else if (ApplicationOptions.PingInterval >= 60000 && ApplicationOptions.PingInterval % 60000 == 0)
            {
                pingIntervalUnits = "minutes";
                pingIntervalDivisor = 60000;
            }
            else
            {
                pingIntervalUnits = "seconds";
                pingIntervalDivisor = 1000;
            }

            pingInterval /= pingIntervalDivisor;
            pingTimeout /= 1000;

            PingInterval.Text = pingInterval.ToString();
            PingTimeout.Text = pingTimeout.ToString();
            AlertThreshold.Text = ApplicationOptions.AlertThreshold.ToString();
            if (pingIntervalUnits == "hours")
                PingIntervalUnits.SelectedIndex = 2;
            else if (pingIntervalUnits == "minutes")
                PingIntervalUnits.SelectedIndex = 1;
            else
                PingIntervalUnits.SelectedIndex = 0;

            // Latency detection settings.
            LatencyDetectionMode.SelectedIndex = (int)ApplicationOptions.LatencyDetectionMode;
            HighLatencyMilliseconds.Text = ApplicationOptions.HighLatencyMilliseconds.ToString();
            HighLatencyTriggerCount.Text = ApplicationOptions.HighLatencyAlertTiggerCount.ToString(); // "Tigger" is the persisted XML key name — do not rename.

            // Get startup mode settings.
            InitialProbeCount.Text = ApplicationOptions.InitialProbeCount.ToString();
            InitialColumnCount.Text = ApplicationOptions.InitialColumnCount.ToString();
            StartupMode.SelectedIndex = (int)ApplicationOptions.InitialStartMode;
            InitialFavorite.ItemsSource = Favorite.GetTitles();
            InitialFavorite.Text = ApplicationOptions.InitialFavorite ?? string.Empty;
        }


        private void PopulateUpdateOptions()
        {
            AutomaticUpdateHeader.Text =
                Text("Options_Header_Updates", "Updates");
            AutomaticUpdateCheckEnabled.Content =
                Text("Options_AutomaticUpdateCheck", "Automatically check for updates");
            AutomaticUpdateCheckFrequencyLabel.Text =
                Text("Options_AutomaticUpdateFrequency", "Frequency:");
            LastAutomaticUpdateCheckLabel.Text =
                Text("Options_LastAutomaticUpdateCheck", "Last check:");

            AutomaticUpdateCheckEnabled.IsChecked =
                ApplicationOptions.AutomaticUpdateCheckEnabled;

            AutomaticUpdateCheckFrequency.Items.Clear();
            AutomaticUpdateCheckFrequency.Items.Add(
                Text("Options_AutoUpdateFrequency_OnStartup", "At every startup"));
            AutomaticUpdateCheckFrequency.Items.Add(
                Text("Options_AutoUpdateFrequency_Daily", "Once daily"));
            AutomaticUpdateCheckFrequency.Items.Add(
                Text("Options_AutoUpdateFrequency_Weekly", "Once weekly"));
            AutomaticUpdateCheckFrequency.Items.Add(
                Text("Options_AutoUpdateFrequency_Monthly", "Once monthly"));
            AutomaticUpdateCheckFrequency.SelectedIndex =
                (int)ApplicationOptions.AutomaticUpdateCheckFrequency;

            LastAutomaticUpdateCheck.Text =
                ApplicationOptions.LastAutomaticUpdateCheckAt.HasValue
                    ? ApplicationOptions.LastAutomaticUpdateCheckAt.Value.ToString("g")
                    : Text("Options_LastAutomaticUpdateCheckNever", "Never");
        }

        private bool SaveUpdateOptions()
        {
            ApplicationOptions.AutomaticUpdateCheckEnabled =
                AutomaticUpdateCheckEnabled.IsChecked == true;

            if (AutomaticUpdateCheckFrequency.SelectedIndex < 0)
            {
                AutomaticUpdateCheckFrequency.SelectedIndex =
                    (int)ApplicationOptions.AutomaticUpdateFrequency.Daily;
            }

            ApplicationOptions.AutomaticUpdateCheckFrequency =
                (ApplicationOptions.AutomaticUpdateFrequency)
                    AutomaticUpdateCheckFrequency.SelectedIndex;

            return true;
        }

        private void PopulateNotificationOptions()
        {
            PopupsDisabledOption.IsChecked = false;
            PopupsMinimizedOption.IsChecked = false;
            PopupsAlwaysOption.IsChecked = false;
            switch (ApplicationOptions.PopupOption)
            {
                case ApplicationOptions.PopupNotificationOption.Never:
                    PopupsDisabledOption.IsChecked = true;
                    break;
                case ApplicationOptions.PopupNotificationOption.WhenMinimized:
                    PopupsMinimizedOption.IsChecked = true;
                    break;
                case ApplicationOptions.PopupNotificationOption.Always:
                    PopupsAlwaysOption.IsChecked = true;
                    break;
            }
            IsAutoDismissEnabled.IsChecked = ApplicationOptions.IsAutoDismissEnabled;
            AutoDismissInterval.Text = (ApplicationOptions.AutoDismissMilliseconds / 1000).ToString();
        }

        private void PopulateEmailAlertOptions()
        {
            IsEmailAlertsEnabled.IsChecked = ApplicationOptions.IsEmailAlertEnabled;
            IsSmtpAuthenticationRequired.IsChecked = ApplicationOptions.IsEmailAuthenticationRequired;
            IsSmtpSslEnabled.IsChecked = ApplicationOptions.IsEmailSslEnabled;
            SmtpServer.Text = ApplicationOptions.EmailServer;
            SmtpPort.Text = ApplicationOptions.EmailPort;
            SmtpUsername.Text = ApplicationOptions.EmailUser;
            SmtpPassword.Password = ApplicationOptions.EmailPassword;
            EmailRecipientAddress.Text = ApplicationOptions.EmailRecipient;
            EmailFromAddress.Text = ApplicationOptions.EmailFromAddress;
        }
        private void PopulateAudioAlertOptions()
        {
            IsAudioDownAlertEnabled.IsChecked = ApplicationOptions.IsAudioDownAlertEnabled;
            AudioDownFilePath.Text = ApplicationOptions.AudioDownFilePath;
            IsAudioUpAlertEnabled.IsChecked = ApplicationOptions.IsAudioUpAlertEnabled;
            AudioUpFilePath.Text = ApplicationOptions.AudioUpFilePath;
            IsAudioNetworkIdentityAlertEnabled.IsChecked = ApplicationOptions.IsAudioNetworkIdentityAlertEnabled;
            AudioNetworkIdentityFilePath.Text = string.IsNullOrWhiteSpace(ApplicationOptions.AudioNetworkIdentityFilePath)
                ? Environment.ExpandEnvironmentVariables(Constants.DefaultAudioNetworkIdentityFilePath)
                : ApplicationOptions.AudioNetworkIdentityFilePath;
        }

        private void PopulateLogOutputOptions()
        {
            LogPath.Text = ApplicationOptions.LogPath;
            IsLogOutputEnabled.IsChecked = ApplicationOptions.IsLogOutputEnabled;
            LogStatusChangesPath.Text = ApplicationOptions.LogStatusChangesPath;
            IsLogStatusChangesEnabled.IsChecked = ApplicationOptions.IsLogStatusChangesEnabled;
        }

        private void PopulateAdvancedOptions()
        {
            TTL.Text = ApplicationOptions.TTL.ToString();
            DontFragment.IsChecked = ApplicationOptions.DontFragment;

            if (ApplicationOptions.UseCustomBuffer)
            {
                UseCustomPacketOption.IsChecked = true;
                PacketData.Text = Encoding.ASCII.GetString(ApplicationOptions.Buffer);
            }
            else
            {
                PacketSizeOption.IsChecked = true;
                PacketSize.Text = ApplicationOptions.Buffer.Length.ToString();
            }

            UpdateByteCount();
        }

        private void PopulateDisplayOptions()
        {
            IsAlwaysOnTopEnabled.IsChecked = ApplicationOptions.IsAlwaysOnTopEnabled;
            IsMinimizeToTrayEnabled.IsChecked = ApplicationOptions.IsMinimizeToTrayEnabled;
            IsExitToTrayEnabled.IsChecked = ApplicationOptions.IsExitToTrayEnabled;
            StartInTray.IsChecked = ApplicationOptions.StartInTray;
            RememberWindowPosition.IsChecked = ApplicationOptions.RememberWindowPosition;

            // Populate theme ComboBox.
            foreach (AppTheme theme in Enum.GetValues(typeof(AppTheme)))
            {
                ThemeComboBox.Items.Add(ThemeManager.GetThemeName(theme));
            }
            ThemeComboBox.SelectedItem = ApplicationOptions.Theme;
            if (ThemeComboBox.SelectedItem == null)
                ThemeComboBox.SelectedIndex = 0;

            // Populate visual style ComboBox.
            foreach (VisualStyle vs in Enum.GetValues(typeof(VisualStyle)))
            {
                VisualStyleComboBox.Items.Add(VisualStyleManager.GetStyleName(vs));
            }
            VisualStyleComboBox.SelectedItem = ApplicationOptions.VisualStyle;
            if (VisualStyleComboBox.SelectedItem == null)
                VisualStyleComboBox.SelectedIndex = 0;

            // Display mode.
            DisplayModeComboBox.Items.Add(Properties.Strings.Options_DisplayMode_Normal);
            DisplayModeComboBox.Items.Add(Properties.Strings.Options_DisplayMode_Compact);
            DisplayModeComboBox.SelectedIndex = (int)ApplicationOptions.CurrentDisplayMode;

            // Compact data source.
            CompactSourceComboBox.Items.Add(Properties.Strings.Options_CompactSource_NormalTargets);
            CompactSourceComboBox.Items.Add(Properties.Strings.Options_CompactSource_CustomTargets);
            CompactSourceComboBox.SelectedIndex = (int)ApplicationOptions.CompactSource;
            UpdateCompactTargetsButtonVisibility();

            PopulateLanguageOptions();

            // Font sizes.
            FontSizeProbe.Text = ApplicationOptions.FontSize_Probe.ToString();
            FontSizeScanner.Text = ApplicationOptions.FontSize_Scanner.ToString();
        }

        private void PopulateLayoutOptions()
        {
            BackgroundColor_Probe_Inactive.Text = ApplicationOptions.BackgroundColor_Probe_Inactive;
            BackgroundColor_Probe_Up.Text = ApplicationOptions.BackgroundColor_Probe_Up;
            BackgroundColor_Probe_Down.Text = ApplicationOptions.BackgroundColor_Probe_Down;
            BackgroundColor_Probe_Error.Text = ApplicationOptions.BackgroundColor_Probe_Error;
            BackgroundColor_Probe_Indeterminate.Text = ApplicationOptions.BackgroundColor_Probe_Indeterminate;
            ForegroundColor_Probe_Inactive.Text = ApplicationOptions.ForegroundColor_Probe_Inactive;
            ForegroundColor_Probe_Up.Text = ApplicationOptions.ForegroundColor_Probe_Up;
            ForegroundColor_Probe_Down.Text = ApplicationOptions.ForegroundColor_Probe_Down;
            ForegroundColor_Probe_Error.Text = ApplicationOptions.ForegroundColor_Probe_Error;
            ForegroundColor_Probe_Indeterminate.Text = ApplicationOptions.ForegroundColor_Probe_Indeterminate;
            ForegroundColor_Stats_Inactive.Text = ApplicationOptions.ForegroundColor_Stats_Inactive;
            ForegroundColor_Stats_Up.Text = ApplicationOptions.ForegroundColor_Stats_Up;
            ForegroundColor_Stats_Down.Text = ApplicationOptions.ForegroundColor_Stats_Down;
            ForegroundColor_Stats_Error.Text = ApplicationOptions.ForegroundColor_Stats_Error;
            ForegroundColor_Stats_Indeterminate.Text = ApplicationOptions.ForegroundColor_Stats_Inactive;
            ForegroundColor_Alias_Inactive.Text = ApplicationOptions.ForegroundColor_Alias_Inactive;
            ForegroundColor_Alias_Up.Text = ApplicationOptions.ForegroundColor_Alias_Up;
            ForegroundColor_Alias_Down.Text = ApplicationOptions.ForegroundColor_Alias_Down;
            ForegroundColor_Alias_Error.Text = ApplicationOptions.ForegroundColor_Alias_Error;
            ForegroundColor_Alias_Indeterminate.Text = ApplicationOptions.ForegroundColor_Alias_Indeterminate;
            BackgroundColor_Probe_Scanner.Text = ApplicationOptions.BackgroundColor_Probe_Scanner;
            ForegroundColor_Probe_Scanner.Text = ApplicationOptions.ForegroundColor_Probe_Scanner;
            ForegroundColor_Alias_Scanner.Text = ApplicationOptions.ForegroundColor_Alias_Scanner;
        }


        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            ApplyOptions(closeAfterApply: false);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ApplyOptions(closeAfterApply: true);
        }

        private bool ApplyOptions(bool closeAfterApply)
        {
            var previousLanguageCode = ApplicationOptions.LanguageCode;
            var oldResources = LanguageRuntimeService.CaptureResourceSnapshot();

            if (SaveAllOptions() == false)
                return false;

            bool languageChanged = !string.Equals(
                previousLanguageCode,
                ApplicationOptions.LanguageCode,
                StringComparison.OrdinalIgnoreCase);

            if (languageChanged)
                LanguageRuntimeService.ApplyLanguage(ApplicationOptions.LanguageCode);

            Configuration.Save();

            ApplyRuntimeChanges(languageChanged, oldResources);
            SnapshotAcceptedPreviewState();

            if (closeAfterApply)
                DialogResult = true;

            return true;
        }

        private bool SaveAllOptions()
        {
            if (SaveGeneralOptions() == false) return false;
            if (SaveUpdateOptions() == false) return false;
            if (SaveNotificationOptions() == false) return false;
            if (SaveEmailAlertOptions() == false) return false;
            if (SaveAudioAlertOptions() == false) return false;
            if (SaveLogOutputOptions() == false) return false;
            if (SaveAdvancedOptions() == false) return false;
            if (SaveLayoutOptions() == false) return false;
            if (SaveDisplayOptions() == false) return false;

            return true;
        }

        private void ApplyRuntimeChanges(
            bool languageChanged,
            IReadOnlyDictionary<string, string> oldResources)
        {
            var mainWindow = HostMainWindow;
            mainWindow?.ApplyRuntimeOptionChanges(languageChanged, oldResources);

            if (languageChanged)
            {
                var newResources = LanguageRuntimeService.CaptureResourceSnapshot();
                RefreshLocalizedText(oldResources, newResources);
            }
        }

        private void RefreshLocalizedText(
            IReadOnlyDictionary<string, string> oldResources,
            IReadOnlyDictionary<string, string> newResources)
        {
            if (oldResources != null && newResources != null)
            {
                LocalizationRefreshService.Refresh(this, oldResources, newResources);

                if (LanguageComboBox != null)
                    PopulateLanguageOptions();
            }

            if (ApplyButton != null)
                ApplyButton.Content = Text("DialogButton_Apply", "Apply");

            if (SaveButton != null)
                SaveButton.Content = Text("DialogButton_Save", "Save");

            if (CancelButton != null)
                CancelButton.Content = Text("DialogButton_Cancel", "Cancel");

            if (LanguageApplyHintText != null)
            {
                LanguageApplyHintText.Text = Text(
                    "Options_LanguageApplyHint",
                    "Changes are applied when you click Apply or Save.");
            }
        }

        private void SnapshotAcceptedPreviewState()
        {
            _originalTheme = ApplicationOptions.Theme;
            _originalVisualStyle = ApplicationOptions.VisualStyle;
            _originalDisplayMode = ApplicationOptions.CurrentDisplayMode;
            _originalCompactSource = ApplicationOptions.CompactSource;
            _originalLanguageCode = ApplicationOptions.LanguageCode;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // If the user dismissed the window without clicking OK, revert the theme preview.
            if (DialogResult != true)
            {
                ApplicationOptions.Theme = _originalTheme;
                ThemeManager.ApplyTheme(ThemeManager.ParseTheme(_originalTheme));

                // Revert visual style preview.
                ApplicationOptions.VisualStyle = _originalVisualStyle;
                VisualStyleManager.ApplyStyle(VisualStyleManager.ParseStyle(_originalVisualStyle));

                // Revert compact source mode.
                if (ApplicationOptions.CompactSource != _originalCompactSource)
                {
                    ApplicationOptions.CompactSource = _originalCompactSource;
                    var mainWindow = HostMainWindow;
                    mainWindow?.ApplyCompactDataSource();
                    mainWindow?.UpdateCompactSourceMenuChecks();
                }

                // Revert display mode preview.
                if (ApplicationOptions.CurrentDisplayMode != _originalDisplayMode)
                {
                    (Owner as MainWindow)?.SwitchDisplayMode(_originalDisplayMode);
                }

                ApplicationOptions.LanguageCode = _originalLanguageCode;
                ApplicationOptions.Language = ApplicationOptions.ToLegacyLanguage(_originalLanguageCode);
            }
            base.OnClosing(e);
        }

        private bool SaveGeneralOptions()
        {
            if (PingInterval.Text.Length == 0)
            {
                ShowError(Properties.Strings.Options_Validation_PingInterval, GeneralTab, PingInterval);
                return false;
            }
            else if (PingTimeout.Text.Length == 0)
            {
                ShowError(Properties.Strings.Options_Validation_PingTimeout, GeneralTab, PingTimeout);
                return false;
            }
            else if (AlertThreshold.Text.Length == 0)
            {
                ShowError(Properties.Strings.Options_Validation_AlertThreshold, GeneralTab, AlertThreshold);
                return false;
            }

            // Ping interval.
            int pingInterval;
            int multiplier = 1000;

            switch (PingIntervalUnits.SelectedIndex)
            {
                case 0:
                    multiplier = 1000;
                    break;
                case 1:
                    multiplier = 1000 * 60;
                    break;
                case 2:
                    multiplier = 1000 * 60 * 60;
                    break;
            }

            if (int.TryParse(PingInterval.Text, out pingInterval) && pingInterval > 0 && pingInterval <= 86400)
            {
                pingInterval *= multiplier;
            }
            else
            {
                pingInterval = Constants.DefaultInterval;
            }
            ApplicationOptions.PingInterval = pingInterval;

            // Ping timeout.
            int pingTimeout;
            if (int.TryParse(PingTimeout.Text, out pingTimeout) && pingTimeout > 0 && pingTimeout <= 60)
            {
                pingTimeout *= 1000;
            }
            else
            {
                pingTimeout = Constants.DefaultTimeout;
            }
            ApplicationOptions.PingTimeout = pingTimeout;

            // Alert threshold.
            int alertThreshold;

            var isThresholdValid = int.TryParse(AlertThreshold.Text, out alertThreshold) && alertThreshold > 0 && alertThreshold <= 60;
            if (!isThresholdValid)
            {
                alertThreshold = 1;
            }

            ApplicationOptions.AlertThreshold = alertThreshold;

            // Latency detection mode.
            ApplicationOptions.LatencyDetectionMode = (ApplicationOptions.LatencyMode)LatencyDetectionMode.SelectedIndex;

            // High latency threshold in milliseconds.
            if (long.TryParse(HighLatencyMilliseconds.Text, out long highLatMs) && highLatMs >= 1)
            {
                ApplicationOptions.HighLatencyMilliseconds = highLatMs;
            }
            else
            {
                ApplicationOptions.HighLatencyMilliseconds = 50;
            }

            // High latency trigger count.
            // Note: "HighLatencyAlertTiggerCount" preserves the existing persisted XML key name (typo intentional — do not rename).
            if (int.TryParse(HighLatencyTriggerCount.Text, out int highLatCount) && highLatCount >= 1)
            {
                ApplicationOptions.HighLatencyAlertTiggerCount = highLatCount;
            }
            else
            {
                ApplicationOptions.HighLatencyAlertTiggerCount = 2;
            }

            // Startup mode.
            ApplicationOptions.InitialStartMode = (ApplicationOptions.StartMode)StartupMode.SelectedIndex;
            switch (StartupMode.SelectedIndex)
            {
                case ((int)ApplicationOptions.StartMode.Blank):
                case ((int)ApplicationOptions.StartMode.MultiInput):
                    // Initial probe count.
                    int count;
                    if (int.TryParse(InitialProbeCount.Text, out count))
                    {
                        if (count < 1)
                        {
                            count = 1;
                        }
                        else if (count > 20)
                        {
                            count = 2;
                        }
                    }
                    else
                    {
                        count = 2;
                    }
                    ApplicationOptions.InitialProbeCount = count;

                    // Initial column count.
                    if (int.TryParse(InitialColumnCount.Text, out count))
                    {
                        if (count < 1)
                        {
                            count = 1;
                        }
                        else if (count > 10)
                        {
                            count = 10;
                        }
                    }
                    else
                    {
                        count = 2;
                    }
                    ApplicationOptions.InitialColumnCount = count;
                    break;
                case ((int)ApplicationOptions.StartMode.Favorite):
                    // Initial favorite.
                    ApplicationOptions.InitialFavorite = InitialFavorite.Text;
                    break;
            }

            return true;
        }

        private bool SaveNotificationOptions()
        {
            if (IsAutoDismissEnabled.IsChecked == true)
            {
                if (int.TryParse(AutoDismissInterval.Text, out int result) && result > 0 && result < 100)
                {
                    ApplicationOptions.AutoDismissMilliseconds = result * 1000;
                    ApplicationOptions.IsAutoDismissEnabled = true;
                }
                else
                {
                    ShowError(Properties.Strings.Options_Validation_AutoDismiss, PopupAlertsTab, AutoDismissInterval);
                    return false;
                }
            }
            else
            {
                ApplicationOptions.IsAutoDismissEnabled = false;
            }

            if (PopupsMinimizedOption.IsChecked == true)
            {
                ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.WhenMinimized;
            }
            else if (PopupsAlwaysOption.IsChecked == true)
            {
                ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Always;
            }
            else
            {
                ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Never;
            }

            return true;
        }

        private bool SaveAdvancedOptions()
        {
            // Validate input.

            var regex = new Regex("^\\d+$");

            // Validate TTL.
            if (!regex.IsMatch(TTL.Text) || int.Parse(TTL.Text) < 1 || int.Parse(TTL.Text) > 255)
            {
                ShowError(Properties.Strings.Options_Validation_TTL, AdvancedTab, TTL);
                return false;
            }

            // Apply TTL.
            ApplicationOptions.TTL = int.Parse(TTL.Text);

            // Validate packet size.
            if (PacketSizeOption.IsChecked == true)
            {
                if (!regex.IsMatch(PacketSize.Text) || int.Parse(PacketSize.Text) < 0 || int.Parse(PacketSize.Text) > 65500)
                {
                    ShowError(Properties.Strings.Options_Validation_PacketSize, AdvancedTab, PacketSize);
                    return false;
                }

                // Apply packet size.
                ApplicationOptions.Buffer = new byte[int.Parse(PacketSize.Text)];
                ApplicationOptions.UseCustomBuffer = false;

                // Fill buffer with default text.
                if (ApplicationOptions.Buffer.Length >= 33)
                {
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(Constants.DefaultIcmpData), 0, ApplicationOptions.Buffer, 0, 33);
                }
            }
            else
            {
                // Use custom packet data.
                ApplicationOptions.Buffer = Encoding.ASCII.GetBytes(PacketData.Text);
                ApplicationOptions.UseCustomBuffer = true;
            }

            // Apply fragment / don't fragment option.
            if (DontFragment.IsChecked == true)
            {
                ApplicationOptions.DontFragment = true;
            }
            else
            {
                ApplicationOptions.DontFragment = false;
            }

            // Update ping options (TTL / Don't fragment settings)
            ApplicationOptions.UpdatePingOptions();

            return true;
        }

        private bool SaveEmailAlertOptions()
        {
            // Validate input.
            if (IsEmailAlertsEnabled.IsChecked == true)
            {
                var regex = new Regex("^\\d+$");

                if (SmtpServer.Text.Length == 0)
                {
                    ShowError(Properties.Strings.Options_Validation_SmtpServer, EmailAlertsTab, SmtpServer);
                    return false;
                }
                else if (SmtpPort.Text.Length == 0 || !regex.IsMatch(SmtpPort.Text))
                {
                    ShowError(Properties.Strings.Options_Validation_SmtpPort, EmailAlertsTab, SmtpPort);
                    return false;
                }
                else if (EmailRecipientAddress.Text.Length == 0)
                {
                    ShowError(Properties.Strings.Options_Validation_EmailRecipient, EmailAlertsTab, EmailRecipientAddress);
                    return false;
                }
                else if (EmailFromAddress.Text.Length == 0)
                {
                    ShowError(Properties.Strings.Options_Validation_EmailFrom, EmailAlertsTab, EmailFromAddress);
                    return false;
                }
                if (IsSmtpAuthenticationRequired.IsChecked == true)
                {
                    ApplicationOptions.IsEmailAuthenticationRequired = true;
                    if (SmtpUsername.Text.Length == 0)
                    {
                        ShowError(Properties.Strings.Options_Validation_SmtpUsername, EmailAlertsTab, SmtpUsername);
                        return false;
                    }
                }
                else
                {
                    ApplicationOptions.IsEmailAuthenticationRequired = false;
                    SmtpUsername.Text = string.Empty;
                    SmtpPassword.Password = string.Empty;
                }

                ApplicationOptions.IsEmailAlertEnabled = true;
                ApplicationOptions.EmailServer = SmtpServer.Text;
                ApplicationOptions.EmailPort = SmtpPort.Text;
                ApplicationOptions.EmailUser = SmtpUsername.Text;
                ApplicationOptions.EmailPassword = SmtpPassword.Password;
                ApplicationOptions.EmailRecipient = EmailRecipientAddress.Text;
                ApplicationOptions.EmailFromAddress = EmailFromAddress.Text;
                ApplicationOptions.IsEmailSslEnabled = IsSmtpSslEnabled.IsChecked == true ? true : false;

                return true;
            }
            else
            {
                ApplicationOptions.IsEmailAlertEnabled = false;
                return true;
            }
        }

        private bool SaveAudioAlertOptions()
        {
            if (IsAudioDownAlertEnabled.IsChecked == true)
            {
                try
                {
                    if (Path.GetFileName(AudioDownFilePath.Text).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        !File.Exists(AudioDownFilePath.Text) ||
                        Path.GetFileName(AudioDownFilePath.Text).Length < 1)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ShowError(Properties.Strings.Options_Validation_AudioPath, AudioAlertTab, AudioDownFilePath);
                    return false;
                }
                ApplicationOptions.IsAudioDownAlertEnabled = true;
                ApplicationOptions.AudioDownFilePath = AudioDownFilePath.Text;
            }
            else
            {
                ApplicationOptions.IsAudioDownAlertEnabled = false;
            }

            if (IsAudioUpAlertEnabled.IsChecked == true)
            {
                try
                {
                    if (Path.GetFileName(AudioUpFilePath.Text).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        !File.Exists(AudioUpFilePath.Text) ||
                        Path.GetFileName(AudioUpFilePath.Text).Length < 1)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ShowError(Properties.Strings.Options_Validation_AudioPath, AudioAlertTab, AudioUpFilePath);
                    return false;
                }
                ApplicationOptions.IsAudioUpAlertEnabled = true;
                ApplicationOptions.AudioUpFilePath = AudioUpFilePath.Text;
            }
            else
            {
                ApplicationOptions.IsAudioUpAlertEnabled = false;
            }
            if (IsAudioNetworkIdentityAlertEnabled.IsChecked == true)
            {
                try
                {
                    if (Path.GetFileName(AudioNetworkIdentityFilePath.Text).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        !File.Exists(AudioNetworkIdentityFilePath.Text) ||
                        Path.GetFileName(AudioNetworkIdentityFilePath.Text).Length < 1)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ShowError(Properties.Strings.Options_Validation_AudioPath, AudioAlertTab, AudioNetworkIdentityFilePath);
                    return false;
                }
                ApplicationOptions.IsAudioNetworkIdentityAlertEnabled = true;
                ApplicationOptions.AudioNetworkIdentityFilePath = AudioNetworkIdentityFilePath.Text;
            }
            else
            {
                ApplicationOptions.IsAudioNetworkIdentityAlertEnabled = false;
                ApplicationOptions.AudioNetworkIdentityFilePath = AudioNetworkIdentityFilePath.Text;
            }

            return true;
        }

        private bool SaveLogOutputOptions()
        {
            if (IsLogOutputEnabled.IsChecked == true)
            {
                string expandedLogPath = Classes.PortablePath.ExpandTokens(LogPath.Text);
                if (!Classes.PortablePath.EnsureDirectoryExists(expandedLogPath))
                {
                    ShowError(Properties.Strings.Options_Validation_LogPath, LogOutputTab, LogPath);
                    return false;
                }

                ApplicationOptions.IsLogOutputEnabled = true;
                ApplicationOptions.LogPath = LogPath.Text;
            }
            else
            {
                ApplicationOptions.IsLogOutputEnabled = false;
            }

            if (IsLogStatusChangesEnabled.IsChecked == true)
            {
                try
                {
                    // Auto-append default filename when user entered a directory-only path.
                    string rawStatusPath = LogStatusChangesPath.Text;
                    string expandedStatusPath = Classes.PortablePath.ExpandTokens(rawStatusPath);
                    if (string.IsNullOrEmpty(Path.GetFileName(expandedStatusPath)))
                    {
                        rawStatusPath = Path.Combine(rawStatusPath, "multipingmonitor-status.txt");
                        expandedStatusPath = Classes.PortablePath.ExpandTokens(rawStatusPath);
                        LogStatusChangesPath.Text = rawStatusPath;
                    }

                    string fileName = Path.GetFileName(expandedStatusPath);
                    if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        fileName.Length < 1)
                    {
                        throw new Exception();
                    }

                    if (!Classes.PortablePath.EnsureParentDirectoryExists(expandedStatusPath))
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ShowError(Properties.Strings.Options_Validation_LogPath, LogOutputTab, LogStatusChangesPath);
                    return false;
                }

                ApplicationOptions.IsLogStatusChangesEnabled = true;
                ApplicationOptions.LogStatusChangesPath = LogStatusChangesPath.Text;
            }
            else
            {
                ApplicationOptions.IsLogStatusChangesEnabled = false;
            }

            return true;
        }

        private void PopulateLanguageOptions()
        {
            LanguageComboBox.Items.Clear();

            AddLanguageOption(LanguageRuntimeService.SystemLanguageCode, Properties.Strings.Language_System);
            AddLanguageOption(LanguageRuntimeService.EnglishLanguageCode, Properties.Strings.Language_English);

            foreach (var pack in LanguagePackService.DiscoverLanguagePacks())
            {
                AddLanguageOption(pack.LanguageCode, pack.ToString());
            }

            SelectLanguageOption(ApplicationOptions.LanguageCode);
            if (LanguageComboBox.SelectedIndex < 0)
                SelectLanguageOption(LanguageRuntimeService.SystemLanguageCode);
        }

        private void AddLanguageOption(string languageCode, string displayName)
        {
            var normalizedLanguageCode = LanguageRuntimeService.NormalizeLanguageCode(languageCode);

            foreach (var existingItem in LanguageComboBox.Items)
            {
                if (existingItem is System.Windows.Controls.ComboBoxItem existingComboBoxItem
                    && existingComboBoxItem.Tag is string existingLanguageCode
                    && string.Equals(existingLanguageCode, normalizedLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            LanguageComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem
            {
                Content = displayName,
                Tag = normalizedLanguageCode,
            });
        }

        private void SelectLanguageOption(string languageCode)
        {
            var normalizedLanguageCode = LanguageRuntimeService.NormalizeLanguageCode(languageCode);

            foreach (var item in LanguageComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboBoxItem
                    && comboBoxItem.Tag is string itemLanguageCode
                    && string.Equals(itemLanguageCode, normalizedLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageComboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }
        }

        private bool SaveDisplayOptions()
        {
            ApplicationOptions.IsAlwaysOnTopEnabled = IsAlwaysOnTopEnabled.IsChecked == true;
            ApplicationOptions.IsMinimizeToTrayEnabled = IsMinimizeToTrayEnabled.IsChecked == true;
            ApplicationOptions.IsExitToTrayEnabled = IsExitToTrayEnabled.IsChecked == true;
            ApplicationOptions.StartInTray = StartInTray.IsChecked == true;
            ApplicationOptions.RememberWindowPosition = RememberWindowPosition.IsChecked == true;

            // Save language selection.
            if (LanguageComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedLanguageItem
                && selectedLanguageItem.Tag is string selectedLanguageCode)
            {
                ApplicationOptions.LanguageCode = LanguageRuntimeService.NormalizeLanguageCode(selectedLanguageCode);
            }
            else
            {
                ApplicationOptions.LanguageCode = LanguageRuntimeService.SystemLanguageCode;
            }

            ApplicationOptions.Language = ApplicationOptions.ToLegacyLanguage(ApplicationOptions.LanguageCode);

            // Validate and save font sizes.
            if (!int.TryParse(FontSizeProbe.Text, out int fontSizeProbe) || fontSizeProbe < 6 || fontSizeProbe > 72)
            {
                ShowError(Properties.Strings.Options_Validation_FontSizeProbe, DisplayTab, FontSizeProbe);
                return false;
            }
            if (!int.TryParse(FontSizeScanner.Text, out int fontSizeScanner) || fontSizeScanner < 6 || fontSizeScanner > 72)
            {
                ShowError(Properties.Strings.Options_Validation_FontSizeScanner, DisplayTab, FontSizeScanner);
                return false;
            }
            ApplicationOptions.FontSize_Probe = fontSizeProbe;
            ApplicationOptions.FontSize_Scanner = fontSizeScanner;

            // Save compact source mode.
            ApplicationOptions.CompactSource = (ApplicationOptions.CompactSourceMode)CompactSourceComboBox.SelectedIndex;
            var mainWindow = HostMainWindow;
            mainWindow?.ApplyCompactDataSource();
            mainWindow?.UpdateCompactSourceMenuChecks();

            return true;
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is string themeName)
            {
                ApplicationOptions.Theme = themeName;
                ThemeManager.ApplyTheme(ThemeManager.ParseTheme(themeName));
            }
        }

        private void VisualStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VisualStyleComboBox.SelectedItem is string styleName)
            {
                ApplicationOptions.VisualStyle = styleName;
                VisualStyleManager.ApplyStyle(VisualStyleManager.ParseStyle(styleName));
            }
        }

        private void DisplayModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DisplayModeComboBox.SelectedIndex < 0) return;
            var mode = (ApplicationOptions.DisplayMode)DisplayModeComboBox.SelectedIndex;
            if (mode == ApplicationOptions.CurrentDisplayMode) return;
            HostMainWindow?.SwitchDisplayMode(mode);
        }

        private void CompactSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CompactSourceComboBox.SelectedIndex < 0) return;
            UpdateCompactTargetsButtonVisibility();

            // Live-apply compact source change.
            var source = (ApplicationOptions.CompactSourceMode)CompactSourceComboBox.SelectedIndex;
            if (source == ApplicationOptions.CompactSource) return;
            ApplicationOptions.CompactSource = source;
            HostMainWindow?.ApplyCompactDataSource();
        }

        private void ManageCompactTargets_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = HostMainWindow;
            mainWindow?.OpenManageCompactSets(this);
        }

        private void UpdateCompactTargetsButtonVisibility()
        {
            // Manage Compact Sets button is always enabled since sets are independent.
            if (ManageCompactTargetsButton != null)
            {
                ManageCompactTargetsButton.IsEnabled = true;
            }
        }

        private bool SaveLayoutOptions()
        {
            // Validate input.
            foreach (var control in ColorsDockPanel.GetChildren())
            {
                if (control is TextBox box)
                {
                    if (!Util.IsValidHtmlColor(box.Text))
                    {
                        ShowError(Properties.Strings.Options_Validation_HtmlColor, LayoutTab, box);
                        box.SelectAll();

                        return false;
                    }
                }
            }

            ApplicationOptions.BackgroundColor_Probe_Inactive = BackgroundColor_Probe_Inactive.Text;
            ApplicationOptions.BackgroundColor_Probe_Up = BackgroundColor_Probe_Up.Text;
            ApplicationOptions.BackgroundColor_Probe_Down = BackgroundColor_Probe_Down.Text;
            ApplicationOptions.BackgroundColor_Probe_Indeterminate = BackgroundColor_Probe_Indeterminate.Text;
            ApplicationOptions.BackgroundColor_Probe_Error = BackgroundColor_Probe_Error.Text;
            ApplicationOptions.ForegroundColor_Probe_Inactive = ForegroundColor_Probe_Inactive.Text;
            ApplicationOptions.ForegroundColor_Probe_Up = ForegroundColor_Probe_Up.Text;
            ApplicationOptions.ForegroundColor_Probe_Down = ForegroundColor_Probe_Down.Text;
            ApplicationOptions.ForegroundColor_Probe_Indeterminate = ForegroundColor_Probe_Indeterminate.Text;
            ApplicationOptions.ForegroundColor_Probe_Error = ForegroundColor_Probe_Error.Text;
            ApplicationOptions.ForegroundColor_Stats_Inactive = ForegroundColor_Stats_Inactive.Text;
            ApplicationOptions.ForegroundColor_Stats_Up = ForegroundColor_Stats_Up.Text;
            ApplicationOptions.ForegroundColor_Stats_Down = ForegroundColor_Stats_Down.Text;
            ApplicationOptions.ForegroundColor_Stats_Indeterminate = ForegroundColor_Stats_Indeterminate.Text;
            ApplicationOptions.ForegroundColor_Stats_Error = ForegroundColor_Stats_Error.Text;
            ApplicationOptions.ForegroundColor_Alias_Inactive = ForegroundColor_Alias_Inactive.Text;
            ApplicationOptions.ForegroundColor_Alias_Up = ForegroundColor_Alias_Up.Text;
            ApplicationOptions.ForegroundColor_Alias_Down = ForegroundColor_Alias_Down.Text;
            ApplicationOptions.ForegroundColor_Alias_Indeterminate = ForegroundColor_Alias_Indeterminate.Text;
            ApplicationOptions.ForegroundColor_Alias_Error = ForegroundColor_Alias_Error.Text;
            ApplicationOptions.BackgroundColor_Probe_Scanner = BackgroundColor_Probe_Scanner.Text;
            ApplicationOptions.ForegroundColor_Probe_Scanner = ForegroundColor_Probe_Scanner.Text;
            ApplicationOptions.ForegroundColor_Alias_Scanner = ForegroundColor_Alias_Scanner.Text;

            return true;
        }


        private void NumericTextbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9.-]+");
            if (regex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void HtmlColor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[#a-fA-F0-9]");
            if (!regex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void EmailRecipientAddress_LostFocus(object sender, RoutedEventArgs e)
        {
            if (EmailFromAddress.Text.Length == 0 && EmailRecipientAddress.Text.IndexOf('@') >= 0)
            {
                EmailFromAddress.Text = "MultiPingMonitor" + EmailRecipientAddress.Text.Substring(EmailRecipientAddress.Text.IndexOf('@'));
            }
        }

        private void IsEmailAlertsEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (IsEmailAlertsEnabled.IsChecked == true && SmtpServer.Text.Length == 0)
            {
                SmtpServer.Focus();
            }
        }

        private void IsSmtpAuthenticationRequired_Click(object sender, RoutedEventArgs e)
        {
            if (IsSmtpAuthenticationRequired.IsChecked == true)
            {
                SmtpUsername.Focus();
            }
        }

        private async void TestEmail_Click(object sender, RoutedEventArgs e)
        {
            TestEmailButton.IsEnabled = false;
            TestEmailButton.Content = Properties.Strings.Options_Email_Test_Sending;
            var serverAddress = SmtpServer.Text;
            var serverPort = SmtpPort.Text;
            var isSslEnabled = IsSmtpSslEnabled.IsChecked == true;
            var isAuthRequired = IsSmtpAuthenticationRequired.IsChecked == true;
            var username = SmtpUsername.Text;
            var password = SmtpPassword.SecurePassword;
            var mailFrom = EmailFromAddress.Text;
            var mailRecipient = EmailRecipientAddress.Text;

            await Task.Run(() =>
            {
                try
                {
                    Util.SendTestEmail(
                        serverAddress,
                        serverPort,
                        isSslEnabled,
                        isAuthRequired,
                        username,
                        password,
                        mailFrom,
                        mailRecipient);
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            if (IsLoaded)
                            {
                                var dialogWindow = new DialogWindow(
                                    DialogWindow.DialogIcon.Info,
                                    Properties.Strings.Options_Email_Test_Title,
                                    Properties.Strings.Options_Email_Test_Success,
                                    Properties.Strings.DialogButton_OK,
                                    false)
                                {
                                    Owner = this
                                };
                                dialogWindow.ShowDialog();
                            }
                        }));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            if (IsLoaded)
                            {
                                ShowError(string.Format(Properties.Strings.Options_Email_Test_Failed, ex.Message), EmailAlertsTab, TestEmailButton);
                            }
                        }));
                }
            });
            TestEmailButton.IsEnabled = true;
            TestEmailButton.Content = Properties.Strings.Options_Test;
        }

        private void BrowseLogPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = Properties.Strings.Options_Log_SelectFolder;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    LogPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowseLogStatusChangesPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = Properties.Strings.Options_Log_SelectFolder;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    LogStatusChangesPath.Text = dialog.SelectedPath + "\\multipingmonitor-status.txt";
                }
            }
        }

        private void InsertAppDataLogPath_Click(object sender, RoutedEventArgs e)
        {
            InsertAppDataToken(LogPath, @"%APPDATA%\");
        }

        private void InsertAppDataLogStatusChangesPath_Click(object sender, RoutedEventArgs e)
        {
            InsertAppDataToken(LogStatusChangesPath, @"%APPDATA%\");
        }

        private static void InsertAppDataToken(System.Windows.Controls.TextBox textBox, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = defaultValue;
            }
            else
            {
                // Insert the token at the current caret position.
                int caret = textBox.CaretIndex;
                string token = Classes.PortablePath.AppDataToken;
                textBox.Text = textBox.Text.Insert(caret, token);
                textBox.CaretIndex = caret + token.Length;
            }
            textBox.Focus();
        }

        private void AudioDownBrowse_Click(object sender, RoutedEventArgs e)
        {
            AudioFileBrowse(AudioDownFilePath);
        }

        private void AudioUpBrowse_Click(object sender, RoutedEventArgs e)
        {
            AudioFileBrowse(AudioUpFilePath);
        }

        private void AudioNetworkIdentityBrowse_Click(object sender, RoutedEventArgs e)
        {
            AudioFileBrowse(AudioNetworkIdentityFilePath);
        }

        private void AudioFileBrowse(TextBox tb)
        {
            using (var audiofileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                audiofileDialog.Title = Properties.Strings.Options_Audio_SelectFile_Title;
                audiofileDialog.RestoreDirectory = true;
                audiofileDialog.Multiselect = false;
                audiofileDialog.Filter = Properties.Strings.Options_Audio_SelectFile_Filter;
                audiofileDialog.DefaultExt = ".wav";

                if (audiofileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    tb.Text = audiofileDialog.FileName;
                }
            }
        }

        private void AudioDownPlay_Click(object sender, RoutedEventArgs e)
        {
            AudioFilePlay(AudioDownFilePath.Text);
        }

        private void AudioUpPlay_Click(object sender, RoutedEventArgs e)
        {
            AudioFilePlay(AudioUpFilePath.Text);
        }

        private void AudioNetworkIdentityPlay_Click(object sender, RoutedEventArgs e)
        {
            AudioFilePlay(AudioNetworkIdentityFilePath.Text);
        }

        private void AudioFilePlay(string path)
        {
            try
            {
                using (var player = new SoundPlayer(path))
                {
                    player.Play();
                }
            }
            catch
            {
                ShowError(Properties.Strings.Options_Audio_PlayError, AudioAlertTab, AudioAlertTab);
            }
        }

        private void IsAudioDownAlertEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (AudioDownFilePath.Text.Length == 0)
            {
                if (File.Exists(Environment.ExpandEnvironmentVariables(Constants.DefaultAudioDownFilePath)))
                {
                    AudioDownFilePath.Text = Environment.ExpandEnvironmentVariables(Constants.DefaultAudioDownFilePath);
                }
            }
        }

        private void IsAudioNetworkIdentityAlertEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (AudioNetworkIdentityFilePath.Text.Length == 0)
            {
                if (File.Exists(Environment.ExpandEnvironmentVariables(Constants.DefaultAudioNetworkIdentityFilePath)))
                {
                    AudioNetworkIdentityFilePath.Text = Environment.ExpandEnvironmentVariables(Constants.DefaultAudioNetworkIdentityFilePath);
                }
            }
        }

        private void IsAudioUpAlertEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (AudioUpFilePath.Text.Length == 0)
            {
                if (File.Exists(Environment.ExpandEnvironmentVariables(Constants.DefaultAudioUpFilePath)))
                {
                    AudioUpFilePath.Text = Environment.ExpandEnvironmentVariables(Constants.DefaultAudioUpFilePath);
                }
            }
        }

        private void UpdateByteCount()
        {
            var regex = new Regex("^\\d+$");
            if (PacketSizeOption.IsChecked == true)
            {
                if (PacketSize != null && regex.IsMatch(PacketSize.Text))
                {
                    Bytes.Text = (int.Parse(PacketSize.Text) + 28).ToString();
                }
                else
                {
                    Bytes.Text = "?";
                }
            }
            else
            {
                Bytes.Text = (PacketData.Text.Length + 28).ToString();
            }
        }

        private void PacketData_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateByteCount();
        }

        private void PacketSizeOption_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateByteCount();
            }
        }

        private void UseCustomPacketOption_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateByteCount();
            }
        }

        private void RestoreDefaultColors_Click(object sender, RoutedEventArgs e)
        {
            BackgroundColor_Probe_Inactive.Text = Constants.Color_Probe_Background_Inactive;
            BackgroundColor_Probe_Up.Text = Constants.Color_Probe_Background_Up;
            BackgroundColor_Probe_Down.Text = Constants.Color_Probe_Background_Down;
            BackgroundColor_Probe_Error.Text = Constants.Color_Probe_Background_Error;
            BackgroundColor_Probe_Indeterminate.Text = Constants.Color_Probe_Background_Indeterminate;
            ForegroundColor_Probe_Inactive.Text = Constants.Color_Probe_Foreground_Inactive;
            ForegroundColor_Probe_Up.Text = Constants.Color_Probe_Foreground_Up;
            ForegroundColor_Probe_Down.Text = Constants.Color_Probe_Foreground_Down;
            ForegroundColor_Probe_Error.Text = Constants.Color_Probe_Foreground_Error;
            ForegroundColor_Probe_Indeterminate.Text = Constants.Color_Probe_Foreground_Indeterminate;
            ForegroundColor_Stats_Inactive.Text = Constants.Color_Statistics_Foreground_Inactive;
            ForegroundColor_Stats_Up.Text = Constants.Color_Statistics_Foreground_Up;
            ForegroundColor_Stats_Down.Text = Constants.Color_Statistics_Foreground_Down;
            ForegroundColor_Stats_Error.Text = Constants.Color_Statistics_Foreground_Error;
            ForegroundColor_Stats_Indeterminate.Text = Constants.Color_Statistics_Foreground_Inactive;
            ForegroundColor_Alias_Inactive.Text = Constants.Color_Alias_Foreground_Inactive;
            ForegroundColor_Alias_Up.Text = Constants.Color_Alias_Foreground_Up;
            ForegroundColor_Alias_Down.Text = Constants.Color_Alias_Foreground_Down;
            ForegroundColor_Alias_Error.Text = Constants.Color_Alias_Foreground_Error;
            ForegroundColor_Alias_Indeterminate.Text = Constants.Color_Alias_Foreground_Indeterminate;
            BackgroundColor_Probe_Scanner.Text = Constants.Color_Probe_Background_Scanner;
            ForegroundColor_Probe_Scanner.Text = Constants.Color_Probe_Foreground_Scanner;
            ForegroundColor_Alias_Scanner.Text = Constants.Color_Alias_Foreground_Scanner;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshTitleBarChromeLocalization()
        {
            SetTitleBarButtonText(titleBarCloseButton, "Tooltip_Close", "Close");
        }

        private static string TitleBarResourceText(string key, string fallback)
        {
            return MultiPingMonitor.Properties.Strings.ResourceManager.GetString(key) ?? fallback;
        }

        private static void SetTitleBarButtonText(System.Windows.Controls.Button button, string key, string fallback)
        {
            string text = TitleBarResourceText(key, fallback);
            button.ToolTip = text;
            System.Windows.Automation.AutomationProperties.SetName(button, text);
        }


    }
}
