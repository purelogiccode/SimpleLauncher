using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Converters;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.ContextMenus;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.GameScan;
using SimpleLauncher.Avalonia.Services.LoadingOverlay;
using SimpleLauncher.Avalonia.Services.QuitOrReinstall;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Avalonia.Services.SystemSelectionOrchestrator;
using SimpleLauncher.Avalonia.Services.Theme;
using SimpleLauncher.Avalonia.Services.UIReset;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.ExternalToolLauncher;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Main application window — OpenEmu-inspired shell with sidebar, toolbar, and game content area.
///     Avalonia port of the WPF-UI MainWindow.
/// </summary>
public partial class MainWindow : Window, IPaginationHost
{
    // Bounds persistence (separate file from the WPF app)
    private static readonly string BoundsFilePath = Path.Combine(
        AppDataPaths.SimpleLauncherDataFolder, "window_bounds_avalonia.json");

    private readonly AvaloniaContextMenuService _contextMenuService;
    private readonly ExternalToolLauncherService _externalToolLauncher;
    private readonly AvaloniaGameFileWatcherService _fileWatcher;
    private readonly EventHandler<EventArgs<string>> _gameFilesChangedHandler;
    private readonly GamePadController _gamePadController;
    private readonly GameScannerService _gameScannerService;
    private readonly AvaloniaLanguageMenuService _languageMenu;
    private readonly AvaloniaLoadingOverlayService _loadingOverlay;
    private readonly LocalizationService _localization;
    private readonly AvaloniaMenuCheckMarkService _menuCheckMarks;
    private readonly IPaginationService _pagination;
    private readonly PlaySoundEffects _playSound;
    private readonly EventHandler<PointerWheelEventArgs> _pointerWheelChangedHandler;
    private readonly AvaloniaQuitSimpleLauncher _quitSimpleLauncher;
    private readonly SettingsManagerService _settings;
    private readonly SystemManagerService _systemManagerService;
    private readonly AvaloniaSystemSelectionOrchestratorService _systemSelectionOrchestrator;

    // Event handler references for cleanup
    private readonly Action<string, string> _toastRequestedHandler;
    private readonly UiResetService _uiResetService;
    private readonly MainViewModel _viewModel;
    private bool _wasControllerRunningBeforeDeactivation;

    public MainWindow(
        MainViewModel viewModel,
        SystemArtRatioService ratioService,
        SystemManagerService systemManagerService,
        SettingsManagerService settings,
        LocalizationService localization,
        ExternalToolLauncherService externalToolLauncher,
        PlaySoundEffects playSound,
        GameScannerService gameScannerService,
        FavoritesSectionViewModel favoritesSection,
        PlayHistorySectionViewModel playHistorySection,
        GlobalSearchSectionViewModel globalSearchSection,
        IPaginationService pagination,
        AvaloniaGameFileWatcherService fileWatcher,
        AvaloniaLanguageMenuService languageMenu,
        AvaloniaMenuCheckMarkService menuCheckMarks,
        GamePadController gamePadController,
        UiResetService uiResetService,
        AvaloniaSystemSelectionOrchestratorService systemSelectionOrchestrator,
        AvaloniaContextMenuService contextMenuService,
        AvaloniaLoadingOverlayService loadingOverlay,
        AvaloniaQuitSimpleLauncher quitSimpleLauncher)
    {
        _viewModel = viewModel;
        _systemManagerService = systemManagerService;
        _settings = settings;
        _localization = localization;
        _externalToolLauncher = externalToolLauncher;
        _playSound = playSound;
        _gameScannerService = gameScannerService;
        _pagination = pagination;
        _fileWatcher = fileWatcher;
        _languageMenu = languageMenu;
        _menuCheckMarks = menuCheckMarks;
        _gamePadController = gamePadController;
        _uiResetService = uiResetService;
        _systemSelectionOrchestrator = systemSelectionOrchestrator;
        _contextMenuService = contextMenuService;
        _loadingOverlay = loadingOverlay;
        _quitSimpleLauncher = quitSimpleLauncher;
        FavoritesSection = favoritesSection;
        PlayHistorySection = playHistorySection;
        GlobalSearchSection = globalSearchSection;
        DataContext = _viewModel;

        // Surface ViewModel toast requests (e.g. RetroAchievements hash scan status)
        _toastRequestedHandler = (title, message) => ShowToast(title, message);
        _viewModel.ToastRequested += _toastRequestedHandler;

        // Initialize converter with ratio service
        ConsoleToCardHeightConverter.SetRatioService(ratioService);
        BooleanToFavoriteStatusConverter.SetLocalizationService(localization);

        InitializeComponent();

        // Wire the extracted services to this host (WPF parity)
        _uiResetService.Initialize(this);
        _systemSelectionOrchestrator.Initialize(this);
        _loadingOverlay.Initialize(this);

        // Localize the emergency return button (WPF DynamicResource ReturnButton parity)
        EmergencyReturnButton.Content = _localization.GetString("ReturnButton");
        ToolTip.SetTip(EmergencyReturnButton,
            _localization.GetString("ClickHereIfTheLoadingScreenIsStuckToReturnToTheMainMenu"));

        // Bind the page-section ViewModels (WPF FavoritesPage / PlayHistoryPage / GlobalSearchPage equivalents)
        FavoritesSectionRoot.DataContext = FavoritesSection;
        PlayHistorySectionRoot.DataContext = PlayHistorySection;
        GlobalSearchSectionRoot.DataContext = GlobalSearchSection;

        // Populate system data from system.xml (sidebar + top System ComboBox)
        PopulateSidebarFromSystemXml();

        // NOTE: no initial System ComboBox selection here — selecting a system would
        // fire SystemComboBox_SelectionChanged synchronously and trigger a full library
        // scan on the UI thread during construction. Window_Opened → InitializeAsync
        // does the single initial scan asynchronously instead.

        // Restore window position/size before the window is shown
        RestoreWindowBounds();

        // Pause/resume gamepad controller on window focus changes to prevent
        // mouse input leaking to other windows (mirrors the WPF app behavior).
        Activated += (_, _) =>
        {
            if (_wasControllerRunningBeforeDeactivation)
            {
                _ = _gamePadController.StartAsync();
                Log.Debug("Gamepad controller restarted on window activation");
            }

            _wasControllerRunningBeforeDeactivation = false;
        };
        Deactivated += (_, _) =>
        {
            if (_gamePadController.IsRunning)
            {
                _wasControllerRunningBeforeDeactivation = true;
                _ = _gamePadController.StopAsync();
                Log.Debug("Gamepad controller temporarily stopped on window deactivation");
            }
            else
            {
                _wasControllerRunningBeforeDeactivation = false;
            }
        };

        // Minimize-to-tray: hide the window and remove from taskbar when minimized
        // (WPF MainWindow_StateChanged parity). The tray icon's Clicked handler
        // already calls OnOpen() which restores ShowInTaskbar + WindowState + Show.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && e.NewValue is WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
        };

        // Start the hash-scan drain at close initiation (not after): App.Dispose's
        // bounded backstop wait then usually returns immediately instead of blocking
        // shutdown on the exit thread (AV-11). Fire-and-forget is safe — the method
        // observes all exceptions internally.
        Closing += (_, _) =>
        {
            try
            {
                var scanner = App.ServiceProvider?.GetService<IRetroAchievementsHashScanner>();
                if (scanner is not null) _ = scanner.CancelScanAndWaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error starting hash scan drain on close");
            }
        };

        // Failsafe shutdown watchdog: if normal shutdown has not terminated the process
        // within the grace period, force-exit so the app can never linger in the background.
        Closed += (_, _) =>
        {
            Log.Information("Main window closed; cleaning up resources");

            // WPF CloseWindowEvents parity: full cleanup on window close — unsubscribe
            // handlers, stop the gamepad, stop the ROM watcher, and kill any lingering
            // CHD mounter processes so no background processes outlive the window.
            try
            {
                _viewModel.ToastRequested -= _toastRequestedHandler;
                _fileWatcher.GameFilesChanged -= _gameFilesChangedHandler;
                RemoveHandler(PointerWheelChangedEvent, _pointerWheelChangedHandler);

                if (_gamePadController.IsRunning) _ = _gamePadController.StopAsync();

                _fileWatcher.StopWatching();

                if (App.ServiceProvider?.GetService<IMountChdFiles>() is { } mountChdFiles)
                    mountChdFiles.KillAllChdMounterProcesses(Log.Logger);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error during main window cleanup");
            }

            _shutdownWatchdogCts?.Cancel();
            _shutdownWatchdogCts?.Dispose();
            _shutdownWatchdogCts = new CancellationTokenSource();
            var token = _shutdownWatchdogCts.Token;

            // 30s grace: must cover App.Dispose's bounded drains (10s hash-scan drain
            // plus saves). A 5s watchdog truncates healthy shutdowns mid-drain and
            // orphans CLI hash processes (AV-12).
            _ = Task.Delay(TimeSpan.FromSeconds(30), token).ContinueWith(_ =>
            {
                try
                {
                    Log.Warning("Shutdown watchdog fired after window close; forcing process exit");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    // Last resort — nothing else we can do
                    Log.Debug(ex, "Shutdown watchdog failed to force exit");
                }
            }, token);
        };

        // Set initial check marks from the saved settings (settings.xml)
        UpdateMenuCheckMarks();

        // Wire the pagination service to the status-bar controls and apply the saved
        // Games Per Page preference (mirrors the WPF app, which paginates the game
        // list once the total exceeds the configured page size).
        _pagination.Initialize(this);
        _viewModel.ConfigurePagination(_settings.GamesPerPage);

        // Populate the letter/number quick-filter bar (WPF FilterMenu parity) and
        // sync the MAME sort-order button tooltip from the current setting.
        PopulateLetterFilterBar();
        UpdateMameSortOrderButtonToolTip();

        // Ctrl+wheel zooms the card size over the game grid (WPF MainWindow_MouseWheelAsync parity).
        _pointerWheelChangedHandler = OnPointerWheelChangedForZoom;
        AddHandler(PointerWheelChangedEvent, _pointerWheelChangedHandler, handledEventsToo: true);

        // Grid/List right-click parity: ListBox (grid view) had only per-card Button handlers
        // like the early System grid; right-clicks that land on the ListBoxItem chrome
        // (or when the Button's PointerPressed is swallowed) would never open the menu.
        // Wire all right-click handlers with handledEventsToo — Avalonia DataGrid/ListBox
        // may mark PointerReleased handled before it bubbles, which would swallow the
        // axaml PointerReleased without handledEventsToo (previous Delete System fix).
        // GridView fallback uses PointerPressed so it can coalesce with the per-card
        // Button's PointerPressed via e.Handled (Pressed+Released would double-open).
        GameGridView.AddHandler(PointerPressedEvent, GameGridView_RightClick, handledEventsToo: true);
        GameDataGrid.AddHandler(PointerReleasedEvent, GameDataGrid_RightClick, handledEventsToo: true);
        FavoritesDataGrid.AddHandler(PointerReleasedEvent, FavoritesDataGrid_RightClick, handledEventsToo: true);
        PlayHistoryDataGrid.AddHandler(PointerReleasedEvent, PlayHistoryDataGrid_RightClick, handledEventsToo: true);
        GlobalSearchResultsDataGrid.AddHandler(PointerReleasedEvent, GlobalSearchResults_RightClick,
            handledEventsToo: true);

