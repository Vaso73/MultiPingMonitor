using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Persists and restores size/position of WPF windows.
    /// Multi-monitor safe: if the saved position is not visible on any screen,
    /// the window is moved to the primary screen.
    /// </summary>
    internal static class WindowPlacementService
    {
        // Key format: "Window.<WindowKey>.Left" etc.
        private const string Prefix = "Window.";

        /// <summary>
        /// Attaches placement persistence to the given window using the specified key.
        /// Call this from the window's constructor (after InitializeComponent).
        /// </summary>
        public static void Attach(Window window, string windowKey)
        {
            window.SourceInitialized += (s, e) => RestorePlacement(window, windowKey);
            window.Closing += (s, e) => SavePlacement(window, windowKey);
        }

        private static void SavePlacement(Window window, string key)
        {
            double left, top, width, height;

            if (window.WindowState == WindowState.Normal)
            {
                left = window.Left;
                top = window.Top;
                width = window.Width;
                height = window.Height;
            }
            else
            {
                // When maximized/minimized, save the restore bounds.
                left = window.RestoreBounds.Left;
                top = window.RestoreBounds.Top;
                width = window.RestoreBounds.Width;
                height = window.RestoreBounds.Height;
            }

            ApplicationOptions.WindowPlacements[key + ".Left"] = left.ToString("F0");
            ApplicationOptions.WindowPlacements[key + ".Top"] = top.ToString("F0");
            ApplicationOptions.WindowPlacements[key + ".Width"] = width.ToString("F0");
            ApplicationOptions.WindowPlacements[key + ".Height"] = height.ToString("F0");
            ApplicationOptions.WindowPlacements[key + ".State"] = window.WindowState.ToString();
        }

        private static void RestorePlacement(Window window, string key)
        {
            var p = ApplicationOptions.WindowPlacements;

            if (!p.TryGetValue(key + ".Left", out string leftStr) ||
                !p.TryGetValue(key + ".Top", out string topStr) ||
                !p.TryGetValue(key + ".Width", out string widthStr) ||
                !p.TryGetValue(key + ".Height", out string heightStr))
            {
                // No saved placement — use defaults.
                return;
            }

            if (!double.TryParse(leftStr, out double left) ||
                !double.TryParse(topStr, out double top) ||
                !double.TryParse(widthStr, out double width) ||
                !double.TryParse(heightStr, out double height))
            {
                return;
            }

            // Clamp minimum size.
            if (width < 100) width = 100;
            if (height < 50) height = 50;

            // Check if the saved position is visible on at least one screen.
            if (!IsVisibleOnAnyScreen(left, top, width, height))
            {
                // Move to primary screen, keep the same size.
                var primary = Screen.PrimaryScreen.WorkingArea;
                left = primary.Left;
                top = primary.Top;
            }

            window.Left = left;
            window.Top = top;
            window.Width = width;
            window.Height = height;

            // Restore window state (must be done after setting bounds).
            if (p.TryGetValue(key + ".State", out string stateStr) &&
                Enum.TryParse(stateStr, out WindowState state))
            {
                window.WindowState = state;
            }
        }

        private static bool IsVisibleOnAnyScreen(double left, double top, double width, double height)
        {
            // A window is considered visible if its top-left quadrant overlaps any monitor.
            var testRect = new System.Drawing.Rectangle(
                (int)left, (int)top, (int)width, (int)height);

            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(testRect))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
