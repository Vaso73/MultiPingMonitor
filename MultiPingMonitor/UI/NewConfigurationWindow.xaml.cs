using System.Windows;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class NewConfigurationWindow : Window
    {
        public NewConfigurationWindow()
        {
            InitializeComponent();
            RefreshTitleBarChromeLocalization();

            FilePath.Text = Configuration.FilePath;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
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
