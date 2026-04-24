using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MultiPingMonitor.Tests
{
    /// <summary>
    /// Regression tests for the Compact Set start/stop feature.
    ///
    /// When Compact mode uses a custom Compact Set the user should be able to
    /// stop the active set (halting all probe loops and suppressing notifications)
    /// and start it again without restarting the application.
    ///
    /// Tests use lightweight source-code and XML inspection so they run on
    /// Linux CI without WPF/WinForms dependencies.
    /// </summary>
    public class CompactSetStartStopTests
    {
        // ── path helpers ────────────────────────────────────────────────────────

        private static string SolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;
            return dir?.FullName
                ?? throw new DirectoryNotFoundException("Cannot locate solution root from " + AppContext.BaseDirectory);
        }

        private static string ApplicationOptionsPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Classes", "ApplicationOptions.cs");

        private static string ConfigurationPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Classes", "Configuration.cs");

        private static string MainWindowSourcePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "MainWindow.xaml.cs");

        private static string MainWindowXamlPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "MainWindow.xaml");

        private static string DefaultResxPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Properties", "Strings.resx");

        private static string SkSkResxPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Properties", "Strings.sk-SK.resx");

        private static string ResxValue(string resxPath, string key)
        {
            var doc = XDocument.Load(resxPath);
            var element = doc.Root!
                .Elements("data")
                .FirstOrDefault(d => (string?)d.Attribute("name") == key);

            if (element == null)
                throw new KeyNotFoundException($"Resource key '{key}' not found in {resxPath}");

            return (string?)element.Element("value") ?? string.Empty;
        }

        // ── ApplicationOptions.IsCompactSetRunning ─────────────────────────────

        [Fact]
        public void ApplicationOptions_HasIsCompactSetRunningProperty()
        {
            var source = File.ReadAllText(ApplicationOptionsPath());
            Assert.Contains("IsCompactSetRunning", source);
        }

        [Fact]
        public void ApplicationOptions_IsCompactSetRunningDefaultsTrue()
        {
            var source = File.ReadAllText(ApplicationOptionsPath());
            // Default must be true so freshly configured sets start monitoring automatically.
            int idx = source.IndexOf("IsCompactSetRunning", StringComparison.Ordinal);
            Assert.True(idx >= 0, "IsCompactSetRunning not found");
            // Look for default assignment near the property declaration.
            string region = source.Substring(idx, Math.Min(120, source.Length - idx));
            Assert.Contains("= true", region);
        }

        // ── Configuration persistence ──────────────────────────────────────────

        [Fact]
        public void Configuration_GenerateCompactSetsNode_WritesIsRunningAttribute()
        {
            var source = File.ReadAllText(ConfigurationPath());
            Assert.Contains("isRunning", source);
            Assert.Contains("IsCompactSetRunning", source);
        }

        [Fact]
        public void Configuration_LoadCompactSets_ReadsIsRunningAttribute()
        {
            var source = File.ReadAllText(ConfigurationPath());
            // The load path must read the isRunning attribute and assign it.
            int loadMethodIdx = source.IndexOf("private static void LoadCompactSets(", StringComparison.Ordinal);
            Assert.True(loadMethodIdx >= 0, "LoadCompactSets not found");
            int nextMethodIdx = source.IndexOf("\n        private static", loadMethodIdx + 1, StringComparison.Ordinal);
            if (nextMethodIdx < 0) nextMethodIdx = source.Length;
            string body = source.Substring(loadMethodIdx, nextMethodIdx - loadMethodIdx);
            Assert.Contains("IsCompactSetRunning", body);
            Assert.Contains("isRunning", body);
        }

        [Fact]
        public void Configuration_LoadCompactSets_DefaultsTrueWhenAttributeAbsent()
        {
            var source = File.ReadAllText(ConfigurationPath());
            int loadMethodIdx = source.IndexOf("private static void LoadCompactSets(", StringComparison.Ordinal);
            Assert.True(loadMethodIdx >= 0);
            int nextMethodIdx = source.IndexOf("\n        private static", loadMethodIdx + 1, StringComparison.Ordinal);
            if (nextMethodIdx < 0) nextMethodIdx = source.Length;
            string body = source.Substring(loadMethodIdx, nextMethodIdx - loadMethodIdx);
            // Must default to true when attribute is absent (older config files).
            Assert.Contains("null", body);
        }

        // ── MainWindow.StartStopCompactSet ─────────────────────────────────────

        [Fact]
        public void MainWindow_HasStartStopCompactSetMethod()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("StartStopCompactSet()", source);
        }

        [Fact]
        public void MainWindow_StartStopCompactSet_TogglesIsCompactSetRunning()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("internal void StartStopCompactSet()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "StartStopCompactSet not found");
            int methodEnd = source.IndexOf("\n        }", methodIdx, StringComparison.Ordinal);
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("IsCompactSetRunning", body);
        }

        [Fact]
        public void MainWindow_StartStopCompactSet_StopsActiveProbes()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("internal void StartStopCompactSet()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        }", methodIdx, StringComparison.Ordinal);
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            // Stop branch must call StartStop() on active probes.
            Assert.Contains("probe.StartStop()", body);
        }

        [Fact]
        public void MainWindow_StartStopCompactSet_SuppressesNotificationsWhenStopped()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("internal void StartStopCompactSet()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        }", methodIdx, StringComparison.Ordinal);
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("SuppressNotifications", body);
        }

        [Fact]
        public void MainWindow_StartStopCompactSet_SavesConfiguration()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("internal void StartStopCompactSet()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        }", methodIdx, StringComparison.Ordinal);
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("Configuration.Save()", body);
        }

        // ── MainWindow.RebuildCompactProbes respects IsCompactSetRunning ────────

        [Fact]
        public void MainWindow_RebuildCompactProbes_RespectsRunningState()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void RebuildCompactProbes()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "RebuildCompactProbes not found");
            int methodEnd = source.IndexOf("\n        }", methodIdx, StringComparison.Ordinal);
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            // Must check IsCompactSetRunning before auto-starting each probe.
            Assert.Contains("IsCompactSetRunning", body);
        }

        // ── UI indicator ───────────────────────────────────────────────────────

        [Fact]
        public void MainWindow_Xaml_HasCompactStoppedBadge()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactStoppedBadge", xaml);
        }

        [Fact]
        public void MainWindow_HasUpdateCompactStoppedIndicator()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("UpdateCompactStoppedIndicator", source);
        }

        // ── Localization strings ───────────────────────────────────────────────

        [Fact]
        public void Strings_Default_HasCompact_StartSet()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_StartSet");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_Default_HasCompact_StopSet()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_StopSet");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_Default_HasCompact_SetStopped()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_SetStopped");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_StartSet()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_StartSet");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_StopSet()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_StopSet");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_SetStopped()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_SetStopped");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_Compact_StartSet_IsNotEnglish()
        {
            var skValue = ResxValue(SkSkResxPath(), "Compact_StartSet");
            var enValue = ResxValue(DefaultResxPath(), "Compact_StartSet");
            Assert.NotEqual(enValue, skValue);
        }

        [Fact]
        public void Strings_SkSk_Compact_StopSet_IsNotEnglish()
        {
            var skValue = ResxValue(SkSkResxPath(), "Compact_StopSet");
            var enValue = ResxValue(DefaultResxPath(), "Compact_StopSet");
            Assert.NotEqual(enValue, skValue);
        }

        // ── Menu integration ───────────────────────────────────────────────────

        [Fact]
        public void MainWindow_CompactMenuButtonClick_UsesCompactStartStopStrings()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactMenuButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "CompactMenuButton_Click not found");
            int methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("Compact_StopSet", body);
            Assert.Contains("Compact_StartSet", body);
            Assert.Contains("StartStopCompactSet", body);
        }

        [Fact]
        public void MainWindow_CompactTargetsMenuSubmenuOpened_UsesCompactStartStopStrings()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactTargetsMenu_SubmenuOpened(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "CompactTargetsMenu_SubmenuOpened not found");
            int methodEnd = source.IndexOf("\n        // ──", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("Compact_StopSet", body);
            Assert.Contains("Compact_StartSet", body);
            Assert.Contains("StartStopCompactSet", body);
        }

        // ── Titlebar Start/Stop button ─────────────────────────────────────────

        [Fact]
        public void MainWindow_Xaml_HasCompactStartStopButton()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactStartStopButton", xaml);
        }

        [Fact]
        public void MainWindow_Xaml_HasCompactStartStopIcon()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactStartStopIcon", xaml);
        }

        [Fact]
        public void MainWindow_Xaml_CompactStartStopButton_IsInitiallyCollapsed()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            // The button must be declared Collapsed so it is hidden by default
            // and only shown by code-behind when applicable.
            int idx = xaml.IndexOf("CompactStartStopButton", StringComparison.Ordinal);
            Assert.True(idx >= 0, "CompactStartStopButton not found in XAML");
            // Look in a reasonable window around the name for Visibility="Collapsed"
            string region = xaml.Substring(Math.Max(0, idx - 200), Math.Min(600, xaml.Length - Math.Max(0, idx - 200)));
            Assert.Contains("Visibility=\"Collapsed\"", region);
        }

        [Fact]
        public void MainWindow_HasUpdateCompactStartStopButton()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("UpdateCompactStartStopButton", source);
        }

        [Fact]
        public void MainWindow_UpdateCompactStartStopButton_ChecksCustomTargetsAndActiveSet()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void UpdateCompactStartStopButton()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "UpdateCompactStartStopButton not found");
            int methodEnd = source.IndexOf("\n        private ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("CustomTargets", body);
            Assert.Contains("GetActiveCompactSet", body);
            Assert.Contains("IsCompactSetRunning", body);
        }

        [Fact]
        public void MainWindow_UpdateCompactStartStopButton_UsesStartStopStrings()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void UpdateCompactStartStopButton()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        private ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("Compact_StopSet", body);
            Assert.Contains("Compact_StartSet", body);
        }

        [Fact]
        public void MainWindow_HasCompactStartStopButton_ClickHandler()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("CompactStartStopButton_Click", source);
        }

        [Fact]
        public void MainWindow_CompactStartStopButton_ClickHandler_CallsStartStopCompactSet()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactStartStopButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "CompactStartStopButton_Click not found");
            int methodEnd = source.IndexOf("\n        }", methodIdx, StringComparison.Ordinal);
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("StartStopCompactSet()", body);
        }

        [Fact]
        public void MainWindow_UpdateCompactStoppedIndicator_CallsUpdateCompactStartStopButton()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void UpdateCompactStoppedIndicator()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "UpdateCompactStoppedIndicator not found");
            int methodEnd = source.IndexOf("\n        private ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("UpdateCompactStartStopButton", body);
        }

        [Fact]
        public void MainWindow_Xaml_CompactStartStopButton_HasClickHandler()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactStartStopButton_Click", xaml);
        }

        [Fact]
        public void MainWindow_Xaml_CompactStartStopButton_UsesTitleBarButtonStyle()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            int idx = xaml.IndexOf("CompactStartStopButton", StringComparison.Ordinal);
            Assert.True(idx >= 0);
            // Look forward from the button name for the style reference within the same element
            string region = xaml.Substring(Math.Max(0, idx - 100), Math.Min(800, xaml.Length - Math.Max(0, idx - 100)));
            Assert.Contains("Style.TitleBarButton", region);
        }
    }
}
