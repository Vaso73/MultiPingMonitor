using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MultiPingMonitor.Classes;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    public partial class TracerouteWindow : Window
    {
        private readonly NetworkRoute _route = new NetworkRoute();
        private CancellationTokenSource _cts;

        public TracerouteWindow()
        {
            InitializeComponent();
            WindowPlacementService.Attach(this, "TracerouteWindow");
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;

            DataContext = _route;
            TraceData.ItemsSource = _route.networkRoute;

            Loaded += (sender, e) =>
                MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Closing += TracerouteWindow_Closing;
        }

        private async void Trace_Click(object sender, RoutedEventArgs e)
        {
            if (!_route.IsActive)
            {
                if (string.IsNullOrWhiteSpace(Hostname.Text))
                    return;

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                // Reset IP address column width so it auto-sizes for new data.
                TraceData.Columns[1].Width = new DataGridLength(100.0);
                TraceData.Columns[1].Width = new DataGridLength(1.0, DataGridLengthUnitType.Auto);

                TraceStatus.Text = Strings.Traceroute_TracingRoute;
                TraceStatus.Visibility = Visibility.Visible;
                _route.DestinationHost = Hostname.Text.Trim();
                _route.MaxHops = 30;
                _route.PingTimeout = 2000;
                _route.networkRoute.Clear();
                _route.IsActive = true;

                await PerformTraceAsync(_cts.Token);
            }
            else
            {
                _cts?.Cancel();
                _route.IsActive = false;
                TraceStatus.Text = "\u2022 " + Strings.Traceroute_TraceCancelled;
                Hostname.Focus();
            }
        }

        private async Task PerformTraceAsync(CancellationToken token)
        {
            var timer = new Stopwatch();
            var pingBuffer = Encoding.ASCII.GetBytes(Constants.DefaultIcmpData);

            // Resolve hostname to IP address.
            IPAddress destinationIp;
            if (!IPAddress.TryParse(_route.DestinationHost, out destinationIp))
            {
                try
                {
                    var entry = await Dns.GetHostEntryAsync(_route.DestinationHost);
                    destinationIp = entry.AddressList[0];
                }
                catch
                {
                    if (!token.IsCancellationRequested)
                    {
                        TraceStatus.Text = "\u2022 " + Strings.Traceroute_InvalidHostname;
                        _route.IsActive = false;
                    }
                    return;
                }
            }

            var ttl = 1;
            while (!token.IsCancellationRequested && ttl <= _route.MaxHops)
            {
                try
                {
                    var options = new PingOptions(ttl: ttl, dontFragment: true);

                    using (var ping = new Ping())
                    {
                        timer.Restart();
                        var reply = await ping.SendPingAsync(destinationIp, _route.PingTimeout, pingBuffer, options);

                        // Retry once on timeout (same as vmPing behavior).
                        if (reply.Status == IPStatus.TimedOut)
                        {
                            timer.Restart();
                            reply = await ping.SendPingAsync(destinationIp, _route.PingTimeout, pingBuffer, options);
                        }

                        timer.Stop();

                        if (token.IsCancellationRequested)
                            break;

                        var node = new NetworkRouteNode
                        {
                            HopId = ttl,
                            RoundTripTime = timer.ElapsedMilliseconds,
                            ReplyStatus = reply.Status
                        };

                        if (reply.Address != null)
                            node.HostAddress = reply.Address.ToString();

                        if (reply.Status == IPStatus.TimedOut)
                            node.HostAddress = Strings.Traceroute_TimedOut;

                        _route.networkRoute.Add(node);
                        TraceData.ScrollIntoView(node);

                        if (reply.Status == IPStatus.Success)
                        {
                            TraceStatus.Text = "\u2605 " + Strings.Traceroute_TraceComplete;
                            break;
                        }

                        ttl++;
                    }

                    await Task.Delay(100, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }
            }

            _route.IsActive = false;

            // Return focus to hostname after trace completes.
            try
            {
                await Task.Delay(100);
                if (!token.IsCancellationRequested)
                    Hostname.Focus();
            }
            catch (TaskCanceledException)
            {
                // Window may have closed.
            }
        }

        private void TracerouteWindow_Closing(object sender, CancelEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
