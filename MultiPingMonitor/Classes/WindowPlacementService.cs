using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Xml.Linq;

namespace MultiPingMonitor.Classes
{
    // ── Window Placement Service v2 ────────────────────────────────────────────
    //
    // Improvements over v1:
    //   • Stores the monitor device name so we can detect monitor topology changes.
    //   • Stores a snapshot of the monitor's working area and DPI at save-time.
    //   • Applies the normal bounds before restoring maximized state (prevents
    //     WPF from maximizing to the wrong monitor).
    //   • Clamps saved bounds to the current working area when the target monitor
    //     is found but the saved size exceeds the current area.
    //   • Proportional DPI rescale: if the DPI changed since the last save, the
    //     bounds are scaled to keep the same relative size on screen.
    //   • Enforces a minimum-visible margin so the title bar is always reachable.
    //   • Falls back to primary monitor (centered) when the saved monitor is gone
    //     and no other monitor contains the saved rect.
    //   • Respects ApplicationOptions.RememberWindowPosition: when false, the
    //     service still attaches (no-op at restore time) so future sessions pick
    //     up the option without needing to re-Attach.
    //   • Stores a schema version attribute ("v") so future changes can detect
    //     and migrate old records.
    //
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reusable helper that persists and restores window position, size, and state.
    /// Attach once per window after InitializeComponent().
    /// Placements are stored in-memory and flushed to the XML config via Configuration.
    /// </summary>
    internal static class WindowPlacementService
    {
        // Minimum pixels of window that must remain visible on-screen (title bar reachable).
        private const int MinVisibleMargin = 40;

        // Current schema version written to the XML attribute "v".
        private const int SchemaVersion = 2;

        // Minimum usable window dimensions to prevent a window from becoming invisible.
        private const double MinWindowWidth = 100;
        private const double MinWindowHeight = 60;

        // DPI difference tolerance before triggering a proportional rescale.
        // 0.5 is chosen to ignore floating-point rounding on identical DPI settings
        // while still catching a real DPI change (e.g. 96→120 → delta = 24).
        private const double DpiChangeTolerance = 0.5;

        private static readonly Dictionary<string, PlacementData> _placements = new Dictionary<string, PlacementData>();

        /// <summary>
        /// Attach placement persistence to a window.
        /// Call this in the window constructor after InitializeComponent() and
        /// after Configuration.Load() so that saved placement data is available
        /// for immediate restore before the window is shown.
        /// </summary>
        /// <param name="window">The WPF window to track.</param>
        /// <param name="key">Unique key for this window type (e.g. "MainWindow").</param>
        public static void Attach(Window window, string key)
        {
            // Apply saved placement immediately, before the Win32 window handle
            // is created.  When Left/Top/Width/Height are set before Show(),
            // WPF passes them directly to CreateWindowEx, making the restore
            // deterministic.  Relying solely on the SourceInitialized event was
            // unreliable because the handle had already been created with the
            // XAML-default dimensions and a subsequent SetWindowPos could be
            // overridden by the system during ShowWindow processing.
            Restore(window, key);

            // Retained as a safety-net for any window whose placement data is
            // loaded after Attach (currently none, but keeps the contract safe).
            window.SourceInitialized += (s, e) => Restore(window, key);
            window.Closing += (s, e) => Save(window, key);
        }

        /// <summary>
        /// Immediately capture and store the current placement of a window.
        /// Call this explicitly before Configuration.Save() for the main window,
        /// where the Closing event ordering would otherwise cause the placement to
        /// be written after the config has already been serialized to disk.
        /// </summary>
        /// <param name="window">The window to snapshot.</param>
        /// <param name="key">The same key used in the matching Attach() call.</param>
        public static void SaveWindow(Window window, string key)
        {
            Save(window, key);
        }

        // ── Save ──────────────────────────────────────────────────────────────

