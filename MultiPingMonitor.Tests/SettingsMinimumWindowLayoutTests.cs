using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class SettingsMinimumWindowLayoutTests
    {
        private static readonly XNamespace Presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        private static readonly XNamespace Xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void GeneralAndDisplayRemainCompactAtTheMinimumWindowSize()
        {
            XDocument document = XDocument.Load(
                RepositoryFile(
                    "MultiPingMonitor",
                    "UI",
                    "OptionsWindow.xaml"));

            XElement window = document.Root
                ?? throw new InvalidOperationException("OptionsWindow root is missing.");

            Assert.Equal("560", RequiredAttribute(window, "MinHeight"));
            Assert.Equal("840", RequiredAttribute(window, "MinWidth"));

            XElement general = Tab(document, "GeneralTab");
            XElement generalScroll = PageScrollViewer(general);
            Assert.Equal(
                "Auto",
                RequiredAttribute(generalScroll, "VerticalScrollBarVisibility"));

            XElement generalPageGrid = generalScroll
                .Elements(Presentation + "Grid")
                .Single();

            Assert.Equal(
                "8,0,8,4",
                RequiredAttribute(generalPageGrid, "Margin"));

            XElement alertThreshold = Named(general, "AlertThreshold");
            Assert.Equal("44", RequiredAttribute(alertThreshold, "Width"));

            XElement droppedPings = general
                .Descendants(Presentation + "TextBlock")
                .Single(element =>
                    OptionalAttribute(element, "Text")
                    == "{x:Static resource:Strings.Options_DroppedPings}");

            Assert.Equal(
                "6,0,0,0",
                RequiredAttribute(droppedPings, "Margin"));

            List<XElement> generalCards = Cards(general);
            Assert.Equal(4, generalCards.Count);
            Assert.All(
                generalCards,
                card => Assert.Equal(
                    "14,10",
                    RequiredAttribute(card, "Padding")));

            XElement display = Tab(document, "DisplayTab");
            XElement displayScroll = PageScrollViewer(display);
            Assert.Equal(
                "Auto",
                RequiredAttribute(displayScroll, "VerticalScrollBarVisibility"));

            XElement displayPageGrid = displayScroll
                .Elements(Presentation + "Grid")
                .Single();

            Assert.Equal(
                "8,0,8,4",
                RequiredAttribute(displayPageGrid, "Margin"));

            XElement displayTopGrid = displayPageGrid
                .Elements(Presentation + "Grid")
                .Single(element =>
                    OptionalAttribute(element, "Grid.Row") == "1");

            string[] widths = displayTopGrid
                .Element(Presentation + "Grid.ColumnDefinitions")!
                .Elements(Presentation + "ColumnDefinition")
                .Select(element =>
                    RequiredAttribute(element, "Width"))
                .ToArray();

            Assert.Equal(new[] { "1*", "16", "1*" }, widths);

            List<XElement> displayCards = Cards(display);
            Assert.Equal(3, displayCards.Count);
            Assert.All(
                displayCards,
                card => Assert.Equal(
                    "14,10",
                    RequiredAttribute(card, "Padding")));

            XElement compactSource = Named(
                display,
                "CompactSourceComboBox");

            XElement compactGrid = compactSource.Parent
                ?? throw new InvalidOperationException(
                    "Compact source grid is missing.");

            string firstColumnWidth = compactGrid
                .Element(Presentation + "Grid.ColumnDefinitions")!
                .Elements(Presentation + "ColumnDefinition")
                .First()
                .Attribute("Width")!
                .Value;

            Assert.Equal("190", firstColumnWidth);

            XElement manageButton = Named(
                display,
                "ManageCompactTargetsButton");

            Assert.Equal(
                "0,6,0,0",
                RequiredAttribute(manageButton, "Margin"));
        }

        private static XElement Tab(
            XDocument document,
            string name)
        {
            return document
                .Descendants(Presentation + "TabItem")
                .Single(element =>
                    OptionalAttribute(element, "Name") == name);
        }

        private static XElement PageScrollViewer(XElement tab)
        {
            return tab
                .Elements(Presentation + "Border")
                .Single()
                .Elements(Presentation + "ScrollViewer")
                .Single();
        }

        private static List<XElement> Cards(XElement tab)
        {
            return tab
                .Descendants(Presentation + "Border")
                .Where(element =>
                    OptionalAttribute(element, "Style")
                    == "{DynamicResource Style.SettingsCard}")
                .ToList();
        }

        private static XElement Named(
            XContainer container,
            string name)
        {
            return container
                .Descendants()
                .Single(element =>
                    OptionalAttribute(element, "Name") == name
                    || (string?)element.Attribute(Xaml + "Name") == name);
        }

        private static string? OptionalAttribute(
            XElement element,
            string name)
        {
            return (string?)element.Attribute(name);
        }

        private static string RequiredAttribute(
            XElement element,
            string name)
        {
            return OptionalAttribute(element, name)
                ?? throw new InvalidOperationException(
                    $"Attribute '{name}' is missing on " +
                    $"'{element.Name.LocalName}'.");
        }

        private static string RepositoryFile(
            params string[] parts)
        {
            foreach (string start in new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            })
            {
                DirectoryInfo? directory =
                    new DirectoryInfo(start);

                while (directory != null)
                {
                    string candidate = Path.Combine(
                        new[] { directory.FullName }
                            .Concat(parts)
                            .ToArray());

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }
            }

            throw new FileNotFoundException(
                "Could not locate repository file.",
                Path.Combine(parts));
        }
    }
}
