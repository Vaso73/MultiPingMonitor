using System;
using System.Linq;
using System.Windows;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Manages application-wide theme switching (Light / Dark).
    /// </summary>
    internal static class ThemeService
    {
        private const string LightThemeUri = "Themes/Light.xaml";
        private const string DarkThemeUri  = "Themes/Dark.xaml";

        /// <summary>
        /// Applies the theme stored in <see cref="ApplicationOptions.Theme"/>.
        /// Can be called at startup and whenever the user changes the theme.
        /// </summary>
        public static void Apply()
        {
            Apply(ApplicationOptions.Theme);
        }

        /// <summary>
        /// Applies the given theme, replacing any existing theme dictionary.
        /// </summary>
        public static void Apply(ApplicationOptions.AppTheme theme)
        {
            ApplicationOptions.Theme = theme;

            var uri = theme == ApplicationOptions.AppTheme.Dark
                ? new Uri(DarkThemeUri, UriKind.Relative)
                : new Uri(LightThemeUri, UriKind.Relative);

            var merged = Application.Current.Resources.MergedDictionaries;

            // Remove existing theme dictionaries.
            var existing = merged
                .Where(d => d.Source != null &&
                            (d.Source.OriginalString.Contains("Light.xaml") ||
                             d.Source.OriginalString.Contains("Dark.xaml")))
                .ToList();
            foreach (var d in existing)
            {
                merged.Remove(d);
            }

            // Add the new theme dictionary at position 0 so it can be
            // overridden by subsequent merged dictionaries if needed.
            merged.Insert(0, new ResourceDictionary { Source = uri });
        }
    }
}
