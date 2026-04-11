using System;
using System.IO;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Expands custom path tokens used in portable mode.
    /// <para>
    /// <c>%APPDATA%</c> is resolved to the directory that contains
    /// <c>MultiPingMonitor.exe</c> (i.e. <see cref="AppDomain.CurrentDomain.BaseDirectory"/>),
    /// <b>not</b> the Windows <c>AppData\Roaming</c> folder.
    /// </para>
    /// </summary>
    internal static class PortablePath
    {
        public const string AppDataToken = "%APPDATA%";

        /// <summary>
        /// Replaces every occurrence of <c>%APPDATA%</c> (case-insensitive)
        /// with the application base directory and returns the resulting path.
        /// Returns the original value unchanged when it is <c>null</c> or empty,
        /// or when it does not contain the token.
        /// </summary>
        public static string ExpandTokens(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (path.IndexOf(AppDataToken, StringComparison.OrdinalIgnoreCase) < 0)
                return path;

            // AppDomain.CurrentDomain.BaseDirectory always ends with a directory
            // separator; trim it so callers get clean paths when combining.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            return path.Replace(AppDataToken, baseDir, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures that the given directory exists, creating it (and parents)
        /// if necessary. Returns <c>true</c> on success.
        /// </summary>
        public static bool EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return false;

            try
            {
                Directory.CreateDirectory(directoryPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures that the parent directory of the given <b>file</b> path exists,
        /// creating it (and parents) if necessary. Returns <c>true</c> on success.
        /// </summary>
        public static bool EnsureParentDirectoryExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
