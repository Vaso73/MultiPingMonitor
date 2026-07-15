using MultiPingMonitor.Classes;

namespace MultiPingMonitor.Tests
{
    public sealed class ConcurrentLogFileWriterTests
    {
        [Fact]
        public async Task ConcurrentWritesToSameFile_PreserveEveryLine()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MultiPingMonitor-LogWriterTests-"
                    + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "same-host.txt");

            try
            {
                const int writeCount = 200;

                Task[] tasks = Enumerable.Range(0, writeCount)
                    .Select(index => Task.Run(() =>
                        ConcurrentLogFileWriter.AppendAllText(
                            path,
                            $"entry-{index:D3}{Environment.NewLine}")))
                    .ToArray();

                await Task.WhenAll(tasks);

                string[] lines = File.ReadAllLines(path);

                Assert.Equal(writeCount, lines.Length);
                Assert.Equal(
                    writeCount,
                    lines.Distinct(StringComparer.Ordinal).Count());

                for (int index = 0; index < writeCount; index++)
                {
                    Assert.Contains($"entry-{index:D3}", lines);
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Theory]
        [InlineData(unchecked((int)0x80070020))]
        [InlineData(unchecked((int)0x80070021))]
        public void RecognizesWindowsSharingAndLockViolations(
            int hresult)
        {
            var exception = new TestIOException(hresult);

            Assert.True(
                ConcurrentLogFileWriter.IsSharingOrLockViolation(
                    exception));
        }

        [Fact]
        public void RejectsUnrelatedIoErrors()
        {
            var exception = new TestIOException(
                unchecked((int)0x80070003));

            Assert.False(
                ConcurrentLogFileWriter.IsSharingOrLockViolation(
                    exception));
        }

        private sealed class TestIOException : IOException
        {
            internal TestIOException(int hresult)
            {
                HResult = hresult;
            }
        }
    }
}