        private static void Save(Window window, string key)
        {
            if (!ApplicationOptions.RememberWindowPosition)
                return;

            var data = new PlacementData();

            // Always persist the normal (restored) bounds.
            if (window.WindowState == WindowState.Normal)
            {
                data.Left = window.Left;
                data.Top = window.Top;
                data.Width = window.Width;
                data.Height = window.Height;
                data.WindowState = WindowState.Normal;
            }
            else
            {
                // Maximized or Minimized: use RestoreBounds so we keep the normal rect.
                var rb = window.RestoreBounds;
                if (!rb.IsEmpty)
                {
                    data.Left = rb.Left;
                    data.Top = rb.Top;
                    data.Width = rb.Width;
                    data.Height = rb.Height;
                }
                else
                {
                    data.Left = window.Left;
                    data.Top = window.Top;
                    data.Width = window.Width;
                    data.Height = window.Height;
                }

                // Persist Maximized as-is; flatten Minimized to Normal.
                data.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Maximized
                    : WindowState.Normal;
            }

            // Capture current monitor context.
            var screen = ScreenFromWindow(window);
            if (screen != null)
            {
                data.MonitorDeviceName = screen.DeviceName;
                data.SavedMonitorWorkingArea = screen.WorkingArea;
                data.SavedDpiX = GetDpiX(window);
                data.SavedDpiY = GetDpiY(window);
            }

            data.SavedAt = DateTime.UtcNow;
            data.SchemaVersion = SchemaVersion;

            _placements[key] = data;
        }

        // ── Restore ───────────────────────────────────────────────────────────

