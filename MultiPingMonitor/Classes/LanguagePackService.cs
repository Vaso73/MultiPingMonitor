using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MultiPingMonitor.Classes
{
    public sealed class LanguagePackInfo
    {
        public LanguagePackInfo(
            string languageCode,
            string languageName,
            string nativeName,
            string direction,
            string filePath)
        {
            LanguageCode = languageCode;
            LanguageName = languageName;
            NativeName = nativeName;
            Direction = direction;
            FilePath = filePath;
        }

        public string LanguageCode { get; }
        public string LanguageName { get; }
        public string NativeName { get; }
        public string Direction { get; }
        public string FilePath { get; }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(NativeName))
                return NativeName + " (" + LanguageCode + ")";
            if (!string.IsNullOrWhiteSpace(LanguageName))
                return LanguageName + " (" + LanguageCode + ")";
            return LanguageCode;
        }
    }

    public static class LanguagePackService
    {
        public const string FormatVersion = "1";
        public const string DefaultExternalLanguageCode = "sk-SK";

        private static readonly Regex HeaderRegex =
            new Regex(@"^#\s*(?<name>[A-Za-z0-9\-]+)\s*:=\s*(?<value>.*)$", RegexOptions.Compiled);

        private static readonly Regex EntryRegex =
            new Regex(
                @"^KEY\s*:=\s*(?<key>[0-9]+)\s*\|\|\s*SOURCE\s*:=\s*(?<source>.*?)\s*\|\|\s*TEXT\s*:=\s*(?<text>.*)$",
                RegexOptions.Compiled);

        public static string LanguageDirectory =>
            Path.Combine(AppContext.BaseDirectory, "lang");

        public static string SlovakLanguagePackPath =>
            Path.Combine(LanguageDirectory, "sk-SK.lang");

        public static void EnsureSeedLanguagePacks()
        {
            try
            {
                Directory.CreateDirectory(LanguageDirectory);
                EnsureSeedLanguagePack(
                    SlovakLanguagePackPath,
                    "sk-SK",
                    "Slovak",
                    "Slovenčina",
                    "ltr",
                    LanguagePackSeeds.Slovak);
            }
            catch
            {
                // Language packs are an optional community/localization feature.
                // Startup must never fail because the lang directory is read-only
                // or a user-edited .lang file is malformed.
            }
        }

        public static IReadOnlyList<LanguagePackInfo> DiscoverLanguagePacks()
        {
            try
            {
                EnsureSeedLanguagePacks();
                if (!Directory.Exists(LanguageDirectory))
                    return Array.Empty<LanguagePackInfo>();

                var result = new List<LanguagePackInfo>();
                foreach (var file in Directory.EnumerateFiles(LanguageDirectory, "*.lang"))
                {
                    var meta = ReadMetadata(file);
                    if (!meta.TryGetValue("app-id", out var appId)
                        || !string.Equals(appId, LanguagePackKeys.AppId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!meta.TryGetValue("language-code", out var languageCode)
                        || string.IsNullOrWhiteSpace(languageCode))
                    {
                        continue;
                    }

                    meta.TryGetValue("language-name", out var languageName);
                    meta.TryGetValue("native-name", out var nativeName);
                    meta.TryGetValue("direction", out var direction);

                    result.Add(new LanguagePackInfo(
                        languageCode.Trim(),
                        (languageName ?? string.Empty).Trim(),
                        (nativeName ?? string.Empty).Trim(),
                        string.IsNullOrWhiteSpace(direction) ? "ltr" : direction.Trim(),
                        file));
                }

                return result
                    .OrderBy(p => p.NativeName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(p => p.LanguageCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return Array.Empty<LanguagePackInfo>();
            }
        }

        public static IReadOnlyDictionary<int, string> LoadTranslations(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return new Dictionary<int, string>();

            try
            {
                EnsureSeedLanguagePacks();
                if (!Directory.Exists(LanguageDirectory))
                    return new Dictionary<int, string>();

                var file = Directory
                    .EnumerateFiles(LanguageDirectory, "*.lang")
                    .FirstOrDefault(path =>
                    {
                        var meta = ReadMetadata(path);
                        return meta.TryGetValue("app-id", out var appId)
                            && string.Equals(appId, LanguagePackKeys.AppId, StringComparison.OrdinalIgnoreCase)
                            && meta.TryGetValue("language-code", out var code)
                            && string.Equals(code, languageCode, StringComparison.OrdinalIgnoreCase);
                    });

                if (file == null)
                    return new Dictionary<int, string>();

                return ParseTranslations(file);
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }

        internal static IReadOnlyDictionary<string, string> ReadMetadata(string filePath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                var match = HeaderRegex.Match(line);
                if (!match.Success)
                    continue;

                result[match.Groups["name"].Value.Trim()] =
                    Unescape(match.Groups["value"].Value.Trim());
            }

            return result;
        }

        internal static IReadOnlyDictionary<int, string> ParseTranslations(string filePath)
        {
            var result = new Dictionary<int, string>();

            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                var match = EntryRegex.Match(line);
                if (!match.Success)
                    continue;

                if (!int.TryParse(match.Groups["key"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var key))
                    continue;

                if (!LanguagePackKeys.ResourceKeys.ContainsKey(key))
                    continue;

                var text = Unescape(match.Groups["text"].Value);
                if (string.IsNullOrEmpty(text))
                    continue;

                result[key] = text;
            }

            return result;
        }

        private static void EnsureSeedLanguagePack(
            string filePath,
            string languageCode,
            string languageName,
            string nativeName,
            string direction,
            IReadOnlyList<LanguagePackSeedEntry> entries)
        {
            if (!File.Exists(filePath))
            {
                WriteSeedLanguagePack(filePath, languageCode, languageName, nativeName, direction, entries);
                return;
            }

            MergeMissingSeedEntries(filePath, entries);
        }

        private static void WriteSeedLanguagePack(
            string filePath,
            string languageCode,
            string languageName,
            string nativeName,
            string direction,
            IReadOnlyList<LanguagePackSeedEntry> entries)
        {
            var builder = new StringBuilder();
            AppendHeader(builder, languageCode, languageName, nativeName, direction);
            foreach (var entry in entries.OrderBy(e => e.Key))
            {
                AppendEntry(builder, entry);
            }

            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void MergeMissingSeedEntries(string filePath, IReadOnlyList<LanguagePackSeedEntry> entries)
        {
            var existingText = File.ReadAllText(filePath, Encoding.UTF8);
            var existingKeys = new HashSet<int>();

            foreach (var line in existingText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var match = EntryRegex.Match(line);
                if (!match.Success)
                    continue;
                if (int.TryParse(match.Groups["key"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var key))
                    existingKeys.Add(key);
            }

            var missing = entries
                .Where(e => !existingKeys.Contains(e.Key))
                .OrderBy(e => e.Key)
                .ToList();

            if (missing.Count == 0)
                return;

            var builder = new StringBuilder();
            if (!existingText.EndsWith("\n", StringComparison.Ordinal))
                builder.AppendLine();

            builder.AppendLine();
            builder.AppendLine("# Added by MultiPingMonitor to keep this language pack compatible with the current version.");
            foreach (var entry in missing)
            {
                AppendEntry(builder, entry);
            }

            File.AppendAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void AppendHeader(
            StringBuilder builder,
            string languageCode,
            string languageName,
            string nativeName,
            string direction)
        {
            builder.AppendLine("# Vaso Language Pack Format v1");
            builder.AppendLine("# app-id := " + LanguagePackKeys.AppId);
            builder.AppendLine("# app-name := " + LanguagePackKeys.AppName);
            builder.AppendLine("# language-code := " + languageCode);
            builder.AppendLine("# language-name := " + languageName);
            builder.AppendLine("# native-name := " + nativeName);
            builder.AppendLine("# direction := " + direction);
            builder.AppendLine("# format-version := " + FormatVersion);
            builder.AppendLine("#");
            builder.AppendLine("# Rules:");
            builder.AppendLine("# - edit only TEXT");
            builder.AppendLine("# - keep KEY and SOURCE unchanged");
            builder.AppendLine("# - keep placeholders such as {0}, {1}, {2}");
            builder.AppendLine("# - encode line breaks as \\n");
            builder.AppendLine("# - one entry = one physical line");
            builder.AppendLine();
        }

        private static void AppendEntry(StringBuilder builder, LanguagePackSeedEntry entry)
        {
            builder.Append("KEY := ");
            builder.Append(entry.Key.ToString(CultureInfo.InvariantCulture));
            builder.Append(" || SOURCE := ");
            builder.Append(Escape(entry.Source));
            builder.Append(" || TEXT := ");
            builder.Append(Escape(entry.Text));
            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\r\n", "\\n")
                .Replace("\r", "\\n")
                .Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            if (value == null)
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    var next = value[i + 1];
                    if (next == 'n')
                    {
                        builder.Append('\n');
                        i++;
                        continue;
                    }
                    if (next == '\\')
                    {
                        builder.Append('\\');
                        i++;
                        continue;
                    }
                }

                builder.Append(value[i]);
            }

            return builder.ToString();
        }
    }
}
