using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class OptionsWindowShellTests
    {
        private static readonly XNamespace Presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        private static string RepositoryFile(params string[] relativeSegments)
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(
                    new[] { directory.FullName }
                        .Concat(relativeSegments)
                        .ToArray());

                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "Could not locate repository file.",
                Path.Combine(relativeSegments));
        }

        private static XDocument Load(params string[] relativeSegments)
        {
            return XDocument.Load(RepositoryFile(relativeSegments));
        }

        private static string? Attribute(XElement element, string localName)
        {
            return element.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == localName)
                ?.Value;
        }

        [Fact]
        public void OptionsWindowUsesEightIconNavigationPages()
        {
            XDocument document = Load(
                "MultiPingMonitor",
                "UI",
                "OptionsWindow.xaml");

            XElement window = Assert.IsType<XElement>(document.Root);
            Assert.Equal("940", Attribute(window, "Width"));
            Assert.Equal("840", Attribute(window, "MinWidth"));
            Assert.Equal("640", Attribute(window, "Height"));
            Assert.Equal("560", Attribute(window, "MinHeight"));

            XElement tabControl = document
                .Descendants(Presentation + "TabControl")
                .Single(element =>
                    Attribute(element, "Name") == "SettingsTabControl");

            Assert.Equal(
                "{DynamicResource Style.SettingsTabControl}",
                Attribute(tabControl, "Style"));

            List<XElement> tabs = tabControl
                .Elements(Presentation + "TabItem")
                .ToList();

            Assert.Equal(8, tabs.Count);

            var expectedIcons = new Dictionary<string, string>
            {
                ["GeneralTab"] = "{StaticResource geom.menu.options}",
                ["AdvancedTab"] = "{StaticResource geom.menu.traceroute}",
                ["PopupAlertsTab"] = "{StaticResource geom.settings.notifications}",
                ["EmailAlertsTab"] = "{StaticResource geom.settings.email}",
                ["AudioAlertTab"] = "{StaticResource geom.settings.sounds}",
                ["LogOutputTab"] = "{StaticResource geom.settings.logging}",
                ["DisplayTab"] = "{StaticResource geom.menu.toggle-display}",
                ["LayoutTab"] = "{StaticResource geom.menu.columns-grid}",
            };

            foreach (XElement tab in tabs)
            {
                string name = Assert.IsType<string>(Attribute(tab, "Name"));

                Assert.True(
                    expectedIcons.ContainsKey(name),
                    $"Unexpected Settings tab: {name}");

                Assert.Equal(
                    "{DynamicResource Style.SettingsTabItem}",
                    Attribute(tab, "Style"));

                Assert.Equal(expectedIcons[name], Attribute(tab, "Tag"));

                XElement pageBorder = tab
                    .Elements(Presentation + "Border")
                    .Single();

                Assert.Equal(
                    "{DynamicResource Style.SettingsPagePanel}",
                    Attribute(pageBorder, "Style"));

                XElement scrollViewer = pageBorder
                    .Descendants(Presentation + "ScrollViewer")
                    .First();

                Assert.Equal(
                    "Auto",
                    Attribute(scrollViewer, "VerticalScrollBarVisibility"));

                Assert.Equal(
                    "Disabled",
                    Attribute(scrollViewer, "HorizontalScrollBarVisibility"));

                Assert.Equal(
                    "False",
                    Attribute(scrollViewer, "CanContentScroll"));

                Assert.Equal(
                    "VerticalOnly",
                    Attribute(scrollViewer, "PanningMode"));
            }
        }

        [Fact]
        public void SettingsFooterRemainsFixedAndPreservesButtonSemantics()
        {
            XDocument document = Load(
                "MultiPingMonitor",
                "UI",
                "OptionsWindow.xaml");

            XElement footer = document
                .Descendants(Presentation + "Border")
                .Single(element =>
                    Attribute(element, "Style")
                    == "{DynamicResource Style.SettingsFooterPanel}");

            Assert.Equal("1", Attribute(footer, "Grid.Row"));

            Assert.DoesNotContain(
                footer.Ancestors(),
                ancestor => ancestor.Name == Presentation + "TabControl");

            XElement apply = document
                .Descendants(Presentation + "Button")
                .Single(element => Attribute(element, "Name") == "ApplyButton");

            XElement save = document
                .Descendants(Presentation + "Button")
                .Single(element => Attribute(element, "Name") == "SaveButton");

            XElement cancel = document
                .Descendants(Presentation + "Button")
                .Single(element => Attribute(element, "Name") == "CancelButton");

            Assert.Equal("Apply_Click", Attribute(apply, "Click"));
            Assert.Equal("Save_Click", Attribute(save, "Click"));
            Assert.Equal("True", Attribute(save, "IsDefault"));
            Assert.Equal("True", Attribute(cancel, "IsCancel"));

            Assert.Equal(
                "{DynamicResource Theme.Accent}",
                Attribute(save, "Background"));

            Assert.Equal(
                "{DynamicResource Theme.Accent}",
                Attribute(save, "BorderBrush"));
        }

        [Fact]
        public void FriendlySettingsLayoutUsesThemeAwareCards()
        {
            XDocument document = Load(
                "MultiPingMonitor",
                "UI",
                "OptionsWindow.xaml");

            int cardCount = document
                .Descendants(Presentation + "Border")
                .Count(element =>
                    Attribute(element, "Style")
                    == "{DynamicResource Style.SettingsCard}");

            Assert.Equal(19, cardCount);

            string xaml = File.ReadAllText(
                RepositoryFile(
                    "MultiPingMonitor",
                    "UI",
                    "OptionsWindow.xaml"));

            Assert.False(
                xaml.Contains(
                    "Background=\"White\"",
                    StringComparison.OrdinalIgnoreCase));

            Assert.False(
                xaml.Contains(
                    "Background=\"#FFFFFF\"",
                    StringComparison.OrdinalIgnoreCase));
        }



        [Fact]
        public void FriendlySettingsPolishUsesResponsiveLabelsAndLocalFocus()
        {
            XDocument options = Load(
                "MultiPingMonitor",
                "UI",
                "OptionsWindow.xaml");

            XElement settingsTabControl = options
                .Descendants(Presentation + "TabControl")
                .Single(element =>
                    Attribute(element, "Name") == "SettingsTabControl");

            foreach (XElement tab in settingsTabControl
                .Elements(Presentation + "TabItem"))
            {
                XElement pageScrollViewer = tab
                    .Elements(Presentation + "Border")
                    .Single()
                    .Elements(Presentation + "ScrollViewer")
                    .Single();

                Assert.Equal("False", Attribute(pageScrollViewer, "Focusable"));
                Assert.Equal(
                    "{x:Null}",
                    Attribute(pageScrollViewer, "FocusVisualStyle"));
            }

            foreach (string name in new[]
            {
                "IsAudioDownAlertEnabled",
                "IsAudioUpAlertEnabled",
                "IsAudioNetworkIdentityAlertEnabled",
                "IsLogOutputEnabled",
                "IsLogStatusChangesEnabled",
                "IsAlwaysOnTopEnabled",
                "IsMinimizeToTrayEnabled",
                "IsExitToTrayEnabled",
                "StartInTray",
                "RememberWindowPosition",
            })
            {
                XElement checkBox = options
                    .Descendants(Presentation + "CheckBox")
                    .Single(element => Attribute(element, "Name") == name);

                Assert.Null(Attribute(checkBox, "Content"));
                Assert.NotNull(Attribute(checkBox, "AutomationProperties.Name"));
                Assert.Equal("Grid", checkBox.Parent!.Name.LocalName);

                XElement content = checkBox
                    .Elements(Presentation + "TextBlock")
                    .Single();

                Assert.Equal("Wrap", Attribute(content, "TextWrapping"));
                Assert.Equal(
                    "Stretch",
                    Attribute(checkBox, "HorizontalContentAlignment"));
            }

            XElement ssl = options
                .Descendants(Presentation + "CheckBox")
                .Single(element =>
                    Attribute(element, "Name") == "IsSmtpSslEnabled");

            Assert.Equal("2", Attribute(ssl, "Grid.Row"));
            Assert.Equal(
                "{x:Static resource:Strings.Options_EnableSSL}",
                Attribute(ssl, "Content"));

            XElement compactButton = options
                .Descendants(Presentation + "Button")
                .Single(element =>
                    Attribute(element, "Name")
                    == "ManageCompactTargetsButton");

            Assert.Equal("220", Attribute(compactButton, "MinWidth"));
            Assert.NotNull(Attribute(compactButton, "Content"));
            Assert.Empty(compactButton.Elements(Presentation + "TextBlock"));

            XElement pingInterval = options
                .Descendants(Presentation + "TextBox")
                .Single(element => Attribute(element, "Name") == "PingInterval");

            Assert.Equal("StackPanel", pingInterval.Parent!.Name.LocalName);
            Assert.Equal("StackPanel", pingInterval.Parent!.Parent!.Name.LocalName);
            Assert.Equal("0", Attribute(pingInterval.Parent!.Parent!, "Grid.Column"));

            XElement packetSizeOption = options
                .Descendants(Presentation + "RadioButton")
                .Single(element => Attribute(element, "Name") == "PacketSizeOption");

            XElement packetSize = options
                .Descendants(Presentation + "TextBox")
                .Single(element => Attribute(element, "Name") == "PacketSize");

            Assert.Equal("0", Attribute(packetSizeOption, "Grid.Row"));
            Assert.Equal("1", Attribute(packetSize.Parent!, "Grid.Row"));

            XElement language = options
                .Descendants(Presentation + "ComboBox")
                .Single(element => Attribute(element, "Name") == "LanguageComboBox");

            Assert.Equal("3", Attribute(language, "Grid.Row"));
            Assert.Equal("2", Attribute(language, "Grid.ColumnSpan"));

            Assert.Equal("2", Attribute(compactButton, "Grid.Row"));
            Assert.Equal("1", Attribute(compactButton, "Grid.Column"));

            string slovakResources = File.ReadAllText(
                RepositoryFile(
                    "MultiPingMonitor",
                    "Properties",
                    "Strings.sk-SK.resx"));

            string seeds = File.ReadAllText(
                RepositoryFile(
                    "MultiPingMonitor",
                    "Classes",
                    "LanguagePackSeeds.cs"));

            Assert.Contains(
                "<value>Prehľadať</value>",
                slovakResources,
                StringComparison.Ordinal);

            Assert.Contains(
                "\"Options_Browse\", \"Browse...\", \"Prehľadať\"",
                seeds,
                StringComparison.Ordinal);
        }

        [Fact]
        public void SettingsShellStylesAndIconsExist()
        {
            XDocument styles = Load(
                "MultiPingMonitor",
                "ResourceDictionaries",
                "TabControlStyle.xaml");

            HashSet<string> styleKeys = styles
                .Descendants()
                .Select(element => Attribute(element, "Key"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("Style.SettingsTabControl", styleKeys);
            Assert.Contains("Style.SettingsTabItem", styleKeys);
            Assert.Contains("Style.SettingsPagePanel", styleKeys);
            Assert.Contains("Style.SettingsFooterPanel", styleKeys);
            Assert.Contains("Style.SettingsCard", styleKeys);
            Assert.Contains("Style.SettingsCardTitle", styleKeys);
            Assert.Contains("Style.SettingsCardHint", styleKeys);

            XDocument icons = Load(
                "MultiPingMonitor",
                "ResourceDictionaries",
                "Icons.xaml");

            HashSet<string> iconKeys = icons
                .Descendants()
                .Select(element => Attribute(element, "Key"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("geom.settings.notifications", iconKeys);
            Assert.Contains("geom.settings.email", iconKeys);
            Assert.Contains("geom.settings.sounds", iconKeys);
            Assert.Contains("geom.settings.logging", iconKeys);
        }
    }
}
