using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class TrayDialogOwnerTests
    {
        private static string SolutionRoot()
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir != null && !File.Exists(Path.Combine(dir, "MultiPingMonitor.sln")))
                dir = Directory.GetParent(dir)?.FullName;
            return dir ?? Directory.GetCurrentDirectory();
        }

        private static string SourcePath(params string[] parts) =>
            Path.Combine(SolutionRoot(), Path.Combine(parts));

        [Fact]
        public void MainWindow_UsesSafeDialogOwnerForTrayOpenedOptionsAndAbout()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "MainWindow.xaml.cs"));

            Assert.Contains("ConfigureOwnedDialog(Window dialog)", source);
            Assert.Contains("if (IsLoaded)", source);
            Assert.Contains("WindowStartupLocation.CenterOwner", source);
            Assert.Contains("WindowStartupLocation.CenterScreen", source);
            Assert.Contains("ConfigureOwnedDialog(optionsWnd)", source);
            Assert.Contains("ConfigureOwnedDialog(aboutWindow)", source);

            Assert.DoesNotContain(
                "var optionsWnd = new OptionsWindow\n            {\n                Owner = this\n            };",
                source.Replace("\r\n", "\n"));
        }

        [Fact]
        public void OptionsWindow_CanReachMainWindowWithoutOwnerWhenOpenedFromTray()
        {
            string source =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "OptionsWindow.xaml.cs"));

            Assert.Contains(
                "Owner as MainWindow ?? Application.Current?.MainWindow as MainWindow",
                source);
            Assert.Contains("HostMainWindow?.SwitchDisplayMode(mode)", source);
            Assert.Contains("HostMainWindow?.ApplyCompactDataSource()", source);
            Assert.Contains("var mainWindow = HostMainWindow;", source);
        }

        [Fact]
        public void ManageCompactSets_UsesSafeVisibleOwnerWhenStartedInTray()
        {
            string mainWindowSource =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "MainWindow.xaml.cs"));

            string optionsWindowSource =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "OptionsWindow.xaml.cs"));

            string manageWindowSource =
                File.ReadAllText(SourcePath(
                    "MultiPingMonitor",
                    "UI",
                    "ManageCompactSetsWindow.xaml.cs"));

            Assert.Contains(
                "internal void OpenManageCompactSets(Window preferredOwner = null)",
                mainWindowSource);
            Assert.Contains(
                "preferredOwner != null && preferredOwner.IsLoaded",
                mainWindowSource);
            Assert.Contains(
                "var window = new ManageCompactSetsWindow(this);",
                mainWindowSource);
            Assert.Contains(
                "window.Owner = dialogOwner;",
                mainWindowSource);
            Assert.Contains(
                "WindowStartupLocation.CenterScreen",
                mainWindowSource);

            Assert.Contains(
                "mainWindow?.OpenManageCompactSets(this);",
                optionsWindowSource);

            Assert.Contains(
                "private readonly MainWindow _hostMainWindow;",
                manageWindowSource);
            Assert.Contains(
                "public ManageCompactSetsWindow(MainWindow hostMainWindow)",
                manageWindowSource);
            Assert.DoesNotContain(
                "(Owner as MainWindow)?.",
                manageWindowSource);

            Assert.DoesNotContain(
                "var window = new ManageCompactSetsWindow();",
                mainWindowSource);
            Assert.DoesNotContain(
                "window.Owner = this;\n            window.ShowDialog();",
                mainWindowSource.Replace("\r\n", "\n"));
        }
    }
}
