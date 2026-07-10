using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class StatusHistoryLocalizationTests
    {
        private static string SolutionRoot()
        {
            DirectoryInfo? current =
                new DirectoryInfo(AppContext.BaseDirectory);

            while (current != null)
            {
                if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "MultiPingMonitor.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "MultiPingMonitor solution root was not found.");
        }

        private static string SourcePath(params string[] parts)
        {
            string result = SolutionRoot();

            foreach (string part in parts)
            {
                result = Path.Combine(result, part);
            }

            return result;
        }

        [Fact]
        public void StatusHistoryWindow_LocalizesColumnsAndFilters()
        {
            string xaml = File.ReadAllText(
                SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "StatusHistoryWindow.xaml"));

            string code = File.ReadAllText(
                SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "StatusHistoryWindow.xaml.cs"));

            string[] expectedNames =
            {
                "TimestampColumn",
                "AddressColumn",
                "AliasColumn",
                "StatusColumn",
                "FilterLabel",
                "IncludeLabel",
            };

            foreach (string name in expectedNames)
            {
                Assert.Contains(
                    $"x:Name=\"{name}\"",
                    xaml);
            }

            string[] expectedKeys =
            {
                "StatusHistory_ColumnTimestamp",
                "StatusHistory_ColumnAddress",
                "StatusHistory_ColumnAlias",
                "StatusHistory_ColumnStatus",
                "StatusHistory_FilterLabel",
                "StatusHistory_IncludeLabel",
                "StatusHistory_FilterProbeStatus",
                "NetworkIdentity_Title",
                "StatusHistory_EventType_CompactSet",
                "StatusHistory_FilterUp",
                "StatusHistory_FilterDown",
                "StatusHistory_FilterStart",
                "StatusHistory_FilterStop",
                "StatusHistory_Export",
            };

            foreach (string key in expectedKeys)
            {
                Assert.Contains(
                    $"\"{key}\"",
                    code);
            }

            Assert.Contains(
                "TimestampColumn.Header =",
                code);
            Assert.Contains(
                "AddressColumn.Header =",
                code);
            Assert.Contains(
                "AliasColumn.Header =",
                code);
            Assert.Contains(
                "StatusColumn.Header =",
                code);
            Assert.Contains(
                "FilterProbeEvents.Content =",
                code);
            Assert.Contains(
                "FilterNetworkIdentityEvents.Content =",
                code);
            Assert.Contains(
                "FilterCompactSetEvents.Content =",
                code);
            Assert.Contains(
                "FilterUp.Content =",
                code);
            Assert.Contains(
                "FilterDown.Content =",
                code);

            Assert.Contains(
                "Content=\"WAN IP\"",
                xaml);
            Assert.Contains(
                "Content=\"LAN IP\"",
                xaml);

            int initializeIndex = code.IndexOf(
                "InitializeComponent();",
                StringComparison.Ordinal);
            int localizationIndex = code.IndexOf(
                "RefreshStatusHistoryLabelLocalization();",
                StringComparison.Ordinal);

            Assert.True(initializeIndex >= 0);
            Assert.True(localizationIndex > initializeIndex);
        }
    }
}
