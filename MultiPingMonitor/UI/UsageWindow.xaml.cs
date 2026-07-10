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
            StartMinimizedDescriptionText.Text = UsageResourceText(
                "Usage_StartMinimizedDescription",
                "Start the application in a minimized state.");
        }

        private static string UsageResourceText(string key, string fallback)
        {
            return MultiPingMonitor.Properties.Strings.ResourceManager.GetString(key) ?? fallback;
        }


    }
}
