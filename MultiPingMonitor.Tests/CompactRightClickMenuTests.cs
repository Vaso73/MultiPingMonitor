using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class CompactRightClickMenuTests
    {
        [Fact]
        public void MainWindow_RegistersCompactRightClickHandler()
        {
            var xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml"));

            Assert.Contains("PreviewMouseRightButtonUp=\"CompactWindow_PreviewMouseRightButtonUp\"", xaml);
        }

        [Fact]
        public void CompactRightClickMenu_ReusesAddHostActionAndVisualStyle()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("CompactWindow_PreviewMouseRightButtonUp", source);
            Assert.Contains("CreateCompactRightClickMenu", source);
            Assert.Contains("CreateThemedCompactContextMenu", source);
            Assert.Contains("FindProbeFromRightClickSource(e.OriginalSource)", source);
            Assert.Contains("Strings.Compact_AddHost", source);
            Assert.Contains("geom.menu.add", source);
            Assert.Contains("CompactAddHostButton_Click(null, null)", source);
            Assert.Contains("PlacementMode.MousePoint", source);
            Assert.Contains("Style.ContextMenu", source);
            Assert.Contains("MenuItemStyle", source);
            Assert.Contains("CanAddCompactHostToActiveSet", source);
        }

        [Fact]
        public void CompactMenuButton_ReusesSameThemedContextMenuFactory()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("private ContextMenu CreateThemedCompactContextMenu()", source);
            Assert.Contains("private void CompactMenuButton_Click(object sender, RoutedEventArgs e)\n        {\n            var menu = CreateThemedCompactContextMenu();", source);
        }


        [Fact]
        public void CompactRightClickMenu_OnSelectedHostOffersRemoveHostFromSet()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("private ContextMenu CreateCompactRightClickMenu(Probe selectedProbe)", source);
            Assert.Contains("if (selectedProbe != null)", source);
            Assert.Contains("Strings.Compact_RemoveHost", source);
            Assert.Contains("geom.menu.remove", source);
            Assert.Contains("CanRemoveCompactHostFromActiveSet(selectedProbe)", source);
            Assert.Contains("RemoveCompactHostFromActiveSet(selectedProbe)", source);
        }

        [Fact]
        public void RemoveSelectedCompactHost_RemovesFromActiveSetAndCompactProbeCollection()
        {
            var source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            Assert.Contains("private void RemoveCompactHostFromActiveSet(Probe probe)", source);
            Assert.Contains("_CompactProbeCollection.Remove(probe)", source);
            Assert.Contains("activeSet.Entries.Remove(entry)", source);
            Assert.Contains("Configuration.Save()", source);
            Assert.Contains("StatusHistory_CompactSet_HostRemoved", source);
            Assert.Contains("UpdateCompactStartStopButton()", source);
            Assert.Contains("UpdateTrayIcon()", source);
        }

        private static string SourcePath(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MultiPingMonitor.sln")))
                dir = dir.Parent;

            if (dir == null)
                throw new DirectoryNotFoundException("MultiPingMonitor solution root was not found.");

            var pathParts = new string[parts.Length + 1];
            pathParts[0] = dir.FullName;
            Array.Copy(parts, 0, pathParts, 1, parts.Length);
            return Path.Combine(pathParts);
        }
        [Fact]
        public void CompactRightClickMenu_IncludesSharedCompactAndAppActions()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

            int helperIdx = source.IndexOf(
                "private void AppendCompactRightClickAppActions(ContextMenu menu)");
            Assert.True(helperIdx >= 0, "AppendCompactRightClickAppActions not found");

            int menuIdx = source.IndexOf(
                "private ContextMenu CreateCompactRightClickMenu(Probe selectedProbe)",
                helperIdx);
            Assert.True(menuIdx > helperIdx, "CreateCompactRightClickMenu not found after helper");

            string helperBody = source.Substring(helperIdx, menuIdx - helperIdx);

            Assert.Contains("Strings.Compact_StopSet", helperBody);
            Assert.Contains("Strings.Compact_StartSet", helperBody);
            Assert.Contains("StartStopCompactSet()", helperBody);

            Assert.Contains("Strings.Options_CompactSource_NormalTargets", helperBody);
            Assert.Contains("SetCompactSource(ApplicationOptions.CompactSourceMode.NormalTargets)", helperBody);
            Assert.Contains("Strings.Options_CompactSource_CustomTargets", helperBody);
            Assert.Contains("SetCompactSource(ApplicationOptions.CompactSourceMode.CustomTargets)", helperBody);
            Assert.Contains("AppendCompactSetMenuItems(menu.Items)", helperBody);

            Assert.Contains("Strings.Menu_CompactManageSets", helperBody);
            Assert.Contains("OpenManageCompactSets()", helperBody);

            Assert.Contains("Strings.LivePing_OpenAllLive", helperBody);
            Assert.Contains("OpenAllLiveWindowsAndArrange(cascade: true)", helperBody);
            Assert.Contains("OpenAllLiveWindowsAndArrange(cascade: false)", helperBody);

            Assert.Contains("Strings.Menu_StatusHistory", helperBody);
            Assert.Contains("StatusHistoryExecute(null, null)", helperBody);

            Assert.Contains("Strings.Menu_Options", helperBody);
            Assert.Contains("OptionsExecute(null, null)", helperBody);

            Assert.Contains("Strings.Menu_About", helperBody);
            Assert.Contains("AboutMenu_Click(null, null)", helperBody);

            Assert.DoesNotContain("Strings.Menu_NewLivePing", helperBody);

            string menuBody = source.Substring(menuIdx, source.IndexOf("return menu;", menuIdx) - menuIdx);

            int newLivePingIdx = menuBody.IndexOf("Strings.Menu_NewLivePing");
            int addHostIdx = menuBody.IndexOf("Strings.Compact_AddHost");
            Assert.True(newLivePingIdx >= 0, "Menu_NewLivePing not found in right-click menu");
            Assert.True(addHostIdx > newLivePingIdx, "New Live Ping must be first, before Add host");

            Assert.Contains("NewLivePingMenu_Click(null, null)", menuBody);
            Assert.Contains("CompactAddHostButton_Click(null, null)", menuBody);
            Assert.Contains("RemoveCompactHostFromActiveSet(selectedProbe)", menuBody);
            Assert.Contains("AppendCompactRightClickAppActions(menu)", menuBody);
        }


    }
}
