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
///     Verifies the Favorites section ViewModel loads stored favorites into rows
///     (the file names resolve against the system folders, matching the WPF flow).
///     The database is redirected to an isolated temp file (exclusive collection) and
///     seeded from the fixture XML so results never depend on real user data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class FavoritesSectionViewModelTests : IDisposable
{
    private readonly IConfiguration _config;
    private readonly string _dataFolder;
    private readonly Mock<ILogger> _logger = new();
    private readonly MainViewModel _mainViewModel;
    private readonly Mock<IMameDataService> _mameData = new();
    private readonly Mock<IMessageBoxLibraryService> _messageBox = new();
    private readonly string _romsFolder;
    private readonly string _systemXmlPath;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_FavSection_{Guid.NewGuid():N}");

    public FavoritesSectionViewModelTests()
    {
        _romsFolder = Path.Combine(_tempRoot, "roms");
        Directory.CreateDirectory(_romsFolder);
        WriteGameFile("asteroids.zip");
        WriteGameFile("abyss.zip");

        _dataFolder = Path.Combine(_tempRoot, "data");
        Directory.CreateDirectory(_dataFolder);

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
                                               </FileFormatsToSearch>
                                               <FileFormatsToLaunch>
                                                 <FormatToLaunch>.zip</FormatToLaunch>
                                               </FileFormatsToLaunch>
                                               <Emulators>
                                                 <Emulator>
                                                   <EmulatorName>Stella</EmulatorName>
                                                   <EmulatorPath>stella.exe</EmulatorPath>
                                                 </Emulator>
                                               </Emulators>
                                             </SystemConfig>
                                           </SystemConfigs>
                                           """);

        _config = TestEnvironment.ConfigurationFromJson(
            $$"""{"SystemXmlPath": "{{_systemXmlPath.Replace("\\", @"\\")}}"}""");

        UnifiedTestDatabase.RedirectToTempDb(_tempRoot);

        _mameData.Setup(m => m.Lookup).Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "abyss", "Abyss" }
        });

        var settings = TestDependencies.Settings(_config, _messageBox);
        var systemManager = new SystemManagerService(_config);
        UnifiedTestDatabase.SeedSystemsFromXml(systemManager, _systemXmlPath);
        _mainViewModel = new MainViewModel(
            new FavoritesManager(),
            new PlayHistoryManager(),
            systemManager,
            CreateLauncher(systemManager, settings),
            new Mock<IFindCoverImageService>().Object,
            new Stats(TestDependencies.HttpFactory(new HttpClient()).Object, _config, _logger.Object),
            settings,
            new AvaloniaPaginationService(TestDependencies.ResourceProvider().Object),
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), _logger.Object),
            new Mock<IRetroAchievementsHashScanner>().Object,
            new Mock<IRetroAchievementsHashStore>().Object,
            new RetroAchievementsManager(),
            _messageBox.Object,
            _mameData.Object,
            new AvaloniaGameFilterService(new Mock<IFindCoverImageService>().Object, settings, _mameData.Object),
            new AvaloniaLoadingOverlayService(new PlaySoundEffects(settings, _logger.Object)),
            new LocalizationService());
        _mainViewModel.ConfigurePagination(1_000_000);
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
            // ignored
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

    private static FavoritesManager CreateManager(params Favorite[] favorites)
    {
        var manager = new FavoritesManager();
        foreach (var f in favorites) manager.FavoriteList.Add(f);

        return manager;
    }

    private FavoritesSectionViewModel CreateViewModel(FavoritesManager manager)
    {
        var settings = TestDependencies.Settings(_config, _messageBox);
        return new FavoritesSectionViewModel(
            manager,
            new SystemManagerService(_config),
            new Mock<IFindCoverImageService>().Object,
            _mameData.Object,
            TestDependencies.PlaySound(settings),
            _messageBox.Object,
            _config,
            _mainViewModel,
            _logger.Object,
            new LocalizationService());
    }

    [Fact]
    public async Task LoadFavoritesAsync_ResolvesStoredFileNamesIntoRows()
    {
        var manager = CreateManager(
            new Favorite { FileName = "asteroids.zip", SystemName = "Test System" },
            new Favorite { FileName = "abyss.zip", SystemName = "Test System" });

        var vm = CreateViewModel(manager);
        await vm.LoadFavoritesAsync();

        Assert.Equal(2, vm.Favorites.Count);
        var row = vm.Favorites[0];
        Assert.Equal(Path.Combine(_romsFolder, "asteroids.zip"), row.FilePath);
        Assert.Equal("Test System", row.SystemName);
        Assert.Equal("Stella", row.DefaultEmulator);
    }

    [Fact]
    public async Task LoadFavoritesAsync_SkipsCorruptEntryWithoutBlankingList()
    {
        var manager = CreateManager(
            new Favorite { FileName = "asteroids.zip", SystemName = "Test System" });

        // A corrupt favorite (missing required fields) must not wipe the healthy one.
        var corrupt = new Favorite { FileName = "", SystemName = "" };
        manager.FavoriteList.Add(corrupt);

        var vm = CreateViewModel(manager);
        await vm.LoadFavoritesAsync();

        Assert.Single(vm.Favorites);
    }

    [Fact]
    public async Task RemoveFavoritesAsync_RemovesAndPersists()
    {
        var manager = CreateManager(
            new Favorite { FileName = "asteroids.zip", SystemName = "Test System" },
            new Favorite { FileName = "abyss.zip", SystemName = "Test System" });

        var vm = CreateViewModel(manager);
        await vm.LoadFavoritesAsync();

        var target =
            vm.Favorites.FirstOrDefault(r => r.FilePath.EndsWith("asteroids.zip", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(target);
        await vm.RemoveFavoritesAsync([target]);

        Assert.Single(vm.Favorites);
        Assert.Single(manager.FavoriteList);
    }
}