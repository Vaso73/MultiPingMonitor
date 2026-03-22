using System;

namespace PingMonitor.Classes
{
    public class ApplicationOptions
    {
        public string ApiUrl { get; set; }
        public int Timeout { get; set; }
        public bool LogToFile { get; set; }

        public ApplicationOptions(string apiUrl, int timeout, bool logToFile)
        {
            ApiUrl = apiUrl;
            Timeout = timeout;
            LogToFile = logToFile;
        }
    }
}