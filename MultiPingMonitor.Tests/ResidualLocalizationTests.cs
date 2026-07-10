using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class ResidualLocalizationTests
    {
        private static readonly IReadOnlyDictionary<
            string,
            (string English, string Slovak)> ExpectedValues =
                new Dictionary<
                    string,
                    (string English, string Slovak)>
                {
                    ["MultiInput_Instructions"] = (
                        "Type a list of addresses to ping.\n"
                            + "Enter one per line or comma separated.\n\n"
                            + "If you have a text file containing hosts,\n"
                            + "drag and drop it here.",
                        "Zadajte zoznam adries na pingovanie.\n"
                            + "Každú zadajte na samostatný riadok alebo ich "
                            + "oddeľte čiarkami.\n\n"
                            + "Ak máte textový súbor obsahujúci hostiteľov,\n"
                            + "presuňte ho sem."),
                    ["NewFavorite_Instructions"] = (
                        "Type a list of addresses to include in this favorite set.\n"
                            + "Enter either one per line or comma separated.\n\n"
                            + "If you have a text file containing hosts, "
                            + "drag and drop it here.",
                        "Zadajte zoznam adries, ktoré chcete zahrnúť do tejto "
                            + "obľúbenej sady.\n"
                            + "Každú zadajte na samostatný riadok alebo ich "
                            + "oddeľte čiarkami.\n\n"
                            + "Ak máte textový súbor obsahujúci hostiteľov, "
                            + "presuňte ho sem."),
                    ["Common_DropSingleFileOnly"] = (
                        "Please drop only one file at a time.",
                        "Presuňte naraz iba jeden súbor."),
                    ["Common_FileTooLargeWithPath"] = (
                        "\"{0}\" is too large. "
                            + "The maximum file size is {1} KB.",
                        "Súbor „{0}“ je príliš veľký. "
                            + "Maximálna veľkosť súboru je {1} KB."),
                    ["Common_FileOpenErrorWithDetails"] = (
                        "File could not be opened: {0}",
                        "Súbor sa nepodarilo otvoriť: {0}"),
                    ["Common_FileTooLarge"] = (
                        "The file is too large and cannot be opened. "
                            + "The maximum file size is {0} KB.",
                        "Súbor je príliš veľký a nemožno ho otvoriť. "
                            + "Maximálna veľkosť súboru je {0} KB."),
                    ["Common_FileOpenPlainText"] = (
                        "File could not be opened. "
                            + "Make sure the file is a plain text file.",
                        "Súbor sa nepodarilo otvoriť. "
                            + "Uistite sa, že ide o obyčajný textový súbor."),
                    ["CommandLine_FileParseError"] = (
                        "Unable to parse \"{0}\": {1}",
                        "Nepodarilo sa spracovať „{0}“: {1}"),
                    ["Favorite_NotFound"] = (
                        "The requested favorite was not found: {0}",
                        "Požadovaná obľúbená sada sa nenašla: {0}"),
                    ["Probe_LogWriteError"] = (
                        "Failed writing to log file. "
                            + "Logging has been disabled. Error: {0}",
                        "Zápis do súboru záznamu zlyhal. "
                            + "Zaznamenávanie bolo vypnuté. Chyba: {0}"),
                    ["Probe_AudioPlaybackError"] = (
                        "Failed to play audio file. "
                            + "Audio alerts have been disabled. Error: {0}",
                        "Prehratie zvukového súboru zlyhalo. "
                            + "Zvukové upozornenia boli vypnuté. Chyba: {0}"),
                    ["LivePing_AddToSet_CompactDestination"] = (
                        "Compact",
                        "Kompaktné"),
                };

        [Fact]
        public void DefaultAndSlovakResourcesContainResidualTranslations()
        {
            IReadOnlyDictionary<string, string> english =
                LoadResx("Strings.resx");
            IReadOnlyDictionary<string, string> slovak =
                LoadResx("Strings.sk-SK.resx");

            foreach (
                KeyValuePair<
                    string,
                    (string English, string Slovak)> expected
                in ExpectedValues)
            {
                Assert.Equal(
                    expected.Value.English,
                    english[expected.Key]);
                Assert.Equal(
                    expected.Value.Slovak,
                    slovak[expected.Key]);
            }
        }

        [Fact]
        public void ResidualCallSitesUseLanguageResources()
        {
            string combinedSource = string.Join(
                "\n",
                new[]
                {
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "MultiInputWindow.xaml.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "NewFavoriteWindow.xaml.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "ManageCompactTargetsWindow.xaml.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "Classes",
                        "CommandLine.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "Classes",
                        "Favorite.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "Classes",
                        "Probe-Util.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "AddToSetDialog.xaml.cs"),
                });

            foreach (string key in ExpectedValues.Keys)
            {
                Assert.Contains(key, combinedSource);
            }

            Assert.Contains(
                "Strings.Error_ReadConfig",
                combinedSource);
            Assert.Contains(
                "Properties.Strings.CompactSets_Target",
                combinedSource);
        }

        [Fact]
        public void PreviousHardcodedResidualLiteralsAreRemoved()
        {
            string combinedSource = string.Join(
                "\n",
                new[]
                {
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "MultiInputWindow.xaml"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "MultiInputWindow.xaml.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "NewFavoriteWindow.xaml"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "NewFavoriteWindow.xaml.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "ManageCompactTargetsWindow.xaml.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "Classes",
                        "CommandLine.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "Classes",
                        "Favorite.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "Classes",
                        "Probe-Util.cs"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "AddToSetDialog.xaml"),
                    ReadRepositoryFile(
                        "MultiPingMonitor",
                        "UI",
                        "AddToSetDialog.xaml.cs"),
                });

            string[] forbidden =
            {
                "Type a list of addresses to ping.<LineBreak/>",
                "Type a list of addresses to include in this favorite set.<LineBreak/>",
                "ShowError(\"Please drop only one file at a time.\")",
                "ShowError($\"File could not be opened:",
                "Failed to read configuration file. {ex.Message}",
                "The requested favorite was not found: {title}",
                "ShowError($\"Failed writing to log file.",
                "ShowError($\"Failed to play audio file.",
                "Content=\"Compact\"",
                "Target:  {_target}",
            };

            foreach (string literal in forbidden)
            {
                Assert.DoesNotContain(
                    literal,
                    combinedSource);
            }
        }

        private static IReadOnlyDictionary<string, string> LoadResx(
            string fileName)
        {
            XDocument document = XDocument.Load(
                RepositoryPath(
                    "MultiPingMonitor",
                    "Properties",
                    fileName));

            XElement root = document.Root
                ?? throw new InvalidDataException(
                    "The RESX document has no root element.");

            return root
                .Elements("data")
                .Where(element => element.Attribute("name") != null)
                .ToDictionary(
                    element =>
                        element.Attribute("name")?.Value
                        ?? throw new InvalidDataException(
                            "A RESX data element has no name attribute."),
                    element =>
                        element.Element("value")?.Value
                        ?? string.Empty,
                    StringComparer.Ordinal);
        }

        private static string ReadRepositoryFile(
            params string[] parts)
        {
            return File.ReadAllText(
                RepositoryPath(parts));
        }

        private static string RepositoryPath(
            params string[] parts)
        {
            DirectoryInfo? directory =
                new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string solutionPath = Path.Combine(
                    directory.FullName,
                    "MultiPingMonitor.sln");

                if (File.Exists(solutionPath))
                {
                    string[] completeParts =
                        new[] { directory.FullName }
                        .Concat(parts)
                        .ToArray();

                    return Path.Combine(completeParts);
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "MultiPingMonitor repository root was not found.");
        }
    }
}
