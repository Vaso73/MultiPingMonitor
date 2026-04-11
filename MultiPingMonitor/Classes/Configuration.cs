using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using MultiPingMonitor.Properties;
using MultiPingMonitor.UI;

namespace MultiPingMonitor.Classes
{
    internal static class Configuration
    {
        public static string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MultiPingMonitor.xml");

        static Configuration()
        {
            // Strict portable mode: configuration file always lives next to the executable.
            // No silent fallback to %LOCALAPPDATA% or any other system path.
            // Logs, exports, or backups may go to user-chosen paths, but the primary
            // config file is always co-located with the application.
        }

        public static bool Exists()
        {
            return File.Exists(FilePath);
        }

        public static bool IsReady()
        {
            if (Exists())
            {
                return true;
            }

            // No configuration found.
            // Prompt the user to create a new configuration file.
            var newConfigWindow = new NewConfigurationWindow();

            // Find the best owner for the new window.
            // Checks: Owned windows of MainWindow, plus owned windows of any child windows.
            var mainOwnedWindows = Application.Current.MainWindow.OwnedWindows;
            if (mainOwnedWindows.Count > 0)
            {
                newConfigWindow.Owner = mainOwnedWindows[0].OwnedWindows.Count > 0
                    ? mainOwnedWindows[0].OwnedWindows[0]
                    : mainOwnedWindows[0];
            }
            else
            {
                newConfigWindow.Owner = Application.Current.MainWindow;
            }

            // Display the new configuration window.
            // FilePath is updated if the user chooses portable mode.
            if (newConfigWindow.ShowDialog() == false)
            {
                // User cancelled.
                return false;
            }

            // Create the directory if it doesn't exist.
            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                }
                catch (Exception ex)
                {
                    Util.ShowError($"{Strings.Error_CreateDirectory} {ex.Message}");
                    return false;
                }
            }

