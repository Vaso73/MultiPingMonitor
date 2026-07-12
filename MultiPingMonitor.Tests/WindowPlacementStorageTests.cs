using System;
using System.IO;
using System.Xml.Linq;
using MultiPingMonitor.Classes;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class WindowPlacementStorageTests
    {
        [Fact]
        public void SanitizeMachineName_PreservesNormalComputerName()
        {
            Assert.Equal(
                "OFFICE-PC",
                WindowPlacementStorage.SanitizeMachineName("OFFICE-PC"));
        }

        [Fact]
        public void SanitizeMachineName_ReplacesInvalidCharacters()
        {
            Assert.Equal(
                "BAD_NAME_WITH_CHARS",
                WindowPlacementStorage.SanitizeMachineName(
                    "BAD:NAME/WITH*CHARS"));
        }

        [Fact]
        public void SanitizeMachineName_UsesFallbackForWhitespace()
        {
            Assert.Equal(
                WindowPlacementStorage.UnknownMachineName,
                WindowPlacementStorage.SanitizeMachineName("   "));
        }

        [Fact]
        public void BuildFilePath_UsesPortableMachineDirectory()
        {
            string result =
                WindowPlacementStorage.BuildFilePath(
                    "/portable",
                    "SURFACE");

            Assert.Equal(
                Path.Combine(
                    "/portable",
                    "data",
                    "machines",
                    "SURFACE",
                    "window-placement.xml"),
                result);
        }

        [Fact]
        public void SaveAndLoad_KeepSeparateProfilesAndBackup()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "MultiPingMonitor.Tests",
                Guid.NewGuid().ToString("N"));

            try
            {
                WindowPlacementStorage.Save(
                    CreateNode("MainWindow"),
                    root,
                    "TEST-PC");

                WindowPlacementStorage.Save(
                    CreateNode("MainWindow.Compact"),
                    root,
                    "TEST-PC");

                XElement? loaded =
                    WindowPlacementStorage.Load(root, "TEST-PC");

                string machineDirectory =
                    WindowPlacementStorage.BuildMachineDirectory(
                        root,
                        "TEST-PC");

                Assert.NotNull(loaded);
                Assert.Equal(
                    "TEST-PC",
                    (string?)loaded!.Attribute("machine"));

                Assert.Equal(
                    "MainWindow.Compact",
                    (string?)loaded.Element("window")?.Attribute("key"));

                Assert.True(
                    File.Exists(Path.Combine(
                        machineDirectory,
                        WindowPlacementStorage.BackupFileName)));

                Assert.Null(
                    WindowPlacementStorage.Load(root, "OTHER-PC"));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static XElement CreateNode(string key)
        {
            return new XElement(
                "windowPlacements",
                new XElement(
                    "window",
                    new XAttribute("key", key)));
        }
    }
}
