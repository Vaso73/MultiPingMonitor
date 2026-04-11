using System.Windows;
using System.Windows.Input;

namespace MultiPingMonitor.UI
{
    /// <summary>
    /// Lightweight input dialog supporting one or two text fields.
    /// Used by ManageCompactSetsWindow for set name and target+alias input.
    /// </summary>
    public partial class CompactSetInputDialog : Window
    {
        public string Value1 => Field1.Text?.Trim() ?? string.Empty;
        public string Value2 => Field2.Text?.Trim() ?? string.Empty;

        /// <summary>
        /// Creates a single-field input dialog.
        /// </summary>
        public CompactSetInputDialog(string title, string prompt, string defaultValue)
        {
            InitializeComponent();
            Title = title;
            Prompt1Text.Text = prompt;
            Field1.Text = defaultValue ?? string.Empty;

            Loaded += (s, e) =>
            {
                Field1.Focus();
                Field1.SelectAll();
            };
        }

        /// <summary>
        /// Creates a two-field input dialog (e.g. target + alias).
        /// </summary>
        public CompactSetInputDialog(string title, string prompt1, string defaultValue1,
                                      string prompt2, string defaultValue2)
        {
            InitializeComponent();
            Title = title;
            Prompt1Text.Text = prompt1;
            Field1.Text = defaultValue1 ?? string.Empty;

            Prompt2Text.Text = prompt2;
            Prompt2Text.Visibility = Visibility.Visible;
            Field2.Text = defaultValue2 ?? string.Empty;
            Field2.Visibility = Visibility.Visible;

            Loaded += (s, e) =>
            {
                Field1.Focus();
                Field1.SelectAll();
            };
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
