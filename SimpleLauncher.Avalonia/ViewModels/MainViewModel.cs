using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.GameFilter;
using SimpleLauncher.Avalonia.Services.GameLauncher;
using SimpleLauncher.Avalonia.Services.LoadingOverlay;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SearchOrchestrator;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UsageStats;
using SearchValidationResult = SimpleLauncher.Avalonia.Services.SearchOrchestrator.SearchValidationResult;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     Main ViewModel for the game browser.
///     Phase 6: Wired to real SystemManagerService, ILauncherService, and game scanning.
/// </summary>
public partial class MainViewModel : ObservableObject, ILoadingState, ILaunchFeedback
{
    private const int MinThumbnailSize = 50;
    private const int MaxThumbnailSize = 800;
    private const int ZoomStep = 50;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly AvaloniaGameFilterService _gameFilter;
    private readonly LauncherService _launcher;
    private readonly AvaloniaGameFileLoadingOrchestrator _loadingOrchestrator;
    private readonly AvaloniaLoadingOverlayService _loadingOverlay;
    private readonly LocalizationService _localization;
    private readonly IMameDataService _mameData;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IPaginationService _pagination;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly IRetroAchievementsHashScanner _raHashScanner;
    private readonly IRetroAchievementsHashStore _raHashStore;
    private readonly RetroAchievementsManager _raManager;
    private readonly AvaloniaSearchOrchestratorService? _searchOrchestrator;
    private readonly SettingsManagerService _settings;
    private readonly Stats _stats;
    private readonly ISystemImageResolverService? _systemImageResolver;
    private readonly SystemManagerService _systemManager;
    private List<SystemManagerConfig> _allSystems;

    /// <summary>
    ///     Font size for the game title caption on cards (from the Filename Font Size setting).
    /// </summary>
    [ObservableProperty]
    public partial double CaptionFontSize { get; set; }

    [ObservableProperty] public partial double CardWidth { get; set; } = 168;

    /// <summary>
    ///     The full (un-paginated) game list of the current view. Pagination slices this
    ///     into <see cref="Games" /> when the total exceeds the configured page size.
    /// </summary>
    private List<GameCardViewModel> _currentAllGames = [];

    /// <summary>
    ///     The unfiltered full game list of the current view (before the letter filter
    ///     is applied). Clearing the letter filter restores the games from this list.
    /// </summary>
    private List<GameCardViewModel> _currentBaseGames = [];

    private HashSet<string> _favoritePaths;

    [ObservableProperty] public partial string GameCountText { get; set; } = "0 games";

    [ObservableProperty] public partial ObservableCollection<GameCardViewModel> Games { get; set; } = new();

