using System;
using System.IO;
using System.Linq;

namespace MultiPingMonitor.Tests
{
    public class IconResourceTests
    {
        private static string SolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new DirectoryNotFoundException("Cannot locate solution root from " + AppContext.BaseDirectory);
        }

        private static string IconsPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "ResourceDictionaries", "Icons.xaml");

        [Fact]
        public void AppLogoResource_UsesModernPulseGlyph()
        {
            string source = File.ReadAllText(IconsPath());

            int start = source.IndexOf("x:Key=\"icon.vmping-logo-simple\"", StringComparison.Ordinal);
            Assert.True(start >= 0, "icon.vmping-logo-simple resource not found");

            int nextIcon = source.IndexOf("<DrawingImage x:Key=", start + 1, StringComparison.Ordinal);
            if (nextIcon < 0) nextIcon = source.Length;

            string body = source.Substring(start, nextIcon - start);

            Assert.Contains("MultiPingMonitor pulse logo", source);
            Assert.Contains("M 9,34 H 21 L 27,19 L 35,48 L 41,30 L 47,34 H 55", body);
            Assert.Contains("#FF22D3EE", body);
        }

        [Fact]
        public void AppLogoResource_DoesNotUseOldCrosshairGeometry()
        {
            string source = File.ReadAllText(IconsPath());

            int start = source.IndexOf("x:Key=\"icon.vmping-logo-simple\"", StringComparison.Ordinal);
            Assert.True(start >= 0, "icon.vmping-logo-simple resource not found");

            int nextIcon = source.IndexOf("<DrawingImage x:Key=", start + 1, StringComparison.Ordinal);
            if (nextIcon < 0) nextIcon = source.Length;

            string body = source.Substring(start, nextIcon - start);

            Assert.DoesNotContain("Credit: Ryan Smith", body);
            Assert.DoesNotContain("Center=\"48,48\"", body);
            Assert.DoesNotContain("M5.25,48L29.25,48", body);
            Assert.DoesNotContain("M65.25,48L89.25,48", body);
        }
    }
}
