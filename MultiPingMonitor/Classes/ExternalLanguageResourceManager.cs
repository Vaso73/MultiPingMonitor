#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace MultiPingMonitor.Classes
{
    internal sealed class ExternalLanguageResourceManager : ResourceManager
    {
        private static readonly CultureInfo EnglishFallbackCulture = CultureInfo.InvariantCulture;

        private readonly ResourceManager fallbackResourceManager;
        private readonly IReadOnlyDictionary<string, string> translationsByResourceName;

        public ExternalLanguageResourceManager(
            ResourceManager fallbackResourceManager,
            IReadOnlyDictionary<int, string> translationsByKey)
        {
            this.fallbackResourceManager = fallbackResourceManager
                ?? throw new ArgumentNullException(nameof(fallbackResourceManager));

            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in translationsByKey ?? new Dictionary<int, string>())
            {
                if (!LanguagePackKeys.ResourceKeys.TryGetValue(pair.Key, out var resourceName))
                    continue;

                if (string.IsNullOrEmpty(pair.Value))
                    continue;

                translations[resourceName] = pair.Value;
            }

            translationsByResourceName = translations;
        }

        public override string GetString(string name, CultureInfo? culture)
        {
            if (!string.IsNullOrEmpty(name)
                && translationsByResourceName.TryGetValue(name, out var translated))
            {
                return translated;
            }

            return fallbackResourceManager.GetString(name, EnglishFallbackCulture) ?? name;
        }
    }
}
