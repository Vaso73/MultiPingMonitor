using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class AddToSetDialog : Window
    {
        public enum DestinationType { Normal, Compact }

        public DestinationType SelectedDestination { get; private set; }
        public CompactTargetSet SelectedCompactSet { get; private set; }
        public string Alias => AliasTextBox.Text.Trim();

        private readonly string _target;
        private readonly List<CompactTargetSet> _compactSets;

        public string TargetDisplayText => $"Target:  {_target}";

        public AddToSetDialog(string target, Window owner)
        {
            _target = target;
            _compactSets = ApplicationOptions.CompactSets.ToList();

            InitializeComponent();
            DataContext = this;
            Owner = owner;

            Loaded += (_, _) => VisualStyleManager.ApplyNativeWindowCorners(this);

            // Populate compact set combo.
            CompactSetCombo.ItemsSource = _compactSets.Select(s => s.Name).ToList();
            if (_compactSets.Count > 0)
                CompactSetCombo.SelectedIndex = 0;
        }

        private void DestType_Changed(object sender, RoutedEventArgs e)
        {
            if (CompactSetPanel == null) return;
            CompactSetPanel.Visibility = RadioCompact.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (RadioNormal.IsChecked == true)
            {
                SelectedDestination = DestinationType.Normal;
            }
            else
            {
                if (_compactSets.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        Properties.Strings.LivePing_AddToSet_NoSets,
                        "MultiPingMonitor",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                int idx = CompactSetCombo.SelectedIndex;
                if (idx < 0 || idx >= _compactSets.Count)
                    idx = 0;
                SelectedDestination = DestinationType.Compact;
                SelectedCompactSet = _compactSets[idx];
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
