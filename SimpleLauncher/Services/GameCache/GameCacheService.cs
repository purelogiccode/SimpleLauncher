using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.SystemManager;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameCache;

/// <summary>
///     Thread-safe in-memory cache for game file lists, providing fast access to
///     all games and search results for the currently selected system.
/// </summary>
public class GameCacheService : IGameCacheService, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger;
    private List<string> _allGamesForCurrentSystem = [];
    private List<string> _currentSearchResults = [];

    /// <summary>
    ///     Initializes a new instance of <see cref="GameCacheService" />.
    /// </summary>
    /// <param name="logger">Error logging service.</param>
    public GameCacheService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets the name of the system whose games are currently cached.
    /// </summary>
    public string SelectedSystem { get; private set; } = "";

    /// <summary>
    ///     Returns a snapshot of all cached game file paths for the current system.
    /// </summary>
    public async Task<IList<string>> GetAllGamesAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return [.. _allGamesForCurrentSystem];
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Returns a snapshot of the current search result file paths.
    /// </summary>
    public async Task<IList<string>> GetSearchResultsAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return [.. _currentSearchResults];
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Determines whether the cache already contains data for the specified system.
    /// </summary>
    public async Task<bool> IsCachePopulatedForSystemAsync(string systemName, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _allGamesForCurrentSystem.Count > 0 &&
                   string.Equals(SelectedSystem, systemName, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Replaces the cached list of all games for the specified system.
    /// </summary>
    public async Task SetAllGamesAsync(IList<string> games, string systemName, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _allGamesForCurrentSystem = new List<string>(games);
            SelectedSystem = systemName;
            _logger.Debug($"[GameCacheService] SetAllGames for '{systemName}'. Count: {games.Count}");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Replaces the cached search results with the provided file list.
    /// </summary>
    public async Task SetSearchResultsAsync(IList<string> results, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _currentSearchResults = new List<string>(results);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Returns the appropriate source list for re-sorting based on whether an active filter is applied.
    /// </summary>
    public async Task<(List<string> allGames, List<string> searchResults)> GetResortSourceAsync(
        bool hasActiveFilter, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var source = hasActiveFilter
                ? new List<string>(_currentSearchResults)
                : new List<string>(_allGamesForCurrentSystem);
            return (source, [.. _currentSearchResults]);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Populates the cache from disk by scanning the system's configured folders,
    ///     skipping if the cache is already populated for the same system.
    /// </summary>
    public async Task PopulateFromDiskAsync(SystemManagerService config,
        IGetListOfFilesService fileService, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_allGamesForCurrentSystem.Count > 0 &&
                string.Equals(SelectedSystem, config.SystemName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug(
                    $"[GameCacheService] Using cached list for '{config.SystemName}'. Count: {_allGamesForCurrentSystem.Count}");
                return;
            }

            _logger.Debug($"[GameCacheService] Populating from disk for '{config.SystemName}'.");
            var uniqueFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in config.SystemFolders)
            {
                ct.ThrowIfCancellationRequested();

                var resolvedPath = PathHelper.ResolveRelativeToAppDirectory(folder);
                if (string.IsNullOrEmpty(resolvedPath) ||
                    !Directory.Exists(resolvedPath) ||
                    config.FileFormatsToSearch == null)
                {
                    continue;
                }

                var filesInFolder = await fileService.GetFilesAsync(resolvedPath, config.FileFormatsToSearch,
                    config.DisableRecursiveSearch, config.GroupByFolder, ct);
                foreach (var file in filesInFolder) uniqueFiles.TryAdd(Path.GetFileName(file), file);
            }

            _allGamesForCurrentSystem = uniqueFiles.Values.ToList();
            SelectedSystem = config.SystemName;
            _logger.Debug($"[GameCacheService] Populated {_allGamesForCurrentSystem.Count} games.");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Clears all cached game and search result data, requiring a fresh load on next access.
    /// </summary>
    public async Task InvalidateAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _allGamesForCurrentSystem.Clear();
            _currentSearchResults.Clear();
            _logger.Debug("[GameCacheService] All game file caches invalidated");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Synchronously clears all cached data, attempting to acquire the lock with a short timeout.
    /// </summary>
    public void ClearSync()
    {
        try
        {
            if (_lock.Wait(5000))
            {
                try
                {
                    _currentSearchResults?.Clear();
                    _allGamesForCurrentSystem?.Clear();
                }
                finally
                {
                    _lock.Release();
                }
            }
            else
            {
                _logger?.Debug("GameCacheService.ClearSync timed out waiting for lock after 5 seconds");
            }
        }
        catch (ObjectDisposedException)
        {
            // Semaphore was disposed, ignore
        }
    }
}