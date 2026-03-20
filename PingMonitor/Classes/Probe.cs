using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace PingMonitor.Classes
{
    public class Probe
    {
        private string _host;

        public Probe(string host)
        {
            _host = host;
        }

        public async Task<bool> PingAsync()
        {
            using (var ping = new Ping())
            {
                try
                {
                    var reply = await ping.SendPingAsync(_host);
                    return reply.Status == IPStatus.Success;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}