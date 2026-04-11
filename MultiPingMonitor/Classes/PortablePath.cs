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
        /// Ensures the directory portion of the given <b>expanded</b> path exists.
        /// If <paramref name="expandedPath"/> looks like a directory (no extension),
        /// the path itself is created; otherwise, only its parent directory is created.
        /// Returns <c>true</c> when the directory exists or was successfully created.
        /// </summary>
        public static bool EnsureDirectoryExists(string expandedPath)
        {
            if (string.IsNullOrEmpty(expandedPath))
                return false;

            try
            {
                // Determine whether the path represents a directory or a file.
                string dir = string.IsNullOrEmpty(Path.GetExtension(expandedPath))
                    ? expandedPath
                    : Path.GetDirectoryName(expandedPath);

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
