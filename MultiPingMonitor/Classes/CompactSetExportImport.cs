using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Handles export and import of compact target sets using a versioned JSON format.
    /// </summary>
    public static class CompactSetExportImport
    {
        private const int CurrentFormatVersion = 1;

        // ── JSON DTOs ────────────────────────────────────────────────────────

        private sealed class ExportRoot
        {
            [JsonPropertyName("formatVersion")]
            public int FormatVersion { get; set; } = CurrentFormatVersion;

            [JsonPropertyName("compactSets")]
            public List<ExportSet> CompactSets { get; set; } = new List<ExportSet>();
        }

        private sealed class ExportSet
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("targets")]
            public List<ExportTarget> Targets { get; set; } = new List<ExportTarget>();
        }

        private sealed class ExportTarget
        {
            [JsonPropertyName("target")]
            public string Target { get; set; } = string.Empty;

            [JsonPropertyName("alias")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public string Alias { get; set; } = string.Empty;
        }

        // ── Export ───────────────────────────────────────────────────────────

        /// <summary>
        /// Exports the given compact sets to a JSON file.
        /// </summary>
        public static void ExportToFile(string filePath, IEnumerable<CompactTargetSet> sets)
        {
            var root = new ExportRoot();

            foreach (var set in sets)
            {
                var exportSet = new ExportSet { Name = set.Name };

                foreach (var entry in set.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Target))
                        continue;

                    exportSet.Targets.Add(new ExportTarget
                    {
                        Target = entry.Target,
                        Alias = entry.Alias ?? string.Empty
                    });
                }

                root.CompactSets.Add(exportSet);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            string json = JsonSerializer.Serialize(root, options);
            File.WriteAllText(filePath, json);
        }

        // ── Import ──────────────────────────────────────────────────────────

        /// <summary>
        /// Result of parsing and validating an import file.
        /// </summary>
        public sealed class ImportResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public List<CompactTargetSet> Sets { get; set; } = new List<CompactTargetSet>();
        }

        /// <summary>
        /// Reads and validates a JSON import file.
        /// Returns parsed compact sets or a user-facing error message.
        /// </summary>
        public static ImportResult ReadFromFile(string filePath)
        {
            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception)
            {
                return new ImportResult { ErrorMessage = Strings.CompactSets_ImportInvalidFile };
            }

            ExportRoot root;
            try
            {
                root = JsonSerializer.Deserialize<ExportRoot>(json);
            }
            catch (JsonException)
            {
                return new ImportResult { ErrorMessage = Strings.CompactSets_ImportInvalidFile };
            }

            if (root == null || root.CompactSets == null)
                return new ImportResult { ErrorMessage = Strings.CompactSets_ImportInvalidFile };

            if (root.FormatVersion < 1 || root.FormatVersion > CurrentFormatVersion)
                return new ImportResult
                {
                    ErrorMessage = string.Format(
                        Strings.CompactSets_ImportUnsupportedVersion,
                        root.FormatVersion)
                };

            var validSets = new List<CompactTargetSet>();
            foreach (var exportSet in root.CompactSets)
            {
                if (exportSet == null)
                    continue;
                if (string.IsNullOrWhiteSpace(exportSet.Name))
                    continue;

                var entries = new List<CompactTargetEntry>();
                if (exportSet.Targets != null)
                {
                    foreach (var t in exportSet.Targets)
                    {
                        if (t == null || string.IsNullOrWhiteSpace(t.Target))
                            continue;
                        entries.Add(new CompactTargetEntry(t.Target.Trim(), t.Alias?.Trim() ?? string.Empty));
                    }
                }

                // Accept sets even if they have no targets (user may want to populate later).
                validSets.Add(new CompactTargetSet(exportSet.Name.Trim(), entries));
            }

            if (validSets.Count == 0)
                return new ImportResult { ErrorMessage = Strings.CompactSets_ImportNoValidSets };

            return new ImportResult { Success = true, Sets = validSets };
        }

        // ── Collision helpers ────────────────────────────────────────────────

        public enum CollisionChoice
        {
            Replace,
            ImportAsCopy,
            Skip,
            CancelAll
        }

        /// <summary>
        /// Finds an existing compact set whose name matches (case-insensitive).
        /// </summary>
        public static CompactTargetSet FindByName(string name)
        {
            return ApplicationOptions.CompactSets
                .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Generates a non-conflicting copy name, e.g. "My Set (Copy)", "My Set (Copy 2)".
        /// </summary>
        public static string GenerateCopyName(string baseName)
        {
            string suffix = Strings.CompactSets_CopyNameSuffix; // "Copy" / "Kópia"
            string candidate = $"{baseName} ({suffix})";
            if (FindByName(candidate) == null)
                return candidate;

            for (int i = 2; i < 1000; i++)
            {
                candidate = $"{baseName} ({suffix} {i})";
                if (FindByName(candidate) == null)
                    return candidate;
            }

            // Fallback – extremely unlikely. Guid.ToString("N") always produces a 32-char hex string.
            return $"{baseName} ({suffix} {Guid.NewGuid().ToString("N").Substring(0, 6)})";
        }

        /// <summary>
        /// Applies a Replace operation: replaces the content and name of the existing set
        /// with the imported set's data, keeping the existing set Id stable.
        /// If the replaced set is the active set, runtime refresh is needed by the caller.
        /// </summary>
        public static void ReplaceSet(CompactTargetSet existing, CompactTargetSet imported)
        {
            existing.Name = imported.Name;
            existing.Entries = imported.Entries.Select(e =>
                new CompactTargetEntry(e.Target, e.Alias)).ToList();
        }

        /// <summary>
        /// Adds an imported set as a new entry with a fresh Id.
        /// </summary>
        public static void AddAsNew(CompactTargetSet imported)
        {
            var newSet = new CompactTargetSet(imported.Name,
                imported.Entries.Select(e => new CompactTargetEntry(e.Target, e.Alias)).ToList());
            ApplicationOptions.CompactSets.Add(newSet);
        }
    }
}
