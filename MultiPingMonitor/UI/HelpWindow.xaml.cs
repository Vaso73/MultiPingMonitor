using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class HelpWindow : Window
    {
        public static HelpWindow _OpenWindow = null;

        public HelpWindow()
        {
            InitializeComponent();
            WindowPlacementService.Attach(this, "HelpWindow");
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;

            Version version = typeof(MainWindow).Assembly.GetName().Version;
            Version.Inlines.Clear();
            Version.Inlines.Add(new Run($"Version: {version.Major}.{version.Minor}.{version.Build}"));

            // Generate copyright text based on the current year.
            Copyright.Inlines.Clear();
            Copyright.Inlines.Add(new Run($"Copyright \u00a9 {DateTime.Now.Year.ToString()} Vaso73"));

            // Localize all body text from string resources.
            ApplyLocalizedHelpContent();

            // Set initial focus to scrollviewer.  That way you can scroll the help window with the keyboard
            // without having to first click in the window.
            MainDocument.Focus();
        }

        private void ApplyLocalizedHelpContent()
        {
            // Intro
            Intro.Inlines.Clear();
            Intro.Inlines.Add(new Run(Properties.Strings.Help_Intro));

            // Basic Usage section header
            BasicUsage.Inlines.Clear();
            BasicUsage.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_Header));

            // Application overview sub-header
            Sub_AppOverview.Inlines.Clear();
            Sub_AppOverview.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_AppOverview_Sub));

            // Monitor a host
            Sub_MonitorHost.Inlines.Clear();
            Sub_MonitorHost.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_MonitorHost_Sub));
            Para_MonitorHost.Inlines.Clear();
            Para_MonitorHost.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_MonitorHost_Text));

            // Add a host monitor
            Sub_AddHost.Inlines.Clear();
            Sub_AddHost.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_AddHost_Sub));
            Para_AddHost.Inlines.Clear();
            Para_AddHost.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_AddHost_Prefix + " "));
            var addIconSource = (ImageSource)TryFindResource("icon.add");
            if (addIconSource != null)
            {
                var addImage = new Image { Source = addIconSource, Width = 14, Height = 14 };
                Para_AddHost.Inlines.Add(new InlineUIContainer(addImage));
            }
            Para_AddHost.Inlines.Add(new Run(" "));
            Para_AddHost.Inlines.Add(new Bold(new Run(Properties.Strings.Help_BasicUsage_AddHost_Button)));
            Para_AddHost.Inlines.Add(new Run(" " + Properties.Strings.Help_BasicUsage_AddHost_Suffix + " "));
            Para_AddHost.Inlines.Add(new Bold(new Run("Alt-A")));
            Para_AddHost.Inlines.Add(new Run("."));

            // Remove a host monitor
            Sub_RemoveHost.Inlines.Clear();
            Sub_RemoveHost.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_RemoveHost_Sub));
            Para_RemoveHost.Inlines.Clear();
            Para_RemoveHost.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_RemoveHost_Text));

            // Columns
            Sub_Columns.Inlines.Clear();
            Sub_Columns.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_Columns_Sub));
            Para_Columns.Inlines.Clear();
            Para_Columns.Inlines.Add(new Run(Properties.Strings.Help_BasicUsage_Columns_Text));

            // Extra Features section header
            ExtraFeatures.Inlines.Clear();
            ExtraFeatures.Inlines.Add(new Run(Properties.Strings.Help_ExtraFeatures_Header));

            // Port Monitor
            Sub_PortMonitor.Inlines.Clear();
            Sub_PortMonitor.Inlines.Add(new Run(Properties.Strings.Help_PortMonitor_Sub));
            Para_PortMonitor.Inlines.Clear();
            Para_PortMonitor.Inlines.Add(new Run(Properties.Strings.Help_PortMonitor_Text_Before + " "));
            Para_PortMonitor.Inlines.Add(new Bold(new Run("SERVER01:80")));
            Para_PortMonitor.Inlines.Add(new Run(Properties.Strings.Help_PortMonitor_Text_After));

            // Traceroute
            Sub_TraceRoute.Inlines.Clear();
            Sub_TraceRoute.Inlines.Add(new Run(Properties.Strings.Help_TraceRoute_Sub));
            Para_TraceRoute.Inlines.Clear();
            Para_TraceRoute.Inlines.Add(new Run(Properties.Strings.Help_TraceRoute_Text));

            // Flood Host
            Sub_FloodHostHelp.Inlines.Clear();
            Sub_FloodHostHelp.Inlines.Add(new Run(Properties.Strings.Help_FloodHost_Sub));
            Para_FloodHostHelp.Inlines.Clear();
            Para_FloodHostHelp.Inlines.Add(new Run(Properties.Strings.Help_FloodHost_Text));

            // Options section header
            Options.Inlines.Clear();
            Options.Inlines.Add(new Run(Properties.Strings.Help_Options_Header));

            // Ping interval
            Sub_PingIntervalHelp.Inlines.Clear();
            Sub_PingIntervalHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_PingInterval_Sub));
            Para_PingIntervalHelp.Inlines.Clear();
            Para_PingIntervalHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_PingInterval_Text));

            // Ping timeout
            Sub_PingTimeoutHelp.Inlines.Clear();
            Sub_PingTimeoutHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_PingTimeout_Sub));
            Para_PingTimeoutHelp.Inlines.Clear();
            Para_PingTimeoutHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_PingTimeout_Text));

            // Send email
            Sub_SendEmailHelp.Inlines.Clear();
            Sub_SendEmailHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_Email_Sub));
            Para_SendEmailHelp.Inlines.Clear();
            Para_SendEmailHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_Email_Text));

            // Always on top
            Sub_AlwaysOnTopHelp.Inlines.Clear();
            Sub_AlwaysOnTopHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_AlwaysOnTop_Sub));
            Para_AlwaysOnTopHelp.Inlines.Clear();
            Para_AlwaysOnTopHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_AlwaysOnTop_Text));

            // Log output
            Sub_LogOutputHelp.Inlines.Clear();
            Sub_LogOutputHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_Logging_Sub));
            Para_LogOutputHelp.Inlines.Clear();
            Para_LogOutputHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_Logging_Text));

            // Start in tray
            Sub_StartInTrayHelp.Inlines.Clear();
            Sub_StartInTrayHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_StartInTray_Sub));
            Para_StartInTrayHelp.Inlines.Clear();
            Para_StartInTrayHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_StartInTray_Text));

            // Language
            Sub_LanguageHelp.Inlines.Clear();
            Sub_LanguageHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_Language_Sub));
            Para_LanguageHelp.Inlines.Clear();
            Para_LanguageHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_Language_Text));

            // Portable configuration
            Sub_PortableConfigHelp.Inlines.Clear();
            Sub_PortableConfigHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_PortableConfig_Sub));
            Para_PortableConfigHelp.Inlines.Clear();
            Para_PortableConfigHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_PortableConfig_Text));

            // Tray behavior
            Sub_TrayBehaviorHelp.Inlines.Clear();
            Sub_TrayBehaviorHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_TrayBehavior_Sub));
            Para_TrayBehaviorHelp.Inlines.Clear();
            Para_TrayBehaviorHelp.Inlines.Add(new Run(Properties.Strings.Help_Options_TrayBehavior_Text));

            // Command Line Usage section header
            CommandLineUsage.Inlines.Clear();
            CommandLineUsage.Inlines.Add(new Run(Properties.Strings.Help_CommandLine_Header));

            // Command line usage sub-header
            Sub_CommandLineUsageHelp.Inlines.Clear();
            Sub_CommandLineUsageHelp.Inlines.Add(new Run(Properties.Strings.Help_CommandLine_Usage));
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Silently handle if the browser cannot be launched.
            }
            finally
            {
                e.Handled = true;
            }
        }

        private void Intro_Selected(object sender, RoutedEventArgs e)
        {
            Intro.BringIntoView();
        }

        private void BasicUsage_Selected(object sender, RoutedEventArgs e)
        {
            BasicUsage.BringIntoView();
        }

        private void ExtraFeatures_Selected(object sender, RoutedEventArgs e)
        {
            ExtraFeatures.BringIntoView();
        }

        private void Options_Selected(object sender, RoutedEventArgs e)
        {
            Options.BringIntoView();
        }

        private void CommandLineUsage_Selected(object sender, RoutedEventArgs e)
        {
            CommandLineUsage.BringIntoView();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _OpenWindow = this;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _OpenWindow = null;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Constants.HelpKeyBinding)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (maximizeButton != null && restoreButton != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    maximizeButton.Visibility = Visibility.Collapsed;
                    restoreButton.Visibility = Visibility.Visible;
                }
                else
                {
                    maximizeButton.Visibility = Visibility.Visible;
                    restoreButton.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
