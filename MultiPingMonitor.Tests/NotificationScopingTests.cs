using System;
using System.IO;
using System.Linq;

namespace MultiPingMonitor.Tests
{
    /// <summary>
    /// Regression tests for notification scoping when Compact mode uses a custom Compact Set.
    ///
    /// When Compact mode is active with a custom Compact Set, popup, sound, email, and
    /// status-change-log notifications must come only from that active Compact Set.
    /// Normal/Main probes must have their notifications suppressed until the user leaves
    /// Compact-custom-set mode.
    ///
    /// Tests use lightweight source-code inspection so they run on Linux CI without
    /// WPF/WinForms dependencies.
    /// </summary>
    public class NotificationScopingTests
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

        private static string ProbeSourcePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Classes", "Probe.cs");

        private static string ProbeUtilSourcePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Classes", "Probe-Util.cs");

        private static string MainWindowSourcePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "MainWindow.xaml.cs");

        // ── Probe.SuppressNotifications property ────────────────────────────────

        [Fact]
        public void Probe_HasSuppressNotificationsProperty()
        {
            var source = File.ReadAllText(ProbeSourcePath());
            Assert.Contains("SuppressNotifications", source);
        }

        [Fact]
        public void Probe_SuppressNotificationsDefaultsFalse()
        {
            var source = File.ReadAllText(ProbeSourcePath());
            // Property declaration must include a false default so normal
            // single-context operation is unaffected.
            Assert.Contains("SuppressNotifications", source);
            Assert.Contains("= false", source);
        }

        // ── Probe-Util.cs: OnStatusChange guard ─────────────────────────────────

        [Fact]
        public void ProbeUtil_OnStatusChange_ChecksSuppressNotificationsBeforeDispatching()
        {
            var source = File.ReadAllText(ProbeUtilSourcePath());

            // The method must exist.
            int methodStart = source.IndexOf("private void OnStatusChange(", StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "OnStatusChange method not found in Probe-Util.cs");

            // Find the closing brace of OnStatusChange by locating TriggerStatusChange after it.
            int triggerIdx = source.IndexOf("TriggerStatusChange(", methodStart, StringComparison.Ordinal);
            Assert.True(triggerIdx > methodStart, "TriggerStatusChange call not found after OnStatusChange");

            // The SuppressNotifications guard must appear between the method start and
            // the first TriggerStatusChange call so it short-circuits before dispatching.
            int guardIdx = source.IndexOf("SuppressNotifications", methodStart, StringComparison.Ordinal);
            Assert.True(guardIdx > methodStart && guardIdx < triggerIdx,
                "SuppressNotifications check must appear before TriggerStatusChange in OnStatusChange");
        }

        [Fact]
        public void ProbeUtil_OnStatusChange_ReturnsEarlyWhenSuppressed()
        {
            var source = File.ReadAllText(ProbeUtilSourcePath());

            int methodStart = source.IndexOf("private void OnStatusChange(", StringComparison.Ordinal);
            Assert.True(methodStart >= 0);

            // There must be an early-return guarded by SuppressNotifications.
            int guardIdx = source.IndexOf("SuppressNotifications", methodStart, StringComparison.Ordinal);
            Assert.True(guardIdx > methodStart);

            // A return; statement must follow the guard (within reasonable proximity).
            int returnIdx = source.IndexOf("return;", guardIdx, StringComparison.Ordinal);
            Assert.True(returnIdx > guardIdx && returnIdx - guardIdx < 200,
                "A return; must follow the SuppressNotifications guard in OnStatusChange");
        }

        // ── MainWindow.xaml.cs: ApplyNormalProbeNotificationScope ───────────────

        [Fact]
        public void MainWindow_HasApplyNormalProbeNotificationScope()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("ApplyNormalProbeNotificationScope", source);
        }

        [Fact]
        public void MainWindow_HasShouldSuppressNormalProbeNotificationsHelper()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("ShouldSuppressNormalProbeNotifications", source);
        }

        [Fact]
        public void MainWindow_ApplyNormalProbeNotificationScope_SetsSuppressOnNormalProbes()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodStart = source.IndexOf("private void ApplyNormalProbeNotificationScope()",
                StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "ApplyNormalProbeNotificationScope not found in MainWindow.xaml.cs");

            // The method body must reference _ProbeCollection and SuppressNotifications.
            int methodEnd = source.IndexOf("\n        }", methodStart, StringComparison.Ordinal);
            string body = source.Substring(methodStart, methodEnd - methodStart);
            Assert.Contains("_ProbeCollection", body);
            Assert.Contains("SuppressNotifications", body);
        }

        [Fact]
        public void MainWindow_ShouldSuppressHelper_ChecksCompactAndCustomTargets()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            int methodStart = source.IndexOf("ShouldSuppressNormalProbeNotifications()",
                StringComparison.Ordinal);
            Assert.True(methodStart >= 0);

            // Locate the lambda/method body (search forward for the condition).
            int conditionRegion = source.IndexOf("DisplayMode.Compact", methodStart, StringComparison.Ordinal);
            Assert.True(conditionRegion > methodStart && conditionRegion - methodStart < 300,
                "ShouldSuppressNormalProbeNotifications must check DisplayMode.Compact");

            int customTargetsRegion = source.IndexOf("CustomTargets", methodStart, StringComparison.Ordinal);
            Assert.True(customTargetsRegion > methodStart && customTargetsRegion - methodStart < 400,
                "ShouldSuppressNormalProbeNotifications must check CustomTargets");
        }

        // ── Call sites ─────────────────────────────────────────────────────────

        [Fact]
        public void MainWindow_ApplyCompactDataSource_CallsScopeMethod()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int methodStart = source.IndexOf("internal void ApplyCompactDataSource()",
                StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "ApplyCompactDataSource not found in MainWindow.xaml.cs");

            // The scope helper must be called inside ApplyCompactDataSource.
            int methodEnd = source.IndexOf("\n        }", methodStart, StringComparison.Ordinal);
            string body = source.Substring(methodStart, methodEnd - methodStart);
            Assert.Contains("ApplyNormalProbeNotificationScope", body);
        }

        [Fact]
        public void MainWindow_ApplyDisplayMode_NormalBranch_CallsScopeMethod()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int methodStart = source.IndexOf("internal void ApplyDisplayMode(", StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "ApplyDisplayMode not found in MainWindow.xaml.cs");

            // The scope helper must appear somewhere inside ApplyDisplayMode
            // (covers the Normal-mode branch that restores _ProbeCollection).
            int nextMethod = source.IndexOf("\n        private void", methodStart + 1, StringComparison.Ordinal);
            if (nextMethod < 0) nextMethod = source.Length;
            string body = source.Substring(methodStart, nextMethod - methodStart);
            Assert.Contains("ApplyNormalProbeNotificationScope", body);
        }

        [Fact]
        public void MainWindow_ProbeCollectionChanged_ScopesNewNormalProbes()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int handlerStart = source.IndexOf("private void ProbeCollection_CollectionChanged(",
                StringComparison.Ordinal);
            Assert.True(handlerStart >= 0, "ProbeCollection_CollectionChanged not found in MainWindow.xaml.cs");

            int handlerEnd = source.IndexOf("\n        }", handlerStart, StringComparison.Ordinal);
            string body = source.Substring(handlerStart, handlerEnd - handlerStart);

            // Must scope new items that arrive in _ProbeCollection while suppress is active.
            Assert.Contains("SuppressNotifications", body);
            Assert.Contains("_ProbeCollection", body);
        }

        [Fact]
        public void Probe_HasSuppressFileLoggingProperty()
        {
            var source = File.ReadAllText(ProbeSourcePath());
            Assert.Contains("SuppressFileLogging", source);
        }

        [Fact]
        public void Probe_SuppressFileLoggingDefaultsFalse()
        {
            var source = File.ReadAllText(ProbeSourcePath());
            Assert.Contains("SuppressFileLogging { get; set; } = false", source);
        }

        [Fact]
        public void ProbeUtil_WriteToLog_ChecksSuppressFileLoggingBeforeAppending()
        {
            var source = File.ReadAllText(ProbeUtilSourcePath());

            int methodStart = source.IndexOf("private void WriteToLog(", StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "WriteToLog method not found in Probe-Util.cs");

            int appendIdx = source.IndexOf("File.AppendAllText", methodStart, StringComparison.Ordinal);
            Assert.True(appendIdx > methodStart, "File.AppendAllText call not found in WriteToLog");

            int guardIdx = source.IndexOf("SuppressFileLogging", methodStart, StringComparison.Ordinal);
            Assert.True(guardIdx > methodStart && guardIdx < appendIdx,
                "SuppressFileLogging check must appear before File.AppendAllText in WriteToLog");
        }

        [Fact]
        public void ProbeUtil_WriteToLog_ReturnsEarlyWhenFileLoggingSuppressed()
        {
            var source = File.ReadAllText(ProbeUtilSourcePath());

            int methodStart = source.IndexOf("private void WriteToLog(", StringComparison.Ordinal);
            Assert.True(methodStart >= 0);

            int guardIdx = source.IndexOf("SuppressFileLogging", methodStart, StringComparison.Ordinal);
            Assert.True(guardIdx > methodStart);

            int returnIdx = source.IndexOf("return;", guardIdx, StringComparison.Ordinal);
            Assert.True(returnIdx > guardIdx && returnIdx - guardIdx < 200,
                "A return; must follow the SuppressFileLogging guard in WriteToLog");
        }

        [Fact]
        public void MainWindow_ApplyNormalProbeNotificationScope_SetsFileLoggingSuppressOnNormalProbes()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int methodStart = source.IndexOf("private void ApplyNormalProbeNotificationScope()",
                StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "ApplyNormalProbeNotificationScope not found in MainWindow.xaml.cs");

            int methodEnd = source.IndexOf("\n        }", methodStart, StringComparison.Ordinal);
            string body = source.Substring(methodStart, methodEnd - methodStart);

            Assert.Contains("_ProbeCollection", body);
            Assert.Contains("SuppressNotifications", body);
            Assert.Contains("SuppressFileLogging", body);
        }

        [Fact]
        public void MainWindow_ProbeCollectionChanged_ScopesNewNormalProbeFileLogging()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int handlerStart = source.IndexOf("private void ProbeCollection_CollectionChanged(",
                StringComparison.Ordinal);
            Assert.True(handlerStart >= 0, "ProbeCollection_CollectionChanged not found in MainWindow.xaml.cs");

            int handlerEnd = source.IndexOf("\n        }", handlerStart, StringComparison.Ordinal);
            string body = source.Substring(handlerStart, handlerEnd - handlerStart);

            Assert.Contains("_ProbeCollection", body);
            Assert.Contains("SuppressNotifications", body);
            Assert.Contains("SuppressFileLogging", body);
        }

        [Fact]
        public void MainWindow_HasShouldSuppressCompactProbeSideEffectsHelper()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("ShouldSuppressCompactProbeSideEffects", source);
        }

        [Fact]
        public void MainWindow_ShouldSuppressCompactProbeSideEffects_ChecksDisplayModeSourceAndRunningState()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int methodStart = source.IndexOf("ShouldSuppressCompactProbeSideEffects()",
                StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "ShouldSuppressCompactProbeSideEffects not found");

            int nextSummary = source.IndexOf("/// <summary>", methodStart + 1, StringComparison.Ordinal);
            if (nextSummary < 0) nextSummary = source.Length;
            string body = source.Substring(methodStart, nextSummary - methodStart);

            Assert.Contains("DisplayMode.Compact", body);
            Assert.Contains("CompactSourceMode.CustomTargets", body);
            Assert.Contains("IsCompactSetRunning", body);
        }

        [Fact]
        public void MainWindow_ApplyNormalProbeNotificationScope_ScopesCompactProbeSideEffects()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int methodStart = source.IndexOf("private void ApplyNormalProbeNotificationScope()",
                StringComparison.Ordinal);
            Assert.True(methodStart >= 0, "ApplyNormalProbeNotificationScope not found in MainWindow.xaml.cs");

            int nextMethod = source.IndexOf("\n        /// <summary>", methodStart + 1, StringComparison.Ordinal);
            if (nextMethod < 0) nextMethod = source.Length;
            string body = source.Substring(methodStart, nextMethod - methodStart);

            Assert.Contains("_CompactProbeCollection", body);
            Assert.Contains("ShouldSuppressCompactProbeSideEffects", body);
            Assert.Contains("SuppressNotifications", body);
            Assert.Contains("SuppressFileLogging", body);
        }

        [Fact]
        public void MainWindow_ProbeCollectionChanged_ScopesNewCompactProbes()
        {
            var source = File.ReadAllText(MainWindowSourcePath());

            int handlerStart = source.IndexOf("private void ProbeCollection_CollectionChanged(",
                StringComparison.Ordinal);
            Assert.True(handlerStart >= 0, "ProbeCollection_CollectionChanged not found in MainWindow.xaml.cs");

            int nextMethod = source.IndexOf("\n        private void", handlerStart + 1, StringComparison.Ordinal);
            if (nextMethod < 0) nextMethod = source.Length;
            string body = source.Substring(handlerStart, nextMethod - handlerStart);

            Assert.Contains("_CompactProbeCollection", body);
            Assert.Contains("ShouldSuppressCompactProbeSideEffects", body);
            Assert.Contains("SuppressNotifications", body);
            Assert.Contains("SuppressFileLogging", body);
        }


    }
}
