using System;
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
            RenameSetButton.IsEnabled = hasSet;
            DeleteSetButton.IsEnabled = hasSet;
            SetActiveButton.IsEnabled = hasSet;
            AddTargetButton.IsEnabled = hasSet;

            bool hasTarget = TargetsListBox.SelectedIndex >= 0;
            EditTargetButton.IsEnabled = hasTarget;
            RemoveTargetButton.IsEnabled = hasTarget;
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
        }

        private void SetActive_Click(object sender, RoutedEventArgs e)
        {
            var set = GetSelectedSet();
            if (set == null) return;

            ApplicationOptions.ActiveCompactSetId = set.Id;
            Configuration.Save();
            RefreshSetsList();
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
