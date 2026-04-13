using System.Windows;
using System.Windows.Media;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    public partial class DialogWindow : Window
    {
        public enum DialogIcon
        {
            Warning,
            Error,
            Info,
            Question,
            None
        }

        public DialogWindow(DialogIcon icon, string title, string body, string confirmationText, bool isCancelButtonVisible, string cancelText = null)
        {
            InitializeComponent();

            MessageHeader.Text = title;
            MessageBody.Text = body;
            OK.Content = confirmationText;
            Cancel.Visibility = isCancelButtonVisible ? Visibility.Visible : Visibility.Collapsed;
            if (cancelText != null)
                Cancel.Content = cancelText;
            SetIcon(icon);
        }

        private void SetIcon(DialogIcon icon)
        {
            if (icon == DialogIcon.None)
            {
                MessageImage.Visibility = Visibility.Collapsed;
                return;
            }

            string resourceKey = null;

            switch (icon)
            {
                case DialogIcon.Warning:
                    resourceKey = "icon.exclamation-triangle";
                    break;
                case DialogIcon.Error:
                    resourceKey = "icon.exclamation-circle";
                    break;
                case DialogIcon.Info:
                    resourceKey = "icon.info-circle";
                    break;
                case DialogIcon.Question:
                    resourceKey = "icon.question-circle";
                    break;
            }

            if (resourceKey != null)
            {
                MessageImage.Source = TryGetDrawingImage(resourceKey);
            }
        }

        private DrawingImage TryGetDrawingImage(string key)
        {
            return Application.Current.TryFindResource(key) as DrawingImage;
        }

        private static void PlayExclamationSound()
        {
            System.Media.SystemSounds.Exclamation.Play();
        }

        public static DialogWindow ErrorWindow(string message)
        {
            PlayExclamationSound();
            return new DialogWindow(
                icon: DialogIcon.Error,
                title: Strings.DialogTitle_Error,
                body: message,
                confirmationText: Strings.DialogButton_OK,
                isCancelButtonVisible: false);
        }

        public static DialogWindow WarningWindow(string message, string confirmButtonText)
        {
            PlayExclamationSound();
            return new DialogWindow(
                icon: DialogIcon.Warning,
                title: Strings.DialogTitle_Warning,
                body: message,
                confirmationText: confirmButtonText,
                isCancelButtonVisible: true);
        }

        public static DialogWindow InfoWindow(string message, Window owner = null)
        {
            var dialog = new DialogWindow(
                icon: DialogIcon.Info,
                title: Strings.DialogTitle_Information,
                body: message,
                confirmationText: Strings.DialogButton_OK,
                isCancelButtonVisible: false);
            if (owner != null)
                dialog.Owner = owner;
            return dialog;
        }

        public static DialogWindow ConfirmWindow(string message, Window owner = null)
        {
            var dialog = new DialogWindow(
                icon: DialogIcon.Question,
                title: Strings.DialogTitle_Confirm,
                body: message,
                confirmationText: Strings.DialogButton_Yes,
                isCancelButtonVisible: true,
                cancelText: Strings.DialogButton_No);
            if (owner != null)
                dialog.Owner = owner;
            return dialog;
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