using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Visual style modes: Classic preserves the original look, Modern applies a cleaner dark dashboard style.
    /// </summary>
    public enum VisualStyle
    {
        Classic = 0,
        Modern = 1
    }

    /// <summary>
    /// Centralized manager for switching between Classic and Modern visual styles.
    /// Works alongside ThemeManager (which controls color palettes) by inserting an
    /// additional style resource dictionary that overrides control templates, spacing,
    /// corner radii, and other structural visual properties.
    /// </summary>
    public static class VisualStyleManager
    {
        private static VisualStyle _currentStyle = VisualStyle.Classic;

        private static readonly Uri StyleDictUri_Classic =
            new Uri("Styles/VisualStyle.Classic.xaml", UriKind.Relative);
        private static readonly Uri StyleDictUri_Modern =
            new Uri("Styles/VisualStyle.Modern.xaml", UriKind.Relative);

        public static VisualStyle CurrentStyle => _currentStyle;

        /// <summary>
        /// Applies the specified visual style globally. All windows using DynamicResource
        /// will update immediately.
        /// </summary>
        public static void ApplyStyle(VisualStyle style)
        {
            _currentStyle = style;
            Uri styleUri = style switch
            {
                VisualStyle.Modern => StyleDictUri_Modern,
                _ => StyleDictUri_Classic
            };

            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            var newStyleDict = new ResourceDictionary { Source = styleUri };

            // Find and replace existing visual style dictionary, or insert after the theme dict.
            // Use Remove+Insert instead of indexer replacement to ensure WPF raises
            // proper change notifications and all DynamicResource bindings re-evaluate.
            for (int i = 0; i < mergedDicts.Count; i++)
            {
                if (IsVisualStyleDictionary(mergedDicts[i]))
                {
                    mergedDicts.RemoveAt(i);
                    mergedDicts.Insert(i, newStyleDict);
                    return;
                }
            }

            // Not found – insert at position 1 (right after the theme color dictionary at [0]).
            int insertIndex = mergedDicts.Count > 0 ? 1 : 0;
            mergedDicts.Insert(insertIndex, newStyleDict);
        }

        /// <summary>
        /// Detects whether a resource dictionary is a visual style dictionary by its URI.
        /// </summary>
        private static bool IsVisualStyleDictionary(ResourceDictionary dict)
        {
            if (dict.Source == null) return false;
            string src = dict.Source.OriginalString;
            return src.StartsWith("Styles/VisualStyle.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a string setting value to a VisualStyle enum. Defaults to Classic.
        /// </summary>
        public static VisualStyle ParseStyle(string name)
        {
            return name switch
            {
                "Modern" => VisualStyle.Modern,
                _ => VisualStyle.Classic
            };
        }

        /// <summary>
        /// Returns the display name for a visual style.
        /// </summary>
        public static string GetStyleName(VisualStyle style)
        {
            return style switch
            {
                VisualStyle.Classic => "Classic",
                VisualStyle.Modern => "Modern",
                _ => "Classic"
            };
        }

        /// <summary>
        /// Applies native OS-level rounded corners (Windows 11+) to the given window
        /// based on the current visual style. Modern uses ROUNDSMALL for a subtle rounding;
        /// Classic uses DONOTROUND to keep window corners square.
        /// Falls back silently on Windows 10 and earlier.
        /// </summary>
        public static void ApplyNativeWindowCorners(Window window)
        {
            if (window == null) return;
            try
            {
                var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
                if (hwndSource == null) return;

                IntPtr hwnd = hwndSource.Handle;
                DwmWindowCornerPreference pref = _currentStyle == VisualStyle.Modern
                    ? DwmWindowCornerPreference.ROUNDSMALL
                    : DwmWindowCornerPreference.DONOTROUND;

                int value = (int)pref;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
            catch
            {
                // DWM API unavailable on this platform – ignore silently.
            }
        }

        // ── DWM P/Invoke ─────────────────────────────────────────────────────

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        private enum DwmWindowCornerPreference
        {
            DEFAULT    = 0,
            DONOTROUND = 1,
            ROUND      = 2,
            ROUNDSMALL = 3,
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}
