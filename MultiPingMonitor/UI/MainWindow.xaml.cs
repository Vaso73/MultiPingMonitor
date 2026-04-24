using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MultiPingMonitor.Classes;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.UI
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Probe> _ProbeCollection = new ObservableCollection<Probe>();
        private Dictionary<string, string> _Aliases = new Dictionary<string, string>();
        private System.Windows.Forms.NotifyIcon NotifyIcon;

        /// <summary>
        /// Exposes the Normal mode probe collection for Add-to-Set operations
        /// from manual live ping windows (same-assembly access).
        /// </summary>
        internal ObservableCollection<Probe> NormalProbeCollection => _ProbeCollection;

        // ── Dynamic tray icon ─────────────────────────────────────────────────
        // Three icons representing aggregate host status: neutral (no hosts),
        // online (all up), offline (at least one down).
        private System.Drawing.Icon _trayIconNeutral;
        private System.Drawing.Icon _trayIconOnline;
        private System.Drawing.Icon _trayIconOffline;

        // Tracks the last aggregate state to avoid redundant icon/tooltip updates.
        private enum TrayAggregateState { Neutral, Online, Offline }
        private TrayAggregateState _trayState = TrayAggregateState.Neutral;

        // Shadow set of probes whose PropertyChanged is currently subscribed.
        // Required to cleanly unsubscribe on ObservableCollection Reset (Clear()),
        // which fires CollectionChanged with OldItems=null.
        private readonly System.Collections.Generic.HashSet<Probe> _subscribedProbes
            = new System.Collections.Generic.HashSet<Probe>();

        // Set to true when a deliberate application shutdown is initiated from the tray exit
        // menu item, so Window_Closing knows to save placement and config instead of re-hiding.
        private bool _IsShuttingDown = false;

        // Set to true when the main window is hidden to the system tray.
        private bool _IsHiddenToTray = false;

        // Set to true once startup content (probes, CLI args) has been initialized.
        // Prevents double-initialization when the window is first shown after a tray-only startup.
        private bool _startupContentInitialized = false;

        // ── Display mode ──────────────────────────────────────────────────────
        // Saved references to the normal-mode ItemTemplate, ItemsPanel, and Template
        // so they can be restored when switching back from Compact to Normal.
        private DataTemplate _normalItemTemplate;
        private ItemsPanelTemplate _normalItemsPanel;
        private ControlTemplate _normalControlTemplate;

        // ── Compact custom targets ────────────────────────────────────────────
        // Separate probe collection used when Compact mode is configured to use
        // custom targets instead of the normal dataset.
        private readonly ObservableCollection<Probe> _CompactProbeCollection = new ObservableCollection<Probe>();

        // ── Edge snap ─────────────────────────────────────────────────────────
        // Pixels within which a window edge is snapped flush to the working-area
        // edge.  Only tiny accidental overshoots/gaps are corrected; the user
        // can still drag deliberately further outside the screen.
        private const int EdgeSnapThresholdPx = 12;

        // Win32 messages handled by the WndProc hook.
        private const int WM_MOVING = 0x0216;
        private const int WM_SIZING = 0x0214;

        // WM_SIZING wParam values that identify which edge / corner is being dragged.
        private const int WMSZ_LEFT        = 1;
        private const int WMSZ_RIGHT       = 2;
        private const int WMSZ_TOP         = 3;
        private const int WMSZ_TOPLEFT     = 4;
        private const int WMSZ_TOPRIGHT    = 5;
        private const int WMSZ_BOTTOM      = 6;
        private const int WMSZ_BOTTOMLEFT  = 7;
        private const int WMSZ_BOTTOMRIGHT = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static RoutedCommand OptionsCommand = new RoutedCommand();
        public static RoutedCommand StartStopCommand = new RoutedCommand();
        public static RoutedCommand HelpCommand = new RoutedCommand();
        public static RoutedCommand NewInstanceCommand = new RoutedCommand();
        public static RoutedCommand TracerouteCommand = new RoutedCommand();
        public static RoutedCommand FloodHostCommand = new RoutedCommand();
        public static RoutedCommand AddProbeCommand = new RoutedCommand();
        public static RoutedCommand MultiInputCommand = new RoutedCommand();
        public static RoutedCommand StatusHistoryCommand = new RoutedCommand();

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
            InitializeTrayIcon();

            // Attach window placement using a mode-specific key so Normal and Compact
            // each remember their own position and size independently.
            WindowPlacementService.Attach(this, PlacementKeyForMode(ApplicationOptions.CurrentDisplayMode));
        }

        /// <summary>
        /// Called by App.xaml.cs instead of Show() when StartInTray=true.
        /// Initializes probes and CLI args without making the window visible,
        /// so the app starts directly in the tray with zero visible window flash.
        /// </summary>
        internal void InitializeForStartInTray()
        {
            InitializeStartupContent();
        }

        private void InitializeApplication()
        {
            InitializeCommandBindings();
            LoadFavorites();
            LoadAliases();
            Configuration.Load();
            ThemeManager.ApplyTheme(ThemeManager.ParseTheme(ApplicationOptions.Theme));
            VisualStyleManager.ApplyStyle(VisualStyleManager.ParseStyle(ApplicationOptions.VisualStyle));
            RefreshGuiState();

            // Set items source for main GUI ItemsControl (default to normal collection).
            ProbeItemsControl.ItemsSource = _ProbeCollection;

            // Apply display mode after setting default ItemsSource.
            // ApplyDisplayMode will override ItemsSource if compact + custom targets.
            ApplyDisplayMode(ApplicationOptions.CurrentDisplayMode);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // When StartInTray=true the window is never shown at startup, so
            // InitializeForStartInTray() is called directly from App.xaml.cs and
            // sets _startupContentInitialized=true before this event can fire.
            // Guard here prevents double-initialization if Window_Loaded fires later
            // (e.g. when the user restores the window from tray for the first time).
            if (!_startupContentInitialized)
            {
                InitializeStartupContent();
            }
        }

        /// <summary>
        /// Initializes probe collection, CLI arguments, and column layout.
        /// Called either from Window_Loaded (normal startup) or InitializeForStartInTray()
        /// (tray-only startup where the window is never shown).
        /// </summary>
        private void InitializeStartupContent()
        {
            _startupContentInitialized = true;

            // Set initial ColumnCount slider value.
            ColumnCount.Value = ApplicationOptions.InitialColumnCount > 0
                ? ApplicationOptions.InitialColumnCount
                : 2;

            // Parse command line arguments. Get any host addresses entered on command line.
            List<string> cliHosts = CommandLine.ParseArguments();

            // Add initial probes.
            if (cliHosts.Count > 0)
            {
                // Host addresses were entered on the command line.
                // Add addresses to probe collection and begin pinging.
                AddProbe(cliHosts.Count);
                for (int i = 0; i < cliHosts.Count; ++i)
                {
                    _ProbeCollection[i].Hostname = cliHosts[i];
                    _ProbeCollection[i].Alias = _Aliases.ContainsKey(_ProbeCollection[i].Hostname.ToLower())
                        ? _Aliases[_ProbeCollection[i].Hostname.ToLower()]
                        : null;
                    _ProbeCollection[i].StartStop();
                }
            }
            else
            {
                // No addresses entered on the command line.
                // Add initial blank probes.
                AddProbe(
                    (ApplicationOptions.InitialProbeCount > 0)
                        ? ApplicationOptions.InitialProbeCount
                        : 2);

                // Determine startup mode. Skip modes that require the window to be
                // visible (e.g. MultiInput dialog) when starting hidden in tray.
                switch (ApplicationOptions.InitialStartMode)
                {
                    case ApplicationOptions.StartMode.MultiInput:
                        RefreshColumnCount();
                        if (IsVisible)
                            MultiInputWindowExecute(null, null);
                        break;
                    case ApplicationOptions.StartMode.Favorite:
                        if (ApplicationOptions.InitialFavorite != null
                            && !string.IsNullOrWhiteSpace(ApplicationOptions.InitialFavorite))
                        {
                            LoadFavorite(ApplicationOptions.InitialFavorite);
                        }
                        break;
                }
            }

            RefreshColumnCount();
        }

        private void RefreshGuiState()
        {
            // Set popup option on menu bar.
            PopupAlways.IsChecked = false;
            PopupNever.IsChecked = false;
            PopupWhenMinimized.IsChecked = false;

            switch (ApplicationOptions.PopupOption)
            {
                case ApplicationOptions.PopupNotificationOption.Always:
                    PopupAlways.IsChecked = true;
                    break;
                case ApplicationOptions.PopupNotificationOption.Never:
                    PopupNever.IsChecked = true;
                    break;
                case ApplicationOptions.PopupNotificationOption.WhenMinimized:
                    PopupWhenMinimized.IsChecked = true;
                    break;
            }

            // Set compact source menu checks.
            UpdateCompactSourceMenuChecks();

            // Set always on top state.
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;
            SyncPinButtonStates();
            if (Probe.StatusHistoryWindow != null && Probe.StatusHistoryWindow.IsLoaded)
            {
                Probe.StatusHistoryWindow.Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;
            }
            if (HelpWindow._OpenWindow != null)
            {
                HelpWindow._OpenWindow.Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;
            }
            foreach (Probe probe in _ProbeCollection)
            {
                if (probe.IsolatedWindow != null && probe.IsolatedWindow.IsLoaded)
                {
                    probe.IsolatedWindow.Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;
                }
            }
        }

        private void RefreshColumnCount()
        {
            // Update ColumnCount.Tag to be whichever is lower: ColumnCount.Value or _ProbeCollection.Count.
            // The actual number of grid columns is bound to the tag value.
            ColumnCount.Tag = ColumnCount.Value > _ProbeCollection.Count
                ? _ProbeCollection.Count
                : (int)ColumnCount.Value;
        }

        // ── Display mode ──────────────────────────────────────────────────────
        // The stored normal-mode WindowChrome settings, used to restore when
        // switching back from Compact to Normal.
        private System.Windows.Shell.WindowChrome _normalChrome;

        // The native WinForms tray context menu.
        private System.Windows.Forms.ContextMenuStrip _trayNativeMenu;

        // The native tray menu item that toggles between Normal ↔ Compact.
        // Kept as a field so its Text can be updated when the mode changes.
        private System.Windows.Forms.ToolStripMenuItem _trayNativeToggleItem;

        // Native tray menu items for quick Classic/Modern visual-style switch.
        private System.Windows.Forms.ToolStripMenuItem _trayNativeStyleClassic;
        private System.Windows.Forms.ToolStripMenuItem _trayNativeStyleModern;

        // Default compact window dimensions used when no saved placement exists yet.
        private const double CompactDefaultWidth = 280;
        private const double CompactDefaultHeight = 400;

        /// <summary>
        /// Returns the WindowPlacementService key for the given display mode.
        /// Normal uses "MainWindow"; Compact uses "MainWindow.Compact".
        /// </summary>
        private static string PlacementKeyForMode(ApplicationOptions.DisplayMode mode)
        {
            return mode == ApplicationOptions.DisplayMode.Compact
                ? "MainWindow.Compact"
                : "MainWindow";
        }

        /// <summary>
        /// Switches the display mode, saving current window bounds under the old mode
        /// and restoring bounds from the new mode. Also updates ApplicationOptions,
        /// the tray toggle text, and saves config.
        /// Called from tray menu and can be reused from other entry points.
        /// </summary>
        internal void SwitchDisplayMode(ApplicationOptions.DisplayMode targetMode)
        {
            if (ApplicationOptions.CurrentDisplayMode == targetMode)
                return;

            var previousMode = ApplicationOptions.CurrentDisplayMode;

            // Save current bounds under the outgoing mode's key.
            WindowPlacementService.SaveWindow(this, PlacementKeyForMode(previousMode));

            // Apply new mode.
            ApplicationOptions.CurrentDisplayMode = targetMode;
            ApplyDisplayMode(targetMode);

            // Restore saved bounds for the target mode (if any).
            if (WindowPlacementService.HasPlacement(PlacementKeyForMode(targetMode)))
            {
                WindowPlacementService.RestoreWindow(this, PlacementKeyForMode(targetMode));
            }
            else
            {
                // First switch to this mode: apply reasonable defaults.
                ApplyDefaultPlacement(targetMode);
            }

            // Update tray toggle text.
            UpdateTrayToggleText();

            // Force tray aggregate state to re-evaluate from the new active collection.
            _trayState = TrayAggregateState.Neutral;
            UpdateTrayIcon();

            // Persist immediately.
            Configuration.Save();
        }

        /// <summary>
        /// Applies reasonable default window dimensions when switching to a mode
        /// that has no saved placement yet. Centers the new bounds on the current
        /// monitor to avoid a jarring position jump.
        /// </summary>
        private void ApplyDefaultPlacement(ApplicationOptions.DisplayMode mode)
        {
            if (mode == ApplicationOptions.DisplayMode.Compact)
            {
                // Compact: slim, tall panel. Center on the current monitor.
                var screen = System.Windows.Forms.Screen.FromRectangle(
                    new System.Drawing.Rectangle((int)Left, (int)Top, (int)Width, (int)Height));
                var wa = screen.WorkingArea;

                double newW = Math.Min(CompactDefaultWidth, wa.Width);
                double newH = Math.Min(CompactDefaultHeight, wa.Height);
                Left = wa.Left + (wa.Width - newW) / 2.0;
                Top = wa.Top + (wa.Height - newH) / 2.0;
                Width = newW;
                Height = newH;
            }
            // Normal mode fallback: keep current bounds (they came from the initial
            // Attach placement or the XAML defaults), which is the least surprising.
        }

        /// <summary>
        /// Updates the tray toggle and main-menu toggle texts to reflect the current mode.
        /// </summary>
        private void UpdateTrayToggleText()
        {
            var text = ApplicationOptions.CurrentDisplayMode == ApplicationOptions.DisplayMode.Compact
                ? Strings.Tray_SwitchToNormal
                : Strings.Tray_SwitchToCompact;

            if (_trayNativeToggleItem != null)
                _trayNativeToggleItem.Text = text;

            if (ToggleDisplayModeMenu != null)
                ToggleDisplayModeMenu.Header = text;
        }

        /// <summary>
        /// Centralized method that switches between Normal and Compact display modes.
        /// Called at startup, after config load, on live Options change, and on cancel rollback.
        /// </summary>
        internal void ApplyDisplayMode(ApplicationOptions.DisplayMode mode)
        {
            bool compact = mode == ApplicationOptions.DisplayMode.Compact;

            // Save the original templates on first call (before any switch).
            if (_normalItemTemplate == null)
                _normalItemTemplate = ProbeItemsControl.ItemTemplate;
            if (_normalItemsPanel == null)
                _normalItemsPanel = ProbeItemsControl.ItemsPanel;
            if (_normalControlTemplate == null)
                _normalControlTemplate = ProbeItemsControl.Template;

            // ── Toggle chrome / border for compact mode ──
            var currentChrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
            if (compact)
            {
                // Save current chrome for later restoration.
                if (_normalChrome == null && currentChrome != null)
                {
                    _normalChrome = new System.Windows.Shell.WindowChrome
                    {
                        CaptionHeight = currentChrome.CaptionHeight,
                        ResizeBorderThickness = currentChrome.ResizeBorderThickness,
                        GlassFrameThickness = currentChrome.GlassFrameThickness,
                        CornerRadius = currentChrome.CornerRadius
                    };
                }
                // Apply compact chrome: small caption for CompactTitleBar drag, keep resize border.
                var compactChrome = new System.Windows.Shell.WindowChrome
                {
                    CaptionHeight = 22,
                    ResizeBorderThickness = currentChrome?.ResizeBorderThickness ?? SystemParameters.WindowResizeBorderThickness,
                    GlassFrameThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(0)
                };
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, compactChrome);
            }
            else if (_normalChrome != null)
            {
                // Restore normal chrome.
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, _normalChrome);
            }

            // ── Toggle UI element visibility ──
            TitleBar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            TitleBarSeparator.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            MainMenu.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            CompactTitleBar.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;

            // ── Mode-specific minimum window width ──
            // Compact mode needs a much smaller minimum to allow narrow side-panel usage.
            // Normal mode retains the original 350 to protect its multi-column layout.
            MinWidth = compact ? 120 : 350;

            // ── Switch ItemTemplate and ItemsPanel ──
            if (compact)
            {
                ProbeItemsControl.ItemTemplate = (DataTemplate)FindResource("CompactProbeTemplate");
                // Use a vertical StackPanel for compact layout.
                var factory = new System.Windows.FrameworkElementFactory(typeof(StackPanel));
                factory.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Vertical);
                ProbeItemsControl.ItemsPanel = new ItemsPanelTemplate(factory);

                // Remove grid margins (no multi-column negative offsets needed).
                ProbeItemsControl.Margin = new Thickness(0);
                ProbeItemsControl.BorderThickness = new Thickness(0);

                // Enable vertical scrolling with a slim scrollbar so long target
                // lists remain reachable.  Template is defined in XAML resources.
                ProbeItemsControl.Template = (ControlTemplate)FindResource("CompactItemsControlTemplate");

                // Switch ItemsSource based on compact data source mode.
                ApplyCompactDataSource();
            }
            else
            {
                // Restore the original Normal-mode templates.
                ProbeItemsControl.ItemTemplate = _normalItemTemplate;
                ProbeItemsControl.ItemsPanel = _normalItemsPanel;
                ProbeItemsControl.Template = _normalControlTemplate;

                // Restore original margins.
                ProbeItemsControl.Margin = new Thickness(0, 0, -2, -2);
                ProbeItemsControl.BorderThickness = new Thickness(0, 1, 0, 0);

                // Normal mode always uses the main probe collection.
                ProbeItemsControl.ItemsSource = _ProbeCollection;

                // Returning to Normal mode: lift any notification suppression so that
                // Normal/Main probes resume firing all alerts.
                ApplyNormalProbeNotificationScope();
            }

            // Update tray toggle text whenever display mode is applied.
            UpdateTrayToggleText();
        }

        // ── Pin / Always-on-top buttons ───────────────────────────────────────

        /// <summary>
        /// Handles click on either pin button (Normal or Compact mode).
        /// Toggles this window's Topmost property without changing ApplicationOptions.
        /// </summary>
        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            // Determine new state from the ToggleButton that was clicked.
            bool pinned = (sender == NormalPinButton)
                ? NormalPinButton.IsChecked == true
                : CompactPinButton.IsChecked == true;

            Topmost = pinned;
            SyncPinButtonStates();
        }

        /// <summary>
        /// Synchronizes both pin buttons' checked state and icon color to match
        /// the current window Topmost value. Called from RefreshGuiState and PinButton_Click.
        /// </summary>
        private void SyncPinButtonStates()
        {
            bool pinned = Topmost;

            // Sync ToggleButton checked states.
            NormalPinButton.IsChecked = pinned;
            CompactPinButton.IsChecked = pinned;

            // Update icon fill: accent-colored when pinned, secondary when not.
            string fillKey = pinned ? "Theme.Accent" : "Theme.Text.Secondary";
            NormalPinIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, fillKey);
            CompactPinIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, fillKey);
        }

        /// <summary>
        /// Applies the correct ItemsSource for compact mode based on CompactSource setting.
        /// When set to NormalTargets, compact displays the main _ProbeCollection as a
        /// read-only view of Normal data (no cross-writing occurs).
        /// When set to CustomTargets, compact uses its own _CompactProbeCollection
        /// populated exclusively from compact sets (fully independent from Normal).
        /// Called from ApplyDisplayMode, OptionsWindow live preview, and OptionsWindow save.
        /// </summary>
        internal void ApplyCompactDataSource()
        {
            if (ApplicationOptions.CurrentDisplayMode != ApplicationOptions.DisplayMode.Compact)
                return;

            if (ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.NormalTargets)
            {
                // Use the same collection as Normal mode.
                ProbeItemsControl.ItemsSource = _ProbeCollection;
            }
            else
            {
                // Rebuild the compact probe collection from custom targets.
                RebuildCompactProbes();
                ProbeItemsControl.ItemsSource = _CompactProbeCollection;
            }

            // Scope notifications to the active monitoring context.
            ApplyNormalProbeNotificationScope();
        }

        /// <summary>
        /// Returns true when Normal/Main probe notifications should be suppressed —
        /// i.e. when Compact mode is the active context and uses its own custom Compact Set.
        /// Centralizes the condition to avoid duplication across <see cref="ApplyNormalProbeNotificationScope"/>
        /// and <see cref="ProbeCollection_CollectionChanged"/>.
        /// </summary>
        private static bool ShouldSuppressNormalProbeNotifications() =>
            ApplicationOptions.CurrentDisplayMode == ApplicationOptions.DisplayMode.Compact
            && ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.CustomTargets;

        /// <summary>
        /// Sets <see cref="Probe.SuppressNotifications"/> on every probe in the Normal/Main
        /// collection to scope alerts to the active monitoring context.
        /// Suppression is enabled when Compact mode is active and uses a custom Compact Set;
        /// in that case all popup, sound, email, and status-change-log notifications must
        /// come only from the active Compact Set, not also from the Normal/Main targets.
        /// Suppression is cleared as soon as the user returns to Normal mode, or switches
        /// the Compact source back to Normal Targets.
        /// </summary>
        private void ApplyNormalProbeNotificationScope()
        {
            bool suppress = ShouldSuppressNormalProbeNotifications();
            foreach (var probe in _ProbeCollection)
                probe.SuppressNotifications = suppress;
        }

        /// <summary>
        /// Stops existing compact probes and rebuilds the collection from
        /// the active compact set, auto-starting each probe.
        /// Compact mode uses exclusively its own alias data (from CompactTargetEntry.Alias)
        /// and never falls back to the global Normal-mode alias dictionary.
        /// If no compact set is active but legacy CompactCustomTargets exist,
        /// they are auto-migrated into a new compact set on-the-fly.
        /// </summary>
        private void RebuildCompactProbes()
        {
            // Stop and clear existing compact probes.
            foreach (var probe in _CompactProbeCollection)
            {
                if (probe.IsActive)
                    probe.StartStop();
            }
            _CompactProbeCollection.Clear();

            // Get entries from the active compact set.
            var activeSet = ApplicationOptions.GetActiveCompactSet();

            // Auto-migrate legacy CompactCustomTargets if no compact set exists yet.
            if (activeSet == null && ApplicationOptions.CompactCustomTargets.Count > 0)
            {
                Configuration.MigrateCompactTargetsToSets();
                activeSet = ApplicationOptions.GetActiveCompactSet();
            }

            if (activeSet == null)
                return;

            foreach (var entry in activeSet.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Target))
                    continue;

                var probe = new Probe();
                probe.Hostname = entry.Target.Trim();
                // Use only the compact-set-local alias. No fallback to the global
                // Normal-mode alias dictionary – compact data is fully independent.
                if (!string.IsNullOrWhiteSpace(entry.Alias))
                    probe.Alias = entry.Alias;
                _CompactProbeCollection.Add(probe);
                probe.StartStop();
            }
        }

        private void CompactTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        // ── Compact data source – centralized switching ───────────────────────

        /// <summary>
        /// Centralized method to switch compact data source mode.
        /// Called from main menu, compact title bar menu, and Options.
        /// Immediately applies the change, updates UI indicators, and persists config.
        /// </summary>
        internal void SetCompactSource(ApplicationOptions.CompactSourceMode mode)
        {
            if (ApplicationOptions.CompactSource == mode)
                return;

            ApplicationOptions.CompactSource = mode;
            ApplyCompactDataSource();
            UpdateCompactSourceMenuChecks();
            // Force tray to re-evaluate from the (possibly new) active collection.
            _trayState = TrayAggregateState.Neutral;
            UpdateTrayIcon();
            Configuration.Save();
        }

        /// <summary>
        /// Opens the Manage Compact Sets window.
        /// Previously opened the legacy ManageCompactTargetsWindow; now redirects
        /// to the full Compact Sets management since compact data is fully independent.
        /// Kept for backward compatibility with any external callers.
        /// </summary>
        internal void OpenManageCompactTargets()
        {
            OpenManageCompactSets();
        }

        /// <summary>
        /// Switches the active compact set by Id.
        /// Immediately refreshes compact view and tray state if compact custom targets are active.
        /// </summary>
        internal void SetActiveCompactSet(string setId)
        {
            if (ApplicationOptions.ActiveCompactSetId == setId)
                return;

            ApplicationOptions.ActiveCompactSetId = setId;

            // If compact mode with custom targets is active, refresh immediately.
            if (ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.CustomTargets)
            {
                ApplyCompactDataSource();
                _trayState = TrayAggregateState.Neutral;
                UpdateTrayIcon();
            }

            Configuration.Save();
        }

        /// <summary>
        /// Refreshes the compact view data source and tray state.
        /// Called when the active compact set's content changes (targets added/removed/edited)
        /// or when the active set is deleted and a new one is selected.
        /// </summary>
        internal void RefreshActiveCompactSetData()
        {
            if (ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.CustomTargets)
            {
                ApplyCompactDataSource();
            }
            _trayState = TrayAggregateState.Neutral;
            UpdateTrayIcon();
        }

        /// <summary>
        /// Opens the Manage Compact Sets window.
        /// Live changes are applied immediately via Owner-cast calls back into MainWindow.
        /// After the user closes it, does a final safety refresh.
        /// </summary>
        internal void OpenManageCompactSets()
        {
            var window = new ManageCompactSetsWindow();
            window.Owner = this;
            window.ShowDialog();

            // Final safety refresh after dialog closes: catches any edge cases
            // where sets were added/removed/renamed or entries edited.
            RefreshActiveCompactSetData();
            Configuration.Save();
        }

        /// <summary>
        /// Updates IsChecked state on all compact source menu items
        /// (main menu + compact title bar context menu).
        /// </summary>
        internal void UpdateCompactSourceMenuChecks()
        {
            bool isNormal = ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.NormalTargets;

            // Main menu items.
            if (MenuCompactSourceNormal != null)
                MenuCompactSourceNormal.IsChecked = isNormal;
            if (MenuCompactSourceCustom != null)
                MenuCompactSourceCustom.IsChecked = !isNormal;

            // Compact title bar context menu items (created dynamically, check if assigned).
            if (_compactMenuSourceNormal != null)
                _compactMenuSourceNormal.IsChecked = isNormal;
            if (_compactMenuSourceCustom != null)
                _compactMenuSourceCustom.IsChecked = !isNormal;
        }

        // References to dynamically created compact title bar context menu items.
        private MenuItem _compactMenuSourceNormal;
        private MenuItem _compactMenuSourceCustom;

        // ── Main menu compact source handlers ─────────────────────────────────

        private void MenuCompactSourceNormal_Click(object sender, RoutedEventArgs e)
        {
            SetCompactSource(ApplicationOptions.CompactSourceMode.NormalTargets);
        }

        private void MenuCompactSourceCustom_Click(object sender, RoutedEventArgs e)
        {
            SetCompactSource(ApplicationOptions.CompactSourceMode.CustomTargets);
        }

        private void MenuManageCompactTargets_Click(object sender, RoutedEventArgs e)
        {
            OpenManageCompactSets();
        }

        /// <summary>
        /// Rebuilds the dynamic portion of the CompactTargets main-menu submenu
        /// every time it opens, so compact set list and check states are always current.
        /// </summary>
        private const int CompactMenuFixedItemCount = 2; // NormalTargets + CustomTargets

        private void CompactTargetsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            // Remove all items after the two fixed source-mode items.
            while (CompactTargetsMenu.Items.Count > CompactMenuFixedItemCount)
                CompactTargetsMenu.Items.RemoveAt(CompactTargetsMenu.Items.Count - 1);

            // Update check states for the two fixed items.
            UpdateCompactSourceMenuChecks();

            // Compact set selection items.
            AppendCompactSetMenuItems(CompactTargetsMenu.Items);

            // Separator + Manage Compact Sets...
            CompactTargetsMenu.Items.Add(new Separator());
            var manageItem = new MenuItem
            {
                Header = Strings.Menu_CompactManageSets
            };
            var editIconSource = Application.Current.TryFindResource("icon.edit") as System.Windows.Media.ImageSource;
            if (editIconSource != null)
                manageItem.Icon = new System.Windows.Controls.Image { Source = editIconSource, Width = 16, Height = 16 };
            manageItem.Click += (s, args) => OpenManageCompactSets();
            CompactTargetsMenu.Items.Add(manageItem);
        }

        // ── Compact title bar menu button handler ─────────────────────────────

        /// <summary>
        /// Builds compact set selection menu items and appends them to the given items collection.
        /// Used by both the compact title bar context menu and the main menu.
        /// </summary>
        private void AppendCompactSetMenuItems(ItemCollection items)
        {
            var sets = ApplicationOptions.CompactSets;
            if (sets.Count == 0)
                return;

            items.Add(new Separator());

            foreach (var set in sets)
            {
                var setItem = new MenuItem
                {
                    Header = set.Name,
                    IsCheckable = true,
                    IsChecked = set.Id == ApplicationOptions.ActiveCompactSetId
                };
                var capturedId = set.Id;
                setItem.Click += (s, args) => SetActiveCompactSet(capturedId);
                items.Add(setItem);
            }
        }

        private void CompactMenuButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            // Apply the visual-style-aware Style (same pattern as tray context menu).
            menu.SetResourceReference(FrameworkElement.StyleProperty, "Style.ContextMenu");
            menu.SetResourceReference(Control.ForegroundProperty, "Theme.Text.Primary");
            var menuItemStyle = (Style)Application.Current.FindResource("MenuItemStyle");
            if (menuItemStyle != null)
                menu.Resources[typeof(MenuItem)] = menuItemStyle;

            _compactMenuSourceNormal = new MenuItem
            {
                Header = Strings.Options_CompactSource_NormalTargets,
                IsCheckable = true,
                IsChecked = ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.NormalTargets
            };
            _compactMenuSourceNormal.Click += (s, args) =>
                SetCompactSource(ApplicationOptions.CompactSourceMode.NormalTargets);

            _compactMenuSourceCustom = new MenuItem
            {
                Header = Strings.Options_CompactSource_CustomTargets,
                IsCheckable = true,
                IsChecked = ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.CustomTargets
            };
            _compactMenuSourceCustom.Click += (s, args) =>
                SetCompactSource(ApplicationOptions.CompactSourceMode.CustomTargets);

            menu.Items.Add(_compactMenuSourceNormal);
            menu.Items.Add(_compactMenuSourceCustom);

            // Compact set selection items.
            AppendCompactSetMenuItems(menu.Items);

            menu.Items.Add(new Separator());

            // "Open All Live Windows" submenu with Cascade and Tile options.
            var openAllSub = new MenuItem
            {
                Header = Strings.LivePing_OpenAllLive
            };
            var openAllIcon = Application.Current.TryFindResource("icon.window-restore-blue") as System.Windows.Media.ImageSource;
            if (openAllIcon != null)
                openAllSub.Icon = new System.Windows.Controls.Image { Source = openAllIcon, Width = 16, Height = 16 };
            var cascadeItem = new MenuItem { Header = Strings.LivePing_OpenAllCascade };
            var cascadeIcon = Application.Current.TryFindResource("icon.cascade") as System.Windows.Media.ImageSource;
            if (cascadeIcon != null)
                cascadeItem.Icon = new System.Windows.Controls.Image { Source = cascadeIcon, Width = 16, Height = 16 };
            cascadeItem.Click += (s, args) => OpenAllLiveWindowsAndArrange(cascade: true);
            var tileItem = new MenuItem { Header = Strings.LivePing_OpenAllTile };
            var tileIcon = Application.Current.TryFindResource("icon.columns-grid") as System.Windows.Media.ImageSource;
            if (tileIcon != null)
                tileItem.Icon = new System.Windows.Controls.Image { Source = tileIcon, Width = 16, Height = 16 };
            tileItem.Click += (s, args) => OpenAllLiveWindowsAndArrange(cascade: false);
            openAllSub.Items.Add(cascadeItem);
            openAllSub.Items.Add(tileItem);
            menu.Items.Add(openAllSub);

            menu.Items.Add(new Separator());

            var manageItem = new MenuItem
            {
                Header = Strings.Menu_CompactManageSets
            };
            var editIconSource = Application.Current.TryFindResource("icon.edit") as System.Windows.Media.ImageSource;
            if (editIconSource != null)
                manageItem.Icon = new System.Windows.Controls.Image { Source = editIconSource, Width = 16, Height = 16 };
            manageItem.Click += (s, args) => OpenManageCompactSets();

            menu.Items.Add(manageItem);

            menu.Items.Add(new Separator());

            var newLivePingItem = new MenuItem
            {
                Header = Strings.Menu_NewLivePing
            };
            var newLivePingIconSource = Application.Current.TryFindResource("icon.window-restore-blue") as System.Windows.Media.ImageSource;
            if (newLivePingIconSource != null)
                newLivePingItem.Icon = new System.Windows.Controls.Image { Source = newLivePingIconSource, Width = 16, Height = 16 };
            newLivePingItem.Click += (s, args) => NewLivePingMenu_Click(null, null);

            menu.Items.Add(newLivePingItem);

            menu.PlacementTarget = sender as Button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void InitializeCommandBindings()
        {
            CommandBindings.Add(new CommandBinding(OptionsCommand, OptionsExecute));
            CommandBindings.Add(new CommandBinding(StartStopCommand, StartStopExecute));
            CommandBindings.Add(new CommandBinding(HelpCommand, HelpExecute));
            CommandBindings.Add(new CommandBinding(NewInstanceCommand, NewInstanceExecute));
            CommandBindings.Add(new CommandBinding(TracerouteCommand, TracerouteExecute));
            CommandBindings.Add(new CommandBinding(FloodHostCommand, FloodHostExecute));
            CommandBindings.Add(new CommandBinding(AddProbeCommand, AddProbeExecute));
            CommandBindings.Add(new CommandBinding(MultiInputCommand, MultiInputWindowExecute));
            CommandBindings.Add(new CommandBinding(StatusHistoryCommand, StatusHistoryExecute));
            
            InputBindings.Add(new InputBinding(
                OptionsCommand,
                new KeyGesture(Key.F10)));
            InputBindings.Add(new InputBinding(
                StartStopCommand,
                new KeyGesture(Key.F5)));
            InputBindings.Add(new InputBinding(
                HelpCommand,
                new KeyGesture(Constants.HelpKeyBinding)));
            InputBindings.Add(new InputBinding(
                NewInstanceCommand,
                new KeyGesture(Key.N, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(
                TracerouteCommand,
                new KeyGesture(Key.T, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(
                FloodHostCommand,
                new KeyGesture(Key.F, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(
                AddProbeCommand,
                new KeyGesture(Key.A, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(
                MultiInputCommand,
                new KeyGesture(Key.F2)));
            InputBindings.Add(new InputBinding(
                StatusHistoryCommand,
                new KeyGesture(Constants.StatusHistoryKeyBinding)));

            OptionsMenu.Command = OptionsCommand;
            StartStopMenu.Command = StartStopCommand;
            HelpMenu.Command = HelpCommand;
            NewInstanceMenu.Command = NewInstanceCommand;
            TracerouteMenu.Command = TracerouteCommand;
            FloodHostMenu.Command = FloodHostCommand;
            AddProbeMenu.Command = AddProbeCommand;
            MultiInputMenu.Command = MultiInputCommand;
            StatusHistoryMenu.Command = StatusHistoryCommand;
        }

        public void AddProbe(int numberOfProbes = 1)
        {
            for (; numberOfProbes > 0; --numberOfProbes)
            {
                _ProbeCollection.Add(new Probe());
            }
        }

        public void ProbeStartStop_Click(object sender, EventArgs e)
        {
            ((Probe)((Button)sender).DataContext).StartStop();
        }

        private void ColumnCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // When the ColumnCount slider value is changed, update Tag to be the lesser of
            // ColumnCount.Value and _ProbeCollection.Count.
            // The visual column count is bound to the Tag value.
            RefreshColumnCount();
        }

        private void Hostname_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var probe = (sender as TextBox).DataContext as Probe;
                probe.StartStop();

                if (_ProbeCollection.IndexOf(probe) < _ProbeCollection.Count - 1)
                {
                    var cp = ProbeItemsControl.ItemContainerGenerator.ContainerFromIndex(_ProbeCollection.IndexOf(probe) + 1) as ContentPresenter;
                    var tb = (TextBox)cp.ContentTemplate.FindName("Hostname", cp);
                    tb?.Focus();
                }
            }
        }

        private void RemoveProbe_Click(object sender, RoutedEventArgs e)
        {
            if (_ProbeCollection.Count <= 1)
            {
                return;
            }

            var probe = (sender as Button).DataContext as Probe;
            if (probe.IsActive)
            {
                // Stop/cancel active probe.
                probe.StartStop();
            }
            _ProbeCollection.Remove(probe);
            RefreshColumnCount();
        }

        private void MultiInputWindowExecute(object sender, ExecutedRoutedEventArgs e)
        {
            // Get list of current addresses to send to multi-input window.
            var addresses = new List<string>();
            for (int i = 0; i < _ProbeCollection.Count; ++i)
            {
                if (!string.IsNullOrWhiteSpace(_ProbeCollection[i].Hostname))
                {
                    addresses.Add(_ProbeCollection[i].Hostname.Trim());
                }
            }

            var wnd = new MultiInputWindow(addresses)
            {
                Owner = this
            };
            if (wnd.ShowDialog() == true)
            {
                RemoveAllProbes();

                if (wnd.Addresses.Count < 1)
                {
                    AddProbe();
                }
                else
                {
                    AddProbe(numberOfProbes: wnd.Addresses.Count);
                    for (int i = 0; i < wnd.Addresses.Count; ++i)
                    {
                        _ProbeCollection[i].Hostname = wnd.Addresses[i];
                        _ProbeCollection[i].Alias = _Aliases.ContainsKey(_ProbeCollection[i].Hostname.ToLower())
                            ? _Aliases[_ProbeCollection[i].Hostname.ToLower()]
                            : null;
                        _ProbeCollection[i].StartStop();
                    }
                }

                // Trigger refresh on ColumnCount (To update binding on window grid, if needed).
                double count = ColumnCount.Value;
                ColumnCount.Value = 1;
                ColumnCount.Value = count;
            }
        }

        private void StartStopExecute(object sender, ExecutedRoutedEventArgs e)
        {
            string toggleStatus = StartStopMenuHeader.Text;

            foreach (var probe in _ProbeCollection)
            {
                if (toggleStatus == Strings.Toolbar_StopAll && probe.IsActive)
                {
                    probe.StartStop();
                }
                else if (toggleStatus == Strings.Toolbar_StartAll && !probe.IsActive)
                {
                    probe.StartStop();
                }
            }
        }

        private void HelpExecute(object sender, ExecutedRoutedEventArgs e)
        {
            if (HelpWindow._OpenWindow == null)
            {
                new HelpWindow().Show();
            }
            else
            {
                HelpWindow._OpenWindow.Activate();
            }
        }

        private void NewInstanceExecute(object sender, ExecutedRoutedEventArgs e)
        {
            LaunchNewInstance();
        }

        private void LaunchNewInstance()
        {
            try
            {
                var exePath = Environment.ProcessPath
                    ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrEmpty(exePath))
                {
                    var errorWindow = DialogWindow.ErrorWindow(Strings.Error_ExecutableNotFound);
                    errorWindow.Owner = this;
                    errorWindow.ShowDialog();
                    return;
                }

                var p = new System.Diagnostics.Process();
                p.StartInfo.FileName = exePath;
                p.Start();
            }

            catch (Exception ex)
            {
                var errorWindow = DialogWindow.ErrorWindow($"{Strings.Error_FailedToLaunch} {ex.Message}");
                errorWindow.Owner = this;
                errorWindow.ShowDialog();
            }
        }

        private void TracerouteExecute(object sender, ExecutedRoutedEventArgs e)
        {
            new TracerouteWindow().Show();
        }

        private void FloodHostExecute(object sender, ExecutedRoutedEventArgs e)
        {
            new FloodHostWindow().Show();
        }

        private void AddProbeExecute(object sender, ExecutedRoutedEventArgs e)
        {
            _ProbeCollection.Add(new Probe());
            RefreshColumnCount();
        }

        private void OptionsExecute(object sender, ExecutedRoutedEventArgs e)
        {
            // Open the options window.
            var optionsWnd = new OptionsWindow
            {
                Owner = this
            };
            if (optionsWnd.ShowDialog() == true)
            {
                RefreshGuiState();
                RefreshProbeColors();
                // Display mode is already applied live via SwitchDisplayMode() during
                // Options preview, so no additional ApplyDisplayMode() is needed here.
            }
        }

        /// <summary>
        /// Main-menu click handler for the display-mode toggle item.
        /// Uses the same centralized SwitchDisplayMode() as the tray toggle.
        /// </summary>
        private void ToggleDisplayMode_Click(object sender, RoutedEventArgs e)
        {
            var target = ApplicationOptions.CurrentDisplayMode == ApplicationOptions.DisplayMode.Compact
                ? ApplicationOptions.DisplayMode.Normal
                : ApplicationOptions.DisplayMode.Compact;
            SwitchDisplayMode(target);
        }

        private void RefreshProbeColors()
        {
            for (int i = 0; i < _ProbeCollection.Count; ++i)
            {
                _ProbeCollection[i].RefreshVisuals();
            }
        }

        private void RemoveAllProbes()
        {
            foreach (var probe in _ProbeCollection)
            {
                if (probe.IsActive)
                {
                    probe.StartStop();
                }
            }
            _ProbeCollection.Clear();
            Probe.ActiveCount = 0;
        }

        private void LoadFavorites()
        {
            // Clear existing favorites menu.
            for (int i = FavoritesMenu.Items.Count - 1; i > 2; --i)
            {
                FavoritesMenu.Items.RemoveAt(i);
            }

            // Load favorites.
            foreach (var fav in Favorite.GetTitles())
            {
                var menuItem = new MenuItem
                {
                    Header = fav
                };
                menuItem.Click += (s, r) =>
                {
                    LoadFavorite((s as MenuItem).Header.ToString());
                };

                FavoritesMenu.Items.Add(menuItem);
            }
        }

        private void LoadFavorite(string favoriteTitle)
        {
            RemoveAllProbes();

            var favorite = Favorite.Load(favoriteTitle);
            if (favorite.Hostnames.Count < 1)
            {
                AddProbe();
            }
            else
            {
                AddProbe(numberOfProbes: favorite.Hostnames.Count);
                for (int i = 0; i < favorite.Hostnames.Count; ++i)
                {
                    _ProbeCollection[i].Hostname = favorite.Hostnames[i];
                    _ProbeCollection[i].Alias = _Aliases.ContainsKey(_ProbeCollection[i].Hostname.ToLower())
                        ? _Aliases[_ProbeCollection[i].Hostname.ToLower()]
                        : null;
                    _ProbeCollection[i].StartStop();
                }
            }

            ColumnCount.Value = 1;  // Ensure window's grid column binding is updated, if needed.
            ColumnCount.Value = favorite.ColumnCount;
            this.Title = $"{favoriteTitle} - MultiPingMonitor";
        }

        private void LoadAliases()
        {
            _Aliases = Alias.GetAll();
            var aliasList = _Aliases.ToList();
            aliasList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

            // Clear existing aliases menu.
            for (int i = AliasesMenu.Items.Count - 1; i > 1; --i)
            {
                AliasesMenu.Items.RemoveAt(i);
            }

            // Load aliases.
            foreach (var alias in aliasList)
            {
                AliasesMenu.Items.Add(BuildAliasMenuItem(alias, false));
            }

            foreach (var probe in _ProbeCollection)
            {
                probe.Alias = probe.Hostname != null && _Aliases.ContainsKey(probe.Hostname.ToLower())
                    ? _Aliases[probe.Hostname.ToLower()]
                    : string.Empty;
            }
        }

        private MenuItem BuildAliasMenuItem(KeyValuePair<string, string> alias, bool isContextMenu)
        {
            var menuItem = new MenuItem
            {
                Header = alias.Value
            };

            if (isContextMenu)
            {
                menuItem.Click += (s, r) =>
                {
                    var selectedMenuItem = s as MenuItem;
                    var selectedAlias = (Probe)selectedMenuItem.DataContext;
                    selectedAlias.Hostname = _Aliases.FirstOrDefault(x => x.Value == selectedMenuItem.Header.ToString()).Key;
                    selectedAlias.StartStop();
                };
            }
            else
            {
                menuItem.Click += (s, r) =>
                {
                    var selectedAlias = s as MenuItem;

                    var didFindEmptyHost = false;
                    for (int i = 0; i < _ProbeCollection.Count; ++i)
                    {
                        if (string.IsNullOrWhiteSpace(_ProbeCollection[i].Hostname))
                        {
                            _ProbeCollection[i].Hostname = _Aliases.FirstOrDefault(x => x.Value == selectedAlias.Header.ToString()).Key;
                            _ProbeCollection[i].StartStop();
                            didFindEmptyHost = true;
                            break;
                        }
                    }

                    if (!didFindEmptyHost)
                    {
                        AddProbe();
                        _ProbeCollection[_ProbeCollection.Count - 1].Hostname = _Aliases.FirstOrDefault(x => x.Value == selectedAlias.Header.ToString()).Key;
                        _ProbeCollection[_ProbeCollection.Count - 1].StartStop();
                    }
                };
            }

            return menuItem;
        }

        private void CreateFavorite_Click(object sender, RoutedEventArgs e)
        {
            // Display new favorite window => Pass in current addresses and column count.
            // If window title ends with " - MultiPingMonitor", then user currently has a
            // favorite loaded. Pass the title of that favorite to the new window.
            const string favTitle = " - MultiPingMonitor";
            var newFavoriteWindow = new NewFavoriteWindow(
                hostList: _ProbeCollection.Select(x => x.Hostname).ToList(),
                columnCount: (int)ColumnCount.Value,
                title: Title.EndsWith(favTitle) ? Title.Remove(Title.Length - favTitle.Length) : string.Empty);
            newFavoriteWindow.Owner = this;
            if (newFavoriteWindow.ShowDialog() == true)
            {
                LoadFavorites();
            }
        }

        private void ManageFavorites_Click(object sender, RoutedEventArgs e)
        {
            // Open the favorites window.
            var manageFavoritesWindow = new ManageFavoritesWindow
            {
                Owner = this
            };
            manageFavoritesWindow.ShowDialog();
            LoadFavorites();
        }

        private void ManageAliases_Click(object sender, RoutedEventArgs e)
        {
            // Open the aliases window.
            var manageAliasesWindow = new ManageAliasesWindow
            {
                Owner = this
            };
            manageAliasesWindow.ShowDialog();
            LoadAliases();
        }

        private void PopupAlways_Click(object sender, RoutedEventArgs e)
        {
            PopupAlways.IsChecked = true;
            PopupNever.IsChecked = false;
            PopupWhenMinimized.IsChecked = false;
            ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Always;
        }

        private void PopupNever_Click(object sender, RoutedEventArgs e)
        {
            PopupAlways.IsChecked = false;
            PopupNever.IsChecked = true;
            PopupWhenMinimized.IsChecked = false;
            ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Never;
        }

        private void PopupWhenMinimized_Click(object sender, RoutedEventArgs e)
        {
            PopupAlways.IsChecked = false;
            PopupNever.IsChecked = false;
            PopupWhenMinimized.IsChecked = true;
            ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.WhenMinimized;
        }

        private void IsolatedView_Click(object sender, RoutedEventArgs e)
        {
            var probe = (sender as Button).DataContext as Probe;
            if (probe.LivePingMonitorWindow != null && probe.LivePingMonitorWindow.IsLoaded)
            {
                probe.LivePingMonitorWindow.Activate();
            }
            else
            {
                var window = new LivePingMonitorWindow(probe, this);
                probe.LivePingMonitorWindow = window;
                window.Show();
            }
        }

        private void CompactItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            if (ApplicationOptions.CurrentDisplayMode != ApplicationOptions.DisplayMode.Compact)
                return;

            var probe = (sender as FrameworkElement)?.DataContext as Probe;
            if (probe == null)
                return;

            if (probe.LivePingMonitorWindow != null && probe.LivePingMonitorWindow.IsLoaded)
            {
                probe.LivePingMonitorWindow.Activate();
            }
            else
            {
                var window = new LivePingMonitorWindow(probe, this);
                probe.LivePingMonitorWindow = window;
                window.Show();
            }

            e.Handled = true;
        }

        /// <summary>
        /// Opens a Live Ping Monitor window for every host in the current compact
        /// data set, reusing already-open windows, and then arranges them.
        /// </summary>
        /// <param name="cascade">true for Cascade layout; false for Tile layout.</param>
        private void OpenAllLiveWindowsAndArrange(bool cascade)
        {
            // Determine the active compact probe collection.
            var probes = ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.NormalTargets
                ? _ProbeCollection
                : _CompactProbeCollection;

            if (probes.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    Strings.LivePing_CompactSetEmpty,
                    "MultiPingMonitor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            LivePingMonitorWindow firstWindow = null;

            foreach (var probe in probes)
            {
                if (probe.LivePingMonitorWindow != null && probe.LivePingMonitorWindow.IsLoaded)
                {
                    // Reuse the existing window.
                    probe.LivePingMonitorWindow.Activate();
                }
                else
                {
                    var window = new LivePingMonitorWindow(probe, this);
                    probe.LivePingMonitorWindow = window;
                    window.Show();
                }

                firstWindow ??= probe.LivePingMonitorWindow;
            }

            // Arrange all open live windows (the arrange service operates on the
            // full LiveWindowRegistry, which now contains all windows we just opened).
            if (firstWindow != null)
            {
                if (cascade)
                    WindowArrangeService.Cascade(firstWindow);
                else
                    WindowArrangeService.Tile(firstWindow);
            }
        }

        /// <summary>
        /// Opens a new empty Live Ping Monitor window in manual/direct mode.
        /// The user enters a target and starts ping manually in that window.
        /// Multiple manual windows can be opened independently.
        /// </summary>
        private void NewLivePingMenu_Click(object sender, RoutedEventArgs e)
        {
            var window = new LivePingMonitorWindow(this);
            window.Show();
        }

        private void EditAlias_Click(object sender, RoutedEventArgs e)
        {
            var probe = (sender as Button).DataContext as Probe;

            if (string.IsNullOrEmpty(probe.Hostname))
            {
                return;
            }

            if (_Aliases.ContainsKey(probe.Hostname.ToLower()))
            {
                probe.Alias = _Aliases[probe.Hostname.ToLower()];
            }
            else
            {
                probe.Alias = string.Empty;
            }

            var wnd = new EditAliasWindow(probe)
            {
                Owner = this
            };

            if (wnd.ShowDialog() == true)
            {
                LoadAliases();
            }
            Focus();
        }

        private void StatusHistoryExecute(object sender, ExecutedRoutedEventArgs e)
        {
            if (Probe.StatusHistoryWindow == null || Probe.StatusHistoryWindow.IsLoaded == false)
            {
                var wnd = new StatusHistoryWindow(Probe.StatusChangeLog);
                Probe.StatusHistoryWindow = wnd;
                wnd.Show();
            }
            else if (Probe.StatusHistoryWindow.IsLoaded)
            {
                Probe.StatusHistoryWindow.Focus();
            }
        }

        private void Hostname_Loaded(object sender, RoutedEventArgs e)
        {
            // Set focus to textbox on newly added monitors.  If the hostname field is blank for any existing monitors, do not change focus.
            for (int i = 0; i < _ProbeCollection.Count - 1; ++i)
            {
                if (string.IsNullOrEmpty(_ProbeCollection[i].Hostname))
                {
                    return;
                }
            }
            ((TextBox)sender).Focus();
        }

        private void Hostname_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Check if there is an alias for the hostname as you type.
            var probe = (sender as TextBox).DataContext as Probe;
            if (probe.Hostname != null)
            {
                probe.Alias = _Aliases.ContainsKey(probe.Hostname.ToLower())
                    ? _Aliases[probe.Hostname.ToLower()]
                    : null;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Set initial focus first text box.
            if (_ProbeCollection.Count > 0)
            {
                var cp = ProbeItemsControl.ItemContainerGenerator.ContainerFromIndex(0) as ContentPresenter;
                var tb = (TextBox)cp.ContentTemplate.FindName("Hostname", cp);
                tb?.Focus();
            }
        }

        private void Logo_TargetUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {
            // This event is tied to the background image that appears in each probe window.
            // After a probe is started, this event removes the image from the ItemsControl.
            var image = (sender as Image);
            if (image.Visibility == Visibility.Collapsed)
            {
                image.Visibility = Visibility.Collapsed;
                image.Source = null;
            }
        }

        private void ProbeTitle_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe)
            {
                var data = new DataObject();
                data.SetData("Source", fe.DataContext as Probe);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
                e.Handled = true;
            }
        }

        private void History_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void Probe_Drop(object sender, DragEventArgs e)
        {
            var source = e.Data.GetData("Source") as Probe;
            if (source != null && sender is FrameworkElement fe)
            {
                int newIndex = _ProbeCollection.IndexOf(fe.DataContext as Probe);
                e.Handled = true;

                int prevIndex = _ProbeCollection.IndexOf(source);
                if (newIndex != prevIndex)
                {
                    _ProbeCollection.RemoveAt(prevIndex);
                    _ProbeCollection.Insert(newIndex, source);
                }
            }
        }

        // ── Native WinForms tray context menu ─────────────────────────────────
        // A ContextMenuStrip attached directly to the NotifyIcon.
        // WinForms handles right-click display, focus capture, and auto-dismiss
        // natively, making this far more reliable than WPF popup hosting.
        // See BuildNativeTrayMenu() for construction and event wiring.

        private void InitializeTrayIcon()
        {
            // ── Step 1: Load icons (with system-icon fallback so this never fails) ──
            try
            {
                _trayIconNeutral = LoadTrayIcon("pack://application:,,,/Resources/tray-neutral.ico")
                                   ?? System.Drawing.SystemIcons.Application;
                _trayIconOnline  = LoadTrayIcon("pack://application:,,,/Resources/tray-online.ico")
                                   ?? _trayIconNeutral;
                _trayIconOffline = LoadTrayIcon("pack://application:,,,/Resources/tray-offline.ico")
                                   ?? _trayIconNeutral;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MultiPingMonitor: Could not load tray icons: {ex}");
                _trayIconNeutral  = System.Drawing.SystemIcons.Application;
                _trayIconOnline   = _trayIconNeutral;
                _trayIconOffline  = _trayIconNeutral;
            }

            // ── Step 2: Create NotifyIcon FIRST so it always appears, even if the
            //           themed menu fails to build in a subsequent step. ──────────
            try
            {
                NotifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = _trayIconNeutral,
                    Text = Strings.Tray_Status_NoHosts,
                    Visible = true
                };
                NotifyIcon.MouseUp += NotifyIcon_MouseUp;

                // Subscribe to probe collection changes to track per-probe status.
                _ProbeCollection.CollectionChanged += ProbeCollection_CollectionChanged;
                _CompactProbeCollection.CollectionChanged += ProbeCollection_CollectionChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MultiPingMonitor: Failed to create NotifyIcon: {ex}");
                return; // Nothing more can be done without the NotifyIcon itself.
            }

            // ── Step 3: Build native WinForms ContextMenuStrip and attach to NotifyIcon ─
            // Building the native menu is simple and never throws for normal reasons, but
            // guard anyway so a failure here does not kill the tray icon itself.
            try
            {
                _trayNativeMenu = BuildNativeTrayMenu();
                NotifyIcon.ContextMenuStrip = _trayNativeMenu;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MultiPingMonitor: Failed to build native tray menu: {ex}");
                _trayNativeMenu = null;
            }
        }

        /// <summary>
        /// Loads a System.Drawing.Icon from an embedded WPF pack URI, returning null on failure.
        /// </summary>
        private static System.Drawing.Icon LoadTrayIcon(string packUri)
        {
            try
            {
                var sri = Application.GetResourceStream(new Uri(packUri));
                if (sri == null) return null;
                using (var s = sri.Stream)
                    return new System.Drawing.Icon(s);
            }
            catch (Exception ex) when (ex is System.IO.IOException
                                    || ex is ArgumentException
                                    || ex is System.IO.InvalidDataException
                                    || ex is UriFormatException)
            {
                System.Diagnostics.Trace.WriteLine($"MultiPingMonitor: Could not load tray icon '{packUri}'.");
                return null;
            }
        }

        /// <summary>
        /// Called when probes are added to or removed from the collection.
        /// Subscribes/unsubscribes PropertyChanged on each probe and refreshes the tray icon.
        /// </summary>
        private void ProbeCollection_CollectionChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                // ObservableCollection.Clear() fires Reset with OldItems=null.
                // Use the shadow set to unsubscribe all previously tracked probes.
                foreach (var p in _subscribedProbes)
                    p.PropertyChanged -= Probe_PropertyChanged;
                _subscribedProbes.Clear();
            }
            if (e.NewItems != null)
            {
                foreach (Probe p in e.NewItems)
                {
                    if (_subscribedProbes.Add(p))
                        p.PropertyChanged += Probe_PropertyChanged;
                }

                // When new probes are added to the Normal/Main collection while Compact
                // custom-set mode is active, mark them suppressed immediately so they
                // don't fire notifications from outside the active monitoring context.
                if (ReferenceEquals(sender, _ProbeCollection) && ShouldSuppressNormalProbeNotifications())
                {
                    foreach (Probe p in e.NewItems)
                        p.SuppressNotifications = true;
                }
            }
            if (e.OldItems != null)
            {
                foreach (Probe p in e.OldItems)
                {
                    if (_subscribedProbes.Remove(p))
                        p.PropertyChanged -= Probe_PropertyChanged;
                }
            }
            UpdateTrayIcon();
        }

        /// <summary>
        /// Called when any property on a monitored probe changes.
        /// Refreshes the tray icon when the probe's Status changes.
        /// </summary>
        private void Probe_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Probe.Status) || e.PropertyName == nameof(Probe.IsActive))
                UpdateTrayIcon();
        }

        /// <summary>
        /// Returns the probe collection that should be used for tray aggregate state.
        /// – Normal mode: always _ProbeCollection
        /// – Compact + NormalTargets: _ProbeCollection (read-only display of Normal data)
        /// – Compact + CustomTargets: _CompactProbeCollection (independent compact data)
        /// </summary>
        private ObservableCollection<Probe> GetActiveTrayProbeCollection()
        {
            if (ApplicationOptions.CurrentDisplayMode == ApplicationOptions.DisplayMode.Compact
                && ApplicationOptions.CompactSource == ApplicationOptions.CompactSourceMode.CustomTargets)
            {
                return _CompactProbeCollection;
            }
            return _ProbeCollection;
        }

        /// <summary>
        /// Evaluates the aggregate status of all probes and updates the tray icon and tooltip text.
        /// Rules:
        ///   – Red   : at least one active probe is Down or Error
        ///   – Green : at least one active probe has a meaningful host and all active ones are Up/LatencyHigh/LatencyNormal
        ///   – Gray  : otherwise (no active probes with hostnames, or all indeterminate/inactive)
        /// Safe to call from any thread; all state evaluation and NotifyIcon updates run on the UI thread.
        /// </summary>
        private void UpdateTrayIcon()
        {
            if (NotifyIcon == null) return;

            // Dispatch fully to the UI thread: both the collection iteration and the
            // NotifyIcon update must happen on the same thread (UI thread).
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (NotifyIcon == null) return;

                // Determine aggregate state across all probes that are active and have a hostname.
                bool anyOffline = false;
                bool anyOnline  = false;

                foreach (var probe in GetActiveTrayProbeCollection())
                {
                    if (!probe.IsActive || string.IsNullOrWhiteSpace(probe.Hostname))
                        continue;

                    switch (probe.Status)
                    {
                        case ProbeStatus.Down:
                        case ProbeStatus.Error:
                            anyOffline = true;
                            break;
                        case ProbeStatus.Up:
                        case ProbeStatus.LatencyHigh:
                        case ProbeStatus.LatencyNormal:
                            anyOnline = true;
                            break;
                    }
                }

                TrayAggregateState newState;
                if (anyOffline)
                    newState = TrayAggregateState.Offline;
                else if (anyOnline)
                    newState = TrayAggregateState.Online;
                else
                    newState = TrayAggregateState.Neutral;

                if (newState == _trayState) return;
                _trayState = newState;

                switch (newState)
                {
                    case TrayAggregateState.Online:
                        NotifyIcon.Icon = _trayIconOnline;
                        NotifyIcon.Text = Strings.Tray_Status_AllOnline;
                        break;
                    case TrayAggregateState.Offline:
                        NotifyIcon.Icon = _trayIconOffline;
                        NotifyIcon.Text = Strings.Tray_Status_SomeOffline;
                        break;
                    default:
                        NotifyIcon.Icon = _trayIconNeutral;
                        NotifyIcon.Text = Strings.Tray_Status_NoHosts;
                        break;
                }
            }));
        }

        /// <summary>
        /// Builds the native WinForms ContextMenuStrip for the tray icon.
        /// Attaching it to NotifyIcon.ContextMenuStrip means WinForms handles
        /// right-click display, focus capture, and auto-dismiss natively.
        /// </summary>
        private System.Windows.Forms.ContextMenuStrip BuildNativeTrayMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();

            // Font parity: Segoe UI 9pt = WPF Style.FontSize.Menu (12 DIP = 9pt at 96 DPI).
            menu.Font = new System.Drawing.Font("Segoe UI", 9f,
                System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);

            // Icon gutter / item height parity: 20×20 scaling makes the icon column
            // and row height match WPF menu dimensions more closely.
            // WinForms default ImageScalingSize(16,16) produces ~22px rows; (20,20)
            // drives rows to ~26-28px — matching WPF Modern ~27px and Classic ~24px.
            menu.ImageScalingSize = new System.Drawing.Size(20, 20);

            // Unify check/icon column: WPF menus have no separate check margin column.
            // Checked items show their mark in the image slot (handled by OnRenderItemCheck).
            menu.ShowCheckMargin = false;

            // Sync Classic/Modern check marks and re-apply the theme renderer
            // every time the menu is about to open so both the style and the
            // current theme palette are always reflected correctly.
            menu.Opening += (s, e) =>
            {
                UpdateTrayStyleChecks();
                ApplyTrayMenuTheme();
            };

            // Apply genuine rounded-corner shape on Modern: clip the popup window
            // Region to a GraphicsPath with rounded corners so the corners are
            // physically transparent (not just painted differently inside a
            // rectangular window).  Must use Opened (not Opening) so Width/Height
            // are final; Resize re-applies when the menu re-flows (e.g. DPI change).
            menu.Opened += (s, e) => ApplyTrayPopupRegion(menu);
            menu.Resize += (s, e) => ApplyTrayPopupRegion(menu);

            menu.Items.Add(MakeItem(Strings.Menu_NewLivePing,   () => Dispatcher.Invoke(() => NewLivePingMenu_Click(null, null)), TrayIcon.NewLivePing));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(MakeItem(Strings.Tray_Open,        () => Dispatcher.Invoke(ShowMainWindowFromTray), TrayIcon.Open));
            menu.Items.Add(MakeItem(Strings.Tray_NewInstance, () => Dispatcher.Invoke(LaunchNewInstance),       TrayIcon.NewInstance));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            menu.Items.Add(MakeItem(Strings.Menu_Traceroute,    () => Dispatcher.Invoke(() => TracerouteExecute(null, null)),    TrayIcon.Traceroute));
            menu.Items.Add(MakeItem(Strings.Menu_FloodHost,     () => Dispatcher.Invoke(() => FloodHostExecute(null, null)),     TrayIcon.FloodHost));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(MakeItem(Strings.Tray_Options,       () => Dispatcher.Invoke(() => OptionsExecute(null, null)),       TrayIcon.Options));
            menu.Items.Add(MakeItem(Strings.Tray_StatusHistory, () => Dispatcher.Invoke(() => StatusHistoryExecute(null, null)), TrayIcon.StatusHistory));
            menu.Items.Add(MakeItem(Strings.Menu_Help,          () => Dispatcher.Invoke(() => HelpExecute(null, null)),          TrayIcon.Help));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // ── Visual style submenu ──────────────────────────────────────────
            var styleParent = new System.Windows.Forms.ToolStripMenuItem(Strings.Tray_VisualStyle)
            {
                Image = MakeTrayMenuBitmap(TrayIcon.VisualStyle),
                Tag   = TrayIcon.VisualStyle
            };

            _trayNativeStyleClassic = new System.Windows.Forms.ToolStripMenuItem("Classic")
            {
                CheckOnClick = false
            };
            _trayNativeStyleClassic.Click += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    VisualStyleManager.ApplyStyle(VisualStyle.Classic);
                    ApplicationOptions.VisualStyle = VisualStyleManager.GetStyleName(VisualStyle.Classic);
                    Configuration.Save();
                    UpdateTrayStyleChecks();
                });
            };

            _trayNativeStyleModern = new System.Windows.Forms.ToolStripMenuItem("Modern")
            {
                CheckOnClick = false
            };
            _trayNativeStyleModern.Click += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    VisualStyleManager.ApplyStyle(VisualStyle.Modern);
                    ApplicationOptions.VisualStyle = VisualStyleManager.GetStyleName(VisualStyle.Modern);
                    Configuration.Save();
                    UpdateTrayStyleChecks();
                });
            };

            styleParent.DropDownItems.Add(_trayNativeStyleClassic);
            styleParent.DropDownItems.Add(_trayNativeStyleModern);
            menu.Items.Add(styleParent);
            UpdateTrayStyleChecks();

            // Apply the same rounded-Region treatment to the Visual style submenu popup.
            styleParent.DropDown.Opened += (s, e) => ApplyTrayPopupRegion(styleParent.DropDown);
            styleParent.DropDown.Resize += (s, e) => ApplyTrayPopupRegion(styleParent.DropDown);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // ── Display mode quick toggle ─────────────────────────────────────
            _trayNativeToggleItem = MakeItem(
                ApplicationOptions.CurrentDisplayMode == ApplicationOptions.DisplayMode.Compact
                    ? Strings.Tray_SwitchToNormal
                    : Strings.Tray_SwitchToCompact,
                () => Dispatcher.Invoke(() =>
                {
                    var target = ApplicationOptions.CurrentDisplayMode == ApplicationOptions.DisplayMode.Compact
                        ? ApplicationOptions.DisplayMode.Normal
                        : ApplicationOptions.DisplayMode.Compact;
                    SwitchDisplayMode(target);
                }),
                TrayIcon.ToggleDisplay);
            _trayNativeToggleItem.Font = new System.Drawing.Font(
                "Segoe UI", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            menu.Items.Add(_trayNativeToggleItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            menu.Items.Add(MakeItem(Strings.Tray_Exit, () => Dispatcher.Invoke(() =>
            {
                _IsShuttingDown = true;
                Application.Current.Shutdown();
            }), TrayIcon.Exit));

            // Apply dark theme immediately so the first open already looks correct.
            ApplyTrayMenuTheme();

            return menu;
        }

        /// <summary>
        /// Convenience factory: creates a ToolStripMenuItem with the given text, click action,
        /// and an optional GDI+ icon drawn to match the current theme text color.
        /// The icon kind is stored in Tag so it can be regenerated when the theme changes.
        /// </summary>
        private static System.Windows.Forms.ToolStripMenuItem MakeItem(string text, Action onClick, int iconKind = -1)
        {
            var item = new System.Windows.Forms.ToolStripMenuItem(text);
            if (iconKind >= 0)
            {
                item.Image = MakeTrayMenuBitmap(iconKind);
                item.Tag   = iconKind;
            }
            item.Click += (s, e) => onClick();
            return item;
        }

        /// <summary>
        /// Updates the checked state of the Classic/Modern tray style menu items
        /// to reflect the currently active visual style.
        /// </summary>
        private void UpdateTrayStyleChecks()
        {
            if (_trayNativeStyleClassic == null || _trayNativeStyleModern == null)
                return;

            bool isClassic = VisualStyleManager.CurrentStyle == VisualStyle.Classic;
            _trayNativeStyleClassic.Checked = isClassic;
            _trayNativeStyleModern.Checked  = !isClassic;
        }

        /// <summary>
        /// Clips the tray popup window to a rounded-corner shape by setting a
        /// Region derived from a GraphicsPath.  This makes the popup corners
        /// physically transparent rather than just painted differently inside a
        /// rectangular window — matching the WPF Modern popup corner feel.
        ///
        /// Modern:  radius = 8, matching WPF Style.ContextMenu CornerRadius = 8.
        /// Classic: Region is cleared (null) — rectangular, conservative shape.
        ///
        /// Must be called after the popup is fully sized (Opened / Resize events)
        /// so Width/Height are valid.  Any previously set Region is disposed to
        /// avoid GDI handle leaks.
        /// </summary>
        private static void ApplyTrayPopupRegion(System.Windows.Forms.ToolStrip strip)
        {
            bool modern = VisualStyleManager.CurrentStyle == VisualStyle.Modern;
            int r = modern ? 8 : 0;

            // Capture existing Region before overwriting so we can dispose it.
            var old = strip.Region;

            if (r <= 0)
            {
                strip.Region = null;
                old?.Dispose();
                return;
            }

            int d = r * 2;
            int w = strip.Width;
            int h = strip.Height;

            // Guard against degenerate dimensions that can occur during early resize events.
            if (w < d || h < d)
            {
                strip.Region = null;
                old?.Dispose();
                return;
            }

            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0,     0,     d, d, 180, 90);
            path.AddArc(w - d, 0,     d, d, 270, 90);
            path.AddArc(w - d, h - d, d, d,   0, 90);
            path.AddArc(0,     h - d, d, d,  90, 90);
            path.CloseFigure();

            strip.Region = new System.Drawing.Region(path);
            old?.Dispose();
        }

        /// <summary>
        /// Applies the current app theme and visual style to the native tray
        /// ContextMenuStrip by installing a custom renderer that reads colors live
        /// from the WPF application resources. Called at construction and on every
        /// Opening event so theme/style switches are immediately visible.
        /// </summary>
        private void ApplyTrayMenuTheme()
        {
            if (_trayNativeMenu == null)
                return;

            _trayNativeMenu.Renderer = new TrayMenuRenderer();

            // Font parity: keep the strip font at Segoe UI 9pt so it always matches
            // the WPF menu font even if the system default font differs.
            var menuFont = new System.Drawing.Font("Segoe UI", 9f,
                System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            _trayNativeMenu.Font = menuFont;

            // Icon gutter / item height: re-assert 20×20 so any system reset is overridden.
            _trayNativeMenu.ImageScalingSize = new System.Drawing.Size(20, 20);

            // Set BackColor/ForeColor on the strip itself as a fallback for any
            // non-renderer codepath (e.g., the system drawing the window border).
            if (Application.Current?.TryFindResource("Theme.Surface") is System.Windows.Media.SolidColorBrush surfaceBrush)
            {
                var c = surfaceBrush.Color;
                _trayNativeMenu.BackColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            if (Application.Current?.TryFindResource("Theme.Text.Primary") is System.Windows.Media.SolidColorBrush textBrush)
            {
                var c = textBrush.Color;
                var foreColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                _trayNativeMenu.ForeColor = foreColor;
                SetTrayItemColors(_trayNativeMenu.Items, foreColor);
            }

            // Regenerate icons in the current theme/palette colors so they always
            // match the active theme regardless of when the menu was first built.
            RefreshTrayItemIcons(_trayNativeMenu.Items);

            // Item padding calibration.
            // With ImageScalingSize(20,20), WinForms auto-sizes items to ~26px (20px icon +
            // internal margin). WPF Modern menu rows are ~27px; Classic ~24px.
            // Vertical padding values are kept SMALL so the image size drives height, not
            // the padding. Previous values (6/4px) made items ~34px — taller than WPF.
            // Modern: top/bottom 2px adds minimal breathing room without overshooting.
            // Classic: top/bottom 1px keeps the tighter WPF Classic rhythm.
            bool isModern = VisualStyleManager.CurrentStyle == VisualStyle.Modern;
            var itemPadding = isModern
                ? new System.Windows.Forms.Padding(4, 2, 6, 2)  // Modern: image height drives row to ~27px
                : new System.Windows.Forms.Padding(4, 1, 4, 1); // Classic: image height drives row to ~24px
            SetTrayItemPadding(_trayNativeMenu.Items, itemPadding);

            // Propagate font, ImageScalingSize, and ShowCheckMargin=false to all
            // sub-dropdowns (e.g., "Visual style"). WinForms ToolStripDropDownMenu
            // instances inherit the renderer from the parent but NOT ShowCheckMargin,
            // Font, or ImageScalingSize — so the sub-menu would show an extra check
            // column and smaller icon gutter without explicit propagation.
            SetTrayDropDownProps(_trayNativeMenu.Items, menuFont, isModern);
        }

        /// <summary>
        /// Recursively sets ForeColor on all ToolStripItems so WinForms has the
        /// right text color regardless of which rendering path is used.
        /// </summary>
        private static void SetTrayItemColors(
            System.Windows.Forms.ToolStripItemCollection items,
            System.Drawing.Color foreColor)
        {
            foreach (System.Windows.Forms.ToolStripItem item in items)
            {
                item.ForeColor = foreColor;
                if (item is System.Windows.Forms.ToolStripMenuItem mi && mi.DropDownItems.Count > 0)
                    SetTrayItemColors(mi.DropDownItems, foreColor);
            }
        }

        /// <summary>
        /// Recursively regenerates the bitmap icon for every item whose Tag holds
        /// an icon-kind integer, so icons always reflect the current theme colors.
        /// </summary>
        private static void RefreshTrayItemIcons(
            System.Windows.Forms.ToolStripItemCollection items)
        {
            foreach (System.Windows.Forms.ToolStripItem item in items)
            {
                if (item.Tag is int kind)
                    item.Image = MakeTrayMenuBitmap(kind);
                if (item is System.Windows.Forms.ToolStripMenuItem mi && mi.DropDownItems.Count > 0)
                    RefreshTrayItemIcons(mi.DropDownItems);
            }
        }

        /// <summary>
        /// Recursively sets Padding on all ToolStripMenuItems so item height
        /// matches the active WPF menu style (Modern vs Classic vertical rhythm).
        /// </summary>
        private static void SetTrayItemPadding(
            System.Windows.Forms.ToolStripItemCollection items,
            System.Windows.Forms.Padding padding)
        {
            foreach (System.Windows.Forms.ToolStripItem item in items)
            {
                if (item is System.Windows.Forms.ToolStripMenuItem menuItem)
                {
                    menuItem.Padding = padding;
                    if (menuItem.DropDownItems.Count > 0)
                        SetTrayItemPadding(menuItem.DropDownItems, padding);
                }
            }
        }

        /// <summary>
        /// Recursively propagates font, ImageScalingSize, and ShowCheckMargin to all
        /// sub-ToolStripDropDownMenu instances (e.g., the "Visual style" submenu).
        /// WinForms inherits the renderer from the parent ContextMenuStrip but does NOT
        /// inherit Font, ImageScalingSize, or ShowCheckMargin — so without explicit
        /// propagation the submenu shows an extra check column and smaller icon gutter.
        /// </summary>
        private static void SetTrayDropDownProps(
            System.Windows.Forms.ToolStripItemCollection items,
            System.Drawing.Font font,
            bool isModern)
        {
            foreach (System.Windows.Forms.ToolStripItem item in items)
            {
                if (item is System.Windows.Forms.ToolStripMenuItem mi && mi.DropDownItems.Count > 0)
                {
                    mi.DropDown.Font            = font;
                    mi.DropDown.ImageScalingSize = new System.Drawing.Size(20, 20);
                    // Remove the separate WinForms check column so the sub-menu column
                    // layout is identical to the root menu (check shown in image slot).
                    if (mi.DropDown is System.Windows.Forms.ToolStripDropDownMenu ddm)
                        ddm.ShowCheckMargin = false;
                    SetTrayDropDownProps(mi.DropDownItems, font, isModern);
                }
            }
        }

        // ── P/Invoke for reliable bring-to-front ────────────────────────────
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Hides the main window to the system tray without shutting down.
        /// </summary>
        private void HideMainWindowToTray()
        {
            if (_IsHiddenToTray)
                return;
            _IsHiddenToTray = true;
            ShowInTaskbar = false;
            Visibility = Visibility.Hidden;
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Restores the main window from the system tray, ensuring it is visible,
        /// in Normal state, activated, and brought to the foreground.
        /// </summary>
        private void ShowMainWindowFromTray()
        {
            _IsHiddenToTray = false;
            Visibility = Visibility.Visible;
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();

            // Reliable bring-to-front on Windows.
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                SetForegroundWindow(hwnd);
        }

        /// <summary>
        /// Toggles the main window between visible and hidden-to-tray.
        /// Called on left single click of the tray icon.
        /// </summary>
        private void ToggleMainWindowFromTray()
        {
            if (_IsHiddenToTray || !IsVisible || WindowState == WindowState.Minimized)
                ShowMainWindowFromTray();
            else
                HideMainWindowToTray();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && ApplicationOptions.IsMinimizeToTrayEnabled)
            {
                HideMainWindowToTray();
            }

            // Toggle maximize / restore title bar buttons.
            if (maximizeButton != null && restoreButton != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    maximizeButton.Visibility = Visibility.Collapsed;
                    restoreButton.Visibility = Visibility.Visible;
                }
                else
                {
                    maximizeButton.Visibility = Visibility.Visible;
                    restoreButton.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Handles notify icon cleanup when MultiPingMonitor is hidden to tray
            // and the window is made visible again (e.g. via a popup alert click).
            if (IsVisible && _IsHiddenToTray)
            {
                ShowMainWindowFromTray();
            }
        }

        private void NotifyIcon_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // Left single click: toggle window visibility.
                // The right-click context menu is handled natively by
                // NotifyIcon.ContextMenuStrip; no explicit code needed here.
                ToggleMainWindowFromTray();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[MainWindow] Window_Closing: IsExitToTrayEnabled={ApplicationOptions.IsExitToTrayEnabled}" +
                $" _IsShuttingDown={_IsShuttingDown} WindowState={WindowState}" +
                $" Left={Left} Top={Top} Width={Width} Height={Height}");

            if (ApplicationOptions.IsExitToTrayEnabled && !_IsShuttingDown)
            {
                System.Diagnostics.Trace.WriteLine("[MainWindow] Window_Closing: hiding to tray (cancel close).");
                HideMainWindowToTray();
                e.Cancel = true;
            }
            else
            {
                // Capture MainWindow placement before serializing config.
                // Save under the current display mode's key so each mode keeps its own bounds.
                System.Diagnostics.Trace.WriteLine("[MainWindow] Window_Closing: saving placement and config.");
                WindowPlacementService.SaveWindow(this, PlacementKeyForMode(ApplicationOptions.CurrentDisplayMode));

                // Stop any active compact probes before shutdown.
                foreach (var probe in _CompactProbeCollection)
                {
                    if (probe.IsActive)
                        probe.StartStop();
                }

                Configuration.Save();
                NotifyIcon?.Dispose();
                _trayNativeMenu?.Dispose();
            }
        }

        private void History_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            tb.SelectionStart = (tb.DataContext as Probe).SelStart;
            tb.SelectionLength = (tb.DataContext as Probe).SelLength;
            if (!tb.IsMouseCaptureWithin && tb.SelectionLength == 0)
            {
                tb.ScrollToEnd();
            }
        }

        private void History_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            (tb.DataContext as Probe).SelStart = tb.SelectionStart;
            (tb.DataContext as Probe).SelLength = tb.SelectionLength;
        }

        // ── Custom title bar button handlers ─────────────────────────────────

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ── Edge snap implementation ──────────────────────────────────────────

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(EdgeSnapWndProc);
        }

        private IntPtr EdgeSnapWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Only snap while the window is in Normal (restored) state.
            if (WindowState != WindowState.Normal)
                return IntPtr.Zero;

            if (msg == WM_MOVING)
            {
                // lParam points to a RECT with the proposed window position.
                var rect = Marshal.PtrToStructure<RECT>(lParam);
                var wa = GetWorkingAreaForRect(rect);
                SnapMovingRect(ref rect, wa);
                Marshal.StructureToPtr(rect, lParam, false);
                handled = true;
            }
            else if (msg == WM_SIZING)
            {
                // lParam points to a RECT with the proposed window bounds.
                // wParam identifies which edge / corner is being dragged.
                var rect = Marshal.PtrToStructure<RECT>(lParam);
                var wa = GetWorkingAreaForRect(rect);
                SnapSizingRect(ref rect, (int)wParam, wa);
                Marshal.StructureToPtr(rect, lParam, false);
                handled = true;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Returns the working area of the monitor that best contains <paramref name="rect"/>.
        /// Falls back to the primary monitor's working area when no monitor matches.
        /// </summary>
        private static System.Drawing.Rectangle GetWorkingAreaForRect(RECT rect)
        {
            var r = new System.Drawing.Rectangle(rect.Left, rect.Top,
                rect.Right - rect.Left, rect.Bottom - rect.Top);
            var screen = System.Windows.Forms.Screen.FromRectangle(r);
            return screen?.WorkingArea ?? System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
        }

        /// <summary>
        /// Applies soft edge snapping to a window rect being moved (all four
        /// edges move together as a unit).  Only adjusts if the overshoot or
        /// gap is within <see cref="EdgeSnapThresholdPx"/>.
        /// Horizontal and vertical axes are evaluated independently so that
        /// corner snapping (e.g. left+top) works correctly.
        /// </summary>
        private static void SnapMovingRect(ref RECT rect, System.Drawing.Rectangle wa)
        {
            int width  = rect.Right  - rect.Left;
            int height = rect.Bottom - rect.Top;

            // ── X axis: snap left or right, whichever is closer (nearest wins). ──
            int distLeft  = Math.Abs(rect.Left  - wa.Left);
            int distRight = Math.Abs(rect.Right - wa.Right);

            if (distLeft <= EdgeSnapThresholdPx && distLeft <= distRight)
            {
                rect.Left  = wa.Left;
                rect.Right = rect.Left + width;
            }
            else if (distRight <= EdgeSnapThresholdPx)
            {
                rect.Right = wa.Right;
                rect.Left  = rect.Right - width;
            }

            // ── Y axis: snap top or bottom, whichever is closer (nearest wins). ──
            int distTop    = Math.Abs(rect.Top    - wa.Top);
            int distBottom = Math.Abs(rect.Bottom - wa.Bottom);

            if (distTop <= EdgeSnapThresholdPx && distTop <= distBottom)
            {
                rect.Top    = wa.Top;
                rect.Bottom = rect.Top + height;
            }
            else if (distBottom <= EdgeSnapThresholdPx)
            {
                rect.Bottom = wa.Bottom;
                rect.Top    = rect.Bottom - height;
            }
        }

        /// <summary>
        /// Applies soft edge snapping to a window rect being resized.  Only the
        /// edges that are actually being dragged are snapped, so the opposite
        /// edges remain stable.
        /// </summary>
        private static void SnapSizingRect(ref RECT rect, int sizingEdge, System.Drawing.Rectangle wa)
        {
            bool snapLeft   = sizingEdge == WMSZ_LEFT   || sizingEdge == WMSZ_TOPLEFT  || sizingEdge == WMSZ_BOTTOMLEFT;
            bool snapRight  = sizingEdge == WMSZ_RIGHT  || sizingEdge == WMSZ_TOPRIGHT || sizingEdge == WMSZ_BOTTOMRIGHT;
            bool snapTop    = sizingEdge == WMSZ_TOP    || sizingEdge == WMSZ_TOPLEFT  || sizingEdge == WMSZ_TOPRIGHT;
            bool snapBottom = sizingEdge == WMSZ_BOTTOM || sizingEdge == WMSZ_BOTTOMLEFT || sizingEdge == WMSZ_BOTTOMRIGHT;

            if (snapLeft   && Math.Abs(rect.Left   - wa.Left  ) <= EdgeSnapThresholdPx)
                rect.Left   = wa.Left;
            if (snapRight  && Math.Abs(rect.Right  - wa.Right ) <= EdgeSnapThresholdPx)
                rect.Right  = wa.Right;
            if (snapTop    && Math.Abs(rect.Top    - wa.Top   ) <= EdgeSnapThresholdPx)
                rect.Top    = wa.Top;
            if (snapBottom && Math.Abs(rect.Bottom - wa.Bottom) <= EdgeSnapThresholdPx)
                rect.Bottom = wa.Bottom;
        }

        // ── Native tray menu dark theming ─────────────────────────────────────────
        //
        // TrayMenuColorTable reads WPF theme colors live on every property access
        // so the menu always matches whichever theme/palette is currently active.
        // TrayMenuRenderer wraps it and controls text color per item state:
        //   - Normal/Classic: Theme.Text.Primary (light on dark surface)
        //   - Selected+Modern: Theme.AccentForeground (dark on accent bg for contrast)
        // MakeTrayMenuBitmap draws simple GDI+ icons in the current text color.

        /// <summary>
        /// Icon kind identifiers used by MakeTrayMenuBitmap.
        /// </summary>
        private static class TrayIcon
        {
            public const int Open         = 0;
            public const int NewInstance  = 1;
            public const int Traceroute   = 2;
            public const int FloodHost    = 3;
            public const int Options      = 4;
            public const int StatusHistory= 5;
            public const int Help         = 6;
            public const int VisualStyle  = 7;
            public const int ToggleDisplay= 8;
            public const int Exit         = 9;
            public const int NewLivePing  = 10;
        }

        /// <summary>
        /// Creates a 20×20 GDI+ bitmap icon for the tray menu, drawn in the
        /// current theme text color so it is always readable on the dark background.
        /// 20×20 matches ImageScalingSize(20,20) exactly (no scaling artefacts) and
        /// produces an icon column that visually matches WPF menu icon area dimensions.
        /// Geometry is scaled ×1.25 from the original 16×16 designs.
        /// </summary>
        private static System.Drawing.Bitmap MakeTrayMenuBitmap(int kind)
        {
            var bmp = new System.Drawing.Bitmap(20, 20,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            System.Drawing.Color ic = GetThemeDrawingColor("Theme.Text.Primary",
                System.Drawing.Color.FromArgb(0xCD, 0xD6, 0xF4));
            System.Drawing.Color ac = GetThemeDrawingColor("Theme.Accent",
                System.Drawing.Color.FromArgb(0x89, 0xB4, 0xFA));

            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            // Pen weight scaled from 1.5px (16px canvas) to 1.9px (20px canvas).
            using var pen = new System.Drawing.Pen(ic, 1.9f);
            using var brush = new System.Drawing.SolidBrush(ic);
            using var accentBrush = new System.Drawing.SolidBrush(ac);

            switch (kind)
            {
                case TrayIcon.Open:
                    // Window outline + outward arrow (coords × 1.25 from 16 px original)
                    g.DrawRectangle(pen, 1f, 4f, 10f, 10f);
                    g.DrawLine(pen, 12f, 9f, 18f, 9f);
                    g.DrawLine(pen, 14f, 4f, 18f, 9f);
                    g.DrawLine(pen, 14f, 14f, 18f, 9f);
                    break;

                case TrayIcon.NewInstance:
                    // Two overlapping window outlines (coords × 1.25)
                    g.DrawRectangle(pen, 6f, 1f, 10f, 9f);
                    g.FillRectangle(new System.Drawing.SolidBrush(
                        GetThemeDrawingColor("Theme.Surface", System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3E))),
                        1f, 6f, 11f, 10f);
                    g.DrawRectangle(pen, 1f, 6f, 10f, 9f);
                    break;

                case TrayIcon.Traceroute:
                    // Dashed path with arrowhead (coords × 1.25)
                    using (var dash = new System.Drawing.Pen(ic, 1.5f)
                        { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    {
                        g.DrawLine(dash, 1f, 10f, 13f, 10f);
                    }
                    g.DrawLine(pen, 10f, 6f, 17f, 10f);
                    g.DrawLine(pen, 10f, 14f, 17f, 10f);
                    break;

                case TrayIcon.FloodHost:
                    // Lightning bolt (filled polygon, coords × 1.25)
                    g.FillPolygon(brush, new System.Drawing.PointF[]
                    {
                        new(11f,1f), new(6f,10f), new(10f,10f),
                        new(9f,18f), new(14f,9f), new(10f,9f)
                    });
                    break;

                case TrayIcon.Options:
                    // Gear: circle + 4 teeth at cardinal directions (coords × 1.25)
                    g.DrawEllipse(pen, 5f, 5f, 9f, 9f);
                    g.DrawLine(pen, 9.5f, 1f,  9.5f, 5f);
                    g.DrawLine(pen, 9.5f, 14f, 9.5f, 18f);
                    g.DrawLine(pen, 1f,  9.5f, 5f,   9.5f);
                    g.DrawLine(pen, 14f, 9.5f, 18f,  9.5f);
                    break;

                case TrayIcon.StatusHistory:
                    // Clock face (coords × 1.25)
                    g.DrawEllipse(pen, 2f, 2f, 15f, 15f);
                    g.DrawLine(pen, 9.5f, 6f, 9.5f, 10f);
                    g.DrawLine(pen, 9.5f, 10f, 13f, 10f);
                    break;

                case TrayIcon.Help:
                    // Circle + "?" glyph (coords × 1.25)
                    g.DrawEllipse(pen, 2f, 2f, 15f, 15f);
                    using (var f = new System.Drawing.Font("Segoe UI", 8f,
                        System.Drawing.FontStyle.Bold,
                        System.Drawing.GraphicsUnit.Point))
                    {
                        g.DrawString("?", f, brush, 5f, 3.5f);
                    }
                    break;

                case TrayIcon.VisualStyle:
                    // Classic vs Modern: two small squares, second in accent color (coords × 1.25)
                    g.FillRectangle(brush,       1f,  4f, 7f, 11f);
                    g.FillRectangle(accentBrush, 11f, 4f, 7f, 11f);
                    g.DrawLine(pen, 9.5f, 1f, 9.5f, 18f);
                    break;

                case TrayIcon.ToggleDisplay:
                    // Two-headed horizontal arrow (swap, coords × 1.25)
                    g.DrawLine(pen, 2f, 10f, 17f, 10f);
                    g.DrawLine(pen, 6f, 6f,  2f,  10f);
                    g.DrawLine(pen, 6f, 14f, 2f,  10f);
                    g.DrawLine(pen, 13f, 6f,  17f, 10f);
                    g.DrawLine(pen, 13f, 14f, 17f, 10f);
                    break;

                case TrayIcon.Exit:
                    // Door outline + outward arrow (coords × 1.25)
                    g.DrawRectangle(pen, 1f, 2f, 8f, 14f);
                    g.DrawLine(pen, 10f, 10f, 18f, 10f);
                    g.DrawLine(pen, 14f, 6f,  18f, 10f);
                    g.DrawLine(pen, 14f, 14f, 18f, 10f);
                    break;

                case TrayIcon.NewLivePing:
                    // Window outline + play-triangle inside (live ping concept)
                    g.DrawRectangle(pen, 1f, 3f, 14f, 13f);
                    g.DrawLine(pen, 1f, 7f, 15f, 7f);
                    g.FillPolygon(accentBrush, new System.Drawing.PointF[]
                    {
                        new(5f, 10f), new(12f, 13f), new(5f, 16f)
                    });
                    break;
            }

            return bmp;
        }

        /// <summary>
        /// Reads a WPF SolidColorBrush theme resource and converts it to a GDI+ Color.
        /// Falls back to <paramref name="fallback"/> if the resource is unavailable.
        /// </summary>
        private static System.Drawing.Color GetThemeDrawingColor(string key, System.Drawing.Color fallback)
        {
            try
            {
                if (Application.Current?.TryFindResource(key) is System.Windows.Media.SolidColorBrush b)
                {
                    var c = b.Color;
                    return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
            }
            catch { }
            return fallback;
        }

        /// <summary>
        /// ProfessionalColorTable that reads all colors from the WPF application theme
        /// resources at access time so any palette or visual-style switch is immediately
        /// reflected without needing to recreate the renderer.
        /// </summary>
        private sealed class TrayMenuColorTable : System.Windows.Forms.ProfessionalColorTable
        {
            // ── Color helpers ─────────────────────────────────────────────────
            private static System.Drawing.Color Get(string key, System.Drawing.Color fallback)
            {
                try
                {
                    if (Application.Current?.TryFindResource(key) is System.Windows.Media.SolidColorBrush b)
                    {
                        var c = b.Color;
                        return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                    }
                }
                catch { }
                return fallback;
            }

            private static System.Drawing.Color Surface     => Get("Theme.Surface",     System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3E));
            private static System.Drawing.Color SurfaceAlt  => Get("Theme.SurfaceAlt",  System.Drawing.Color.FromArgb(0x36, 0x36, 0x50));
            private static System.Drawing.Color Border      => Get("Theme.Border",      System.Drawing.Color.FromArgb(0x44, 0x44, 0x5A));
            private static System.Drawing.Color Accent      => Get("Theme.Accent",      System.Drawing.Color.FromArgb(0x89, 0xB4, 0xFA));
            private static System.Drawing.Color AccentHover => Get("Theme.AccentHover", System.Drawing.Color.FromArgb(0x74, 0xC7, 0xEC));

            private static bool IsModern    => VisualStyleManager.CurrentStyle == VisualStyle.Modern;
            // Modern: accent highlight; Classic: subtle surface-alt highlight
            private static System.Drawing.Color HighlightBg => IsModern ? Accent      : SurfaceAlt;
            private static System.Drawing.Color PressedBg   => IsModern ? AccentHover : Border;
            // Classic image margin: subtle SurfaceAlt gutter matches WPF Classic icon column.
            // Modern image margin: same Surface as background — no visible gutter, matching
            // WPF Modern menu items whose icon area has no separate background tint.
            private static System.Drawing.Color ImageMargin => IsModern ? Surface : SurfaceAlt;

            // ── ProfessionalColorTable overrides ──────────────────────────────
            public override System.Drawing.Color ToolStripDropDownBackground   => Surface;
            public override System.Drawing.Color MenuBorder                    => IsModern ? Accent : Border;
            public override System.Drawing.Color MenuItemBorder                => IsModern ? Accent : Border;
            public override System.Drawing.Color MenuItemSelected              => HighlightBg;
            public override System.Drawing.Color MenuItemSelectedGradientBegin => HighlightBg;
            public override System.Drawing.Color MenuItemSelectedGradientEnd   => HighlightBg;
            public override System.Drawing.Color MenuItemPressedGradientBegin  => PressedBg;
            public override System.Drawing.Color MenuItemPressedGradientMiddle => PressedBg;
            public override System.Drawing.Color MenuItemPressedGradientEnd    => PressedBg;
            public override System.Drawing.Color ImageMarginGradientBegin      => ImageMargin;
            public override System.Drawing.Color ImageMarginGradientMiddle     => ImageMargin;
            public override System.Drawing.Color ImageMarginGradientEnd        => ImageMargin;
            public override System.Drawing.Color SeparatorLight                => Surface;
            public override System.Drawing.Color SeparatorDark                 => Border;
            public override System.Drawing.Color CheckBackground               => HighlightBg;
            public override System.Drawing.Color CheckSelectedBackground       => PressedBg;
            public override System.Drawing.Color CheckPressedBackground        => PressedBg;
            public override System.Drawing.Color ButtonCheckedGradientBegin    => HighlightBg;
            public override System.Drawing.Color ButtonCheckedGradientMiddle   => HighlightBg;
            public override System.Drawing.Color ButtonCheckedGradientEnd      => HighlightBg;
            public override System.Drawing.Color ButtonSelectedGradientBegin   => HighlightBg;
            public override System.Drawing.Color ButtonSelectedGradientMiddle  => HighlightBg;
            public override System.Drawing.Color ButtonSelectedGradientEnd     => HighlightBg;
            public override System.Drawing.Color ButtonPressedGradientBegin    => PressedBg;
            public override System.Drawing.Color ButtonPressedGradientMiddle   => PressedBg;
            public override System.Drawing.Color ButtonPressedGradientEnd      => PressedBg;
            public override System.Drawing.Color ToolStripGradientBegin        => Surface;
            public override System.Drawing.Color ToolStripGradientMiddle       => Surface;
            public override System.Drawing.Color ToolStripGradientEnd          => Surface;
            public override System.Drawing.Color ToolStripBorder               => IsModern ? Accent : Border;
            public override System.Drawing.Color GripLight                     => Surface;
            public override System.Drawing.Color GripDark                      => Border;
        }

        /// <summary>
        /// ToolStripProfessionalRenderer backed by TrayMenuColorTable.
        /// Controls text, arrow, background, border, image, separator, and check-mark
        /// rendering per item state to ensure the menu feels like the same family as
        /// the WPF menus:
        ///   Classic normal/hover: Theme.Text.Primary on SurfaceAlt flat highlight
        ///   Modern  normal:       Theme.Text.Primary
        ///   Modern  hover/pressed:Theme.AccentForeground on rounded Accent highlight
        ///   Modern  border:       inset rounded rect (r=4) in Accent — simulates WPF popup corners
        ///   Classic border:       flat 1 px Border rect
        /// </summary>
        private sealed class TrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
        {
            public TrayMenuRenderer() : base(new TrayMenuColorTable()) { }

            /// <summary>
            /// Returns the appropriate text/arrow color for an item, choosing between
            /// the primary text color and AccentForeground depending on state + style.
            /// </summary>
            private static System.Drawing.Color ItemForeColor(System.Windows.Forms.ToolStripItem item)
            {
                bool active = item.Selected || item.Pressed;
                bool modern = VisualStyleManager.CurrentStyle == VisualStyle.Modern;
                if (active && modern)
                {
                    // Accent background in Modern — use the dedicated AccentForeground
                    // (dark) so the text/arrow has correct contrast against the light accent.
                    return GetThemeDrawingColor("Theme.AccentForeground",
                        System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x2E));
                }
                return GetThemeDrawingColor("Theme.Text.Primary",
                    System.Drawing.Color.FromArgb(0xCD, 0xD6, 0xF4));
            }

            /// <summary>
            /// Fills the entire menu background with the Surface color so no
            /// system-default gradient or color bleeds through from the base renderer.
            /// </summary>
            protected override void OnRenderToolStripBackground(
                System.Windows.Forms.ToolStripRenderEventArgs e)
            {
                using var bg = new System.Drawing.SolidBrush(
                    GetThemeDrawingColor("Theme.Surface",
                        System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3E)));
                e.Graphics.FillRectangle(bg, e.AffectedBounds);
            }

            /// <summary>
            /// Draws the outer popup border.
            /// Modern: 1 px inset rounded rectangle with Accent color (r = 8) matching the
            ///   WPF Modern popup CornerRadius = 8.  The popup window is also clipped to the
            ///   same shape via ApplyTrayPopupRegion so the corners are physically transparent —
            ///   not just drawn differently inside a rectangular window.
            /// Classic: 1 px straight border in Theme.Border — matches WPF Classic popup.
            /// </summary>
            protected override void OnRenderToolStripBorder(
                System.Windows.Forms.ToolStripRenderEventArgs e)
            {
                bool modern = VisualStyleManager.CurrentStyle == VisualStyle.Modern;
                var borderColor = modern
                    ? GetThemeDrawingColor("Theme.Accent",
                        System.Drawing.Color.FromArgb(0x89, 0xB4, 0xFA))
                    : GetThemeDrawingColor("Theme.Border",
                        System.Drawing.Color.FromArgb(0x44, 0x44, 0x5A));

                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new System.Drawing.Pen(borderColor, 1f);

                if (modern)
                {
                    // Inset by 0.5 so the 1 px line sits exactly on the inner edge of the
                    // clipped Region.  Radius 8 matches WPF Style.ContextMenu CornerRadius = 8.
                    var rect = new System.Drawing.RectangleF(
                        0.5f, 0.5f, e.ToolStrip.Width - 1f, e.ToolStrip.Height - 1f);
                    DrawRoundedRect(g, pen, rect, 8f);
                }
                else
                {
                    g.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                }
            }

            protected override void OnRenderItemText(
                System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = ItemForeColor(e.Item);
                // Ensure vertical centering flag is always present so text sits optically
                // centered in the row regardless of how WinForms initialises the flags.
                e.TextFormat |= System.Windows.Forms.TextFormatFlags.VerticalCenter;
                base.OnRenderItemText(e);
            }

            /// <summary>
            /// Draws the item icon, vertically centering it in the full item height so
            /// the icon optical axis matches the text baseline. WinForms positions icons
            /// relative to ImageRectangle.Y which can be asymmetric with custom padding.
            /// </summary>
            protected override void OnRenderItemImage(
                System.Windows.Forms.ToolStripItemImageRenderEventArgs e)
            {
                if (e.Image == null) return;
                var img = e.Image;
                var r   = e.ImageRectangle;
                // Vertically center the icon in the full item row height.
                int destY = (e.Item.Height - img.Height) / 2;
                e.Graphics.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.DrawImage(img, r.X, destY, img.Width, img.Height);
            }

            protected override void OnRenderArrow(
                System.Windows.Forms.ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = ItemForeColor(e.Item);
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                if (e.Direction == System.Windows.Forms.ArrowDirection.Right)
                {
                    // Draw a clean filled triangle arrow pointing right — matches the feel
                    // of the RightArrow Path in WPF MenuItemStyle.xaml.
                    var r  = e.ArrowRectangle;
                    float cx = r.X + r.Width  / 2f;
                    float cy = r.Y + r.Height / 2f;
                    // Filled right-pointing triangle:
                    // 2.5 px half-width left of center, 4 px half-height, 2 px tip right of center.
                    // These proportions (aspect ≈ 0.6) match the WPF RightArrow path geometry.
                    using var brush = new System.Drawing.SolidBrush(e.ArrowColor);
                    g.FillPolygon(brush, new System.Drawing.PointF[]
                    {
                        new(cx - 2.5f, cy - 4f),
                        new(cx + 2f,   cy),
                        new(cx - 2.5f, cy + 4f)
                    });
                }
                else
                {
                    // Fallback for scrollable-menu up/down arrows (rare in this menu).
                    base.OnRenderArrow(e);
                }
            }

            /// <summary>
            /// Draws the image margin (icon gutter). Modern: invisible (same Surface as
            /// the menu background) to match WPF Modern menus. Classic: subtle SurfaceAlt
            /// tint matching the WPF Classic icon column feel.
            /// </summary>
            protected override void OnRenderImageMargin(
                System.Windows.Forms.ToolStripRenderEventArgs e)
            {
                bool modern = VisualStyleManager.CurrentStyle == VisualStyle.Modern;
                var color = modern
                    ? GetThemeDrawingColor("Theme.Surface",
                        System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3E))
                    : GetThemeDrawingColor("Theme.SurfaceAlt",
                        System.Drawing.Color.FromArgb(0x36, 0x36, 0x50));
                using var bg = new System.Drawing.SolidBrush(color);
                e.Graphics.FillRectangle(bg, e.AffectedBounds);
            }

            /// <summary>
            /// Draws the hover/pressed background. Modern: filled rounded rectangle with
            /// Accent color — matches WPF Modern menu item hover. Classic: flat SurfaceAlt
            /// rectangle — matches WPF Classic menu item hover.
            /// </summary>
            protected override void OnRenderMenuItemBackground(
                System.Windows.Forms.ToolStripItemRenderEventArgs e)
            {
                // Always fill the full row with the surface color first.
                using var bgBrush = new System.Drawing.SolidBrush(
                    GetThemeDrawingColor("Theme.Surface",
                        System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3E)));
                e.Graphics.FillRectangle(bgBrush, 0, 0, e.Item.Width, e.Item.Height);

                if (!e.Item.Selected && !e.Item.Pressed) return;

                bool modern = VisualStyleManager.CurrentStyle == VisualStyle.Modern;
                var hlColor = modern
                    ? GetThemeDrawingColor("Theme.Accent",
                        System.Drawing.Color.FromArgb(0x89, 0xB4, 0xFA))
                    : GetThemeDrawingColor("Theme.SurfaceAlt",
                        System.Drawing.Color.FromArgb(0x36, 0x36, 0x50));

                // Horizontal inset keeps the highlight 2 px from the outer menu border on
                // each side, giving the same visual breathing room seen in WPF menu items
                // where the IsHighlighted border rect is inset from the item template root.
                const int inset = 2;
                var rect = new System.Drawing.RectangleF(
                    inset, 1f, e.Item.Width - inset * 2, e.Item.Height - 2f);

                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var hlBrush = new System.Drawing.SolidBrush(hlColor);
                if (modern)
                    FillRoundedRect(e.Graphics, hlBrush, rect, 4f);
                else
                    e.Graphics.FillRectangle(hlBrush, rect.X, rect.Y, rect.Width, rect.Height);
            }

            /// <summary>
            /// Draws a separator line that aligns with the WPF menu separator feel.
            /// Modern: faint accent-tinted full-width line. Classic: Border-colored
            /// line inset past the icon gutter.
            /// </summary>
            protected override void OnRenderSeparator(
                System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
            {
                bool modern = VisualStyleManager.CurrentStyle == VisualStyle.Modern;

                using var bg = new System.Drawing.SolidBrush(
                    GetThemeDrawingColor("Theme.Surface",
                        System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3E)));
                e.Graphics.FillRectangle(bg, 0, 0, e.Item.Width, e.Item.Height);

                var lineBase = modern
                    ? GetThemeDrawingColor("Theme.Accent",
                        System.Drawing.Color.FromArgb(0x89, 0xB4, 0xFA))
                    : GetThemeDrawingColor("Theme.Border",
                        System.Drawing.Color.FromArgb(0x44, 0x44, 0x5A));
                // Modern: alpha 120/255 ≈ 47 % opacity — visible accent tint matches
                //   WPF Modern separator which uses a thin accent-colored line.
                //   Previous value (70) was too faint to distinguish from the background.
                // Classic: alpha 180/255 ≈ 71 % opacity — opaque enough to clearly
                //   separate sections against the SurfaceAlt gutter.
                var lineColor = System.Drawing.Color.FromArgb(modern ? 120 : 180, lineBase);

                // Classic: start line after the ~30 px icon-gutter column so the
                // separator aligns with the text column, matching WPF Classic menu layout
                // (the icon column in WPF SubmenuHeaderTemplate is MinWidth=22 + 3 px gap
                // + ~5 px inner padding ≈ 30 px effective gutter).
                // Modern: start at 4 px for a near-full-width line (no visible gutter).
                int x1 = modern ? 4 : 30;
                int y  = e.Item.Height / 2;
                int x2 = e.Item.Width - 4;

                using var pen = new System.Drawing.Pen(lineColor, 1f);
                e.Graphics.DrawLine(pen, x1, y, x2, y);
            }

            /// <summary>
            /// Draws the checkmark for checked menu items (Classic/Modern style toggle).
            /// Renders a clean anti-aliased tick glyph vertically centered in the row.
            /// Uses item.Height rather than ImageRectangle.Y to avoid asymmetric
            /// placement caused by custom padding.
            /// </summary>
            protected override void OnRenderItemCheck(
                System.Windows.Forms.ToolStripItemImageRenderEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var color = ItemForeColor(e.Item);

                // Centre on item height (not e.ImageRectangle.Y) for optical alignment.
                float cx = e.ImageRectangle.X + e.ImageRectangle.Width  / 2f;
                float cy = e.Item.Height / 2f;

                using var pen = new System.Drawing.Pen(color, 1.5f)
                {
                    LineJoin    = System.Drawing.Drawing2D.LineJoin.Round,
                    StartCap    = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap      = System.Drawing.Drawing2D.LineCap.Round
                };
                // Two-stroke tick: a short descending stroke (the foot of the check,
                // 4 px left of center → 1 px left + 3.5 px below), then a longer
                // ascending stroke (1 px left + 3.5 px below → 4 px right + 3 px above).
                // Shape matches the WPF Checkmark path geometry "F1 M 10,1.2 L 4.7,9.1 ...".
                g.DrawLine(pen, cx - 4f, cy,        cx - 1f, cy + 3.5f);
                g.DrawLine(pen, cx - 1f, cy + 3.5f, cx + 4f, cy - 3f);
            }

            /// <summary>
            /// Fills a rectangle with rounded corners using the given brush.
            /// Falls back to a plain rectangle when radius is zero or negative.
            /// </summary>
            private static void FillRoundedRect(
                System.Drawing.Graphics g,
                System.Drawing.Brush brush,
                System.Drawing.RectangleF rect,
                float radius)
            {
                if (radius <= 0f)
                {
                    g.FillRectangle(brush, rect);
                    return;
                }
                float d = radius * 2f;
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(rect.X,          rect.Y,          d, d, 180, 90);
                path.AddArc(rect.Right - d,  rect.Y,          d, d, 270, 90);
                path.AddArc(rect.Right - d,  rect.Bottom - d, d, d,   0, 90);
                path.AddArc(rect.X,          rect.Bottom - d, d, d,  90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }

            /// <summary>
            /// Draws an unfilled rounded-corner rectangle with the given pen.
            /// Falls back to a plain rectangle when radius is zero or negative.
            /// Used for the outer popup border on Modern style.
            /// </summary>
            private static void DrawRoundedRect(
                System.Drawing.Graphics g,
                System.Drawing.Pen pen,
                System.Drawing.RectangleF rect,
                float radius)
            {
                if (radius <= 0f)
                {
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    return;
                }
                float d = radius * 2f;
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(rect.X,          rect.Y,          d, d, 180, 90);
                path.AddArc(rect.Right - d,  rect.Y,          d, d, 270, 90);
                path.AddArc(rect.Right - d,  rect.Bottom - d, d, d,   0, 90);
                path.AddArc(rect.X,          rect.Bottom - d, d, d,  90, 90);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
        }
    }
}