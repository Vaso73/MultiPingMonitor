using Microsoft.Win32;
using System;
using System.Windows;

namespace MultiPingMonitor.Classes
{
    public enum AppTheme
    {
        Auto,
        Light,
        Dark,
        Nord,
        Dracula,
        SolarizedLight,
        SolarizedDark,
        Forest,
        Ocean,
        Sunset
    }

    public static class ThemeManager
    {
        private static AppTheme _currentTheme = AppTheme.Auto;
        private static readonly Uri ThemeDictUri_Light = new Uri("Themes/Theme.Light.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_Dark = new Uri("Themes/Theme.Dark.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_Nord = new Uri("Themes/Theme.Nord.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_Dracula = new Uri("Themes/Theme.Dracula.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_SolarizedLight = new Uri("Themes/Theme.SolarizedLight.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_SolarizedDark = new Uri("Themes/Theme.SolarizedDark.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_Forest = new Uri("Themes/Theme.Forest.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_Ocean = new Uri("Themes/Theme.Ocean.xaml", UriKind.Relative);
        private static readonly Uri ThemeDictUri_Sunset = new Uri("Themes/Theme.Sunset.xaml", UriKind.Relative);

        public static AppTheme CurrentTheme => _currentTheme;

        public static void ApplyTheme(AppTheme theme)
        {
            _currentTheme = theme;
            Uri themeUri;

            if (theme == AppTheme.Auto)
            {
                themeUri = IsWindowsDarkMode() ? ThemeDictUri_Dark : ThemeDictUri_Light;
            }
            else
            {
                themeUri = theme switch
                {
                    AppTheme.Light => ThemeDictUri_Light,
                    AppTheme.Dark => ThemeDictUri_Dark,
                    AppTheme.Nord => ThemeDictUri_Nord,
                    AppTheme.Dracula => ThemeDictUri_Dracula,
                    AppTheme.SolarizedLight => ThemeDictUri_SolarizedLight,
                    AppTheme.SolarizedDark => ThemeDictUri_SolarizedDark,
                    AppTheme.Forest => ThemeDictUri_Forest,
                    AppTheme.Ocean => ThemeDictUri_Ocean,
                    AppTheme.Sunset => ThemeDictUri_Sunset,
                    _ => ThemeDictUri_Light
                };
            }

            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary newThemeDict = new ResourceDictionary { Source = themeUri };
            if (mergedDicts.Count > 0 && IsThemeDictionary(mergedDicts[0]))
            {
                mergedDicts[0] = newThemeDict;
            }
            else
            {
                mergedDicts.Insert(0, newThemeDict);
            }
        }

        private static bool IsThemeDictionary(ResourceDictionary dict)
        {
            if (dict.Source == null) return false;
            string src = dict.Source.OriginalString;
            return src.StartsWith("Themes/Theme.", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWindowsDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int value)
                    return value == 0;
            }
            catch { }
            return false;
        }

        public static string GetThemeName(AppTheme theme)
        {
            return theme switch
            {
                AppTheme.Auto => "Auto",
                AppTheme.Light => "Light",
                AppTheme.Dark => "Dark",
                AppTheme.Nord => "Nord",
                AppTheme.Dracula => "Dracula",
                AppTheme.SolarizedLight => "Solarized Light",
                AppTheme.SolarizedDark => "Solarized Dark",
                AppTheme.Forest => "Forest",
                AppTheme.Ocean => "Ocean",
                AppTheme.Sunset => "Sunset",
                _ => "Auto"
            };
        }

        public static AppTheme ParseTheme(string themeName)
        {
            return themeName switch
            {
                "Light" => AppTheme.Light,
                "Dark" => AppTheme.Dark,
                "Nord" => AppTheme.Nord,
                "Dracula" => AppTheme.Dracula,
                "Solarized Light" => AppTheme.SolarizedLight,
                "Solarized Dark" => AppTheme.SolarizedDark,
                "Forest" => AppTheme.Forest,
                "Ocean" => AppTheme.Ocean,
                "Sunset" => AppTheme.Sunset,
                _ => AppTheme.Auto
            };
        }
    }
}
