// ApplicationOptions.cs

namespace PingMonitor.Classes
{
    public class ApplicationOptions
    {
        public int Timeout { get; set; } = 3000; // Default timeout in milliseconds
        public int Retries { get; set; } = 3; // Default number of ping retries
        public string TargetHost { get; set; }
        public bool UseDns { get; set; } = true; // Flag to use DNS resolution

        public ApplicationOptions(string targetHost)
        {
            TargetHost = targetHost;
        }
    }
}