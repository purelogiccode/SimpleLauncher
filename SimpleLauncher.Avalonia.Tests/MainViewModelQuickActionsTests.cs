using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.GameFilter;
using SimpleLauncher.Avalonia.Services.GameLauncher;
using SimpleLauncher.Avalonia.Services.LoadingOverlay;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UsageStats;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the WPF-parity quick actions added to MainViewModel (Phase 11):
///     the letter filter bar, Feeling Lucky (random game), the MAME sort-order toggle,
///     and Ctrl+wheel card-size zoom. All I/O is isolated to temp ROM folders and a
///     temp system.xml (the same pattern as GameScannerServiceTests).
///     The database is redirected to an isolated temp file (exclusive collection) and
///     seeded from the fixture XML so results never depend on real user data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class MainViewModelQuickActionsTests : IDisposable
{
    private readonly IConfiguration _config;
    private readonly Mock<IFindCoverImageService> _findCover = new();
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<IMameDataService> _mameData = new();
    private readonly Mock<IMessageBoxLibraryService> _messageBox = new();
    private readonly Mock<IRetroAchievementsHashScanner> _raHashScanner = new();
    private readonly Mock<IRetroAchievementsHashStore> _raHashStore = new();
    private readonly FakeLoadingOverlayHost _loadingHost = new();
    private ManualResetEventSlim? _scanGate;
    private readonly AvaloniaGameCacheService _gameCache = new();
    private readonly AvaloniaPaginationService _pagination;
    private readonly string _romsFolder;
    private readonly SettingsManagerService _settings;
    private readonly SystemManagerService _systemManager;
    private readonly string _systemXmlPath;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_QuickActionsTest_{Guid.NewGuid():N}");
    private readonly MainViewModel _viewModel;

    public MainViewModelQuickActionsTests()
    {
        _romsFolder = Path.Combine(_tempRoot, "roms");
        Directory.CreateDirectory(_romsFolder);
        WriteGameFile("abyss.zip");
        WriteGameFile("beyond.zip");
        WriteGameFile("cantina.zip");
        WriteGameFile("123start.zip");
        WriteGameFile("zero.was");

        _systemXmlPath = Path.Combine(_tempRoot, "system.xml");
        File.WriteAllText(_systemXmlPath, $"""
                                           <SystemConfigs>
                                             <SystemConfig>
                                               <SystemName>Test System</SystemName>
                                               <SystemFolders>
                                                 <SystemFolder>{_romsFolder}</SystemFolder>
                                               </SystemFolders>
                                               <SystemImageFolder>{_romsFolder}\images</SystemImageFolder>
                                               <FileFormatsToSearch>
                                                 <FormatToSearch>.zip</FormatToSearch>
                                                 <FormatToSearch>.was</FormatToSearch>
                                               </FileFormatsToSearch>
                                               <FileFormatsToLaunch>
                                                 <FormatToLaunch>.zip</FormatToLaunch>
                                               </FileFormatsToLaunch>
                                             </SystemConfig>
                                           </SystemConfigs>
                                           """);

        _config = TestEnvironment.ConfigurationFromJson(
            $$"""{"SystemXmlPath": "{{_systemXmlPath.Replace("\\", @"\\")}}"}""");

        UnifiedTestDatabase.RedirectToTempDb(_tempRoot);

        var settings = TestDependencies.Settings(_config, _messageBox);
        _settings = settings;
        _systemManager = new SystemManagerService(_config);
        UnifiedTestDatabase.SeedSystemsFromXml(_systemManager, _systemXmlPath);
        var loadingOrchestrator = new AvaloniaGameFileLoadingOrchestrator(
            _gameCache, _logger.Object);
        _pagination = new AvaloniaPaginationService(TestDependencies.ResourceProvider().Object);

        // Wire the reference-counted loading overlay to a recording host so tests can
        // assert the WPF-parity wait overlay is shown/hidden around long operations.
        var loadingOverlay = new AvaloniaLoadingOverlayService(new PlaySoundEffects(settings, _logger.Object));
        loadingOverlay.Initialize(_loadingHost);

        _mameData.Setup(m => m.Lookup).Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "beyond", "Beyond the Beyond" },
            { "cantina", "Cantina" }
        });

        // The gate lets a test hold the background scan open and verify that the old
        // grid was cleared before the new cards are produced.
        _findCover
            .Setup(s => s.FindCoverImagePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string)null!)
            .Callback(() => _scanGate?.Wait());

        _viewModel = new MainViewModel(
            new FavoritesManager(),
            new PlayHistoryManager(),
            _systemManager,
            CreateLauncher(_systemManager, settings),
            _findCover.Object,
            settings,
            _pagination,
            loadingOrchestrator,
            _raHashScanner.Object,
            _raHashStore.Object,
            new RetroAchievementsManager(),
            _messageBox.Object,
            _mameData.Object,
            new AvaloniaGameFilterService(settings, _mameData.Object),
            loadingOverlay,
            new LocalizationService());

        // Paginate only above 1 million games so the test views are never sliced.
        _viewModel.ConfigurePagination(1_000_000);
    }

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // best effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    private void WriteGameFile(string name)
    {
        File.WriteAllText(Path.Combine(_romsFolder, name), "fake rom");
    }

    private static LauncherService CreateLauncher(SystemManagerService systemManager, SettingsManagerService settings)
    {
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger>().Object;
        var messageBox = new Mock<IMessageBoxLibraryService>();

        var askAi = new AskAiToFixParameters(
            messageBox.Object,
            new Mock<IParameterResolverService>().Object,
            new Mock<ISystemConfigurationWriterService>().Object,
            systemManager,
            logger,
            new LocalizationService());

        return new LauncherService(
            messageBox.Object,
            [],
            config,
            new Mock<IExtractionService>().Object,
            new Mock<IMountXisoFiles>().Object,
            new Mock<IMountChdFiles>().Object,
            new Mock<IMountZipFiles>().Object,
            askAi,
            settings,
            [new DefaultLaunchStrategy()],
            new PlayHistoryManager(),
            new Mock<Stats>(new Mock<IHttpClientFactory>().Object, config, logger).Object,
            new Mock<GamePadController>(messageBox.Object, config, logger).Object,
            new LocalizationService());
    }

    private int FullGameCount()
    {
        _viewModel.ClearLetterFilter();
        return _viewModel.Games.Count;
    }

    [Fact]
    public async Task LetterFilter_FiltersByFirstLetterOfFileName()
    {
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);

        _viewModel.SetLetterFilter("B");

        var titles = _viewModel.Games.Select(static g => Path.GetFileName(g.FilePath)).ToList();
        Assert.True(titles.All(t => t.StartsWith("b", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("beyond.zip", titles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("abyss.zip", titles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("cantina.zip", titles, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LetterFilter_HashMatchesDigitLedFiles()
    {
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
        _viewModel.SetLetterFilter("#");

        var titles = _viewModel.Games.Select(static g => Path.GetFileName(g.FilePath)).ToList();
        Assert.Contains("123start.zip", titles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("abyss.zip", titles, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LetterFilter_ClearRestoresAllGames()
    {
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
        _viewModel.SetLetterFilter("C");

        Assert.True(_viewModel.Games.Count < FullGameCount());

        _viewModel.ClearLetterFilter();

        Assert.Equal(FullGameCount(), _viewModel.Games.Count);
    }

    [Fact]
    public async Task ApplySortedCurrentView_SortsBackingListAndSurvivesLetterFilterChanges()
    {
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);

        var descending = _viewModel.CurrentBaseGames
            .OrderByDescending(static g => g.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _viewModel.ApplySortedCurrentView(descending);

        Assert.Equal(
            descending.Select(static g => g.FileName),
            _viewModel.Games.Select(static g => g.FileName),
            StringComparer.OrdinalIgnoreCase);

        // The order must survive a letter-filter round trip: sorting the displayed
        // slice (the old behavior) was reverted by every re-filter/re-page.
        _viewModel.SetLetterFilter("B");
        _viewModel.ClearLetterFilter();

        Assert.Equal(
            descending.Select(static g => g.FileName),
            _viewModel.Games.Select(static g => g.FileName),
            StringComparer.OrdinalIgnoreCase);
    }

    // ── Pagination restart (WPF parity: loading a system / clicking "All") ──

    [Fact]
    public async Task NavigateToSystem_RestartsPaginationOnFirstPage()
    {
        _viewModel.ConfigurePagination(2);
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
        _viewModel.GoToNextPage();
        Assert.Equal(2, _pagination.CurrentPage);

        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        Assert.Equal(1, _pagination.CurrentPage);
        Assert.Equal(
            _viewModel.CurrentBaseGames.Take(2).Select(static g => g.FilePath),
            _viewModel.Games.Select(static g => g.FilePath),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllLetterButton_RestartsPaginationOnFirstPage()
    {
        _viewModel.ConfigurePagination(2);
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
        _viewModel.GoToNextPage();
        Assert.Equal(2, _pagination.CurrentPage);

        _viewModel.SetLetterFilter(""); // "All" letter button

        Assert.Equal(1, _pagination.CurrentPage);
        Assert.Equal(
            _viewModel.CurrentBaseGames.Take(2).Select(static g => g.FilePath),
            _viewModel.Games.Select(static g => g.FilePath),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshCurrentView_KeepsCurrentPage()
    {
        _viewModel.ConfigurePagination(2);
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
        _viewModel.GoToNextPage();
        Assert.Equal(2, _pagination.CurrentPage);

        await _viewModel.RefreshCurrentViewAsync();

        Assert.Equal(2, _pagination.CurrentPage);
    }

    // ── System information panel (WPF DisplaySystemInformation parity) ──

    [Fact]
    public async Task ApplyLetterFilterAsync_AfterRetroAchievementsFilter_RestoresCompleteLibrary()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        // Pretend the system has an up-to-date scan that matches no game (the mocked
        // hash store returns null), so the filter shows an empty RetroAchievements view.
        _raHashScanner.Setup(s => s.IsScanUpToDate("Test System")).Returns(true);
        await _viewModel.ShowGamesWithRetroAchievementsCommand.ExecuteAsync(null);
        Assert.True(_viewModel.IsShowingRetroAchievements);
        Assert.Empty(_viewModel.Games);

        // WPF parity: a letter button (including "All") loads the complete library of
        // the current context, so the RetroAchievements view never limits the results.
        await _viewModel.ApplyLetterFilterAsync("");
        Assert.False(_viewModel.IsShowingRetroAchievements);
        Assert.Equal(5, _viewModel.Games.Count);

        await _viewModel.ApplyLetterFilterAsync("B");
        var titles = _viewModel.Games.Select(static g => Path.GetFileName(g.FilePath)).ToList();
        Assert.Contains("beyond.zip", titles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("abyss.zip", titles, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearSearchTextSilently_ClearsQueryWithoutReloading()
    {
        _viewModel.SearchText = "abyss";

        _viewModel.ClearSearchTextSilently();

        Assert.Equal("", _viewModel.SearchText);
    }

    [Fact]
    public void SetSystemInformation_StoresLinesWithoutCoveringGames()
    {
        var lines = new List<SystemInfoLine> { new() { Text = "System Folder: C:\\roms" } };

        _viewModel.SetSystemInformation(lines);

        Assert.Same(lines, _viewModel.SystemInfoLines);
        Assert.False(_viewModel.IsSystemInfoVisible);

        // The status-bar info button opens the stored summary explicitly.
        _viewModel.ShowSystemInformation(_viewModel.SystemInfoLines);

        Assert.True(_viewModel.IsSystemInfoVisible);
    }

    [Fact]
    public async Task NavigateToAllGames_ClearsStoredSystemInformation()
    {
        _viewModel.SetSystemInformation([new SystemInfoLine { Text = "System Folder: C:\\roms" }]);

        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);

        Assert.Empty(_viewModel.SystemInfoLines);
        Assert.False(_viewModel.IsSystemInfoVisible);
    }

    [Fact]
    public void ShowSystemInformation_VisibleUntilLetterFilterLoadsGames()
    {
        var lines = new List<SystemInfoLine>
        {
            new() { Text = "Click on the letter buttons above to see the games" },
            new() { Text = " " },
            new() { Text = "System Folder: C:\\roms" }
        };

        _viewModel.ShowSystemInformation(lines);

        Assert.True(_viewModel.IsSystemInfoVisible);
        Assert.Same(lines, _viewModel.SystemInfoLines);

        // The letter buttons are the "load games" action: they replace the info panel.
        _viewModel.SetLetterFilter("A");

        Assert.False(_viewModel.IsSystemInfoVisible);
    }

    [Fact]
    public async Task ShowSystemInformation_HiddenWhenNavigatingToSystem()
    {
        _viewModel.ShowSystemInformation([new SystemInfoLine { Text = "System Folder: C:\\roms" }]);

        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        Assert.False(_viewModel.IsSystemInfoVisible);
        Assert.NotEmpty(_viewModel.Games);
    }

    // ── Loading overlay (WPF wait-overlay parity) ──

    [Fact]
    public async Task NavigateToSystem_ShowsAndHidesLoadingOverlay()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        await HeadlessAvalonia.WaitUntilAsync(() =>
            _loadingHost.States.Count > 0 && !_loadingHost.IsLoading);

        Assert.Contains(true, _loadingHost.States);
        Assert.False(_loadingHost.IsLoading);
        Assert.Contains(_loadingHost.Messages,
            static m => m.Contains("Loading system", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReloadCurrentSystem_PreservesSystemAndLetterFilter()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");
        Assert.NotEmpty(_viewModel.Games);

        var letter = _viewModel.Games[0].DisplayTitle[..1];
        _viewModel.SetLetterFilter(letter);
        var filteredCount = _viewModel.Games.Count;

        await _viewModel.ReloadCurrentSystemAsync();

        Assert.Equal("Test System", _viewModel.SelectedSystem);
        Assert.Equal(letter, _viewModel.LetterFilter);
        Assert.Equal(filteredCount, _viewModel.Games.Count);
    }

    [Fact]
    public async Task SupersededViewLoad_ReleasesItsLoadingOverlayImmediately()
    {
        var calls = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _findCover
            .Setup(s => s.FindCoverImagePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string)null!)
            .Callback(() =>
            {
                if (Interlocked.Increment(ref calls) != 1) return;
                entered.TrySetResult();
                gate.Task.GetAwaiter().GetResult();
            });

        Task? supersededLoad = null;
        try
        {
            supersededLoad = _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // A newer view load must release the old scan's overlay reference even though
            // the uncancellable filesystem scan is still blocked in the background.
            await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);

            Assert.False(_loadingHost.IsLoading);
            Assert.False(_viewModel.IsLoading);
        }
        finally
        {
            gate.TrySetResult();
            if (supersededLoad != null) await supersededLoad;
        }
    }

    [Fact]
    public async Task ViewLoad_ClearsThePreviousGridBeforeTheScanCompletes()
    {
        // Start with a populated All Games view.
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
        Assert.NotEmpty(_viewModel.Games);

        using var gate = new ManualResetEventSlim(false);
        _scanGate = gate;
        try
        {
            // Switching to a system must empty the old grid immediately: the loading
            // overlay is translucent, so previous covers must not show through it.
            var systemNavigation = _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");
            Assert.Empty(_viewModel.Games);
            Assert.True(_viewModel.IsEmpty);

            gate.Set();
            await systemNavigation;
            Assert.NotEmpty(_viewModel.Games);

            // Switching back to All Games clears the system's cards the same way.
            gate.Reset();
            var allGamesNavigation = _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
            Assert.Empty(_viewModel.Games);

            gate.Set();
            await allGamesNavigation;
            Assert.NotEmpty(_viewModel.Games);
        }
        finally
        {
            gate.Set();
            _scanGate = null;
        }
    }

    [Fact]
    public async Task Search_ShowsLoadingOverlay()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        _viewModel.SearchText = "abyss";

        await HeadlessAvalonia.WaitUntilAsync(() =>
            _loadingHost.Messages.Any(static m => m.Contains("Searching", StringComparison.OrdinalIgnoreCase)));

        await HeadlessAvalonia.WaitUntilAsync(() => !_loadingHost.IsLoading);
    }

    [Fact]
    public async Task Search_AfterSelectingSystem_MatchesMameMachineDescription()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        _viewModel.SearchText = "Beyond the Beyond";

        await HeadlessAvalonia.WaitUntilAsync(() =>
            !_viewModel.IsLoading && _viewModel.Games.Count == 1);

        Assert.Equal("beyond", _viewModel.Games[0].FileName, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Test System", _viewModel.Games[0].SystemName);
    }

    [Fact]
    public async Task Search_AfterSelectingSystem_MatchesFileNameWhenDisplayTitleIsHidden()
    {
        _settings.FilenameDisplayMode = "NoFilename";
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        _viewModel.SearchText = "abyss";

        await HeadlessAvalonia.WaitUntilAsync(() =>
            !_viewModel.IsLoading && _viewModel.Games.Count == 1);

        Assert.Equal("abyss", _viewModel.Games[0].FileName, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisplayMachineName_Toggle_ShowsMameDescriptionOnLoadedCards()
    {
        _settings.DisplayMachineName = true;
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        var beyond = _viewModel.Games.Single(static g => string.Equals(g.FileName, "beyond", StringComparison.Ordinal));
        Assert.True(beyond.ShowMachineDescription);
        Assert.Equal("Beyond the Beyond", beyond.AccessibleName);

        _settings.DisplayMachineName = false;
        _viewModel.RefreshMachineNameVisibility();

        Assert.False(beyond.ShowMachineDescription);
        Assert.Equal(beyond.DisplayTitle, beyond.AccessibleName);
    }

    [Fact]
    public async Task SearchAsync_ExecutesEvenWhenQueryIsAlreadyInSearchText()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        // Run the debounced search once, then replace the view with the full library.
        _viewModel.SearchText = "abyss";
        await HeadlessAvalonia.WaitUntilAsync(() =>
            !_viewModel.IsLoading && _viewModel.Games.Count == 1);
        await _viewModel.ApplyLetterFilterAsync("");
        Assert.Equal(5, _viewModel.Games.Count);

        // The Search button/Enter path must run even though SearchText was previously
        // "abyss": assigning the same value alone would not fire OnSearchTextChanged.
        await _viewModel.SearchAsync("abyss");

        await HeadlessAvalonia.WaitUntilAsync(() =>
            !_viewModel.IsLoading && _viewModel.Games.Count == 1);
        Assert.Equal("abyss", _viewModel.Games[0].FileName, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearingSearchText_KeepsSelectedSystemAndReloadsItsGames()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");
        _viewModel.SearchText = "abyss";
        await HeadlessAvalonia.WaitUntilAsync(() =>
            !_viewModel.IsLoading && _viewModel.Games.Count == 1);

        _viewModel.SearchText = "";

        await HeadlessAvalonia.WaitUntilAsync(() =>
            !_viewModel.IsLoading &&
            string.Equals(_viewModel.SelectedSystem, "Test System", StringComparison.Ordinal) &&
            _viewModel.Games.Count == 5);
        Assert.Equal("Test System", _viewModel.SelectedSystem);
    }

    [Fact]
    public async Task PickRandomGame_ReplacesViewWithSingleGameFromSelectedSystem()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");
        // The explicit info panel covers the browser; the random pick must replace it
        // with the picked game's card.
        _viewModel.ShowSystemInformation([new SystemInfoLine { Text = "System Folder: C:\\roms" }]);
        Assert.True(_viewModel.IsSystemInfoVisible);

        var randomGame = await _viewModel.PickRandomGameAsync();

        Assert.NotNull(randomGame);
        Assert.Equal("Test System", randomGame.SystemName);
        Assert.False(_viewModel.IsSystemInfoVisible);
        var shown = Assert.Single(_viewModel.Games);
        Assert.Equal(randomGame.FilePath, shown.FilePath);
    }

    [Fact]
    public async Task PickRandomGame_WithoutSelectedSystemReturnsNullAndKeepsView()
    {
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);
        var countBefore = _viewModel.Games.Count;

        var randomGame = await _viewModel.PickRandomGameAsync();

        Assert.Null(randomGame);
        Assert.Equal(countBefore, _viewModel.Games.Count);
    }

    [Fact]
    public async Task PickRandomGame_ClearsLetterFilterAndPicksFromFullLibrary()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");
        _viewModel.SetLetterFilter("A");

        var randomGame = await _viewModel.PickRandomGameAsync();

        // The pick comes from the FULL system library, not the letter-filtered subset
        Assert.NotNull(randomGame);
        Assert.Equal("", _viewModel.LetterFilter);
        Assert.Contains(_viewModel.Games,
            g => string.Equals(g.FilePath, randomGame.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ToggleMameSortOrder_ReordersByMachineDescription()
    {
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);

        _viewModel.ToggleMameSortOrder();

        var titles = _viewModel.Games.Select(static g => Path.GetFileNameWithoutExtension(g.FilePath)).ToList();

        // Machine-description order: "beyond" → "Beyond the Beyond" and "cantina" →
        // "Cantina" replace their file names for sorting; the other games keep theirs.
        // Keys: 123start, abyss, Beyond the Beyond, Cantina, zero
        Assert.Equal(3, titles.IndexOf("cantina"));
        Assert.Equal(2, titles.IndexOf("beyond"));
        Assert.Equal(0, titles.IndexOf("123start"));
    }

    [Fact]
    public async Task NavigateToSystem_SortsByFileNameAndMachineDescription()
    {
        var system = _systemManager.GetSystem("Test System");
        Assert.NotNull(system);

        // Serve the cache in reverse order so the test never depends on the OS
        // directory-enumeration order (Windows NTFS and Linux ext4 differ).
        var files = Directory.GetFiles(_romsFolder, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(static f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        PrimeCache();
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        var fileNameOrder = _viewModel.Games.Select(static g => Path.GetFileName(g.FilePath)).ToList();
        Assert.Equal(
            fileNameOrder.OrderBy(static f => f, StringComparer.OrdinalIgnoreCase),
            fileNameOrder,
            StringComparer.OrdinalIgnoreCase);

        // The MAME descriptions reorder the list differently from the file names.
        _mameData.Setup(m => m.Lookup).Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["beyond"] = "Aardvark",
            ["cantina"] = "Zulu"
        });
        _viewModel.ToggleMameSortOrder();
        Assert.Equal("MachineDescription", _viewModel.MameSortOrder);

        PrimeCache();
        await _viewModel.ApplyLetterFilterAsync(""); // the "All" button reloads and must sort

        Assert.Equal(
            new[] { "123start.zip", "beyond.zip", "abyss.zip", "zero.was", "cantina.zip" },
            _viewModel.Games.Select(static g => Path.GetFileName(g.FilePath)),
            StringComparer.OrdinalIgnoreCase);
        return;

        void PrimeCache() => _gameCache.SetCachedFiles(system.SystemName, files, BuildSignature(system));
    }

    private static string BuildSignature(SystemManagerConfig system)
    {
        var folders = string.Join('\u001E', system.SystemFolders ?? []);
        var formats = string.Join('\u001E', system.FileFormatsToSearch ?? []);
        var recursion = system is { DisableRecursiveSearch: true, GroupByFolder: false } ? '0' : '1';
        var groupByFolder = system.GroupByFolder ? '1' : '0';
        return $"{folders}\u001F{formats}\u001F{recursion}\u001F{groupByFolder}";
    }

    [Fact]
    public void ZoomIn_IncreasesCardWidthWithinBounds()
    {
        _viewModel.CardWidth = 780;

        _viewModel.ZoomIn();

        Assert.True(_viewModel.CardWidth > 780);
        Assert.True(_viewModel.CardWidth <= 800);
    }

    [Fact]
    public void ZoomOut_DecreasesCardWidthWithinBounds()
    {
        _viewModel.CardWidth = 100;

        _viewModel.ZoomOut();

        Assert.True(_viewModel.CardWidth < 100);
        Assert.True(_viewModel.CardWidth >= 50);
    }

    [Fact]
    public void Zoom_ClampsAtLimits()
    {
        _viewModel.CardWidth = 800;
        _viewModel.ZoomIn();
        Assert.Equal(800, (int)_viewModel.CardWidth);

        _viewModel.CardWidth = 50;
        _viewModel.ZoomOut();
        Assert.Equal(50, (int)_viewModel.CardWidth);
    }

    // ── Button size: menu-grid snapping (WPF HandleButtonSizeAsync parity) ──

    [Fact]
    public void ApplyButtonSize_SnapsToMenuOptionsAndPersists()
    {
        Assert.Equal(200, _viewModel.ApplyButtonSize(213));
        Assert.Equal(200, (int)_viewModel.CardWidth);
        Assert.Equal(200, _settings.ThumbnailSize);

        Assert.Equal(800, _viewModel.ApplyButtonSize(1000));
        Assert.Equal(800, _settings.ThumbnailSize);

        Assert.Equal(50, _viewModel.ApplyButtonSize(0));
        Assert.Equal(50, _settings.ThumbnailSize);
    }

    [Fact]
    public void ApplyButtonSize_RoundsToNearestMenuOption()
    {
        Assert.Equal(250, _viewModel.ApplyButtonSize(225));
        Assert.Equal(200, _viewModel.ApplyButtonSize(224));
    }

    [Fact]
    public void ZoomIn_FromOffGridSize_LandsOnMenuOption()
    {
        _viewModel.CardWidth = 168; // legacy Avalonia default, not a Button Size option

        _viewModel.ZoomIn();

        Assert.Equal(200, (int)_viewModel.CardWidth);
        Assert.Equal(200, _settings.ThumbnailSize);
    }

    [Fact]
    public void ZoomOut_FromOffGridSize_LandsOnMenuOption()
    {
        _viewModel.CardWidth = 168;

        _viewModel.ZoomOut();

        Assert.Equal(100, (int)_viewModel.CardWidth);
        Assert.Equal(100, _settings.ThumbnailSize);
    }

    // ── Filename preferences (no full-library reload on toggle) ──

    [Fact]
    public async Task RefreshFilenameDisplayTitles_NoFilename_ClearsTitlesInPlaceAndKeepsView()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");
        var countBefore = _viewModel.Games.Count;
        Assert.True(countBefore > 0);
        Assert.All(_viewModel.Games, static g => Assert.False(string.IsNullOrEmpty(g.DisplayTitle)));

        var overlayStatesBefore = _loadingHost.States.Count;

        _settings.FilenameDisplayMode = "NoFilename";
        _viewModel.RefreshFilenameDisplayTitles();

        // Same cards, titles cleared, system view untouched, no loading overlay churn.
        Assert.Equal(countBefore, _viewModel.Games.Count);
        Assert.All(_viewModel.Games, static g => Assert.Equal("", g.DisplayTitle));
        Assert.Equal("Test System", _viewModel.SelectedSystem);
        Assert.Equal(overlayStatesBefore, _loadingHost.States.Count);
    }

    [Fact]
    public async Task RefreshFilenameDisplayTitles_Original_RestoresFileNames()
    {
        await _viewModel.NavigateToAllGamesCommand.ExecuteAsync(null);

        _settings.FilenameDisplayMode = "NoFilename";
        _viewModel.RefreshFilenameDisplayTitles();
        Assert.All(_viewModel.Games, static g => Assert.Equal("", g.DisplayTitle));

        _settings.FilenameDisplayMode = "Original";
        _viewModel.RefreshFilenameDisplayTitles();

        Assert.All(_viewModel.Games,
            static g => Assert.Equal(Path.GetFileNameWithoutExtension(g.FilePath), g.DisplayTitle));
    }

    [Fact]
    public async Task RefreshFilenameDisplayTitles_CleanUp_StripsAnnotations()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "galaga.[USA].(Rev 1).zip"), "fake rom");
        _viewModel.InvalidateGameFileCacheForSystem("Test System");
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        _settings.FilenameDisplayMode = "CleanUp";
        _viewModel.RefreshFilenameDisplayTitles();

        var galaga = Assert.Single(_viewModel.Games,
            g => g.FilePath.EndsWith("galaga.[USA].(Rev 1).zip", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("galaga", galaga.DisplayTitle);
    }
}
