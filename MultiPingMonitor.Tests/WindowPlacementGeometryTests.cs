using System.Collections.Generic;
using MultiPingMonitor.Classes;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class WindowPlacementGeometryTests
    {
        [Fact]
        public void SelectTargetMonitor_PrefersExactMachineMonitor()
        {
            IReadOnlyList<MonitorGeometry> monitors =
                CreateMonitors();

            int result =
                WindowPlacementGeometry.SelectTargetMonitor(
                    monitors,
                    @"\\.\DISPLAY2",
                    new LogicalRect(100, 100, 800, 600));

            Assert.Equal(1, result);
        }

        [Fact]
        public void SelectTargetMonitor_UsesIntersectionWhenMonitorIsGone()
        {
            IReadOnlyList<MonitorGeometry> monitors =
                CreateMonitors();

            int result =
                WindowPlacementGeometry.SelectTargetMonitor(
                    monitors,
                    @"\\.\REMOVED",
                    new LogicalRect(1800, 100, 900, 600));

            Assert.Equal(2, result);
        }

        [Fact]
        public void SelectTargetMonitor_FallsBackToPrimaryWhenOffScreen()
        {
            IReadOnlyList<MonitorGeometry> monitors =
                CreateMonitors();

            int result =
                WindowPlacementGeometry.SelectTargetMonitor(
                    monitors,
                    @"\\.\REMOVED",
                    new LogicalRect(9000, 9000, 800, 600));

            Assert.Equal(0, result);
        }

        [Fact]
        public void RemapToWorkArea_PreservesRightEdgeAnchor()
        {
            LogicalRect result =
                WindowPlacementGeometry.RemapToWorkArea(
                    new LogicalRect(1120, 120, 800, 600),
                    new LogicalRect(0, 0, 1920, 1040),
                    new LogicalRect(0, 0, 2560, 1400));

            Assert.Equal(1760, result.Left);
            Assert.Equal(800, result.Width);
        }

        [Fact]
        public void RemapToWorkArea_PreservesRightGap()
        {
            LogicalRect result =
                WindowPlacementGeometry.RemapToWorkArea(
                    new LogicalRect(1108, 120, 800, 600),
                    new LogicalRect(0, 0, 1920, 1040),
                    new LogicalRect(0, 0, 2560, 1400));

            Assert.Equal(1748, result.Left);
            Assert.Equal(12, 2560 - result.Right);
        }

        [Fact]
        public void RemapToWorkArea_PreservesRelativePosition()
        {
            LogicalRect result =
                WindowPlacementGeometry.RemapToWorkArea(
                    new LogicalRect(560, 220, 800, 600),
                    new LogicalRect(0, 0, 1920, 1040),
                    new LogicalRect(0, 0, 2560, 1400));

            Assert.Equal(880, result.Left);
            Assert.Equal(400, result.Top);
        }

        [Fact]
        public void RemapToWorkArea_ClampsOversizedWindow()
        {
            LogicalRect result =
                WindowPlacementGeometry.RemapToWorkArea(
                    new LogicalRect(-500, -400, 4000, 3000),
                    new LogicalRect(0, 0, 1920, 1040),
                    new LogicalRect(0, 0, 1366, 728));

            Assert.Equal(0, result.Left);
            Assert.Equal(0, result.Top);
            Assert.Equal(1366, result.Width);
            Assert.Equal(728, result.Height);
        }

        [Fact]
        public void RemapToWorkArea_AlwaysReturnsVisibleWindow()
        {
            LogicalRect result =
                WindowPlacementGeometry.RemapToWorkArea(
                    new LogicalRect(9000, 9000, 800, 600),
                    default,
                    new LogicalRect(0, 0, 1920, 1040));

            Assert.InRange(result.Left, 0, 1120);
            Assert.InRange(result.Top, 0, 440);
            Assert.Equal(800, result.Width);
            Assert.Equal(600, result.Height);
        }

        private static IReadOnlyList<MonitorGeometry>
            CreateMonitors()
        {
            return new[]
            {
                new MonitorGeometry(
                    @"\\.\DISPLAY1",
                    new LogicalRect(0, 0, 1920, 1040),
                    true),

                new MonitorGeometry(
                    @"\\.\DISPLAY2",
                    new LogicalRect(-1920, 0, 1920, 1040),
                    false),

                new MonitorGeometry(
                    @"\\.\DISPLAY3",
                    new LogicalRect(1920, 0, 2560, 1400),
                    false)
            };
        }
    }
}
