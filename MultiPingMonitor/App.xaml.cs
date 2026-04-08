using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace MultiPingMonitor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Force software rendering. Otherwise application may have high GPU usage on some video cards.
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            // Load only the language setting early, before any window is created.
            Classes.Configuration.LoadLanguageSetting();

            // Apply language setting before any window is created.
            ApplyLanguage(Classes.ApplicationOptions.Language);

            // Now create main window.
            var mainWindow = new UI.MainWindow();
            MainWindow = mainWindow;
            if (Classes.ApplicationOptions.StartInTray)
            {
                // Start directly in tray: initialize probes without ever showing the
                // main window, so there is zero visible flash or taskbar appearance.
                mainWindow.InitializeForStartInTray();
            }
            else
            {
                mainWindow.Show();
            }
        }

        private static void ApplyLanguage(Classes.ApplicationOptions.AppLanguage language)
        {
            CultureInfo culture;
            switch (language)
            {
                case Classes.ApplicationOptions.AppLanguage.English:
                    culture = new CultureInfo("en");
                    break;
                case Classes.ApplicationOptions.AppLanguage.Slovak:
                    culture = new CultureInfo("sk-SK");
                    break;
                default: // System
                    culture = CultureInfo.CurrentCulture;
                    break;
            }
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            MultiPingMonitor.Properties.Strings.Culture = culture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        }
    }
}
