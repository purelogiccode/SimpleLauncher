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
///     Tests for the card overlay buttons (WPF GameButtonFactory parity): the Options menu
///     settings OverlayRetroAchievementButton / OverlayOpenVideoButton / OverlayOpenInfoButton
///     drive the per-card visibility flags, and the menu toggle refreshes the loaded cards
///     in place without a full library reload. I/O is isolated to temp ROM folders and a
///     temp system.xml; the database is redirected to an isolated temp file.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class MainViewModelOverlayButtonTests : IDisposable
{
    private readonly IConfiguration _config;
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<IMameDataService> _mameData = new();
    private readonly Mock<IMessageBoxLibraryService> _messageBox = new();
    private readonly FakeLoadingOverlayHost _loadingHost = new();
    private readonly string _nesRomsFolder;
    private readonly string _otherRomsFolder;
    private readonly SettingsManagerService _settings;
    private readonly string _systemXmlPath;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_OverlayTest_{Guid.NewGuid():N}");
    private readonly MainViewModel _viewModel;

    public MainViewModelOverlayButtonTests()
    {
        // "Nintendo NES" is in the card view model's RA-supported list; "Test System" is not.
        _nesRomsFolder = Path.Combine(_tempRoot, "nes");
        _otherRomsFolder = Path.Combine(_tempRoot, "other");
        Directory.CreateDirectory(_nesRomsFolder);
        Directory.CreateDirectory(_otherRomsFolder);
        WriteGameFile(_nesRomsFolder, "adventure.zip");
        WriteGameFile(_nesRomsFolder, "battle.zip");
        WriteGameFile(_otherRomsFolder, "puzzle.zip");

        _systemXmlPath = Path.Combine(_tempRoot, "system.xml");
        File.WriteAllText(_systemXmlPath, $"""
                                           <SystemConfigs>
                                             <SystemConfig>
                                               <SystemName>Nintendo NES</SystemName>
                                               <SystemFolders>
                                                 <SystemFolder>{_nesRomsFolder}</SystemFolder>
                                               </SystemFolders>
                                               <SystemImageFolder>{_nesRomsFolder}\images</SystemImageFolder>
                                               <FileFormatsToSearch>
                                                 <FormatToSearch>.zip</FormatToSearch>
                                               </FileFormatsToSearch>
                                               <FileFormatsToLaunch>
                                                 <FormatToLaunch>.zip</FormatToLaunch>
                                               </FileFormatsToLaunch>
                                             </SystemConfig>
                                             <SystemConfig>
                                               <SystemName>Test System</SystemName>
                                               <SystemFolders>
                                                 <SystemFolder>{_otherRomsFolder}</SystemFolder>
                                               </SystemFolders>
                                               <SystemImageFolder>{_otherRomsFolder}\images</SystemImageFolder>
                                               <FileFormatsToSearch>
                                                 <FormatToSearch>.zip</FormatToSearch>
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
        var systemManager = new SystemManagerService(_config);
        UnifiedTestDatabase.SeedSystemsFromXml(systemManager, _systemXmlPath);
        var loadingOrchestrator = new AvaloniaGameFileLoadingOrchestrator(
            new AvaloniaGameCacheService(), _logger.Object);

        var loadingOverlay = new AvaloniaLoadingOverlayService(new PlaySoundEffects(settings, _logger.Object));
        loadingOverlay.Initialize(_loadingHost);

        _mameData.Setup(m => m.Lookup).Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        _viewModel = new MainViewModel(
            new FavoritesManager(),
            new PlayHistoryManager(),
            systemManager,
            CreateLauncher(systemManager, settings),
            new Mock<IFindCoverImageService>().Object,
            new Stats(TestDependencies.HttpFactory(new HttpClient()).Object, _config, _logger.Object),
            settings,
            new AvaloniaPaginationService(TestDependencies.ResourceProvider().Object),
            loadingOrchestrator,
            new Mock<IRetroAchievementsHashScanner>().Object,
            new Mock<IRetroAchievementsHashStore>().Object,
            new RetroAchievementsManager(),
            _messageBox.Object,
            _mameData.Object,
            new AvaloniaGameFilterService(new Mock<IFindCoverImageService>().Object, settings, _mameData.Object),
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

    private static void WriteGameFile(string folder, string name)
    {
        File.WriteAllText(Path.Combine(folder, name), "fake rom");
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

    [Fact]
    public async Task DefaultSettings_ShowVideoOverlayOnly()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Nintendo NES");

        Assert.NotEmpty(_viewModel.Games);
        Assert.All(_viewModel.Games, static g => Assert.False(g.ShowRetroAchievementOverlay));
        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowVideoOverlay));
        Assert.All(_viewModel.Games, static g => Assert.False(g.ShowInfoOverlay));
    }

    [Fact]
    public async Task AllSettingsEnabled_ShowAllOverlaysOnRaSupportedSystem()
    {
        _settings.OverlayRetroAchievementButton = true;
        _settings.OverlayOpenVideoButton = true;
        _settings.OverlayOpenInfoButton = true;

        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Nintendo NES");

        Assert.NotEmpty(_viewModel.Games);
        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowRetroAchievementOverlay));
        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowVideoOverlay));
        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowInfoOverlay));
    }

    [Fact]
    public async Task RaSettingEnabled_NonRaSupportedSystem_KeepsTrophyHidden()
    {
        _settings.OverlayRetroAchievementButton = true;
        _settings.OverlayOpenInfoButton = true;

        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Test System");

        Assert.NotEmpty(_viewModel.Games);
        Assert.All(_viewModel.Games, static g => Assert.False(g.ShowRetroAchievementOverlay));
        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowVideoOverlay));
        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowInfoOverlay));
    }

    [Fact]
    public async Task RefreshOverlayButtons_UpdatesLoadedCardsInPlace_AndKeepsView()
    {
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Nintendo NES");
        _viewModel.SetLetterFilter("A");
        var visibleCount = _viewModel.Games.Count;
        Assert.True(visibleCount > 0);
        var overlayStatesBefore = _loadingHost.States.Count;

        _settings.OverlayRetroAchievementButton = true;
        _settings.OverlayOpenVideoButton = false;
        _settings.OverlayOpenInfoButton = true;
        _viewModel.RefreshOverlayButtons();

        // Same view, same page, no loading overlay churn.
        Assert.Equal(visibleCount, _viewModel.Games.Count);
        Assert.Equal("Nintendo NES", _viewModel.SelectedSystem);
        Assert.Equal("A", _viewModel.LetterFilter);
        Assert.Equal(overlayStatesBefore, _loadingHost.States.Count);

        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowRetroAchievementOverlay));
        Assert.All(_viewModel.Games, static g => Assert.False(g.ShowVideoOverlay));
        Assert.All(_viewModel.Games, static g => Assert.True(g.ShowInfoOverlay));

        // Cards filtered out by the active letter must be updated too (the refresh covers
        // the whole backing list, not only the displayed page/filter slice).
        _viewModel.ClearLetterFilter();
        Assert.All(_viewModel.Games, static g => Assert.False(g.ShowVideoOverlay));
    }

    [Fact]
    public void RefreshOverlayButtons_WithoutLoadedGames_IsNoOp()
    {
        _viewModel.RefreshOverlayButtons();
    }
}
