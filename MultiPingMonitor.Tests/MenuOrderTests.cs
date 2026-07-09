using System;
using System.IO;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public class MenuOrderTests
    {
        private static string SolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MultiPingMonitor.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
                throw new DirectoryNotFoundException("Could not locate solution root.");

            return dir.FullName;
        }

        private static string SourcePath(params string[] parts)
        {
            string[] pathParts = new string[parts.Length + 1];
            pathParts[0] = SolutionRoot();
            Array.Copy(parts, 0, pathParts, 1, parts.Length);
            return Path.Combine(pathParts);
        }

        private static string MethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Method not found: {signature}");

            int brace = source.IndexOf('{', start);
            Assert.True(brace >= 0, $"Method brace not found: {signature}");

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            throw new InvalidOperationException($"Method end not found: {signature}");
        }

        private static void AssertOrder(string source, params string[] markers)
        {
            int previous = -1;
            foreach (string marker in markers)
            {
                int current = source.IndexOf(marker, previous + 1, StringComparison.Ordinal);
                Assert.True(current >= 0, $"Marker not found after previous marker: {marker}");
                previous = current;
            }
        }

        [Fact]
        public void MainHamburgerMenu_UsesTaskFirstAndSupportLastOrder()
        {
            string xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml"));

            AssertOrder(
                xaml,
                "x:Name=\"NewLivePingMenu\"",
                "Name=\"MultiInputMenu\"",
                "x:Name=\"NewInstanceMenu\"",
                "x:Name=\"CompactTargetsMenu\"",
                "Name=\"StatusHistoryMenu\"",
                "Strings.Menu_PopupAlerts",
                "x:Name=\"FavoritesMenu\"",
                "x:Name=\"AliasesMenu\"",
                "x:Name=\"TracerouteMenu\"",
                "x:Name=\"FloodHostMenu\"",
                "Name=\"OptionsMenu\"",
                "x:Name=\"ToggleDisplayModeMenu\"",
                "x:Name=\"HelpMenu\"",
                "Strings.Menu_About");
        }

        [Fact]
        public void CompactMenuButton_UsesGlobalCompactOrderWithoutHostSpecificRemove()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));
            string body = MethodBody(source, "private void CompactMenuButton_Click(object sender, RoutedEventArgs e)");

            Assert.DoesNotContain("Strings.Compact_RemoveHost", body);

            AssertOrder(
                body,
                "Strings.Menu_NewLivePing",
                "Strings.Compact_AddHost",
                "Strings.Compact_StopSet",
                "Strings.Options_CompactSource_NormalTargets",
                "Strings.Options_CompactSource_CustomTargets",
                "AppendCompactSetMenuItems(menu.Items)",
                "Strings.Menu_CompactManageSets",
                "Strings.LivePing_OpenAllLive",
                "Strings.Menu_StatusHistory",
                "Strings.Menu_Options",
                "CreateCompactToggleDisplayModeMenuItem()",
                "Strings.Menu_About");
        }

        [Fact]
        public void CompactRightClickMenu_KeepsHostSpecificRemoveAndSupportOrder()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));
            string menuBody = MethodBody(source, "private ContextMenu CreateCompactRightClickMenu(Probe selectedProbe)");
            string helperBody = MethodBody(source, "private void AppendCompactRightClickAppActions(ContextMenu menu)");

            AssertOrder(
                menuBody,
                "Strings.Menu_NewLivePing",
                "Strings.Compact_AddHost",
                "Strings.Compact_RemoveHost",
                "AppendCompactRightClickAppActions(menu)");

            AssertOrder(
                helperBody,
                "Strings.Compact_StopSet",
                "Strings.Options_CompactSource_NormalTargets",
                "Strings.Options_CompactSource_CustomTargets",
                "AppendCompactSetMenuItems(menu.Items)",
                "Strings.Menu_CompactManageSets",
                "Strings.LivePing_OpenAllLive",
                "Strings.Menu_StatusHistory",
                "Strings.Menu_Options",
                "CreateCompactToggleDisplayModeMenuItem()",
                "Strings.Menu_About");
        }

        [Fact]
        public void CompactToggleDisplayMenuItem_UsesSharedToggleDisplayIcon()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));
            string body = MethodBody(source, "private MenuItem CreateCompactToggleDisplayModeMenuItem()");

            Assert.Contains("Strings.Tray_SwitchToNormal", body);
            Assert.Contains("Strings.Tray_SwitchToCompact", body);
            Assert.Contains("geom.menu.toggle-display", body);
        }

        [Fact]
        public void TrayMenu_UsesTaskFirstSupportAndExitLastOrder()
        {
            string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));
            string body = MethodBody(source, "private System.Windows.Forms.ContextMenuStrip BuildNativeTrayMenu()");

            AssertOrder(
                body,
                "Strings.Menu_NewLivePing",
                "Strings.Tray_Open",
                "Strings.Tray_NewInstance",
                "Strings.Menu_Traceroute",
                "Strings.Menu_FloodHost",
                "Strings.Tray_Options",
                "Strings.Tray_StatusHistory",
                "menu.Items.Add(styleParent)",
                "menu.Items.Add(_trayNativeToggleItem)",
                "Strings.Menu_Help",
                "Strings.Menu_About",
                "Strings.Tray_Exit");
        }
    }
}
