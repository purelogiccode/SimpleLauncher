using System.Windows;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.RetroAchievements;
using SimpleLauncher.Services.SystemManager;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameFileLoadingOrchestrator;

/// <summary>
///     Orchestrates the game file loading pipeline: building file lists from disk or cache,
///     applying filters (letter, search, favorites, RetroAchievements), sorting, and rendering.
/// </summary>
public class GameFileLoadingOrchestratorService : IGameFileLoadingOrchestrator
{
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly IGameCacheService _gameCacheService;
    private readonly IGameFilterService _gameFilterService;
    private readonly IGameItemRenderService _gameItemRenderService;
    private readonly IGetListOfFilesService _getListOfFiles;
    private readonly ILogger _logger;
    private readonly IMameDataService _mameDataService;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IRetroAchievementsHashStore _raHashStore;
    private readonly RetroAchievementsService _retroAchievementsService;
    private readonly SettingsManagerService _settings;
    private readonly IUpdateStatusBar _updateStatusBarService;
    private IGameFileLoadingHost _host = null!;

    /// <summary>
    ///     Initializes a new instance of <see cref="GameFileLoadingOrchestratorService" /> with all required dependencies.
    /// </summary>
    public GameFileLoadingOrchestratorService(
        IGameCacheService gameCacheService,
        IGameFilterService gameFilterService,
        IMameDataService mameDataService,
        IGetListOfFilesService getListOfFiles,
        FavoritesManager favoritesManager,
        RetroAchievementsService retroAchievementsService,
        IFindCoverImageService findCoverImage,
        IGameItemRenderService gameItemRenderService,
        SettingsManagerService settings,
        IUpdateStatusBar updateStatusBarService,
        IMessageBoxLibraryService messageBox,
        ILogger logger,
        IRetroAchievementsHashStore raHashStore)
    {
        _gameCacheService = gameCacheService;
        _gameFilterService = gameFilterService;
        _mameDataService = mameDataService;
        _getListOfFiles = getListOfFiles;
        _favoritesManager = favoritesManager;
        _retroAchievementsService = retroAchievementsService;
        _findCoverImage = findCoverImage;
        _gameItemRenderService = gameItemRenderService;
        _settings = settings;
        _updateStatusBarService = updateStatusBarService;
        _messageBox = messageBox;
        _logger = logger;
        _raHashStore = raHashStore;
    }

    /// <summary>
    ///     Binds the orchestrator to the host that provides UI controls and system context.
    /// </summary>
    public void Initialize(IGameFileLoadingHost host)
    {
        _host = host;
    }

    /// <summary>
    ///     Loads game files for the currently selected system, applying letter/search/favorites/RA filters,
    ///     sorting, pagination, and rendering the results to the UI.
    /// </summary>
    public async Task LoadGameFilesAsync(string? startLetter = null, string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        _updateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("Loading") ?? "Loading...");

