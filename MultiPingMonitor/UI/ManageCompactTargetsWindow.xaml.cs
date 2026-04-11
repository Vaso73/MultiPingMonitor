using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class ManageCompactTargetsWindow : Window
    {
        public List<string> Targets { get; private set; }

        public ManageCompactTargetsWindow(List<string> existingTargets = null)
        {
            InitializeComponent();
            WindowPlacementService.Attach(this, "ManageCompactTargetsWindow");

            // Set initial keyboard focus to text box.
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            // Pre-populate textbox with existing targets.
            if (existingTargets != null && existingTargets.Count > 0)
            {
                TargetsTextBox.Text = string.Join(Environment.NewLine, existingTargets);
                TargetsTextBox.SelectAll();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Parse targets from text: split on newlines and commas, trim, remove empty.
            Targets = TargetsTextBox.Text
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            DialogResult = true;
        }

        private void TargetsTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TargetsTextBox_Drop(object sender, DragEventArgs e)
        {
            const long MaxSizeInBytes = 10240;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            try
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths == null || paths.Length == 0)
                {
                    return;
                }

                if (paths.Length > 1)
                {
                    ShowError("Please drop only one file at a time.");
                    return;
                }

                var fileInfo = new FileInfo(paths[0]);
                if (fileInfo.Length > MaxSizeInBytes)
                {
                    ShowError($"\"{paths[0]}\" is too large. The maximum file size is {MaxSizeInBytes / 1024} KB.");
                    return;
                }

                var validLines = File.ReadLines(paths[0])
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line) &&
                        (char.IsLetterOrDigit(line[0]) || line[0] == '['));

                TargetsTextBox.Text = string.Join(Environment.NewLine, validLines);
            }
            catch (Exception ex)
            {
                ShowError($"File could not be opened: {ex.Message}");
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
    }
}