        private static void Restore(Window window, string key)
        {
            if (!ApplicationOptions.RememberWindowPosition)
                return;

            if (!_placements.TryGetValue(key, out var data))
                return;

            // Clamp saved dimensions to sensible minimums.
            double w = Math.Max(data.Width, MinWindowWidth);
            double h = Math.Max(data.Height, MinWindowHeight);
            double l = data.Left;
            double t = data.Top;

            // ── Step 1: Try to find the original monitor. ──────────────────
            System.Drawing.Rectangle targetArea = System.Drawing.Rectangle.Empty;

            if (!string.IsNullOrEmpty(data.MonitorDeviceName))
            {
                foreach (var scr in Screen.AllScreens)
                {
                    if (string.Equals(scr.DeviceName, data.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetArea = scr.WorkingArea;
                        break;
                    }
                }
            }

            // ── Step 2: If the saved monitor is gone, look for any monitor
            //           that contains the saved point. ──────────────────────
            if (targetArea.IsEmpty)
            {
                var savedRect = new System.Drawing.Rectangle((int)l, (int)t, (int)w, (int)h);
                foreach (var scr in Screen.AllScreens)
                {
                    if (scr.WorkingArea.IntersectsWith(savedRect))
                    {
                        targetArea = scr.WorkingArea;
                        break;
                    }
                }
            }

            // ── Step 3: Fall back to primary monitor. ──────────────────────
            if (targetArea.IsEmpty)
            {
                targetArea = Screen.PrimaryScreen.WorkingArea;
                // Center on primary.
                l = targetArea.Left + (targetArea.Width - w) / 2.0;
                t = targetArea.Top + (targetArea.Height - h) / 2.0;
            }

            // ── Step 4: DPI rescale if the monitor DPI changed. ───────────
            if (data.SavedDpiX > 0 && data.SavedDpiY > 0 && !targetArea.IsEmpty)
            {
                // Retrieve the DPI of the target monitor at restore time.
                double currentDpiX = GetDpiForScreen(Screen.FromRectangle(targetArea));
                double currentDpiY = currentDpiX; // Windows uses uniform scaling per monitor.

                if (Math.Abs(currentDpiX - data.SavedDpiX) > DpiChangeTolerance)
                {
                    double scaleX = currentDpiX / data.SavedDpiX;
                    double scaleY = currentDpiY / data.SavedDpiY;

                    // Scale the size.
                    w = Math.Round(w * scaleX);
                    h = Math.Round(h * scaleY);

                    // Reposition relative to the monitor so the offset from
                    // the top-left corner is also scaled proportionally.
                    if (!data.SavedMonitorWorkingArea.IsEmpty)
                    {
                        double relX = l - data.SavedMonitorWorkingArea.Left;
                        double relY = t - data.SavedMonitorWorkingArea.Top;
                        l = targetArea.Left + relX * scaleX;
                        t = targetArea.Top + relY * scaleY;
                    }
                }
            }

            // ── Step 5: Clamp to the target working area. ─────────────────
            w = Math.Min(w, targetArea.Width);
            h = Math.Min(h, targetArea.Height);

            // Ensure the window does not extend past the right / bottom edges.
            if (l + w > targetArea.Right)
                l = targetArea.Right - w;
            if (t + h > targetArea.Bottom)
                t = targetArea.Bottom - h;

            // Ensure the top-left corner is not past the left / top edges.
            if (l < targetArea.Left)
                l = targetArea.Left;
            if (t < targetArea.Top)
                t = targetArea.Top;

            // ── Step 6: Enforce minimum-visible margin. ───────────────────
            // At least MinVisibleMargin pixels of the window must sit inside
            // the combined desktop rectangle so the title bar can be reached.
            var desktopBounds = GetCombinedDesktopBounds();

            if (l + MinVisibleMargin > desktopBounds.Right)
                l = desktopBounds.Right - MinVisibleMargin;
            if (t + MinVisibleMargin > desktopBounds.Bottom)
                t = desktopBounds.Bottom - MinVisibleMargin;
            if (l + w < desktopBounds.Left + MinVisibleMargin)
                l = desktopBounds.Left + MinVisibleMargin - w;
            if (t + h < desktopBounds.Top + MinVisibleMargin)
                t = desktopBounds.Top + MinVisibleMargin - h;

            // ── Step 7: Apply bounds, then state. ─────────────────────────
            // Set Normal bounds first so that when the state is Maximized, WPF
            // knows the correct monitor to maximize to.
            window.Left = l;
            window.Top = t;
            window.Width = w;
            window.Height = h;
            window.WindowState = data.WindowState;
        }

        // ── XML serialization ─────────────────────────────────────────────────

        /// <summary>
        /// Serialize all stored placements as an XElement for XML config.
        /// </summary>
        public static XElement GeneratePlacementsNode()
        {
            var node = new XElement("windowPlacements");
            foreach (var kvp in _placements)
            {
                var el = new XElement("window",
                    new XAttribute("v", kvp.Value.SchemaVersion),
                    new XAttribute("key", kvp.Key),
                    new XAttribute("left", kvp.Value.Left),
                    new XAttribute("top", kvp.Value.Top),
                    new XAttribute("width", kvp.Value.Width),
                    new XAttribute("height", kvp.Value.Height),
                    new XAttribute("state", kvp.Value.WindowState));

                if (!string.IsNullOrEmpty(kvp.Value.MonitorDeviceName))
                    el.Add(new XAttribute("monitor", kvp.Value.MonitorDeviceName));

                if (!kvp.Value.SavedMonitorWorkingArea.IsEmpty)
                {
                    el.Add(new XAttribute("monitorLeft", kvp.Value.SavedMonitorWorkingArea.Left));
                    el.Add(new XAttribute("monitorTop", kvp.Value.SavedMonitorWorkingArea.Top));
                    el.Add(new XAttribute("monitorWidth", kvp.Value.SavedMonitorWorkingArea.Width));
                    el.Add(new XAttribute("monitorHeight", kvp.Value.SavedMonitorWorkingArea.Height));
                }

                if (kvp.Value.SavedDpiX > 0)
                    el.Add(new XAttribute("dpiX", kvp.Value.SavedDpiX));
                if (kvp.Value.SavedDpiY > 0)
                    el.Add(new XAttribute("dpiY", kvp.Value.SavedDpiY));

                if (kvp.Value.SavedAt != default)
                    el.Add(new XAttribute("savedAt", kvp.Value.SavedAt.ToString("o")));

                node.Add(el);
            }
            return node;
        }

        /// <summary>
        /// Load placements from an XElement read from the XML config.
        /// Handles both v1 records (no "v" attribute) and v2 records transparently.
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
                        Left = ParseDouble(el, "left"),
                        Top = ParseDouble(el, "top"),
                        Width = ParseDouble(el, "width"),
                        Height = ParseDouble(el, "height"),
                        WindowState = Enum.TryParse<WindowState>((string)el.Attribute("state"), out var ws)
                            ? ws
                            : WindowState.Normal,
                        SchemaVersion = ParseInt(el, "v", 1),

                        // v2 fields (absent in v1 records – safe default when missing).
                        MonitorDeviceName = (string)el.Attribute("monitor"),
                        SavedDpiX = ParseDouble(el, "dpiX"),
                        SavedDpiY = ParseDouble(el, "dpiY"),
                    };