        // Live library refresh: when a watched ROM folder changes on disk, reload the
        // current view on the UI thread (same debounced behavior as the WPF app).
        _gameFilesChangedHandler = (_, e) =>
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // WPF parity: only refresh when the affected system is the currently
                    // selected system (or when viewing all systems / favorites / etc.).
                    var selectedSystem = _viewModel.SelectedSystem;
                    if (!string.IsNullOrEmpty(selectedSystem)
                        && !string.Equals(selectedSystem, e.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        // File change is for a different system — ignore it.
                        return;
                    }

                    // The affected system's cached file list is stale — drop it so the
                    // refresh below re-scans that system's folders from disk.
                    _viewModel.InvalidateGameFileCacheForSystem(e.Value);
                    _viewModel.RefreshCurrentView();
                    RefreshSidebarCounts();
                    ShowToast(_localization.GetString("GameLibrary", "Game Library"),
                        _localization.GetString("Toast.Refreshed", "Game list reloaded."));
                    _playSound.PlayNotificationSound();
                });
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to refresh the game list after a file change for system '{System}'", e.Value);
            }
        };
        _fileWatcher.GameFilesChanged += _gameFilesChangedHandler;

        Log.Information("Main window initialized");
    }

    /// <summary>Favorites page section ViewModel (WPF FavoritesPage equivalent).</summary>
    public FavoritesSectionViewModel FavoritesSection { get; }

    /// <summary>Play history page section ViewModel (WPF PlayHistoryPage equivalent).</summary>
    public PlayHistorySectionViewModel PlayHistorySection { get; }

    /// <summary>Global search page section ViewModel (WPF GlobalSearchPage equivalent).</summary>
    public GlobalSearchSectionViewModel GlobalSearchSection { get; }

    /// <summary>
    ///     Loads the game library after the window is shown so the UI thread is not
    ///     blocked during window construction. Refreshes sidebar count badges when done.
    /// </summary>
    private async void Window_Opened(object? sender, EventArgs e)
    {
        try
        {
            Log.Debug("Main window opened; initializing the game library");

            InitializeThemeMenu();

            await _viewModel.InitializeAsync();
            RefreshSidebarCounts();

            var systems = _systemManagerService.LoadSystems();

            // Start watching all configured ROM folders (debounced live refresh)
            _fileWatcher.StartWatchingForSystems(systems);

            // First-run experience (parity with WPF MainWindow.HandleLoadedAsync):
            // if no systems are configured yet, try to auto-discover Windows Store
            // games and, failing that, offer Easy Mode to bootstrap configuration.
            if (systems.Count == 0) await RunFirstRunExperienceAsync();

            // WPF parity: show system selection screen at startup instead of
            // the All Games browser. User picks a system from the grid to begin.
            await ShowSystemSelectionScreenAsync();
            Log.Debug("DisplaySystemSelectionScreenAsync called");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Main window load failed");
        }
    }

    /// <summary>
    ///     Mirrors WPF MainWindow.HandleLoadedAsync first-run flow: scans for Windows
    ///     Store games when no systems exist, then offers Easy Mode if still empty.
    /// </summary>
    private async Task RunFirstRunExperienceAsync()
    {
        try
        {
            Log.Debug("Running first-run experience: no systems configured");

            var scanningText = _localization.GetString("ScanningForWindowsGames", "Scanning for Windows games...");
            _loadingOverlay.SetLoadingState(true, scanningText);
            _viewModel.StatusText = scanningText;
            var result = await _gameScannerService.ScanForStoreGamesAsync();
            if (result.SystemWasCreated)
            {
                var foundText = _localization.GetString("FoundNewMicrosoftWindowsGames",
                    "Found new Microsoft Windows games. Refreshing system list.");
                _viewModel.StatusText = foundText;
                ShowToast(_localization.GetString("MicrosoftWindows", "Microsoft Windows"), foundText,
                    ToastType.Success);
                _systemSelectionOrchestrator.LoadOrReloadSystemManager();
                RefreshSidebarCounts();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during initial Windows games scan");
        }
        finally
        {
            _loadingOverlay.SetLoadingState(false);
        }

        var systems = _systemManagerService.LoadSystems();
        if (systems.Count == 0)
        {
            var messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();
            var welcome = await messageBox.FirstRunWelcomeMessageBoxAsync();
            if (welcome == MessageBoxResult.Yes)
            {
                Log.Information("User accepted the Easy Mode prompt");

                var easyModeWindow = App.ServiceProvider.GetRequiredService<EasyModeWindow>();
                await easyModeWindow.ShowDialog(this);
                if (easyModeWindow.DataContext is EasyModeViewModel { SystemAdded: true })
                    await RefreshAfterSystemConfigurationChangeAsync();
            }
        }
    }

    /// <summary>
    ///     Builds the Theme menu: populates the accent-color submenu and applies the
    ///     saved check marks. Parity with WPF ThemeMenuService.
    /// </summary>
    private void InitializeThemeMenu()
    {
        if (AccentColorMenu is null || BaseThemeMenu is null) return;

        AccentColorMenu.Items.Clear();
        var currentAccent = _settings.AccentColor;
        foreach (var accent in AvaloniaThemeService.AccentColorNames)
        {
            var item = new MenuItem
            {
                Header = accent,
                Tag = accent,
                ToggleType = MenuItemToggleType.CheckBox,
                GroupName = "AccentGroup",
                IsChecked = string.Equals(accent, currentAccent, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += ChangeAccentColor_Click;
            AccentColorMenu.Items.Add(item);
        }

        var currentBase = _settings.BaseTheme;
        foreach (var item in BaseThemeMenu.Items.OfType<MenuItem>())
            item.IsChecked = string.Equals(item.Tag?.ToString(), currentBase, StringComparison.OrdinalIgnoreCase);
    }

    private void ChangeBaseTheme_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var baseTheme = item.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(baseTheme)) return;

        _settings.BaseTheme = baseTheme;
        _ = _settings.SaveAsync();
        AvaloniaThemeService.ApplyTheme(_settings.BaseTheme, _settings.AccentColor);
        Log.Information("Base theme changed to {BaseTheme}", baseTheme);

        foreach (var child in BaseThemeMenu.Items.OfType<MenuItem>()) child.IsChecked = child == item;
    }

    private void ChangeAccentColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var accent = item.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(accent)) return;

        _settings.AccentColor = accent;
        _ = _settings.SaveAsync();
        AvaloniaThemeService.ApplyTheme(_settings.BaseTheme, _settings.AccentColor);
        Log.Information("Accent color changed to {AccentColor}", accent);

        foreach (var child in AccentColorMenu.Items.OfType<MenuItem>()) child.IsChecked = child == item;
    }

    /// <summary>
    ///     Populates the sidebar system groups (manufacturer mapping, icons, counts)
    ///     from system.xml — logic lives in SidebarViewModel.
    /// </summary>
    private void PopulateSidebarFromSystemXml()
    {
        try
        {
            var systems = _systemManagerService.LoadSystems();
            _viewModel.PopulateSidebar(systems);
            _viewModel.Sidebar.RefreshCounts(_viewModel.SystemGameCounts);
            _systemSelectionOrchestrator.LoadOrReloadSystemManager();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to populate sidebar from system.xml");
        }
    }

    /// <summary>
    ///     Refreshes count badges on sidebar system entries after scanning.
    /// </summary>
    public void RefreshSidebarCounts()
    {
        _viewModel.Sidebar.RefreshCounts(_viewModel.SystemGameCounts);
    }

    #region Keyboard Shortcuts

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape:
                SearchBox.Text = "";
                _viewModel.SearchText = "";
                _ = _uiResetService.ResetUiAsync();
                e.Handled = true;
                break;
            case Key.F when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                break;
            case Key.Enter:
                switch (_viewModel.IsGridView)
                {
                    case true when GameGridView.SelectedItem is GameCardViewModel gridGame:
                        _viewModel.PlayGameCommand.Execute(gridGame);
                        break;
                    case false when GameDataGrid.SelectedItem is GameCardViewModel listGame:
                        _viewModel.PlayGameCommand.Execute(listGame);
                        break;
                }

                e.Handled = true;
                break;
            case Key.F5:
                Log.Debug("F5 pressed: refreshing the game list");
                _viewModel.NavigateToAllGamesCommand.Execute(null);
                RefreshSidebarCounts();
                ShowToast(_localization.GetString("Refreshed", "Refreshed"),
                    _localization.GetString("Toast.Refreshed", "Game list reloaded."));
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Emergency Return Button

    private void EmergencyReturnButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _loadingOverlay.EmergencyRelease();
            ShowToast(_localization.GetString("EmergencyReset", "Emergency Reset"),
                _localization.GetString("Toast.EmergencyReset"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in EmergencyReturnButton_Click");
        }
    }

    #endregion

    #region EasyMode

    private async void AddSystem_Click()
    {
        try
        {
            Log.Information("Opening Easy Mode window");

            var easyModeWindow = App.ServiceProvider.GetRequiredService<EasyModeWindow>();
            // Avalonia ShowDialog returns immediately without a nested pump — must await it
            // before reading the result (otherwise the SystemAdded refresh below never runs).
            await easyModeWindow.ShowDialog(this);

            // If a system was added, refresh the UI and show the updated system list
            if (easyModeWindow.DataContext is EasyModeViewModel { SystemAdded: true })
            {
                Log.Information("Easy Mode added a system; refreshing the UI");
                _viewModel.InvalidateAllGameFileCaches();
                await RefreshAfterSystemConfigurationChangeAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method AddSystem_Click");
        }
    }

    #endregion

    #region Emulator Settings (config injection)

    private void OpenInjectWindow<T>(Action<T> initialize) where T : Window
    {
        var win = App.ServiceProvider.GetRequiredService<T>();
        initialize(win);

        // Observed fire-and-forget: dialog faults must never go unobserved (AV-07).
        win.ShowDialog(this).ContinueWith(t =>
        {
            if (t.IsFaulted) Log.Error(t.Exception, $"Error in injected config dialog {typeof(T).Name}.");
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    #endregion

    #region IPaginationHost

    /// <inheritdoc />
    public void SetPrevPageButtonEnabled(bool enabled)
    {
        PrevPageButton.IsEnabled = enabled;
    }

    /// <inheritdoc />
    public void SetNextPageButtonEnabled(bool enabled)
    {
        NextPageButton.IsEnabled = enabled;
    }

    /// <inheritdoc />
    public void ScrollToTop()
    {
        if (GameGridView.Items.Count > 0) GameGridView.ScrollIntoView(GameGridView.Items[0]!);

        if (_viewModel.Games.Count > 0) GameDataGrid.ScrollIntoView(_viewModel.Games[0], null);
    }

    /// <inheritdoc />
    public void UpdateTotalFilesLabel(string? text)
    {
        PaginationPanel.IsVisible = !string.IsNullOrEmpty(text);
        PaginationLabel.Text = text;
    }

    /// <inheritdoc />
    public void AddNoFilesMessage()
    {
        PaginationLabel.Text = "";
        StatusRight.Text = _localization.GetString("Empty.Title", "No Games Found");
    }

    /// <summary>
    ///     Clears the status text (wired to the status-bar timeout timer of the startup
    ///     initialization service).
    /// </summary>
    public void ResetStatusText()
    {
        StatusRight.Text = "";
    }

    private void PrevPage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Debug("Navigating to the previous page");
            _viewModel.GoToPreviousPage();
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error navigating to the previous page");
        }
    }

    private void NextPage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Debug("Navigating to the next page");
            _viewModel.GoToNextPage();
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error navigating to the next page");
        }
    }

    #endregion

    #region Window Bounds Persistence

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        Log.Debug("Main window closing");

        // WPF CloseWindowEvents parity: cancel in-flight background work first so
        // background threads release locks sooner.
        try
        {
            _uiResetCancellationSource.Cancel();
            _uiResetCancellationSource.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error cancelling the UI reset token on close");
        }

        SaveBounds();
    }

    private CancellationTokenSource? _shutdownWatchdogCts;

    private void SaveBounds()
    {
        try
        {
            var dir = Path.GetDirectoryName(BoundsFilePath);
            if (dir is not null) Directory.CreateDirectory(dir);

            var data = new WindowBoundsData
            {
                State = WindowState.ToString()
            };

            if (WindowState == WindowState.Normal)
            {
                data.Left = Position.X;
                data.Top = Position.Y;
                data.Width = Width;
                data.Height = Height;
            }

            File.WriteAllText(BoundsFilePath, JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to save window bounds");
        }
    }

    private void RestoreWindowBounds()
    {
        try
        {
            if (!File.Exists(BoundsFilePath)) return;

            var json = File.ReadAllText(BoundsFilePath);
            var data = JsonSerializer.Deserialize<WindowBoundsData>(json);
            switch (data)
            {
                case null:
                    return;
                case { Left: not null, Top: not null, Width: not null, Height: not null }:
                {
                    // Only restore when the saved top-left corner is still on some screen
                    // (positions may be negative on multi-monitor setups with left displays).
                    var corner = new PixelPoint((int)data.Left.Value, (int)data.Top.Value);
                    var onScreen = Screens?.All.Any(s => s.Bounds.Contains(corner)) ?? false;

                    if (onScreen)
                    {
                        Position = corner;
                        Width = data.Width.Value;
                        Height = data.Height.Value;
                    }

                    break;
                }
            }

            if (data.State is not null && Enum.TryParse<WindowState>(data.State, out var state)) WindowState = state;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to restore window bounds");
        }
    }

    private sealed class WindowBoundsData
    {
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public string? State { get; set; }
    }

    #endregion

    #region View Toggle — DataGrid parity (WPF 6 columns: Favorite / FileName / MachineDescription / FolderPath / TimesPlayed / PlayTime)

    private string _lastSortColumn = "";
    private bool _sortAscending = true;

    private void GameDataGrid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        // Intercept DataGrid's built-in sort and apply it to the underlying Games collection
        // so pagination / status counts remain correct (WPF parity).
        var column = e.Column;
        var sortMember = column.SortMemberPath;
        if (string.IsNullOrEmpty(sortMember))
        {
            // Map header translate fallback
            sortMember = column.Header?.ToString() ?? "";
        }

        // Map WPF header keys to sort fields
        var columnName = sortMember switch
        {
            "IsFavorite" => "Favorite",
            "FileName" => "FileName",
            "MachineDescription" => "MachineDescription",
            "FolderPath" => "FolderPath",
            "TimesPlayed" => "TimesPlayed",
            "PlayTime" => "PlayTime",
            _ => sortMember
        };

        // Prevent DataGrid's default view-sort (we sort the source)
        e.Handled = true;

        // Toggle direction
        SortGamesByColumn(columnName);
    }

    private void SortGamesByColumn(string columnName)
    {
        var collection = _viewModel.Games;
        if (string.Equals(_lastSortColumn, columnName, StringComparison.OrdinalIgnoreCase))
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortAscending = true;
            _lastSortColumn = columnName;
        }

        Log.Debug("Sorting games by {Column} ({Direction})", columnName,
            _sortAscending ? "ascending" : "descending");

        var sorted = columnName switch
        {
            "Favorite" => _sortAscending
                ? collection.OrderBy(g => g.IsFavorite)
                : collection.OrderByDescending(g => g.IsFavorite),
            "FileName" => _sortAscending
                ? collection.OrderBy(g => g.FileName, StringComparer.OrdinalIgnoreCase)
                : collection.OrderByDescending(g => g.FileName, StringComparer.OrdinalIgnoreCase),
            "MachineDescription" => _sortAscending
                ? collection.OrderBy(
                    g => string.IsNullOrEmpty(g.MachineDescription) ? g.FileName : g.MachineDescription,
                    StringComparer.OrdinalIgnoreCase)
                : collection.OrderByDescending(
                    g => string.IsNullOrEmpty(g.MachineDescription) ? g.FileName : g.MachineDescription,
                    StringComparer.OrdinalIgnoreCase),
            "FolderPath" => _sortAscending
                ? collection.OrderBy(g => g.FolderPath, StringComparer.OrdinalIgnoreCase)
                : collection.OrderByDescending(g => g.FolderPath, StringComparer.OrdinalIgnoreCase),
            "TimesPlayed" or "Times Played" => _sortAscending
                ? collection.OrderBy(g => g.PlayCount)
                : collection.OrderByDescending(g => g.PlayCount),
            "PlayTime" or "Play Time" => _sortAscending
                ? collection.OrderBy(g => g.PlayTime, StringComparer.OrdinalIgnoreCase)
                : collection.OrderByDescending(g => g.PlayTime, StringComparer.OrdinalIgnoreCase),
            "Name" => _sortAscending
                ? collection.OrderBy(g => g.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                : collection.OrderByDescending(g => g.DisplayTitle, StringComparer.OrdinalIgnoreCase),
            "System" => _sortAscending
                ? collection.OrderBy(g => g.SystemName, StringComparer.OrdinalIgnoreCase)
                : collection.OrderByDescending(g => g.SystemName, StringComparer.OrdinalIgnoreCase),
            "Path" => _sortAscending
                ? collection.OrderBy(g => g.FilePath, StringComparer.OrdinalIgnoreCase)
                : collection.OrderByDescending(g => g.FilePath, StringComparer.OrdinalIgnoreCase),
            _ => collection.OrderBy(g => g.FileName, StringComparer.OrdinalIgnoreCase)
        };

        _viewModel.Games = new ObservableCollection<GameCardViewModel>(sorted);
    }

    private void GameDataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (GameDataGrid.SelectedItem is GameCardViewModel game)
        {
            Log.Debug("Game list double-click: launching {Game}", game.FilePath);
            _viewModel.PlayGameCommand.Execute(game);
        }
    }

    private void GameDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (GameDataGrid.SelectedItem is GameCardViewModel game && !string.IsNullOrEmpty(game.CoverPath))
            {
                var converter = new PathToImageConverter();
                var bmp =
                    converter.Convert(game.CoverPath, typeof(Bitmap), null, CultureInfo.InvariantCulture) as Bitmap;
                ListViewPreviewImage.Source = bmp;
            }
            else
            {
                ListViewPreviewImage.Source = null;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to update list view preview");
            ListViewPreviewImage.Source = null;
        }
    }

    private void GameDataGrid_RightClick(object? sender, PointerReleasedEventArgs e)
    {
        // Only handle right button
        if (!e.InitialPressMouseButton.HasFlag(MouseButton.Right)) return;

        var point = e.GetPosition(GameDataGrid);
        // Walk up to the row
        var row = GameDataGrid.InputHitTest(point) is Visual hit ? FindParent<DataGridRow>(hit) : null;
        if (row?.DataContext is not GameCardViewModel game) return;

        // Ensure row is selected
        GameDataGrid.SelectedItem = game;
        ShowGameContextMenu(game, GameDataGrid);
        e.Handled = true;
    }

    private void GameGridView_RightClick(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        var props = e.GetCurrentPoint(GameGridView).Properties;
        if (!props.IsRightButtonPressed) return;

        var point = e.GetPosition(GameGridView);
        var item = GameGridView.InputHitTest(point) is Visual hit ? FindParent<ListBoxItem>(hit) : null;
        if (item?.DataContext is not GameCardViewModel game) return;

        GameGridView.SelectedItem = game;
        ShowGameContextMenu(game, GameGridView);
        e.Handled = true;
    }

    #endregion

    #region Quick Actions (Home / Random / Sort / Letter Bar)

    private async void HomeButton_Click()
    {
        try
        {
            Log.Debug("Home button pressed; resetting the UI");
            await _uiResetService.ResetUiAsync();
            ScrollToTop();
            _playSound.PlayNotificationSound();
            UpdateLetterBarSelection("");
            ShowToast(_localization.GetString("Restart", "Restart"),
                _localization.GetString("Toast.Restarted", "Returned to the main game list."));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in HomeButton_Click");
        }
    }

    private async void RandomGameButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // WPF parity: Feeling Lucky operates on the currently selected system
            if (string.IsNullOrEmpty(_viewModel.SelectedSystem))
            {
                ShowToast(_localization.GetString("FeelingLucky", "Feeling Lucky"),
                    _localization.GetString("Toast.SelectSystemFirst", "Please select a system first."));
                return;
            }

            _playSound.PlayNotificationSound();

            // Reset the visible filter controls (the ViewModel resets its own state)
            UpdateLetterBarSelection("");
            SearchBox.Text = "";

            var randomGame = await _viewModel.PickRandomGameAsync();
            if (randomGame is null)
            {
                ShowToast(_localization.GetString("FeelingLucky", "Feeling Lucky"),
                    _localization.GetString("Toast.NoGameFound", "No games found to pick from."));
                return;
            }

            // ListView mode auto-selects the picked game (WPF DataGrid row-0 parity);
            // GridView intentionally leaves nothing selected, like the WPF app.
            if (!_viewModel.IsGridView)
            {
                GameDataGrid.SelectedItem = randomGame;
                GameDataGrid.ScrollIntoView(randomGame, null);
            }

            ShowToast(_localization.GetString("FeelingLucky", "Feeling Lucky"),
                _localization.GetString("Toast.FeelingLucky", "Picked a random game."));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in RandomGameButton_Click");
            var messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();
            await messageBox.ErrorMessageBoxAsync();
        }
    }

    /// <summary>Star button (WPF SelectedSystemFavoriteButton parity): favorites of the selected system.</summary>
    private void SelectedSystemFavoriteButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Debug("Showing favorite games for the selected system");
            _playSound.PlayNotificationSound();

            if (string.IsNullOrEmpty(_viewModel.SelectedSystem))
            {
                _viewModel.NavigateToFavoritesCommand.Execute(null);
                return;
            }

            _viewModel.NavigateToSelectedSystemFavoritesCommand.Execute(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method SelectedSystemFavoriteButton_Click");
        }
    }

    private void MameSortOrderButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _playSound.PlayNotificationSound();
            _viewModel.ToggleMameSortOrder();
            Log.Debug("MAME sort order toggled to {SortOrder}", _viewModel.MameSortOrder);
            UpdateMameSortOrderButtonToolTip();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in MameSortOrderButton_Click");
        }
    }

    private void UpdateMameSortOrderButtonToolTip()
    {
        try
        {
            var tooltip = string.Equals(_viewModel.MameSortOrder, "MachineDescription", StringComparison.Ordinal)
                ? _localization.GetString("SortorderMachineDescription", "Sort order: Machine Description")
                : _localization.GetString("SortorderFileName", "Sort order: File Name");
            ToolTip.SetTip(MameSortOrderButton, tooltip);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to update the MAME sort order button tooltip");
        }
    }

    private void PopulateLetterFilterBar()
    {
        try
        {
            LetterFilterBar.Children.Clear();
            AddLetterButton("All", "");
            AddLetterButton("#", "#");
            foreach (var c in Enumerable.Range('A', 26).Select(static x => (char)x))
                AddLetterButton(c.ToString(), c.ToString());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to populate the letter filter bar");
        }
    }

    private void AddLetterButton(string label, string letter)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(7, 2),
            MinWidth = 28,
            Tag = letter
        };
        button.Classes.Add("toolbar-icon");
        button.Click += (_, _) => LetterFilterButton_Click(letter, button);
        LetterFilterBar.Children.Add(button);
    }

    private void LetterFilterButton_Click(string letter, Button _)
    {
        try
        {
            Log.Debug("Letter filter selected: '{Letter}'", letter);
            _playSound.PlayNotificationSound();
            _viewModel.SetLetterFilter(letter);
            UpdateLetterBarSelection(letter);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in LetterFilterButton_Click");
        }
    }

    private void UpdateLetterBarSelection(string letter)
    {
        foreach (var child in LetterFilterBar.Children)
        {
            if (child is not Button button) continue;

            var isActive = string.Equals(button.Tag as string ?? "", letter, StringComparison.Ordinal);
            button.Classes.Set("active", isActive);
        }
    }

    private void OnPointerWheelChangedForZoom(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Delta.Y)
            {
                case > 0:
                    Log.Debug("Ctrl+MouseWheel: zooming the card size in");
                    _viewModel.ZoomIn();
                    e.Handled = true;
                    break;
                case < 0:
                    Log.Debug("Ctrl+MouseWheel: zooming the card size out");
                    _viewModel.ZoomOut();
                    e.Handled = true;
                    break;
            }
        }
    }

    #endregion

    #region Category Tabs

    /// <summary>
    ///     The page sections embedded in the content area (WPF FavoritesPage /
    ///     PlayHistoryPage / GlobalSearchPage equivalents).
    /// </summary>
    private enum MainSection
    {
        None,
        Favorites,
        PlayHistory,
        GlobalSearch
    }

    /// <summary>
    ///     Shows the requested section and hides the others (including the game browser).
    ///     Favorites and Play History reload their data every time they are opened.
    /// </summary>
    private async Task ShowSectionAsync(MainSection section)
    {
        Log.Debug("Showing section {Section}", section);

        // System selection screen is always hidden when a content view is active
        SystemSelectionRoot.IsVisible = false;

        // WPF parity: the status bar is shown whenever a system/game view is active
        StatusBarArea.IsVisible = true;

        FavoritesSectionRoot.IsVisible = section == MainSection.Favorites;
        PlayHistorySectionRoot.IsVisible = section == MainSection.PlayHistory;
        GlobalSearchSectionRoot.IsVisible = section == MainSection.GlobalSearch;
        GameBrowserPanel.IsVisible = section == MainSection.None;

        switch (section)
        {
            case MainSection.Favorites:
                await FavoritesSection.LoadFavoritesAsync();
                break;
            case MainSection.PlayHistory:
                await PlayHistorySection.LoadHistoryAsync();
                break;
        }
    }

    #endregion

    #region Search

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text;
        _viewModel.SearchText = query ?? "";
        _viewModel.StatusText = string.IsNullOrEmpty(query)
            ? _localization.GetString("Status.Ready", "Ready")
            : string.Format(_localization.GetString("Status.SearchQuery", "Search: \"{0}\""), query);
    }

    /// <summary>Search button (WPF SearchButton parity): re-applies the current filter.</summary>
    private void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text;
        Log.Debug("Search button clicked for query '{Query}'", query);
        _viewModel.SearchText = query ?? "";
        _viewModel.StatusText = string.IsNullOrEmpty(query)
            ? _localization.GetString("Status.Ready", "Ready")
            : string.Format(_localization.GetString("Status.SearchQuery", "Search: \"{0}\""), query);
        SearchBox.Focus();
    }

    #endregion

    #region System & Emulator Selection (Top Bar)

    /// <summary>
    ///     Top System ComboBox: delegates to the system selection orchestrator
    ///     which navigates to the selected system and refreshes the Emulator ComboBox.
    /// </summary>
    private void SystemComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.Debug("System ComboBox selection changed to '{System}'", SystemComboBox.SelectedItem);
        _ = _systemSelectionOrchestrator.HandleSystemSelectionChangedAsync();
    }

    /// <summary>
    ///     Top Emulator ComboBox: stores the chosen emulator so launches use it instead
    ///     of the system's first emulator.
    /// </summary>
    private void EmulatorComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectedEmulatorName = EmulatorComboBox.SelectedItem?.ToString();
        Log.Debug("Emulator ComboBox selection changed to '{Emulator}'", _viewModel.SelectedEmulatorName);
    }

    #endregion

    #region Left Navigation Rail

    /// <summary>Nav rail: restart / home — returns to the All Games view.</summary>
    private void NavRestartButton_Click(object? sender, RoutedEventArgs e)
    {
        HomeButton_Click();
    }

    /// <summary>Nav rail: opens the Favorites section.</summary>
    private async void NavFavoritesButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ShowSectionAsync(MainSection.Favorites);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method NavFavoritesButton_Click");
        }
    }

    /// <summary>Nav rail: opens the Global Search section.</summary>
    private async void NavGlobalSearchButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ShowSectionAsync(MainSection.GlobalSearch);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method NavGlobalSearchButton_Click");
        }
    }

    /// <summary>Nav rail: opens the Play History section.</summary>
    private async void NavHistoryButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ShowSectionAsync(MainSection.PlayHistory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method NavHistoryButton_Click");
        }
    }

    /// <summary>Nav rail: opens the RetroAchievements window.</summary>
    private void NavRetroAchievementsButton_Click(object? sender, RoutedEventArgs e)
    {
        RetroAchievements_Click(sender, e);
    }

    /// <summary>Nav rail: opens the Edit System (expert) window.</summary>
    private void NavEditSystemButton_Click(object? sender, RoutedEventArgs e)
    {
        ExpertMode_Click(sender, e);
    }

    /// <summary>Nav rail: toggles between grid and list view (WPF ToggleViewMode parity).</summary>
    private void NavToggleViewModeButton_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsGridView = !_viewModel.IsGridView;
        _settings.ViewMode = _viewModel.IsGridView ? "GridView" : "ListView";
        _ = _settings.SaveAsync();
        Log.Information("View mode toggled to {ViewMode}", _settings.ViewMode);
        UpdateViewModeCheckMarks();
        _playSound.PlayNotificationSound();
    }

    /// <summary>Nav rail: cycles the button aspect ratio (WPF NavToggleButtonAspectRatio parity).</summary>
    private async void NavToggleButtonAspectRatioButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // WPF parity: ignore the toggle while a game-library load is in progress.
            if (_viewModel.IsLoading) return;

            var aspectRatios = new List<string>
                { "Square", "Wider", "SuperWider", "SuperWider2", "Taller", "SuperTaller", "SuperTaller2" };

            var currentIndex = Math.Max(aspectRatios.IndexOf(_settings.ButtonAspectRatio ?? "Square"), 0);
            var nextIndex = (currentIndex + 1) % aspectRatios.Count;
            var newAspectRatio = aspectRatios[nextIndex];

            _settings.ButtonAspectRatio = newAspectRatio;
            await _settings.SaveAsync();
            UpdateButtonAspectRatioCheckMarks(newAspectRatio);
            Log.Information("Button aspect ratio toggled to {AspectRatio}", newAspectRatio);
            _viewModel.ReloadGames();
            _playSound.PlayNotificationSound();
            ShowToast(_localization.GetString("ButtonAspectRatio", "Button Aspect Ratio"),
                $"{_localization.GetString("TogglingButtonAspectRatio", "Toggling button aspect ratio...")} {newAspectRatio}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method NavToggleButtonAspectRatioButton_Click");
        }
    }

    /// <summary>Nav rail: zooms the card size in (WPF NavZoomInButton parity).</summary>
    private void NavZoomInButton_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.ZoomIn();
        _playSound.PlayNotificationSound();
    }

    /// <summary>Nav rail: zooms the card size out (WPF NavZoomOutButton parity).</summary>
    private void NavZoomOutButton_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.ZoomOut();
        _playSound.PlayNotificationSound();
    }

    #endregion

    #region Game Card Interaction

    /// <summary>Single left click on a game card: launches the game (and keeps it selected).</summary>
    private void GameCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: GameCardViewModel game } card)
        {
            var listBoxItem = FindParent<ListBoxItem>(card);
            if (listBoxItem is not null) listBoxItem.IsSelected = true;

            _viewModel.PlayGameCommand.Execute(game);
        }
    }

    /// <summary>Right-button press on a game card: opens the context menu.</summary>
    private void GameCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: GameCardViewModel game } card)
        {
            var properties = e.GetCurrentPoint(card).Properties;

            if (properties.IsRightButtonPressed)
            {
                ShowGameContextMenu(game, card);
                e.Handled = true; // prevent the popup from closing on the subsequent mouse-up
            }
        }
    }

    private void ShowGameContextMenu(GameCardViewModel game, Control placementTarget)
    {
        var context = BuildRightClickContext(game.FilePath, game.SystemName, game);
        _contextMenuService.ShowContextMenu(context, placementTarget, BuildExtraCallbacks());
    }

    /// <summary>Builds the WPF-parity right-click context for a game file.</summary>
    private AvaloniaRightClickContext BuildRightClickContext(string filePath, string systemName,
        GameCardViewModel? card = null,
        string? fileNameWithExtensionOverride = null, Action? onFavoriteRemoved = null)
    {
        var sp = App.ServiceProvider;
        var fileNameWithExtension = fileNameWithExtensionOverride ?? Path.GetFileName(filePath) ?? filePath;
        var safeFilePath = filePath ?? "";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(safeFilePath);
        return new AvaloniaRightClickContext(
            safeFilePath,
            fileNameWithExtension,
            fileNameWithoutExtension,
            systemName,
            _systemManagerService,
            sp.GetRequiredService<SettingsManagerService>(),
            sp.GetRequiredService<FavoritesManager>(),
            this,
            _viewModel,
            card,
            onFavoriteRemoved);
    }

    /// <summary>Avalonia-only extras appended to the WPF-parity context menu.</summary>
    private GameContextMenuCallbacks BuildExtraCallbacks()
    {
        return new GameContextMenuCallbacks
        {
            OnShowDetails = OpenGameDetail,
            OnCopyPath = g =>
            {
                _ = CopyToClipboardAsync(g.FilePath);
                ShowToast(_localization.GetString("Context.Copied"), g.FilePath);
            },
            OnCopyName = g =>
            {
                var fileName = Path.GetFileName(g.FilePath);
                _ = CopyToClipboardAsync(fileName);
                ShowToast(_localization.GetString("Context.Copied"), fileName);
            },
            OnShowInFolder = g => _ = ShowGameInFolderAsync(g),
            OnEditSystem = g => _ = OpenEditSystemForGameAsync(g)
        };
    }

    /// <summary>
    ///     Reveals the game's containing folder in the OS file manager.
    ///     Windows uses explorer's /select to highlight the file; other platforms open the folder.
    /// </summary>
    private async Task ShowGameInFolderAsync(GameCardViewModel game)
    {
        var directory = Path.GetDirectoryName(game.FilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            ShowToast(_localization.GetString("Context.ShowInFolder", "Show in Folder"),
                _localization.GetString("Foldernotfound", "Folder not found."));
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // AV-14: FilePath originates from scanned game files and may contain
                // double quotes. Strip quoting/control characters so a crafted path
                // cannot break out of the /select:"..." argument and inject extra
                // explorer.exe arguments.
                var sanitizedPath = (game.FilePath ?? "")
                    .Replace("\"", "", StringComparison.Ordinal)
                    .Replace("\r", "", StringComparison.Ordinal)
                    .Replace("\n", "", StringComparison.Ordinal);
                if (string.IsNullOrWhiteSpace(sanitizedPath))
                {
                    ShowToast(_localization.GetString("Context.ShowInFolder", "Show in Folder"),
                        _localization.GetString("Foldernotfound", "Folder not found."));
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{sanitizedPath}\"",
                    UseShellExecute = true
                });
            }
            else if (GetTopLevel(this)?.Launcher is { } launcher)
            {
                await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(directory));
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to show game in folder: {Path}", game.FilePath);
            ShowToast(_localization.GetString("Context.ShowInFolder", "Show in Folder"),
                _localization.GetString("Couldnotopenthefolder", "Could not open the folder."));
        }
    }

    /// <summary>
    ///     Opens the Edit System window (Expert Mode) pre-selected to the game's system.
    /// </summary>
    private async Task OpenEditSystemForGameAsync(GameCardViewModel game)
    {
        try
        {
            if (string.IsNullOrEmpty(game.SystemName)) return;

            var factory = App.ServiceProvider.GetRequiredService<Func<string?, EditSystemWindow>>();
            var editWindow = factory(game.SystemName);
            await editWindow.ShowDialog(this);

            // The edited system may have been renamed/deleted — refresh and return to the
            // system selection view.
            await RefreshAfterSystemConfigurationChangeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method OpenEditSystemForGame");
        }
    }

    private async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (Clipboard is null) return;

            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.CreateText(text));
            await Clipboard.SetDataAsync(dataTransfer);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to copy text to clipboard");
        }
    }

    /// <summary>
    ///     Opens the GameDetailWindow for the given game.
    /// </summary>
    private void OpenGameDetail(GameCardViewModel game)
    {
        // GameDetailWindow takes per-game constructor arguments (game + main VM),
        // so it is created manually — DI cannot resolve it. Sync signature required by
        // the Action delegate; faults are observed via continuation (AV-07).
        var window = new GameDetailWindow(game, _viewModel);
        window.ShowDialog(this).ContinueWith(t =>
        {
            if (t.IsFaulted) Log.Error(t.Exception, "Error in GameDetail dialog");
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private static T? FindParent<T>(Visual child) where T : Visual
    {
        return child.FindAncestorOfType<T>();
    }

    #endregion

    #region Page Sections (Favorites / Play History / Global Search)

    private async void FavoritesDataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await FavoritesSection.LaunchSelectedCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method FavoritesDataGrid_DoubleTapped");
        }
    }

    /// <summary>Remove button (WPF RemoveFavoriteButton_ClickAsync parity): removes all selected favorites.</summary>
    private async void FavoritesRemoveButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = FavoritesDataGrid.SelectedItems.Cast<FavoriteRowViewModel>().ToList();
            await FavoritesSection.RemoveFavoritesAsync(selected);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method FavoritesRemoveButton_Click");
        }
    }

    /// <summary>Right-click on a favorites row: opens the WPF-parity context menu.</summary>
    private void FavoritesDataGrid_RightClick(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (e.InitialPressMouseButton != MouseButton.Right) return;

            if (e.Source is not Visual favoritesVisual
                || FindParent<DataGridRow>(favoritesVisual) is not { DataContext: FavoriteRowViewModel favorite })
            {
                return;
            }

            // WPF stores favorites as a file NAME resolved against the system folders;
            // resolve it for launch/media actions while keeping the stored name for
            // favorites matching.
            var filePath = FavoritesSection.ResolveFavoritePath(favorite) ?? favorite.FilePath;
            var context = BuildRightClickContext(
                filePath, favorite.SystemName,
                fileNameWithExtensionOverride: favorite.FilePath,
                onFavoriteRemoved: () => _ = FavoritesSection.LoadFavoritesAsync());

            _contextMenuService.ShowContextMenu(context, FavoritesDataGrid);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method FavoritesDataGrid_RightClick");
        }
    }

    /// <summary>Right-click on a play history row: opens the WPF-parity context menu.</summary>
    private void PlayHistoryDataGrid_RightClick(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (e.InitialPressMouseButton != MouseButton.Right) return;

            if (e.Source is not Visual historyVisual
                || FindParent<DataGridRow>(historyVisual) is not { DataContext: PlayHistoryItem item })
            {
                return;
            }

            var context = BuildRightClickContext(item.FileName, item.SystemName);
            _contextMenuService.ShowContextMenu(context, PlayHistoryDataGrid);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method PlayHistoryDataGrid_RightClick");
        }
    }

    /// <summary>Right-click on a global search result row: opens the WPF-parity context menu.</summary>
    private void GlobalSearchResults_RightClick(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (e.InitialPressMouseButton != MouseButton.Right) return;

            if (e.Source is not Visual searchVisual
                || FindParent<DataGridRow>(searchVisual) is not { DataContext: SearchResult result })
            {
                return;
            }

            var context = BuildRightClickContext(result.FilePath, result.SystemName);
            _contextMenuService.ShowContextMenu(context, GlobalSearchResultsDataGrid);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method GlobalSearchResults_RightClick");
        }
    }

    private async void FavoritesDataGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Delete:
                    e.Handled = true;
                    var selected = FavoritesDataGrid.SelectedItems.Cast<FavoriteRowViewModel>().ToList();
                    await FavoritesSection.RemoveFavoritesAsync(selected);
                    break;
                case Key.Enter:
                    e.Handled = true;
                    await FavoritesSection.LaunchSelectedCommand.ExecuteAsync(null);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method FavoritesDataGrid_KeyDown");
        }
    }

    private async void PlayHistoryDataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await PlayHistorySection.LaunchSelectedCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method PlayHistoryDataGrid_DoubleTapped");
        }
    }

    private async void PlayHistoryDataGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Delete:
                    e.Handled = true;
                    await PlayHistorySection.RemoveSelectedCommand.ExecuteAsync(null);
                    break;
                case Key.Enter:
                    e.Handled = true;
                    await PlayHistorySection.LaunchSelectedCommand.ExecuteAsync(null);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method PlayHistoryDataGrid_KeyDown");
        }
    }

    private async void GlobalSearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;
            await GlobalSearchSection.SearchCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method GlobalSearchTextBox_KeyDown");
        }
    }

    private async void GlobalSearchResults_DoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await GlobalSearchSection.LaunchSelectedCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method GlobalSearchResults_DoubleTapped");
        }
    }

    #endregion

    #region RetroAchievements

    /// <summary>
    ///     Opens the RetroAchievements profile window.
    /// </summary>
    private void RetroAchievements_Click(object? sender, RoutedEventArgs e)
    {
        Log.Information("Opening RetroAchievements window");

        var raWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsWindow>();
        if (raWindow.IsVisible)
        {
            raWindow.Activate();
            return;
        }

        raWindow.Show(this);
    }

    #endregion

    #region Menu Bar

    /// <summary>
    ///     Initializes check marks on all menu items from the saved settings (settings.xml).
    ///     Called once after the window is constructed.
    /// </summary>
    private void UpdateMenuCheckMarks()
    {
        // Language
        UpdateLanguageCheckMarks(_settings.Language);

        // Button size
        UpdateThumbnailSizeCheckMarks(_settings.ThumbnailSize);

        // Aspect ratio
        UpdateButtonAspectRatioCheckMarks(_settings.ButtonAspectRatio);

        // Games per page
        UpdateGamesPerPageCheckMarks(_settings.GamesPerPage);

        // View mode
        UpdateViewModeCheckMarks();

        // Show games
        UpdateShowGamesCheckMarks(_settings.ShowGames);

        // Filename preferences
        UpdateFilenameCheckMarks();

        // Gamepad / fuzzy / annotation
        ToggleGamepad.IsChecked = _settings.EnableGamePadNavigation;
        ToggleFuzzyMatching.IsChecked = _settings.EnableFuzzyMatching;
        ToggleAnnotationStripping.IsChecked = _settings.EnableAnnotationStripping;

        // Overlay buttons
        RetroAchievementButton.IsChecked = _settings.OverlayRetroAchievementButton;
        VideoLinkButton.IsChecked = _settings.OverlayOpenVideoButton;
        InfoLinkButton.IsChecked = _settings.OverlayOpenInfoButton;
    }

    private void UpdateLanguageCheckMarks(string lang)
    {
        // Check exactly the language menu item whose code matches the active language
        var checkedName = _languageMenu.GetMenuItemNameForLanguageCode(lang);
        foreach (var item in LanguageMenu.Items.OfType<MenuItem>())
        {
            item.IsChecked = _languageMenu.IsLanguageMenuItem(item.Name) &&
                             string.Equals(item.Name, checkedName, StringComparison.Ordinal);
        }
    }

    private void UpdateThumbnailSizeCheckMarks(int size)
    {
        _menuCheckMarks.UpdateCheckedByTag(SizeMenu.Items.OfType<MenuItem>(), size);
    }

    private void UpdateButtonAspectRatioCheckMarks(string? ratio)
    {
        _menuCheckMarks.UpdateCheckedByName(AspectRatioMenu.Items.OfType<MenuItem>(), ratio);
    }

    private void UpdateGamesPerPageCheckMarks(int page)
    {
        _menuCheckMarks.UpdateCheckedByTag(GamesPerPageMenu.Items.OfType<MenuItem>(), page);
    }

    private void UpdateViewModeCheckMarks()
    {
        _menuCheckMarks.SetViewModeCheckMarks(GridView, ListView, _viewModel.IsGridView);
    }

    private void UpdateShowGamesCheckMarks(string? mode)
    {
        _menuCheckMarks.UpdateShowGamesCheckMarks(
            [ShowAll, ShowWithCover, ShowWithoutCover], mode);
    }

    private void UpdateFilenameCheckMarks()
    {
        var mode = _settings.FilenameDisplayMode;
        _menuCheckMarks.UpdateFilenameDisplayModeCheckMarks(
            [FilenameDisplayOriginal, FilenameDisplayCleanUp, FilenameDisplayNoFilename], mode);
        DisplayMachineNameToggle.IsChecked = _settings.DisplayMachineName;

        _menuCheckMarks.UpdateFilenameFontSizeCheckMarks(
            [FilenameFontSizeSmall, FilenameFontSizeNormal, FilenameFontSizeBig], _settings.FilenameFontSize);
        _menuCheckMarks.UpdateMachineNameFontSizeCheckMarks(
            [MachineNameFontSizeSmall, MachineNameFontSizeNormal, MachineNameFontSizeBig],
            _settings.MachineNameFontSize);
    }

    // ── Language ──

    private async void ChangeLanguage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            var lang = _languageMenu.GetLanguageCodeFromMenuItemName(item.Name) ?? "en";

            _settings.Language = lang;
            await _settings.SaveAsync();
            _localization.LoadLanguage(lang);
            UpdateLanguageCheckMarks(lang);
            Log.Information("Language changed to {Language}", lang);
            _playSound.PlayNotificationSound();

            // WPF parity: LanguageMenuService.ChangeLanguageAsync restarts the app so
            // the new language applies everywhere (not just the partial live apply).
            var messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();
            await _quitSimpleLauncher.RestartApplicationAsync(messageBox);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ChangeLanguage_Click");
        }
    }

    // ── Button size ──

    private async void ButtonSizeClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: string tag } ||
                !int.TryParse(tag, CultureInfo.InvariantCulture, out var size))
            {
                return;
            }

            _settings.ThumbnailSize = size;
            await _settings.SaveAsync();
            _viewModel.CardWidth = size;
            UpdateThumbnailSizeCheckMarks(size);
            Log.Information("Thumbnail size changed to {Size}", size);
            _playSound.PlayNotificationSound();
            ShowToast(_localization.GetString("ButtonSizeIcon", "Button Size"),
                $"{size} {_localization.GetString("Px", "px")}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ButtonSizeClickAsync");
        }
    }

    // ── Button aspect ratio ──

    private async void ButtonAspectRatioClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            var ratio = item.Name ?? "Square";
            _settings.ButtonAspectRatio = ratio;
            await _settings.SaveAsync();
            UpdateButtonAspectRatioCheckMarks(ratio);
            Log.Information("Button aspect ratio changed to {AspectRatio}", ratio);
            _viewModel.ReloadGames();
            _playSound.PlayNotificationSound();
            ShowToast(_localization.GetString("ButtonAspectRatio", "Button Aspect Ratio"), ratio);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ButtonAspectRatioClickAsync");
        }
    }

    // ── Games per page ──

    private async void GamesPerPageClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: string tag } ||
                !int.TryParse(tag, CultureInfo.InvariantCulture, out var page))
            {
                return;
            }

            _settings.GamesPerPage = page;
            await _settings.SaveAsync();
            UpdateGamesPerPageCheckMarks(page);
            _viewModel.ConfigurePagination(page);
            Log.Information("Games per page changed to {GamesPerPage}", page);
            _viewModel.ReloadGames();
            _playSound.PlayNotificationSound();
            var preferenceSavedTemplate =
                _localization.GetString("Preferencesavedgamesperpage", "Preference saved: {0} games per page.");
            ShowToast(_localization.GetString("GamesPerPage", "Games Per Page"),
                string.Format(CultureInfo.InvariantCulture, preferenceSavedTemplate, page));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method GamesPerPageClickAsync");
        }
    }

    // ── View mode ──

    private void ChangeViewMode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            _viewModel.IsGridView = string.Equals(item.Name, "GridView", StringComparison.Ordinal);
            _settings.ViewMode = _viewModel.IsGridView ? "GridView" : "ListView";
            Log.Information("View mode changed to {ViewMode}", _settings.ViewMode);
            _ = _settings.SaveAsync().ContinueWith(t =>
            {
                if (t.IsFaulted) Log.Warning(t.Exception, "Failed to save view mode preference");
            }, TaskContinuationOptions.OnlyOnFaulted);
            UpdateViewModeCheckMarks();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ChangeViewMode_Click");
        }
    }

    // ── Show games filter ──

    private async void ShowGamesClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            var mode = item.Name switch
            {
                "ShowWithCover" => "ShowWithCover",
                "ShowWithoutCover" => "ShowWithoutCover",
                _ => "ShowAll"
            };

            _settings.ShowGames = mode;
            await _settings.SaveAsync();
            UpdateShowGamesCheckMarks(mode);
            _viewModel.ReloadGames();
            Log.Information("Show games filter changed to {ShowGamesMode}", mode);
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ShowGamesClickAsync");
        }
    }

    // ── Filename preferences ──

    private async void FilenameDisplayMode_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            var mode = item.Name switch
            {
                "FilenameDisplayCleanUp" => "CleanUp",
                "FilenameDisplayNoFilename" => "NoFilename",
                _ => "Original"
            };

            _settings.FilenameDisplayMode = mode;
            await _settings.SaveAsync();
            UpdateFilenameCheckMarks();
            _viewModel.ReloadGames();
            Log.Information("Filename display mode changed to {FilenameDisplayMode}", mode);
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method FilenameDisplayMode_ClickAsync");
        }
    }

    private async void DisplayMachineName_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            _settings.DisplayMachineName = item.IsChecked;
            await _settings.SaveAsync();
            UpdateFilenameCheckMarks();
            _viewModel.ReloadGames();
            Log.Information("Display machine name changed to {DisplayMachineName}", _settings.DisplayMachineName);
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method DisplayMachineName_ClickAsync");
        }
    }

    private async void FilenameFontSize_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            var size = item.Name switch
            {
                "FilenameFontSizeSmall" => "Small",
                "FilenameFontSizeBig" => "Big",
                _ => "Normal"
            };

            _settings.FilenameFontSize = size;
            await _settings.SaveAsync();
            _viewModel.CaptionFontSize = size switch
            {
                "Small" => 11,
                "Big" => 16,
                _ => 13
            };
            UpdateFilenameCheckMarks();
            Log.Information("Filename font size changed to {FontSize}", size);
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method FilenameFontSize_ClickAsync");
        }
    }

    private async void MachineNameFontSize_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            var size = item.Name switch
            {
                "MachineNameFontSizeSmall" => "Small",
                "MachineNameFontSizeBig" => "Big",
                _ => "Normal"
            };

            _settings.MachineNameFontSize = size;
            await _settings.SaveAsync();
            UpdateFilenameCheckMarks();
            Log.Information("Machine name font size changed to {FontSize}", size);
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method MachineNameFontSize_ClickAsync");
        }
    }

    // ── Phase 4.1 windows ──

    private async void EditLinks_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening link settings window");

            var window = App.ServiceProvider.GetRequiredService<SetLinksWindow>();
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method EditLinks_Click");
        }
    }

    private async void SetGamepadDeadZone_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening gamepad dead zone settings window");

            var window = App.ServiceProvider.GetRequiredService<SetGamepadDeadZoneWindow>();
            await window.ShowDialog(this);

            // Apply the new dead zone values to the running controller
            _gamePadController.DeadZoneX = _settings.DeadZoneX;
            _gamePadController.DeadZoneY = _settings.DeadZoneY;
            if (_settings.EnableGamePadNavigation)
            {
                await _gamePadController.StopAsync();
                await _gamePadController.StartAsync();
            }
            else
            {
                // WPF parity: also stop the controller when gamepad navigation is off,
                // in case anything (e.g. the settings dialog) left it running.
                await _gamePadController.StopAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method SetGamepadDeadZone_Click");
        }
    }

    private async void SetFuzzyMatchingThreshold_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening fuzzy matching settings window");

            var window = App.ServiceProvider.GetRequiredService<SetFuzzyMatchingWindow>();
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method SetFuzzyMatchingThreshold_Click");
        }
    }

    private async void SoundConfiguration_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening sound configuration window");

            var window = App.ServiceProvider.GetRequiredService<SoundConfigurationWindow>();
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method SoundConfiguration_Click");
        }
    }

    private async void DownloadImagePack_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening image pack downloader window");

            var window = App.ServiceProvider.GetRequiredService<DownloadImagePackWindow>();
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method DownloadImagePack_Click");
        }
    }

    private async void GlobalStats_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening global statistics window");

            var window = App.ServiceProvider.GetRequiredService<GlobalStatsWindow>();
            window.Initialize(_systemManagerService.LoadSystems());
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method GlobalStats_Click");
        }
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening About window");

            var window = App.ServiceProvider.GetRequiredService<AboutWindow>();
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method About_Click");
        }
    }

    private async void Support_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening support window");

            var window = App.ServiceProvider.GetRequiredService<SupportWindow>();
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method Support_Click");
        }
    }

    // ── Gamepad / fuzzy / overlay toggles ──

    private async void ToggleGamepad_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            _settings.EnableGamePadNavigation = item.IsChecked;
            await _settings.SaveAsync();

            if (item.IsChecked)
                await _gamePadController.StartAsync();
            else
                await _gamePadController.StopAsync();

            Log.Information("Gamepad navigation {State}", item.IsChecked ? "enabled" : "disabled");
            _playSound.PlayNotificationSound();
            ShowToast(_localization.GetString("GamepadSupport", "Gamepad Support"),
                item.IsChecked
                    ? _localization.GetString("Enabled", "Enabled")
                    : _localization.GetString("Disabled", "Disabled"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ToggleGamepad_Click");
        }
    }

    private async void ToggleFuzzyMatchingClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            _settings.EnableFuzzyMatching = item.IsChecked;
            await _settings.SaveAsync();
            _viewModel.ReloadGames();
            Log.Information("Fuzzy matching {State}", item.IsChecked ? "enabled" : "disabled");
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ToggleFuzzyMatchingClickAsync");
        }
    }

    private async void ToggleAnnotationStrippingClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            _settings.EnableAnnotationStripping = item.IsChecked;
            await _settings.SaveAsync();
            _viewModel.ReloadGames();
            Log.Information("Annotation stripping {State}", item.IsChecked ? "enabled" : "disabled");
            _playSound.PlayNotificationSound();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ToggleAnnotationStrippingClickAsync");
        }
    }

    private async void ToggleOverlayButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem item) return;

            var isChecked = item.IsChecked;
            if (ReferenceEquals(item, RetroAchievementButton))
                _settings.OverlayRetroAchievementButton = isChecked;
            else if (ReferenceEquals(item, VideoLinkButton))
                _settings.OverlayOpenVideoButton = isChecked;
            else
                _settings.OverlayOpenInfoButton = isChecked;

            await _settings.SaveAsync();
            _playSound.PlayNotificationSound();
            var header = item.Header?.ToString() ?? _localization.GetString("Overlaybutton", "Overlay button");
            Log.Information("Overlay button '{OverlayButton}' {State}", header,
                isChecked ? "enabled" : "disabled");
            ShowToast(_localization.GetString("OverlayButtonIcon", "Overlay Button"),
                $"{header} {(isChecked
                    ? _localization.GetString("OverlayEnabled", "enabled")
                    : _localization.GetString("OverlayDisabled", "disabled"))}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ToggleOverlayButton_ClickAsync");
        }
    }

    // ── Edit System ──

    private void EasyMode_Click(object? sender, RoutedEventArgs e)
    {
        AddSystem_Click();
    }

    private async void ExpertMode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening Expert Mode (Edit System) window");

            var selectedSystem = _viewModel.SelectedSystem;
            var systemToPreselect = string.IsNullOrWhiteSpace(selectedSystem) ? null : selectedSystem;
            var factory = App.ServiceProvider.GetRequiredService<Func<string?, EditSystemWindow>>();
            var editWindow = factory(systemToPreselect);
            await editWindow.ShowDialog(this);

            // The Expert window can add, rename, or delete systems — keep the sidebar in
            // sync and return to the system selection view.
            await RefreshAfterSystemConfigurationChangeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ExpertMode_Click");
        }
    }

    private async void ScanForMicrosoftWindowsGames_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Scanning for Windows Store games");
            _playSound.PlayNotificationSound();
            var scanningText = _localization.GetString("ScanningForWindowsGames", "Scanning for Windows games...");
            _loadingOverlay.SetLoadingState(true, scanningText);
            _viewModel.IsLoading = true;
            _viewModel.StatusText = scanningText;
            await Task.Yield();

            StorefrontScanResult result;
            try
            {
                result = await _gameScannerService.ScanForStoreGamesAsync();
                // WPF parity: small delay before reloading so the loading overlay is visible
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in the method ScanForMicrosoftWindowsGames_ClickAsync");
                throw;
            }

            // WPF parity: always reload the system manager so a newly created
            // "Microsoft Windows" system appears even when 0 games were found.
            // Avalonia previously only reloaded when GamesFound>0, leaving an
            // empty newly-created system invisible until restart.
            if (result.SystemWasCreated || result.ShortcutsCreated > 0 || result.GamesFound > 0)
            {
                await _viewModel.InitializeAsync();
                await _systemSelectionOrchestrator.ReloadAfterConfigurationChangeAsync();
                RefreshSidebarCounts();
                await ShowSystemSelectionScreenAsync();
            }
            else
            {
                // No new shortcuts but still ensure the manager is fresh (parity with
                // WPF HandleScanForWindowsGamesAsync which always reloads).
                await _systemSelectionOrchestrator.ReloadAfterConfigurationChangeAsync();
            }

            if (result.SystemWasCreated || result.GamesFound > 0)
            {
                Log.Information(
                    "Windows games scan complete: {GamesFound} game(s), {ShortcutsCreated} shortcut(s), system created: {SystemWasCreated}",
                    result.GamesFound, result.ShortcutsCreated, result.SystemWasCreated);

                var foundText = _localization.GetString("FoundNewMicrosoftWindowsGames",
                    "Found new Microsoft Windows games. Refreshing system list.");
                _viewModel.StatusText = foundText;
                ShowToast(_localization.GetString("MicrosoftWindows", "Microsoft Windows"), foundText,
                    ToastType.Success);
            }

            var action = result.SystemWasCreated
                ? _localization.GetString("Created", "Created")
                : _localization.GetString("SystemUpdated", "Updated");
            var scanCompleteTitle = _localization.GetString("ScanComplete", "Scan Complete");
            if (result.GamesFound == 0 && !result.SystemWasCreated)
            {
                var noGames = _localization.GetString("NoPcGamesFound", "No PC games were found on this system.");
                _viewModel.StatusText = noGames;
                ShowToast(scanCompleteTitle, noGames);
            }
            else
            {
                var template = _localization.GetString("FoundPcGamesDetail",
                    "Found {0} PC games. {1} the Microsoft Windows system with {2} new game shortcut(s).");
                var detail = string.Format(CultureInfo.InvariantCulture, template, result.GamesFound, action,
                    result.ShortcutsCreated);
                _viewModel.StatusText = detail;
                ShowToast(scanCompleteTitle, detail);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method ScanForMicrosoftWindowsGames_ClickAsync");
            _viewModel.StatusText =
                _localization.GetString("ErrorScanningForWindowsGames", "Error scanning for Windows games");
        }
        finally
        {
            _loadingOverlay.SetLoadingState(false);
            _viewModel.IsLoading = false;
        }
    }

    // ── System Selection Screen (WPF DisplaySystemSelectionScreenAsync parity) ──

    /// <summary>
    ///     Refreshes everything that depends on system.xml after a configuration change and
    ///     returns to the system selection view. Shared by the Easy Mode, Expert Mode and
    ///     system-selection-grid edit flows.
    /// </summary>
    private async Task RefreshAfterSystemConfigurationChangeAsync()
    {
        Log.Debug("Refreshing the UI after a system configuration change");
        await _systemSelectionOrchestrator.ReloadAfterConfigurationChangeAsync();
        // Refresh the view-model's system snapshot + counts: without this, opening a
        // newly added system filters the stale snapshot and shows 0 games.
        await _viewModel.ReloadSystemsAfterConfigurationChangeAsync();
        RefreshSidebarCounts();
        await ShowSystemSelectionScreenAsync();
    }

    /// <summary>
    ///     Shows the system selection grid and hides all other content panels.
    ///     Mirrors WPF's DisplaySystemSelectionScreenAsync which populates the
    ///     GameFileGrid with clickable system icon buttons.
    /// </summary>
    private async Task ShowSystemSelectionScreenAsync()
    {
        // WPF parity: the system selection screen implies no active system. WPF's
        // UiResetService clears the System ComboBox before displaying this screen
        // (SetSystemComboBoxSelectedItem(null)). Keeping the previous selection here
        // made a system card click a no-op after closing Edit System: setting the
        // ComboBox to the identical item raises no SelectionChanged, so the games
        // were never loaded. A restart worked because UiResetService clears it.
        SystemComboBox.SelectedItem = null;

        // WPF parity: both the top system-selection bar and the status bar are hidden
        // while the full-screen system selection grid is shown
        // (SystemSelectionOrchestratorService lines 115-116).
        TopSystemSelection.IsVisible = false;
        StatusBarArea.IsVisible = false;
        FavoritesSectionRoot.IsVisible = false;
        PlayHistorySectionRoot.IsVisible = false;
        GlobalSearchSectionRoot.IsVisible = false;
        GameBrowserPanel.IsVisible = false;
        SystemSelectionRoot.IsVisible = true;

        await PopulateSystemSelectionGridAsync();
    }

    /// <summary>
    ///     Populates the system selection grid with clickable system cards
    ///     (icon + name), mirroring WPF PopulateSystemSelectionGridAsync.
    /// </summary>
    private async Task PopulateSystemSelectionGridAsync()
    {
        SystemSelectionWrapPanel.Children.Clear();
        NoSystemsConfiguredMessage.IsVisible = false;

        var systems = _systemManagerService.LoadSystems()
            .OrderBy(static s => s.SystemName, StringComparer.Ordinal)
            .ToList();

        Log.Debug("System selection grid loaded {Count} configured system(s)", systems.Count);

        if (systems.Count == 0)
        {
            NoSystemsConfiguredMessage.IsVisible = true;
            return;
        }

        // Rebuild sidebar data so icon paths are resolved
        _viewModel.PopulateSidebar(systems);
        _viewModel.Sidebar.RefreshCounts(_viewModel.SystemGameCounts);

        var systemImageSize = _settings.ThumbnailSizeForSystem;

        foreach (var sidebarItem in _viewModel.Sidebar.Systems)
        {
            var systemName = sidebarItem.SystemName;
            var iconPath = sidebarItem.IconPath;

            var buttonContentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // System icon image — or fallback glyph if no icon resolved
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    await using var stream = File.OpenRead(iconPath);
                    var bitmap = Bitmap.DecodeToWidth(stream, (int)(systemImageSize * 1.3 * 1.6));
                    var image = new Image
                    {
                        Source = bitmap,
                        Height = systemImageSize * 1.3,
                        Width = systemImageSize * 1.3 * 1.6,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(5)
                    };
                    buttonContentPanel.Children.Add(image);
                }
                catch
                {
                    buttonContentPanel.Children.Add(new TextBlock
                    {
                        Text = "🎮",
                        FontSize = 36,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 0)
                    });
                }
            }
            else
            {
                buttonContentPanel.Children.Add(new TextBlock
                {
                    Text = "🎮",
                    FontSize = 36,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                });
            }

            buttonContentPanel.Children.Add(new TextBlock
            {
                Text = systemName,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeight.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12,
                MaxWidth = systemImageSize * 1.3 * 1.6,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            });
            ToolTip.SetTip(buttonContentPanel.Children[^1], systemName);

            var systemButton = new Button
            {
                Content = buttonContentPanel,
                Tag = systemName,
                Width = (systemImageSize * 1.3 * 1.6) + 20,
                Height = (systemImageSize * 1.3) + 40 + 20,
                Margin = new Thickness(5),
                Padding = new Thickness(5)
            };
            systemButton.Classes.Add("game-button-3d"); // WPF SystemButton3DTemplate parity
            systemButton.Click += SystemCard_Click;

            // Right-click context menu (WPF parity: Select / Edit / Delete)
            var contextMenu = new ContextMenu();
            var selectItem = new MenuItem { Header = _localization.GetString("SelectSystem", "Select System") };
            selectItem.Click += (_, _) => SystemComboBox.SelectedItem = systemName;
            var editItem = new MenuItem { Header = _localization.GetString("EditSystem", "Edit System") };
            editItem.Click += (_, _) => EditSystemFromGrid(systemName);
            var deleteItem = new MenuItem { Header = _localization.GetString("DeleteSystem", "Delete System") };
            deleteItem.Click += (_, _) => _ = DeleteSystemFromGrid(systemName);
            contextMenu.Items.Add(selectItem);
            contextMenu.Items.Add(editItem);
            contextMenu.Items.Add(deleteItem);
            systemButton.ContextMenu = contextMenu;
            // Avalonia Button handles pointer events internally which can prevent the automatic
            // ContextMenu opening on right-click. Explicitly open on right PointerPressed (WPF
            // shows the menu automatically via Button.ContextMenu).
            systemButton.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(systemButton).Properties.IsRightButtonPressed)
                {
                    contextMenu.Open(systemButton);
                    e.Handled = true;
                }
            };

            SystemSelectionWrapPanel.Children.Add(systemButton);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     System card click — sets the System ComboBox to the clicked system,
    ///     which fires the orchestrator pipeline that loads games and returns
    ///     to the game browser.
    /// </summary>
    private void SystemCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        var systemName = btn.Tag as string;
        if (string.IsNullOrEmpty(systemName)) return;

        _playSound.PlayNotificationSound();
        Log.Debug("System card clicked: selecting {System}", systemName);
        SystemComboBox.SelectedItem = systemName;
    }

    /// <summary>
    ///     Opens Edit System for the specified system, then refreshes the grid.
    /// </summary>
    private async void EditSystemFromGrid(string systemName)
    {
        try
        {
            _playSound.PlayNotificationSound();
            Log.Information("Opening Edit System window for {System}", systemName);
            var factory = App.ServiceProvider.GetRequiredService<Func<string?, EditSystemWindow>>();
            var editWindow = factory(systemName);
            await editWindow.ShowDialog(this);
            await RefreshAfterSystemConfigurationChangeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error editing system from grid");
        }
    }

    /// <summary>
    ///     Deletes a system after confirmation, then refreshes the grid.
    /// </summary>
    private async Task DeleteSystemFromGrid(string systemName)
    {
        try
        {
            var messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();
            var result = await messageBox.AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync();
            if (result != MessageBoxResult.Yes) return;

            Log.Information("Deleting system {System}", systemName);
            _playSound.PlayNotificationSound();

            var writer = App.ServiceProvider.GetRequiredService<ISystemConfigurationWriterService>();
            await writer.DeleteSystemAsync(systemName);

            await _systemSelectionOrchestrator.ReloadAfterConfigurationChangeAsync();
            RefreshSidebarCounts();
            await ShowSystemSelectionScreenAsync();

            await messageBox.SystemHasBeenDeletedMessageBoxAsync(systemName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting system from grid");
        }
    }

    // ── RetroAchievements ──

    private async void RetroAchievementsSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening RetroAchievements settings window");

            var settingsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsSettingsWindow>();
            await settingsWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method RetroAchievementsSettings_Click");
        }
    }

    private async void RescanRetroAchievements_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Rescanning RetroAchievements hashes for the selected system");
            await _viewModel.RescanRetroAchievementsForSelectedSystemAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method RescanRetroAchievements_Click");
        }
    }

    // ── Inject Emulator Config (21 emulators) ──

    private void ShowEmulatorConfig_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string emulatorName }) return;

        try
        {
            Log.Information("Opening {Emulator} configuration window", emulatorName);

            switch (emulatorName)
            {
                case "Ares":
                    OpenInjectWindow<InjectAresConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Azahar":
                    OpenInjectWindow<InjectAzaharConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Blastem":
                    OpenInjectWindow<InjectBlastemConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Cemu":
                    OpenInjectWindow<InjectCemuConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Daphne":
                    OpenInjectWindow<InjectDaphneConfigWindow>(w => w.Initialize(false));
                    break;
                case "Dolphin":
                    OpenInjectWindow<InjectDolphinConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "DuckStation":
                    OpenInjectWindow<InjectDuckStationConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Flycast":
                    OpenInjectWindow<InjectFlycastConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Mame":
                    OpenInjectWindow<InjectMameConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Mednafen":
                    OpenInjectWindow<InjectMednafenConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Mesen":
                    OpenInjectWindow<InjectMesenConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "PCSX2":
                    OpenInjectWindow<InjectPcsx2ConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Raine":
                    OpenInjectWindow<InjectRaineConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Redream":
                    OpenInjectWindow<InjectRedreamConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "RetroArch":
                    OpenInjectWindow<InjectRetroArchConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "RPCS3":
                    OpenInjectWindow<InjectRpcs3ConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "SegaModel2":
                    OpenInjectWindow<InjectSegaModel2ConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Stella":
                    OpenInjectWindow<InjectStellaConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Supermodel":
                    OpenInjectWindow<InjectSupermodelConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Xenia":
                    OpenInjectWindow<InjectXeniaConfigWindow>(w => w.Initialize(null, false));
                    break;
                case "Yumir":
                    OpenInjectWindow<InjectYumirConfigWindow>(w => w.Initialize(null, false));
                    break;
                default:
                    Log.Warning("Unknown emulator for config injection: {Emulator}", emulatorName);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open {Emulator} config window", emulatorName);
        }
    }

    // ── Tools (external executables) ──

    private async void LaunchTool_Click(object? sender, RoutedEventArgs e)
    {
        var tool = "";
        try
        {
            if (sender is not MenuItem { Tag: string toolTag }) return;

            tool = toolTag;

            Log.Information("Launching external tool {Tool}", tool);

            try
            {
                _viewModel.StatusText = string.Format(
                    _localization.GetString("Status.LaunchingTool", "Launching tool: {0}..."), tool);
                _playSound.PlayNotificationSound();

                var romFolder = GetSelectedRomFolder();
                var imageFolder = GetSelectedImageFolder();

                switch (tool)
                {
                    case "BatchConvertIsoToXiso":
                        await _externalToolLauncher.BatchConvertIsoToXisoAsync();
                        break;
                    case "BatchConvertToCHD":
                        Log.Debug("Called BatchConvertToCHD with args: {RomFolder}", romFolder);
                        await _externalToolLauncher.BatchConvertToChdAsync(romFolder);
                        break;
                    case "BatchConvertToCompressedFile":
                        await _externalToolLauncher.BatchConvertToCompressedFileAsync();
                        break;
                    case "BatchConvertToRVZ":
                        await _externalToolLauncher.BatchConvertToRvzAsync();
                        break;
                    case "CreateBatchFilesForPS3Games":
                        await _externalToolLauncher.CreateBatchFilesForPs3GamesAsync();
                        break;
                    case "CreateBatchFilesForScummVMGames":
                        await _externalToolLauncher.CreateBatchFilesForScummVmGamesAsync();
                        break;
                    case "CreateBatchFilesForWindowsGames":
                        await _externalToolLauncher.CreateBatchFilesForWindowsGamesAsync();
                        break;
                    case "CreateBatchFilesForXbox360XBLAGames":
                        await _externalToolLauncher.CreateBatchFilesForXbox360XblaGamesAsync();
                        break;
                    case "FindRomCover":
                        // WPF parity: reset the UI state before handing over to the external tool.
                        await _uiResetService.ResetUiAsync();
                        await _externalToolLauncher.FindRomCoverLaunchAsync(imageFolder, romFolder);
                        break;
                    case "RetroGameCoverDownloader":
                        await _uiResetService.ResetUiAsync();
                        await _externalToolLauncher.RetroGameCoverDownloaderAsync(imageFolder, romFolder);
                        break;
                    case "RomValidator":
                        await _externalToolLauncher.RomValidatorAsync();
                        break;
                    default:
                        Log.Warning("Unknown tool menu item: {Tool}", tool);
                        break;
                }

                _viewModel.StatusText = _localization.GetString("Status.Ready", "Ready");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error launching tool {Tool}", tool);
                _viewModel.StatusText = _localization.GetString("Errorlaunchingtool", "Error launching tool");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in LaunchTool_Click for tool {Tool}", tool);
        }
    }

    /// <summary>
    ///     Resolves the first ROM folder of the currently selected system (null when none).
    /// </summary>
    private string? GetSelectedRomFolder()
    {
        try
        {
            var system = _systemManagerService.GetSystem(_viewModel.SelectedSystem);
            var folder = system?.SystemFolders.FirstOrDefault();
            return folder is null ? null : PathHelper.ResolveRelativeToAppDirectory(folder);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to resolve selected ROM folder");
            return null;
        }
    }

    /// <summary>
    ///     Resolves the image folder of the currently selected system (null when none).
    /// </summary>
    private string? GetSelectedImageFolder()
    {
        try
        {
            var system = _systemManagerService.GetSystem(_viewModel.SelectedSystem);
            var folder = system?.SystemImageFolder;
            return string.IsNullOrEmpty(folder)
                ? null
                : PathHelper.ResolveRelativeToAppDirectory(folder);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to resolve selected image folder");
            return null;
        }
    }

    // ── Donate / AppData / Exit ──

    private async void Donate_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Information("Opening the donation page");

            var configuration = App.ServiceProvider.GetRequiredService<IConfiguration>();
            var url = configuration.GetValue<string>("Urls:DonationPage") ?? "https://www.purelogiccode.com/Donate/";
            // AV-15: config-controlled URL — validate the scheme and open via the
            // cross-platform launcher instead of assuming Windows shell-execute.
            if (!await ExternalLinkHelper.TryOpenUrlAsync(url, GetTopLevel(this)))
                Log.Error("Unable to open the donation link from the menu: invalid or unreachable URL");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to open the donation link from the menu");
        }
    }

    private async void OpenAppDataPath_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var appDataPath = AppDataPaths.SimpleLauncherDataFolder;
            if (string.IsNullOrEmpty(appDataPath) || !Directory.Exists(appDataPath))
            {
                Log.Debug("AppData path does not exist: {Path}", appDataPath);
                return;
            }

            // AV-15: open via the cross-platform launcher (shell-execute throws on
            // Linux); the helper falls back to shell-execute where appropriate.
            Log.Debug("Opening the AppData folder: {Path}", appDataPath);
            if (!await ExternalLinkHelper.TryOpenFolderAsync(appDataPath, GetTopLevel(this)))
                Log.Error("Unable to open the AppData folder: {Path}", appDataPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the method OpenAppDataPath_Click");
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Log.Information("Exit requested from the menu; shutting down");
        _playSound.PlayNotificationSound();

        // WPF parity: Exit_Click routes through QuitSimpleLauncher.SimpleQuitApplication()
        // for a clean shutdown (tray/process cleanup), not just Close().
        _quitSimpleLauncher.SimpleQuitApplication();
    }

    #endregion

    #region Toast

    public void ShowToast(string title, string message, ToastType type = ToastType.Info)
    {
        var color = GetBrush(type switch
        {
            ToastType.Success => "NotificationSuccessBrush",
            ToastType.Warning => "NotificationWarningBrush",
            ToastType.Error => "NotificationErrorBrush",
            _ => "NotificationInfoBrush"
        }) ?? Brushes.Blue;

        var toast = new Border
        {
            Background = GetBrush("BgTertiaryBrush"),
            BorderBrush = color,
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = GetBrush("TextPrimaryBrush"),
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 12
                    },
                    new TextBlock
                    {
                        Text = message,
                        Foreground = GetBrush("TextSecondaryBrush"),
                        FontSize = 11,
                        Margin = new Thickness(0, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        ToastStack.IsVisible = true;
        ToastStack.Children.Add(toast);

        _ = DismissToastAsync(toast);
    }

    private IBrush? GetBrush(string key)
    {
        return this.TryFindResource(key, out var resource) ? resource as IBrush : null;
    }

    private async Task DismissToastAsync(Border toast)
    {
        // AV-23: the whole body is guarded — if MainWindow closes within the
        // 5s delay, the continuation must not touch the detached ToastStack
        // (previously an unobserved exception on a dead visual).
        try
        {
            await Task.Delay(5000);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    if (!IsLoaded) return;

                    ToastStack.Children.Remove(toast);
                    if (ToastStack.Children.Count == 0) ToastStack.IsVisible = false;
                }
                catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
                {
                    // Window closed mid-dismissal — the toast dies with it.
                }
            });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Toast auto-dismissal skipped");
        }
    }

    #endregion
}