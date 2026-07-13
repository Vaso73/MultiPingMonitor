using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class LivePingDisabledVisualTests
    {
        [Theory]
        [InlineData("VisualStyle.Modern.xaml")]
        [InlineData("VisualStyle.Classic.xaml")]
        public void LivePingButtonStylesExposeStrongDisabledState(
            string fileName)
        {
            XDocument document = XDocument.Load(
                RepositoryPath(
                    "MultiPingMonitor",
                    "Styles",
                    fileName),
                LoadOptions.PreserveWhitespace);

            XNamespace xaml =
                "http://schemas.microsoft.com/winfx/2006/xaml";

            foreach (string styleKey in new[]
            {
                "Style.LivePingFooterButton",
                "Style.LivePingActionButton",
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
                                styleKey,
                                StringComparison.Ordinal));

                XElement triggerContainer = style
                    .Descendants()
                    .Single(
                        element =>
                            element.Name.LocalName
                                == "ControlTemplate.Triggers");

                IReadOnlyList<XElement> triggers =
                    triggerContainer
                        .Elements()
                        .Where(
                            element =>
                                element.Name.LocalName == "Trigger")
                        .ToArray();

                XElement disabledTrigger = triggers
                    .Single(
                        element =>
                            string.Equals(
                                (string?)element.Attribute("Property"),
                                "IsEnabled",
                                StringComparison.Ordinal)
                            && string.Equals(
                                (string?)element.Attribute("Value"),
                                "False",
                                StringComparison.OrdinalIgnoreCase));

                Assert.Same(
                    disabledTrigger,
                    triggers[triggers.Count - 1]);

                IReadOnlyList<XElement> setters =
                    disabledTrigger
                        .Elements()
                        .Where(
                            element =>
                                element.Name.LocalName == "Setter")
                        .ToArray();

                AssertSetter(
                    setters,
                    "border",
                    "Background",
                    "{DynamicResource Theme.Surface}");

                AssertSetter(
                    setters,
                    "border",
                    "BorderBrush",
                    "{DynamicResource Theme.Border}");

                AssertSetter(
                    setters,
                    "border",
                    "Opacity",
                    "0.40");

                AssertSetter(
                    setters,
                    null,
                    "Cursor",
                    "Arrow");
            }
        }

        private static void AssertSetter(
            IEnumerable<XElement> setters,
            string? targetName,
            string property,
            string value)
        {
            Assert.Contains(
                setters,
                setter =>
                    string.Equals(
                        (string?)setter.Attribute("TargetName"),
                        targetName,
                        StringComparison.Ordinal)
                    && string.Equals(
                        (string?)setter.Attribute("Property"),
                        property,
                        StringComparison.Ordinal)
                    && string.Equals(
                        (string?)setter.Attribute("Value"),
                        value,
                        StringComparison.Ordinal));
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
