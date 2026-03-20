using System;
using System.Collections.Generic;

namespace PingMonitor.Classes
{
    public class Configuration
    {
        public string AppName { get; set; }
        public string Version { get; set; }
        public string Environment { get; set; }
        public Dictionary<string, string> Settings { get; set; }

        public Configuration()
        {
            Settings = new Dictionary<string, string>();
        }

        public void Load(string path)
        {
            // Logic to load configuration from a file at the specified path.
        }

        public void Save(string path)
        {
            // Logic to save configuration to a file at the specified path.
        }
    }
}