    [ObservableProperty] public partial bool IsGridView { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool IsMixedView { get; set; } = true;

    /// <summary>
    ///     Whether the play-time display is visible for the selected system (hidden for
    ///     url/lnk systems, matching WPF IsPlayTimeVisible).
    /// </summary>
    [ObservableProperty]
    public partial bool IsPlayTimeVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsShowingFavorites { get; set; }

    /// <summary>
    ///     True when the game list is filtered to RetroAchievements-compatible games
    ///     (hash-based match, same as the WPF "Show Games With RetroAchievements").
    /// </summary>
    [ObservableProperty]
    public partial bool IsShowingRetroAchievements { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "Loading…";

    /// <summary>
    ///     MAME sort order toggle state: "FileName" (default) or "MachineDescription".
    ///     Matches the WPF sort-order toggle button.
    /// </summary>
    private string _mameSortOrder = "FileName";

    /// <summary>
    ///     Play-time string shown for the selected system (WPF PlayTime parity, driven by
    ///     the system selection orchestrator from the user's play-time settings).
    /// </summary>
    [ObservableProperty]
    public partial string PlayTime { get; set; } = "00:00:00";

    private CancellationTokenSource? _searchCts;

    [ObservableProperty] public partial string SearchText { get; set; } = "";

    [ObservableProperty] public partial string SelectedSystem { get; set; } = "";

    [ObservableProperty] public partial string StatusText { get; set; } = "Ready";

    /// <summary>
    ///     Guards the programmatic <see cref="SearchText" /> reset inside
    ///     <see cref="PickRandomGameAsync" /> so it does not start a debounced reload
    ///     that would wipe the random-pick result.
    /// </summary>
    private bool _suppressSearchReload;

    [ObservableProperty] public partial string ToolbarTitle { get; set; } = "SimpleLauncher";

    public MainViewModel(
        FavoritesManager favoritesManager,
        PlayHistoryManager playHistoryManager,
        SystemManagerService systemManager,
        LauncherService launcher,
        IFindCoverImageService findCoverImage,
        Stats stats,
        SettingsManagerService settings,
        IPaginationService pagination,
        AvaloniaGameFileLoadingOrchestrator loadingOrchestrator,
        IRetroAchievementsHashScanner raHashScanner,
        IRetroAchievementsHashStore raHashStore,
        RetroAchievementsManager raManager,
        IMessageBoxLibraryService messageBox,
        IMameDataService mameData,
        AvaloniaGameFilterService gameFilter,
        AvaloniaLoadingOverlayService loadingOverlay,
        LocalizationService localization,
        ISystemImageResolverService? systemImageResolver = null,
        AvaloniaSearchOrchestratorService? searchOrchestrator = null)
    {
        _favoritesManager = favoritesManager;
        _playHistoryManager = playHistoryManager;
        _systemManager = systemManager;
        _launcher = launcher;
        _findCoverImage = findCoverImage;
        _stats = stats;
        _settings = settings;
        _pagination = pagination;
        _loadingOrchestrator = loadingOrchestrator;
        _raHashScanner = raHashScanner;
        _raHashStore = raHashStore;
        _raManager = raManager;
        _messageBox = messageBox;
        _mameData = mameData;
        _gameFilter = gameFilter;
        _loadingOverlay = loadingOverlay;
        _searchOrchestrator = searchOrchestrator;
        _systemImageResolver = systemImageResolver;
        _localization = localization;
        Sidebar = new SidebarViewModel();

        _favoritePaths = _favoritesManager.GetFavoritePaths();
        _allSystems = _systemManager.LoadSystems();

        // Apply the saved preferences (settings.xml): default view mode and card size
        IsGridView = !string.Equals(settings.ViewMode, "ListView", StringComparison.OrdinalIgnoreCase);
        if (settings.ThumbnailSize is >= 50 and <= 800) CardWidth = settings.ThumbnailSize;

        CaptionFontSize = settings.FilenameFontSize switch
        {
            "Small" => 11,
            "Big" => 16,
            _ => 13
        };

        // NOTE: game loading is deferred to InitializeAsync() (called after the window
        // loads) so this constructor never blocks the UI thread scanning large ROM collections.
    }

    /// <summary>
    ///     Gets the current MAME sort order ("FileName" or "MachineDescription").
    /// </summary>
    public string MameSortOrder => _mameSortOrder;

    /// <summary>
    ///     The emulator selected in the top System Selection bar. When set, launches use
    ///     it instead of the system's first configured emulator (WPF EmulatorComboBox parity).
    /// </summary>
    public string? SelectedEmulatorName { get; set; }

    public bool IsEmpty => Games.Count == 0;

    /// <summary>
    ///     Sidebar state (system groups, icons, live counts). Populated by the window after load.
    /// </summary>
    public SidebarViewModel Sidebar { get; }

    /// <summary>
    ///     Gets the number of games per system name. Updated after each navigation/scan.
    /// </summary>
    public Dictionary<string, int> SystemGameCounts { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the current active letter filter (empty string = All).
    ///     Used by the UI reset host to read and restore the filter state.
    /// </summary>
    public string LetterFilter { get; private set; } = "";

    /// <summary>
    ///     ILaunchFeedback implementation for the launcher: raises the toast event.
    /// </summary>
    public void ShowToast(string title, string message)
    {
        ToastRequested?.Invoke(title, message);
    }

    /// <summary>
    ///     ILaunchFeedback implementation for the launcher: sets the status bar text.
    /// </summary>
    public void SetStatusText(string text)
    {
        StatusText = text;
    }

    /// <summary>
    ///     ILoadingState implementation for the launcher: delegates to the
    ///     <see cref="AvaloniaLoadingOverlayService" /> for thread-safe reference-counted
    ///     loading state management.
    /// </summary>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        _loadingOverlay.SetLoadingState(isLoading, message);
    }

    /// <summary>
    ///     Rebuilds the sidebar from the given system configurations, passing the
    ///     image resolver (if registered) so sidebar icons use annotation-stripped
    ///     and fuzzy matching (WPF SystemImageResolverService parity).
    /// </summary>
    public void PopulateSidebar(IEnumerable<SystemManagerConfig> systems)
    {
        Sidebar.Populate(systems, _systemImageResolver);
    }

    /// <summary>
    ///     Reloads the game library and reapplies the Show Games filter, filename
    ///     display mode, and card sizing (called after menu-driven setting changes).
    /// </summary>
    public void ReloadGames()
    {
        LoadAllGames();
    }

    /// <summary>
    ///     Reloads the system snapshot and game counts after the system configuration
    ///     changes (system added/edited/deleted in Easy Mode or Edit System).
    ///     Without this, navigation keeps filtering the stale snapshot: a newly added
    ///     system shows 0 games, an edited system scans the old folders, and a
    ///     deleted system keeps showing its games until restart.
    ///     Disk enumeration runs on the thread pool (startup InitializeAsync parity);
    ///     state is applied back on the UI context.
    /// </summary>
    public async Task ReloadSystemsAfterConfigurationChangeAsync()
    {
        try
        {
            var (systems, counts) = await Task.Run(() =>
            {
                var loadedSystems = _systemManager.LoadSystems();
                var loadedCounts = ComputeSystemCounts(loadedSystems);
                return (loadedSystems, loadedCounts);
            });

            _allSystems = systems;
            SystemGameCounts = counts;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload systems after configuration change");
        }
    }

    /// <summary>
    ///     Applies the saved Games Per Page setting to the pagination service (called once
    ///     after the main window loads, and again whenever the setting changes via the menu).
    /// </summary>
    public void ConfigurePagination(int filesPerPage)
    {
        _pagination.FilesPerPage = filesPerPage;
        _pagination.PaginationThreshold = filesPerPage;
    }

    /// <summary>
    ///     Navigates to the previous page of the current view (called by the status-bar button).
    /// </summary>
    public void GoToPreviousPage()
    {
        _pagination.GoToPreviousPage();
        ReapplyPagination();
    }

    /// <summary>
    ///     Navigates to the next page of the current view (called by the status-bar button).
    /// </summary>
    public void GoToNextPage()
    {
        _pagination.GoToNextPage();
        ReapplyPagination();
    }

    /// <summary>
    ///     Re-applies pagination to <see cref="_currentAllGames" /> and updates the displayed
    ///     <see cref="Games" /> collection plus the game count label.
    /// </summary>
    private void ReapplyPagination()
    {
        var pageFiles = _pagination.ApplyPagination(_currentAllGames.ConvertAll(g => g.FilePath));
        var pageSet = pageFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Games = new ObservableCollection<GameCardViewModel>(
            _currentAllGames.Where(g => pageSet.Contains(g.FilePath)));
        UpdateGameCount();
    }

    /// <summary>
    ///     Stores the full list for the current view and displays it through pagination
    ///     (every navigation/search/refresh path routes through this method).
    /// </summary>
    private void ShowGames(List<GameCardViewModel> fullList)
    {
        _currentBaseGames = fullList;
        ReapplyLetterFilterAndPagination();
    }

    /// <summary>
    ///     Re-applies the current letter filter to the base game list, then re-applies
    ///     pagination and updates the displayed collection.
    /// </summary>
    private void ReapplyLetterFilterAndPagination()
    {
        _currentAllGames = ApplyLetterFilter(_currentBaseGames);
        ReapplyPagination();
    }

    /// <summary>
    ///     Filters the given game list by the active <see cref="LetterFilter" /> ("" = all).
    ///     Delegates to <see cref="AvaloniaGameFilterService.FilterByLetter" />.
    /// </summary>
    private List<GameCardViewModel> ApplyLetterFilter(List<GameCardViewModel> games)
    {
        return AvaloniaGameFilterService.FilterByLetter(games, LetterFilter);
    }

    /// <summary>
    ///     Sets the active letter filter ("" = All) and re-applies it to the current view.
    /// </summary>
    public void SetLetterFilter(string letter)
    {
        LetterFilter = letter ?? "";
        ReapplyLetterFilterAndPagination();
        StatusText = string.IsNullOrEmpty(LetterFilter)
            ? _localization.GetString("Status.AllGames", "All Games")
            : string.Format(_localization.GetString("Filteringby", "Filtering by {0}"), LetterFilter);
    }

    /// <summary>
    ///     Clears the letter filter (equivalent to pressing the "All" letter button).
    /// </summary>
    public void ClearLetterFilter()
    {
        SetLetterFilter("");
    }

    /// <summary>
    ///     Feeling Lucky (WPF ShowSystemFeelingLuckyClickAsync parity): resets every view
    ///     filter, forces the Show Games setting back to "ShowAll" (so games without cover
    ///     art can be picked too), then picks ONE random game from the FULL library of the
    ///     currently selected system and replaces the displayed list with it.
    ///     Returns the picked game, or null when the system has no games.
    /// </summary>
    public async Task<GameCardViewModel?> PickRandomGameAsync()
    {
        var systems = string.IsNullOrEmpty(SelectedSystem)
            ? []
            : _allSystems.Where(s => string.Equals(s.SystemName, SelectedSystem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        if (systems.Count == 0) return null;

        // Force the Show Games filter to ShowAll and persist it (WPF parity)
        if (!string.Equals(_settings.ShowGames, "ShowAll", StringComparison.OrdinalIgnoreCase))
        {
            _settings.ShowGames = "ShowAll";
            await _settings.SaveAsync();
        }

        // Reset every active filter (letter / search / favorites / RetroAchievements)
        CancelPendingSearch();
        IsShowingFavorites = false;
        IsShowingRetroAchievements = false;
        LetterFilter = "";
        _suppressSearchReload = true;
        try
        {
            SearchText = "";
        }
        finally
        {
            _suppressSearchReload = false;
        }

        SetLoadingState(true, _localization.GetString("LoadingGames", "Loading Games..."));
        try
        {
            // Full library scan for the selected system — ignores letter/search/
            // cover-image filters exactly like the WPF RANDOM_SELECTION mode
            var pool = await Task.Run(() => ScanGames(systems));

            GameCardViewModel? picked = null;
            if (pool.Count > 0)
            {
                picked = pool[Random.Shared.Next(pool.Count)];
                picked.IsFavorite = _favoritesManager.GetFavoritePaths().Contains(Path.GetFileName(picked.FilePath));

                var historyLookup = _playHistoryManager.GetHistoryLookup();
                if (historyLookup.TryGetValue(picked.FilePath, out var history))
                {
                    picked.PlayCount = history.TimesPlayed;
                    picked.LastPlayed = history.LastPlayDate;
                }
            }

            // Replace the whole visible list with the single picked game (an empty
            // pool renders the inline no-games state, like the WPF app)
            _currentBaseGames = picked is null ? [] : [picked];
            ReapplyLetterFilterAndPagination();

            if (picked != null)
            {
                StatusText = string.Format(
                    _localization.GetString("Pickedarandomgame", "Picked a random game: {0}"), picked.DisplayTitle);
                ToolbarTitle =
                    $"{_localization.GetString("App.Title", "SimpleLauncher")} — {SelectedSystem} (1 {_localization.GetString("Game", "game")})";
            }
            else
            {
                StatusText = _localization.GetString("Nogamesfoundfortherandomselection",
                    "No games found for the random selection.");
            }

            return picked;
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    /// <summary>
    ///     Cancels a pending debounced search (if any) so a queued reload cannot
    ///     overwrite the random-pick result.
    /// </summary>
    private void CancelPendingSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    /// <summary>
    ///     Toggles the MAME sort order between file name and machine description
    ///     (WPF sort-order toggle), then re-sorts the current view.
    /// </summary>
    public void ToggleMameSortOrder()
    {
        var newOrder = string.Equals(_mameSortOrder, "FileName", StringComparison.Ordinal)
            ? "MachineDescription"
            : "FileName";
        SetMameSortOrder(newOrder);
    }

    /// <summary>
    ///     Sets the MAME sort order explicitly (used by the UI reset host) and re-sorts the current view.
    /// </summary>
    public void SetMameSortOrder(string sortOrder)
    {
        _mameSortOrder = sortOrder;
        _currentBaseGames = SortByMameOrder(_currentBaseGames);
        ReapplyLetterFilterAndPagination();
        StatusText = string.Equals(_mameSortOrder, "MachineDescription", StringComparison.Ordinal)
            ? _localization.GetString("Sortedbymachinedescription", "Sorted by machine description")
            : _localization.GetString("Sortedbyfilename", "Sorted by file name");
    }

    /// <summary>
    ///     Sorts the given game list by the current <see cref="_mameSortOrder" />.
    ///     Delegates to <see cref="AvaloniaGameFilterService.SortByMameOrder" />.
    /// </summary>
    private List<GameCardViewModel> SortByMameOrder(List<GameCardViewModel> games)
    {
        return _gameFilter.SortByMameOrder(games, _mameSortOrder);
    }

    /// <summary>
    ///     Increases the thumbnail/card width by one zoom step (saves the preference).
    /// </summary>
    public void ZoomIn()
    {
        AdjustZoomStep(1);
    }

    /// <summary>
    ///     Decreases the thumbnail/card width by one zoom step (saves the preference).
    /// </summary>
    public void ZoomOut()
    {
        AdjustZoomStep(-1);
    }

    private void AdjustZoomStep(int direction)
    {
        var newSize = Math.Clamp((int)CardWidth + (direction * ZoomStep), MinThumbnailSize, MaxThumbnailSize);
        if (newSize == (int)CardWidth) return;

        CardWidth = newSize;
        _settings.ThumbnailSize = newSize;
        _ = _settings.SaveAsync();
        StatusText = direction > 0
            ? $"{_localization.GetString("ZoomingIn", "Zooming in...")} {newSize}{_localization.GetString("Px", "px")}"
            : $"{_localization.GetString("ZoomingOut", "Zooming out...")} {newSize}{_localization.GetString("Px", "px")}";
    }

    /// <summary>
    ///     Reloads the current view (search / favorites / selected system / all games)
    ///     after the game file watcher detects changes on disk. Keeps the user where
    ///     they were instead of resetting to the All Games view.
    /// </summary>
    public void RefreshCurrentView()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            _ = ExecuteSearchAsync(SearchText);
            return;
        }

        if (IsShowingRetroAchievements)
        {
            _ = RefreshRetroAchievementsViewAsync();
            return;
        }

        if (IsShowingFavorites)
        {
            NavigateToFavoritesCommand.Execute(null);
            return;
        }

        if (!string.IsNullOrEmpty(SelectedSystem))
        {
            NavigateToSystemCommand.Execute(SelectedSystem);
            return;
        }

        LoadAllGames();
    }

    /// <summary>
    ///     Requests a toast notification (surfaced by the main window). Raised on the
    ///     UI thread so subscribers can touch UI directly.
    /// </summary>
#pragma warning disable MA0046
    public event Action<string, string>? ToastRequested;
#pragma warning restore MA0046

    // ---- RetroAchievements Filter ----

    /// <summary>
    ///     Filters the game list to show only games that have RetroAchievements support
    ///     (same hash-based flow as the WPF app): when no hash scan exists for the selected
    ///     system, the user is prompted to run one in the background first. With no system
    ///     selected, every system is filtered using its stored hash scan.
    /// </summary>
    [RelayCommand]
    private async Task ShowGamesWithRetroAchievements()
    {
        try
        {
            // Prevent parallel hash calculations (they would spawn many CLI processes at once)
            if (_raHashScanner.IsScanning)
            {
                ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                    _localization.GetString("RaHashCalculationInProgress",
                        "A RetroAchievements hash calculation is already in progress. Please wait for it to finish before trying again."));
                return;
            }

            // No system selected: best-effort filter across all systems using the
            // stored hash scans (systems without a scan simply contribute nothing).
            if (string.IsNullOrEmpty(SelectedSystem))
            {
                await ShowRetroAchievementsGamesAsync(_allSystems);
                return;
            }

            var system = _systemManager.GetSystem(SelectedSystem);
            if (system == null) return;

            // If no valid hash scan result exists yet (missing or produced by older
            // hash logic), ask the user to scan the game path first.
            // IsScanUpToDate performs synchronous file I/O + JSON deserialization, so it
            // runs on a worker thread to keep the UI responsive for large libraries.
            var isScanUpToDate = await Task.Run(() => _raHashScanner.IsScanUpToDate(system.SystemName));
            if (!isScanUpToDate)
            {
                if (!_raHashScanner.IsSystemScannable(system.SystemName))
                {
                    ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                        $"{system.SystemName} {_localization.GetString("RaHashSystemNotSupported", "is not supported for RetroAchievements hashing.")}");
                    return;
                }

                var result = await _messageBox.ScanGamePathForRetroAchievementsMessageBoxAsync();
                if (result != MessageBoxResult.Yes)
                {
                    // User cancelled: do not filter the list of games
                    return;
                }

                // Non-blocking notification: the app stays fully responsive while
                // the hash calculation runs in the background
                _ = _raHashScanner.ScanSystemAsync(
                    system.SystemName,
                    system.SystemFolders,
                    system.FileFormatsToSearch,
                    system.FileFormatsToLaunch,
                    system.DisableRecursiveSearch,
                    system.GroupByFolder,
                    OnHashScanCompleted);

                ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                    _localization.GetString("RaHashScanInBackgroundMessage",
                        "The hash calculation will happen in the background. You can click the filter button again later to see if the hashing is complete."));
                return;
            }

            await ShowRetroAchievementsGamesAsync([system]);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error showing games with RetroAchievements");
            StatusText = _localization.GetString("Errorfilteringgames", "Error filtering games");
        }
    }

    /// <summary>
    ///     Re-applies the RetroAchievements filter to the current view without prompting
    ///     for a scan (called after the game file watcher detects changes on disk).
    /// </summary>
    private async Task RefreshRetroAchievementsViewAsync()
    {
        try
        {
            var systems = string.IsNullOrEmpty(SelectedSystem)
                ? _allSystems
                : _allSystems
                    .Where(s => string.Equals(s.SystemName, SelectedSystem, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            await ShowRetroAchievementsGamesAsync(systems);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing the RetroAchievements filter");
        }
    }

    /// <summary>
    ///     Filters the given systems' games to those whose file hash exists in the local
    ///     RetroAchievements hash scan AND resolves to a known RA game (the exact same
    ///     hash-based matching used by the WPF app).
    /// </summary>
    private async Task ShowRetroAchievementsGamesAsync(List<SystemManagerConfig> systems)
    {
        var (matched, total) = await Task.Run(() =>
        {
            var allGames = ScanGames(systems);
            ApplyFavoritesAndHistory(allGames);

            // Hash-based matching: only games whose file hash exists in the local
            // RetroAchievements hash scan AND resolves to a known RA game are kept.
            var systemHashesCache = new Dictionary<string, RaSystemHashes?>(StringComparer.OrdinalIgnoreCase);
            var matched = allGames.Where(game =>
            {
                if (!systemHashesCache.TryGetValue(game.SystemName, out var systemHashes))
                {
                    systemHashes = _raHashStore.LoadSystemHashes(game.SystemName);
                    systemHashesCache[game.SystemName] = systemHashes;
                }

                if (systemHashes == null || systemHashes.Hashes.Count == 0) return false;

                return systemHashes.Hashes.TryGetValue(game.FilePath, out var hash) &&
                       !string.IsNullOrEmpty(hash) &&
                       _raManager.GetGameInfoByHash(hash) != null;
            }).ToList();

            return (matched, allGames.Count);
        });

        IsShowingRetroAchievements = true;
        LetterFilter = "";
        ShowGames(matched);
        var raMatchTemplate =
            _localization.GetString("OfgameswithRetroAchievements", "{0} of {1} games with RetroAchievements");
        StatusText = string.Format(CultureInfo.InvariantCulture, raMatchTemplate, matched.Count, total);
        ToolbarTitle =
            $"{_localization.GetString("App.Title", "SimpleLauncher")} — {_localization.GetString("RetroAchievements", "RetroAchievements")}";
    }

    /// <summary>
    ///     Shows a completion toast after a system hash scan finishes. Runs on a
    ///     background thread, so the toast is marshaled to the UI thread.
    /// </summary>
    private void OnHashScanCompleted(string systemName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var raCompleteTemplate = _localization.GetString("RaHashCalculationComplete",
                "RetroAchievements hash calculation is complete for {0}.");
            ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                string.Format(raCompleteTemplate, systemName));
        });
    }

