using System;
using System.Collections.Generic;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// A single entry in a compact target set: the raw probe target and an optional display alias.
    /// </summary>
    public class CompactTargetEntry
    {
        public string Target { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;

        public CompactTargetEntry() { }

        public CompactTargetEntry(string target, string alias = "")
        {
            Target = target ?? string.Empty;
            Alias = alias ?? string.Empty;
        }

        /// <summary>
        /// Returns the alias if non-empty, otherwise the raw target.
        /// </summary>
        public string DisplayName => !string.IsNullOrWhiteSpace(Alias) ? Alias : Target;
    }

    /// <summary>
    /// A named set of compact targets.  Multiple sets can exist; one is active at a time.
    /// </summary>
    public class CompactTargetSet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public List<CompactTargetEntry> Entries { get; set; } = new List<CompactTargetEntry>();

        public CompactTargetSet() { }

        public CompactTargetSet(string name)
        {
            Name = name ?? string.Empty;
        }

        public CompactTargetSet(string name, List<CompactTargetEntry> entries)
        {
            Name = name ?? string.Empty;
            Entries = entries ?? new List<CompactTargetEntry>();
        }
    }
}
