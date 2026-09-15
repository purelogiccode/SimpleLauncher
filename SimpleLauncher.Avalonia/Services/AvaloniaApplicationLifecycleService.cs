using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.UsageStats;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Manages the application lifecycle: startup initialization, the silent update
///     check, usage reporting, play-history migration, and game file watching.
///     Avalonia port of the WPF <c>ApplicationLifecycleService</c> (no WPF dependencies).
/// </summary>
public class AvaloniaApplicationLifecycleService
{
    private readonly AvaloniaGameFileWatcherService _gameFileWatcherService;
    private readonly ILogger _logger;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly AvaloniaStartupInitializationService _startupInitializationService;
    private readonly Stats _stats;
    private readonly AvaloniaCheckForUpdatesService _updateChecker;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AvaloniaApplicationLifecycleService" /> class.
    /// </summary>
    /// <param name="startupInitializationService">The startup initialization service.</param>
    /// <param name="updateChecker">The service for checking application updates.</param>
    /// <param name="stats">The usage statistics reporting service.</param>
    /// <param name="playHistoryManager">The manager for play history entries.</param>
    /// <param name="gameFileWatcherService">The service for monitoring game file changes.</param>
    /// <param name="logger">The Serilog logger.</param>
    public AvaloniaApplicationLifecycleService(
        AvaloniaStartupInitializationService startupInitializationService,
        AvaloniaCheckForUpdatesService updateChecker,
        Stats stats,
        PlayHistoryManager playHistoryManager,
        AvaloniaGameFileWatcherService gameFileWatcherService,
        ILogger logger)
    {
        _startupInitializationService = startupInitializationService;
        _updateChecker = updateChecker;
        _stats = stats;
        _playHistoryManager = playHistoryManager;
        _gameFileWatcherService = gameFileWatcherService;
        _logger = logger;
    }

    /// <summary>
    ///     Runs the startup initialization sequence (status-bar timer, write-access check,
    ///     pagination defaults, required-files check). No-ops safe to call once per launch.
    /// </summary>
    public async Task RunStartupInitializationAsync()
    {
        _startupInitializationService.InitializeStatusBarTimer();
        await _startupInitializationService.CheckWriteAccessAsync();
        _startupInitializationService.ResetPaginationDefaults();
        _startupInitializationService.InitializeGamePad();
        await _startupInitializationService.CheckRequiredFilesAsync();
    }

    /// <summary>
    ///     Silently checks for application updates without notifying the user.
    /// </summary>
    public Task SilentCheckForUpdatesAsync()
    {
        return _updateChecker.SilentCheckForUpdatesAsync();
    }

    /// <summary>
    ///     Reports anonymous usage statistics.
    /// </summary>
    public async Task ReportUsageAsync()
    {
        try
        {
            await _stats.CallApiAsync();
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Usage stats reporting failed");
        }
    }

    /// <summary>
    ///     Migrates play history entries from file names to full paths for the given systems.
    /// </summary>
    /// <param name="systems">The configured systems used to resolve legacy entries.</param>
    public Task MigratePlayHistoryAsync(List<SystemManagerConfig> systems)
    {
        return _playHistoryManager.MigrateFilenamesToFullPathsAsync(systems);
    }

    /// <summary>
    ///     Occurs when the set of game files being watched changes.
    /// </summary>
    public event EventHandler<EventArgs<string>>? GameFilesChanged
    {
        add => _gameFileWatcherService.GameFilesChanged += value;
        remove => _gameFileWatcherService.GameFilesChanged -= value;
    }

    /// <summary>
    ///     Starts watching the given folders for game file changes for the specified system.
    /// </summary>
    /// <param name="folders">The folders to watch for game file changes.</param>
    /// <param name="systemName">The name of the system the folders belong to.</param>
    /// <param name="fileExtensions">The optional file extensions to filter the watched files by.</param>
    public void StartWatching(IEnumerable<string> folders, string systemName,
        IEnumerable<string>? fileExtensions = null)
    {
        _gameFileWatcherService.StartWatchingForSystems(
        [
            new SystemManagerConfig
            {
                SystemName = systemName,
                SystemFolders = folders.ToList(),
                FileFormatsToSearch = fileExtensions?.ToList() ?? []
            }
        ]);
    }

    /// <summary>
    ///     Stops watching for game file changes.
    /// </summary>
    public void StopWatching()
    {
        _gameFileWatcherService.StopWatching();
    }
}