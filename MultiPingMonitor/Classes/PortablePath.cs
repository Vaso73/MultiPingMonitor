using System;

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
        private const string AppDataToken = "%APPDATA%";

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
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);

            return path.Replace(AppDataToken, baseDir, StringComparison.OrdinalIgnoreCase);
        }
    }
}
