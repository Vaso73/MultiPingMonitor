using System;
using System.IO;
using System.Linq;
using MultiPingMonitor.Classes;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class LivePingActionStateTests
    {
        [Fact]
        public void EmptyManualWindowDisablesTargetDependentActions()
        {
            LivePingActionState state = LivePingActionState.Evaluate(
                true,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0);

            Assert.False(state.CanStart);
            Assert.False(state.CanCopyTarget);
            Assert.False(state.CanCopyAddress);
            Assert.False(state.CanPauseResume);
            Assert.False(state.CanClear);
        }

        [Fact]
        public void WhitespaceInputDoesNotEnableStart()
        {
            LivePingActionState state = LivePingActionState.Evaluate(
                true,
                "   ",
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0);

            Assert.False(state.CanStart);
        }

        [Fact]
        public void TypedManualTargetEnablesOnlyStartBeforeProbeExists()
        {
            LivePingActionState state = LivePingActionState.Evaluate(
                true,
                "example.com",
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0);

            Assert.True(state.CanStart);
            Assert.False(state.CanCopyTarget);
            Assert.False(state.CanCopyAddress);
            Assert.False(state.CanPauseResume);
            Assert.False(state.CanClear);
        }

        [Fact]
        public void ExistingTargetEnablesCopyTargetAndPauseResume()
        {
            LivePingActionState state = LivePingActionState.Evaluate(
                false,
                string.Empty,
                "example.com",
                string.Empty,
                0,
                0,
                0,
                0);

            Assert.False(state.CanStart);
            Assert.True(state.CanCopyTarget);
            Assert.False(state.CanCopyAddress);
            Assert.True(state.CanPauseResume);
            Assert.False(state.CanClear);
        }

        [Fact]
        public void ResolvedAddressAndResultsEnableAddressAndClear()
        {
            LivePingActionState state = LivePingActionState.Evaluate(
                true,
                "example.com",
                "example.com",
                "192.0.2.10",
                1,
                1,
                1,
                0);

            Assert.True(state.CanStart);
            Assert.True(state.CanCopyTarget);
            Assert.True(state.CanCopyAddress);
            Assert.True(state.CanPauseResume);
            Assert.True(state.CanClear);
        }

        [Fact]
        public void CountersKeepClearEnabledWhenVisibleLogIsEmpty()
        {
            LivePingActionState state = LivePingActionState.Evaluate(
                false,
                string.Empty,
                "example.com",
                string.Empty,
                0,
                3,
                2,
                1);

            Assert.True(state.CanClear);
        }

        [Fact]
        public void WindowWiresCentralStateEvaluationIntoLifecycle()
        {
            string source = File.ReadAllText(
                RepositoryPath(
                    "MultiPingMonitor",
                    "UI",
                    "LivePingMonitorWindow.xaml.cs"));

            Assert.Contains(
                "ManualTargetBox.TextChanged += ManualTargetBox_TextChanged;",
                source);
            Assert.Contains(
                "private void UpdateActionStates()",
                source);
            Assert.Contains(
                "StartPingButton.IsEnabled = state.CanStart;",
                source);
            Assert.Contains(
                "CopyTargetButton.IsEnabled = state.CanCopyTarget;",
                source);
            Assert.Contains(
                "CopyAddressButton.IsEnabled = state.CanCopyAddress;",
                source);
            Assert.Contains(
                "StopResumeButton.IsEnabled = state.CanPauseResume;",
                source);
            Assert.Contains(
                "ClearButton.IsEnabled = state.CanClear;",
                source);
            Assert.Contains(
                "private void ManualTargetBox_TextChanged(",
                source);
            Assert.Contains(
                "_probe.StartStop();\n            UpdateActionStates();",
                source);
            Assert.Contains(
                "ResetSessionCounters();\n"
                + "            UpdateSessionStatisticsDisplay();\n"
                + "            UpdateActionStates();",
                source);
        }

        private static string RepositoryPath(
            params string[] parts)
        {
            DirectoryInfo? directory =
                new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "MultiPingMonitor.sln")))
                {
                    return Path.Combine(
                        new[] { directory.FullName }
                            .Concat(parts)
                            .ToArray());
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "MultiPingMonitor repository root was not found.");
        }
    }
}
