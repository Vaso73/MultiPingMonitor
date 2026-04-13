using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MultiPingMonitor.Tests
{
    /// <summary>
    /// Regression tests for menu localization and tray-menu construction invariants.
    ///
    /// These tests guard against the recent regressions that were fixed:
    ///   • Slovak strings overriding updated default strings (Tray_VisualStyle was
    ///     hardcoded "Visual style" in code instead of using Strings.Tray_VisualStyle).
    ///   • Dialog-opening commands missing their ellipsis (…) suffix.
    ///   • Tray menu ordering: "Nový live ping…" must remain the first actionable item.
    ///
    /// Tests are intentionally kept free of WPF/WinForms dependencies so they run
    /// on Linux CI as well as Windows. Resource string values are read directly from
    /// the .resx XML source files; tray-menu construction invariants are verified via
    /// lightweight source-code inspection.
    /// </summary>
    public class MenuLocalizationTests
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

        private static string DefaultResxPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Properties", "Strings.resx");

        private static string SkSkResxPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Properties", "Strings.sk-SK.resx");

        private static string MainWindowSourcePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "MainWindow.xaml.cs");

        /// <summary>
        /// Returns the value for the given resource key from the specified .resx file.
        /// </summary>
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

        // ── Tray_VisualStyle localization ──────────────────────────────────────

        [Fact]
        public void TrayVisualStyle_Default_ValueIsVisualStyle()
        {
            // Neutral/en resource must carry the canonical English label.
            Assert.Equal("Visual style", ResxValue(DefaultResxPath(), "Tray_VisualStyle"));
        }

        [Fact]
        public void TrayVisualStyle_SkSk_IsVizualnyStyl()
        {
            // Slovak translation must be the correct localized form.
            Assert.Equal("Vizuálny štýl", ResxValue(SkSkResxPath(), "Tray_VisualStyle"));
        }

        [Fact]
        public void TrayVisualStyle_SkSk_IsNotHardcodedEnglish()
        {
            // Guard: if this were the hardcoded English literal it means the fix was reverted.
            Assert.NotEqual("Visual style", ResxValue(SkSkResxPath(), "Tray_VisualStyle"));
        }

        // ── Menu_NewLivePing localization ──────────────────────────────────────

        [Fact]
        public void MenuNewLivePing_Default_EndsWithEllipsis()
        {
            Assert.EndsWith("...", ResxValue(DefaultResxPath(), "Menu_NewLivePing"));
        }

        [Fact]
        public void MenuNewLivePing_SkSk_EndsWithEllipsis()
        {
            Assert.EndsWith("...", ResxValue(SkSkResxPath(), "Menu_NewLivePing"));
        }

        [Fact]
        public void MenuNewLivePing_SkSk_StartsWithNovy()
        {
            // Slovak entry must begin with "Nový", not revert to English "New".
            Assert.StartsWith("Nový", ResxValue(SkSkResxPath(), "Menu_NewLivePing"));
        }

        [Fact]
        public void MenuNewLivePing_SkSk_IsNotEnglishFallback()
        {
            // "New Live Ping..." would indicate a missing/reverted Slovak translation.
            Assert.DoesNotContain("New Live Ping", ResxValue(SkSkResxPath(), "Menu_NewLivePing"));
        }

        // ── Ellipsis invariant for dialog-opening commands ─────────────────────

        [Theory]
        [InlineData("Menu_FloodHost")]
        [InlineData("Menu_ManageAliases")]
        [InlineData("Menu_ManageFavorites")]
        [InlineData("Menu_SaveToFavorites")]
        [InlineData("Menu_Traceroute")]
        [InlineData("Menu_CompactManageTargets")]
        [InlineData("Menu_InputAddresses")]
        [InlineData("Menu_CompactManageSets")]
        [InlineData("Menu_NewLivePing")]
        public void DialogOpeningCommand_Default_EndsWithEllipsis(string key)
        {
            Assert.EndsWith("...", ResxValue(DefaultResxPath(), key));
        }

        [Theory]
        [InlineData("Menu_FloodHost")]
        [InlineData("Menu_ManageAliases")]
        [InlineData("Menu_ManageFavorites")]
        [InlineData("Menu_SaveToFavorites")]
        [InlineData("Menu_Traceroute")]
        [InlineData("Menu_CompactManageTargets")]
        [InlineData("Menu_InputAddresses")]
        [InlineData("Menu_CompactManageSets")]
        [InlineData("Menu_NewLivePing")]
        public void DialogOpeningCommand_SkSk_EndsWithEllipsis(string key)
        {
            Assert.EndsWith("...", ResxValue(SkSkResxPath(), key));
        }

        // ── Tray menu construction invariants (source-code inspection) ─────────

        [Fact]
        public void TrayMenu_FirstActionableItem_IsMenuNewLivePing()
        {
            // BuildNativeTrayMenu() must add Menu_NewLivePing as its first real item.
            // This prevents "Nový live ping..." from accidentally being pushed down.
            var source = File.ReadAllText(MainWindowSourcePath());

            int buildStart = source.IndexOf("private System.Windows.Forms.ContextMenuStrip BuildNativeTrayMenu()",
                StringComparison.Ordinal);
            Assert.True(buildStart >= 0, "BuildNativeTrayMenu method not found in MainWindow.xaml.cs");

            int firstAdd = source.IndexOf("menu.Items.Add(MakeItem(", buildStart, StringComparison.Ordinal);
            Assert.True(firstAdd >= 0, "No menu.Items.Add(MakeItem( call found inside BuildNativeTrayMenu");

            // The first MakeItem call must reference the Menu_NewLivePing string key.
            string callSnippet = source.Substring(firstAdd, Math.Min(160, source.Length - firstAdd));
            Assert.Contains("Menu_NewLivePing", callSnippet);
        }

        [Fact]
        public void TrayMenu_VisualStyleParent_UsesStringsResourceNotLiteral()
        {
            // The visual-style submenu parent label must be sourced from
            // Strings.Tray_VisualStyle so that it is localized, not hardcoded.
            var source = File.ReadAllText(MainWindowSourcePath());

            // Positive: the resource accessor must be present in the file.
            Assert.Contains("Strings.Tray_VisualStyle", source);

            // Negative: a hardcoded English literal as a ToolStripMenuItem argument
            // would bypass localization and is the exact regression that was fixed.
            Assert.DoesNotContain(
                "new System.Windows.Forms.ToolStripMenuItem(\"Visual style\")",
                source);
        }
    }
}
