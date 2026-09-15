using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckIfDirectoryIsWritable;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Orchestrates application startup initialization tasks that the Avalonia app
///     performs after the main window is shown: the status-bar timeout timer, the
///     write-access check, the required-files check, pagination defaults, and
///     gamepad controller initialization.
///     Avalonia port of the WPF <c>StartupInitializationService</c>.
/// </summary>
public class AvaloniaStartupInitializationService
{
    private readonly IConfiguration _configuration;
    private readonly GamePadController _gamePadController;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly CheckForRequiredFilesService _requiredFiles;
    private readonly SettingsManagerService _settings;
    private DispatcherTimer? _statusBarTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AvaloniaStartupInitializationService" /> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="messageBox">The message box service (used by the required-files check).</param>
    /// <param name="logger">The Serilog logger.</param>
    /// <param name="requiredFiles">The required-files checker.</param>
    /// <param name="gamePadController">The gamepad input controller.</param>
    /// <param name="settings">The application settings manager.</param>
    public AvaloniaStartupInitializationService(
        IConfiguration configuration,
        IMessageBoxLibraryService messageBox,
        ILogger logger,
        CheckForRequiredFilesService requiredFiles,
        GamePadController gamePadController,
        SettingsManagerService settings)
    {
        _configuration = configuration;
        _messageBox = messageBox;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requiredFiles = requiredFiles;
        _gamePadController = gamePadController;
        _settings = settings;
    }

    /// <summary>
    ///     Raised (on the UI thread) when the status-bar timeout elapses; the host clears
    ///     its status text.
    /// </summary>
    public event Action? StatusBarTimeout;

    /// <summary>
    ///     Raised when the pagination buttons should be reset to their defaults; the host
    ///     disables both navigation buttons.
    /// </summary>
    public event Action? PaginationReset;

    /// <summary>
    ///     Starts the status-bar timeout timer (clears the status text after
    ///     <c>StatusBarTimeoutSeconds</c> seconds, default 3 — same as the WPF app).
    /// </summary>
    public void InitializeStatusBarTimer()
    {
        try
        {
            var statusBarTimeoutSeconds = _configuration.GetValue("StatusBarTimeoutSeconds", 3);
            _statusBarTimer?.Stop();
            _statusBarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(statusBarTimeoutSeconds)
            };
            _statusBarTimer.Tick += (_, _) =>
            {
                StatusBarTimeout?.Invoke();
                _statusBarTimer.Stop();
            };
            _statusBarTimer.Start();
            _logger.Debug("StatusBarTimer was initialized");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize the status bar timer");
        }
    }

    /// <summary>
    ///     Checks whether the application directory is writable; when it is not, prompts
    ///     the user to move the application to a writable folder.
    /// </summary>
    public async Task CheckWriteAccessAsync()
    {
        try
        {
            _logger.Debug("Checking if the application directory is writable");
            if (!CheckIfDirectoryIsWritableService.IsWritableDirectory(AppContext.BaseDirectory, _logger))
            {
                await _messageBox.MoveToWritableFolderMessageBoxAsync();
                _logger.Debug("Application does not have write access");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check directory write access");
        }
    }

    /// <summary>
    ///     Checks that all files required by the application exist next to the executable
    ///     (mame.dat, default images, audio files, ...) and notifies the user of any
    ///     missing files.
    /// </summary>
    public async Task CheckRequiredFilesAsync()
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

    /// <summary>
    ///     Resets the pagination navigation buttons to their disabled defaults.
    /// </summary>
    public void ResetPaginationDefaults()
    {
        try
        {
            PaginationReset?.Invoke();
            _logger.Debug("Pagination was set");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to reset pagination defaults");
        }
    }

    /// <summary>
    ///     Initializes the gamepad controller: wires the error logger, applies dead zone
    ///     settings, and starts or stops the controller based on the saved preference.
    /// </summary>
    public void InitializeGamePad()
    {
        try
        {
            _gamePadController.ErrorLogger = (ex, msg) => _logger.Error(ex, msg);
            _gamePadController.DeadZoneX = _settings.DeadZoneX;
            _gamePadController.DeadZoneY = _settings.DeadZoneY;

            if (_settings.EnableGamePadNavigation)
                _ = _gamePadController.StartAsync();
            else
                _ = _gamePadController.StopAsync();

            _logger.Debug("GamePadController was initialized. Navigation enabled: {Enabled}",
                _settings.EnableGamePadNavigation);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize the gamepad controller");
        }
    }
}