using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.GameFilter;
using SimpleLauncher.Avalonia.Services.GameLauncher;
using SimpleLauncher.Avalonia.Services.LoadingOverlay;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
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
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<IMameDataService> _mameData = new();
    private readonly Mock<IMessageBoxLibraryService> _messageBox = new();
    private readonly string _romsFolder;
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
        var systemManager = new SystemManagerService(_config);
        UnifiedTestDatabase.SeedSystemsFromXml(systemManager, _systemXmlPath);
        var loadingOrchestrator = new AvaloniaGameFileLoadingOrchestrator(
            new AvaloniaGameCacheService(), _logger.Object);
        var pagination = new AvaloniaPaginationService(TestDependencies.ResourceProvider().Object);

        _mameData.Setup(m => m.Lookup).Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "beyond", "Beyond the Beyond" },
            { "cantina", "Cantina" }
        });

        _viewModel = new MainViewModel(
            new FavoritesManager(),
            new PlayHistoryManager(),
            systemManager,
            CreateLauncher(systemManager, settings),
            new Mock<IFindCoverImageService>().Object,
            new Stats(TestDependencies.HttpFactory(new HttpClient()).Object, _config, _logger.Object),
            settings,
            pagination,
            loadingOrchestrator,
            new Mock<IRetroAchievementsHashScanner>().Object,
            new Mock<IRetroAchievementsHashStore>().Object,
            new RetroAchievementsManager(),
            _messageBox.Object,
            _mameData.Object,
            new AvaloniaGameFilterService(new Mock<IFindCoverImageService>().Object, settings, _mameData.Object),
            new AvaloniaLoadingOverlayService(new PlaySoundEffects(settings, _logger.Object)),
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
    public void LetterFilter_FiltersByFirstLetterOfFileName()
    {
        _viewModel.NavigateToAllGamesCommand.Execute(null);

        _viewModel.SetLetterFilter("B");

        var titles = _viewModel.Games.Select(static g => Path.GetFileName(g.FilePath)).ToList();
        Assert.True(titles.All(t => t.StartsWith("b", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("beyond.zip", titles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("abyss.zip", titles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("cantina.zip", titles, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LetterFilter_HashMatchesDigitLedFiles()
    {
        _viewModel.NavigateToAllGamesCommand.Execute(null);
        _viewModel.SetLetterFilter("#");

        var titles = _viewModel.Games.Select(static g => Path.GetFileName(g.FilePath)).ToList();
        Assert.Contains("123start.zip", titles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("abyss.zip", titles, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LetterFilter_ClearRestoresAllGames()
    {
        _viewModel.NavigateToAllGamesCommand.Execute(null);
        _viewModel.SetLetterFilter("C");

        Assert.True(_viewModel.Games.Count < FullGameCount());

        _viewModel.ClearLetterFilter();

        Assert.Equal(FullGameCount(), _viewModel.Games.Count);
    }

    [Fact]
    public void ApplySortedCurrentView_SortsBackingListAndSurvivesLetterFilterChanges()
    {
        _viewModel.NavigateToAllGamesCommand.Execute(null);

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

    [Fact]
    public async Task PickRandomGame_ReplacesViewWithSingleGameFromSelectedSystem()
    {
        _viewModel.NavigateToSystemCommand.Execute("Test System");

        var randomGame = await _viewModel.PickRandomGameAsync();

        Assert.NotNull(randomGame);
        Assert.Equal("Test System", randomGame.SystemName);
        var shown = Assert.Single(_viewModel.Games);
        Assert.Equal(randomGame.FilePath, shown.FilePath);
    }

    [Fact]
    public async Task PickRandomGame_WithoutSelectedSystemReturnsNullAndKeepsView()
    {
        _viewModel.NavigateToAllGamesCommand.Execute(null);
        var countBefore = _viewModel.Games.Count;

        var randomGame = await _viewModel.PickRandomGameAsync();

        Assert.Null(randomGame);
        Assert.Equal(countBefore, _viewModel.Games.Count);
    }

    [Fact]
    public async Task PickRandomGame_ClearsLetterFilterAndPicksFromFullLibrary()
    {
        _viewModel.NavigateToSystemCommand.Execute("Test System");
        _viewModel.SetLetterFilter("A");

        var randomGame = await _viewModel.PickRandomGameAsync();

        // The pick comes from the FULL system library, not the letter-filtered subset
        Assert.NotNull(randomGame);
        Assert.Equal("", _viewModel.LetterFilter);
        Assert.Contains(_viewModel.Games,
            g => string.Equals(g.FilePath, randomGame.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToggleMameSortOrder_ReordersByMachineDescription()
    {
        _viewModel.NavigateToAllGamesCommand.Execute(null);

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
}