            // Create a minimal XML configuration file.
            try
            {
                var xd = new XDocument(
                    new XElement("vmping",
                        new XElement("aliases"),
                        new XElement("favorites"),
                        new XElement("configuration"),
                        new XElement("colors")));
                xd.Save(FilePath);

                return true;
            }
            catch (Exception ex)
            {
                Util.ShowError($"{Strings.Error_CreateConfig} {ex.Message}");
                return false;
            }
        }

        public static string GetEscapedXpath(string xpath)
        {
            if (!xpath.Contains("'"))
            {
                return '\'' + xpath + '\'';
            }
            else if (!xpath.Contains("\""))
            {
                return '"' + xpath + '"';
            }
            else
            {
                return "concat('" + xpath.Replace("'", "',\"'\",'") + "')";
            }
        }

        // ── Config root name decision ──────────────────────────────────────────────
        //
        // The XML root element is kept as <vmping> deliberately for backward
        // compatibility.  MultiPingMonitor is a derivative of vmPing and existing
        // users may have config files with that root.
        //
        // Decision (feature/window-placement-v2): keep <vmping> as the only
        // supported root.  Rationale:
        //   1. Introducing a second accepted root (<MultiPingMonitor>) would add
        //      branch logic throughout Load() and Save() for zero user benefit.
        //   2. The root name is an implementation detail invisible to users.
        //   3. A future major version bump can rename the root in a single commit
        //      by adding a one-time migration step inside Load().
        //
        // See docs/ARCHITECTURE.md for the full architecture note.
        // ──────────────────────────────────────────────────────────────────────────

        public static void Save()
        {
            if (!IsReady())
            {
                return;
            }

            var tempPath = FilePath + ".tmp";
            var bakPath = FilePath + ".bak";

            try
            {
                // Open XML configuration file and get root <vmping> node.
                var xd = XDocument.Load(FilePath);
                XElement root = xd.Element("vmping")
                    ?? throw new XmlException("Invalid configuration file.");

                // Delete old nodes then recreate them with current config.
                root.Descendants("configuration").Remove();
                root.Descendants("colors").Remove();
                root.Descendants("windowPlacements").Remove();
                root.Descendants("compactTargets").Remove();
                root.Descendants("compactSets").Remove();
                root.Add(GenerateConfigurationNode());
                root.Add(GenerateColorsNode());
                root.Add(WindowPlacementService.GeneratePlacementsNode());
                root.Add(GenerateCompactTargetsNode());
                root.Add(GenerateCompactSetsNode());

                // Atomic save: write to a temp file first, then replace the real file.
                // This prevents a truncated/corrupted config if an error occurs mid-write.
                xd.Save(tempPath);

                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, bakPath, overwrite: true);
                }

                File.Move(tempPath, FilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                // Clean up the temporary file if it was created.
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                // If the real config is gone but a backup exists, restore it.
                if (!File.Exists(FilePath) && File.Exists(bakPath))
                {
                    try { File.Copy(bakPath, FilePath); } catch { }
                }

                Util.ShowError($"{Strings.Error_WriteConfig} {ex.Message}");
            }
        }

        private static XElement GenerateConfigurationNode()
        {
            // In XML, options are written as:
            // <configuration>
            //   <option name="MyOptionName">myValue</option>
            // </configuration>

            // Local function to create nodes.
            XElement Node(string name, object value) =>
                new XElement("option", new XAttribute("name", name), value ?? string.Empty);

            return new XElement("configuration",
                Node("PingInterval", ApplicationOptions.PingInterval),
                Node("PingTimeout", ApplicationOptions.PingTimeout),
                Node("TTL", ApplicationOptions.TTL),
                Node("DontFragment", ApplicationOptions.DontFragment),
                Node("UseCustomBuffer", ApplicationOptions.UseCustomBuffer),
                Node("Buffer", "base64:" + Convert.ToBase64String(ApplicationOptions.Buffer ?? Array.Empty<byte>())),
                Node("AlertThreshold", ApplicationOptions.AlertThreshold),
                new XComment(" LatencyDetectionMode: [Off, Auto, Fixed] "),
                Node("LatencyDetectionMode", ApplicationOptions.LatencyDetectionMode),
                Node("HighLatencyMilliseconds", ApplicationOptions.HighLatencyMilliseconds),
                Node("HighLatencyAlertTiggerCount", ApplicationOptions.HighLatencyAlertTiggerCount),
                new XComment(" InitialStartMode: [Blank, MultiInput, Favorite] "),
                Node("InitialStartMode", ApplicationOptions.InitialStartMode),
                Node("InitialProbeCount", ApplicationOptions.InitialProbeCount),
                Node("InitialColumnCount", ApplicationOptions.InitialColumnCount),
                Node("InitialFavorite", ApplicationOptions.InitialFavorite),
                new XComment(" PopupNotifications: [Always, Never, WhenMinimized] "),
                Node("PopupNotifications", ApplicationOptions.PopupOption),
                Node("IsAutoDismissEnabled", ApplicationOptions.IsAutoDismissEnabled),
                Node("AutoDismissMilliseconds", ApplicationOptions.AutoDismissMilliseconds),
                Node("IsEmailAlertEnabled", ApplicationOptions.IsEmailAlertEnabled),
                Node("EmailServer", ApplicationOptions.EmailServer),
                Node("EmailPort", ApplicationOptions.EmailPort),
                Node("IsEmailSslEnabled", ApplicationOptions.IsEmailSslEnabled),
                Node("IsEmailAuthenticationRequired", ApplicationOptions.IsEmailAuthenticationRequired),
                Node("EmailUser", string.IsNullOrWhiteSpace(ApplicationOptions.EmailUser)
                    ? string.Empty
                    : Util.EncryptStringAES(ApplicationOptions.EmailUser)),
                Node("EmailPassword", string.IsNullOrWhiteSpace(ApplicationOptions.EmailPassword)
                        ? string.Empty
                        : Util.EncryptStringAES(ApplicationOptions.EmailPassword)),
                Node("EmailRecipient", ApplicationOptions.EmailRecipient),
                Node("EmailFromAddress", ApplicationOptions.EmailFromAddress),
                Node("IsAudioUpAlertEnabled", ApplicationOptions.IsAudioUpAlertEnabled),
                Node("AudioUpFilePath", ApplicationOptions.AudioUpFilePath),
                Node("IsAudioDownAlertEnabled", ApplicationOptions.IsAudioDownAlertEnabled),
                Node("AudioDownFilePath", ApplicationOptions.AudioDownFilePath),
                Node("IsLogOutputEnabled", ApplicationOptions.IsLogOutputEnabled),
                Node("LogPath", ApplicationOptions.LogPath),
                Node("IsLogStatusChangesEnabled", ApplicationOptions.IsLogStatusChangesEnabled),
                Node("LogStatusChangesPath", ApplicationOptions.LogStatusChangesPath),
                Node("IsAlwaysOnTopEnabled", ApplicationOptions.IsAlwaysOnTopEnabled),
                Node("IsMinimizeToTrayEnabled", ApplicationOptions.IsMinimizeToTrayEnabled),
                Node("IsExitToTrayEnabled", ApplicationOptions.IsExitToTrayEnabled),
                Node("StartInTray", ApplicationOptions.StartInTray),
                Node("RememberWindowPosition", ApplicationOptions.RememberWindowPosition),
                Node("Theme", ApplicationOptions.Theme),
                Node("FontSize_Probe", ApplicationOptions.FontSize_Probe),
                Node("FontSize_Scanner", ApplicationOptions.FontSize_Scanner),
                new XComment(" Language: [System, English, Slovak] "),
                Node("Language", ApplicationOptions.Language),
                new XComment(" DisplayMode: [Normal, Compact] "),
                Node("DisplayMode", ApplicationOptions.CurrentDisplayMode),
                new XComment(" CompactSourceMode: [NormalTargets, CustomTargets] "),
                Node("CompactSourceMode", ApplicationOptions.CompactSource)
            );
        }

        private static XElement GenerateCompactTargetsNode()
        {
            // Legacy node kept for backward compatibility during migration period.
            var element = new XElement("compactTargets");
            foreach (var target in ApplicationOptions.CompactCustomTargets)
            {
                if (!string.IsNullOrWhiteSpace(target))
                    element.Add(new XElement("host", target));
            }
            return element;
        }

        private static XElement GenerateCompactSetsNode()
        {
            var element = new XElement("compactSets",
                new XAttribute("activeSetId", ApplicationOptions.ActiveCompactSetId ?? string.Empty));

            foreach (var set in ApplicationOptions.CompactSets)
            {
                var setElement = new XElement("set",
                    new XAttribute("id", set.Id),
                    new XAttribute("name", set.Name ?? string.Empty));

                foreach (var entry in set.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Target))
                        continue;
                    var entryElement = new XElement("entry",
                        new XAttribute("target", entry.Target));
                    if (!string.IsNullOrWhiteSpace(entry.Alias))
                        entryElement.SetAttributeValue("alias", entry.Alias);
                    setElement.Add(entryElement);
                }

                element.Add(setElement);
            }

            return element;
        }

        private static XElement GenerateColorsNode()
        {
            // In XML, options are written as:
            // <colors>
            //   <option name="MyOptionName">myValue</option>
            // </colors>

            // Local function to create nodes.
            XElement Node(string name, object value) =>
                new XElement("option", new XAttribute("name", name), value ?? string.Empty);

            return new XElement("colors",
                new XComment(" Probe background "),
                Node("Probe.Background.Inactive", ApplicationOptions.BackgroundColor_Probe_Inactive),
                Node("Probe.Background.Up", ApplicationOptions.BackgroundColor_Probe_Up),
                Node("Probe.Background.Down", ApplicationOptions.BackgroundColor_Probe_Down),
                Node("Probe.Background.Indeterminate", ApplicationOptions.BackgroundColor_Probe_Indeterminate),
                Node("Probe.Background.Error", ApplicationOptions.BackgroundColor_Probe_Error),

                new XComment(" Probe foreground "),
                Node("Probe.Foreground.Inactive", ApplicationOptions.ForegroundColor_Probe_Inactive),
                Node("Probe.Foreground.Up", ApplicationOptions.ForegroundColor_Probe_Up),
                Node("Probe.Foreground.Down", ApplicationOptions.ForegroundColor_Probe_Down),
                Node("Probe.Foreground.Indeterminate", ApplicationOptions.ForegroundColor_Probe_Indeterminate),
                Node("Probe.Foreground.Error", ApplicationOptions.ForegroundColor_Probe_Error),

                new XComment(" Statistics foreground "),
                Node("Statistics.Foreground.Inactive", ApplicationOptions.ForegroundColor_Stats_Inactive),
                Node("Statistics.Foreground.Up", ApplicationOptions.ForegroundColor_Stats_Up),
                Node("Statistics.Foreground.Down", ApplicationOptions.ForegroundColor_Stats_Down),
                Node("Statistics.Foreground.Indeterminate", ApplicationOptions.ForegroundColor_Stats_Indeterminate),
                Node("Statistics.Foreground.Error", ApplicationOptions.ForegroundColor_Stats_Error),

                new XComment(" Alias foreground "),
                Node("Alias.Foreground.Inactive", ApplicationOptions.ForegroundColor_Alias_Inactive),
                Node("Alias.Foreground.Up", ApplicationOptions.ForegroundColor_Alias_Up),
                Node("Alias.Foreground.Down", ApplicationOptions.ForegroundColor_Alias_Down),
                Node("Alias.Foreground.Indeterminate", ApplicationOptions.ForegroundColor_Alias_Indeterminate),
                Node("Alias.Foreground.Error", ApplicationOptions.ForegroundColor_Alias_Error),

                new XComment(" Scanner "),
                Node("Probe.Background.Scanner", ApplicationOptions.BackgroundColor_Probe_Scanner),
                Node("Probe.Foreground.Scanner", ApplicationOptions.ForegroundColor_Probe_Scanner),
                Node("Alias.Foreground.Scanner", ApplicationOptions.ForegroundColor_Alias_Scanner)
            );
        }

        public static void Load()
        {
            if (!Exists())
            {
                return;
            }

            try
            {
                var xd = new XmlDocument();
                xd.Load(FilePath);

                LoadAppOptions(xd.SelectNodes("/vmping/configuration/option"));
                LoadColors(xd.SelectNodes("/vmping/colors/option"));
                ApplicationOptions.UpdatePingOptions();

                // Load window placements using XDocument (LINQ to XML).
                try
                {
                    var xdoc = XDocument.Load(FilePath);
                    var placementsNode = xdoc.Root?.Element("windowPlacements");
                    WindowPlacementService.LoadPlacements(placementsNode);
                }
                catch
                {
                    // Ignore placement load errors; windows will use default positions.
                }

                // Load compact custom targets (legacy).
                try
                {
                    var targets = new System.Collections.Generic.List<string>();
                    var hostNodes = xd.SelectNodes("/vmping/compactTargets/host");
                    if (hostNodes != null)
                    {
                        foreach (XmlNode node in hostNodes)
                        {
                            var host = node.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(host))
                                targets.Add(host);
                        }
                    }
                    ApplicationOptions.CompactCustomTargets = targets;
                }
                catch
                {
                    // Ignore compact target load errors; use empty list.
                }

                // Load compact sets.
                LoadCompactSets(xd);

                // Migration: if old compact custom targets exist and no compact sets exist yet,
                // create a default compact set from the old entries.
                MigrateCompactTargetsToSets();
            }

            catch (Exception ex)
            {
                // Config is corrupted. Rename it to .corrupt and recreate a clean one.
                var corruptPath = FilePath + ".corrupt";
                try
                {
                    File.Copy(FilePath, corruptPath, overwrite: true);
                    File.Delete(FilePath);

                    var xd = new XDocument(
                        new XElement("vmping",
                            new XElement("aliases"),
                            new XElement("favorites"),
                            new XElement("configuration"),
                            new XElement("colors")));
                    xd.Save(FilePath);

                    Util.ShowError(
                        $"{Strings.Error_LoadConfig} {ex.Message}\n\n" +
                        $"The corrupted configuration has been saved as:\n{corruptPath}\n\n" +
                        "A clean configuration file has been created automatically.");
                }
                catch
                {
                    Util.ShowError($"{Strings.Error_LoadConfig} {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Loads compact sets from the XML configuration.
        /// </summary>
        private static void LoadCompactSets(XmlDocument xd)
        {
            try
            {
                var sets = new List<CompactTargetSet>();
                var setsNode = xd.SelectSingleNode("/vmping/compactSets");
                if (setsNode != null)
                {
                    ApplicationOptions.ActiveCompactSetId = setsNode.Attributes?["activeSetId"]?.Value ?? string.Empty;

                    foreach (XmlNode setNode in setsNode.SelectNodes("set"))
                    {
                        var id = setNode.Attributes?["id"]?.Value;
                        var name = setNode.Attributes?["name"]?.Value ?? string.Empty;
                        if (string.IsNullOrEmpty(id))
                            continue;

                        var set = new CompactTargetSet(name) { Id = id };

                        foreach (XmlNode entryNode in setNode.SelectNodes("entry"))
                        {
                            var target = entryNode.Attributes?["target"]?.Value?.Trim();
                            if (string.IsNullOrEmpty(target))
                                continue;
                            var alias = entryNode.Attributes?["alias"]?.Value ?? string.Empty;
                            set.Entries.Add(new CompactTargetEntry(target, alias));
                        }

                        sets.Add(set);
                    }
                }
                ApplicationOptions.CompactSets = sets;
            }
            catch
            {
                // Ignore compact set load errors; use empty list.
            }
        }

        /// <summary>
        /// Migration: if old compact custom targets exist and no compact sets exist yet,
        /// create one default compact set from the old entries and set it as active.
        /// Safe and idempotent – only runs when CompactSets is empty and CompactCustomTargets is not.
        /// </summary>
        private static void MigrateCompactTargetsToSets()
        {
            if (ApplicationOptions.CompactSets.Count > 0)
                return;
            if (ApplicationOptions.CompactCustomTargets.Count == 0)
                return;

            var entries = new List<CompactTargetEntry>();
            foreach (var target in ApplicationOptions.CompactCustomTargets)
            {
                if (!string.IsNullOrWhiteSpace(target))
                    entries.Add(new CompactTargetEntry(target.Trim()));
            }

            if (entries.Count == 0)
                return;

            var defaultSet = new CompactTargetSet(Strings.CompactSets_MigratedDefaultName, entries);
            ApplicationOptions.CompactSets.Add(defaultSet);
            ApplicationOptions.ActiveCompactSetId = defaultSet.Id;
        }

        private static void LoadColors(XmlNodeList nodes)
        {
            if (nodes == null)
            {
                return;
            }

            var options = nodes.Cast<XmlNode>()
                .Where(n => n.Attributes?["name"] != null)
                .ToDictionary(n => n.Attributes["name"].Value, n => n.InnerText);

            // Load probe backgorund colors.
            ApplyColor("Probe.Background.Inactive", v => ApplicationOptions.BackgroundColor_Probe_Inactive = v, options);
            ApplyColor("Probe.Background.Up", v => ApplicationOptions.BackgroundColor_Probe_Up = v, options);
            ApplyColor("Probe.Background.Down", v => ApplicationOptions.BackgroundColor_Probe_Down = v, options);
            ApplyColor("Probe.Background.Indeterminate", v => ApplicationOptions.BackgroundColor_Probe_Indeterminate = v, options);
            ApplyColor("Probe.Background.Error", v => ApplicationOptions.BackgroundColor_Probe_Error = v, options);
            ApplyColor("Probe.Foreground.Inactive", v => ApplicationOptions.ForegroundColor_Probe_Inactive = v, options);
            ApplyColor("Probe.Foreground.Up", v => ApplicationOptions.ForegroundColor_Probe_Up = v, options);
            ApplyColor("Probe.Foreground.Down", v => ApplicationOptions.ForegroundColor_Probe_Down = v, options);
            ApplyColor("Probe.Foreground.Indeterminate", v => ApplicationOptions.ForegroundColor_Probe_Indeterminate = v, options);
            ApplyColor("Probe.Foreground.Error", v => ApplicationOptions.ForegroundColor_Probe_Error = v, options);
            ApplyColor("Statistics.Foreground.Inactive", v => ApplicationOptions.ForegroundColor_Stats_Inactive = v, options);
            ApplyColor("Statistics.Foreground.Up", v => ApplicationOptions.ForegroundColor_Stats_Up = v, options);
            ApplyColor("Statistics.Foreground.Down", v => ApplicationOptions.ForegroundColor_Stats_Down = v, options);
            ApplyColor("Statistics.Foreground.Indeterminate", v => ApplicationOptions.ForegroundColor_Stats_Indeterminate = v, options);
            ApplyColor("Statistics.Foreground.Error", v => ApplicationOptions.ForegroundColor_Stats_Error = v, options);
            ApplyColor("Alias.Foreground.Inactive", v => ApplicationOptions.ForegroundColor_Alias_Inactive = v, options);
            ApplyColor("Alias.Foreground.Up", v => ApplicationOptions.ForegroundColor_Alias_Up = v, options);
            ApplyColor("Alias.Foreground.Down", v => ApplicationOptions.ForegroundColor_Alias_Down = v, options);
            ApplyColor("Alias.Foreground.Indeterminate", v => ApplicationOptions.ForegroundColor_Alias_Indeterminate = v, options);
            ApplyColor("Alias.Foreground.Error", v => ApplicationOptions.ForegroundColor_Alias_Error = v, options);
            ApplyColor("Probe.Background.Scanner", v => ApplicationOptions.BackgroundColor_Probe_Scanner = v, options);
            ApplyColor("Probe.Foreground.Scanner", v => ApplicationOptions.ForegroundColor_Probe_Scanner = v, options);
            ApplyColor("Alias.Foreground.Scanner", v => ApplicationOptions.ForegroundColor_Alias_Scanner = v, options);
        }

        private static void ApplyColor(string key, Action<string> setter, IDictionary<string, string> options)
        {
            if (options.TryGetValue(key, out var value) && Util.IsValidHtmlColor(value))
                setter(value);
        }

        private static void LoadAppOptions(XmlNodeList nodes)
        {
            if (nodes == null)
            {
                return;
            }

            var options = nodes.Cast<XmlNode>()
                .Where(n => n.Attributes?["name"] != null)
                .ToDictionary(n => n.Attributes["name"].Value, n => n.InnerText);

            if (options.TryGetValue("PingInterval", out string optionValue))
            {
                ApplicationOptions.PingInterval = int.Parse(optionValue);
            }
            if (options.TryGetValue("PingTimeout", out optionValue))
            {
                ApplicationOptions.PingTimeout = int.Parse(optionValue);
            }
            if (options.TryGetValue("TTL", out optionValue))
            {
                ApplicationOptions.TTL = int.Parse(optionValue);
            }
            if (options.TryGetValue("DontFragment", out optionValue))
            {
                ApplicationOptions.DontFragment = bool.Parse(optionValue);
            }
            if (options.TryGetValue("UseCustomBuffer", out optionValue))
            {
                ApplicationOptions.UseCustomBuffer = bool.Parse(optionValue);
            }
            if (options.TryGetValue("Buffer", out optionValue))
            {
                // New format uses "base64:" prefix to avoid NUL chars in XML.
                // Old format stored the buffer as a raw ASCII string (backward compat).
                if (optionValue.StartsWith("base64:", StringComparison.Ordinal))
                {
                    ApplicationOptions.Buffer = Convert.FromBase64String(optionValue.Substring(7));
                }
                else
                {
                    ApplicationOptions.Buffer = Encoding.ASCII.GetBytes(optionValue);
                }
            }
            if (options.TryGetValue("AlertThreshold", out optionValue))
            {
                ApplicationOptions.AlertThreshold = int.Parse(optionValue);
            }
            if (options.TryGetValue("LatencyDetectionMode", out optionValue))
            {
                if (optionValue.Equals(ApplicationOptions.LatencyMode.Auto.ToString()))
                    ApplicationOptions.LatencyDetectionMode = ApplicationOptions.LatencyMode.Auto;
                else if (optionValue.Equals(ApplicationOptions.LatencyMode.Fixed.ToString()))
                    ApplicationOptions.LatencyDetectionMode = ApplicationOptions.LatencyMode.Fixed;
                else
                    ApplicationOptions.LatencyDetectionMode = ApplicationOptions.LatencyMode.Off;
            }
            if (options.TryGetValue("HighLatencyMilliseconds", out optionValue))
            {
                ApplicationOptions.HighLatencyMilliseconds = long.Parse(optionValue);
            }
            if (options.TryGetValue("HighLatencyAlertTiggerCount", out optionValue))
            {
                ApplicationOptions.HighLatencyAlertTiggerCount = int.Parse(optionValue);
            }
            if (options.TryGetValue("InitialStartMode", out optionValue))
            {
                if (optionValue.Equals(ApplicationOptions.StartMode.Favorite.ToString()))
                {
                    ApplicationOptions.InitialStartMode = ApplicationOptions.StartMode.Favorite;
                }
                else if (optionValue.Equals(ApplicationOptions.StartMode.MultiInput.ToString()))
                {
                    ApplicationOptions.InitialStartMode = ApplicationOptions.StartMode.MultiInput;
                }
                else
                {
                    ApplicationOptions.InitialStartMode = ApplicationOptions.StartMode.Blank;
                }
            }
            if (options.TryGetValue("InitialProbeCount", out optionValue))
            {
                ApplicationOptions.InitialProbeCount = int.Parse(optionValue);
            }
            if (options.TryGetValue("InitialColumnCount", out optionValue))
            {
                ApplicationOptions.InitialColumnCount = int.Parse(optionValue);
            }
            if (options.TryGetValue("InitialFavorite", out optionValue))
            {
                ApplicationOptions.InitialFavorite = optionValue;
            }
            if (options.TryGetValue("PopupNotifications", out optionValue))
            {
                if (optionValue.Equals(ApplicationOptions.PopupNotificationOption.Always.ToString()))
                {
                    ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Always;
                }
                else if (optionValue.Equals(ApplicationOptions.PopupNotificationOption.WhenMinimized.ToString()))
                {
                    ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.WhenMinimized;
                }
                else
                {
                    ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Never;
                }
            }
            if (options.TryGetValue("IsAutoDismissEnabled", out optionValue))
            {
                ApplicationOptions.IsAutoDismissEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("AutoDismissMilliseconds", out optionValue))
            {
                ApplicationOptions.AutoDismissMilliseconds = int.Parse(optionValue);
            }
            if (options.TryGetValue("IsEmailAlertEnabled", out optionValue))
            {
                ApplicationOptions.IsEmailAlertEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("IsEmailAuthenticationRequired", out optionValue))
            {
                ApplicationOptions.IsEmailAuthenticationRequired = bool.Parse(optionValue);
            }
            if (options.TryGetValue("EmailServer", out optionValue))
            {
                ApplicationOptions.EmailServer = optionValue;
            }
            if (options.TryGetValue("EmailPort", out optionValue))
            {
                ApplicationOptions.EmailPort = optionValue;
            }
            if (options.TryGetValue("IsEmailSslEnabled", out optionValue))
            {
                ApplicationOptions.IsEmailSslEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("EmailRecipient", out optionValue))
            {
                ApplicationOptions.EmailRecipient = optionValue;
            }
            if (options.TryGetValue("EmailFromAddress", out optionValue))
            {
                ApplicationOptions.EmailFromAddress = optionValue;
            }
            if (options.TryGetValue("IsAudioAlertEnabled", out optionValue))
            {
                // For compatibility with version 1.3.4 and lower.
                ApplicationOptions.IsAudioDownAlertEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("AudioFilePath", out optionValue))
            {
                // For compatibility with version 1.3.4 and lower.
                ApplicationOptions.AudioDownFilePath = optionValue;
            }
            if (options.TryGetValue("IsAudioUpAlertEnabled", out optionValue))
            {
                ApplicationOptions.IsAudioUpAlertEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("IsAudioDownAlertEnabled", out optionValue))
            {
                ApplicationOptions.IsAudioDownAlertEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("AudioUpFilePath", out optionValue))
            {
                ApplicationOptions.AudioUpFilePath = optionValue;
            }
            if (options.TryGetValue("AudioDownFilePath", out optionValue))
            {
                ApplicationOptions.AudioDownFilePath = optionValue;
            }
            if (options.TryGetValue("IsLogOutputEnabled", out optionValue))
            {
                ApplicationOptions.IsLogOutputEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("LogPath", out optionValue))
            {
                ApplicationOptions.LogPath = optionValue;
            }
            if (options.TryGetValue("IsLogStatusChangesEnabled", out optionValue))
            {
                ApplicationOptions.IsLogStatusChangesEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("LogStatusChangesPath", out optionValue))
            {
                ApplicationOptions.LogStatusChangesPath = optionValue;
            }
            if (options.TryGetValue("EmailUser", out optionValue))
            {
                if (optionValue.Length > 0)
                {
                    ApplicationOptions.EmailUser = Util.DecryptStringAES(optionValue);
                }
            }
            if (options.TryGetValue("EmailPassword", out optionValue))
            {
                if (optionValue.Length > 0)
                {
                    ApplicationOptions.EmailPassword = Util.DecryptStringAES(optionValue);
                }
            }
            if (options.TryGetValue("IsAlwaysOnTopEnabled", out optionValue))
            {
                ApplicationOptions.IsAlwaysOnTopEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("IsMinimizeToTrayEnabled", out optionValue))
            {
                ApplicationOptions.IsMinimizeToTrayEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("IsExitToTrayEnabled", out optionValue))
            {
                ApplicationOptions.IsExitToTrayEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("StartInTray", out optionValue))
            {
                ApplicationOptions.StartInTray = bool.Parse(optionValue);
            }
            if (options.TryGetValue("RememberWindowPosition", out optionValue))
            {
                ApplicationOptions.RememberWindowPosition = bool.Parse(optionValue);
            }
            if (options.TryGetValue("Theme", out optionValue))
            {
                ApplicationOptions.Theme = optionValue;
            }
            if (options.TryGetValue("FontSize_Probe", out optionValue))
            {
                ApplicationOptions.FontSize_Probe = int.Parse(optionValue);
            }
            if (options.TryGetValue("FontSize_Scanner", out optionValue))
            {
                ApplicationOptions.FontSize_Scanner = int.Parse(optionValue);
            }
            if (options.TryGetValue("Language", out optionValue))
            {
                if (Enum.TryParse<ApplicationOptions.AppLanguage>(optionValue, out var lang))
                    ApplicationOptions.Language = lang;
            }
            if (options.TryGetValue("DisplayMode", out optionValue))
            {
                if (Enum.TryParse<ApplicationOptions.DisplayMode>(optionValue, out var mode))
                    ApplicationOptions.CurrentDisplayMode = mode;
            }
            if (options.TryGetValue("CompactSourceMode", out optionValue))
            {
                if (Enum.TryParse<ApplicationOptions.CompactSourceMode>(optionValue, out var csm))
                    ApplicationOptions.CompactSource = csm;
            }
        }

        public static void LoadLanguageSetting()
        {
            if (!Exists()) return;
            try
            {
                var xd = new XmlDocument();
                xd.Load(FilePath);
                var nodes = xd.SelectNodes("/vmping/configuration/option");
                if (nodes == null) return;
                var options = nodes.Cast<XmlNode>()
                    .Where(n => n.Attributes?["name"] != null)
                    .ToDictionary(n => n.Attributes["name"].Value, n => n.InnerText);
                if (options.TryGetValue("Language", out var val))
                {
                    if (Enum.TryParse<ApplicationOptions.AppLanguage>(val, out var lang))
                        ApplicationOptions.Language = lang;
                }
            }
            catch { /* Ignore */ }
        }
    }
}
