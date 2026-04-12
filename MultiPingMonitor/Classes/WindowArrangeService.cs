using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
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

            var wa = GetWorkingAreaInDips(activeWindow);

            // Use a uniform window size: the saved size of the active window or defaults,
            // clamped to not exceed the working area.
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
                double availW = wa.Width - winW;
                double availH = wa.Height - winH;
                if (availW > 0 && left + winW > wa.Right)
                    left = wa.Left + ((i * CascadeOffsetX) % (int)availW);
                if (availH > 0 && top + winH > wa.Bottom)
                    top = wa.Top + ((i * CascadeOffsetY) % (int)availH);

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

            var wa = GetWorkingAreaInDips(activeWindow);
            int count = windows.Count;

            // Compute grid dimensions: prefer more columns than rows for landscape monitors.
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            // Uniform cell size — all windows get the same dimensions.
            double cellW = Math.Floor(wa.Width / cols);
            double cellH = Math.Floor(wa.Height / rows);

            for (int i = 0; i < count; i++)
            {
                var w = windows[i];

                // Ensure Normal state before setting bounds.
                if (w.WindowState != WindowState.Normal)
                    w.WindowState = WindowState.Normal;

                int row = i / cols;
                int colInRow = i % cols;

                // Compute position.
                double left = wa.Left + colInRow * cellW;
                double top = wa.Top + row * cellH;

                // For the last row, center the windows if fewer than cols.
                int windowsInLastRow = count - (rows - 1) * cols;
                if (row == rows - 1 && windowsInLastRow < cols)
                {
                    double totalUsed = windowsInLastRow * cellW;
                    double offset = (wa.Width - totalUsed) / 2.0;
                    left = wa.Left + offset + colInRow * cellW;
                }

                // Clamp bounds to the working area.
                left = Clamp(left, wa.Left, wa.Right - cellW);
                top = Clamp(top, wa.Top, wa.Bottom - cellH);

                w.Left = left;
                w.Top = top;
                w.Width = cellW;
                w.Height = cellH;
            }

            activeWindow.Activate();
        }

        /// <summary>
        /// Get the working area of the monitor that contains the specified window,
        /// converted from physical pixels (<see cref="Screen.WorkingArea"/>) to
        /// WPF device-independent pixels (DIPs).
        /// </summary>
        private static Rect GetWorkingAreaInDips(Window window)
        {
            // Screen.WorkingArea returns physical pixels.
            var screen = Screen.FromRectangle(
                new System.Drawing.Rectangle(
                    (int)window.Left, (int)window.Top,
                    (int)window.Width, (int)window.Height));
            var wa = screen.WorkingArea;

            // Obtain the DPI transform to convert physical pixels → DIPs.
            Matrix transform = GetTransformFromDevice(window);
            var topLeft = transform.Transform(new Point(wa.Left, wa.Top));
            var size = transform.Transform(new Vector(wa.Width, wa.Height));

            return new Rect(topLeft.X, topLeft.Y, size.X, size.Y);
        }

        /// <summary>
        /// Return the device → DIP transformation matrix for the given window.
        /// Falls back to identity (1:1) if no presentation source is available.
        /// </summary>
        private static Matrix GetTransformFromDevice(Window window)
        {
            var source = PresentationSource.FromVisual(window)
                         ?? HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            if (source?.CompositionTarget != null)
                return source.CompositionTarget.TransformFromDevice;
            return Matrix.Identity;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
