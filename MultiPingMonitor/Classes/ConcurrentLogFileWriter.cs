using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Serializes append operations per normalized file path and briefly retries
    /// Windows sharing and lock violations.
    /// </summary>
    internal static class ConcurrentLogFileWriter
    {
        private static readonly ConcurrentDictionary<string, object> PathLocks =
            new ConcurrentDictionary<string, object>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly int[] RetryDelaysMilliseconds =
        {
            25,
            50,
            100,
            200
        };

        internal static void AppendAllText(string path, string contents)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (contents == null)
                throw new ArgumentNullException(nameof(contents));

            string fullPath = Path.GetFullPath(path);
            object pathLock = PathLocks.GetOrAdd(
                fullPath,
                _ => new object());

            lock (pathLock)
            {
                int retryIndex = 0;

                while (true)
                {
                    try
                    {
                        using (var stream = new FileStream(
                            fullPath,
                            FileMode.Append,
                            FileAccess.Write,
                            FileShare.Read))
                        using (var writer = new StreamWriter(
                            stream,
                            new UTF8Encoding(
                                encoderShouldEmitUTF8Identifier: false)))
                        {
                            writer.Write(contents);
                        }

                        return;
                    }
                    catch (IOException ex) when (
                        IsSharingOrLockViolation(ex)
                        && retryIndex < RetryDelaysMilliseconds.Length)
                    {
                        Thread.Sleep(
                            RetryDelaysMilliseconds[retryIndex]);

                        retryIndex++;
                    }
                }
            }
        }

        internal static bool IsSharingOrLockViolation(
            IOException exception)
        {
            if (exception == null)
                return false;

            int win32Error = exception.HResult & 0xFFFF;
            return win32Error == 32 || win32Error == 33;
        }
    }
}
