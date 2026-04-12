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
    }
}
