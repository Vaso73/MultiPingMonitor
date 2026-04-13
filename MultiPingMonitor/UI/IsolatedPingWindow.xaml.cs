using System;
using System.Windows;
using System.Windows.Controls;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class IsolatedPingWindow : Window
    {
        private int SelStart = 0;
        private int SelLength = 0;

        public IsolatedPingWindow(Probe pingItem)
        {
            InitializeComponent();
            WindowPlacementService.Attach(this, "IsolatedPingWindow");
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;
            pingItem.IsolatedWindow = this;
            DataContext = pingItem;

            // Initialize the always-on-top toggle to match the window's current state.
            AlwaysOnTopCheckBox.IsChecked = Topmost;

            // Set localized text.
            AlwaysOnTopText.Text = Properties.Strings.LivePing_AlwaysOnTop;
            CopyTargetText.Text  = Properties.Strings.LivePing_CopyTarget;

            // Apply initial pin icon color.
            UpdatePinIconState();

            Loaded += (_, _) => VisualStyleManager.ApplyNativeWindowCorners(this);
        }

        private void History_TextChanged(object sender, TextChangedEventArgs e)
        {
            History.SelectionStart = SelStart;
            History.SelectionLength = SelLength;
            if (!History.IsMouseCaptureWithin && History.SelectionLength == 0)
            {
                History.ScrollToEnd();
            }
        }

        private void History_SelectionChanged(object sender, RoutedEventArgs e)
        {
            SelStart = History.SelectionStart;
            SelLength = History.SelectionLength;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            (DataContext as Probe).IsolatedWindow = null;
            DataContext = null;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        // ── Header toolbar actions ────────────────────────────────────────

        private void AlwaysOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            Topmost = AlwaysOnTopCheckBox.IsChecked == true;
            UpdatePinIconState();
        }

        /// <summary>
        /// Updates the pin icon fill based on the always-on-top state.
        /// Active: pin icon uses Danger color. Inactive: secondary color.
        /// </summary>
        private void UpdatePinIconState()
        {
            if (AlwaysOnTopCheckBox.IsChecked == true)
            {
                PinIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Theme.Danger");
                AlwaysOnTopCheckBox.SetResourceReference(BorderBrushProperty, "Theme.Danger");
            }
            else
            {
                PinIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Theme.Text.Secondary");
                AlwaysOnTopCheckBox.SetResourceReference(BorderBrushProperty, "Theme.Border");
            }
        }

        private void CopyTargetButton_Click(object sender, RoutedEventArgs e)
        {
            string target = (DataContext as Probe)?.Hostname;
            if (!string.IsNullOrWhiteSpace(target))
            {
                try { Clipboard.SetText(target); } catch { }
            }
        }
    }
}
