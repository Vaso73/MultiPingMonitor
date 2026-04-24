using System.Windows;

namespace MultiPingMonitor.UI
{
    /// <summary>
    /// Compact quick-add dialog for adding a new host directly to the active Compact Set.
    /// Provides a host field, an optional alias field, and an optional "Open Live Ping after adding" checkbox.
    /// </summary>
    public partial class AddCompactHostDialog : Window
    {
        /// <summary>The trimmed host/IP/domain entered by the user.</summary>
        public string Host => HostField.Text?.Trim() ?? string.Empty;

        /// <summary>The trimmed alias entered by the user (may be empty).</summary>
        public string Alias => AliasField.Text?.Trim() ?? string.Empty;

        /// <summary>Whether the user checked "Open Live Ping after adding".</summary>
        public bool OpenLivePing => OpenLivePingCheckBox.IsChecked == true;

        public AddCompactHostDialog()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                HostField.Focus();
            };
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