    /// <summary>
    ///     Rescans the game folders of the selected system and recalculates its
    ///     RetroAchievements hashes in the background (unchanged files are skipped
    ///     by the scanner).
    /// </summary>
    public Task RescanRetroAchievementsForSelectedSystemAsync()
    {
        try
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedSystem))
                {
                    ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                        _localization.GetString("RaHashNoSystemSelected",
                            "Please select a system before rescanning RetroAchievements hashes."));
                    return Task.CompletedTask;
                }

                if (_raHashScanner.IsScanning)
                {
                    ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                        _localization.GetString("RaHashCalculationInProgress",
                            "A RetroAchievements hash calculation is already in progress. Please wait for it to finish before trying again."));
                    return Task.CompletedTask;
                }

                var system = _systemManager.GetSystem(SelectedSystem);
                if (system == null) return Task.CompletedTask;

                if (!_raHashScanner.IsSystemScannable(system.SystemName))
                {
                    ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                        $"{system.SystemName} {_localization.GetString("RaHashSystemNotSupported", "is not supported for RetroAchievements hashing.")}");
                    return Task.CompletedTask;
                }

                StatusText = _localization.GetString("CalculatingRetroAchievementsHashes",
                    "Calculating RetroAchievements hashes...");

                _ = _raHashScanner.ScanSystemAsync(
                    system.SystemName,
                    system.SystemFolders,
                    system.FileFormatsToSearch,
                    system.FileFormatsToLaunch,
                    system.DisableRecursiveSearch,
                    system.GroupByFolder,
                    OnHashScanCompleted);

                // Non-blocking notification: the app stays fully responsive while
                // the hash calculation runs in the background
                ShowToast(_localization.GetString("RetroAchievements", "RetroAchievements"),
                    _localization.GetString("RaHashScanInBackgroundMessage",
                        "The hash calculation will happen in the background. You can click the filter button again later to see if the hashing is complete."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error rescanning RetroAchievements for the selected system");
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    /// <summary>
    ///     Loads all games asynchronously. Called once after the main window loads.
    ///     Heavy work (file enumeration + cover checks) runs on the thread pool;
    ///     UI-bound properties are updated on the captured UI context.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            // Reconcile favorites against the current system configuration (mirrors the WPF app):
            // entries whose system no longer exists (renamed without migration, or deleted)
            // are dropped so they never linger in the favorites filter.
            // Only reconcile when systems are actually loaded: an empty list must never be
            // interpreted as "every favorite belongs to a missing system" (which would wipe them).
            var validSystemNames = _allSystems.ConvertAll(static s => s.SystemName);
            if (validSystemNames.Count != 0)
            {
                var removedCount = await _favoritesManager.RemoveFavoritesForMissingSystemsAsync(validSystemNames);
                if (removedCount > 0) _favoritePaths = _favoritesManager.GetFavoritePaths();
            }

            // WPF parity: do not build the full cross-system game list at startup.
            // The window opens on the system-selection screen; per-system scanning
            // happens only when a system (or All Games) is selected by the user.
            var (systems, counts) = await Task.Run(() =>
            {
                var loadedSystems = _systemManager.LoadSystems();
                var loadedCounts = ComputeSystemCounts(loadedSystems);
                return (loadedSystems, loadedCounts);
            });

            _allSystems = systems;
            SystemGameCounts = counts;
            IsShowingFavorites = false;
            IsShowingRetroAchievements = false;
            IsMixedView = true;
            SelectedSystem = "";
            LetterFilter = "";
            _currentBaseGames = [];
            _currentAllGames = [];
            ShowGames([]);
            StatusText = _localization.GetString("Status.AllGames", "All Games");
            ToolbarTitle = _localization.GetString("App.Title", "SimpleLauncher");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize game library");
            StatusText = _localization.GetString("Errorloadinggames", "Error loading games");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_suppressSearchReload) return;

        _ = DebounceSearchAsync(value);
    }

    private async Task DebounceSearchAsync(string query)
    {
        try
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(180, token);
                if (token.IsCancellationRequested) return;

                if (string.IsNullOrWhiteSpace(query))
                {
                    LoadAllGames();
                    StatusText = _localization.GetString("Status.Ready", "Ready");
                }
                else
                {
                    await ExecuteSearchAsync(query);
                }
            }
            catch (TaskCanceledException)
            {
                Log.Debug("Search debounce cancelled by newer input");
            }
        }
        catch (Exception ex)
        {
            // async Task: exceptions never escape to the process — they are
            // observed by the awaiting fire-and-forget discard
            Log.Error(ex, "Search debounce error");
        }
    }

    private async Task ExecuteSearchAsync(string query)
    {
        // WPF SearchOrchestrator parity: require a selected system AND a non-blank
        // query, and clear prior search results so stale results never persist.
        var validation = _searchOrchestrator?.ValidateAndPrepare(query, SelectedSystem)
                         ?? (string.IsNullOrWhiteSpace(query)
                             ? SearchValidationResult.Failure()
                             : SearchValidationResult.Success(
                                 query.Trim()));
        if (!validation.IsValid)
        {
            _currentBaseGames = [];
            _currentAllGames = [];
            Games.Clear();
            UpdateGameCount();

            // Show the appropriate warning dialog (WPF parity)
            if (string.IsNullOrEmpty(SelectedSystem))
            {
                StatusText = _localization.GetString("Selectasystembeforesearching",
                    "Select a system before searching.");
                if (_messageBox != null)
                    await _messageBox.SelectSystemBeforeSearchMessageBoxAsync();
            }
            else
            {
                StatusText = _localization.GetString("Enterasearchquery", "Enter a search query.");
                if (_messageBox != null)
                    await _messageBox.EnterSearchQueryMessageBoxAsync();
            }

            return;
        }

        IsShowingRetroAchievements = false;
        LetterFilter = "";

        var allGames = ScanGames(_allSystems);
        var results = AvaloniaGameFilterService.FilterBySearchQuery(allGames, validation.ValidatedQuery);

        ApplyFavoritesAndHistory(results);
        ShowGames(results);
        var searchResultsTemplate = _localization.GetString("Status.SearchResults", "{0} result(s) for \"{1}\"");
        StatusText = string.Format(CultureInfo.InvariantCulture, searchResultsTemplate, results.Count,
            validation.ValidatedQuery);
    }

    [RelayCommand]
    private void NavigateToSystem(string systemName)
    {
        try
        {
            SelectedSystem = systemName;
            IsMixedView = string.IsNullOrEmpty(systemName);
            IsShowingRetroAchievements = false;
            LetterFilter = "";

            var systems = string.IsNullOrEmpty(systemName)
                ? _allSystems
                : _allSystems.Where(s => string.Equals(s.SystemName, systemName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            // Snapshot safety net: if the snapshot predates a configuration change,
            // fall back to a direct lookup so a configured system never shows 0 games.
            if (!string.IsNullOrEmpty(systemName) && systems.Count == 0)
            {
                var direct = _systemManager.GetSystem(systemName);
                if (direct is not null)
                    systems = [direct];
            }

            var games = ScanGames(systems);
            ApplyFavoritesAndHistory(games);
            ShowGames(games);
            var count = games.Count;
            StatusText = string.IsNullOrEmpty(systemName)
                ? _localization.GetString("Status.AllGames", "All Games")
                : systemName;
            ToolbarTitle = string.IsNullOrEmpty(systemName)
                ? _localization.GetString("App.Title", "SimpleLauncher")
                : $"{_localization.GetString("App.Title", "SimpleLauncher")} — {systemName} ({count} {(count == 1 ? _localization.GetString("Game", "game") : _localization.GetString("GamePlural", "games"))})";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to system {System}", systemName);
            StatusText = _localization.GetString("Errorloadinggames", "Error loading games");
        }
    }

    [RelayCommand]
    private void NavigateToAllGames()
    {
        try
        {
            LoadAllGames();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to All Games");
            StatusText = _localization.GetString("Errorloadinggames", "Error loading games");
        }
    }

    [RelayCommand]
    private void NavigateToFavorites()
    {
        try
        {
            IsShowingFavorites = true;
            IsShowingRetroAchievements = false;
            LetterFilter = "";
            _favoritePaths = _favoritesManager.GetFavoritePaths();

            // Force the Show Games filter to ShowAll and persist it (WPF parity)
            // Favorites might not have covers, so we need to show all games
            if (!string.Equals(_settings.ShowGames, "ShowAll", StringComparison.OrdinalIgnoreCase))
            {
                _settings.ShowGames = "ShowAll";
                _ = _settings.SaveAsync();
            }

            var allGames = ScanGames(_allSystems);
            ApplyFavoritesAndHistory(allGames);
            var favorites = allGames.Where(g => g.IsFavorite).ToList();

            ShowGames(favorites);
            StatusText = _localization.GetString("Status.Favorites", "Favorites");
            ToolbarTitle =
                $"{_localization.GetString("App.Title", "SimpleLauncher")} — {_localization.GetString("Toolbar.Favorites", "Favorites")}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to Favorites");
            StatusText = _localization.GetString("Errorloadingfavorites", "Error loading favorites");
        }
    }

    /// <summary>
    ///     WPF SelectedSystemFavoriteButton parity: shows only the favorites
    ///     for the currently selected system (all systems when none is selected).
    /// </summary>
    [RelayCommand]
    private void NavigateToSelectedSystemFavorites()
    {
        try
        {
            IsShowingFavorites = true;
            IsShowingRetroAchievements = false;
            LetterFilter = "";
            _favoritePaths = _favoritesManager.GetFavoritePaths();

            // Force the Show Games filter to ShowAll and persist it (WPF parity)
            // Favorites might not have covers, so we need to show all games
            if (!string.Equals(_settings.ShowGames, "ShowAll", StringComparison.OrdinalIgnoreCase))
            {
                _settings.ShowGames = "ShowAll";
                _ = _settings.SaveAsync();
            }

            var systems = string.IsNullOrEmpty(SelectedSystem)
                ? _allSystems
                : _allSystems
                    .Where(s => string.Equals(s.SystemName, SelectedSystem, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var allGames = ScanGames(systems);
            ApplyFavoritesAndHistory(allGames);
            var favorites = allGames.Where(g => g.IsFavorite).ToList();

            ShowGames(favorites);

            if (string.IsNullOrEmpty(SelectedSystem))
            {
                StatusText = _localization.GetString("Status.Favorites", "Favorites");
                ToolbarTitle =
                    $"{_localization.GetString("App.Title", "SimpleLauncher")} — {_localization.GetString("Toolbar.Favorites", "Favorites")}";
            }
            else
            {
                StatusText = $"{_localization.GetString("Status.Favorites", "Favorites")} — {SelectedSystem}";
                ToolbarTitle =
                    $"{_localization.GetString("App.Title", "SimpleLauncher")} — {SelectedSystem} {_localization.GetString("Toolbar.Favorites", "Favorites")}";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to Favorites of the selected system");
            StatusText = _localization.GetString("Errorloadingfavorites", "Error loading favorites");
        }
    }

    [RelayCommand]
    private void NavigateToRecentlyPlayed()
    {
        try
        {
            IsShowingRetroAchievements = false;
            LetterFilter = "";

            var historyLookup = _playHistoryManager.GetHistoryLookup();
            var allGames = ScanGames(_allSystems);
            ApplyFavoritesAndHistory(allGames);

            var recent = allGames
                .Where(g => historyLookup.ContainsKey(g.FilePath))
                .OrderByDescending(g => historyLookup[g.FilePath].LastPlayDate, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            ShowGames(recent);
            StatusText = _localization.GetString("Status.RecentlyPlayed", "Recently Played");
            ToolbarTitle =
                $"{_localization.GetString("App.Title", "SimpleLauncher")} — {_localization.GetString("Toolbar.RecentlyPlayed", "Recently Played")}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to Recently Played");
            StatusText = _localization.GetString("Errorloadingrecentlyplayed", "Error loading recently played");
        }
    }

    [RelayCommand]
    private void NavigateToRecentlyAdded()
    {
        try
        {
            IsShowingRetroAchievements = false;
            LetterFilter = "";

            var allGames = ScanGames(_allSystems);
            ApplyFavoritesAndHistory(allGames);

            // Sort by file creation/modification date (newest first)
            var recent = allGames
                .Where(g => File.Exists(g.FilePath))
                .OrderByDescending(g =>
                {
                    try
                    {
                        return new FileInfo(g.FilePath).LastWriteTime;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to read LastWriteTime for {Path}", g.FilePath);
                        return DateTime.MinValue;
                    }
                })
                .Take(50)
                .ToList();

            ShowGames(recent);
            StatusText = _localization.GetString("Status.RecentlyAdded", "Recently Added");
            ToolbarTitle =
                $"{_localization.GetString("App.Title", "SimpleLauncher")} — {_localization.GetString("Status.RecentlyAdded", "Recently Added")}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to Recently Added");
            StatusText = _localization.GetString("Errorloadingrecentlyadded", "Error loading recently added");
        }
    }

    [RelayCommand]
    private void ToggleView()
    {
        IsGridView = !IsGridView;
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(GameCardViewModel? game)
    {
        if (game is null) return;

        var isNowFavorite = await _favoritesManager.ToggleAsync(game.FilePath, game.SystemName);
        game.IsFavorite = isNowFavorite;

        var favoriteName = Path.GetFileName(game.FilePath) ?? game.FilePath;
        if (isNowFavorite)
            _favoritePaths.Add(favoriteName);
        else
            _favoritePaths.Remove(favoriteName);

        StatusText = isNowFavorite
            ? string.Format(_localization.GetString("Favorites.Added", "Added to favorites: {0}"),
                game.DisplayTitle)
            : string.Format(_localization.GetString("Favorites.Removed", "Removed from favorites: {0}"),
                game.DisplayTitle);
    }

    [RelayCommand]
    private async Task PlayGameAsync(GameCardViewModel? game)
    {
        if (game is null) return;

        var playTime = await LaunchGameAtPathAsync(game.FilePath, game.SystemName);
        if (playTime.TotalSeconds >= 5)
        {
            game.PlayCount++;
            game.LastPlayed = DateTime.Now.ToString("d", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///     Launches a game by file path and system name (shared by the game grid and the
    ///     Favorites / Play History / Global Search sections). Records play history and
    ///     usage stats; returns the measured play time so callers can update their rows.
    /// </summary>
    public async Task<TimeSpan> LaunchGameAtPathAsync(string filePath, string systemName)
    {
        var system = _systemManager.GetSystem(systemName);
        var emulator = system?.Emulators.FirstOrDefault(e =>
                           string.Equals(e.EmulatorName, SelectedEmulatorName, StringComparison.OrdinalIgnoreCase))
                       ?? system?.Emulators.FirstOrDefault();
        var windowContext = App.ServiceProvider.GetRequiredService<IWindowContext>();

        if (system is null || emulator is null)
        {
            StatusText = string.Format(
                _localization.GetString("Cannotlaunchnoemulatorconfiguredfor",
                    "Cannot launch: no emulator configured for {0}"), systemName);
            return TimeSpan.Zero;
        }

        IsLoading = true;
        StatusText = string.Format(CultureInfo.InvariantCulture,
            _localization.GetString("Launch.Launching", "Launching: {0}..."),
            Path.GetFileNameWithoutExtension(filePath));

        try
        {
            await _launcher.HandleButtonClickAsync(
                filePath,
                emulator.EmulatorName,
                system.SystemName,
                system,
                emulator,
                emulator.EmulatorParameters,
                windowContext,
                this);

            // Play history and usage stats are already recorded once by
            // LauncherService.HandleButtonClickAsync -> UpdateStatsAndPlayCountAsync.
            // Recording them again here would double-count launches.

            StatusText = string.Format(CultureInfo.InvariantCulture,
                _localization.GetString("Status.Played", "Played: {0}"),
                Path.GetFileNameWithoutExtension(filePath));
            return _launcher.LastPlayTime;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch game {Game}", filePath);
            StatusText = string.Format(
                _localization.GetString("Launcherror", "Launch error: {0}"), ex.Message);
            return TimeSpan.Zero;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     Re-applies favorites and play-history state to the currently displayed games
    ///     (called after the Favorites / Play History sections mutate their data).
    /// </summary>
    public void RefreshFavoritesAndHistory()
    {
        ApplyFavoritesAndHistory(_currentAllGames);
        ReapplyPagination();
    }

    /// <summary>
    ///     Removes a game from the currently displayed list without touching the file on
    ///     disk (the library is filesystem-derived, so the game reappears on the next
    ///     scan/navigation). Used by the Game Detail window's Remove button.
    /// </summary>
    public void RemoveGameFromCurrentList(GameCardViewModel game)
    {
        if (game is null) return;

        _currentAllGames.Remove(game);
        Games.Remove(game);
        StatusText = string.Format(
            _localization.GetString("Removedfromview", "Removed from view: {0}"), game.DisplayTitle);
        UpdateGameCount();
    }

    /// <summary>
    ///     Scans ROM folders for games and returns card ViewModels with cover art resolved
    ///     from each system's image folder. Folder paths are resolved (%BASEFOLDER% / relative) first.
    ///     File enumeration goes through the loading orchestrator, which caches the file
    ///     list per system so repeat navigation does not re-enumerate the disk.
    /// </summary>
    private List<GameCardViewModel> ScanGames(List<SystemManagerConfig> systems)
    {
        var games = new List<GameCardViewModel>();

        foreach (var system in systems)
        {
            foreach (var file in _loadingOrchestrator.GetGameFiles(system))
            {
                var coverPath = _findCoverImage.FindCoverImagePath(
                    Path.GetFileNameWithoutExtension(file),
                    system.SystemName,
                    system.SystemImageFolder);

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file) ?? file;
                var machineDescription = _mameData.Lookup.TryGetValue(fileNameWithoutExt, out var desc) ? desc : "";
                var folderPath = Path.GetDirectoryName(file) ?? "";

                games.Add(new GameCardViewModel
                {
                    DisplayTitle = GetDisplayTitle(file),
                    FileName = fileNameWithoutExt,
                    FilePath = file,
                    FolderPath = folderPath,
                    MachineDescription = machineDescription,
                    SystemName = system.SystemName,
                    CoverPath = coverPath,
                    // Show art only when the file actually exists (the service falls back
                    // to default.png, which may itself be missing → placeholder instead)
                    HasCover = File.Exists(coverPath),
                    IsRaSupported = GameCardViewModel.IsSystemRaSupported(system.SystemName)
                });
            }
        }

        return games;
    }

    /// <summary>
    ///     Applies the Filename Preferences setting (Original / CleanUp / NoFilename)
    ///     to a game file path, mirroring the WPF game button titles.
    /// </summary>
    private string GetDisplayTitle(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath) ?? filePath;

        return _settings.FilenameDisplayMode switch
        {
            "CleanUp" => CleanUpTitle(name),
            "NoFilename" => "",
            _ => name
        };
    }

    /// <summary>
    ///     Cleans a filename for display: strips bracketed/parenthesized annotations
    ///     (e.g. "[USA]", "(Rev 1)"), replaces separators with spaces, collapses whitespace.
    /// </summary>
    private static string CleanUpTitle(string name)
    {
        var timeout = TimeSpan.FromSeconds(1);
        var cleaned = Regex.Replace(name, @"\s*[\[\(][^\]\)]*[\]\)]", "", RegexOptions.None, timeout);
        cleaned = cleaned.Replace('_', ' ').Replace('.', ' ');
        cleaned = Regex.Replace(cleaned, @"\s+", " ", RegexOptions.None, timeout).Trim();
        return cleaned;
    }

    /// <summary>
    ///     Invalidates the cached file list for one system (called when the game file
    ///     watcher detects changes on disk, so the next scan picks them up).
    /// </summary>
    /// <param name="systemName">The affected system name.</param>
    public void InvalidateGameFileCacheForSystem(string systemName)
    {
        _loadingOrchestrator.InvalidateSystem(systemName);
    }

    /// <summary>
    ///     Invalidates all cached file lists (called when the system configuration
    ///     changes, e.g. after adding a system in Easy Mode).
    /// </summary>
    public void InvalidateAllGameFileCaches()
    {
        _loadingOrchestrator.InvalidateAll();
    }

    private void LoadAllGames()
    {
        try
        {
            IsShowingFavorites = false;
            IsShowingRetroAchievements = false;
            IsMixedView = true;
            SelectedSystem = "";
            LetterFilter = "";
            _allSystems = _systemManager.LoadSystems();

            RefreshSystemCounts();

            var games = ScanGames(_allSystems);
            ApplyFavoritesAndHistory(games);
            ShowGames(games);
            StatusText = _localization.GetString("Status.AllGames", "All Games");
            ToolbarTitle = _localization.GetString("App.Title", "SimpleLauncher");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load all games");
            StatusText = _localization.GetString("Errorloadinggames", "Error loading games");
        }
    }

    private void ApplyFavoritesAndHistory(List<GameCardViewModel> games)
    {
        _favoritePaths = _favoritesManager.GetFavoritePaths();
        var historyLookup = _playHistoryManager.GetHistoryLookup();

        foreach (var game in games)
        {
            game.IsFavorite = _favoritePaths.Contains(Path.GetFileName(game.FilePath));

            if (historyLookup.TryGetValue(game.FilePath, out var history))
            {
                game.PlayCount = history.TimesPlayed;
                game.TimesPlayed = history.TimesPlayed.ToString(CultureInfo.InvariantCulture);
                var ts = TimeSpan.FromSeconds(history.TotalPlayTime);
                game.PlayTime = ts.TotalHours >= 1
                    ? $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s"
                    : $"{ts.Minutes}m {ts.Seconds}s";
                game.LastPlayed = history.LastPlayDate;
            }
            else
            {
                game.TimesPlayed = "0";
                game.PlayTime = "0m 0s";
            }
        }

        // Apply Show Games filter via the service
        var filtered = _gameFilter.FilterByShowGamesSetting(games);
        if (filtered.Count != games.Count)
        {
            games.Clear();
            games.AddRange(filtered);
        }
    }

    partial void OnGamesChanged(ObservableCollection<GameCardViewModel> value)
    {
        UpdateGameCount(value.Count);
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    ///     Recomputes per-system game counts from a full scan of all configured system folders
    ///     (resolving %BASEFOLDER% / relative paths), independent of the current view.
    /// </summary>
    private void RefreshSystemCounts()
    {
        try
        {
            SystemGameCounts = ComputeSystemCounts(_allSystems);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh system game counts");
        }
    }

    /// <summary>
    ///     Computes per-system game counts via the loading orchestrator (cached file lists).
    ///     Pure computation — safe on any thread.
    /// </summary>
    private Dictionary<string, int> ComputeSystemCounts(List<SystemManagerConfig> systems)
    {
        return _loadingOrchestrator.ComputeSystemCounts(systems);
    }

    private void UpdateGameCount(int? count = null)
    {
        var c = count ?? Games.Count;
        GameCountText = string.Format(CultureInfo.InvariantCulture,
            _localization.GetString("Status.Games", "{0} game(s)"), c);
    }
}