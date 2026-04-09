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

        // ── Dynamic tray icon ─────────────────────────────────────────────────
        // Three icons representing aggregate host status: neutral (no hosts),
        // online (all up), offline (at least one down).
        private System.Drawing.Icon _trayIconNeutral;
        private System.Drawing.Icon _trayIconOnline;
        private System.Drawing.Icon _trayIconOffline;

        // Tracks the last aggregate state to avoid redundant icon/tooltip updates.
        private enum TrayAggregateState { Neutral, Online, Offline }
        private TrayAggregateState _trayState = TrayAggregateState.Neutral;

        // Set to true when a deliberate application shutdown is initiated from the tray exit
        // menu item, so Window_Closing knows to save placement and config instead of re-hiding.
        private bool _IsShuttingDown = false;

        // Set to true when the main window is hidden to the system tray.
        private bool _IsHiddenToTray = false;

        // Set to true once startup content (probes, CLI args) has been initialized.
        // Prevents double-initialization when the window is first shown after a tray-only startup.
        private bool _startupContentInitialized = false;

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
            WindowPlacementService.Attach(this, "MainWindow");
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
            RefreshGuiState();

            // Set items source for main GUI ItemsControl.
            ProbeItemsControl.ItemsSource = _ProbeCollection;
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

            // Set always on top state.
            Topmost = ApplicationOptions.IsAlwaysOnTopEnabled;
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
            }
        }

        private void RefreshProbeColors()
        {
            for (int i = 0; i < _ProbeCollection.Count; ++i)
            {
                _ProbeCollection[i].Status = _ProbeCollection[i].Status;
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
            if (probe.IsolatedWindow == null || probe.IsolatedWindow.IsLoaded == false)
            {
                new IsolatedPingWindow(probe).Show();
            }
            else if (probe.IsolatedWindow.IsLoaded)
            {
                probe.IsolatedWindow.Focus();
            }
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
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var data = new DataObject();
                data.SetData("Source", (sender as Label).DataContext as Probe);
                DragDrop.DoDragDrop(sender as DependencyObject, data, DragDropEffects.Move);
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
            if (source != null)
            {
                int newIndex;
                if (sender is Label)
                {
                    newIndex = _ProbeCollection.IndexOf((sender as Label).DataContext as Probe);
                    e.Handled = true;
                }
                else if (sender is DockPanel)
                {
                    newIndex = _ProbeCollection.IndexOf((sender as DockPanel).DataContext as Probe);
                    e.Handled = true;
                }
                else
                {
                    return;
                }

                int prevIndex = _ProbeCollection.IndexOf(source);
                if (newIndex != prevIndex)
                {
                    _ProbeCollection.RemoveAt(prevIndex);
                    _ProbeCollection.Insert(newIndex, source);
                }
            }
        }

        // ── Themed tray context menu ───────────────────────────────────────────
        // WPF ContextMenu replaces the old WinForms ContextMenuStrip so the tray
        // menu respects the current application theme. A tiny invisible "host"
        // window provides the HWND needed by WPF to display the popup near the
        // tray icon and to receive foreground activation (required for the menu
        // to close when the user clicks outside).
        private ContextMenu _trayContextMenu;
        private Window _trayMenuHost;

        private void InitializeTrayIcon()
        {
            try
            {
                // Load the three state-specific tray icons from embedded resources.
                // Fall back gracefully if any are missing.
                _trayIconNeutral = LoadTrayIcon("pack://application:,,,/Resources/tray-neutral.ico")
                                   ?? System.Drawing.SystemIcons.Application;
                _trayIconOnline  = LoadTrayIcon("pack://application:,,,/Resources/tray-online.ico")
                                   ?? _trayIconNeutral;
                _trayIconOffline = LoadTrayIcon("pack://application:,,,/Resources/tray-offline.ico")
                                   ?? _trayIconNeutral;

                // Build themed WPF context menu for the tray icon.
                _trayContextMenu = BuildTrayContextMenu();

                // Create a tiny, invisible helper window that owns the ContextMenu.
                // This ensures the menu gets a valid HWND for positioning and that
                // Windows gives it foreground focus so it auto-closes on outside click.
                _trayMenuHost = new Window
                {
                    Width = 0,
                    Height = 0,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    AllowsTransparency = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Topmost = true
                };
                _trayMenuHost.Show();
                _trayMenuHost.Hide();

                // Create tray icon. No WinForms ContextMenuStrip – right-click is
                // handled in NotifyIcon_MouseUp to show the themed WPF menu instead.
                // Start with neutral icon; UpdateTrayIcon() will refine once probes run.
                NotifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = _trayIconNeutral,
                    Text = Strings.Tray_Status_NoHosts,
                    Visible = true
                };
                NotifyIcon.MouseUp += NotifyIcon_MouseUp;

                // Subscribe to probe collection changes so we can track each probe's status.
                _ProbeCollection.CollectionChanged += ProbeCollection_CollectionChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"MultiPingMonitor: Failed to initialize tray icon: {ex}");
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
            catch
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
                // Collection was cleared; unsubscribe all is handled implicitly since
                // we re-subscribe on every Add. No lingering subscriptions cause harm
                // because removed probes are no longer in the collection.
            }
            if (e.NewItems != null)
            {
                foreach (Probe p in e.NewItems)
                    p.PropertyChanged += Probe_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (Probe p in e.OldItems)
                    p.PropertyChanged -= Probe_PropertyChanged;
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

                foreach (var probe in _ProbeCollection)
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
        /// Builds a theme-aware WPF ContextMenu with the same items and actions
        /// that the old WinForms ContextMenuStrip had.
        /// </summary>
        private ContextMenu BuildTrayContextMenu()
        {
            var menu = new ContextMenu();

            // Apply theme-aware brushes explicitly. The ContextMenu popup lives in
            // its own visual tree and does not inherit theme colors from the main
            // window shell, so we bind directly to Theme.* brush keys via
            // SetResourceReference (code equivalent of DynamicResource).  When the
            // user switches theme, the brushes re-resolve automatically.
            menu.SetResourceReference(Control.BackgroundProperty, "Theme.Surface");
            menu.SetResourceReference(Control.BorderBrushProperty, "Theme.Border");
            menu.SetResourceReference(Control.ForegroundProperty, "Theme.Text.Primary");
            menu.BorderThickness = new Thickness(1);
            menu.Padding = new Thickness(2);

            menu.Items.Add(CreateTrayMenuItem(Strings.Tray_Open, (s, e) => RestoreFromTray()));
            menu.Items.Add(CreateTrayMenuItem(Strings.Tray_NewInstance, (s, e) => LaunchNewInstance()));
            menu.Items.Add(CreateTraySeparator());
            menu.Items.Add(CreateTrayMenuItem(Strings.Menu_Traceroute, (s, e) => TracerouteExecute(null, null)));
            menu.Items.Add(CreateTrayMenuItem(Strings.Menu_FloodHost, (s, e) => FloodHostExecute(null, null)));
            menu.Items.Add(CreateTraySeparator());
            menu.Items.Add(CreateTrayMenuItem(Strings.Tray_Options, (s, e) => OptionsExecute(null, null)));
            menu.Items.Add(CreateTrayMenuItem(Strings.Tray_StatusHistory, (s, e) => StatusHistoryExecute(null, null)));
            menu.Items.Add(CreateTrayMenuItem(Strings.Menu_Help, (s, e) => HelpExecute(null, null)));
            menu.Items.Add(CreateTraySeparator());
            menu.Items.Add(CreateTrayMenuItem(Strings.Tray_Exit, (s, e) =>
            {
                _IsShuttingDown = true;
                Application.Current.Shutdown();
            }));

            // Subscribe once: hide the host window when the menu closes.
            menu.Closed += TrayContextMenu_Closed;

            return menu;
        }

        private static MenuItem CreateTrayMenuItem(string header, RoutedEventHandler clickHandler)
        {
            var item = new MenuItem { Header = header };
            // Ensure text is readable: bind Foreground to theme text brush so it
            // stays correct in both light and dark themes and across theme switches.
            item.SetResourceReference(Control.ForegroundProperty, "Theme.Text.Primary");
            item.Click += clickHandler;
            return item;
        }

        private static Separator CreateTraySeparator()
        {
            var sep = new Separator();
            sep.SetResourceReference(Separator.BackgroundProperty, "Theme.Border");
            sep.Margin = new Thickness(4, 2, 4, 2);
            return sep;
        }

        /// <summary>
        /// Shows the themed WPF tray context menu near the cursor position.
        /// Uses the hidden helper window to get foreground activation so the
        /// menu closes correctly when the user clicks elsewhere.
        /// </summary>
        private void ShowTrayContextMenu()
        {
            if (_trayContextMenu == null || _trayMenuHost == null)
                return;

            // Get cursor position (physical pixels).
            var cursorPos = System.Windows.Forms.Cursor.Position;

            // Convert physical screen pixels to WPF device-independent units.
            var source = PresentationSource.FromVisual(_trayMenuHost)
                         ?? HwndSource.FromHwnd(new WindowInteropHelper(_trayMenuHost).Handle);
            double dpiScaleX = 1.0, dpiScaleY = 1.0;
            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformFromDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformFromDevice.M22;
            }

            double x = cursorPos.X * dpiScaleX;
            double y = cursorPos.Y * dpiScaleY;

            // Position the helper window at the cursor so the ContextMenu opens there.
            _trayMenuHost.Left = x;
            _trayMenuHost.Top = y;
            _trayMenuHost.Show();
            _trayMenuHost.Activate();

            _trayContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            _trayContextMenu.PlacementTarget = _trayMenuHost;
            _trayContextMenu.IsOpen = true;
        }

        private void TrayContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            _trayMenuHost?.Hide();
        }

        private void HideToTray()
        {
            if (_IsHiddenToTray)
                return;
            _IsHiddenToTray = true;
            Visibility = Visibility.Hidden;
            WindowState = WindowState.Minimized;
        }

        private void RestoreFromTray()
        {
            _IsHiddenToTray = false;
            WindowState = WindowState.Minimized;
            Visibility = Visibility.Visible;
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && ApplicationOptions.IsMinimizeToTrayEnabled)
            {
                HideToTray();
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
                RestoreFromTray();
            }
        }

        private void NotifyIcon_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // Left click. Restore application window.
                RestoreFromTray();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                // Right click. Display themed WPF context menu at cursor position.
                ShowTrayContextMenu();
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
                HideToTray();
                e.Cancel = true;
            }
            else
            {
                // Capture MainWindow placement before serializing config.
                // WindowPlacementService.Attach registers its Closing handler after the
                // XAML-declared Window_Closing, so Configuration.Save would otherwise
                // run before the placement dict is updated for this window.
                System.Diagnostics.Trace.WriteLine("[MainWindow] Window_Closing: saving placement and config.");
                WindowPlacementService.SaveWindow(this, "MainWindow");
                Configuration.Save();
                NotifyIcon?.Dispose();
                _trayMenuHost?.Close();
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
    }
}