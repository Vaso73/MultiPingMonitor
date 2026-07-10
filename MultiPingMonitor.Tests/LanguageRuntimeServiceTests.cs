using System.Globalization;
using MultiPingMonitor.Classes;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class LanguageRuntimeServiceTests
    {
        [Fact]
        public void SelectSystemCulture_PrefersWindowsUserUiCultureOverEnglishProcessCultures()
        {
            CultureInfo result = LanguageRuntimeService.SelectSystemCulture(
                new CultureInfo("sk-SK"),
                new CultureInfo("en-US"),
                new CultureInfo("en-US"));

            Assert.Equal("sk-SK", result.Name);
        }

        [Fact]
        public void SelectSystemCulture_FallsBackToStartupUiCultureOutsideWindows()
        {
            CultureInfo result = LanguageRuntimeService.SelectSystemCulture(
                null,
                new CultureInfo("sk-SK"),
                new CultureInfo("en-US"));

            Assert.Equal("sk-SK", result.Name);
        }
    }
}
