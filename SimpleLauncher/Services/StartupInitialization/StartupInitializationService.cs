using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.LanguageMenu;
using SimpleLauncher.Services.NotificationToast;
using SimpleLauncher.Services.ThemeMenu;
using CheckDirWritable = SimpleLauncher.Core.Services.CheckIfDirectoryIsWritable.CheckIfDirectoryIsWritableService;
using RequiredFiles = SimpleLauncher.Core.Services.CheckForRequiredFilesService;
using Settings = SimpleLauncher.Core.Services.SettingsManager.SettingsManagerService;
using TrayIconManager = SimpleLauncher.Services.TrayIcon.TrayIconManager;

namespace SimpleLauncher.Services.StartupInitialization;

/// <summary>
///     Orchestrates application startup initialization, including theme, language, tray icon, gamepad, and required file
///     checks.
/// </summary>
public class StartupInitializationService
{
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly IConfiguration _configuration;
    private readonly GamePadController _gamePadController;
    private readonly LanguageMenuService _languageMenuService;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly RequiredFiles _requiredFiles;
    private readonly Settings _settings;
    private readonly ThemeMenuService _themeMenuService;
    private readonly IToastNotificationService _toastNotificationService;
    private IStartupInitializationHost _host = null!;

    /// <summary>
    ///     Initializes a new instance of the StartupInitializationService with the specified dependencies.
    /// </summary>
    public StartupInitializationService(
        IConfiguration configuration,
        Settings settings,
        GamePadController gamePadController,
        ThemeMenuService themeMenuService,
        LanguageMenuService languageMenuService,
        IMessageBoxLibraryService messageBoxLibrary,
        IApplicationLifetime applicationLifetime,
        ILogger logger,
        IToastNotificationService toastNotificationService)
    {
        _configuration = configuration;
        _settings = settings;
        _gamePadController = gamePadController;
        _themeMenuService = themeMenuService;
        _languageMenuService = languageMenuService;
        _messageBoxLibrary = messageBoxLibrary;
        _applicationLifetime = applicationLifetime;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toastNotificationService = toastNotificationService ??
                                    throw new ArgumentNullException(nameof(toastNotificationService));
        _requiredFiles = new RequiredFiles(messageBoxLibrary);
    }

    /// <summary>
    ///     Performs all startup initialization steps using the provided host for UI interaction.
    /// </summary>
    public async Task InitializeAsync(IStartupInitializationHost host)
    {
        _host = host;

        InitializeStatusBarTimer();
        ApplyInitialThemeAndLanguage();
        InitializeUiState();
        await CheckWriteAccessAsync();
        InitializePagination();
        InitializeTrayIcon();
        await CheckRequiredFilesAsync();
        InitializeOverlayButtons();
        InitializeGamePad();
    }

    private void InitializeStatusBarTimer()
    {
        var statusBarTimeoutSeconds = _configuration.GetValue("StatusBarTimeoutSeconds", 3);
        _host.StatusBarTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(statusBarTimeoutSeconds)
        };
        _host.StatusBarTimer.Tick += (_, _) =>
        {
            _host.StatusBarText.Content = "";
            _host.StatusBarTimer.Stop();
        };

        _logger.Debug("StatusBarTimer was initialized");
    }

    private void ApplyInitialThemeAndLanguage()
    {
        _languageMenuService.SetLanguageCheckMarks(_settings.Language);
        _logger.Debug("Language and menu was set");

        App.ChangeTheme(_settings.BaseTheme, _settings.AccentColor);
        _themeMenuService.SetCheckedTheme(_settings.BaseTheme, _settings.AccentColor);
        _logger.Debug("Theme was set");
    }

    private void InitializeUiState()
    {
        var nosystemselected = (string)Application.Current.TryFindResource("Nosystemselected") ?? "No system selected";
        _host.SelectedSystem = nosystemselected;
        _host.PlayTime = "00:00:00";
        _logger.Debug("SelectedSystem and PlayTime was set");

        _host.SetViewMode(_settings.ViewMode);
        _logger.Debug("ViewMode was set");
    }

    private async Task CheckWriteAccessAsync()
    {
        if (!CheckDirWritable.IsWritableDirectory(AppDomain.CurrentDomain.BaseDirectory, _logger))
        {
            await _messageBoxLibrary.MoveToWritableFolderMessageBoxAsync();
            _logger.Debug("Application does not have write access");
        }
    }

    private void InitializePagination()
    {
        _host.SetPaginationButtonsDefault();
        _logger.Debug("Pagination was set");
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _host.SetTrayIconManager(new TrayIconManager(_host.HostWindow, _applicationLifetime, _logger,
                _toastNotificationService));
            _logger.Debug("TrayIconManager was initialized");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error initializing the tray icon. The application will continue without it");
        }
    }

    private async Task CheckRequiredFilesAsync()
    {
        try
        {
            await _requiredFiles.CheckFilesAsync(_configuration, _logger);
            _logger.Debug("Required files were checked");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method CheckRequiredFilesAsync");
        }
    }

    private void InitializeOverlayButtons()
    {
        _host.RetroAchievementButton.IsChecked = _settings.OverlayRetroAchievementButton;
        _host.VideoLinkButton.IsChecked = _settings.OverlayOpenVideoButton;
        _host.InfoLinkButton.IsChecked = _settings.OverlayOpenInfoButton;
        _logger.Debug("Overlay buttons were set");
    }

    private void InitializeGamePad()
    {
        _gamePadController.ErrorLogger = (ex, msg) => _logger.Error(ex, msg);
        if (_settings.EnableGamePadNavigation)
            _ = _gamePadController.StartAsync();
        else
            _ = _gamePadController.StopAsync();

        _gamePadController.DeadZoneX = _settings.DeadZoneX;
        _gamePadController.DeadZoneY = _settings.DeadZoneY;
        _logger.Debug("GamePadController was initialized");
    }
}