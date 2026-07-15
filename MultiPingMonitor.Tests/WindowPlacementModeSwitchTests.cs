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

        [Fact]
        public void MainWindow_InitialAttachUsesMachineProfileOnly()
        {
            string source =
                File.ReadAllText(FindMainWindowSource());

            Assert.Contains(
                "WindowPlacementService.HasMachinePlacement(",
                source);

            Assert.Contains(
                "allowPortableFallback: false",
                source);

            Assert.Contains(
                "ApplyDefaultPlacement(" +
                Environment.NewLine +
                "                                initialDisplayMode," +
                Environment.NewLine +
                "                                usePrimaryMonitor: true)",
                source);
        }

        [Fact]
        public void MainWindow_ModeSwitchRejectsPortablePlacement()
        {
            string source =
                File.ReadAllText(FindMainWindowSource());

            Assert.Contains(
                "WindowPlacementService.HasMachinePlacement(PlacementKeyForMode(targetMode))",
                source);

            Assert.Contains(
                "WindowPlacementService.RestoreWindow(this, PlacementKeyForMode(targetMode), allowPortableFallback: false);",
                source);
        }

        [Fact]
        public void MainWindow_FirstMachineDefaultsAreDpiAwareAndEdgeAnchored()
        {
            string source =
                File.ReadAllText(FindMainWindowSource());

            Assert.Contains(
                "System.Windows.Forms.Screen.PrimaryScreen",
                source);

            Assert.Contains(
                "TransformFromDevice",
                source);

            Assert.Contains(
                "workingArea.Bottom - height - verticalMargin",
                source);

            Assert.Contains(
                "workingArea.Right - width - horizontalMargin",
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
