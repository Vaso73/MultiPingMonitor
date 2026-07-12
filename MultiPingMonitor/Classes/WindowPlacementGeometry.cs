using System;
using System.Collections.Generic;

namespace MultiPingMonitor.Classes
{
    internal readonly struct LogicalRect
    {
        public LogicalRect(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public double Left { get; }
        public double Top { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => Left + Width;
        public double Bottom => Top + Height;

        public bool IsUsable =>
            double.IsFinite(Left) &&
            double.IsFinite(Top) &&
            double.IsFinite(Width) &&
            double.IsFinite(Height) &&
            Width > 0 &&
            Height > 0;
    }

    internal readonly struct MonitorGeometry
    {
        public MonitorGeometry(
            string deviceName,
            LogicalRect workingArea,
            bool isPrimary)
        {
            DeviceName = deviceName ?? string.Empty;
            WorkingArea = workingArea;
            IsPrimary = isPrimary;
        }

        public string DeviceName { get; }
        public LogicalRect WorkingArea { get; }
        public bool IsPrimary { get; }
    }

    internal static class WindowPlacementGeometry
    {
        internal const double MinimumWidth = 100;
        internal const double MinimumHeight = 60;
        internal const double EdgeTolerance = 24;

        internal static int SelectTargetMonitor(
            IReadOnlyList<MonitorGeometry> monitors,
            string savedDeviceName,
            LogicalRect savedRect)
        {
            ValidateMonitors(monitors);

            if (!string.IsNullOrWhiteSpace(savedDeviceName))
            {
                for (int i = 0; i < monitors.Count; i++)
                {
                    if (string.Equals(
                        monitors[i].DeviceName,
                        savedDeviceName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            int bestIndex = -1;
            double bestArea = 0;

            if (savedRect.IsUsable)
            {
                for (int i = 0; i < monitors.Count; i++)
                {
                    double area = IntersectionArea(
                        monitors[i].WorkingArea,
                        savedRect);

                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestIndex = i;
                    }
                }
            }

            return bestIndex >= 0
                ? bestIndex
                : SelectPrimaryMonitor(monitors);
        }

        internal static int SelectPrimaryMonitor(
            IReadOnlyList<MonitorGeometry> monitors)
        {
            ValidateMonitors(monitors);

            for (int i = 0; i < monitors.Count; i++)
            {
                if (monitors[i].IsPrimary)
                    return i;
            }

            return 0;
        }

        internal static LogicalRect RemapToWorkArea(
            LogicalRect savedRect,
            LogicalRect sourceWorkArea,
            LogicalRect targetWorkArea)
        {
            if (!targetWorkArea.IsUsable)
                throw new ArgumentException(
                    "Target work area is invalid.",
                    nameof(targetWorkArea));

            double width = double.IsFinite(savedRect.Width)
                ? Math.Max(savedRect.Width, MinimumWidth)
                : MinimumWidth;

            double height = double.IsFinite(savedRect.Height)
                ? Math.Max(savedRect.Height, MinimumHeight)
                : MinimumHeight;

            width = Math.Min(width, targetWorkArea.Width);
            height = Math.Min(height, targetWorkArea.Height);

            double left;
            double top;

            if (savedRect.IsUsable && sourceWorkArea.IsUsable)
            {
                left = MapAxis(
                    savedRect.Left,
                    savedRect.Width,
                    sourceWorkArea.Left,
                    sourceWorkArea.Width,
                    targetWorkArea.Left,
                    targetWorkArea.Width,
                    width);

                top = MapAxis(
                    savedRect.Top,
                    savedRect.Height,
                    sourceWorkArea.Top,
                    sourceWorkArea.Height,
                    targetWorkArea.Top,
                    targetWorkArea.Height,
                    height);
            }
            else
            {
                left = double.IsFinite(savedRect.Left)
                    ? savedRect.Left
                    : targetWorkArea.Left +
                      (targetWorkArea.Width - width) / 2.0;

                top = double.IsFinite(savedRect.Top)
                    ? savedRect.Top
                    : targetWorkArea.Top +
                      (targetWorkArea.Height - height) / 2.0;
            }

            left = Clamp(
                left,
                targetWorkArea.Left,
                targetWorkArea.Right - width);

            top = Clamp(
                top,
                targetWorkArea.Top,
                targetWorkArea.Bottom - height);

            return new LogicalRect(left, top, width, height);
        }

        private static double MapAxis(
            double savedStart,
            double savedSize,
            double sourceStart,
            double sourceSize,
            double targetStart,
            double targetSize,
            double targetWindowSize)
        {
            double sourceEnd = sourceStart + sourceSize;
            double savedEnd = savedStart + savedSize;
            double startGap = savedStart - sourceStart;
            double endGap = sourceEnd - savedEnd;

            bool anchoredStart =
                Math.Abs(startGap) <= EdgeTolerance;

            bool anchoredEnd =
                Math.Abs(endGap) <= EdgeTolerance;

            if (anchoredEnd &&
                (!anchoredStart || Math.Abs(endGap) <= Math.Abs(startGap)))
            {
                return targetStart +
                    targetSize -
                    targetWindowSize -
                    Math.Max(0, endGap);
            }

            if (anchoredStart)
                return targetStart + Math.Max(0, startGap);

            double sourceTravel = sourceSize - savedSize;
            double targetTravel = targetSize - targetWindowSize;

            if (sourceTravel <= 0 || targetTravel <= 0)
                return targetStart + targetTravel / 2.0;

            double ratio = Clamp(startGap / sourceTravel, 0, 1);
            return targetStart + ratio * targetTravel;
        }

        private static double IntersectionArea(
            LogicalRect first,
            LogicalRect second)
        {
            if (!first.IsUsable || !second.IsUsable)
                return 0;

            double width = Math.Max(
                0,
                Math.Min(first.Right, second.Right) -
                Math.Max(first.Left, second.Left));

            double height = Math.Max(
                0,
                Math.Min(first.Bottom, second.Bottom) -
                Math.Max(first.Top, second.Top));

            return width * height;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            if (maximum < minimum)
                return minimum;

            return Math.Min(
                Math.Max(value, minimum),
                maximum);
        }

        private static void ValidateMonitors(
            IReadOnlyList<MonitorGeometry> monitors)
        {
            if (monitors == null || monitors.Count == 0)
            {
                throw new ArgumentException(
                    "At least one monitor is required.",
                    nameof(monitors));
            }
        }
    }
}
