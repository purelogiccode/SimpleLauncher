using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Repro for: add XBOX via Easy Mode -> open the system -> 0 games.
///     Uses isolated temp folders/files only (SystemXmlPath points at a temp file).
///     Skipped when a real unified database exists, because the static save path
///     would otherwise take the database branch against real user data.
///     Joins the non-parallel collection so the database-path override used by
///     sibling test classes can never redirect these legacy-path reads/writes.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public sealed class EasyModeAddSystemReproTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_EasyRepro_{Guid.NewGuid():N}");
    private readonly string _gamesFolder;
    private readonly string _systemXmlPath;

    public EasyModeAddSystemReproTests()
    {
        _gamesFolder = Path.Combine(_tempRoot, "xboxgames");
        Directory.CreateDirectory(_gamesFolder);
        File.WriteAllText(Path.Combine(_gamesFolder, "game1.iso"), "fake");
        File.WriteAllText(Path.Combine(_gamesFolder, "game2.chd"), "fake");
        Directory.CreateDirectory(Path.Combine(_gamesFolder, "sub"));
        File.WriteAllText(Path.Combine(_gamesFolder, "sub", "game3.iso"), "fake");

        _systemXmlPath = Path.Combine(_tempRoot, "system.xml");
        File.WriteAllText(_systemXmlPath, "<SystemConfigs></SystemConfigs>");
    }

    [Fact]
    public async Task EasyModeSave_ThenScan_FindsGames()
    {
        if (Core.Services.UnifiedSettings.UnifiedSettingsDatabase.DatabaseExists())
            return; // Safety: never touch a real database from tests.

        var config = TestEnvironment.ConfigurationFromJson(
            $$"""{"SystemXmlPath": "{{_systemXmlPath.Replace("\\", @"\\")}}" }""");
        var logger = TestDependencies.Logger();

        var easyPreset = new EasyModeSystemConfig
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

        await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(
            easyPreset, _gamesFolder, config, logger.Object, null);

        var manager = new SystemManagerService(config);
        var systems = manager.LoadSystems();
        var xbox = Assert.Single(systems);
        Assert.Equal("Microsoft Xbox", xbox.SystemName);
        Assert.Contains(_gamesFolder, xbox.SystemFolders, StringComparer.Ordinal);

        var orchestrator =
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), logger.Object);
        var files = orchestrator.GetGameFiles(xbox);
        Assert.Equal(3, files.Count);
    }

    [Fact]
    public async Task EasyModeSave_DatabaseBranch_ThenScan_FindsGames()
    {
        // Same flow through the unified-database branch (isolated temp database).
        var dbPath = Path.Combine(_tempRoot, "settings.dat");
        var config = TestEnvironment.ConfigurationFromJson(
            $$"""{"SystemXmlPath": "{{_systemXmlPath.Replace("\\", @"\\")}}" }""");
        var logger = TestDependencies.Logger();

        await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(
            CreateXboxPreset(), _gamesFolder, config, logger.Object, null, dbPath);

        Assert.True(Core.Services.UnifiedSettings.UnifiedSettingsDatabase.IsValidDatabase(dbPath));

        var manager = new SystemManagerService(config);
        var systems = manager.LoadSystemsFromDatabase(dbPath);
        var xbox = Assert.Single(systems);
        Assert.Equal("Microsoft Xbox", xbox.SystemName);
        Assert.Contains(_gamesFolder, xbox.SystemFolders, StringComparer.Ordinal);
        Assert.Equal(["iso", "chd"], xbox.FileFormatsToSearch, StringComparer.Ordinal);

        var orchestrator =
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), logger.Object);
        var files = orchestrator.GetGameFiles(xbox);
        Assert.Equal(3, files.Count);
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

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // ignored
        }
    }
}
