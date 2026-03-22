using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Reusable helper that persists and restores window position, size, and state.
    /// Attach once per window after InitializeComponent().
    /// Placements are stored in-memory and flushed to the XML config via Configuration.
    /// </summary>
    internal static class WindowPlacementService
    {
        private static readonly Dictionary<string, PlacementData> _placements = new Dictionary<string, PlacementData>();

        /// <summary>
        /// Attach placement persistence to a window.
        /// Call this in the window constructor after InitializeComponent().
        /// </summary>
        /// <param name="window">The WPF window to track.</param>
        /// <param name="key">Unique key for this window type (e.g. "MainWindow").</param>
        public static void Attach(Window window, string key)
        {
            window.SourceInitialized += (s, e) => Restore(window, key);
            window.Closing += (s, e) => Save(window, key);
        }

        private static void Save(Window window, string key)
        {
            var data = new PlacementData();

            if (window.WindowState == WindowState.Maximized)
            {
                // Use RestoreBounds so we remember the normal-state rectangle.
                var rb = window.RestoreBounds;
                data.Left = rb.Left;
                data.Top = rb.Top;
                data.Width = rb.Width;
                data.Height = rb.Height;
                data.WindowState = WindowState.Maximized;
            }
            else if (window.WindowState == WindowState.Minimized)
            {
                // If minimized, save the RestoreBounds as Normal.
                var rb = window.RestoreBounds;
                data.Left = rb.Left;
                data.Top = rb.Top;
                data.Width = rb.Width;
                data.Height = rb.Height;
                data.WindowState = WindowState.Normal;
            }
            else
            {
                data.Left = window.Left;
                data.Top = window.Top;
                data.Width = window.Width;
                data.Height = window.Height;
                data.WindowState = WindowState.Normal;
            }

            _placements[key] = data;
        }

        private static void Restore(Window window, string key)
        {
            if (!_placements.TryGetValue(key, out var data))
                return;

            // Check that the saved rectangle is visible on at least one monitor.
            var savedRect = new System.Drawing.Rectangle(
                (int)data.Left, (int)data.Top,
                (int)data.Width, (int)data.Height);

            bool isVisible = false;
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(savedRect))
                {
                    isVisible = true;
                    break;
                }
            }

            if (isVisible)
            {
                window.Left = data.Left;
                window.Top = data.Top;
                window.Width = data.Width;
                window.Height = data.Height;
            }
            else
            {
                // Saved position is not on any monitor; move to primary monitor center.
                var primary = Screen.PrimaryScreen.WorkingArea;
                window.Left = primary.Left + (primary.Width - data.Width) / 2;
                window.Top = primary.Top + (primary.Height - data.Height) / 2;
                window.Width = data.Width;
                window.Height = data.Height;
            }

            window.WindowState = data.WindowState;
        }

        /// <summary>
        /// Serialize all stored placements as an XElement for XML config.
        /// </summary>
        public static XElement GeneratePlacementsNode()
        {
            var node = new XElement("windowPlacements");
            foreach (var kvp in _placements)
            {
                node.Add(new XElement("window",
                    new XAttribute("key", kvp.Key),
                    new XAttribute("left", kvp.Value.Left),
                    new XAttribute("top", kvp.Value.Top),
                    new XAttribute("width", kvp.Value.Width),
                    new XAttribute("height", kvp.Value.Height),
                    new XAttribute("state", kvp.Value.WindowState)));
            }
            return node;
        }

        /// <summary>
        /// Load placements from an XElement read from the XML config.
        /// </summary>
        public static void LoadPlacements(XElement placementsNode)
        {
            if (placementsNode == null)
                return;

            foreach (var el in placementsNode.Elements("window"))
            {
                var key = (string)el.Attribute("key");
                if (string.IsNullOrEmpty(key))
                    continue;

                try
                {
                    var data = new PlacementData
                    {
                        Left = (double)el.Attribute("left"),
                        Top = (double)el.Attribute("top"),
                        Width = (double)el.Attribute("width"),
                        Height = (double)el.Attribute("height"),
                        WindowState = Enum.TryParse<WindowState>((string)el.Attribute("state"), out var ws)
                            ? ws
                            : WindowState.Normal
                    };
                    _placements[key] = data;
                }
                catch
                {
                    // Skip malformed entries.
                }
            }
        }

        private class PlacementData
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public WindowState WindowState { get; set; }
        }
    }
}
