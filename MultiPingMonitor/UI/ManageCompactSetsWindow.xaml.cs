using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MultiPingMonitor.Classes;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    public partial class ManageCompactSetsWindow : Window
    {
        public ManageCompactSetsWindow()
        {
            InitializeComponent();

            // If a saved placement exists, switch to Manual so that
            // WindowStartupLocation="CenterOwner" does not override the
            // restored position/size.  On first open (no saved placement)
            // the window will still center on its owner as usual.
            if (WindowPlacementService.HasPlacement("ManageCompactSetsWindow"))
                WindowStartupLocation = WindowStartupLocation.Manual;

            WindowPlacementService.Attach(this, "ManageCompactSetsWindow");
            RefreshSetsList();
        }

        // ── Sets list ─────────────────────────────────────────────────────────

        private void RefreshSetsList()
        {
            var previousSelection = SetsListBox.SelectedIndex;
            SetsListBox.Items.Clear();

            foreach (var set in ApplicationOptions.CompactSets)
            {
                string display = set.Id == ApplicationOptions.ActiveCompactSetId
                    ? $"{set.Name}  {Strings.CompactSets_Active}"
                    : set.Name;
                SetsListBox.Items.Add(display);
            }

            // Restore selection.
            if (previousSelection >= 0 && previousSelection < SetsListBox.Items.Count)
                SetsListBox.SelectedIndex = previousSelection;
            else if (SetsListBox.Items.Count > 0)
                SetsListBox.SelectedIndex = 0;

            UpdateButtonStates();
        }

        private CompactTargetSet GetSelectedSet()
        {
            int idx = SetsListBox.SelectedIndex;
            if (idx < 0 || idx >= ApplicationOptions.CompactSets.Count)
                return null;
            return ApplicationOptions.CompactSets[idx];
        }

        private void SetsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshTargetsList();
            UpdateButtonStates();
        }

        private void TargetsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSet = SetsListBox.SelectedIndex >= 0;
            int setIdx = SetsListBox.SelectedIndex;
            int setCount = ApplicationOptions.CompactSets.Count;

            RenameSetButton.IsEnabled = hasSet;
            DeleteSetButton.IsEnabled = hasSet;
            SetActiveButton.IsEnabled = hasSet;
            AddTargetButton.IsEnabled = hasSet;
            ExportSelectedButton.IsEnabled = hasSet;
            ExportAllButton.IsEnabled = setCount > 0;

            MoveSetUpButton.IsEnabled = hasSet && setIdx > 0;
            MoveSetDownButton.IsEnabled = hasSet && setIdx < setCount - 1;

            bool hasTarget = TargetsListBox.SelectedIndex >= 0;
            EditTargetButton.IsEnabled = hasTarget;
            RemoveTargetButton.IsEnabled = hasTarget;

            var set = GetSelectedSet();
            int targetIdx = TargetsListBox.SelectedIndex;
            int targetCount = set?.Entries.Count ?? 0;
            MoveTargetUpButton.IsEnabled = hasTarget && targetIdx > 0;
            MoveTargetDownButton.IsEnabled = hasTarget && targetIdx < targetCount - 1;
        }

        // ── Set operations ────────────────────────────────────────────────────

        private void NewSet_Click(object sender, RoutedEventArgs e)
        {
            string name = PromptForText(Strings.CompactSets_EnterName, Strings.CompactSets_DefaultName);
            if (name == null)
                return;

            var newSet = new CompactTargetSet(name);
            ApplicationOptions.CompactSets.Add(newSet);

            // If this is the first set, make it active automatically.
            if (ApplicationOptions.CompactSets.Count == 1)
                ApplicationOptions.ActiveCompactSetId = newSet.Id;

            Configuration.Save();
            RefreshSetsList();
            SetsListBox.SelectedIndex = ApplicationOptions.CompactSets.Count - 1;
        }

        private void RenameSet_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            string name = PromptForText(Strings.CompactSets_EnterName, set.Name);
            if (name == null) return;

            set.Name = name;
            Configuration.Save();
            RefreshSetsList();
        }

        private void DeleteSet_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            // Confirm deletion.
            var dialog = new DialogWindow(
                DialogWindow.DialogIcon.Warning,
                Strings.DialogTitle_ConfirmDelete,
                $"{Strings.CompactSets_ConfirmDelete}\n\n\"{set.Name}\"",
                Strings.DialogButton_Remove,
                true)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            bool wasActive = set.Id == ApplicationOptions.ActiveCompactSetId;
            ApplicationOptions.CompactSets.Remove(set);

            // If we deleted the active set, select the first remaining set (if any).
            if (wasActive)
            {
                ApplicationOptions.ActiveCompactSetId = ApplicationOptions.CompactSets.Count > 0
                    ? ApplicationOptions.CompactSets[0].Id
                    : string.Empty;
            }

            Configuration.Save();
            RefreshSetsList();

            if (wasActive)
                (Owner as MainWindow)?.RefreshActiveCompactSetData();
        }

        private void SetActive_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            // Delegate to MainWindow.SetActiveCompactSet – the exact same code path
            // used by the main menu and compact title bar menu active-set switching.
            (Owner as MainWindow)?.SetActiveCompactSet(set.Id);
            RefreshSetsList();
        }

        private void MoveSetUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = SetsListBox.SelectedIndex;
            if (idx <= 0) return;

            var sets = ApplicationOptions.CompactSets;
            var temp = sets[idx];
            sets[idx] = sets[idx - 1];
            sets[idx - 1] = temp;

            Configuration.Save();
            RefreshSetsList();
            SetsListBox.SelectedIndex = idx - 1;
        }

        private void MoveSetDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = SetsListBox.SelectedIndex;
            var sets = ApplicationOptions.CompactSets;
            if (idx < 0 || idx >= sets.Count - 1) return;

            var temp = sets[idx];
            sets[idx] = sets[idx + 1];
            sets[idx + 1] = temp;

            Configuration.Save();
            RefreshSetsList();
            SetsListBox.SelectedIndex = idx + 1;
        }

        // ── Import / Export ────────────────────────────────────────────────

        private void ExportSelected_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            using (var dlg = new System.Windows.Forms.SaveFileDialog())
            {
                dlg.Title = Strings.CompactSets_ExportTitle;
                dlg.RestoreDirectory = true;
                dlg.OverwritePrompt = true;
                dlg.AddExtension = true;
                dlg.AutoUpgradeEnabled = true;
                dlg.Filter = Strings.CompactSets_ExportFileFilter;
                dlg.FileName = SanitizeFileName(set.Name) + ".json";

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dlg.FileName))
                {
                    try
                    {
                        CompactSetExportImport.ExportToFile(dlg.FileName, new[] { set });
                    }
                    catch
                    {
                        ShowError(Strings.CompactSets_ExportError);
                    }
                }
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            if (ApplicationOptions.CompactSets.Count == 0) return;

            using (var dlg = new System.Windows.Forms.SaveFileDialog())
            {
                dlg.Title = Strings.CompactSets_ExportTitle;
                dlg.RestoreDirectory = true;
                dlg.OverwritePrompt = true;
                dlg.AddExtension = true;
                dlg.AutoUpgradeEnabled = true;
                dlg.Filter = Strings.CompactSets_ExportFileFilter;
                dlg.FileName = "compact-sets.json";

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dlg.FileName))
                {
                    try
                    {
                        CompactSetExportImport.ExportToFile(dlg.FileName, ApplicationOptions.CompactSets);
                    }
                    catch
                    {
                        ShowError(Strings.CompactSets_ExportError);
                    }
                }
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            string filePath;
            using (var dlg = new System.Windows.Forms.OpenFileDialog())
            {
                dlg.Title = Strings.CompactSets_ImportTitle;
                dlg.RestoreDirectory = true;
                dlg.Multiselect = false;
                dlg.Filter = Strings.CompactSets_ExportFileFilter;

                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrEmpty(dlg.FileName))
                    return;
                filePath = dlg.FileName;
            }

            var result = CompactSetExportImport.ReadFromFile(filePath);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                return;
            }

            int importedCount = 0;
            bool activeSetAffected = false;

            foreach (var importedSet in result.Sets)
            {
                var existing = CompactSetExportImport.FindByName(importedSet.Name);
                if (existing != null)
                {
                    // Show collision dialog.
                    var collisionDialog = new ImportCollisionDialog(importedSet.Name) { Owner = this };
                    collisionDialog.ShowDialog();

                    switch (collisionDialog.Choice)
                    {
                        case CompactSetExportImport.CollisionChoice.Replace:
                            if (IsActiveSet(existing))
                                activeSetAffected = true;
                            CompactSetExportImport.ReplaceSet(existing, importedSet);
                            importedCount++;
                            break;

                        case CompactSetExportImport.CollisionChoice.ImportAsCopy:
                            importedSet.Name = CompactSetExportImport.GenerateCopyName(importedSet.Name);
                            CompactSetExportImport.AddAsNew(importedSet);
                            importedCount++;
                            break;

                        case CompactSetExportImport.CollisionChoice.Skip:
                            // Leave existing data unchanged, continue to next set.
                            break;

                        case CompactSetExportImport.CollisionChoice.CancelAll:
                            // Stop entire import. Whatever was imported so far stays.
                            goto ImportDone;
                    }
                }
                else
                {
                    CompactSetExportImport.AddAsNew(importedSet);
                    importedCount++;
                }
            }

        ImportDone:
            if (importedCount > 0)
            {
                // If this is the first set(s) and no active set is selected, auto-activate the first.
                if (string.IsNullOrEmpty(ApplicationOptions.ActiveCompactSetId) && ApplicationOptions.CompactSets.Count > 0)
                    ApplicationOptions.ActiveCompactSetId = ApplicationOptions.CompactSets[0].Id;

                Configuration.Save();
                RefreshSetsList();

                // Live refresh if active set was replaced or compact mode is active.
                if (activeSetAffected)
                    (Owner as MainWindow)?.RefreshActiveCompactSetData();

                ShowInfo(string.Format(Strings.CompactSets_ImportSuccess, importedCount));
            }
            else if (importedCount == 0)
            {
                // User may have skipped everything – no changes needed.
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, System.StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private void ShowError(string message)
        {
            var dlg = DialogWindow.ErrorWindow(message);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void ShowInfo(string message)
        {
            var dlg = new DialogWindow(
                DialogWindow.DialogIcon.Info,
                Strings.CompactSets_ImportTitle,
                message,
                Strings.DialogButton_OK,
                false)
            { Owner = this };
            dlg.ShowDialog();
        }

        // ── Target list ───────────────────────────────────────────────────────

        private void RefreshTargetsList()
        {
            TargetsListBox.Items.Clear();
            var set = GetSelectedSet();
            if (set == null)
            {
                TargetsHeader.Text = Strings.CompactSets_Targets;
                return;
            }

            TargetsHeader.Text = $"{set.Name} – {Strings.CompactSets_Targets}";

            foreach (var entry in set.Entries)
            {
                string display = !string.IsNullOrWhiteSpace(entry.Alias)
                    ? $"{entry.Target}  →  {entry.Alias}"
                    : entry.Target;
                TargetsListBox.Items.Add(display);
            }

            UpdateButtonStates();
        }

        private bool IsActiveSet(CompactTargetSet set)
        {
            return set != null && set.Id == ApplicationOptions.ActiveCompactSetId;
        }

        // ── Target operations ─────────────────────────────────────────────────

        private void AddTarget_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            var result = PromptForTargetEntry(string.Empty, string.Empty);
            if (result == null) return;

            set.Entries.Add(result);
            Configuration.Save();
            RefreshTargetsList();

            if (IsActiveSet(set))
                (Owner as MainWindow)?.RefreshActiveCompactSetData();
        }

        private void EditTarget_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            int idx = TargetsListBox.SelectedIndex;
            if (idx < 0 || idx >= set.Entries.Count) return;

            var entry = set.Entries[idx];
            var result = PromptForTargetEntry(entry.Target, entry.Alias);
            if (result == null) return;

            set.Entries[idx] = result;
            Configuration.Save();
            RefreshTargetsList();
            TargetsListBox.SelectedIndex = idx;

            if (IsActiveSet(set))
                (Owner as MainWindow)?.RefreshActiveCompactSetData();
        }

        private void RemoveTarget_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            int idx = TargetsListBox.SelectedIndex;
            if (idx < 0 || idx >= set.Entries.Count) return;

            set.Entries.RemoveAt(idx);
            Configuration.Save();
            RefreshTargetsList();

            if (IsActiveSet(set))
                (Owner as MainWindow)?.RefreshActiveCompactSetData();
        }

        private void MoveTargetUp_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            int idx = TargetsListBox.SelectedIndex;
            if (idx <= 0) return;

            var temp = set.Entries[idx];
            set.Entries[idx] = set.Entries[idx - 1];
            set.Entries[idx - 1] = temp;

            Configuration.Save();
            RefreshTargetsList();
            TargetsListBox.SelectedIndex = idx - 1;

            if (IsActiveSet(set))
                (Owner as MainWindow)?.RefreshActiveCompactSetData();
        }

        private void MoveTargetDown_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            int idx = TargetsListBox.SelectedIndex;
            if (idx < 0 || idx >= set.Entries.Count - 1) return;

            var temp = set.Entries[idx];
            set.Entries[idx] = set.Entries[idx + 1];
            set.Entries[idx + 1] = temp;

            Configuration.Save();
            RefreshTargetsList();
            TargetsListBox.SelectedIndex = idx + 1;

            if (IsActiveSet(set))
                (Owner as MainWindow)?.RefreshActiveCompactSetData();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string PromptForText(string prompt, string defaultValue)
        {
            var dialog = new CompactSetInputDialog(
                Strings.CompactSets_Title,
                prompt,
                defaultValue ?? string.Empty);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var text = dialog.Value1;
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            return null;
        }

        private CompactTargetEntry PromptForTargetEntry(string currentTarget, string currentAlias)
        {
            var dialog = new CompactSetInputDialog(
                Strings.CompactSets_Title,
                Strings.CompactSets_EnterTarget,
                currentTarget ?? string.Empty,
                Strings.CompactSets_EnterAlias,
                currentAlias ?? string.Empty);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var target = dialog.Value1;
                if (string.IsNullOrWhiteSpace(target))
                    return null;
                return new CompactTargetEntry(target, dialog.Value2);
            }
            return null;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }
    }
}
