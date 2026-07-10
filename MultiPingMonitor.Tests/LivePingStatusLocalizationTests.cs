using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class LivePingStatusLocalizationTests
    {
        private static readonly IReadOnlyDictionary<string, string>
            ExpectedValues =
                new Dictionary<string, string>
                {
                    ["LivePing_Status_Up"] = "UP",
                    ["LivePing_Status_Down"] = "DOWN",
                    ["LivePing_Status_Error"] = "ERROR",
                    ["LivePing_Status_HighLatency"] = "HIGH LATENCY",
                    ["LivePing_Status_Indeterminate"] = "INDETERMINATE",
                    ["LivePing_Status_Inactive"] = "INACTIVE",
                };

        [Fact]
        public void DefaultAndSlovakResourcesKeepTechnicalStatusNamesInEnglish()
        {
            IReadOnlyDictionary<string, string> english =
                LoadResx("Strings.resx");
            IReadOnlyDictionary<string, string> slovak =
                LoadResx("Strings.sk-SK.resx");

            foreach (KeyValuePair<string, string> expected in ExpectedValues)
            {
                Assert.Equal(expected.Value, english[expected.Key]);
                Assert.Equal(expected.Value, slovak[expected.Key]);
            }
        }

        [Fact]
        public void LivePingStatusRenderingUsesDedicatedLanguagePackKeys()
        {
            string source = File.ReadAllText(
                RepositoryPath(
                    "MultiPingMonitor",
                    "UI",
                    "LivePingMonitorWindow.xaml.cs"));

            foreach (string key in ExpectedValues.Keys)
            {
                Assert.Contains(
                    "Text(\"" + key + "\"",
                    source);
            }

            Assert.DoesNotContain(
                "HeaderStatus.Text = \"● UP\";",
                source);
            Assert.DoesNotContain(
                "HeaderStatus.Text = \"▼ DOWN\";",
                source);
            Assert.DoesNotContain(
                "HeaderStatus.Text = \"✖ ERROR\";",
                source);
            Assert.DoesNotContain(
                "HeaderStatus.Text = \"⚠ HIGH LATENCY\";",
                source);
            Assert.DoesNotContain(
                "HeaderStatus.Text = \"⚠ INDETERMINATE\";",
                source);
            Assert.DoesNotContain(
                "HeaderStatus.Text = \"INACTIVE\";",
                source);
        }

        [Fact]
        public void SlovakSeedExposesTechnicalStatusValuesForManualEditing()
        {
            string source = File.ReadAllText(
                RepositoryPath(
                    "MultiPingMonitor",
                    "Classes",
                    "LanguagePackSeeds.cs"));

            int keyId = 20568;

            foreach (KeyValuePair<string, string> expected in ExpectedValues)
            {
                string expectedEntry =
                    "new LanguagePackSeedEntry("
                    + keyId
                    + ", \""
                    + expected.Key
                    + "\", \""
                    + expected.Value
                    + "\", \""
                    + expected.Value
                    + "\")";

                Assert.Contains(expectedEntry, source);
                keyId++;
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
