using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class StatusHistoryNetworkIdentityPolishTests
    {
        private static string SolutionRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "MultiPingMonitor.sln")))
                    return dir;

                var parent = Directory.GetParent(dir);
                if (parent == null)
                    break;

                dir = parent.FullName;
            }

            throw new DirectoryNotFoundException("Could not locate solution root.");
        }

        private static string SourcePath(params string[] parts)
        {
            var all = new string[parts.Length + 1];
            all[0] = SolutionRoot();
            Array.Copy(parts, 0, all, 1, parts.Length);
            return Path.Combine(all);
        }

        [Fact]
        public void StatusChangeLog_HasEventTypeForNetworkIdentityEntries()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "StatusChangeLog.cs"));

            Assert.Contains("enum StatusChangeEventType", source);
            Assert.Contains("Probe,", source);
            Assert.Contains("NetworkIdentity", source);
            Assert.Contains("public StatusChangeEventType EventType", source);
            Assert.Contains("public bool IsNetworkIdentityEvent", source);
            Assert.Contains("public string EventTypeAsString", source);
        }

        [Fact]
        public void MainWindow_NetworkIdentityStatusEntries_AreTypedAsNetworkIdentity()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("EventType = StatusChangeEventType.NetworkIdentity", source);
            Assert.Contains("TrackNetworkIdentityIpChange(\"LAN IP\"", source);
            Assert.Contains("TrackNetworkIdentityIpChange(\"WAN IP\"", source);
        }

        [Fact]
        public void StatusHistoryWindow_HasNetworkIdentityFilters()
        {
            var xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "StatusHistoryWindow.xaml"));
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "StatusHistoryWindow.xaml.cs"));

            Assert.Contains("FilterProbeEvents", xaml);
            Assert.Contains("FilterNetworkIdentityEvents", xaml);
            Assert.Contains("FilterWanIpEvents", xaml);
            Assert.Contains("FilterLanIpEvents", xaml);

            Assert.Contains("StatusChangeEventType.NetworkIdentity", source);
            Assert.Contains("IsNetworkIdentityFilterMatch", source);
            Assert.Contains("IsProbeStatusFilterMatch", source);
            Assert.Contains("IsTextFilterMatch", source);
        }

        [Fact]
        public void StatusHistoryWindow_VisuallyDistinguishesNetworkIdentityRows()
        {
            var xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "StatusHistoryWindow.xaml"));

            Assert.Contains("IsNetworkIdentityEvent", xaml);
            Assert.Contains("#182a38", xaml);
            Assert.Contains("#61b8ff", xaml);
        }

        [Fact]
        public void StatusHistoryExport_IncludesEventType()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "StatusHistoryWindow.xaml.cs"));

            Assert.Contains("s.EventTypeAsString", source);
            Assert.Contains("s.Hostname", source);
            Assert.Contains("s.StatusAsString", source);
        }
    }
}