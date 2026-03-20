using System;
using System.Windows;

namespace PingMonitor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Initialize your application startup logic here
            Console.WriteLine("Application has started.");
            // Add any additional startup logic needed.
        }
    }
}