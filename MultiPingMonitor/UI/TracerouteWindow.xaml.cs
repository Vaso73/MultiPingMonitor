using System.Windows;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class TracerouteWindow : Window
    {
        private Probe _probe;

        public TracerouteWindow()
        {
            InitializeComponent();
        }

        private void TraceButton_Click(object sender, RoutedEventArgs e)
        {
            string host = HostTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
                return;

            ResultsListBox.Items.Clear();

            if (_probe != null && _probe.IsActive)
                _probe.StartStop();

            _probe = new Probe
            {
                Hostname = "T/" + host
            };
            _probe.History.CollectionChanged += (s, args) =>
            {
                if (args.NewItems != null)
                {
                    foreach (var item in args.NewItems)
                        ResultsListBox.Items.Add(item);
                }
            };
            _probe.StartStop();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_probe != null && _probe.IsActive)
                _probe.StartStop();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_probe != null && _probe.IsActive)
                _probe.StartStop();
            Close();
        }
    }
}
