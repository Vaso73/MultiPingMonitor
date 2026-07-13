using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class WindowMainPanelBorderVisualTests
    {
        [Theory]
        [InlineData("VisualStyle.Modern.xaml", "Theme.Accent")]
        [InlineData("VisualStyle.Classic.xaml", "Theme.Border")]
        public void WindowMainPanelProvidesCrispInsetResizableFrame(
            string fileName,
            string expectedBrush)
        {
            XElement style = LoadWindowMainPanelStyle(fileName);

            AssertSetter(
                style.Elements(),
                "BorderBrush",
                "{DynamicResource " + expectedBrush + "}");

            AssertSetter(
                style.Elements(),
                "BorderThickness",
                "1");

            AssertSetter(
                style.Elements(),
                "Margin",
                "0");

            AssertSetter(
                style.Elements(),
                "SnapsToDevicePixels",
                "True");

            AssertSetter(
                style.Elements(),
                "UseLayoutRounding",
                "True");

            XElement triggers = style
                .Elements()
                .Single(
                    element =>
                        element.Name.LocalName == "Style.Triggers");

            AssertResizeTrigger(
                triggers,
                "CanResize");

            AssertResizeTrigger(
                triggers,
                "CanResizeWithGrip");

            XElement maximized = triggers
                .Elements()
                .Single(
                    element =>
                        element.Name.LocalName == "DataTrigger"
                        && AttributeContains(
                            element,
                            "Binding",
                            "Path=WindowState")
                        && string.Equals(
                            (string?)element.Attribute("Value"),
                            "Maximized",
                            StringComparison.Ordinal));

            AssertSetter(
                maximized.Elements(),
                "BorderThickness",
                "0");

            AssertSetter(
                maximized.Elements(),
                "Margin",
                "0");
        }

        [Fact]
        public void CustomChromeWindowsUseSharedWindowMainPanel()
        {
            string uiDirectory = RepositoryPath(
                "MultiPingMonitor",
                "UI");

            foreach (string path in Directory.EnumerateFiles(
                uiDirectory,
                "*.xaml",
                SearchOption.TopDirectoryOnly))
            {
                XDocument document = XDocument.Load(path);
                XElement? window = document.Root;

                if (window == null
                    || window.Name.LocalName != "Window"
                    || !window.Elements().Any(
                        element =>
                            element.Name.LocalName
                                == "WindowChrome.WindowChrome"))
                {
                    continue;
                }

                string fileName = Path.GetFileName(path);

                if (string.Equals(
                    fileName,
                    "PopupNotificationWindow.xaml",
                    StringComparison.Ordinal))
                {
                    continue;
                }

                XElement visualRoot = window
                    .Elements()
                    .First(
                        element =>
                            !element.Name.LocalName.Contains(
                                ".",
                                StringComparison.Ordinal));

                Assert.Equal(
                    "Border",
                    visualRoot.Name.LocalName);

                Assert.Equal(
                    "{DynamicResource Style.WindowMainPanel}",
                    (string?)visualRoot.Attribute("Style"));
            }
        }

        private static XElement LoadWindowMainPanelStyle(
            string fileName)
        {
            XDocument document = XDocument.Load(
                RepositoryPath(
                    "MultiPingMonitor",
                    "Styles",
                    fileName));

            XNamespace xaml =
                "http://schemas.microsoft.com/winfx/2006/xaml";

            return document
                .Descendants()
                .Single(
                    element =>
                        element.Name.LocalName == "Style"
                        && string.Equals(
                            (string?)element.Attribute(
                                xaml + "Key"),
                            "Style.WindowMainPanel",
                            StringComparison.Ordinal));
        }

        private static void AssertResizeTrigger(
            XElement triggerContainer,
            string resizeMode)
        {
            XElement trigger = triggerContainer
                .Elements()
                .Single(
                    element =>
                        element.Name.LocalName == "DataTrigger"
                        && AttributeContains(
                            element,
                            "Binding",
                            "Path=ResizeMode")
                        && string.Equals(
                            (string?)element.Attribute("Value"),
                            resizeMode,
                            StringComparison.Ordinal));

            AssertSetter(
                trigger.Elements(),
                "Margin",
                "1");
        }

        private static bool AttributeContains(
            XElement element,
            string attributeName,
            string expectedText)
        {
            string value =
                (string?)element.Attribute(attributeName)
                ?? string.Empty;

            return value.Contains(
                expectedText,
                StringComparison.Ordinal);
        }

        private static void AssertSetter(
            IEnumerable<XElement> elements,
            string property,
            string value)
        {
            Assert.Contains(
                elements,
                element =>
                    element.Name.LocalName == "Setter"
                    && string.Equals(
                        (string?)element.Attribute("Property"),
                        property,
                        StringComparison.Ordinal)
                    && string.Equals(
                        (string?)element.Attribute("Value"),
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
