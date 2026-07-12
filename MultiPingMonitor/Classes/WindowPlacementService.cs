using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Persists exact per-machine window placement and a portable logical fallback.
    /// Geometry remains in WPF logical units. No physical-pixel or manual DPI
    /// conversion is performed.
    /// </summary>
    internal static class WindowPlacementService
    {
        private const int SchemaVersion = 4;

        private static readonly Dictionary<string, PlacementData>
            PortablePlacements =
                new Dictionary<string, PlacementData>();

        private static readonly Dictionary<string, PlacementData>
            MachinePlacements =
                new Dictionary<string, PlacementData>();

        public static void Attach(
            Window window,
            string key,
            bool saveOnClosing = true)
        {
            Restore(window, key);

            window.SourceInitialized += (s, e) =>
                Restore(window, key);

            if (ApplicationOptions.RememberWindowPosition &&
                HasPlacement(key))
            {
                EventHandler contentRenderedHandler = null;
                contentRenderedHandler = (s, e) =>
                {
                    window.ContentRendered -= contentRenderedHandler;

                    if (!ApplicationOptions.RememberWindowPosition ||
                        !HasPlacement(key))
                    {
                        return;
                    }

                    window.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Loaded,
                        new Action(() => Restore(window, key)));
                };

                window.ContentRendered += contentRenderedHandler;
            }

            if (saveOnClosing)
            {
                window.Closing += (s, e) => Save(window, key);
            }
        }

        public static void SaveWindow(Window window, string key)
        {
            Save(window, key);
        }

        public static void RestoreWindow(Window window, string key)
        {
            Restore(window, key);
        }

        public static bool HasPlacement(string key)
        {
            return MachinePlacements.ContainsKey(key) ||
                   PortablePlacements.ContainsKey(key);
        }

        private static void Save(Window window, string key)
        {
            if (!ApplicationOptions.RememberWindowPosition)
                return;

            PlacementData data = CapturePlacement(window);
            if (data == null)
                return;

            PortablePlacements[key] = data.Clone();
            MachinePlacements[key] = data.Clone();
            PersistMachinePlacements();
        }

        private static PlacementData CapturePlacement(Window window)
        {
            Rect bounds;

            if (window.WindowState == WindowState.Normal)
            {
                bounds = new Rect(
                    window.Left,
                    window.Top,
                    ResolveDimension(window.Width, window.ActualWidth),
                    ResolveDimension(window.Height, window.ActualHeight));
            }
            else
            {
                bounds = window.RestoreBounds;
            }

            if (!IsUsable(bounds))
                return null;

            var savedRect = new LogicalRect(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height);

            var data = new PlacementData
            {
                Left = savedRect.Left,
                Top = savedRect.Top,
                Width = savedRect.Width,
                Height = savedRect.Height,
                WindowState =
                    window.WindowState == WindowState.Maximized
                        ? WindowState.Maximized
                        : WindowState.Normal,
                SchemaVersion = SchemaVersion,
                SavedAt = DateTime.UtcNow
            };

            Screen screen = Screen.FromRectangle(
                ToDrawingRectangle(savedRect));

            if (screen != null)
            {
                data.MonitorDeviceName = screen.DeviceName;
                data.SavedMonitorWorkingArea = screen.WorkingArea;
            }

            return data;
        }

        private static void Restore(Window window, string key)
        {
            if (!ApplicationOptions.RememberWindowPosition)
                return;

            bool hasMachinePlacement =
                MachinePlacements.TryGetValue(
                    key,
                    out PlacementData data);

            if (!hasMachinePlacement &&
                !PortablePlacements.TryGetValue(key, out data))
            {
                return;
            }

            IReadOnlyList<MonitorGeometry> monitors =
                Screen.AllScreens
                    .Select(ToMonitorGeometry)
                    .ToArray();

            if (monitors.Count == 0)
                return;

            var savedRect = new LogicalRect(
                data.Left,
                data.Top,
                data.Width,
                data.Height);

            int monitorIndex = hasMachinePlacement
                ? WindowPlacementGeometry.SelectTargetMonitor(
                    monitors,
                    data.MonitorDeviceName,
                    savedRect)
                : WindowPlacementGeometry.SelectPrimaryMonitor(monitors);

            MonitorGeometry targetMonitor = monitors[monitorIndex];

            var sourceWorkArea = ToLogicalRect(
                data.SavedMonitorWorkingArea);

            LogicalRect restoredRect =
                WindowPlacementGeometry.RemapToWorkArea(
                    savedRect,
                    sourceWorkArea,
                    targetMonitor.WorkingArea);

            WindowState desiredState =
                data.WindowState == WindowState.Maximized
                    ? WindowState.Maximized
                    : WindowState.Normal;

            if (window.WindowState != WindowState.Normal)
                window.WindowState = WindowState.Normal;

            window.WindowStartupLocation =
                WindowStartupLocation.Manual;

            window.Left = restoredRect.Left;
            window.Top = restoredRect.Top;
            window.Width = restoredRect.Width;
            window.Height = restoredRect.Height;
            window.WindowState = desiredState;
        }

        public static XElement GeneratePlacementsNode()
        {
            return GeneratePlacementsNode(PortablePlacements);
        }

        public static void LoadPlacements(XElement placementsNode)
        {
            LoadPortablePlacements(placementsNode);
        }

        public static void LoadPortablePlacements(
            XElement placementsNode)
        {
            LoadInto(PortablePlacements, placementsNode);
        }

        public static void LoadMachinePlacements(
            XElement placementsNode)
        {
            LoadInto(MachinePlacements, placementsNode);
        }

        private static void LoadInto(
            Dictionary<string, PlacementData> destination,
            XElement placementsNode)
        {
            destination.Clear();

            if (placementsNode == null)
                return;

            foreach (XElement element in
                placementsNode.Elements("window"))
            {
                string key =
                    (string)element.Attribute("key");

                if (string.IsNullOrWhiteSpace(key))
                    continue;

                try
                {
                    int schemaVersion =
                        ParseInt(element, "v", 1);

                    // Schema v3 belongs to the rejected physical-pixel
                    // experiment. Never reinterpret it as WPF logical units.
                    if (schemaVersion == 3)
                        continue;

                    var data = new PlacementData
                    {
                        Left = ParseDouble(element, "left"),
                        Top = ParseDouble(element, "top"),
                        Width = ParseDouble(element, "width"),
                        Height = ParseDouble(element, "height"),
                        WindowState =
                            Enum.TryParse(
                                (string)element.Attribute("state"),
                                out WindowState state)
                                ? state
                                : WindowState.Normal,
                        SchemaVersion = schemaVersion,
                        MonitorDeviceName =
                            (string)element.Attribute("monitor")
                    };

                    int monitorLeft =
                        ParseInt(element, "monitorLeft");
                    int monitorTop =
                        ParseInt(element, "monitorTop");
                    int monitorWidth =
                        ParseInt(element, "monitorWidth");
                    int monitorHeight =
                        ParseInt(element, "monitorHeight");

                    if (monitorWidth > 0 &&
                        monitorHeight > 0)
                    {
                        data.SavedMonitorWorkingArea =
                            new Rectangle(
                                monitorLeft,
                                monitorTop,
                                monitorWidth,
                                monitorHeight);
                    }

                    if (DateTime.TryParse(
                            (string)element.Attribute("savedAt"),
                            out DateTime savedAt))
                    {
                        data.SavedAt =
                            savedAt.ToUniversalTime();
                    }

                    if (data.IsUsable)
                        destination[key] = data;
                }
                catch
                {
                    // A malformed placement must never prevent startup.
                }
            }
        }

        private static XElement GeneratePlacementsNode(
            IReadOnlyDictionary<string, PlacementData> placements)
        {
            var root = new XElement("windowPlacements");

            foreach (KeyValuePair<string, PlacementData> entry
                in placements)
            {
                PlacementData data = entry.Value;

                var element = new XElement(
                    "window",
                    new XAttribute("v", SchemaVersion),
                    new XAttribute("key", entry.Key),
                    new XAttribute("left", data.Left),
                    new XAttribute("top", data.Top),
                    new XAttribute("width", data.Width),
                    new XAttribute("height", data.Height),
                    new XAttribute("state", data.WindowState));

                if (!string.IsNullOrWhiteSpace(
                        data.MonitorDeviceName))
                {
                    element.Add(
                        new XAttribute(
                            "monitor",
                            data.MonitorDeviceName));
                }

                if (!data.SavedMonitorWorkingArea.IsEmpty)
                {
                    Rectangle area =
                        data.SavedMonitorWorkingArea;

                    element.Add(
                        new XAttribute(
                            "monitorLeft",
                            area.Left));

                    element.Add(
                        new XAttribute(
                            "monitorTop",
                            area.Top));

                    element.Add(
                        new XAttribute(
                            "monitorWidth",
                            area.Width));

                    element.Add(
                        new XAttribute(
                            "monitorHeight",
                            area.Height));
                }

                if (data.SavedAt != default)
                {
                    element.Add(
                        new XAttribute(
                            "savedAt",
                            data.SavedAt.ToString("o")));
                }

                root.Add(element);
            }

            return root;
        }

        private static void PersistMachinePlacements()
        {
            try
            {
                WindowPlacementStorage.Save(
                    GeneratePlacementsNode(
                        MachinePlacements));
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.WriteLine(
                    "[WPS] Could not persist machine placement: " +
                    exception.Message);
            }
        }

        private static MonitorGeometry ToMonitorGeometry(
            Screen screen)
        {
            Rectangle area = screen.WorkingArea;

            return new MonitorGeometry(
                screen.DeviceName,
                new LogicalRect(
                    area.Left,
                    area.Top,
                    area.Width,
                    area.Height),
                screen.Primary);
        }

        private static LogicalRect ToLogicalRect(
            Rectangle rectangle)
        {
            return rectangle.IsEmpty
                ? default
                : new LogicalRect(
                    rectangle.Left,
                    rectangle.Top,
                    rectangle.Width,
                    rectangle.Height);
        }

        private static Rectangle ToDrawingRectangle(
            LogicalRect rectangle)
        {
            return new Rectangle(
                Convert.ToInt32(Math.Round(rectangle.Left)),
                Convert.ToInt32(Math.Round(rectangle.Top)),
                Math.Max(
                    1,
                    Convert.ToInt32(
                        Math.Round(rectangle.Width))),
                Math.Max(
                    1,
                    Convert.ToInt32(
                        Math.Round(rectangle.Height))));
        }

        private static double ResolveDimension(
            double configured,
            double actual)
        {
            if (double.IsFinite(configured) &&
                configured > 0)
            {
                return configured;
            }

            return double.IsFinite(actual) && actual > 0
                ? actual
                : 0;
        }

        private static bool IsUsable(Rect rectangle)
        {
            return !rectangle.IsEmpty &&
                   double.IsFinite(rectangle.Left) &&
                   double.IsFinite(rectangle.Top) &&
                   double.IsFinite(rectangle.Width) &&
                   double.IsFinite(rectangle.Height) &&
                   rectangle.Width > 0 &&
                   rectangle.Height > 0;
        }

        private static double ParseDouble(
            XElement element,
            string attribute,
            double fallback = 0)
        {
            string raw =
                (string)element.Attribute(attribute);

            return double.TryParse(
                raw,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double value)
                ? value
                : fallback;
        }

        private static int ParseInt(
            XElement element,
            string attribute,
            int fallback = 0)
        {
            string raw =
                (string)element.Attribute(attribute);

            return int.TryParse(raw, out int value)
                ? value
                : fallback;
        }

        private sealed class PlacementData
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public WindowState WindowState { get; set; }
            public int SchemaVersion { get; set; }
            public string MonitorDeviceName { get; set; }
            public Rectangle SavedMonitorWorkingArea { get; set; }
            public DateTime SavedAt { get; set; }

            public bool IsUsable =>
                double.IsFinite(Left) &&
                double.IsFinite(Top) &&
                double.IsFinite(Width) &&
                double.IsFinite(Height) &&
                Width > 0 &&
                Height > 0;

            public PlacementData Clone()
            {
                return new PlacementData
                {
                    Left = Left,
                    Top = Top,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState,
                    SchemaVersion = SchemaVersion,
                    MonitorDeviceName = MonitorDeviceName,
                    SavedMonitorWorkingArea =
                        SavedMonitorWorkingArea,
                    SavedAt = SavedAt
                };
            }
        }
    }
}
