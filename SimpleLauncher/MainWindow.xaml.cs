using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Models;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.GameLauncher;
using SimpleLauncher.Services.GameListUI;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.TakeScreenshot;
using SimpleLauncher.Services.TrayIcon;
using SimpleLauncher.Services.UiHelpers;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;

namespace SimpleLauncher;

/// <summary>
///     Main application window for SimpleLauncher, hosting game browsing, filtering, pagination, and system management UI.
/// </summary>
public partial class MainWindow : INotifyPropertyChanged, IDisposable, ILoadingState, IMenuCheckMarkHost, IUiResetHost,
    IUiOrchestratorHost, IStartupInitializationHost, IThemeMenuHost, ILanguageMenuHost, IStatusBarHost
{
    /// <summary>
    ///     The UI orchestrator service that coordinates UI updates.
    /// </summary>
    internal readonly IUiOrchestrator UiOrchestratorService;

    /// <summary>
    ///     The UI reset service used to reset the UI to its default state.
    /// </summary>
    internal readonly IUiResetService UiResetService;

    /// <summary>
    ///     The status bar update service used to display messages in the status bar.
    /// </summary>
    internal readonly IUpdateStatusBar UpdateStatusBarService;

    private readonly RoutedEventHandler _asyncLoadedHandler;
    private readonly IAudioInputService _audioInput;
    private readonly IContextMenuFunctions _contextMenuFunctions;
    private readonly IContextMenuService _contextMenuService;

    private readonly IExternalToolLauncher _externalToolLauncher;
    private readonly IGameBrowserService _gameBrowser;
    private readonly EventHandler<EventArgs<string>> _gameFilesChangedHandler;
    private readonly GameLauncherService _gameLauncherService;
    private readonly EventHandler<GamePlayedEventArgs> _gamePlayedHandler;
    private readonly IApplicationLifecycleService _lifecycle;
    private readonly ILogger _logger;
    private readonly IMenuOrchestrator _menuOrchestrator;
    private readonly IMessageBoxLibraryService _messageBox;

    private readonly IMountChdFiles _mountChdFiles;
    private readonly SettingsManagerService _settings;
    private readonly FilterMenu _topLetterNumberMenu;

    // Pagination
    /// <summary>
    ///     The next page navigation button used for pagination.
    /// </summary>
    internal Button? NextPageButton2;

    /// <summary>
    ///     The previous page navigation button used for pagination.
    /// </summary>
    internal Button? PrevPageButton2;

    /// <summary>
    ///     The tray icon manager instance for system tray integration.
    /// </summary>
    internal TrayIconManager? TrayIconManager;

    private ActiveWindowScreenshotService? _activeWindowScreenshotService;
    private CancellationTokenSource _cancellationSource = new();

    // Event handler references for proper unsubscription to prevent memory leaks
    private RoutedEventHandler? _emergencyButtonClickHandler;

    // F8 global hotkey for active window screenshots
    private GlobalHotkeyService? _globalHotkeyService;

    private bool _isDisposed;

    private bool _isLoadingGames;
    private volatile bool _isResortOperation;

    private bool _playHistoryMigrated;

    /// <summary>
    ///     Gets or sets the play time display string for the current game.
    /// </summary>
    private string _playTime = "";

    private string _selectedImageFolder = "";
    private List<string> _selectedRomFolders = [];

    private string? _selectedSystem = "";

    private List<SystemManager> _systemManagers = null!;
    private bool _wasControllerRunningBeforeDeactivation;

    /// <summary>
    ///     Initializes a new instance of the MainWindow class with the required services.
    /// </summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="playHistoryManager">The play history manager.</param>
    /// <param name="externalToolLauncher">The external tool launcher service.</param>
    /// <param name="messageBox">The message box library service.</param>
    /// <param name="updateStatusBarService">The status bar update service.</param>
    /// <param name="uiResetService">The UI reset service.</param>
    /// <param name="uiOrchestrator">The UI orchestrator service.</param>
    /// <param name="gameBrowser">The game browser service.</param>
    /// <param name="menuOrchestrator">The menu orchestrator service.</param>
    /// <param name="lifecycle">The application lifecycle service.</param>
    /// <param name="audioInput">The audio input service.</param>
    /// <param name="contextMenuFunctions">The context menu functions service.</param>
    /// <param name="logger">The logger service.</param>
    /// <param name="contextMenuService">The context menu service.</param>
    /// <param name="gameLauncherService">The game launcher service.</param>
    /// <param name="mountChdFiles">The CHD mounting service.</param>
    public MainWindow(
        SettingsManagerService settings,
        PlayHistoryManager playHistoryManager,
        IExternalToolLauncher externalToolLauncher,
        IMessageBoxLibraryService messageBox,
        IUpdateStatusBar updateStatusBarService,
        IUiResetService uiResetService,
        IUiOrchestrator uiOrchestrator,
        IGameBrowserService gameBrowser,
        IMenuOrchestrator menuOrchestrator,
        IApplicationLifecycleService lifecycle,
        IAudioInputService audioInput,
        IContextMenuFunctions contextMenuFunctions,
        ILogger logger,
        IContextMenuService contextMenuService,
        GameLauncherService gameLauncherService,
        IMountChdFiles mountChdFiles)
    {
        InitializeComponent();

        _settings = settings;
        PlayHistoryManager = playHistoryManager;
        _externalToolLauncher = externalToolLauncher;
        _messageBox = messageBox;
        UpdateStatusBarService = updateStatusBarService;

        _gameBrowser = gameBrowser;
        _menuOrchestrator = menuOrchestrator;
        _lifecycle = lifecycle;
        _audioInput = audioInput;
        _contextMenuFunctions = contextMenuFunctions;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextMenuService = contextMenuService;

        _gameLauncherService = gameLauncherService;
        _mountChdFiles = mountChdFiles;

        UiResetService = uiResetService;
        UiOrchestratorService = uiOrchestrator;

        UiOrchestratorService.Initialize(this);
        _gameBrowser.Initialize(this, this, this);
        _menuOrchestrator.Initialize(this, this, this, this);
        UiResetService.Initialize(this);
        UpdateStatusBarService.Initialize(this);

        DataContext = this;

        // Load and Apply _settings
        ToggleGamepad.IsChecked = _settings.EnableGamePadNavigation;
        _menuOrchestrator.UpdateThumbnailSizeCheckMarks(_settings.ThumbnailSize);

        // Set the initial value before subscribing to ValueChanged: the XAML loader
        // raises that event while applying Minimum/Maximum (when _settings is still
        // null), and the initial assignment must not schedule a reload either.
        CardSizeSlider.Value = _settings.ThumbnailSize;
        CardSizeSlider.ValueChanged += ButtonSizeSliderValueChanged;
        _menuOrchestrator.UpdateButtonAspectRatioCheckMarks(_settings.ButtonAspectRatio);
        _menuOrchestrator.UpdateNumberOfGamesPerPageCheckMarks(_settings.GamesPerPage);
        _menuOrchestrator.UpdateShowGamesCheckMarks(_settings.ShowGames);
        _menuOrchestrator.UpdateFilenameDisplayModeCheckMarks(_settings.FilenameDisplayMode);
        _menuOrchestrator.UpdateFilenameFontSizeCheckMarks(_settings.FilenameFontSize);
        _menuOrchestrator.UpdateMachineNameFontSizeCheckMarks(_settings.MachineNameFontSize);
        UiOrchestratorService.PaginationFilesPerPage = _settings.GamesPerPage;
        UiOrchestratorService.PaginationThreshold = _settings.GamesPerPage;
        ToggleFuzzyMatching.IsChecked = _settings.EnableFuzzyMatching;
        ToggleAnnotationStripping.IsChecked = _settings.EnableAnnotationStripping;

        _gameBrowser.LoadOrReloadSystemManager();

        _topLetterNumberMenu = new FilterMenu(_audioInput);

        // Add _topLetterNumberMenu to the UI
        LetterNumberMenu.Children.Clear();
        LetterNumberMenu.Children.Add(_topLetterNumberMenu.LetterPanel);

        // Create and integrate FilterMenu
        _topLetterNumberMenu.OnLetterSelected += TopLetterNumberMenu_OnLetterSelectedAsync;

        // Closing is wired in MainWindow.xaml (Closing="MainWindow_Closing"); subscribing
        // it again here made both handlers run, and the second one tore the window down
        // before the deferred settings save finished.
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;

        // Wire up game file watcher to detect external file changes
        _gameFilesChangedHandler = (_, e) => _gameBrowser.OnGameFilesChangedAsync(e.Value);
        _lifecycle.GameFilesChanged += _gameFilesChangedHandler;

        // Wire up game played to refresh the play time and times played in the game list
        _gamePlayedHandler = (_, e) => RefreshGameListAfterPlay(e.FileName, e.SystemName);
        _gameLauncherService.GamePlayed += _gamePlayedHandler;

        // Store the Loaded handler reference so it can be unsubscribed later
        _asyncLoadedHandler = (_, _) => _ = OnLoadedAsync();
        Loaded += _asyncLoadedHandler;
    }

    /// <summary>
    ///     Gets or sets the timer used for temporary status bar messages.
    /// </summary>
    internal DispatcherTimer? StatusBarTimer { get; set; }

    /// <summary>
    ///     Collection of game list items displayed in the UI.
    /// </summary>
    public ObservableCollection<GameListViewItem> GameListItems { get; } = [];

    /// <summary>
    ///     Gets or sets whether the play time display is visible.
    /// </summary>
    public bool IsPlayTimeVisible
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged(nameof(IsPlayTimeVisible));
        }
    } = true;

    /// <summary>
    ///     The play history manager that tracks play history for games.
    /// </summary>
    internal PlayHistoryManager PlayHistoryManager { get; }

    /// <summary>
    ///     Sets the loading state of the UI, optionally displaying a message.
    /// </summary>
    /// <param name="isLoading">Whether the UI is in a loading state.</param>
    /// <param name="message">An optional message to display during loading.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        UiOrchestratorService.SetLoadingState(isLoading, message);
    }

    /// <summary>
    ///     Gets whether games are currently being loaded.
    /// </summary>
    public bool IsLoadingGames
    {
        get => _isLoadingGames;
        private set
        {
            _isLoadingGames = value;
            OnPropertyChanged(nameof(IsLoadingGames));
        }
    }

    /// <summary>
    ///     Occurs when a property value changes, supporting data binding updates.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets or sets the currently selected system name.
    /// </summary>
    public string? SelectedSystem
    {
        get => _selectedSystem;
        set
        {
            _selectedSystem = value;
            OnPropertyChanged(nameof(SelectedSystem));
        }
    }

    public string PlayTime
    {
        get => _playTime;
        set
        {
            _playTime = value;
            OnPropertyChanged(nameof(PlayTime));
        }
    }

    // IUiOrchestratorHost implementation
    ScrollViewer IUiOrchestratorHost.Scroller => Scroller;
    Image IUiOrchestratorHost.PreviewImage => PreviewImage;
    WrapPanel IUiOrchestratorHost.GameFileGrid => GameFileGrid;
    Grid IUiOrchestratorHost.ListViewPreviewArea => ListViewPreviewArea;
    Frame IUiOrchestratorHost.PageContentFrame => PageContentFrame;
    Grid IUiOrchestratorHost.MainGameContent => MainGameContent;
    Grid IUiOrchestratorHost.MainContentGrid => MainContentGrid;
    Label IUiOrchestratorHost.TotalFilesLabel => TotalFilesLabel;
    Button IUiOrchestratorHost.PrevPageButton2 => PrevPageButton2!;
    Button IUiOrchestratorHost.NextPageButton2 => NextPageButton2!;
    UIElement IUiOrchestratorHost.LoadingOverlay => LoadingOverlay;
    Button IUiOrchestratorHost.SortOrderToggleButton => SortOrderToggleButton;
    TextBox IUiOrchestratorHost.SearchTextBox => SearchTextBox;
    ComboBox IUiOrchestratorHost.SystemComboBox => SystemComboBox;
    ComboBox IUiOrchestratorHost.EmulatorComboBox => EmulatorComboBox;
    ObservableCollection<GameListViewItem> IUiOrchestratorHost.GameListItems => GameListItems;

    bool IUiOrchestratorHost.IsLoadingGames => _isLoadingGames;

    void IUiOrchestratorHost.SetIsLoadingGamesInternal(bool value)
    {
        SetIsLoadingGamesInternal(value);
    }

    void IUiOrchestratorHost.CancelAndRecreateToken()
    {
        CancelAndRecreateToken();
    }

    Task IUiOrchestratorHost.ResetUiAsync()
    {
        return ResetUiAsync();
    }

    private void OnPropertyChanged(string propertyName)
    {
        if (Dispatcher.CheckAccess())
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        else
            Dispatcher.BeginInvoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }

    private async Task OnLoadedAsync()
    {
        try
        {
            // Wire Emergency Return Button from Template
            // LoadingOverlay starts Collapsed, so WPF has not built its template visual tree yet.
            // ApplyTemplate() is required for Template.FindName to locate the PART_EmergencyReturnButton.
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
            {
                _emergencyButtonClickHandler = EmergencyOverlayRelease_Click;
                emergencyBtn.Click += _emergencyButtonClickHandler;
            }

            await _lifecycle.InitializeStartupAsync(this);

            // F8 global hotkey for active window screenshots
            _globalHotkeyService =
                App.ServiceProvider.GetRequiredService<GlobalHotkeyService>();
            _activeWindowScreenshotService = App.ServiceProvider
                .GetRequiredService<ActiveWindowScreenshotService>();
            _globalHotkeyService.Initialize(this);

            if (!_globalHotkeyService.IsRegistered)
            {
                var msg = (string)Application.Current.TryFindResource("F8ShortcutInUse")
                          ??
                          "The F8 shortcut key is already in use by another program. Because of this, the screenshot functionality is turned off.";
                MessageBox.Show(msg, "SimpleLauncher", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            _globalHotkeyService.F8Pressed += async () =>
            {
                try
                {
                    await _activeWindowScreenshotService.CaptureActiveWindowAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in F8 screenshot handler");
                }
            };

            await HandleLoadedAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the Loaded handler");
        }
    }

    private async Task HandleLoadedAsync()
    {
        try
        {
            await _gameBrowser.DisplaySystemSelectionScreenAsync(((IMenuActionHost)this).CurrentCancellationToken);
            _logger.Debug("DisplaySystemSelectionScreenAsync called");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the DisplaySystemSelectionScreenAsync method");
            _logger.Debug($"Error in the DisplaySystemSelectionScreenAsync method: {ex.Message}");
        }

        try
        {
            await _lifecycle.SilentCheckForUpdatesAsync(this);
            _logger.Debug("Silent check for updates was done");
            await _lifecycle.ReportUsageAsync();
            _logger.Debug("Stats API call was done");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TimeoutException)
        {
            // Expected network conditions (offline, rate limit, timeout) are never bugs.
            _logger.Information(ex, "Silent update/stats check failed in the Loaded event");
            _logger.Debug($"Silent update/stats check failed in the Loaded event: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the Loaded event");
            _logger.Debug($"Error in the Loaded event: {ex.Message}");
        }

        try
        {
            // --- First-run experience: Check if system.xml is empty ---
            if (_systemManagers == null || _systemManagers.Count == 0)
            {
                // This is the first run. Let's scan for Windows games automatically.
                SetLoadingState(true,
                    (string)Application.Current.TryFindResource("ScanningForWindowsGames") ??
                    "Scanning for Windows games...");
                try
                {
                    await _gameBrowser.ScanForStoreGamesAsync();
                    if (_gameBrowser.WasNewSystemCreated)
                    {
                        UpdateStatusBarService.UpdateContent(
                            (string)Application.Current.TryFindResource("FoundNewMicrosoftWindowsGames") ??
                            "Found new Microsoft Windows games. Refreshing system list.");

                        // Reload to get the new system
                        await _gameBrowser.LoadOrReloadSystemManagerAsync();

                        // After reloading, the system selection screen needs to be updated.
                        await _gameBrowser.DisplaySystemSelectionScreenAsync(((IMenuActionHost)this)
                            .CurrentCancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during initial Windows games scan");
                }
                finally
                {
                    SetLoadingState(false);
                }

                // After the scan, check again if any systems exist.
                // If still no systems, show the Easy Mode prompt.
                if (_systemManagers == null || _systemManagers.Count == 0)
                {
                    var result =
                        (MessageBoxResult)(int)await _messageBox.FirstRunWelcomeMessageBoxAsync();
                    if (result == MessageBoxResult.Yes)
                    {
                        var easyModeWindow = App.ServiceProvider.GetRequiredService<EasyModeWindow>();
                        easyModeWindow.Owner = this;
                        easyModeWindow.ShowDialog();

                        await _gameBrowser.LoadOrReloadSystemManagerAsync();
                        await _gameBrowser.DisplaySystemSelectionScreenAsync(((IMenuActionHost)this)
                            .CurrentCancellationToken); // Await this now
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the Loaded event's first-run logic");
            _logger.Debug($"Error in the Loaded event's first-run logic: {ex.Message}");
        }
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        if (_wasControllerRunningBeforeDeactivation)
        {
            _audioInput.StartGamepad();
            _logger.Debug("Gamepad controller restarted on window activation");
        }

        _wasControllerRunningBeforeDeactivation = false; // Reset flag
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (_audioInput.IsGamepadRunning)
        {
            _wasControllerRunningBeforeDeactivation = true;
            _audioInput.StopGamepad();
            _logger.Debug("Gamepad controller temporarily stopped on window deactivation");
        }
        else
        {
            _wasControllerRunningBeforeDeactivation = false;
        }
    }

    private async void TopLetterNumberMenu_OnLetterSelectedAsync(object? sender, EventArgs<string?> e)
    {
        try
        {
            if (_isDisposed) return;

            await TopLetterNumberMenuClickAsync(e.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method TopLetterNumberMenu_OnLetterSelectedAsync");
        }
    }

    private async void SystemComboBoxSelectionChangedAsync(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                await _gameBrowser.SystemComboBoxSelectionChangedAsync(_cancellationSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when user rapidly switches systems, no action needed
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in SystemComboBoxSelectionChangedAsync");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when user rapidly switches systems, no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in SystemComboBoxSelectionChangedAsync");
        }
    }

    /// <summary>
    ///     Cancels the current operation token and replaces it with a new one.
    /// </summary>
    internal void CancelAndRecreateToken()
    {
        // Atomically exchange the old CancellationTokenSource with a new one
        // This prevents race conditions when multiple threads try to recreate the token
        var oldCts = Interlocked.Exchange(ref _cancellationSource, new CancellationTokenSource());

        try
        {
            oldCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Token was already disposed, nothing left to do
            return;
        }

        // Dispose the old CTS after a grace period instead of inline or never:
        // in-flight operations may still hold its token across awaits (immediate
        // Dispose would throw ObjectDisposedException in them), but never disposing
        // leaks a kernel handle per navigation (WPF-11). Cancellation is already
        // signaled, so holders unwind promptly and the 5s bound only caps handle
        // lifetime. Process exit reclaims anything still pending at shutdown.
        _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(static (_, state) =>
        {
            try
            {
                ((CancellationTokenSource)state!).Dispose();
            }
            catch
            {
                // Best effort; the GC reclaims the rest
            }
        }, oldCts, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    ///     Navigates to a Page within the MainWindow, hiding the main game content.
    /// </summary>
    internal void NavigateToPage(Page page)
    {
        // Hide main game content and show the page frame
        MainGameContent.Visibility = Visibility.Collapsed;
        PageContentFrame.Visibility = Visibility.Visible;

        // Navigate to the page
        PageContentFrame.Content = page;
    }

    /// <summary>
    ///     Returns to the main game content from a page.
    /// </summary>
    internal void NavigateBackToMainContent()
    {
        // Clear the frame content
        PageContentFrame.Content = null;

        // Show main game content and hide the page frame
        MainGameContent.Visibility = Visibility.Visible;
        PageContentFrame.Visibility = Visibility.Collapsed;

        // Refresh the game list to ensure it's up to date
        _audioInput.PlayNotificationSound();
    }

    private (string? startLetter, string? searchQuery) GetLoadGameFilesParams()
    {
        var searchQueryToUse = ((IUiResetHost)this).ActiveSearchQueryOrMode;
        var startLetterToUse = string.IsNullOrEmpty(searchQueryToUse) ? ((IUiResetHost)this).CurrentFilter : null;
        return (startLetterToUse, searchQueryToUse);
    }

    private async void MainWindow_MouseWheelAsync(object sender, MouseWheelEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                // Check if the Ctrl key is pressed
                if (Keyboard.Modifiers != ModifierKeys.Control) return;

                try
                {
                    switch (e.Delta)
                    {
                        case > 0:
                            await _menuOrchestrator.HandleZoomInAsync();
                            SyncCardSizeSlider();
                            break;
                        case < 0:
                            await _menuOrchestrator.HandleZoomOutAsync();
                            SyncCardSizeSlider();
                            break;
                    }

                    // Mark the event as handled to prevent scrolling the ScrollViewer
                    e.Handled = true;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in the method MainWindow_MouseWheelAsync");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method MainWindow_MouseWheelAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method MainWindow_MouseWheelAsync");
        }
    }

    private async Task TopLetterNumberMenuClickAsync(string? selectedLetter)
    {
        try
        {
            if (_isLoadingGames) CancelAndRecreateToken();

            _audioInput.PlayNotificationSound();

            ResetPaginationButtons(); // Ensure pagination is reset at the beginning
            SearchTextBox.Text = ""; // Clear SearchTextBox
            ((IUiResetHost)this).CurrentFilter = selectedLetter; // Update current filter
            ((IUiResetHost)this).ActiveSearchQueryOrMode = null; // Reset special search mode

            // Show loading overlay immediately with proper message
            SetLoadingState(true, (string)Application.Current.TryFindResource("LoadingGames") ?? "Loading Games...");
            await Task.Yield(); // Allow UI to render the loading overlay

            await _gameBrowser.LoadGameFilesAsync(selectedLetter, null,
                _cancellationSource.Token); // searchQuery is null
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error in TopLetterNumberMenuClickAsync");
        }
    }

    private async Task ShowSystemFavoriteGamesClickAsync()
    {
        try
        {
            if (_isLoadingGames) CancelAndRecreateToken();

            _audioInput.PlayNotificationSound();

            // Change the filter to ShowAll (as favorites might not have covers)
            _settings.ShowGames = "ShowAll";
            await _settings.SaveAsync();
            ApplyShowGamesSetting();

            SearchTextBox.Text = "";
            ((IUiResetHost)this).CurrentFilter = null;
            ((IUiResetHost)this).ActiveSearchQueryOrMode = AppConstants.Favorites;

            // Show loading overlay immediately with proper message
            SetLoadingState(true,
                (string)Application.Current.TryFindResource("LoadingFavoriteGamesForSystem") ??
                "Loading favorite games for system...");
            await Task.Yield(); // Allow UI to render the loading overlay

            await _gameBrowser.LoadGameFilesAsync(null, AppConstants.Favorites, _cancellationSource.Token);
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error in ShowSystemFavoriteGamesClickAsync");
        }
    }

    private async Task ShowSystemFeelingLuckyClickAsync()
    {
        try
        {
            CancelAndRecreateToken();

            _audioInput.PlayNotificationSound();

            // Change the filter to ShowAll
            _settings.ShowGames = "ShowAll";
            await _settings.SaveAsync();
            ApplyShowGamesSetting();

            _topLetterNumberMenu.DeselectLetter();
            SearchTextBox.Text = "";
            ((IUiResetHost)this).CurrentFilter = null;
            ((IUiResetHost)this).ActiveSearchQueryOrMode = AppConstants.RandomSelection;

            // Show loading overlay immediately with proper message
            SetLoadingState(true, (string)Application.Current.TryFindResource("LoadingGames") ?? "Loading Games...");
            await Task.Yield(); // Allow UI to render the loading overlay

            await _gameBrowser.LoadGameFilesAsync(null, AppConstants.RandomSelection, _cancellationSource.Token);

            // If in list view, select the game in the DataGrid
            if (!string.Equals(_settings.ViewMode, "ListView", StringComparison.Ordinal) ||
                GameDataGrid.Items.Count <= 0)
            {
                return;
            }

            GameDataGrid.SelectedIndex = 0;
            GameDataGrid.ScrollIntoView(GameDataGrid.SelectedItem);
            GameDataGrid.Focus();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in ShowSystemFeelingLuckyClickAsync");
            await _messageBox.ErrorMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Refreshes the game list play time and times played after a game has been played.
    /// </summary>
    /// <param name="fileName">The file name of the played game.</param>
    /// <param name="systemName">The system name the game belongs to.</param>
    private void RefreshGameListAfterPlay(string fileName, string systemName)
    {
        UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("RefreshingGameList") ??
                                             "Refreshing game list...");
        try
        {
            // Only update if in ListView mode
            if (!string.Equals(_settings.ViewMode, "ListView", StringComparison.Ordinal) || GameListItems.Count == 0)
                return;

            // Get the current playtime from history
            var historyItem = PlayHistoryManager.PlayHistoryList
                .FirstOrDefault(h =>
                    h.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) &&
                    h.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));

            if (historyItem == null) return;

            // Post to the UI thread without blocking the launcher background thread.
            // BeginInvoke (not Invoke) so a busy/disposing UI can never deadlock this
            // caller, and the update is skipped if the window is already gone (WPF-16).
            if (_isDisposed) return;

            Dispatcher.BeginInvoke(() =>
            {
                if (_isDisposed) return;

                // Find and update the specific item (null-safe: placeholders may
                // carry a null FilePath)
                var gameItem = GameListItems.FirstOrDefault(item =>
                    string.Equals(item.FilePath, fileName, StringComparison.OrdinalIgnoreCase));

                if (gameItem != null)
                {
                    // Update playtime
                    var timeSpan = TimeSpan.FromSeconds(historyItem.TotalPlayTime);
                    gameItem.PlayTime = timeSpan.TotalHours >= 1
                        ? $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s"
                        : $"{timeSpan.Minutes}m {timeSpan.Seconds}s";

                    // Update times played
                    gameItem.TimesPlayed = historyItem.TimesPlayed.ToString(CultureInfo.InvariantCulture);

                    // No Items.Refresh(): the item raises PropertyChanged, which updates
                    // the grid. Refresh() is redundant and throws InvalidOperationException
                    // when the grid is mid-render.
                }
            });
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error refreshing game list play time";
            _logger.Error(ex, contextMessage);
        }
    }

    /// <summary>
    ///     Sets the internal loading games flag for UI state tracking.
    /// </summary>
    /// <param name="value">Whether games are currently loading.</param>
    internal void SetIsLoadingGamesInternal(bool value)
    {
        IsLoadingGames = value;
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        UiOrchestratorService.EmergencyRelease();
    }

    /// <summary>
    ///     Initializes the pagination buttons to their default disabled state.
    /// </summary>
    internal void SetPaginationButtonsDefault()
    {
        PrevPageButton2 = PrevPageButton;
        NextPageButton2 = NextPageButton;
        PrevPageButton2.IsEnabled = false;
        NextPageButton2.IsEnabled = false;
    }

    /// <summary>
    ///     Sets the tray icon manager instance for system tray integration.
    /// </summary>
    /// <param name="manager">The tray icon manager to use.</param>
    internal void SetTrayIconManager(TrayIconManager manager)
    {
        TrayIconManager = manager;
    }

    private Task SaveApplicationSettings()
    {
        // Save application's current state
        _settings.ThumbnailSize = _gameBrowser.ImageHeight;
        _settings.GamesPerPage = UiOrchestratorService.PaginationFilesPerPage;
        _settings.EnableGamePadNavigation = ToggleGamepad.IsChecked;
        _settings.EnableFuzzyMatching = ToggleFuzzyMatching.IsChecked;
        _settings.EnableAnnotationStripping = ToggleAnnotationStripping.IsChecked;

        // Note: Theme settings (BaseTheme and AccentColor) are already saved
        // when the user changes them via App.ChangeTheme().
        // We do NOT detect and overwrite them here because DetectTheme()
        // cannot properly identify custom themes like "Adaptive", "HighContrast",
        // or "Midnight" - it would incorrectly save them as "Dark" or "Light".

        return _settings.SaveAsync();
    }

    private async void GameListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (GameDataGrid.SelectedItem is not GameListViewItem selectedItem)
                {
                    PreviewImage.Source = null; // Clear preview if selection is cleared
                    return;
                }

                // If the selected item is the "No results" message, its FilePath will be null.
                // In this case, just clear the preview and do nothing else.
                if (string.IsNullOrEmpty(selectedItem.FilePath))
                {
                    PreviewImage.Source = null;
                    return;
                }

                // If it's a real game item, proceed with loading the preview.
                await _gameBrowser.HandleSelectionChangedAsync(selectedItem);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in GameListSelectionChanged");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in GameListSelectionChanged");
        }
    }

    private async void GameListDoubleClickOnSelectedItemAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (GameDataGrid.SelectedItem is not GameListViewItem selectedItem) return;

                if (string.IsNullOrEmpty(selectedItem.FilePath))
                {
                    // This is likely the "No results found" placeholder item.
                    return;
                }

                // Delegate the double-click handling to the render service
                await _gameBrowser.HandleDoubleClickAsync(selectedItem);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while using the method GameListDoubleClickOnSelectedItemAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error while using the method GameListDoubleClickOnSelectedItemAsync");
        }
    }

    private async void GameListRightClickContextMenuAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (GameDataGrid.SelectedItem is not GameListViewItem selectedItem) return;
                if (string.IsNullOrEmpty(selectedItem.FilePath)) return;

                var systemManager = _systemManagers?.FirstOrDefault(s =>
                    s.SystemName.Equals(selectedItem.SystemName, StringComparison.OrdinalIgnoreCase));
                if (systemManager == null)
                {
                    // Expected condition (stale game entry); the user is already notified via the message box.
                    _logger.Information("systemManager is null for the selected game item");
                    await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
                    return;
                }

                var fileNameWithExtension = Path.GetFileName(selectedItem.FilePath);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(selectedItem.FilePath);

                var gameLauncher = App.ServiceProvider.GetRequiredService<GameLauncherService>();
                var gamePadController = App.ServiceProvider.GetRequiredService<GamePadController>();
                var playSoundEffects = App.ServiceProvider.GetRequiredService<PlaySoundEffects>();
                var machines = App.ServiceProvider.GetRequiredService<IMameDataService>().Machines.ToList();
                var favoritesManager = App.ServiceProvider.GetRequiredService<FavoritesManager>();
                var findCoverImage = App.ServiceProvider.GetRequiredService<IFindCoverImageService>();

                var context = new RightClickContext(
                    selectedItem.FilePath,
                    fileNameWithExtension,
                    fileNameWithoutExtension,
                    selectedItem.SystemName,
                    systemManager,
                    machines,
                    favoritesManager,
                    _settings,
                    EmulatorComboBox,
                    null,
                    null,
                    null,
                    null,
                    this,
                    gamePadController,
                    null,
                    gameLauncher,
                    playSoundEffects,
                    this
                );

                var contextMenu =
                    _contextMenuService.AddRightClickReturnContextMenu(context, findCoverImage, _contextMenuFunctions);
                if (contextMenu != null)
                {
                    // Close the previous context menu before assigning a new one to prevent leaks.
                    if (GameDataGrid.ContextMenu is { IsOpen: true } oldMenu) oldMenu.IsOpen = false;

                    GameDataGrid.ContextMenu = contextMenu;
                    contextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "There was an error in the game list right-click context menu");
                await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "There was an error in the method GameListRightClickContextMenuAsync");
        }
    }

    /// <summary>
    ///     Enables or disables the game launch buttons in the UI.
    /// </summary>
    /// <param name="isEnabled">Whether the game buttons should be enabled.</param>
    internal void SetGameButtonsEnabled(bool isEnabled)
    {
        UiOrchestratorService.SetGameButtonsEnabled(isEnabled);
    }

    /// <summary>
    ///     Clears all game button images from the specified panel.
    /// </summary>
    /// <param name="panel">The panel containing game button images to clear.</param>
    internal static void ClearGameButtonImages(Panel panel)
    {
        GameListUiService.ClearGameButtonImages(panel);
    }

    private Task SetUiBeforeLoadGameFilesAsync()
    {
        return UiOrchestratorService.SetUiBeforeLoadGameFilesAsync();
    }

    private IList<string> SetPaginationOfListOfFiles(IList<string> allFiles)
    {
        return UiOrchestratorService.ApplyPagination(allFiles);
    }

    private async void SortOrderToggleButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (_isLoadingGames) return;

                CancelAndRecreateToken();

                _audioInput.PlayNotificationSound();
                ((IUiResetHost)this).MameSortOrder =
                    string.Equals(((IUiResetHost)this).MameSortOrder, AppConstants.MameSortOrderFileName,
                        StringComparison.Ordinal)
                        ? AppConstants.MameSortOrderMachineDescription
                        : AppConstants.MameSortOrderFileName;
                UpdateSortOrderButtonUi();

                _isResortOperation = true; // Set flag before loading
                try
                {
                    var (sl, sq) = GetLoadGameFilesParams();
                    await _gameBrowser.LoadGameFilesAsync(sl, sq, _cancellationSource.Token);
                }
                finally
                {
                    _isResortOperation = false; // Reset flag after loading
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when user cancels the operation, no action needed
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in method SortOrderToggleButtonClickAsync");
                _logger.Debug("Error in method SortOrderToggleButtonClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SortOrderToggleButtonClickAsync");
            _logger.Debug("Error in method SortOrderToggleButtonClickAsync");
        }
    }

    private void UpdateSortOrderButtonUi()
    {
        if (SortOrderToggleButton == null) return;

        if (string.Equals(((IUiResetHost)this).MameSortOrder, AppConstants.MameSortOrderFileName,
                StringComparison.Ordinal))
        {
            var tooltipText = (string)Application.Current.TryFindResource("SortByMachineDescriptionTooltip") ??
                              "Sort by Machine Description";
            SortOrderToggleButton.ToolTip = tooltipText;
        }
        else
        {
            var tooltipText = (string)Application.Current.TryFindResource("SortByFileNameTooltip") ??
                              "Sort by File Name";
            SortOrderToggleButton.ToolTip = tooltipText;
        }
    }

    /// <summary>
    ///     Loads game files asynchronously based on the specified filter and search query.
    /// </summary>
    /// <param name="startLetter">An optional starting letter filter for game names.</param>
    /// <param name="searchQuery">An optional search query to filter games.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous load operation.</returns>
    internal Task LoadGameFilesAsync(string? startLetter = null, string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        return _gameBrowser.LoadGameFilesAsync(startLetter, searchQuery, cancellationToken);
    }

    /// <summary>
    ///     Invalidates all game file caches so they are reloaded on the next request.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous invalidation operation.</returns>
    internal Task InvalidateGameFileCachesAsync(CancellationToken cancellationToken = default)
    {
        return _gameBrowser.InvalidateGameFileCachesAsync(cancellationToken);
    }
}