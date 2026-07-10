using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class MultiInputWindow : Window
    {
        public List<string> Addresses
        {
            get
            {
                // Split and trim multi-address text. Split occurs on both newlines and commas.
                // Return as a list with empty entries removed.
                return MultiAddress.Text
                    .Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
            }
        }

        public MultiInputWindow(List<string> addresses = null)
        {
            InitializeComponent();

            InstructionsText.Text =
                MultiPingMonitor.Properties.Strings.ResourceManager.GetString(
                    "MultiInput_Instructions")
                ?? "Type a list of addresses to ping.\n"
                    + "Enter one per line or comma separated.\n\n"
                    + "If you have a text file containing hosts,\n"
                    + "drag and drop it here.";
            RefreshTitleBarChromeLocalization();
            WindowPlacementService.Attach(this, "MultiInputWindow");

            // Set initial keyboard focus to text box.
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            // Pre-populate textbox if any addresses were supplied.
            if (addresses != null
                && addresses.Count > 0
                && !addresses.All(x => string.IsNullOrWhiteSpace(x))
                )
            {
                // Convert list to multiline string and select all text.
                MultiAddress.Text = string.Join(Environment.NewLine, addresses);
                MultiAddress.SelectAll();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void MultiAddress_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void MultiAddress_Drop(object sender, DragEventArgs e)
        {
            const long MaxSizeInBytes = 10240;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            try
            {
                // Get paths of dropped files.
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths == null || paths.Length == 0)
                {
                    return;
                }

                // Only 1 file drop is supported.
                if (paths.Length > 1)
                {
                    ShowError(
                        MultiPingMonitor.Properties.Strings.ResourceManager.GetString(
                            "Common_DropSingleFileOnly")
                        ?? "Please drop only one file at a time.");
                    return;
                }

                // Check filesize.
                var fileInfo = new FileInfo(paths[0]);
                if (fileInfo.Length > MaxSizeInBytes)
                {
                    ShowError(
                        string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            MultiPingMonitor.Properties.Strings.ResourceManager.GetString(
                                "Common_FileTooLargeWithPath")
                            ?? "\"{0}\" is too large. "
                                + "The maximum file size is {1} KB.",
                            paths[0],
                            MaxSizeInBytes / 1024));
                    return;
                }

                // Extract valid lines: valid lines are non-empty and begin with a letter, number, or the `[` character (for IPv6).
                var validLines = File.ReadLines(paths[0])
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line) &&
                        (char.IsLetterOrDigit(line[0]) || line[0] == '['));

                // Convert list to multiline string.
                MultiAddress.Text = string.Join(Environment.NewLine, validLines);
            }
            catch (Exception ex) 
            {
                ShowError(
                    string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        MultiPingMonitor.Properties.Strings.ResourceManager.GetString(
                            "Common_FileOpenErrorWithDetails")
                        ?? "File could not be opened: {0}",
                        ex.Message));
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShowError(string message)
        {
            var dialog = DialogWindow.ErrorWindow(message);
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void RefreshTitleBarChromeLocalization()
        {
            SetTitleBarButtonText(titleBarCloseButton, "Tooltip_Close", "Close");
        }

        private static string TitleBarResourceText(string key, string fallback)
        {
            return MultiPingMonitor.Properties.Strings.ResourceManager.GetString(key) ?? fallback;
        }

        private static void SetTitleBarButtonText(System.Windows.Controls.Button button, string key, string fallback)
        {
            string text = TitleBarResourceText(key, fallback);
            button.ToolTip = text;
            System.Windows.Automation.AutomationProperties.SetName(button, text);
        }


    }
}
