using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using MultiPingMonitor.UI;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Arranges open <see cref="LivePingMonitorWindow"/> instances in Cascade or Tile layout
    /// on the working area of a target monitor.
    /// </summary>
    internal static class WindowArrangeService
    {
        private const int CascadeOffsetX = 26;
        private const int CascadeOffsetY = 26;
        private const int MinVisibleMargin = 80;

        /// <summary>
        /// Cascade all open Live Ping Monitor windows on the monitor that contains
        /// the specified <paramref name="activeWindow"/>.
        /// </summary>
        internal static void Cascade(LivePingMonitorWindow activeWindow)
        {
            var windows = LiveWindowRegistry.GetOpenWindows();
            if (windows.Count == 0) return;

            var wa = GetWorkingArea(activeWindow);

            // Use a uniform window size: the saved size of the active window or defaults.
            double winW = Math.Min(activeWindow.Width, wa.Width);
            double winH = Math.Min(activeWindow.Height, wa.Height);

            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];

                // Restore from maximized/minimized so that Left/Top/Width/Height take effect.
                if (w.WindowState != WindowState.Normal)
                    w.WindowState = WindowState.Normal;

                double left = wa.Left + (i * CascadeOffsetX);
                double top = wa.Top + (i * CascadeOffsetY);

                // Wrap around if the window would be too far off-screen.
                if (left + winW > wa.Right)
                    left = wa.Left + ((i * CascadeOffsetX) % Math.Max(1, wa.Width - winW));
                if (top + winH > wa.Bottom)
                    top = wa.Top + ((i * CascadeOffsetY) % Math.Max(1, wa.Height - winH));

                // Final clamp: ensure at least MinVisibleMargin pixels stay visible.
                left = Clamp(left, wa.Left, wa.Right - MinVisibleMargin);
                top = Clamp(top, wa.Top, wa.Bottom - MinVisibleMargin);

                w.Left = left;
                w.Top = top;
                w.Width = winW;
                w.Height = winH;
            }

            // Bring the active window on top last.
            activeWindow.Activate();
        }

        /// <summary>
        /// Tile all open Live Ping Monitor windows into a non-overlapping grid on
        /// the monitor that contains the specified <paramref name="activeWindow"/>.
        /// </summary>
        internal static void Tile(LivePingMonitorWindow activeWindow)
        {
            var windows = LiveWindowRegistry.GetOpenWindows();
            if (windows.Count == 0) return;

            var wa = GetWorkingArea(activeWindow);
            int count = windows.Count;

            // Compute grid dimensions: prefer more columns than rows for landscape monitors.
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            double cellW = wa.Width / cols;
            double cellH = wa.Height / rows;

            for (int i = 0; i < count; i++)
            {
                var w = windows[i];

                if (w.WindowState != WindowState.Normal)
                    w.WindowState = WindowState.Normal;

                int col = i % cols;
                int row = i / cols;

                // For the last row, if it has fewer windows, distribute evenly.
                int windowsInThisRow = (row < rows - 1) ? cols : (count - row * cols);
                double rowCellW = wa.Width / windowsInThisRow;
                int colInRow = i - row * cols;

                w.Left = wa.Left + colInRow * rowCellW;
                w.Top = wa.Top + row * cellH;
                w.Width = rowCellW;
                w.Height = cellH;
            }

            activeWindow.Activate();
        }

        /// <summary>
        /// Get the working area (in device-independent pixels) of the monitor
        /// that contains the specified window.
        /// </summary>
        private static Rect GetWorkingArea(Window window)
        {
            var screen = Screen.FromRectangle(
                new System.Drawing.Rectangle(
                    (int)window.Left, (int)window.Top,
                    (int)window.Width, (int)window.Height));
            var wa = screen.WorkingArea;
            return new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
