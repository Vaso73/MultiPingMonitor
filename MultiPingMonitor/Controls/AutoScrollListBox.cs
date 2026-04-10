using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MultiPingMonitor.Controls
{
    public class AutoScrollListBox : ListBox
    {
        private bool IsAutoScrollEnabled = true;
        private AdornerLayer _adornerLayer;
        private AutoScrollAdorner _autoScrollAdorner;

        public AutoScrollListBox()
        {
            Loaded += ListBox_Loaded;
        }

        private void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            // When ListBox is loaded, automatically scroll to the bottom of the list.
            // This is to handle dragging/dropping probes.
            if (Items.Count > 1)
            {
                ScrollIntoView(Items[Items.Count - 1]);
            }

            // Subscribe to the ScrollViewers LostMouseCapture and ScrollChanged events.
            if (VisualTreeHelper.GetChildrenCount(this) > 0 && VisualTreeHelper.GetChild(this, 0) is Decorator border)
            {
                ScrollViewer scroll = border.Child as ScrollViewer;
                scroll.LostMouseCapture += Scroll_LostMouseCapture;
                scroll.ScrollChanged += Scroll_ScrollChanged;
            }

            _adornerLayer = AdornerLayer.GetAdornerLayer(this);
            _autoScrollAdorner = new AutoScrollAdorner(this);
        }

        /// <summary>
        /// Deferred auto-scroll that runs once after layout is complete.
        /// Avoids layout-invalidation inside OnRender which caused cascading
        /// re-measure/re-arrange passes during window resize.
        /// </summary>
        private void DeferAutoScroll()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (System.Action)(() =>
            {
                if (_adornerLayer == null || _autoScrollAdorner == null)
                    return;

                if (VisualTreeHelper.GetChildrenCount(this) == 0)
                    return;

                if (!(VisualTreeHelper.GetChild(this, 0) is Decorator border))
                    return;

                ScrollViewer scroll = border.Child as ScrollViewer;
                if (scroll == null)
                    return;

                if (scroll.ComputedVerticalScrollBarVisibility != Visibility.Visible)
                {
                    IsAutoScrollEnabled = true;
                    _adornerLayer.Remove(_autoScrollAdorner);
                }

                if (IsAutoScrollEnabled && !scroll.IsMouseCaptureWithin)
                {
                    scroll.ScrollToEnd();
                }
            }));
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (IsAutoScrollEnabled)
            {
                DeferAutoScroll();
            }
        }

        private void Scroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            const double MinimumAdornerHeight = 11.0;
            ScrollViewer scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight || scrollViewer.ActualHeight < MinimumAdornerHeight)
            {
                _adornerLayer.Remove(_autoScrollAdorner);

            }
            else
            {
                if (_adornerLayer.GetAdorners(this) == null)
                {
                    _adornerLayer.Add(_autoScrollAdorner);
                }
            }
        }

        private void Scroll_LostMouseCapture(object sender, MouseEventArgs e)
        {
            // User released mouse after clicking or dragging the scrollbar.
            // Check the position of the scrollbar - If it's scrolled to
            // the bottom, then automatically re-enable auto-scrolling.
            ScrollViewer scrollViewer = (ScrollViewer)sender;
            IsAutoScrollEnabled = scrollViewer.VerticalOffset >=
                (scrollViewer.ScrollableHeight > 100
                ? scrollViewer.ScrollableHeight - 2
                : scrollViewer.ScrollableHeight - 1);
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // An item was added to the ListBox.
                if (IsAutoScrollEnabled && VisualTreeHelper.GetChildrenCount(this) > 0 && VisualTreeHelper.GetChild(this, 0) is Decorator border)
                {
                    // Scroll to the bottom of the ListBox. If user is currently clicking the scrollbar, then do nothing.
                    ScrollViewer scroll = border.Child as ScrollViewer;
                    if (!scroll.IsMouseCaptureWithin)
                    {
                        scroll?.ScrollToEnd();
                    }
                }
            }
        }
    }
}
