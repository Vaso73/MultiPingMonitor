using System.Collections.Generic;
using System.Windows;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    /// <summary>
    /// Compact quick-remove dialog for removing a host from the active Compact Set.
    /// Shows the current targets in a list, lets the user pick one, and optionally
    /// closes the probe's open Live Ping window on removal.
    /// </summary>
    public partial class RemoveCompactHostDialog : Window
    {
        /// <summary>The entry selected by the user, or null if none.</summary>
        public CompactTargetEntry SelectedEntry => HostListBox.SelectedItem as CompactTargetEntry;

        /// <summary>Whether the user checked "Close open Live Ping window".</summary>
        public bool CloseLivePing => CloseLivePingCheckBox.IsChecked == true;

        public RemoveCompactHostDialog(IEnumerable<CompactTargetEntry> entries)
        {
            InitializeComponent();

            var list = entries != null ? new List<CompactTargetEntry>(entries) : new List<CompactTargetEntry>();
            HostListBox.ItemsSource = list;

            if (list.Count == 0)
            {
                HostListBox.Visibility = Visibility.Collapsed;
                NoHostsText.Visibility = Visibility.Visible;
                RemoveButton.IsEnabled = false;
            }
            else
            {
                HostListBox.SelectedIndex = 0;
            }

            Loaded += (s, e) => HostListBox.Focus();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntry == null)
                return;
            DialogResult = true;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
