using System.Collections.Generic;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Classification of a log line for color-coding in the Live Ping Monitor.
    /// </summary>
    public enum LogEntryKind
    {
        /// <summary>Successful reply (UP).</summary>
        Success,
        /// <summary>Timeout, host unreachable, or DOWN.</summary>
        Failure,
        /// <summary>Warning — high latency, indeterminate, raw system error codes.</summary>
        Warning,
        /// <summary>Informational / system line (e.g. "*** Pinging …").</summary>
        Info
    }

    /// <summary>
    /// A single classified log line for the Live Ping Monitor rolling log.
    /// </summary>
    public sealed class LogEntry
    {
        public string Text { get; }
        public LogEntryKind Kind { get; }

        public LogEntry(string text, LogEntryKind kind)
        {
            Text = text;
            Kind = kind;
        }

        public override string ToString() => Text;

        /// <summary>
        /// Known IPStatus / WinSock numeric codes that .NET may emit as raw ToString() values.
        /// Maps numeric code string → human-readable description.
        /// Shared by LivePingMonitorWindow (log classification) and Probe-Icmp (FormatIpStatus).
        /// </summary>
        public static readonly Dictionary<string, string> IpStatusCodeMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "11001", "Buffer too small" },
            { "11002", "Destination net unreachable" },
            { "11003", "Destination host unreachable" },
            { "11004", "Destination protocol unreachable" },
            { "11005", "Destination port unreachable" },
            { "11006", "No resources" },
            { "11007", "Bad option" },
            { "11008", "Hardware error" },
            { "11009", "Packet too big" },
            { "11010", "Request timed out" },
            { "11011", "Bad route" },
            { "11012", "TTL expired in transit" },
            { "11013", "TTL expired reassembly" },
            { "11014", "Parameter problem" },
            { "11015", "Source quench" },
            { "11016", "Option too big" },
            { "11017", "Bad destination" },
            { "11018", "Destination unreachable" },
            { "11032", "Time exceeded" },
            { "11033", "Bad header" },
            { "11034", "Unrecognized next header" },
            { "11035", "ICMP error" },
            { "11036", "Destination scope mismatch" },
            { "11050", "General failure — network unavailable" },
        };
    }
}
