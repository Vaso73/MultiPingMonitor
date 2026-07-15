using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MultiPingMonitor.Tests
{
    public sealed class ManageCompactSetsWindowLayoutTests
    {
        private static readonly XNamespace Presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        private static readonly XNamespace Xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void ManagerUsesStableThemedMasterDetailLayout()
        {
            XDocument document = XDocument.Load(
                RepositoryFile(
                    "MultiPingMonitor",
                    "UI",
                    "ManageCompactSetsWindow.xaml"));

            XElement window = document.Root
                ?? throw new InvalidOperationException(
                    "ManageCompactSetsWindow root is missing.");

            Assert.Equal("920", Required(window, "Width"));
            Assert.Equal("840", Required(window, "MinWidth"));
            Assert.Equal("560", Required(window, "Height"));
            Assert.Equal("560", Required(window, "MinHeight"));

            XElement leftPane = Named(document, "LeftPaneColumn");
            Assert.Equal("300", Required(leftPane, "Width"));
            Assert.Equal("220", Required(leftPane, "MinWidth"));
            Assert.Equal("380", Required(leftPane, "MaxWidth"));

            XElement titleIcon = document
                .Descendants(Presentation + "Image")
                .First();

            Assert.Equal(
                "{StaticResource icon.vmping-logo-simple}",
                Required(titleIcon, "Source"));

            XElement setsToolbar = Named(document, "SetsToolbar");
            Assert.Equal(
                new[] { "NewSetButton", "SetsMoreButton" },
                setsToolbar
                    .Descendants(Presentation + "Button")
                    .Select(Name)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToArray());

            XElement targetsToolbar = Named(document, "TargetsToolbar");
            Assert.Equal(
                new[]
                {
                    "AddTargetButton",
                    "EditTargetButton",
                    "RemoveTargetButton",
                },
                targetsToolbar
                    .Descendants(Presentation + "Button")
                    .Select(Name)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToArray());

            XElement setsList = Named(document, "SetsListBox");
            XElement targetsList = Named(document, "TargetsListBox");

            Assert.Equal(
                "SetsListBox_PreviewMouseRightButtonUp",
                Required(setsList, "PreviewMouseRightButtonUp"));
            Assert.Equal(
                "TargetsListBox_PreviewMouseRightButtonUp",
                Required(targetsList, "PreviewMouseRightButtonUp"));
            Assert.Equal(
                "TargetsListBox_MouseDoubleClick",
                Required(targetsList, "MouseDoubleClick"));

            Assert.Empty(
                document.Descendants(
                    Presentation + "ContextMenu"));

            string xaml = File.ReadAllText(
                RepositoryFile(
                    "MultiPingMonitor",
                    "UI",
                    "ManageCompactSetsWindow.xaml"));

            foreach (string binding in new[]
            {
                "{Binding ActiveMarker}",
                "{Binding Name}",
                "{Binding TargetCount}",
                "{Binding Target}",
                "{Binding Alias}",
            })
            {
                Assert.Contains(binding, xaml);
            }

            Assert.Contains(
                "x:Key=\"CompactManagerMenuItemStyle\"",
                xaml);
            Assert.Contains(
                "BasedOn=\"{StaticResource MenuItemStyle}\"",
                xaml);
            Assert.Contains(
                "Value=\"{DynamicResource Theme.Text.Primary}\"",
                xaml);
            Assert.Contains(
                "Value=\"{DynamicResource Theme.Text.Secondary}\"",
                xaml);
            Assert.Contains(
                "<Setter Property=\"Opacity\" Value=\"0.45\"/>",
                xaml);

            XElement countBadge = document
                .Descendants(Presentation + "TextBlock")
                .Single(element =>
                    (string?)element.Attribute("Text")
                    == "{Binding TargetCount}");

            Assert.Equal(
                "{DynamicResource Theme.Text.Primary}",
                Required(countBadge, "Foreground"));

            Assert.Equal(
                2,
                xaml.Split(
                    "Foreground=\"{DynamicResource Theme.Text.Secondary}\"")
                    .Length - 1);
            Assert.Contains("SetsListBox_Drop", xaml);
            Assert.Contains("TargetsListBox_Drop", xaml);
        }

        [Fact]
        public void ManagerCreatesFreshMenusAndKeepsExistingOperations()
        {
            string source = File.ReadAllText(
                RepositoryFile(
                    "MultiPingMonitor",
                    "UI",
                    "ManageCompactSetsWindow.xaml.cs"));

            Assert.Contains(
                "private ContextMenu CreateSetsContextMenu()",
                source);
            Assert.Contains(
                "private ContextMenu CreateTargetsContextMenu()",
                source);
            Assert.Contains(
                "private ContextMenu CreateThemedContextMenu()",
                source);
            Assert.Contains(
                "private MenuItem CreateThemedMenuItem(",
                source);
            Assert.Contains(
                "CompactManagerMenuItemStyle",
                source);
            Assert.DoesNotContain(
                "TryFindResource(\"MenuItemStyle\")",
                source);
            Assert.Contains(
                "SetsListBox_PreviewMouseRightButtonUp",
                source);
            Assert.Contains(
                "TargetsListBox_PreviewMouseRightButtonUp",
                source);
            Assert.Contains(
                "TargetsListBox_MouseDoubleClick",
                source);
            Assert.Contains(
                "MaxWidth",
                source);
            Assert.Contains(
                "CreateSetsContextMenu()",
                source);
            Assert.DoesNotContain(
                "SetsListBox.ContextMenu",
                source);

            foreach (string existingOperation in new[]
            {
                "NewSet_Click",
                "RenameSet_Click",
                "DeleteSet_Click",
                "SetActive_Click",
                "MoveSetUp_Click",
                "MoveSetDown_Click",
                "ExportSelected_Click",
                "ExportAll_Click",
                "Import_Click",
                "AddTarget_Click",
                "EditTarget_Click",
                "RemoveTarget_Click",
                "MoveTargetUp_Click",
                "MoveTargetDown_Click",
                "SetsListBox_Drop",
                "TargetsListBox_Drop",
                "RestoreSplitterWidth",
                "SaveSplitterWidth",
            })
            {
                Assert.Contains(existingOperation, source);
            }
        }

        private static XElement Named(
            XContainer container,
            string name)
        {
            return (container is XElement root
                    ? new[] { root }.Concat(container.Descendants())
                    : container.Descendants())
                .Single(element => Name(element) == name);
        }

        private static string Name(XElement element)
        {
            return (string?)element.Attribute("Name")
                ?? (string?)element.Attribute(Xaml + "Name")
                ?? string.Empty;
        }

        private static string Required(
            XElement element,
            string name)
        {
            return (string?)element.Attribute(name)
                ?? throw new InvalidOperationException(
                    $"Attribute '{name}' is missing on "
                    + $"'{element.Name.LocalName}'.");
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
