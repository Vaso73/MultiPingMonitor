using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class WindowPlacementModeSwitchTests
    {
        [Fact]
        public void MainWindow_DisablesStaticAttachClosingKey()
        {
            string source =
                File.ReadAllText(FindMainWindowSource());

            Assert.Contains(
                "saveOnClosing: false",
                source);
        }

        [Fact]
        public void MainWindow_SavesOutgoingAndCurrentModeKeys()
        {
            string source =
                File.ReadAllText(FindMainWindowSource());

            Assert.Contains(
                "WindowPlacementService.SaveWindow(this, PlacementKeyForMode(previousMode));",
                source);

            Assert.Contains(
                "WindowPlacementService.SaveWindow(this, PlacementKeyForMode(ApplicationOptions.CurrentDisplayMode));",
                source);
        }

        private static string FindMainWindowSource()
        {
            DirectoryInfo? directory =
                new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(
                    directory.FullName,
                    "MultiPingMonitor",
                    "UI",
                    "MainWindow.xaml.cs");

                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "Could not locate MultiPingMonitor/UI/MainWindow.xaml.cs.");
        }
    }
}
