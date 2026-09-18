using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.InjectConfigWindows;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Pages;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.GameLauncher;
using SimpleLauncher.Services.GameScan;
using SimpleLauncher.Services.NotificationToast;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.QuitOrReinstall;
using MessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using Settings = SimpleLauncher.Core.Services.SettingsManager.SettingsManagerService;

namespace SimpleLauncher.Services.MenuActionHandler;

/// <summary>
///     Handles all menu actions for the main application window, including emulator configuration, mode switching,
///     game scanning, navigation, view settings, and user preferences.
/// </summary>
public class MenuActionHandlerService
{
    // ---- Zoom ----

    private const int MaxThumbnailSizeForSystem = 150;
    private const int MaxThumbnailSize = 800;
    private const int MinThumbnailSize = 50;
    private const int ZoomStep = 50;
    private readonly IConfiguration _configuration;

    private readonly Dictionary<string, Action> _emulatorConfigWindowFactory;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly GameLauncherService _gameLauncher;
    private readonly GamePadController _gamePadController;
    private readonly GameScannerService _gameScannerService;
    private readonly IGetListOfFilesService _getListOfFiles;
    private readonly IHelpUserService _helpUserService;

    // ReSharper disable once NotAccessedField.Local
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IImageLoader _imageLoader;
    private readonly ILogger _logger;
    private readonly IMenuCheckMarkService _menuCheckMarkService;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly IParameterResolverService _parameterResolverService;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly QuitSimpleLauncher _quitSimpleLauncher;

    private readonly IRetroAchievementsHashScanner _raHashScanner;
    private readonly IServiceProvider _serviceProvider;
    private readonly Settings _settings;
    private readonly IToastNotificationService _toastNotificationService;
    private readonly IUpdateStatusBar _updateStatusBar;

    private IMenuActionHost _host = null!;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MenuActionHandlerService" /> class with all required dependencies.
    /// </summary>
    public MenuActionHandlerService(
        Settings settings,
        PlaySoundEffects playSoundEffects,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        GamePadController gamePadController,
        GameLauncherService gameLauncher,
        GameScannerService gameScannerService,
        FavoritesManager favoritesManager,
        PlayHistoryManager playHistoryManager,
        IHelpUserService helpUserService,
        IGetListOfFilesService getListOfFiles,
        IServiceProvider serviceProvider,
        IFindCoverImageService findCoverImage,
        IImageLoader imageLoader,
        IMenuCheckMarkService menuCheckMarkService,
        IMessageBoxLibraryService messageBoxLibrary,
        IUpdateStatusBar updateStatusBar,
        QuitSimpleLauncher quitSimpleLauncher,
        ILogger logger,
        IParameterResolverService parameterResolverService,
        IRetroAchievementsHashScanner raHashScanner,
        IToastNotificationService toastNotificationService)
    {
        _settings = settings;
        _playSoundEffects = playSoundEffects;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _gamePadController = gamePadController;
        _gameLauncher = gameLauncher;
        _gameScannerService = gameScannerService;
        _favoritesManager = favoritesManager;
        _playHistoryManager = playHistoryManager;
        _helpUserService = helpUserService;
        _getListOfFiles = getListOfFiles;
        _serviceProvider = serviceProvider;
        _findCoverImage = findCoverImage;
        _imageLoader = imageLoader;
        _menuCheckMarkService = menuCheckMarkService;
        _messageBoxLibrary = messageBoxLibrary;
        _updateStatusBar = updateStatusBar;
        _quitSimpleLauncher = quitSimpleLauncher;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parameterResolverService = parameterResolverService;
        _raHashScanner = raHashScanner;
        _toastNotificationService = toastNotificationService;

        _emulatorConfigWindowFactory = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
        {
            ["Xenia"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectXeniaConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Mame"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectMameConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["RetroArch"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectRetroArchConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Supermodel"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectSupermodelConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Mednafen"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectMednafenConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["SegaModel2"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectSegaModel2ConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Ares"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectAresConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Daphne"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectDaphneConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(false);
                w.ShowDialog();
            },
            ["Blastem"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectBlastemConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Mesen"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectMesenConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["DuckStation"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectDuckStationConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["RPCS3"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectRpcs3ConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Flycast"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectFlycastConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Stella"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectStellaConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Dolphin"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectDolphinConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Cemu"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectCemuConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["PCSX2"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectPcsx2ConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Azahar"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectAzaharConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Yumir"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectYumirConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Raine"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectRaineConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            },
            ["Redream"] = () =>
            {
                var w = serviceProvider.GetRequiredService<InjectRedreamConfigWindow>();
                w.Owner = Application.Current.MainWindow;
                w.Initialize(null, false);
                w.ShowDialog();
            }
        };
    }

    /// <summary>
    ///     Initializes the menu action handler with the specified host.
    /// </summary>
    /// <param name="host">The host that provides UI interaction capabilities.</param>
    public void Initialize(IMenuActionHost host)
    {
        _host = host;
    }

    // ---- Emulator Config Windows ----

    /// <summary>
    ///     Opens the configuration window for the specified emulator.
    /// </summary>
    /// <param name="emulatorName">The name of the emulator to configure.</param>
    public void ShowEmulatorConfigWindow(string emulatorName)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent($"Opening {emulatorName} configuration...");

            if (!_emulatorConfigWindowFactory.TryGetValue(emulatorName, out var showWindow))
            {
                _logger.Warning($"Unknown emulator config: {emulatorName}");
                return;
            }

