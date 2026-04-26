using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class CompactSetStatusHistoryTests
    {
        private static string SolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MultiPingMonitor.sln")))
                dir = dir.Parent;

            if (dir == null)
                throw new DirectoryNotFoundException("Solution root not found.");

            return dir.FullName;
        }

        private static string SourcePath(params string[] parts)
        {
            return Path.Combine(SolutionRoot(), Path.Combine(parts));
        }

        [Fact]
        public void StatusChangeLog_HasCompactSetEventType()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "StatusChangeLog.cs"));

            Assert.Contains("CompactSet", source);
            Assert.Contains("IsCompactSetEvent", source);
            Assert.Contains("StatusHistory_EventType_CompactSet", source);
        }

        [Fact]
        public void StatusHistoryWindow_HasCompactSetFilter()
        {
            var xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "StatusHistoryWindow.xaml"));
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "StatusHistoryWindow.xaml.cs"));

            Assert.Contains("FilterCompactSetEvents", xaml);
            Assert.Contains("Compact set", xaml);
            Assert.Contains("StatusChangeEventType.CompactSet", source);
            Assert.Contains("FilterCompactSetEvents.IsChecked", source);
        }

        [Fact]
        public void MainWindow_TracksCompactSetHistoryEvents()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("TrackCompactSetHistoryEvent", source);
            Assert.Contains("EventType = StatusChangeEventType.CompactSet", source);
            Assert.Contains("StatusHistory_CompactSet_Started", source);
            Assert.Contains("StatusHistory_CompactSet_Stopped", source);
            Assert.Contains("StatusHistory_CompactSet_ActiveChanged", source);
            Assert.Contains("StatusHistory_CompactSet_HostAdded", source);
            Assert.Contains("StatusHistory_CompactSet_HostRemoved", source);
        }

        [Fact]
        public void Resources_HaveCompactSetHistoryTexts()
        {
            var defaultResx = File.ReadAllText(SourcePath("MultiPingMonitor", "Properties", "Strings.resx"));
            var skResx = File.ReadAllText(SourcePath("MultiPingMonitor", "Properties", "Strings.sk-SK.resx"));

            foreach (var key in new[]
            {
                "StatusHistory_EventType_CompactSet",
                "StatusHistory_CompactSet_NameFallback",
                "StatusHistory_CompactSet_None",
                "StatusHistory_CompactSet_Started",
                "StatusHistory_CompactSet_Stopped",
                "StatusHistory_CompactSet_ActiveChanged",
                "StatusHistory_CompactSet_HostAdded",
                "StatusHistory_CompactSet_HostRemoved"
            })
            {
                Assert.Contains(key, defaultResx);
                Assert.Contains(key, skResx);
            }
        }
    }
}