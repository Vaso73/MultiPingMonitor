#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace MultiPingMonitor.Classes
{
    internal static class WindowPlacementStorage
    {
        internal const string PlacementFileName = "window-placement.xml";
        internal const string BackupFileName = "window-placement.xml.bak";
        internal const string UnknownMachineName = "UNKNOWN-MACHINE";

        internal static string CurrentMachineName =>
            SanitizeMachineName(Environment.MachineName);

        internal static string CurrentFilePath =>
            BuildFilePath(AppContext.BaseDirectory, Environment.MachineName);

        internal static string BuildMachineDirectory(
            string baseDirectory,
            string machineName)
        {
            return Path.Combine(
                baseDirectory,
                "data",
                "machines",
                SanitizeMachineName(machineName));
        }

        internal static string BuildFilePath(
            string baseDirectory,
            string machineName)
        {
            return Path.Combine(
                BuildMachineDirectory(baseDirectory, machineName),
                PlacementFileName);
        }

        internal static string SanitizeMachineName(string machineName)
        {
            if (string.IsNullOrWhiteSpace(machineName))
                return UnknownMachineName;

            const string invalidWindowsNameCharacters = "<>:\"/\\|?*";

            char[] sanitized = machineName
                .Trim()
                .Select(character =>
                    char.IsControl(character) ||
                    invalidWindowsNameCharacters.Contains(character)
                        ? '_'
                        : character)
                .ToArray();

            string result = new string(sanitized)
                .Trim()
                .TrimEnd('.', ' ');

            return string.IsNullOrWhiteSpace(result)
                ? UnknownMachineName
                : result;
        }

        internal static XElement? Load()
        {
            return Load(AppContext.BaseDirectory, Environment.MachineName);
        }

        internal static XElement? Load(
            string baseDirectory,
            string machineName)
        {
            string directory =
                BuildMachineDirectory(baseDirectory, machineName);

            string primaryPath =
                Path.Combine(directory, PlacementFileName);

            string backupPath =
                Path.Combine(directory, BackupFileName);

            foreach (string candidate in new[] { primaryPath, backupPath })
            {
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    XDocument document = XDocument.Load(candidate);

                    if (document.Root?.Name != "windowPlacements")
                        continue;

                    return new XElement(document.Root);
                }
                catch (Exception exception)
                    when (exception is IOException ||
                          exception is UnauthorizedAccessException ||
                          exception is XmlException)
                {
                    Trace.WriteLine(
                        $"[WPS] Could not load {candidate}: " +
                        exception.Message);
                }
            }

            return null;
        }

        internal static void Save(XElement placementsNode)
        {
            Save(
                placementsNode,
                AppContext.BaseDirectory,
                Environment.MachineName);
        }

        internal static void Save(
            XElement placementsNode,
            string baseDirectory,
            string machineName)
        {
            if (placementsNode == null)
                throw new ArgumentNullException(nameof(placementsNode));

            string safeMachineName =
                SanitizeMachineName(machineName);

            string directory =
                BuildMachineDirectory(baseDirectory, safeMachineName);

            string primaryPath =
                Path.Combine(directory, PlacementFileName);

            string backupPath =
                Path.Combine(directory, BackupFileName);

            string temporaryPath = primaryPath + ".tmp";

            Directory.CreateDirectory(directory);

            var root = new XElement(placementsNode);
            root.SetAttributeValue("machine", safeMachineName);

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root);

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    document.Save(stream);
                    stream.Flush(true);
                }

                _ = XDocument.Load(temporaryPath);

                if (File.Exists(primaryPath))
                {
                    try
                    {
                        File.Replace(
                            temporaryPath,
                            primaryPath,
                            backupPath,
                            true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithFallback(
                            temporaryPath,
                            primaryPath,
                            backupPath);
                    }
                    catch (IOException)
                    {
                        ReplaceWithFallback(
                            temporaryPath,
                            primaryPath,
                            backupPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, primaryPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static void ReplaceWithFallback(
            string temporaryPath,
            string primaryPath,
            string backupPath)
        {
            File.Copy(primaryPath, backupPath, true);
            File.Move(temporaryPath, primaryPath, true);
        }
    }
}
