using System;
using System.Windows;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class UsageWindow : Window
    {
        public UsageWindow()
        {
            InitializeComponent();
            RefreshUsageWindowLocalization();
            WindowPlacementService.Attach(this, "UsageWindow");

            Version version = typeof(MainWindow).Assembly.GetName().Version;
            AppVersion.Text = $"{version.Major}.{version.Minor}.{version.Build}";
        }

        private void RefreshUsageWindowLocalization()
        {
            CommandLineUsageHeader.Text = UsageResourceText(
                "Help_CommandLine_Header",
                "Command Line Usage");
            OptionsHeader.Text = UsageResourceText(
                "Usage_OptionsHeader",
                "Options");

            IntervalDescriptionText.Text = UsageResourceText(
                "Usage_IntervalDescription",
                "Specifies the interval, in seconds, between pings.");
            IntervalRangeText.Text = UsageResourceText(
                "Usage_IntervalRange",
                "Valid range: 1 to 86400 (1 second to 24 hours).");

            TimeoutDescriptionText.Text = UsageResourceText(
                "Usage_TimeoutDescription",
                "Set the timeout, in seconds, for each ping request.");
            TimeoutRangeText.Text = UsageResourceText(
                "Usage_TimeoutRange",
                "Valid range: 1 to 60 seconds.");

            StartMinimizedDescriptionText.Text = UsageResourceText(
                "Usage_StartMinimizedDescription",
                "Start the application in a minimized state.");

            HostnameDescriptionText.Text = UsageResourceText(
                "Usage_HostnameDescription",
                "A hostname or IP address to ping.");
            MultipleHostnamesDescriptionText.Text = UsageResourceText(
                "Usage_MultipleHostnamesDescription",
                "You can provide multiple hostnames.");
            FileDescriptionText.Text = UsageResourceText(
                "Usage_FileDescription",
                "The path to a text file containing hostnames or IP addresses to ping. Wrap the path in quotes if it contains spaces. You can provide multiple files.");

            ExamplesHeader.Text = UsageResourceText(
                "Usage_ExamplesHeader",
                "Examples");
        }

        private static string UsageResourceText(string key, string fallback)
        {
            return MultiPingMonitor.Properties.Strings.ResourceManager.GetString(key) ?? fallback;
        }


    }
}