                    // Reconstruct SavedMonitorWorkingArea from individual attributes.
                    int mLeft = ParseInt(el, "monitorLeft");
                    int mTop = ParseInt(el, "monitorTop");
                    int mWidth = ParseInt(el, "monitorWidth");
                    int mHeight = ParseInt(el, "monitorHeight");
                    if (mWidth > 0 && mHeight > 0)
                        data.SavedMonitorWorkingArea = new System.Drawing.Rectangle(mLeft, mTop, mWidth, mHeight);

                    if (DateTime.TryParse((string)el.Attribute("savedAt"), out var dt))
                        data.SavedAt = dt.ToUniversalTime();

                    _placements[key] = data;
                }
                catch
                {
                    // Skip malformed entries.
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Screen ScreenFromWindow(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return Screen.PrimaryScreen;

            var bounds = new System.Drawing.Rectangle(
                (int)window.Left, (int)window.Top,
                (int)window.Width, (int)window.Height);
            return Screen.FromRectangle(bounds);
        }

        private static double GetDpiX(Window window)
        {
            var source = PresentationSource.FromVisual(window);
            return source?.CompositionTarget?.TransformToDevice.M11 * 96.0 ?? 0;
        }

        private static double GetDpiY(Window window)
        {
            var source = PresentationSource.FromVisual(window);
            return source?.CompositionTarget?.TransformToDevice.M22 * 96.0 ?? 0;
        }

        private static double GetDpiForScreen(Screen screen)
        {
            // NOTE: Graphics.FromHwnd(IntPtr.Zero) retrieves the desktop device context,
            // which reports the primary monitor's DPI.  For per-monitor DPI accuracy,
            // the Win32 GetDpiForMonitor API would be needed via P/Invoke.  Using the
            // primary-monitor DPI is an intentional approximation: it is correct for
            // single-monitor setups and for the most common multi-monitor configuration
            // where all monitors share the same DPI scaling.  The error on mixed-DPI
            // setups is bounded by the clamp step that follows, so no window is ever
            // placed fully off-screen.
            try
            {
                using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
                return g.DpiX;
            }
            catch
            {
                return 96.0;
            }
        }

        private static System.Drawing.Rectangle GetCombinedDesktopBounds()
        {
            int left = int.MaxValue, top = int.MaxValue;
            int right = int.MinValue, bottom = int.MinValue;

            foreach (var scr in Screen.AllScreens)
            {
                var wa = scr.WorkingArea;
                if (wa.Left < left) left = wa.Left;
                if (wa.Top < top) top = wa.Top;
                if (wa.Right > right) right = wa.Right;
                if (wa.Bottom > bottom) bottom = wa.Bottom;
            }

            return new System.Drawing.Rectangle(left, top, right - left, bottom - top);
        }

        private static double ParseDouble(XElement el, string attr, double fallback = 0)
        {
            var raw = (string)el.Attribute(attr);
            return double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        private static int ParseInt(XElement el, string attr, int fallback = 0)
        {
            var raw = (string)el.Attribute(attr);
            return int.TryParse(raw, out var v) ? v : fallback;
        }

        // ── Data model ────────────────────────────────────────────────────────

        private class PlacementData
        {
            // Core geometry (always present since v1).
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public WindowState WindowState { get; set; }

            // v2 additions.
            public int SchemaVersion { get; set; } = 1;
            public string MonitorDeviceName { get; set; }
            public System.Drawing.Rectangle SavedMonitorWorkingArea { get; set; }
            public double SavedDpiX { get; set; }
            public double SavedDpiY { get; set; }
            public DateTime SavedAt { get; set; }
        }
    }
}
