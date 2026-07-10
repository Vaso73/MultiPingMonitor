using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MultiPingMonitor.Classes
{
    internal static class LocalizationRefreshService
    {
        public static void Refresh(
            DependencyObject root,
            IReadOnlyDictionary<string, string> oldResources,
            IReadOnlyDictionary<string, string> newResources)
        {
            if (root == null || oldResources == null || newResources == null)
                return;

            var replacements = BuildReplacementMap(oldResources, newResources);
            if (replacements.Count == 0)
                return;

            RefreshObject(root, replacements, new HashSet<DependencyObject>());
        }

        private static Dictionary<string, string> BuildReplacementMap(
            IReadOnlyDictionary<string, string> oldResources,
            IReadOnlyDictionary<string, string> newResources)
        {
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var pair in oldResources)
            {
                if (string.IsNullOrEmpty(pair.Value))
                    continue;

                if (!newResources.TryGetValue(pair.Key, out var newValue))
                    continue;

                if (string.IsNullOrEmpty(newValue))
                    continue;

                if (string.Equals(pair.Value, newValue, StringComparison.Ordinal))
                    continue;

                if (!replacements.ContainsKey(pair.Value))
                    replacements[pair.Value] = newValue;
            }

            return replacements;
        }

        private static string Replace(
            string value,
            IReadOnlyDictionary<string, string> replacements)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return replacements.TryGetValue(value, out var replacement)
                ? replacement
                : value;
        }

        private static object ReplaceObject(
            object value,
            IReadOnlyDictionary<string, string> replacements)
        {
            return value is string text
                ? Replace(text, replacements)
                : value;
        }

        private static void RefreshObject(
            DependencyObject obj,
            IReadOnlyDictionary<string, string> replacements,
            HashSet<DependencyObject> visited)
        {
            if (obj == null || visited.Contains(obj))
                return;

            visited.Add(obj);

            switch (obj)
            {
                case Window window:
                    window.Title = Replace(window.Title, replacements);
                    break;

                case TextBlock textBlock:
                    textBlock.Text = Replace(textBlock.Text, replacements);
                    break;

                case AccessText accessText:
                    accessText.Text = Replace(accessText.Text, replacements);
                    break;

                case HeaderedItemsControl headeredItemsControl:
                    headeredItemsControl.Header = ReplaceObject(headeredItemsControl.Header, replacements);
                    break;

                case HeaderedContentControl headeredContentControl:
                    headeredContentControl.Header = ReplaceObject(headeredContentControl.Header, replacements);
                    break;

                case ContentControl contentControl:
                    contentControl.Content = ReplaceObject(contentControl.Content, replacements);
                    break;
            }

            if (obj is FrameworkElement element)
            {
                if (element.ToolTip is string toolTipText)
                    element.ToolTip = Replace(toolTipText, replacements);
                else if (element.ToolTip is DependencyObject toolTipObject)
                    RefreshObject(toolTipObject, replacements, visited);
            }

            foreach (var child in LogicalTreeHelper.GetChildren(obj).OfType<DependencyObject>())
            {
                RefreshObject(child, replacements, visited);
            }

            int visualChildren;
            try
            {
                visualChildren = VisualTreeHelper.GetChildrenCount(obj);
            }
            catch (InvalidOperationException)
            {
                visualChildren = 0;
            }

            for (int index = 0; index < visualChildren; index++)
            {
                RefreshObject(VisualTreeHelper.GetChild(obj, index), replacements, visited);
            }
        }
    }
}
