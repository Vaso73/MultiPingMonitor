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
            WindowPlacementService.Attach(this, "UsageWindow");

            Version version = typeof(MainWindow).Assembly.GetName().Version;
            AppVersion.Text = $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