        await _host.SetUiBeforeLoadGameFilesAsync();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_host.SystemComboBox.SelectedItem == null)
            {
                await _host.DisplaySystemSelectionScreenAsync(cancellationToken);
                return;
            }

            var selectedSystem = _host.SystemComboBox.SelectedItem.ToString() ?? "";
            var systemManagers = _host.GetSystemManagers();
            var selectedManager = systemManagers.FirstOrDefault(c =>
                c.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
            if (selectedManager == null)
            {
                const string contextMessage = "selectedConfig is null.";
                _logger.Warning(contextMessage);

                await _messageBox.InvalidSystemConfigMessageBoxAsync();

                await _host.DisplaySystemSelectionScreenAsync(cancellationToken);

                return;
            }

            var allFiles =
                await BuildListOfAllFilesToLoad(selectedManager, startLetter, searchQuery, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (selectedManager.GroupByFolder)
            {
                static string EnsureTrailingSlash(string? path)
                {
                    if (string.IsNullOrEmpty(path))
                        return path ?? "";

                    return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                           Path.DirectorySeparatorChar;
                }

                var rootFolders = selectedManager.SystemFolders
                    .Select(PathHelper.ResolveRelativeToAppDirectory)
                    .Where(static p => !string.IsNullOrEmpty(p))
                    .Select(EnsureTrailingSlash)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var groupedFiles = allFiles
                    .GroupBy(f =>
                    {
                        var fileDir = Path.GetDirectoryName(f) ?? "";
                        var normalizedFileDir = EnsureTrailingSlash(fileDir);

                        if (rootFolders.Contains(normalizedFileDir)) return f;

                        return fileDir;
                    }, StringComparer.Ordinal)
                    .Select(static g => g.Key)
                    .ToList();
                allFiles = groupedFiles;
            }

            allFiles = _gameFilterService.SortByMameDescription(allFiles, _host.GetMameSortOrder(),
                _mameDataService.Lookup);
            cancellationToken.ThrowIfCancellationRequested();

            allFiles = await _gameFilterService.FilterByShowGamesSettingAsync(allFiles, selectedSystem,
                selectedManager);
            cancellationToken.ThrowIfCancellationRequested();

            allFiles = _host.SetPaginationOfListOfFiles(allFiles);
            cancellationToken.ThrowIfCancellationRequested();

            await _gameItemRenderService.RenderGameItemsAsync(allFiles, selectedSystem, selectedManager,
                cancellationToken);

            switch (_settings.ViewMode)
            {
                case "GridView":
                    _host.Scroller.Focus();
                    break;
                case "ListView":
                    _host.GameDataGrid.Focus();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("[LoadGameFilesAsync] Operation was canceled");
            _gameItemRenderService.ClearRenderedItems();
        }
        catch (Exception ex)
        {
            const string contextMessage = "Error in the method LoadGameFilesAsync.";
            _logger.Error(ex, contextMessage);

            await _messageBox.ErrorMethodLoadGameFilesAsyncMessageBoxAsync();
        }
        finally
        {
            // BeginInvoke (not Invoke): this finally runs on pool threads and during
            // close; blocking on a busy/disposed UI thread hangs or throws (WPF-17).
            try
            {
                _ = _host.Dispatcher.BeginInvoke(() => _host.SetLoadingState(false));
            }
            catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
            {
                _logger.Debug($"[LoadGameFilesAsync] SetLoadingState dropped during shutdown: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Invalidates all game file caches, forcing a fresh disk scan on the next load.
    /// </summary>
    public Task InvalidateGameFileCachesAsync(CancellationToken cancellationToken = default)
    {
        return _gameCacheService.InvalidateAsync(cancellationToken);
    }

    private readonly Lock _watcherReloadLock = new();
    private CancellationTokenSource? _watcherReloadCts;

    /// <summary>
    ///     Handles file system change notifications for a system by invalidating caches and reloading game files.
    ///     Invoked as an event handler from the watcher debounce's pool thread (hence async void):
    ///     the reload itself is marshaled to the UI thread like every other load path, coalesced so
    ///     overlapping reloads never fight pagination/search, and cancellable (WPF-03).
    /// </summary>
    public async void OnGameFilesChangedAsync(string systemName)
    {
        try
        {
            CancellationToken token;
            lock (_watcherReloadLock)
            {
                // A newer change supersedes any reload still running from an earlier change.
                var oldCts = _watcherReloadCts;
                oldCts?.Cancel();
                if (oldCts is not null) DisposeCtsAfterGracePeriod(oldCts);

                _watcherReloadCts = new CancellationTokenSource();
                token = _watcherReloadCts.Token;
            }

            token.ThrowIfCancellationRequested();

            // The load pipeline touches UI controls throughout and is not thread-safe,
            // so run it on the UI thread exactly like button-initiated loads.
            var inner = await _host.Dispatcher.InvokeAsync(async () =>
            {
                token.ThrowIfCancellationRequested();

                var currentSystem = _host.SystemComboBox.SelectedItem?.ToString();
                if (!string.Equals(currentSystem, systemName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Debug(
                        $"[OnGameFilesChangedAsync] Ignoring change for system '{systemName}' (current: '{currentSystem}').");
                    return;
                }

                _logger.Debug(
                    $"[OnGameFilesChangedAsync] File change detected for system '{systemName}'. Reloading game list.");

                await InvalidateGameFileCachesAsync(token);
                await LoadGameFilesAsync(cancellationToken: token);
            });
            await inner;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer change or shut down; expected.
            _logger.Debug($"[OnGameFilesChangedAsync] Reload for system '{systemName}' was superseded.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            // Dispatcher shut down mid-reload; nothing left to update.
            _logger.Debug($"[OnGameFilesChangedAsync] Reload for system '{systemName}' aborted: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[OnGameFilesChangedAsync] Error reloading game list for system '{systemName}'.");
        }
    }

    /// <summary>
    ///     Disposes a superseded CTS after a grace period instead of inline: the cancelled
    ///     reload may still hold its token across awaits, where immediate disposal would
    ///     throw ObjectDisposedException (same pattern as MainWindow.CancelAndRecreateToken).
    /// </summary>
    private static void DisposeCtsAfterGracePeriod(CancellationTokenSource cts)
    {
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
        }, cts, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    ///     build list of all files to load.
    /// </summary>
    /// <param name="selectedManager">The selected manager.</param>
    /// <param name="startLetter">The start letter.</param>
    /// <param name="searchQuery">The search query.</param>
    /// <param name="token">The token.</param>
    private async Task<IList<string>> BuildListOfAllFilesToLoad(SystemManagerService selectedManager,
        string? startLetter, string? searchQuery, CancellationToken token)
    {
        if (_host.IsResortOperation)
        {
            var hasActiveFilter = !string.IsNullOrEmpty(_host.GetActiveSearchQueryOrMode()) ||
                                  !string.IsNullOrEmpty(_host.GetCurrentFilter());
            var (sourceList, _) = await _gameCacheService.GetResortSourceAsync(hasActiveFilter, token);

            if (sourceList.Count > 0)
            {
                _logger.Debug($"[BuildListOfAllFilesToLoad] Re-sorting existing list. Count: {sourceList.Count}");
                return sourceList;
            }
        }

        IList<string> allFiles;

        switch (searchQuery)
        {
            case AppConstants.Favorites:
                var favoriteGames = GetFavoriteGamesForSelectedSystem(selectedManager);
                allFiles = favoriteGames.ToList();
                await _gameCacheService.SetSearchResultsAsync(allFiles, token);
                break;

            case AppConstants.RetroAchievements:
                await _gameCacheService.PopulateFromDiskAsync(selectedManager, _getListOfFiles, token);

                try
                {
                    if (_retroAchievementsService?.RaManager?.AllGames == null)
                    {
                        allFiles = [];
                        await _gameCacheService.SetSearchResultsAsync(allFiles, token);
                        await _host.Dispatcher.BeginInvoke(() => _messageBox.ErrorMessageBoxAsync());
                        break;
                    }

                    // Hash-based matching: only games whose file hash exists in the local
                    // RetroAchievements hash scan AND resolves to a known RA game are kept.
                    // Loading the hash store and matching hashes do synchronous file I/O and
                    // CPU work, so they run on a worker thread to keep the UI responsive
                    // for large libraries (mirrors the Avalonia app).
                    var systemHashes = await Task.Run(
                        () => _raHashStore.LoadSystemHashes(selectedManager.SystemName), token);
                    var cachedGames = await _gameCacheService.GetAllGamesAsync(token);

                    if (systemHashes == null || systemHashes.Hashes.Count == 0 || cachedGames.Count == 0)
                    {
                        allFiles = [];
                        await _gameCacheService.SetSearchResultsAsync(allFiles, token);
                        break;
                    }

                    allFiles = await Task.Run(() => cachedGames.Where(filePath =>
                    {
                        if (!systemHashes.Hashes.TryGetValue(filePath, out var hash) ||
                            string.IsNullOrEmpty(hash))
                        {
                            return false;
                        }

                        return _retroAchievementsService.RaManager.GetGameInfoByHash(hash) != null;
                    }).ToList(), token);

                    await _gameCacheService.SetSearchResultsAsync(allFiles, token);
                }
                catch (Exception ex)
                {
                    allFiles = [];
                    _logger.Debug($"[BuildListOfAllFilesToLoad] Error matching RA hashes against local files: {ex}");
                    _logger.Error(ex,
                        $"[BuildListOfAllFilesToLoad] Error matching RA hashes against local files: {ex}");
                }

                break;

            case AppConstants.RandomSelection:
                await _gameCacheService.PopulateFromDiskAsync(selectedManager, _getListOfFiles, token);

                var allGamesForRandom = await _gameCacheService.GetAllGamesAsync(token);
                if (allGamesForRandom.Count == 0)
                {
                    allFiles = [];
                }
                else
                {
                    var randomIndex = Random.Shared.Next(0, allGamesForRandom.Count);
                    var selectedGame = allGamesForRandom[randomIndex];
                    allFiles = [selectedGame];
                    await _gameCacheService.SetSearchResultsAsync(allFiles, token);
                }

                break;

            default:
            {
                if (string.IsNullOrWhiteSpace(startLetter) && string.IsNullOrWhiteSpace(searchQuery))
                {
                    var isPopulated =
                        await _gameCacheService.IsCachePopulatedForSystemAsync(selectedManager.SystemName, token);
                    if (isPopulated)
                    {
                        allFiles = await _gameCacheService.GetAllGamesAsync(token);
                        _logger.Debug(
                            $"[BuildListOfAllFilesToLoad] Reusing cached list for '{selectedManager.SystemName}'. Count: {allFiles.Count}");
                    }
                    else
                    {
                        var uniqueFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var folder in selectedManager.SystemFolders)
                        {
                            token.ThrowIfCancellationRequested();
                            var resolvedSystemFolderPath = PathHelper.ResolveRelativeToAppDirectory(folder);
                            if (string.IsNullOrEmpty(resolvedSystemFolderPath) ||
                                !Directory.Exists(resolvedSystemFolderPath))
                            {
                                continue;
                            }

                            var filesInFolder = await _getListOfFiles.GetFilesAsync(resolvedSystemFolderPath,
                                selectedManager.FileFormatsToSearch, selectedManager.DisableRecursiveSearch,
                                selectedManager.GroupByFolder, token);
                            // Key by full path, not file name: two folders may contain
                            // different dumps with the same name (WPF-07).
                            foreach (var file in filesInFolder)
                                uniqueFiles.TryAdd(Path.GetFullPath(file), file);
                        }

                        allFiles = uniqueFiles.Values.ToList();
                        await _gameCacheService.SetAllGamesAsync(allFiles, selectedManager.SystemName, token);
                        _logger.Debug(
                            $"[BuildListOfAllFilesToLoad] Populated cache for '{selectedManager.SystemName}'. Count: {allFiles.Count}");
                    }
                }
                else
                {
                    var isPopulated =
                        await _gameCacheService.IsCachePopulatedForSystemAsync(selectedManager.SystemName, token);
                    if (!isPopulated)
                    {
                        var uniqueFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var folder in selectedManager.SystemFolders)
                        {
                            token.ThrowIfCancellationRequested();
                            var resolvedSystemFolderPath = PathHelper.ResolveRelativeToAppDirectory(folder);
                            if (string.IsNullOrEmpty(resolvedSystemFolderPath) ||
                                !Directory.Exists(resolvedSystemFolderPath))
                            {
                                continue;
                            }

                            var filesInFolder = await _getListOfFiles.GetFilesAsync(resolvedSystemFolderPath,
                                selectedManager.FileFormatsToSearch, selectedManager.DisableRecursiveSearch,
                                selectedManager.GroupByFolder, token);
                            // Key by full path, not file name (see above, WPF-07).
                            foreach (var file in filesInFolder)
                                uniqueFiles.TryAdd(Path.GetFullPath(file), file);
                        }

                        allFiles = uniqueFiles.Values.ToList();
                        await _gameCacheService.SetAllGamesAsync(allFiles, selectedManager.SystemName, token);
                    }
                    else
                    {
                        allFiles = await _gameCacheService.GetAllGamesAsync(token);
                    }
                }

                if (!string.IsNullOrWhiteSpace(startLetter))
                {
                    allFiles = await _gameFilterService.FilterByLetterAsync(allFiles, startLetter);
                    await _gameCacheService.SetSearchResultsAsync(allFiles, token);
                }

                if (!string.IsNullOrWhiteSpace(searchQuery) &&
                    !string.Equals(searchQuery, AppConstants.RandomSelection, StringComparison.Ordinal) &&
                    !string.Equals(searchQuery, AppConstants.Favorites, StringComparison.Ordinal))
                {
                    allFiles = await _gameFilterService.FilterBySearchQueryAsync(allFiles, searchQuery,
                        _mameDataService.Lookup);
                    await _gameCacheService.SetSearchResultsAsync(allFiles, token);
                }

                break;
            }
        }

        return allFiles;
    }

    /// <summary>
    ///     get favorite games for selected system.
    /// </summary>
    /// <param name="selectedManager">The selected manager.</param>
    private List<string> GetFavoriteGamesForSelectedSystem(SystemManagerService selectedManager)
    {
        var favorites = _favoritesManager.FavoriteList;

        var selectedSystem = _host.SystemComboBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selectedSystem) || selectedManager == null) return [];

        var favoriteGamePaths = favorites
            .Where(fav => fav.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase))
            .Select(fav => PathHelper.FindFileInSystemFolders(selectedManager.SystemFolders, fav.FileName))
            .Where(static path => !string.IsNullOrEmpty(path))
            .Select(static path => path!)
            .ToList();

        return favoriteGamePaths;
    }
}