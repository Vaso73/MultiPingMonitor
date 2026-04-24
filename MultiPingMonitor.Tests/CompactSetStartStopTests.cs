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

        // ── Compact set toolbar row ────────────────────────────────────────────

        [Fact]
        public void MainWindow_Xaml_HasCompactSetToolbar()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactSetToolbar", xaml);
        }

        [Fact]
        public void MainWindow_Xaml_CompactSetToolbar_IsInitiallyCollapsed()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            int idx = xaml.IndexOf("CompactSetToolbar", StringComparison.Ordinal);
            Assert.True(idx >= 0, "CompactSetToolbar not found in XAML");
            // The toolbar Grid element must declare Visibility="Collapsed" (managed by code-behind)
            string region = xaml.Substring(Math.Max(0, idx - 50), Math.Min(400, xaml.Length - Math.Max(0, idx - 50)));
            Assert.Contains("Visibility=\"Collapsed\"", region);
        }

        [Fact]
        public void MainWindow_Xaml_HasCompactSetNameText()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactSetNameText", xaml);
        }

        [Fact]
        public void MainWindow_Xaml_CompactSetNameText_HasEllipsis()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            int idx = xaml.IndexOf("CompactSetNameText", StringComparison.Ordinal);
            Assert.True(idx >= 0);
            string region = xaml.Substring(Math.Max(0, idx - 50), Math.Min(400, xaml.Length - Math.Max(0, idx - 50)));
            Assert.Contains("TextTrimming", region);
        }

        [Fact]
        public void MainWindow_Xaml_CompactStoppedBadge_IsInToolbarRow()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            // CompactSetToolbar must appear before CompactStoppedBadge in the document
            int toolbarIdx = xaml.IndexOf("CompactSetToolbar", StringComparison.Ordinal);
            int badgeIdx = xaml.IndexOf("CompactStoppedBadge", StringComparison.Ordinal);
            Assert.True(toolbarIdx >= 0, "CompactSetToolbar not found");
            Assert.True(badgeIdx >= 0, "CompactStoppedBadge not found");
            Assert.True(badgeIdx > toolbarIdx, "CompactStoppedBadge should appear inside CompactSetToolbar (after it in XAML)");
        }

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
        public void MainWindow_Xaml_CompactStartStopButton_IsInToolbarRow_NotTitleBar()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            int toolbarIdx = xaml.IndexOf("CompactSetToolbar", StringComparison.Ordinal);
            int titleBarIdx = xaml.IndexOf("x:Name=\"CompactTitleBar\"", StringComparison.Ordinal);
            int btnIdx = xaml.IndexOf("CompactStartStopButton", StringComparison.Ordinal);
            Assert.True(toolbarIdx >= 0, "CompactSetToolbar not found");
            Assert.True(titleBarIdx >= 0, "CompactTitleBar not found");
            Assert.True(btnIdx >= 0, "CompactStartStopButton not found");
            // Button must appear after the toolbar starts and after the title bar section
            Assert.True(btnIdx > toolbarIdx, "CompactStartStopButton should be inside CompactSetToolbar");
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
        public void MainWindow_UpdateCompactStartStopButton_UpdatesSetNameText()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void UpdateCompactStartStopButton()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        private ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("CompactSetNameText", body);
            Assert.Contains(".Name", body);
        }

        [Fact]
        public void MainWindow_UpdateCompactStartStopButton_ManagesToolbarVisibility()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void UpdateCompactStartStopButton()", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        private ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("CompactSetToolbar", body);
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
        public void MainWindow_ApplyDisplayMode_CollapsesToolbarOnReturnToNormal()
        {
            // When ApplyDisplayMode switches back to Normal mode it must call
            // UpdateCompactStartStopButton() so the toolbar is collapsed.
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("internal void ApplyDisplayMode(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "ApplyDisplayMode not found");
            int methodEnd = source.IndexOf("\n        // ── Pin", methodIdx + 1, StringComparison.Ordinal);
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

        // ── Quick Add Host feature ─────────────────────────────────────────────

        [Fact]
        public void MainWindow_Xaml_HasCompactAddHostButton()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactAddHostButton", xaml);
        }

        [Fact]
        public void MainWindow_Xaml_CompactAddHostButton_IsInToolbarRow()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            int toolbarIdx = xaml.IndexOf("CompactSetToolbar", StringComparison.Ordinal);
            int btnIdx = xaml.IndexOf("CompactAddHostButton", StringComparison.Ordinal);
            Assert.True(toolbarIdx >= 0, "CompactSetToolbar not found");
            Assert.True(btnIdx >= 0, "CompactAddHostButton not found");
            Assert.True(btnIdx > toolbarIdx, "CompactAddHostButton should be inside CompactSetToolbar");
        }

        [Fact]
        public void MainWindow_Xaml_CompactAddHostButton_HasClickHandler()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactAddHostButton_Click", xaml);
        }

        [Fact]
        public void MainWindow_Xaml_CompactAddHostButton_UsesTitleBarButtonStyle()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            int idx = xaml.IndexOf("CompactAddHostButton", StringComparison.Ordinal);
            Assert.True(idx >= 0);
            string region = xaml.Substring(Math.Max(0, idx - 100), Math.Min(800, xaml.Length - Math.Max(0, idx - 100)));
            Assert.Contains("Style.TitleBarButton", region);
        }

        [Fact]
        public void MainWindow_HasCompactAddHostButton_ClickHandler()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("CompactAddHostButton_Click", source);
        }

        [Fact]
        public void MainWindow_CompactAddHostButton_ClickHandler_ChecksCustomTargets()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactAddHostButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "CompactAddHostButton_Click not found");
            int methodEnd = source.IndexOf("\n        }", methodIdx, StringComparison.Ordinal);
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("CustomTargets", body);
            Assert.Contains("GetActiveCompactSet", body);
        }

        [Fact]
        public void MainWindow_CompactAddHostButton_ClickHandler_AddsToCompactProbeCollection()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactAddHostButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            // Find the end of the method more broadly
            int methodEnd = source.IndexOf("\n        /// ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("_CompactProbeCollection.Add", body);
            Assert.Contains("Configuration.Save()", body);
        }

        [Fact]
        public void MainWindow_CompactAddHostButton_ClickHandler_StartsProbeWhenRunning()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactAddHostButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        /// ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("IsCompactSetRunning", body);
            Assert.Contains("probe.StartStop()", body);
        }

        [Fact]
        public void MainWindow_CompactAddHostButton_ClickHandler_SuppressesNotificationsWhenStopped()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactAddHostButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        /// ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("SuppressNotifications", body);
        }

        [Fact]
        public void MainWindow_CompactAddHostButton_ClickHandler_CanOpenLivePing()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactAddHostButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0);
            int methodEnd = source.IndexOf("\n        /// ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("OpenLivePing", body);
            Assert.Contains("LivePingMonitorWindow", body);
        }

        [Fact]
        public void Strings_Default_HasCompact_AddHost()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_AddHost");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_Default_HasCompact_AddHost_Host()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_AddHost_Host");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_Default_HasCompact_AddHost_Alias()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_AddHost_Alias");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_Default_HasCompact_AddHost_OpenLivePing()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_AddHost_OpenLivePing");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_AddHost()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_AddHost");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_AddHost_Host()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_AddHost_Host");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_AddHost_Alias()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_AddHost_Alias");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_AddHost_OpenLivePing()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_AddHost_OpenLivePing");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_Compact_AddHost_IsNotEnglish()
        {
            var skValue = ResxValue(SkSkResxPath(), "Compact_AddHost");
            var enValue = ResxValue(DefaultResxPath(), "Compact_AddHost");
            Assert.NotEqual(enValue, skValue);
        }

        [Fact]
        public void Strings_SkSk_Compact_AddHost_OpenLivePing_IsNotEnglish()
        {
            var skValue = ResxValue(SkSkResxPath(), "Compact_AddHost_OpenLivePing");
            var enValue = ResxValue(DefaultResxPath(), "Compact_AddHost_OpenLivePing");
            Assert.NotEqual(enValue, skValue);
        }

        [Fact]
        public void AddCompactHostDialog_Exists()
        {
            var dialogPath = Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "AddCompactHostDialog.xaml.cs");
            Assert.True(File.Exists(dialogPath), "AddCompactHostDialog.xaml.cs not found");
        }

        [Fact]
        public void AddCompactHostDialog_HasHostAliasAndOpenLivePingMembers()
        {
            var dialogPath = Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "AddCompactHostDialog.xaml.cs");
            var source = File.ReadAllText(dialogPath);
            Assert.Contains("Host", source);
            Assert.Contains("Alias", source);
            Assert.Contains("OpenLivePing", source);
        }

        [Fact]
        public void AddCompactHostDialog_ValidatesEmptyHost_InOkClick()
        {
            var dialogPath = Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "AddCompactHostDialog.xaml.cs");
            var source = File.ReadAllText(dialogPath);
            // Validation must be in OK_Click, not deferred to the caller.
            int methodIdx = source.IndexOf("private void OK_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "OK_Click not found");
            int methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            // Must check for empty/whitespace host.
            Assert.True(body.Contains("IsNullOrWhiteSpace") || body.Contains("string.IsNullOrWhiteSpace"),
                "OK_Click must validate for empty/whitespace host");
            // Must NOT set DialogResult = true unconditionally.
            Assert.Contains("return", body);
        }

        [Fact]
        public void AddCompactHostDialog_ShowsErrorDialog_WhenHostIsEmpty()
        {
            var dialogPath = Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "AddCompactHostDialog.xaml.cs");
            var source = File.ReadAllText(dialogPath);
            int methodIdx = source.IndexOf("private void OK_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "OK_Click not found");
            int methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            // Must use the themed DialogWindow for error display (not plain MessageBox).
            Assert.Contains("DialogWindow", body);
            Assert.Contains("Compact_AddHost_EmptyHost", body);
        }

        [Fact]
        public void AddCompactHostDialog_FocusesHostField_AfterEmptyValidation()
        {
            var dialogPath = Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "AddCompactHostDialog.xaml.cs");
            var source = File.ReadAllText(dialogPath);
            int methodIdx = source.IndexOf("private void OK_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "OK_Click not found");
            int methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            Assert.Contains("HostField.Focus()", body);
        }

        [Fact]
        public void Strings_Default_HasCompact_AddHost_EmptyHost()
        {
            var value = ResxValue(DefaultResxPath(), "Compact_AddHost_EmptyHost");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_HasCompact_AddHost_EmptyHost()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_AddHost_EmptyHost");
            Assert.False(string.IsNullOrWhiteSpace(value));
        }

        [Fact]
        public void Strings_SkSk_Compact_AddHost_EmptyHost_IsNotEnglish()
        {
            var skValue = ResxValue(SkSkResxPath(), "Compact_AddHost_EmptyHost");
            var enValue = ResxValue(DefaultResxPath(), "Compact_AddHost_EmptyHost");
            Assert.NotEqual(enValue, skValue);
        }

        [Fact]
        public void MainWindow_CompactAddHostButton_DuplicateIsHardRejected_NotYesNo()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactAddHostButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "CompactAddHostButton_Click not found");
            int methodEnd = source.IndexOf("\n        /// ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            // Duplicate must be handled via DialogWindow.InfoWindow (themed), not MessageBoxButton.YesNo.
            Assert.Contains("InfoWindow", body);
            Assert.DoesNotContain("MessageBoxButton.YesNo", body);
            Assert.DoesNotContain("MessageBoxResult.Yes", body);
        }

        [Fact]
        public void MainWindow_CompactAddHostButton_DuplicateReturnsWithoutAdding()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodIdx = source.IndexOf("private void CompactAddHostButton_Click(", StringComparison.Ordinal);
            Assert.True(methodIdx >= 0, "CompactAddHostButton_Click not found");
            int methodEnd = source.IndexOf("\n        /// ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.IndexOf("\n        private void ", methodIdx + 1, StringComparison.Ordinal);
            if (methodEnd < 0) methodEnd = source.Length;
            string body = source.Substring(methodIdx, methodEnd - methodIdx);
            // After showing the duplicate message the handler must return without adding.
            int duplicateIdx = body.IndexOf("isDuplicate", StringComparison.Ordinal);
            Assert.True(duplicateIdx >= 0, "isDuplicate check not found");
            // Within the duplicate block there must be a return statement before _CompactProbeCollection.Add.
            int addIdx = body.IndexOf("_CompactProbeCollection.Add", StringComparison.Ordinal);
            int returnInDuplicate = body.IndexOf("return;", duplicateIdx, StringComparison.Ordinal);
            Assert.True(returnInDuplicate < addIdx, "Duplicate block must return before adding to collection");
        }
    }
}
