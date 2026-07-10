using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MultiPingMonitor.Classes;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    public partial class ManageAliasesWindow : Window
    {
        public ManageAliasesWindow()
        {
            InitializeComponent();
            RefreshActionButtonLocalization();
            RefreshTitleBarChromeLocalization();
            WindowPlacementService.Attach(this, "ManageAliasesWindow");
            RefreshAliasList();
        }

        public void RefreshAliasList()
        {
            var aliases = Alias.GetAll()
                .OrderBy(alias => alias.Value, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Aliases.ItemsSource = aliases;
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedAlias();
            if (selected == null)
            {
                return;
            }

            var dialogWindow = new DialogWindow(
                icon: DialogWindow.DialogIcon.Warning,
                title: Strings.DialogTitle_ConfirmDelete,
                body: $"{Strings.ManageAliases_Warn_DeleteA} {selected.Value.Value} {Strings.ManageAliases_Warn_DeleteB}",
                confirmationText: Strings.DialogButton_Remove,
                isCancelButtonVisible: true)
            {
                Owner = this
            };

            if (dialogWindow.ShowDialog() == true)
            {
                Alias.Delete(selected.Value.Key);
                RefreshAliasList();
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedAlias();
            if (selected == null)
            {
                return;
            }

            var editWindow = new EditAliasWindow(selected.Value.Key, selected.Value.Value)
            {
                Owner = this
            };

            if (editWindow.ShowDialog() == true)
            {
                RefreshAliasList();
            }
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new NewAliasWindow
            {
                Owner = this
            };

            if (newWindow.ShowDialog() == true)
            {
                RefreshAliasList();
            }
        }

        private KeyValuePair<string, string>? GetSelectedAlias()
        {
            var selected = Aliases.SelectedItem as KeyValuePair<string, string>?;
            return selected.HasValue ? selected : null;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
        private void RefreshActionButtonLocalization()
        {
            AliasNewButton.Content = "_" + (
                MultiPingMonitor.Properties.Strings.ResourceManager.GetString(
                    "DialogButton_New") ?? "New");

            AliasEditButton.Content = "_" + (
                MultiPingMonitor.Properties.Strings.ResourceManager.GetString(
                    "DialogButton_Edit") ?? "Edit");

            AliasRemoveButton.Content = "_" + (
                MultiPingMonitor.Properties.Strings.ResourceManager.GetString(
                    "DialogButton_Remove") ?? "Remove");
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
