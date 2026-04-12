using System;
using System.Collections.Generic;
using System.Linq;
using MultiPingMonitor.UI;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Central registry of open <see cref="LivePingMonitorWindow"/> instances.
    /// Thread-safe through locking.  Windows register on open and unregister on close.
    /// </summary>
    internal static class LiveWindowRegistry
    {
        private static readonly object _lock = new object();
        private static readonly HashSet<LivePingMonitorWindow> _windows = new HashSet<LivePingMonitorWindow>();

        /// <summary>Register a newly opened Live Ping Monitor window.</summary>
        internal static void Register(LivePingMonitorWindow window)
        {
            if (window == null) return;
            lock (_lock)
            {
                _windows.Add(window);
            }
        }

        /// <summary>Unregister a closed Live Ping Monitor window.</summary>
        internal static void Unregister(LivePingMonitorWindow window)
        {
            if (window == null) return;
            lock (_lock)
            {
                _windows.Remove(window);
            }
        }

        /// <summary>Return a snapshot of all currently registered (open) windows.</summary>
        internal static List<LivePingMonitorWindow> GetOpenWindows()
        {
            lock (_lock)
            {
                return _windows.ToList();
            }
        }

        /// <summary>Number of currently registered windows.</summary>
        internal static int Count
        {
            get { lock (_lock) { return _windows.Count; } }
        }
    }
}
