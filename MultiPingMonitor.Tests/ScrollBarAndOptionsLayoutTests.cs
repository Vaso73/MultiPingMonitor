using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class ScrollBarAndOptionsLayoutTests
    {
        [Fact]
        public void ScrollBarStylesUseSharedApplicationTemplate()
        {
            XDocument document = XDocument.Load(
                RepositoryPath(
                    "MultiPingMonitor",
                    "ResourceDictionaries",
                    "ScrollBarStyle.xaml"));

            XNamespace xaml =
                "http://schemas.microsoft.com/winfx/2006/xaml";

            XElement appStyle = document
                .Descendants()
                .Single(
                    element =>
                        element.Name.LocalName == "Style"
                        && string.Equals(
                            (string?)element.Attribute(
                                xaml + "Key"),
                            "ScrollBarStyle.App",
                            StringComparison.Ordinal));

            Assert.Equal(
                "{x:Type ScrollBar}",
                (string?)appStyle.Attribute("TargetType"));

            XElement implicitStyle = document
                .Root!
                .Elements()
                .Single(
                    element =>
                        element.Name.LocalName == "Style"
                        && element.Attribute(xaml + "Key") == null
                        && string.Equals(
                            (string?)element.Attribute("TargetType"),
                            "{x:Type ScrollBar}",
                            StringComparison.Ordinal));

            Assert.Equal(
                "{StaticResource ScrollBarStyle.App}",
                (string?)implicitStyle.Attribute("BasedOn"));

            foreach (string key in new[]
            {
                "ScrollBarStyle.Dark",
                "ScrollBarStyle.Probe",
                "ScrollBarStyle.Compact",
            })
            {
                XElement style = document
                    .Descendants()
                    .Single(
                        element =>
                            element.Name.LocalName == "Style"
                            && string.Equals(
                                (string?)element.Attribute(
                                    xaml + "Key"),
                                key,
                                StringComparison.Ordinal));

                Assert.Equal(
                    "{StaticResource ScrollBarStyle.App}",
                    (string?)style.Attribute("BasedOn"));
            }

            string source = File.ReadAllText(
                RepositoryPath(
                    "MultiPingMonitor",
                    "ResourceDictionaries",
                    "ScrollBarStyle.xaml"));

            Assert.Contains(
                "{DynamicResource Theme.Border}",
                source,
                StringComparison.Ordinal);

            Assert.Contains(
                "{DynamicResource Theme.Accent}",
                source,
                StringComparison.Ordinal);
        }

        [Fact]
        public void ScrollBarTemplatesSupportBothOrientationsWithoutLineButtons()
        {
            XDocument document = XDocument.Load(
                RepositoryPath(
                    "MultiPingMonitor",
                    "ResourceDictionaries",
                    "ScrollBarStyle.xaml"));

            XNamespace xaml =
                "http://schemas.microsoft.com/winfx/2006/xaml";

            foreach (string key in new[]
            {
                "ScrollBarTemplate.App.Vertical",
                "ScrollBarTemplate.App.Horizontal",
            })
            {
                Assert.Single(
                    document
                        .Descendants()
                        .Where(
                            element =>
                                element.Name.LocalName
                                    == "ControlTemplate"
                                && string.Equals(
                                    (string?)element.Attribute(
                                        xaml + "Key"),
                                    key,
                                    StringComparison.Ordinal)));
            }

            IReadOnlyList<string> commands = document
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName == "RepeatButton")
                .Select(
                    element =>
                        (string?)element.Attribute("Command")
                        ?? string.Empty)
                .ToArray();

            Assert.DoesNotContain(
                commands,
                command =>
                    command.Contains(
                        "LineUp",
                        StringComparison.Ordinal)
                    || command.Contains(
                        "LineDown",
                        StringComparison.Ordinal)
                    || command.Contains(
                        "LineLeft",
                        StringComparison.Ordinal)
                    || command.Contains(
                        "LineRight",
                        StringComparison.Ordinal));
        }

        [Fact]
        public void EveryOptionsTabScrollsAboveTheFixedFooter()
        {
            XDocument document = XDocument.Load(
                RepositoryPath(
                    "MultiPingMonitor",
                    "UI",
                    "OptionsWindow.xaml"));

            XNamespace xaml =
                "http://schemas.microsoft.com/winfx/2006/xaml";
            XNamespace presentation =
                "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

            string[] tabNames =
            {
                "GeneralTab",
                "AdvancedTab",
                "PopupAlertsTab",
                "EmailAlertsTab",
                "AudioAlertTab",
                "LogOutputTab",
                "DisplayTab",
                "LayoutTab",
            };

            foreach (string tabName in tabNames)
            {
                XElement tab = document
                    .Descendants(presentation + "TabItem")
                    .Single(
                        element =>
                            string.Equals(
                                ((string?)element.Attribute(
                                    xaml + "Name")
                                ?? (string?)element.Attribute("Name")),
                                tabName,
                                StringComparison.Ordinal));

                XElement border = tab
                    .Elements()
                    .Single(
                        element =>
                            element.Name.LocalName == "Border");

                XElement scrollViewer = border
                    .Elements()
                    .Single(
                        element =>
                            element.Name.LocalName
                                == "ScrollViewer");

                Assert.Equal(
                    "Auto",
                    (string?)scrollViewer.Attribute(
                        "VerticalScrollBarVisibility"));

                Assert.Equal(
                    "Disabled",
                    (string?)scrollViewer.Attribute(
                        "HorizontalScrollBarVisibility"));

                Assert.Equal(
                    "False",
                    (string?)scrollViewer.Attribute(
                        "CanContentScroll"));

                Assert.Equal(
                    "VerticalOnly",
                    (string?)scrollViewer.Attribute(
                        "PanningMode"));
            }

            XElement tabControl = document
                .Descendants(presentation + "TabControl")
                .Single();

            XElement footer = document
                .Descendants(presentation + "Border")
                .Single(
                    element =>
                        string.Equals(
                            (string?)element.Attribute("Style"),
                            "{DynamicResource Style.FooterPanel}",
                            StringComparison.Ordinal));

            Assert.Equal(
                "1",
                (string?)footer.Attribute("Grid.Row"));

            Assert.DoesNotContain(
                footer.Ancestors(),
                ancestor =>
                    ReferenceEquals(
                        ancestor,
                        tabControl));
        }

        [Fact]
        public void LocalAndDataGridScrollbarsUseUnifiedDimensions()
        {
            XDocument isolated = XDocument.Load(
                RepositoryPath(
                    "MultiPingMonitor",
                    "UI",
                    "IsolatedPingWindow.xaml"));

            XNamespace xaml =
                "http://schemas.microsoft.com/winfx/2006/xaml";

            XElement localStyle = isolated
                .Descendants()
                .Single(
                    element =>
                        element.Name.LocalName == "Style"
                        && element.Attribute(xaml + "Key") == null
                        && string.Equals(
                            (string?)element.Attribute("TargetType"),
                            "{x:Type ScrollBar}",
                            StringComparison.Ordinal));

            Assert.Equal(
                "{StaticResource ScrollBarStyle.App}",
                (string?)localStyle.Attribute("BasedOn"));

            string dataGrid = File.ReadAllText(
                RepositoryPath(
                    "MultiPingMonitor",
                    "ResourceDictionaries",
                    "DataGridStyle.xaml"));

            Assert.DoesNotContain(
                "Margin=\"-17,0,0,0\"",
                dataGrid,
                StringComparison.Ordinal);

            Assert.Contains(
                "Margin=\"-10,0,0,0\"",
                dataGrid,
                StringComparison.Ordinal);
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
