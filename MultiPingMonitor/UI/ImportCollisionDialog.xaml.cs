using System.Windows;
using System.Windows.Input;
using MultiPingMonitor.Classes;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    /// <summary>
    /// Dialog shown when an imported compact set name collides with an existing one.
    /// Returns the user's choice via the <see cref="Choice"/> property.
    /// </summary>
    public partial class ImportCollisionDialog : Window
    {
        public CompactSetExportImport.CollisionChoice Choice { get; private set; }
            = CompactSetExportImport.CollisionChoice.CancelAll;

        public ImportCollisionDialog(string conflictingName)
        {
            InitializeComponent();
            Title = Strings.CompactSets_ImportCollisionTitle;
            MessageText.Text = string.Format(Strings.CompactSets_ImportCollisionMessage, conflictingName);
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            Choice = CompactSetExportImport.CollisionChoice.Replace;
            DialogResult = true;
        }

        private void ImportAsCopy_Click(object sender, RoutedEventArgs e)
        {
            Choice = CompactSetExportImport.CollisionChoice.ImportAsCopy;
            DialogResult = true;
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            Choice = CompactSetExportImport.CollisionChoice.Skip;
            DialogResult = true;
        }

        private void CancelImport_Click(object sender, RoutedEventArgs e)
        {
            Choice = CompactSetExportImport.CollisionChoice.CancelAll;
            DialogResult = false;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Choice = CompactSetExportImport.CollisionChoice.CancelAll;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Choice = CompactSetExportImport.CollisionChoice.CancelAll;
                Close();
            }
        }
    }
}
