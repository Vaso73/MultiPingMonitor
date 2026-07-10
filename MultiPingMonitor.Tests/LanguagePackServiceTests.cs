using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MultiPingMonitor.Classes;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class LanguagePackServiceTests
    {
        private static string SolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;
            return dir?.FullName
                ?? throw new DirectoryNotFoundException("Cannot locate solution root from " + AppContext.BaseDirectory);
        }

        [Fact]
        public void LanguagePackKeys_AreStableMultiPingMonitorRange()
        {
            Assert.Equal("multipingmonitor", LanguagePackKeys.AppId);
            Assert.Equal("MultiPingMonitor", LanguagePackKeys.AppName);
            Assert.Equal(20000, LanguagePackKeys.FirstKey);
            Assert.Equal(522, LanguagePackKeys.EntryCount);
            Assert.Equal(522, LanguagePackKeys.ResourceKeys.Count);
            Assert.Equal("About_AccountNotConnectedStatus", LanguagePackKeys.ResourceKeys[20000]);
            var firstKey = LanguagePackKeys.ResourceKeys.Keys.Min();
            var lastKey = LanguagePackKeys.ResourceKeys.Keys.Max();

            Assert.Equal(20000, firstKey);
            Assert.Equal(20521, lastKey);
            Assert.Equal(LanguagePackKeys.EntryCount, lastKey - firstKey + 1);
            Assert.Contains("Compact_Footer_Refresh", LanguagePackKeys.ResourceKeys.Values);
        }

        [Fact]
        public void SlovakSeed_CoversEveryLanguagePackKey()
        {
            Assert.Equal(LanguagePackKeys.EntryCount, LanguagePackSeeds.Slovak.Count);

            var keys = LanguagePackSeeds.Slovak.Select(e => e.Key).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
            Assert.All(keys, key => Assert.True(LanguagePackKeys.ResourceKeys.ContainsKey(key)));
        }

        [Fact]
        public void SlovakSeed_UsesVasoLanguagePackFormatEntries()
        {
            var yes = LanguagePackSeeds.Slovak.Single(e => e.ResourceKey == "DialogButton_Yes");
            var no = LanguagePackSeeds.Slovak.Single(e => e.ResourceKey == "DialogButton_No");
            var confirm = LanguagePackSeeds.Slovak.Single(e => e.ResourceKey == "DialogTitle_Confirm");
            var info = LanguagePackSeeds.Slovak.Single(e => e.ResourceKey == "DialogTitle_Information");

            Assert.Equal("Áno", yes.Text);
            Assert.Equal("Nie", no.Text);
            Assert.Equal("Potvrdenie", confirm.Text);
            Assert.Equal("Informácia", info.Text);
        }

        [Fact]
        public void SlovakSeed_PlaceholdersMatchEnglishSource()
        {
            foreach (var entry in LanguagePackSeeds.Slovak)
            {
                Assert.Equal(Placeholders(entry.Source), Placeholders(entry.Text));
            }
        }

        [Fact]
        public void LanguagePackService_DiscoversAndParsesSlovakPack()
        {
            LanguagePackService.EnsureSeedLanguagePacks();

            Assert.True(Directory.Exists(LanguagePackService.LanguageDirectory));
            Assert.True(File.Exists(LanguagePackService.SlovakLanguagePackPath));

            var meta = LanguagePackService.ReadMetadata(LanguagePackService.SlovakLanguagePackPath);
            Assert.Equal("multipingmonitor", meta["app-id"]);
            Assert.Equal("MultiPingMonitor", meta["app-name"]);
            Assert.Equal("sk-SK", meta["language-code"]);
            Assert.Equal("Slovenčina", meta["native-name"]);
            Assert.Equal("1", meta["format-version"]);

            var translations = LanguagePackService.LoadTranslations("sk-SK");
            Assert.Equal(LanguagePackKeys.EntryCount, translations.Count);
            Assert.Contains(translations, p => p.Key == 20004 && p.Value == "Skontrolovať aktualizácie");
        }

        [Fact]
        public void ExistingLanguagePack_IsNotOverwrittenWhenTextWasEdited()
        {
            LanguagePackService.EnsureSeedLanguagePacks();

            var file = LanguagePackService.SlovakLanguagePackPath;
            var original = File.ReadAllText(file);
            try
            {
                var edited = Regex.Replace(
                    original,
                    @"KEY := 20004 \|\| SOURCE := Check for updates \|\| TEXT := .*",
                    "KEY := 20004 || SOURCE := Check for updates || TEXT := MOJ PREKLAD");

                File.WriteAllText(file, edited);

                LanguagePackService.EnsureSeedLanguagePacks();

                var after = File.ReadAllText(file);
                Assert.Contains("KEY := 20004 || SOURCE := Check for updates || TEXT := MOJ PREKLAD", after);
            }
            finally
            {
                File.WriteAllText(file, original);
            }
        }

        [Fact]
        public void ExternalLanguageResourceManager_UsesExternalTranslationAndEnglishFallback()
        {
            var fallback = new TestResourceManager();
            var manager = new ExternalLanguageResourceManager(
                fallback,
                new Dictionary<int, string>
                {
                    [20034] = "Externý ping",
                });

            Assert.Equal("Externý ping", manager.GetString("Button_Ping", new CultureInfo("sk-SK")));
            Assert.Equal("fallback:Button_Stop", manager.GetString("Button_Stop", new CultureInfo("sk-SK")));
        }

        private sealed class TestResourceManager : ResourceManager
        {
            public override string GetString(string name, CultureInfo? culture)
            {
                return "fallback:" + name;
            }
        }

        private static IReadOnlyList<string> Placeholders(string text)
        {
            return Regex.Matches(text ?? string.Empty, @"\{[0-9]+\}")
                .Cast<Match>()
                .Select(m => m.Value)
                .Distinct()
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList();
        }
    }
}
