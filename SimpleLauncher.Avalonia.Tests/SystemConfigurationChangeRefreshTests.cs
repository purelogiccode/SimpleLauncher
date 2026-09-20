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
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UsageStats;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression tests for: add a system (e.g. XBOX via Easy Mode) then open it
///     shows 0 games. The view-model kept a stale system snapshot that was never
///     refreshed after configuration changes, so navigation filtered it out.
///     Fully hermetic: the unified database is redirected to an isolated temp file,
///     so the real AppData folder is never touched on any machine.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public sealed class SystemConfigurationChangeRefreshTests : IDisposable
{
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<IMessageBoxLibraryService> _messageBox = new();
    private readonly SystemManagerService _systemManager;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_SysRefresh_{Guid.NewGuid():N}");
    private readonly MainViewModel _viewModel;
    private readonly string _xboxFolder;

    public SystemConfigurationChangeRefreshTests()
    {
        _xboxFolder = Path.Combine(_tempRoot, "xboxgames");
        Directory.CreateDirectory(_xboxFolder);
        File.WriteAllText(Path.Combine(_xboxFolder, "halo.iso"), "fake rom");
        File.WriteAllText(Path.Combine(_xboxFolder, "fable.iso"), "fake rom");

        // Redirect the database process-wide (exclusive collection: no other
        // test class runs concurrently) and start from a valid, empty database.
        UnifiedTestDatabase.RedirectToTempDb(_tempRoot);

        var settings = TestDependencies.Settings(_config, _messageBox);
        _systemManager = new SystemManagerService(_config);
        var loadingOrchestrator = new AvaloniaGameFileLoadingOrchestrator(
            new AvaloniaGameCacheService(), _logger.Object);
        var pagination = new AvaloniaPaginationService(TestDependencies.ResourceProvider().Object);
        var mameData = new Mock<IMameDataService>();
        mameData.Setup(m => m.Lookup)
            .Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        _viewModel = new MainViewModel(
            new FavoritesManager(),
            new PlayHistoryManager(),
            _systemManager,
            CreateLauncher(_systemManager, settings),
            new Mock<IFindCoverImageService>().Object,
            new Stats(TestDependencies.HttpFactory(new HttpClient()).Object, _config, _logger.Object),
            settings,
            pagination,
            loadingOrchestrator,
            new Mock<IRetroAchievementsHashScanner>().Object,
            new Mock<IRetroAchievementsHashStore>().Object,
            new RetroAchievementsManager(),
            _messageBox.Object,
            mameData.Object,
            new AvaloniaGameFilterService(new Mock<IFindCoverImageService>().Object, settings, mameData.Object),
            new AvaloniaLoadingOverlayService(new PlaySoundEffects(settings, _logger.Object)),
            new LocalizationService());

        _viewModel.ConfigurePagination(1_000_000);
    }

    [Fact]
    public async Task ReloadSystemsAfterConfigurationChange_NewSystemBecomesNavigable()
    {
        // Add XBOX exactly like Easy Mode does (prod passes the shared manager).
        // No path override: the prod default path (redirected above) is used.
        await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(
            CreateXboxPreset(), _xboxFolder, _config, _logger.Object, _systemManager);

        // Before the refresh the snapshot (and its counts) still predate the change.
        Assert.False(_viewModel.SystemGameCounts.ContainsKey("Microsoft Xbox"));

        // The refresh MainWindow runs after every configuration change.
        await _viewModel.ReloadSystemsAfterConfigurationChangeAsync();

        Assert.True(_viewModel.SystemGameCounts.TryGetValue("Microsoft Xbox", out var count));
        Assert.Equal(2, count);

        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Microsoft Xbox");
        Assert.Equal(2, _viewModel.Games.Count);
    }

    [Fact]
    public async Task NavigateToSystem_FallsBackToDirectLookup_WhenSnapshotIsStale()
    {
        await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(
            CreateXboxPreset(), _xboxFolder, _config, _logger.Object, _systemManager);

        // No ReloadSystemsAfterConfigurationChange call: the snapshot is stale,
        // but navigation must still find the configured system.
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Microsoft Xbox");
        Assert.Equal(2, _viewModel.Games.Count);
    }

    [Fact]
    public async Task NavigateToSystem_UsesFreshConfiguration_WhenSnapshotPredatesAnEdit()
    {
        // First configuration: the original folder (2 games), opened once so the
        // game cache holds the old file list.
        await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(
            CreateXboxPreset(), _xboxFolder, _config, _logger.Object, _systemManager);
        await _viewModel.ReloadSystemsAfterConfigurationChangeAsync();
        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Microsoft Xbox");
        Assert.Equal(2, _viewModel.Games.Count);

        // Edit the system to point at another folder (1 game) without reloading the
        // view-model snapshot or invalidating the game cache. This is the post-edit
        // race window: the still-visible system card can be clicked before the
        // asynchronous reload has applied the new configuration.
        var editedFolder = Path.Combine(_tempRoot, "xboxgames-edited");
        Directory.CreateDirectory(editedFolder);
        File.WriteAllText(Path.Combine(editedFolder, "newgame.iso"), "fake rom");
        await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(
            CreateXboxPreset(), editedFolder, _config, _logger.Object, _systemManager);
        _systemManager.InvalidateCache();

        await _viewModel.NavigateToSystemCommand.ExecuteAsync("Microsoft Xbox");

        // The fresh configuration must win over both the stale snapshot and the
        // cached old-folder file list.
        var game = Assert.Single(_viewModel.Games);
        Assert.Contains(editedFolder, game.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private static EasyModeSystemConfig CreateXboxPreset()
    {
        return new EasyModeSystemConfig
        {
            SystemName = "Microsoft Xbox",
            SystemFolder = "%BASEFOLDER%\\roms\\Microsoft Xbox",
            SystemImageFolder = "%BASEFOLDER%\\images\\Microsoft Xbox",
            FileFormatsToSearch = ["iso", "chd"],
            FileFormatsToLaunch = [],
            ExtractFileBeforeLaunch = false,
            Emulators = new EmulatorsConfig
            {
                Emulator = new EmulatorConfig
                {
                    EmulatorName = "Xemu",
                    EmulatorLocation = "%BASEFOLDER%\\emulators\\Xemu\\xemu.exe",
                    EmulatorParameters = "-full-screen -dvd_path"
                }
            }
        };
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

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}