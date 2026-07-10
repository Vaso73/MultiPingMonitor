using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class PopupNotificationLocalizationTests
    {
        private static string SolutionRoot()
        {
            string dir = AppContext.BaseDirectory;

            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(
                    Path.Combine(dir, "MultiPingMonitor.sln")))
                {
                    return dir;
                }

                DirectoryInfo parent = Directory.GetParent(dir);

                if (parent == null)
                {
                    break;
                }

                dir = parent.FullName;
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
        public void PopupHistoryButton_UsesLocalizedTooltipAndAutomationName()
        {
            string xaml = File.ReadAllText(
                SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "PopupNotificationWindow.xaml"));

            string code = File.ReadAllText(
                SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "PopupNotificationWindow.xaml.cs"));

            Assert.Contains(
                "x:Name=\"OpenStatusHistoryButton\"",
                xaml);

            Assert.DoesNotContain(
                "ToolTip=\"Open status history window\"",
                xaml);

            Assert.Contains(
                "SetTitleBarButtonText(",
                code);

            Assert.Contains(
                "OpenStatusHistoryButton",
                code);

            Assert.Contains(
                "\"PopupNotification_OpenStatusHistory\"",
                code);

            Assert.Contains(
                "AutomationProperties.SetName(button, text)",
                code);
        }
    }
}
