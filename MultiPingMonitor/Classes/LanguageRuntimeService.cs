using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace MultiPingMonitor.Classes
{
    public static class LanguageRuntimeService
    {
        public const string SystemLanguageCode = "System";
        public const string EnglishLanguageCode = "en";

        private static readonly CultureInfo SystemDefaultCulture = CultureInfo.CurrentCulture;
        private static readonly CultureInfo SystemDefaultUICulture = CultureInfo.CurrentUICulture;

        public static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return SystemLanguageCode;

            var trimmed = languageCode.Trim();

            if (string.Equals(trimmed, "System", StringComparison.OrdinalIgnoreCase))
                return SystemLanguageCode;

            if (string.Equals(trimmed, "English", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "en", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "en-US", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "en-GB", StringComparison.OrdinalIgnoreCase))
            {
                return EnglishLanguageCode;
            }

            if (string.Equals(trimmed, "Slovak", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "sk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "sk-SK", StringComparison.OrdinalIgnoreCase))
            {
                return LanguagePackService.DefaultExternalLanguageCode;
            }

            return trimmed;
        }


        public static IReadOnlyDictionary<string, string> CaptureResourceSnapshot()
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var resourceName in LanguagePackKeys.ResourceKeys.Values.Distinct(StringComparer.Ordinal))
            {
                var value = MultiPingMonitor.Properties.Strings.ResourceManager.GetString(resourceName);
                if (!string.IsNullOrEmpty(value))
                    snapshot[resourceName] = value;
            }

            return snapshot;
        }

        public static CultureInfo ApplyLanguage(string languageCode)
        {
            LanguagePackService.EnsureSeedLanguagePacks();

            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
            var culture = ResolveCulture(normalizedLanguageCode);
            var resourceManager = CreateResourceManager(normalizedLanguageCode, culture);

            InstallResourceManager(resourceManager);

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            MultiPingMonitor.Properties.Strings.Culture = culture;

            return culture;
        }

        private static CultureInfo GetSystemDefaultCulture()
        {
            if (!string.IsNullOrWhiteSpace(SystemDefaultCulture.Name))
                return new CultureInfo(SystemDefaultCulture.Name);

            if (!string.IsNullOrWhiteSpace(SystemDefaultUICulture.Name))
                return new CultureInfo(SystemDefaultUICulture.Name);

            return CultureInfo.InvariantCulture;
        }

        public static CultureInfo ResolveCulture(string languageCode)
        {
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            if (string.Equals(normalizedLanguageCode, SystemLanguageCode, StringComparison.OrdinalIgnoreCase))
                return GetSystemDefaultCulture();

            if (string.Equals(normalizedLanguageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
                return new CultureInfo(EnglishLanguageCode);

            try
            {
                return new CultureInfo(normalizedLanguageCode);
            }
            catch (CultureNotFoundException)
            {
                return new CultureInfo(EnglishLanguageCode);
            }
        }

        private static ResourceManager CreateResourceManager(string normalizedLanguageCode, CultureInfo culture)
        {
            var fallbackResourceManager = new ResourceManager(
                "MultiPingMonitor.Properties.Strings",
                typeof(MultiPingMonitor.Properties.Strings).Assembly);

            var externalLanguageCode = ResolveExternalLanguageCode(normalizedLanguageCode, culture);
            var translations = externalLanguageCode == null
                ? new Dictionary<int, string>()
                : LanguagePackService.LoadTranslations(externalLanguageCode);

            return new ExternalLanguageResourceManager(fallbackResourceManager, translations);
        }

        private static string ResolveExternalLanguageCode(string normalizedLanguageCode, CultureInfo culture)
        {
            if (string.Equals(normalizedLanguageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
                return null;

            if (!string.Equals(normalizedLanguageCode, SystemLanguageCode, StringComparison.OrdinalIgnoreCase))
                return normalizedLanguageCode;

            var packs = LanguagePackService.DiscoverLanguagePacks();

            var currentNames = new[]
                {
                    SystemDefaultCulture.Name,
                    SystemDefaultUICulture.Name,
                    culture.Name,
                    SystemDefaultCulture.TwoLetterISOLanguageName,
                    SystemDefaultUICulture.TwoLetterISOLanguageName,
                    culture.TwoLetterISOLanguageName,
                }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var currentName in currentNames)
            {
                var exact = packs.FirstOrDefault(
                    pack => string.Equals(pack.LanguageCode, currentName, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return exact.LanguageCode;

                var prefix = currentName + "-";
                var parentMatch = packs.FirstOrDefault(
                    pack => pack.LanguageCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (parentMatch != null)
                    return parentMatch.LanguageCode;
            }

            return null;
        }

        private static void InstallResourceManager(ResourceManager resourceManager)
        {
            var field = typeof(MultiPingMonitor.Properties.Strings).GetField(
                "resourceMan",
                BindingFlags.NonPublic | BindingFlags.Static);

            field?.SetValue(null, resourceManager);
        }
    }
}
