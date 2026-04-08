using System.Windows;
using MultiPingMonitor.Classes;

namespace MultiPingMonitor.UI
{
    public partial class NewConfigurationWindow : Window
    {
        public NewConfigurationWindow()
        {
            InitializeComponent();

            FilePath.Text = Configuration.FilePath;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
