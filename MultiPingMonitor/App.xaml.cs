using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            // ── Diagnostic CLI modes ──────────────────────────────────────────────
            // Handle --network-identity-lookup and --network-identity-diagnose BEFORE
            // creating any WPF window so they run cleanly as headless console commands.
            // stdout is written via Console.OpenStandardOutput() so output is available
            // when the caller uses -RedirectStandardOutput (e.g. Start-Process in PowerShell).
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var cliArg = args[1].ToLowerInvariant();

                if (cliArg == "--network-identity-lookup")
                {
                    // Run WAN lookup diagnostics; write compact JSON to stdout; exit.
                    System.Diagnostics.Debug.WriteLine(
                        "NetworkIdentityDiagnostics: --network-identity-lookup");
                    try
                    {
                        var json = Task.Run(
                            () => Classes.NetworkIdentityDiagnostics.RunLookupJsonAsync())
                            .GetAwaiter().GetResult();
                        WriteToStdout(json);
                    }
                    catch (Exception ex)
                    {
                        WriteToStdout("{\"error\":" + System.Text.Json.JsonSerializer.Serialize(
                            ex.GetType().Name + ": " + ex.Message) + "}");
                    }
                    Environment.Exit(0);
                    return;
                }

                if (cliArg == "--network-identity-diagnose")
                {
                    // Run in-process + child-process lookup; compare; write JSON; exit.
                    System.Diagnostics.Debug.WriteLine(
                        "NetworkIdentityDiagnostics: --network-identity-diagnose");
                    var exePath = args[0]; // args[0] is the full path of this exe
                    try
                    {
                        var json = Task.Run(
                            () => Classes.NetworkIdentityDiagnostics.RunDiagnoseJsonAsync(exePath))
                            .GetAwaiter().GetResult();
                        WriteToStdout(json);
                    }
                    catch (Exception ex)
                    {
                        WriteToStdout("{\"error\":" + System.Text.Json.JsonSerializer.Serialize(
                            ex.GetType().Name + ": " + ex.Message) + "}");
                    }
                    Environment.Exit(0);
                    return;
                }
            }
            // ─────────────────────────────────────────────────────────────────────

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

        /// <summary>
        /// Writes <paramref name="text"/> followed by a newline to the process's standard
        /// output stream.  Works even for a WinExe process when the caller has set up
        /// stdout redirection (e.g. <c>Start-Process -RedirectStandardOutput</c>), because
        /// <see cref="Console.OpenStandardOutput"/> opens the raw Win32 stdout handle rather
        /// than relying on the WPF-initialized <see cref="Console.Out"/> wrapper.
        /// </summary>
        private static void WriteToStdout(string text)
        {
            try
            {
                using var writer = new StreamWriter(
                    Console.OpenStandardOutput(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 4096,
                    leaveOpen: true)
                {
                    AutoFlush = true,
                };
                writer.WriteLine(text);
                writer.Flush();
            }
            catch { }
        }
    }
}
