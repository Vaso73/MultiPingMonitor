using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.NetworkInformation;
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
        private readonly ObservableCollection<LogEntry> _logLines = new ObservableCollection<LogEntry>();
        private ObservableCollection<string> _subscribedHistory;
        private const int MaxLogLines = 500;
        private const double BottomScrollThreshold = 20;
        private bool _autoScroll = true;
        private bool _paused;

        // Known IPStatus numeric codes that .NET may emit as raw ToString() values.
        // Maps numeric code → human-readable description.
        private static readonly Dictionary<string, string> IpStatusCodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "11001", "Buffer too small" },
            { "11002", "Destination net unreachable" },
            { "11003", "Destination host unreachable" },
            { "11004", "Destination protocol unreachable" },
            { "11005", "Destination port unreachable" },
            { "11006", "No resources" },
            { "11007", "Bad option" },
            { "11008", "Hardware error" },
            { "11009", "Packet too big" },
            { "11010", "Request timed out" },
            { "11011", "Bad route" },
            { "11012", "TTL expired in transit" },
            { "11013", "TTL expired reassembly" },
            { "11014", "Parameter problem" },
            { "11015", "Source quench" },
            { "11016", "Option too big" },
            { "11017", "Bad destination" },
            { "11018", "Destination unreachable" },
            { "11032", "Time exceeded" },
            { "11033", "Bad header" },
            { "11034", "Unrecognized next header" },
            { "11035", "ICMP error" },
            { "11036", "Destination scope mismatch" },
            { "11050", "General failure — network unavailable" },
        };

        public LivePingMonitorWindow(Probe probe, Window owner)
        {
            InitializeComponent();

            Owner = owner;
            _probe = probe;
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;

            LogListBox.ItemsSource = _logLines;

            // Set localized button/banner text.
            StopResumeButton.Content = Properties.Strings.LivePing_Stop;
            PausedBannerText.Text = Properties.Strings.LivePing_Paused;

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

        // ── Log entry classification ──

        /// <summary>
        /// Classify a raw history line into a LogEntryKind and optionally
        /// rewrite raw numeric IPStatus codes into readable text.
        /// </summary>
        private static LogEntry ClassifyLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new LogEntry(text, LogEntryKind.Info);

            // Trim the timestamp prefix for pattern matching.
            // History lines look like: "[10:32:05]  Reply from …" or "[10:32:05]  Request timed out."
            string body = text;
            int bracketClose = text.IndexOf(']');
            if (bracketClose >= 0 && bracketClose + 1 < text.Length)
                body = text.Substring(bracketClose + 1).TrimStart();

            // Success: "Reply from …" with an ms bracket.
            if (body.StartsWith("Reply from ", StringComparison.OrdinalIgnoreCase) &&
                body.Contains("[") && body.Contains("ms]"))
                return new LogEntry(text, LogEntryKind.Success);

            // TCP port open.
            if (body.Contains("[") && body.Contains("ms]"))
                return new LogEntry(text, LogEntryKind.Success);

            // Timeout / unreachable / down.
            if (body.StartsWith("Request timed out", StringComparison.OrdinalIgnoreCase))
                return new LogEntry(text, LogEntryKind.Failure);
            if (body.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
                return new LogEntry(text, LogEntryKind.Failure);
            if (body.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase))
                return new LogEntry(text, LogEntryKind.Failure);
            if (body.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                return new LogEntry(text, LogEntryKind.Failure);

            // TCP port closed.
            if (body.Contains("Closed", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("closed", StringComparison.OrdinalIgnoreCase))
                return new LogEntry(text, LogEntryKind.Failure);

            // Raw numeric IPStatus code — rewrite to human-readable text.
            string trimmed = body.Trim();
            if (IpStatusCodeMap.TryGetValue(trimmed, out string readable))
            {
                string rewritten = text.Replace(trimmed, $"{readable}  (code {trimmed})");
                return new LogEntry(rewritten, LogEntryKind.Warning);
            }

            // Any other purely numeric body — treat as unknown error code.
            if (int.TryParse(trimmed, out _))
            {
                string rewritten = text.Replace(trimmed, $"Error {trimmed}");
                return new LogEntry(rewritten, LogEntryKind.Warning);
            }

            // Informational lines (e.g. "*** Pinging …", stats summary).
            if (body.StartsWith("***", StringComparison.Ordinal) ||
                body.StartsWith("Sent ", StringComparison.OrdinalIgnoreCase) ||
                body.StartsWith("Minimum ", StringComparison.OrdinalIgnoreCase))
                return new LogEntry(text, LogEntryKind.Info);

            // Default: info / neutral.
            return new LogEntry(text, LogEntryKind.Info);
        }

        // ── History seeding & subscription ──

        private void SeedExistingHistory()
        {
            if (_probe.History == null)
                return;

            // Take last MaxLogLines entries from existing history.
            int startIndex = Math.Max(0, _probe.History.Count - MaxLogLines);
            for (int i = startIndex; i < _probe.History.Count; i++)
            {
                _logLines.Add(ClassifyLine(_probe.History[i]));
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

            if (_paused)
                return;

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (string item in e.NewItems)
                {
                    _logLines.Add(ClassifyLine(item));
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
                    if (!_paused)
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
                if (!_paused)
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

        // ── Header / status / statistics ──

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
                    HeaderStatus.Text = "● UP";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Success");
                    HeaderBorder.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
                    break;
                case ProbeStatus.Down:
                    HeaderStatus.Text = "▼ DOWN";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Danger");
                    // Emphasize DOWN state with tinted header background.
                    HeaderBorder.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xF3, 0x8B, 0xA8));
                    break;
                case ProbeStatus.Error:
                    HeaderStatus.Text = "✖ ERROR";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Danger");
                    HeaderBorder.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xF3, 0x8B, 0xA8));
                    break;
                case ProbeStatus.LatencyHigh:
                    HeaderStatus.Text = "⚠ HIGH LATENCY";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Warning");
                    HeaderBorder.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
                    break;
                case ProbeStatus.Indeterminate:
                    HeaderStatus.Text = "⚠ INDETERMINATE";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Warning");
                    HeaderBorder.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
                    break;
                case ProbeStatus.Inactive:
                    HeaderStatus.Text = "INACTIVE";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Text.Secondary");
                    HeaderBorder.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
                    break;
                default:
                    HeaderStatus.Text = "—";
                    HeaderStatus.Foreground = FindBrushResource("Theme.Text.Secondary");
                    HeaderBorder.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
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

        // ── Auto-scroll ──

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
                    _autoScroll = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - BottomScrollThreshold;
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

        // ── Stop / Resume ──

        private void StopResumeButton_Click(object sender, RoutedEventArgs e)
        {
            _paused = !_paused;

            if (_paused)
            {
                StopResumeButton.Content = Properties.Strings.LivePing_Resume;
                PausedBanner.Visibility = Visibility.Visible;
            }
            else
            {
                StopResumeButton.Content = Properties.Strings.LivePing_Stop;
                PausedBanner.Visibility = Visibility.Collapsed;

                // Refresh header and stats to current state on resume.
                UpdateHeader();
                UpdateStatistics();
            }
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