            showWindow();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error opening {emulatorName} configuration window.");
        }
    }

    // ---- Easy Mode / Expert Mode ----

    /// <summary>
    ///     Opens the Easy Mode configuration window and reloads the system manager upon closing.
    /// </summary>
    public void HandleEasyMode()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningEasyMode") ??
                                           "Opening Easy Mode...");

            var easyModeWindow = _serviceProvider.GetRequiredService<EasyModeWindow>();
            easyModeWindow.Owner = Application.Current.MainWindow;
            easyModeWindow.ShowDialog();

            _host.LoadOrReloadSystemManager();
            _ = _host.ResetUiAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method EasyMode_Click");
        }
    }

    /// <summary>
    ///     Opens the Expert Mode (Edit System) configuration window and reloads the system manager upon closing.
    /// </summary>
    public void HandleExpertMode()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningExpertMode") ??
                                           "Opening Expert Mode...");

            var nosystemselected =
                (string)Application.Current.TryFindResource("Nosystemselected") ?? "No system selected";
            var selectedSystem = _host.GetSelectedSystem();
            var systemToPreselect = !string.IsNullOrEmpty(selectedSystem) && !string.Equals(selectedSystem,
                nosystemselected
                , StringComparison.Ordinal)
                ? selectedSystem
                : null;

            var editSystemWindow = new EditSystemWindow(_settings, _playSoundEffects, _configuration, _helpUserService,
                _imageLoader, _messageBoxLibrary, _quitSimpleLauncher, _logger, _parameterResolverService,
                systemToPreselect, _favoritesManager, _playHistoryManager)
            {
                Owner = Application.Current.MainWindow
            };
            editSystemWindow.ShowDialog();

            _host.LoadOrReloadSystemManager();
            _ = _host.ResetUiAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ExpertMode_Click");
        }
    }

    // ---- Download Image Pack ----

    /// <summary>
    ///     Opens the image pack downloader window.
    /// </summary>
    public void HandleDownloadImagePack()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningImagePackDownloader") ??
                                           "Opening Image Pack Downloader...");

            _ = _host.ResetUiAsync();

            var downloadImagePack = _serviceProvider.GetRequiredService<DownloadImagePackWindow>();
            downloadImagePack.Owner = Application.Current.MainWindow;
            downloadImagePack.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method DownloadImagePack_Click");
        }
    }

    // ---- Scan for Windows Games ----

    /// <summary>
    ///     Scans for Windows store games and reloads the system manager when complete.
    /// </summary>
    public async Task HandleScanForWindowsGamesAsync()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _host.SetLoadingState(true,
                (string)Application.Current.TryFindResource("ScanningForWindowsGames") ??
                "Scanning for Windows games...");
            await Task.Yield();
            try
            {
                await _gameScannerService.ScanForStoreGamesAsync();
                await Task.Delay(2000, _host.CurrentCancellationToken);
                await _host.LoadOrReloadSystemManagerAsync();
                await _host.ResetUiAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in method ScanForMicrosoftWindowsGames_ClickAsync");
            }
            finally
            {
                _host.SetLoadingState(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ScanForMicrosoftWindowsGames_ClickAsync");
        }
    }

    // ---- Edit Links ----

    /// <summary>
    ///     Opens the link settings window and reloads game files after links are updated.
    /// </summary>
    public async Task HandleEditLinksAsync()
    {
        try
        {
            _host.CancelAndRecreateToken();
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningLinkSettings") ??
                                           "Opening link settings...");
            _playSoundEffects.PlayNotificationSound();

            var setLinksWindow = _serviceProvider.GetRequiredService<SetLinksWindow>();
            setLinksWindow.Owner = Application.Current.MainWindow;
            setLinksWindow.ShowDialog();

            var (sl, sq) = _host.GetLoadGameFilesParams();
            _host.SetLoadingState(true,
                (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
            await Task.Yield();
            await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method EditLinksClickAsync");
        }
    }

    // ---- Toggle Gamepad ----

    /// <summary>
    ///     Toggles gamepad navigation on or off and saves the setting.
    /// </summary>
    /// <param name="isChecked">True to enable gamepad navigation; false to disable it.</param>
    public async Task HandleToggleGamepadAsync(bool isChecked)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();

            _settings.EnableGamePadNavigation = isChecked;
            await _settings.SaveAsync();

            if (isChecked)
                await _gamePadController.StartAsync();
            else
                await _gamePadController.StopAsync();

            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("TogglingGamepadNavigation") ??
                                           "Toggling gamepad navigation...");
        }
        catch (Exception ex)
        {
            const string contextMessage = "Failed to toggle gamepad.";
            _logger.Error(ex, contextMessage);
            await _messageBoxLibrary.ToggleGamepadFailureMessageBoxAsync();
        }
    }

    // ---- Set Gamepad Dead Zone ----

    /// <summary>
    ///     Opens the gamepad dead zone settings window and applies the updated values.
    /// </summary>
    public void HandleSetGamepadDeadZone()
    {
        _playSoundEffects.PlayNotificationSound();
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningGamepadDeadZoneSettings") ??
                                       "Opening Gamepad Dead Zone settings...");

        var setGamepadDeadZoneWindow = _serviceProvider.GetRequiredService<SetGamepadDeadZoneWindow>();
        setGamepadDeadZoneWindow.ShowDialog();

        _gamePadController.DeadZoneX = _settings.DeadZoneX;
        _gamePadController.DeadZoneY = _settings.DeadZoneY;

        if (_settings.EnableGamePadNavigation)
        {
            _ = _gamePadController.StopAsync();
            _ = _gamePadController.StartAsync();
        }
        else
        {
            _ = _gamePadController.StopAsync();
        }
    }

    // ---- Toggle Fuzzy Matching ----

    /// <summary>
    ///     Toggles fuzzy matching on or off, saves the setting, and reloads the game list.
    /// </summary>
    /// <param name="isChecked">True to enable fuzzy matching; false to disable it.</param>
    public async Task HandleToggleFuzzyMatchingAsync(bool isChecked)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("ApplyingGameVisibilityFilter") ??
                    "Applying game visibility filter...");
                _playSoundEffects.PlayNotificationSound();

                _settings.EnableFuzzyMatching = isChecked;
                await _settings.SaveAsync();

                var (sl, sq) = _host.GetLoadGameFilesParams();
                _host.SetLoadingState(true,
                    (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                await Task.Yield();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);

                _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("TogglingFuzzyMatching") ??
                                               "Toggling fuzzy matching...");
            }
            catch (Exception ex)
            {
                const string contextMessage = "Failed to toggle fuzzy matching.";
                _logger.Error(ex, contextMessage);
                await _messageBoxLibrary.ToggleFuzzyMatchingFailureMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ToggleFuzzyMatchingClickAsync");
        }
    }

    // ---- Set Fuzzy Matching Threshold ----

    /// <summary>
    ///     Opens the fuzzy matching threshold settings window and reloads the game list with the new threshold.
    /// </summary>
    public async Task HandleSetFuzzyMatchingThresholdAsync()
    {
        try
        {
            _host.CancelAndRecreateToken();
            _playSoundEffects.PlayNotificationSound();

            var setThresholdWindow = _serviceProvider.GetRequiredService<SetFuzzyMatchingWindow>();
            setThresholdWindow.ShowDialog();

            if (!_settings.EnableFuzzyMatching) return;

            var (sl, sq) = _host.GetLoadGameFilesParams();
            _updateStatusBar.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningFuzzyMatchingSettings") ??
                "Opening fuzzy matching settings...");
            _host.SetLoadingState(true,
                (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
            await Task.Yield();
            await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method SetFuzzyMatchingThresholdClickAsync");
        }
    }

    // ---- Toggle Annotation Stripping ----

    /// <summary>
    ///     Toggles annotation stripping on or off, saves the setting, and reloads the game list.
    /// </summary>
    /// <param name="isChecked">True to enable annotation stripping; false to disable it.</param>
    public async Task HandleToggleAnnotationStrippingAsync(bool isChecked)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("TogglingAnnotationStripping") ??
                    "Toggling annotation stripping...");
                _playSoundEffects.PlayNotificationSound();

                _settings.EnableAnnotationStripping = isChecked;
                await _settings.SaveAsync();

                var (sl, sq) = _host.GetLoadGameFilesParams();
                _host.SetLoadingState(true,
                    (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                await Task.Yield();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            catch (Exception ex)
            {
                const string contextMessage = "Failed to toggle annotation stripping.";
                _logger.Error(ex, contextMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method HandleToggleAnnotationStrippingAsync");
        }
    }

    // ---- Support / Donate / About / Exit ----

    /// <summary>
    ///     Opens the support request window.
    /// </summary>
    public void HandleSupport()
    {
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningSupportWindow") ??
                                       "Opening support window...");
        _playSoundEffects.PlayNotificationSound();

        var supportRequestWindow = _serviceProvider.GetRequiredService<SupportWindow>();
        supportRequestWindow.Owner = Application.Current.MainWindow;
        supportRequestWindow.ShowDialog();
    }

    /// <summary>
    ///     Opens the donation page in the default browser.
    /// </summary>
    public async Task HandleDonateAsync()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningDonationPage") ??
                                           "Opening donation page...");

            var psi = new ProcessStartInfo
            {
                FileName = _configuration.GetValue<string>("Urls:DonationPage") ??
                           "https://www.purelogiccode.com/Donate/",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            const string contextMessage = "Unable to open the Donation Link from the menu.";
            _logger.Error(ex, contextMessage);
            await _messageBoxLibrary.ErrorOpeningDonationLinkMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Opens the About window.
    /// </summary>
    public void HandleAbout()
    {
        _playSoundEffects.PlayNotificationSound();
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningAboutWindow") ??
                                       "Opening About window...");

        var aboutWindow = _serviceProvider.GetRequiredService<AboutWindow>();
        aboutWindow.Owner = Application.Current.MainWindow;
        aboutWindow.ShowDialog();
    }

    /// <summary>
    ///     Closes the main application window.
    /// </summary>
    public void HandleExit()
    {
        _playSoundEffects.PlayNotificationSound();
        Application.Current.MainWindow?.Close();
    }

    // ---- Show Games Settings ----

    /// <summary>
    ///     Changes the game visibility mode (e.g., show all, show favorites only) and reloads the game list.
    /// </summary>
    /// <param name="showGamesMode">The visibility mode to apply.</param>
    public async Task HandleShowGamesAsync(string showGamesMode)
    {
        try
        {
            _host.CancelAndRecreateToken();
            _updateStatusBar.UpdateContent(
                (string)Application.Current.TryFindResource("ApplyingGameVisibilityFilter") ??
                "Applying game visibility filter...");

            try
            {
                _playSoundEffects.PlayNotificationSound();

                _settings.ShowGames = showGamesMode;
                await _settings.SaveAsync();
                _menuCheckMarkService.UpdateShowGamesCheckMarks(showGamesMode);

                var (sl, sq) = _host.GetLoadGameFilesParams();
                _host.SetLoadingState(true,
                    (string)Application.Current.TryFindResource("ApplyingGameVisibilityFilter") ??
                    "Applying game visibility filter...");
                await Task.Yield();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error in the method ShowGames ({showGamesMode}).");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error in the method ShowGames ({showGamesMode}).");
        }
    }

    // ---- Button Size ----

    /// <summary>
    ///     Changes the game button thumbnail size and reloads the game list.
    /// </summary>
    /// <param name="newSize">The new thumbnail size in pixels.</param>
    public async Task HandleButtonSizeAsync(int newSize)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                _playSoundEffects.PlayNotificationSound();

                // Keep the persisted size on the 50-px grid the settings whitelist accepts:
                // an arbitrary value (e.g. straight from a slider drag) would silently reset
                // to 250 on the next launch.
                newSize = Math.Clamp(
                    (int)Math.Round(newSize / (double)ZoomStep, MidpointRounding.AwayFromZero) * ZoomStep,
                    MinThumbnailSize, MaxThumbnailSize);

                _host.SetGameButtonImageHeight(newSize);
                _settings.ThumbnailSize = newSize;
                await _settings.SaveAsync();

                _menuCheckMarkService.UpdateThumbnailSizeCheckMarks(newSize);
                _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("AdjustingButtonSize") ??
                                               "Adjusting button size...");

                var (sl, sq) = _host.GetLoadGameFilesParams();
                _host.SetLoadingState(true,
                    (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                await Task.Yield();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            catch (Exception ex)
            {
                const string errorMessage = "Error in method ButtonSizeClickAsync.";
                _logger.Error(ex, errorMessage);
                await _messageBoxLibrary.ErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ButtonSizeClickAsync");
        }
    }

    // ---- Button Aspect Ratio ----

    /// <summary>
    ///     Changes the game button aspect ratio and reloads the game list.
    /// </summary>
    /// <param name="aspectRatio">The aspect ratio string to apply (e.g., "16:9", "4:3").</param>
    public async Task HandleButtonAspectRatioAsync(string aspectRatio)
    {
        try
        {
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("AdjustingButtonAspectRatio") ??
                                           "Adjusting button aspect ratio...");
            _host.CancelAndRecreateToken();

            try
            {
                _playSoundEffects.PlayNotificationSound();

                _settings.ButtonAspectRatio = aspectRatio;
                await _settings.SaveAsync();

                _menuCheckMarkService.UpdateButtonAspectRatioCheckMarks(aspectRatio);

                var (sl, sq) = _host.GetLoadGameFilesParams();
                _host.SetLoadingState(true,
                    (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                await Task.Yield();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            catch (Exception ex)
            {
                const string contextMessage = "Error in method ButtonAspectRatioClickAsync";
                _logger.Error(ex, contextMessage);
                await _messageBoxLibrary.ErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ButtonAspectRatioClickAsync");
        }
    }

    // ---- Games Per Page ----

    /// <summary>
    ///     Changes the number of games displayed per page and reloads the game list.
    /// </summary>
    /// <param name="newPage">The number of games per page.</param>
    public async Task HandleGamesPerPageAsync(int newPage)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                if (newPage is 1000 or 10000 or 1000000)
                {
                    if (await _messageBoxLibrary.WarnUserAboutMemoryConsumptionMessageBoxAsync() == MessageBoxResult.No)
                        return;
                }

                _playSoundEffects.PlayNotificationSound();

                _host.SetFilesPerPage(newPage);
                _host.SetPaginationThreshold(newPage);
                _settings.GamesPerPage = newPage;
                await _settings.SaveAsync();

                _menuCheckMarkService.UpdateNumberOfGamesPerPageCheckMarks(newPage);
                _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("AdjustingGamesPerPage") ??
                                               "Adjusting games per page...");

                var (sl, sq) = _host.GetLoadGameFilesParams();
                _host.SetLoadingState(true,
                    (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                await Task.Yield();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method GamesPerPageClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method GamesPerPageClickAsync");
        }
    }

    // ---- Navigation: Global Search ----

    /// <summary>
    ///     Navigates to the global search page.
    /// </summary>
    public void HandleShowGlobalSearch()
    {
        _playSoundEffects.PlayNotificationSound();
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningGlobalSearch") ??
                                       "Opening Global Search...");

        if (Application.Current.MainWindow is not MainWindow mainWindow) return;

        var contextMenuFunctions = _serviceProvider.GetRequiredService<IContextMenuFunctions>();
        var contextMenuService = _serviceProvider.GetRequiredService<IContextMenuService>();
        var globalSearchPage = new GlobalSearchPage(
            _host.GetSystemManagers().ToList(), _host.GetMachines().ToList(),
            new Dictionary<string, string>(_host.GetMameLookup(), StringComparer.Ordinal),
            _favoritesManager, _settings, mainWindow,
            _gamePadController, _gameLauncher, _playSoundEffects,
            _configuration, _getListOfFiles, _findCoverImage, _imageLoader, contextMenuFunctions, _logger,
            contextMenuService);

        _host.NavigateToPage(globalSearchPage);
    }

    // ---- Navigation: Global Stats ----

    /// <summary>
    ///     Opens the global statistics window.
    /// </summary>
    public void HandleShowGlobalStats()
    {
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningGlobalStatistics") ??
                                       "Opening Global Statistics...");
        _playSoundEffects.PlayNotificationSound();

        var globalStatsWindow = _serviceProvider.GetRequiredService<GlobalStatsWindow>();
        globalStatsWindow.Owner = Application.Current.MainWindow;
        globalStatsWindow.Initialize(_host.GetSystemManagers().ToList());
        globalStatsWindow.Show();
    }

    // ---- Navigation: Favorites ----

    /// <summary>
    ///     Navigates to the favorites page.
    /// </summary>
    public void HandleShowFavorites()
    {
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningFavorites") ??
                                       "Opening Favorites...");
        _playSoundEffects.PlayNotificationSound();

        var contextMenuFunctions = _serviceProvider.GetRequiredService<IContextMenuFunctions>();
        var contextMenuService = _serviceProvider.GetRequiredService<IContextMenuService>();
        var favoritesPage = new FavoritesPage(
            _settings, _host.GetSystemManagers().ToList(), _host.GetMachines().ToList(), _favoritesManager,
            // ReSharper disable once AssignNullToNotNullAttribute
            (MainWindow)Application.Current.MainWindow, _gamePadController, _gameLauncher, _playSoundEffects,
            _configuration, _findCoverImage, _imageLoader, contextMenuFunctions, _logger, contextMenuService);

        _host.NavigateToPage(favoritesPage);
    }

    // ---- Navigation: Play History ----

    /// <summary>
    ///     Navigates to the play history page.
    /// </summary>
    public void HandleShowPlayHistory()
    {
        _playSoundEffects.PlayNotificationSound();
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningPlayHistory") ??
                                       "Opening Play History...");

        if (Application.Current.MainWindow is not MainWindow mainWindow) return;

        var contextMenuFunctions = _serviceProvider.GetRequiredService<IContextMenuFunctions>();
        var contextMenuService = _serviceProvider.GetRequiredService<IContextMenuService>();
        var playHistoryPage = new PlayHistoryPage(
            _host.GetSystemManagers(), _host.GetMachines(), _settings,
            _favoritesManager, _playHistoryManager, mainWindow,
            _gamePadController, _gameLauncher, _playSoundEffects, _configuration, _findCoverImage, _imageLoader,
            contextMenuFunctions, _logger, contextMenuService);

        _host.NavigateToPage(playHistoryPage);
    }

    // ---- Navigation: Retro Achievements ----

    /// <summary>
    ///     Opens the RetroAchievements window.
    /// </summary>
    public void HandleShowRetroAchievements()
    {
        _playSoundEffects.PlayNotificationSound();
        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("OpeningRetroAchievements") ??
                                       "Opening RetroAchievements...");

        var retroAchievementsWindow = _serviceProvider.GetRequiredService<RetroAchievementsWindow>();
        retroAchievementsWindow.Owner = Application.Current.MainWindow;
        retroAchievementsWindow.Show();
    }

    // ---- Navigation: Restart ----

    /// <summary>
    ///     Navigates back to the main content and resets the UI.
    /// </summary>
    public void HandleRestart()
    {
        _playSoundEffects.PlayNotificationSound();
        _host.NavigateBackToMainContent();
        _ = _host.ResetUiAsync();
    }

    // ---- System Favorites ----

    /// <summary>
    ///     Displays favorite games for the currently selected system.
    /// </summary>
    public async Task HandleShowSystemFavoritesAsync()
    {
        try
        {
            _updateStatusBar.UpdateContent(
                (string)Application.Current.TryFindResource("LoadingFavoriteGamesForSystem") ??
                "Loading favorite games for system...");
            _playSoundEffects.PlayNotificationSound();
            await _host.ShowSystemFavoriteGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method NavSelectedSystemFavoriteButtonClickAsync");
        }
    }

    // ---- Random / Feeling Lucky ----

    /// <summary>
    ///     Selects and displays a random game from the current system.
    /// </summary>
    public async Task HandleFeelingLuckyAsync()
    {
        try
        {
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("PickingARandomGame") ??
                                           "Picking a random game...");
            _playSoundEffects.PlayNotificationSound();
            await _host.ShowSystemFeelingLuckyAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavRandomLuckGameButtonClickAsync");
        }
    }

    // ---- Retro Achievements Filter ----

    /// <summary>
    ///     Filters the game list to show only games that have RetroAchievements support.
    ///     The match is hash-based: when no hash scan exists for the selected system, the
    ///     user is prompted to run one in the background first.
    /// </summary>
    public async Task HandleShowGamesWithRetroAchievementsAsync()
    {
        try
        {
            if (_host.IsLoadingGames) _host.CancelAndRecreateToken();

            var selectedSystem = _host.GetSelectedSystem();

            // No system selected yet: keep the old behavior of asking the user to pick one
            if (string.IsNullOrEmpty(selectedSystem))
            {
                _playSoundEffects.PlayNotificationSound();
                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("FilteringRetroAchievements") ??
                    "Filtering games with achievements...");

                _host.DeselectTopLetterNumberMenu();
                _host.SetSearchTextBoxText("");
                _host.SetCurrentFilter(null);
                _host.SetActiveSearchQueryOrMode(AppConstants.RetroAchievements);

                await _host.LoadGameFilesAsync(null, AppConstants.RetroAchievements, _host.CurrentCancellationToken);
                return;
            }

            // Prevent parallel hash calculations (that could crash the application)
            if (_raHashScanner.IsScanning)
            {
                _toastNotificationService.ShowToast(
                    (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements",
                    (string)Application.Current.TryFindResource("RaHashCalculationInProgress") ??
                    "A RetroAchievements hash calculation is already in progress. Please wait for it to finish before trying again.");
                return;
            }

            // If no valid hash scan result exists yet (missing or produced by older hash
            // logic), ask the user to scan the game path first.
            // IsScanUpToDate performs synchronous file I/O + JSON deserialization, so it
            // runs on a worker thread to keep the UI responsive for large libraries.
            var isScanUpToDate = await Task.Run(() => _raHashScanner.IsScanUpToDate(selectedSystem));
            if (!isScanUpToDate)
            {
                if (!_raHashScanner.IsSystemScannable(selectedSystem))
                {
                    _toastNotificationService.ShowToast(
                        (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements",
                        $"{selectedSystem} {(string)Application.Current.TryFindResource("RaHashSystemNotSupported") ?? "is not supported for RetroAchievements hashing."}");
                    return;
                }

                var result = await _messageBoxLibrary.ScanGamePathForRetroAchievementsMessageBoxAsync();
                if (result != MessageBoxResult.Yes)
                {
                    // User cancelled: do not filter the list of games
                    return;
                }

                var selectedManager = _host.GetSystemManagers()
                    .FirstOrDefault(m => m.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
                if (selectedManager == null) return;

                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("CalculatingRetroAchievementsHashes") ??
                    "Calculating RetroAchievements hashes...");

                _ = _raHashScanner.ScanSystemAsync(
                    selectedManager.SystemName,
                    selectedManager.SystemFolders,
                    selectedManager.FileFormatsToSearch,
                    selectedManager.FileFormatsToLaunch,
                    selectedManager.DisableRecursiveSearch,
                    selectedManager.GroupByFolder,
                    ShowHashScanCompletedToast);

                // Non-blocking notification: the app stays fully responsive while
                // the hash calculation runs in the background
                _toastNotificationService.ShowToast(
                    (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements",
                    (string)Application.Current.TryFindResource("RaHashScanInBackgroundMessage") ??
                    "The hash calculation will happen in the background. You can click the filter button again later to see if the hashing is complete.");
                return;
            }

            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("FilteringRetroAchievements") ??
                                           "Filtering games with achievements...");

            _host.DeselectTopLetterNumberMenu();
            _host.SetSearchTextBoxText("");
            _host.SetCurrentFilter(null);
            _host.SetActiveSearchQueryOrMode(AppConstants.RetroAchievements);

            await _host.LoadGameFilesAsync(null, AppConstants.RetroAchievements, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavShowGamesWithRetroAchievementsButtonClickAsync");
        }
    }

    // ---- Rescan RetroAchievements for Selected System ----

    /// <summary>
    ///     Rescans the game folders of the selected system and recalculates its
    ///     RetroAchievements hashes in the background (unchanged files are skipped
    ///     by the scanner).
    /// </summary>
    public Task HandleRescanRetroAchievementsForSelectedSystemAsync()
    {
        try
        {
            var selectedSystem = _host.GetSelectedSystem();
            if (string.IsNullOrEmpty(selectedSystem))
            {
                _toastNotificationService.ShowToast(
                    (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements",
                    (string)Application.Current.TryFindResource("RaHashNoSystemSelected") ??
                    "Please select a system before rescanning RetroAchievements hashes.");
                return Task.CompletedTask;
            }

            if (_raHashScanner.IsScanning)
            {
                _toastNotificationService.ShowToast(
                    (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements",
                    (string)Application.Current.TryFindResource("RaHashCalculationInProgress") ??
                    "A RetroAchievements hash calculation is already in progress. Please wait for it to finish before trying again.");
                return Task.CompletedTask;
            }

            if (!_raHashScanner.IsSystemScannable(selectedSystem))
            {
                _toastNotificationService.ShowToast(
                    (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements",
                    $"{selectedSystem} {(string)Application.Current.TryFindResource("RaHashSystemNotSupported") ?? "is not supported for RetroAchievements hashing."}");
                return Task.CompletedTask;
            }

            var selectedManager = _host.GetSystemManagers()
                .FirstOrDefault(m => m.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
            if (selectedManager == null) return Task.CompletedTask;

            _updateStatusBar.UpdateContent(
                (string)Application.Current.TryFindResource("CalculatingRetroAchievementsHashes") ??
                "Calculating RetroAchievements hashes...");

            _ = _raHashScanner.ScanSystemAsync(
                selectedManager.SystemName,
                selectedManager.SystemFolders,
                selectedManager.FileFormatsToSearch,
                selectedManager.FileFormatsToLaunch,
                selectedManager.DisableRecursiveSearch,
                selectedManager.GroupByFolder,
                ShowHashScanCompletedToast,
                force: true);

            // Non-blocking notification: the app stays fully responsive while
            // the hash calculation runs in the background
            _toastNotificationService.ShowToast(
                (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements",
                (string)Application.Current.TryFindResource("RaHashScanInBackgroundMessage") ??
                "The hash calculation will happen in the background. You can click the filter button again later to see if the hashing is complete.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method HandleRescanRetroAchievementsForSelectedSystemAsync");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Shows a completion toast after a system hash scan finishes.
    ///     Runs on a background thread, so every WPF/UI-touch is marshaled to the
    ///     dispatcher with BeginInvoke (fire-and-forget) — the scanner thread is
    ///     never blocked waiting on the UI thread.
    /// </summary>
    private void ShowHashScanCompletedToast(string systemName)
    {
        try
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                ShowHashScanCompletedToastCore(systemName);
            else
                _ = dispatcher.BeginInvoke(() => ShowHashScanCompletedToastCore(systemName));
        }
        catch (Exception ex)
        {
            _logger.Debug($"[RA Hash Scanner] Failed to show completion toast: {ex.Message}");
        }
    }

    /// <summary>
    ///     Runs on the UI thread: resolves the localized strings and shows the completion toast.
    /// </summary>
    private void ShowHashScanCompletedToastCore(string systemName)
    {
        var title = (string)Application.Current.TryFindResource("RetroAchievements") ?? "RetroAchievements";
        var template = (string)Application.Current.TryFindResource("RaHashCalculationComplete") ??
                       "RetroAchievements hash calculation is complete for {0}.";
        _toastNotificationService.ShowToast(title, string.Format(template, systemName));
    }

    /// <summary>
    ///     Increases the thumbnail size by one zoom step and reloads the game list.
    /// </summary>
    public async Task HandleZoomInAsync()
    {
        try
        {
            _host.CancelAndRecreateToken();
            _playSoundEffects.PlayNotificationSound();

            var isSystemSelectionMode = !_host.IsTopSystemSelectionVisible();

            if (isSystemSelectionMode)
            {
                var newSize = Math.Min(MaxThumbnailSizeForSystem, _settings.ThumbnailSizeForSystem + ZoomStep);
                if (newSize != _settings.ThumbnailSizeForSystem)
                {
                    _settings.ThumbnailSizeForSystem = newSize;
                    await _settings.SaveAsync();
                    _menuCheckMarkService.UpdateThumbnailSizeCheckMarks(newSize);
                }

                _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("ZoomingIn") ??
                                               "Zooming in...");
                var (sl, sq) = _host.GetLoadGameFilesParams();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            else
            {
                var newSize = Math.Min(MaxThumbnailSize, _settings.ThumbnailSize + ZoomStep);
                if (newSize != _settings.ThumbnailSize)
                {
                    _settings.ThumbnailSize = newSize;
                    _host.SetGameButtonImageHeight(newSize);
                    await _settings.SaveAsync();
                    _menuCheckMarkService.UpdateThumbnailSizeCheckMarks(newSize);
                }

                _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("ZoomingIn") ??
                                               "Zooming in...");
                var (sl, sq) = _host.GetLoadGameFilesParams();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method HandleZoomInAsync");
        }
    }

    /// <summary>
    ///     Decreases the thumbnail size by one zoom step and reloads the game list.
    /// </summary>
    public async Task HandleZoomOutAsync()
    {
        try
        {
            _host.CancelAndRecreateToken();
            _playSoundEffects.PlayNotificationSound();

            var isSystemSelectionMode = !_host.IsTopSystemSelectionVisible();

            if (isSystemSelectionMode)
            {
                var newSize = Math.Max(MinThumbnailSize, _settings.ThumbnailSizeForSystem - ZoomStep);
                if (newSize != _settings.ThumbnailSizeForSystem)
                {
                    _settings.ThumbnailSizeForSystem = newSize;
                    await _settings.SaveAsync();
                    _menuCheckMarkService.UpdateThumbnailSizeCheckMarks(newSize);
                }

                _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("ZoomingOut") ??
                                               "Zooming out...");
                var (sl, sq) = _host.GetLoadGameFilesParams();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            else
            {
                var newSize = Math.Max(MinThumbnailSize, _settings.ThumbnailSize - ZoomStep);
                if (newSize != _settings.ThumbnailSize)
                {
                    _settings.ThumbnailSize = newSize;
                    _host.SetGameButtonImageHeight(newSize);
                    await _settings.SaveAsync();
                    _menuCheckMarkService.UpdateThumbnailSizeCheckMarks(newSize);
                }

                _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("ZoomingOut") ??
                                               "Zooming out...");
                var (sl, sq) = _host.GetLoadGameFilesParams();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method HandleZoomOutAsync");
        }
    }

    // ---- View Mode ----

    /// <summary>
    ///     Toggles between grid view and list view and reloads the game list.
    /// </summary>
    public async Task HandleToggleViewModeAsync()
    {
        try
        {
            _host.CancelAndRecreateToken();

            _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("TogglingViewMode") ??
                                           "Toggling view mode...");
            _playSoundEffects.PlayNotificationSound();

            if (string.Equals(_host.GetViewMode(), "GridView", StringComparison.Ordinal))
            {
                _host.SetGridViewChecked(false);
                _host.SetListViewChecked(true);
                _host.SetGameFileGridVisible(false);
                _host.SetListViewPreviewAreaVisible(true);
                _settings.ViewMode = "ListView";
            }
            else
            {
                _host.SetGridViewChecked(true);
                _host.SetListViewChecked(false);
                _host.SetGameFileGridVisible(true);
                _host.SetListViewPreviewAreaVisible(false);
                _settings.ViewMode = "GridView";
            }

            await _settings.SaveAsync();

            var (sl, sq) = _host.GetLoadGameFilesParams();
            await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavToggleViewModeClickAsync");
        }
    }

    /// <summary>
    ///     Changes the view mode based on the sender menu item and reloads the game list.
    /// </summary>
    /// <param name="sender">The menu item that triggered the view mode change.</param>
    public async Task HandleChangeViewModeAsync(object sender)
    {
        try
        {
            _host.CancelAndRecreateToken();

            _playSoundEffects.PlayNotificationSound();

            switch (sender)
            {
                case MenuItem mi when string.Equals(mi.Name, _host.GridViewMenuItemId, StringComparison.Ordinal):
                    _host.SetGridViewChecked(true);
                    _host.SetListViewChecked(false);
                    _settings.ViewMode = "GridView";

                    _host.SetGameFileGridVisible(true);
                    _host.SetListViewPreviewAreaVisible(false);

                    _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("ChangingViewMode") ??
                                                   "Changing view mode...");
                    break;
                case MenuItem mi2 when string.Equals(mi2.Name, _host.ListViewMenuItemId, StringComparison.Ordinal):
                    _host.SetGridViewChecked(false);
                    _host.SetListViewChecked(true);
                    _settings.ViewMode = "ListView";

                    _host.SetGameFileGridVisible(false);
                    _host.SetListViewPreviewAreaVisible(true);

                    _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("ChangingViewMode") ??
                                                   "Changing view mode...");
                    break;
            }

            await _settings.SaveAsync();

            var (sl, sq) = _host.GetLoadGameFilesParams();
            await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            const string errorMessage = "Error while using the method ChangeViewMode_Click.";
            _logger.Error(ex, errorMessage);
            await _messageBoxLibrary.ErrorChangingViewModeMessageBoxAsync();
        }
    }

    // ---- Filename Display Mode ----

    /// <summary>
    ///     Changes the filename display mode and reloads the game list.
    /// </summary>
    /// <param name="mode">The filename display mode to apply.</param>
    public async Task HandleFilenameDisplayModeAsync(string mode)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                _playSoundEffects.PlayNotificationSound();

                _settings.FilenameDisplayMode = mode;
                await _settings.SaveAsync();

                _menuCheckMarkService.UpdateFilenameDisplayModeCheckMarks(mode);

                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("ChangingFilenameDisplayMode") ??
                    "Changing filename display mode...");

                if (string.Equals(_host.GetViewMode(), "GridView", StringComparison.Ordinal))
                {
                    var (sl, sq) = _host.GetLoadGameFilesParams();
                    _host.SetLoadingState(true,
                        (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                    await Task.Yield();
                    await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in method FilenameDisplayMode_ClickAsync");
                await _messageBoxLibrary.ErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method FilenameDisplayMode_ClickAsync");
        }
    }

    // ---- Display Machine Name ----

    /// <summary>
    ///     Toggles the display of MAME machine names on game buttons and reloads the game list.
    /// </summary>
    /// <param name="isChecked">True to display machine names; false to hide them.</param>
    public async Task HandleDisplayMachineNameAsync(bool isChecked)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                _playSoundEffects.PlayNotificationSound();

                _settings.DisplayMachineName = isChecked;
                await _settings.SaveAsync();

                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("ChangingDisplayMachineName") ??
                    "Changing machine name display...");

                if (string.Equals(_host.GetViewMode(), "GridView", StringComparison.Ordinal))
                {
                    var (sl, sq) = _host.GetLoadGameFilesParams();
                    _host.SetLoadingState(true,
                        (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                    await Task.Yield();
                    await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in method DisplayMachineName_ClickAsync");
                await _messageBoxLibrary.ErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method DisplayMachineName_ClickAsync");
        }
    }

    // ---- Filename Font Size ----

    /// <summary>
    ///     Changes the filename font size and reloads the game list.
    /// </summary>
    /// <param name="size">The font size to apply to filenames.</param>
    public async Task HandleFilenameFontSizeAsync(string size)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                _playSoundEffects.PlayNotificationSound();

                _settings.FilenameFontSize = size;
                await _settings.SaveAsync();

                _menuCheckMarkService.UpdateFilenameFontSizeCheckMarks(size);

                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("ChangingFilenameFontSize") ??
                    "Changing filename font size...");

                if (string.Equals(_host.GetViewMode(), "GridView", StringComparison.Ordinal))
                {
                    var (sl, sq) = _host.GetLoadGameFilesParams();
                    _host.SetLoadingState(true,
                        (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                    await Task.Yield();
                    await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in method FilenameFontSize_ClickAsync");
                await _messageBoxLibrary.ErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method FilenameFontSize_ClickAsync");
        }
    }

    // ---- Machine Name Font Size ----

    /// <summary>
    ///     Changes the machine name font size and reloads the game list.
    /// </summary>
    /// <param name="size">The font size to apply to machine names.</param>
    public async Task HandleMachineNameFontSizeAsync(string size)
    {
        try
        {
            _host.CancelAndRecreateToken();

            try
            {
                _playSoundEffects.PlayNotificationSound();

                _settings.MachineNameFontSize = size;
                await _settings.SaveAsync();

                _menuCheckMarkService.UpdateMachineNameFontSizeCheckMarks(size);

                _updateStatusBar.UpdateContent(
                    (string)Application.Current.TryFindResource("ChangingMachineNameFontSize") ??
                    "Changing machine name font size...");

                if (string.Equals(_host.GetViewMode(), "GridView", StringComparison.Ordinal))
                {
                    var (sl, sq) = _host.GetLoadGameFilesParams();
                    _host.SetLoadingState(true,
                        (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
                    await Task.Yield();
                    await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in method MachineNameFontSize_ClickAsync");
                await _messageBoxLibrary.ErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method MachineNameFontSize_ClickAsync");
        }
    }

    // ---- Sound Configuration ----

    /// <summary>
    ///     Opens the sound configuration window.
    /// </summary>
    public async Task HandleSoundConfigurationAsync()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningSoundConfigurationSettings") ??
                "Opening Sound Configuration settings...");

            var soundConfigWindow = _serviceProvider.GetRequiredService<SoundConfigurationWindow>();
            soundConfigWindow.Owner = Application.Current.MainWindow;
            soundConfigWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening Sound Configuration window");
            await _messageBoxLibrary.CouldNotOpenSoundConfigurationWindowMessageBoxAsync();
        }
    }

    // ---- RetroAchievements Settings ----

    /// <summary>
    ///     Opens the RetroAchievements settings window.
    /// </summary>
    public async Task HandleShowRetroAchievementsSettingsAsync()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBar.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningRetroAchievementsSettings") ??
                "Opening RetroAchievements settings...");

            var raSettingsWindow = _serviceProvider.GetRequiredService<RetroAchievementsSettingsWindow>();
            raSettingsWindow.Owner = Application.Current.MainWindow;
            raSettingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening RetroAchievements settings window");
            await _messageBoxLibrary.ErrorMessageBoxAsync();
        }
    }

    // ---- Overlay Button Toggles ----

    /// <summary>
    ///     Toggles the RetroAchievements overlay button visibility and reloads the game list.
    /// </summary>
    /// <param name="isChecked">True to show the button; false to hide it.</param>
    public async Task HandleToggleRetroAchievementButtonAsync(bool isChecked)
    {
        _host.CancelAndRecreateToken();

        _updateStatusBar.UpdateContent(
            (string)Application.Current.TryFindResource("TogglingRetroAchievementsOverlayButton") ??
            "Toggling RetroAchievements overlay button...");
        try
        {
            _playSoundEffects.PlayNotificationSound();

            _settings.OverlayRetroAchievementButton = isChecked;
            await _settings.SaveAsync();

            var (sl, sq) = _host.GetLoadGameFilesParams();
            _ = _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error toggling RetroAchievements overlay button");
            await _messageBoxLibrary.ErrorMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Toggles the video link overlay button visibility and reloads the game list.
    /// </summary>
    /// <param name="isChecked">True to show the button; false to hide it.</param>
    public async Task HandleToggleVideoLinkButtonAsync(bool isChecked)
    {
        _host.CancelAndRecreateToken();

        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("TogglingVideoLinkOverlayButton") ??
                                       "Toggling video link overlay button...");
        try
        {
            _playSoundEffects.PlayNotificationSound();

            _settings.OverlayOpenVideoButton = isChecked;
            await _settings.SaveAsync();

            var (sl, sq) = _host.GetLoadGameFilesParams();
            _ = _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error toggling video link overlay button");
            await _messageBoxLibrary.ErrorMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Toggles the info link overlay button visibility and reloads the game list.
    /// </summary>
    /// <param name="isChecked">True to show the button; false to hide it.</param>
    public async Task HandleToggleInfoLinkButtonAsync(bool isChecked)
    {
        _host.CancelAndRecreateToken();

        _updateStatusBar.UpdateContent((string)Application.Current.TryFindResource("TogglingInfoLinkOverlayButton") ??
                                       "Toggling info link overlay button...");
        try
        {
            _playSoundEffects.PlayNotificationSound();

            _settings.OverlayOpenInfoButton = isChecked;
            await _settings.SaveAsync();

            var (sl, sq) = _host.GetLoadGameFilesParams();
            _ = _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error toggling info link overlay button");
            await _messageBoxLibrary.ErrorMessageBoxAsync();
        }
    }

    // ---- Language ----

    /// <summary>
    ///     Delegates the language change to the host's language menu service.
    /// </summary>
    /// <param name="languageCode">The two-letter language code to apply.</param>
    public void HandleChangeLanguage(string languageCode)
    {
        _host.ChangeLanguageAsync(languageCode);
    }

    // ---- Top Letter/Number Menu ----

    /// <summary>
    ///     Filters the game list by the selected letter or number and reloads the display.
    /// </summary>
    /// <param name="selectedLetter">The letter or number to filter by.</param>
    public async Task HandleTopLetterNumberMenuClickAsync(string selectedLetter)
    {
        try
        {
            if (_host.IsLoadingGames) _host.CancelAndRecreateToken();

            _playSoundEffects.PlayNotificationSound();

            _host.ResetPaginationButtons();
            _host.SetSearchTextBoxText("");
            _host.SetCurrentFilter(selectedLetter);
            _host.SetActiveSearchQueryOrMode(null);

            _host.SetLoadingState(true,
                (string)Application.Current.TryFindResource("LoadingGames") ?? "Loading Games...");
            await Task.Yield();

            await _host.LoadGameFilesAsync(selectedLetter, null, _host.CurrentCancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in TopLetterNumberMenuClickAsync");
        }
    }

    // ---- Sort Order Toggle ----

    /// <summary>
    ///     Toggles the MAME sort order between filename and machine description, then reloads the game list.
    /// </summary>
    public async Task HandleSortOrderToggleAsync()
    {
        try
        {
            if (_host.IsLoadingGames) return;

            _host.CancelAndRecreateToken();

            _playSoundEffects.PlayNotificationSound();
            var currentSort = _host.GetMameSortOrder();
            var newSort = string.Equals(currentSort, AppConstants.MameSortOrderFileName, StringComparison.Ordinal)
                ? AppConstants.MameSortOrderMachineDescription
                : AppConstants.MameSortOrderFileName;
            _host.SetMameSortOrder(newSort);
            _host.UpdateSortOrderButtonUi();

            _host.SetIsResortOperation(true);
            try
            {
                var (sl, sq) = _host.GetLoadGameFilesParams();
                await _host.LoadGameFilesAsync(sl, sq, _host.CurrentCancellationToken);
            }
            finally
            {
                _host.SetIsResortOperation(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in SortOrderToggleButtonClickAsync");
            _logger.Debug("Error in SortOrderToggleButtonClickAsync");
        }
    }
}