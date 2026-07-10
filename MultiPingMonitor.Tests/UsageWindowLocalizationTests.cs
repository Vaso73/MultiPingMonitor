using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class UsageWindowLocalizationTests
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
        public void UsageWindow_LocalizesHumanTextAndPreservesCommands()
        {
            string xaml = File.ReadAllText(
                SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "UsageWindow.xaml"));

            string code = File.ReadAllText(
                SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "UsageWindow.xaml.cs"));

            string[] expectedNames =
            {
                "CommandLineUsageHeader",
                "OptionsHeader",
                "IntervalDescriptionText",
                "IntervalRangeText",
                "TimeoutDescriptionText",
                "TimeoutRangeText",
                "StartMinimizedDescriptionText",
                "HostnameDescriptionText",
                "MultipleHostnamesDescriptionText",
                "FileDescriptionText",
                "ExamplesHeader",
            };

            foreach (string name in expectedNames)
            {
                Assert.Contains(
                    $"x:Name=\"{name}\"",
                    xaml);
            }

            string[] expectedKeys =
            {
                "Help_CommandLine_Header",
                "Usage_OptionsHeader",
                "Usage_IntervalDescription",
                "Usage_IntervalRange",
                "Usage_TimeoutDescription",
                "Usage_TimeoutRange",
                "Usage_StartMinimizedDescription",
                "Usage_HostnameDescription",
                "Usage_MultipleHostnamesDescription",
                "Usage_FileDescription",
                "Usage_ExamplesHeader",
            };

            foreach (string key in expectedKeys)
            {
                Assert.Contains(
                    $"\"{key}\"",
                    code);
            }

            string[] preservedTechnicalText =
            {
                "MultiPingMonitor [OPTIONS] [HOSTNAME...] [FILE...]",
                "-i &lt;interval>",
                "-w &lt;timeout>",
                "-minimized",
                "&lt;hostname>",
                "&lt;file>",
                "MultiPingMonitor.exe -i 5 -w 2 "
                    + "192.168.0.1 172.16.50.1 10.2.0.32",
                "MultiPingMonitor.exe &quot;"
                    + "c:\\documents\\servers.txt&quot;",
                "MultiPingMonitor.exe -minimized "
                    + "192.168.5.20 web.example.com",
            };

            foreach (string text in preservedTechnicalText)
            {
                Assert.Contains(text, xaml);
            }

            int initializeIndex = code.IndexOf(
                "InitializeComponent();",
                StringComparison.Ordinal);
            int localizationIndex = code.IndexOf(
                "RefreshUsageWindowLocalization();",
                StringComparison.Ordinal);

            Assert.True(initializeIndex >= 0);
            Assert.True(localizationIndex > initializeIndex);
        }
    }
}
