using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class LivePingMonitorWindow : Window
    {
        private Probe _probe;
        private readonly ObservableCollection<string> _logLines = new ObservableCollection<string>();
        private ObservableCollection<string> _subscribedHistory;
        private const int MaxLogLines = 500;
        private bool _autoScroll = true;

        public LivePingMonitorWindow(Probe probe, Window owner)
        {
            InitializeComponent();

            Owner = owner;
            _probe = probe;
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;

            LogListBox.ItemsSource = _logLines;

            // Populate header from current probe state.
            UpdateHeader();

            // Seed log with existing history (tail only to stay within cap).
            SeedExistingHistory();

            // Update stats from current probe values.
            UpdateStatistics();

            // Subscribe to probe property changes for live updates.
            _probe.PropertyChanged += Probe_PropertyChanged;

            // Subscribe to history collection changes for new lines.
            SubscribeHistory(_probe.History);
        }

        private void SeedExistingHistory()
        {
            if (_probe.History == null)
                return;

            // Take last MaxLogLines entries from existing history.
            int startIndex = Math.Max(0, _probe.History.Count - MaxLogLines);
            for (int i = startIndex; i < _probe.History.Count; i++)
            {
                _logLines.Add(_probe.History[i]);
            }

            // Scroll to bottom after seeding.
            ScrollToBottom();
        }

        private void History_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => History_CollectionChanged(sender, e)));
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (string item in e.NewItems)
                {
                    _logLines.Add(item);
                }

                // Trim oldest lines if over cap.
                while (_logLines.Count > MaxLogLines)
                {
                    _logLines.RemoveAt(0);
                }

                if (_autoScroll)
                {
                    ScrollToBottom();
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // History was cleared (probe restarted).
                _logLines.Clear();
            }
        }

        private void Probe_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Probe_PropertyChanged(sender, e)));
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(Probe.Status):
                case nameof(Probe.LastRoundtripTime):
                    UpdateHeader();
                    break;
                case nameof(Probe.Alias):
                case nameof(Probe.Hostname):
                    UpdateHeader();
                    break;
                case nameof(Probe.History):
                    // History collection was replaced (probe restarted).
                    // Resubscribe to the new collection.
                    ResubscribeHistory();
                    break;
            }

            // Update statistics on any property change from the probe
            // (Statistics.Sent etc. fire through the probe's chain).
            if (e.PropertyName == nameof(Probe.StatisticsText))
            {
                UpdateStatistics();
            }
        }

        private void ResubscribeHistory()
        {
            // Detach from old collection, attach to new.
            UnsubscribeHistory();
            _logLines.Clear();

            if (_probe.History != null)
            {
                SubscribeHistory(_probe.History);
                SeedExistingHistory();
            }

            UpdateStatistics();
        }

        private void SubscribeHistory(ObservableCollection<string> history)
        {
            if (history == null)
                return;

            _subscribedHistory = history;
            _subscribedHistory.CollectionChanged += History_CollectionChanged;
        }

        private void UnsubscribeHistory()
        {
            if (_subscribedHistory != null)
            {
                _subscribedHistory.CollectionChanged -= History_CollectionChanged;
                _subscribedHistory = null;
            }
        }

        private void UpdateHeader()
        {
            if (_probe == null)
                return;

            // Display name: prefer alias.
            string displayName = !string.IsNullOrWhiteSpace(_probe.Alias)
                ? _probe.Alias
                : (!string.IsNullOrWhiteSpace(_probe.Hostname) ? _probe.Hostname : "—");

            HeaderDisplayName.Text = displayName;

            // Secondary target: show hostname if alias is different.
            if (!string.IsNullOrWhiteSpace(_probe.Alias) && !string.IsNullOrWhiteSpace(_probe.Hostname))
            {
                HeaderTarget.Text = _probe.Hostname;
                HeaderTarget.Visibility = Visibility.Visible;
            }
            else
            {
                HeaderTarget.Visibility = Visibility.Collapsed;
            }

            // Status indicator.
            UpdateStatusIndicator();

            // Latency.
            if (_probe.LastRoundtripTime >= 0)
            {
                HeaderLatency.Text = _probe.LastRoundtripTime < 1
                    ? "<1ms"
                    : $"{_probe.LastRoundtripTime}ms";
            }
            else
            {
                HeaderLatency.Text = "—";
            }

            // Window title.
            Title = $"{displayName} - Live Ping Monitor";
        }

        private void UpdateStatusIndicator()
        {
            switch (_probe.Status)
            {
                case ProbeStatus.Up:
                case ProbeStatus.LatencyNormal:
                    HeaderStatus.Text = "UP";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Success");
                    break;
                case ProbeStatus.Down:
                    HeaderStatus.Text = "DOWN";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Danger");
                    break;
                case ProbeStatus.Error:
                    HeaderStatus.Text = "ERROR";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Danger");
                    break;
                case ProbeStatus.LatencyHigh:
                    HeaderStatus.Text = "HIGH LATENCY";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Warning");
                    break;
                case ProbeStatus.Indeterminate:
                    HeaderStatus.Text = "INDETERMINATE";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Warning");
                    break;
                case ProbeStatus.Inactive:
                    HeaderStatus.Text = "INACTIVE";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Text.Secondary");
                    break;
                default:
                    HeaderStatus.Text = "—";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Text.Secondary");
                    break;
            }
        }

        private Brush FindBrushResource(string key)
        {
            var resource = TryFindResource(key);
            return resource is Brush brush ? brush : Brushes.Gray;
        }

        private void UpdateStatistics()
        {
            if (_probe?.Statistics == null)
                return;

            StatsSent.Text = _probe.Statistics.Sent.ToString();
            StatsReceived.Text = _probe.Statistics.Received.ToString();
            StatsLost.Text = _probe.Statistics.Lost.ToString();

            if (_probe.Statistics.Sent > 0)
            {
                double lossPercent = 100.0 * _probe.Statistics.Lost / _probe.Statistics.Sent;
                StatsLossPercent.Text = $"({lossPercent:0.#}% loss)";
            }
            else
            {
                StatsLossPercent.Text = string.Empty;
            }
        }

        private void ScrollToBottom()
        {
            if (_logLines.Count == 0)
                return;

            // Use Dispatcher to defer scroll until layout is updated.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_logLines.Count > 0)
                {
                    LogListBox.ScrollIntoView(_logLines[_logLines.Count - 1]);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // Detect manual scroll to disable auto-scroll.
        // Re-enable when user scrolls back to bottom.
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            // After the scroll event, check if we're at the bottom.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var scrollViewer = FindScrollViewer(LogListBox);
                if (scrollViewer != null)
                {
                    _autoScroll = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 20;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer sv)
                return sv;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        // ── Button handlers ──

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _logLines.Clear();
            _autoScroll = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // Adjust maximize border thickness.
            if (WindowState == WindowState.Maximized)
            {
                BorderThickness = new Thickness(8);
            }
            else
            {
                BorderThickness = new Thickness(0);
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Unsubscribe from all probe events.
            if (_probe != null)
            {
                _probe.PropertyChanged -= Probe_PropertyChanged;
                UnsubscribeHistory();

                // Clear the back-reference.
                _probe.LivePingMonitorWindow = null;
                _probe = null;
            }

            _logLines.Clear();
        }
    }
}
