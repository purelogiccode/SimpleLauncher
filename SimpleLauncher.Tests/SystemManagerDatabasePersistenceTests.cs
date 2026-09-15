using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Services.SystemManager;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests the unified-database persistence paths of the WPF <see cref="SystemManagerService" />:
///     loading, saving, renaming, deleting and existence checks against a redirected temp
///     <c>settings.dat</c>, so the real user data is never touched.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class SystemManagerDatabasePersistenceTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logErrors = new NoOpLogger();
    private readonly string _testDirectory;

    public SystemManagerDatabasePersistenceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_SystemDbTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        UnifiedTestDatabase.RedirectToTempDb(_testDirectory);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SystemXmlPath"] = Path.Combine(_testDirectory, "system.xml")
            })
            .Build();
    }

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    private static SystemManagerService BuildSystem(string name, string folder, string emulator = "MAME")
    {
        return new SystemManagerService
        {
            SystemName = name,
            SystemFolders = [folder],
            SystemImageFolder = folder + "_images",
            FileFormatsToSearch = [".zip"],
            FileFormatsToLaunch = [".zip"],
            Emulators =
            [
                new Emulator
                {
                    EmulatorName = emulator,
                    EmulatorLocation = @"C:\emu\emu.exe",
                    EmulatorParameters = "%ROM%",
                    ReceiveANotificationOnEmulatorError = true
                }
            ]
        };
    }

    [Fact]
    public void LoadSystemManagers_FromDatabase_ReturnsSeededSystemsSorted()
    {
        UnifiedTestDatabase.SeedSystems(
        [
            BuildSystem("NES", @"C:\roms\nes", "Mesen"),
            BuildSystem("Arcade", @"C:\roms\arcade", "MAME")
        ]);

        var loaded = SystemManagerService.LoadSystemManagers(_configuration, _logErrors);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Arcade", loaded[0].SystemName);
        Assert.Equal("NES", loaded[1].SystemName);
        Assert.Equal(@"C:\roms\nes", loaded[1].SystemFolders[0]);
        Assert.Single(loaded[1].Emulators);
        Assert.Equal("Mesen", loaded[1].Emulators[0].EmulatorName);
    }

    [Fact]
    public void SystemExists_ReadsDatabaseCaseInsensitively()
    {
        UnifiedTestDatabase.SeedSystems([BuildSystem("Arcade", @"C:\roms\arcade")]);

        Assert.True(SystemManagerService.SystemExists("Arcade", _configuration));
        Assert.True(SystemManagerService.SystemExists("aRcAdE", _configuration));
        Assert.False(SystemManagerService.SystemExists("NES", _configuration));
    }

    [Fact]
    public async Task SaveSystemConfigurationAsync_UpsertsIntoDatabase()
    {
        await SystemManagerService.SaveSystemConfigurationAsync(
            BuildSystem("Genesis", @"C:\roms\genesis", "Blastem"), logErrors: _logErrors, configuration: _configuration);

        var loaded = SystemManagerService.LoadSystemManagers(_configuration, _logErrors);
        var genesis = Assert.Single(loaded);
        Assert.Equal("Genesis", genesis.SystemName);
        Assert.Equal("Blastem", genesis.Emulators[0].EmulatorName);

        // Update the same system: no duplicate row.
        var updated = BuildSystem("Genesis", @"D:\roms\genesis", "RetroArch");
        await SystemManagerService.SaveSystemConfigurationAsync(updated, logErrors: _logErrors,
            configuration: _configuration);

        loaded = SystemManagerService.LoadSystemManagers(_configuration, _logErrors);
        genesis = Assert.Single(loaded);
        Assert.Equal(@"D:\roms\genesis", genesis.SystemFolders[0]);
        Assert.Equal("RetroArch", genesis.Emulators[0].EmulatorName);
    }

    [Fact]
    public async Task SaveSystemConfigurationAsync_RenameRemovesOldKey()
    {
        UnifiedTestDatabase.SeedSystems([BuildSystem("Arcade", @"C:\roms\arcade")]);

        await SystemManagerService.SaveSystemConfigurationAsync(
            BuildSystem("ArcadeRenamed", @"C:\roms\arcade"), "Arcade", _logErrors, _configuration);

        var loaded = SystemManagerService.LoadSystemManagers(_configuration, _logErrors);
        var renamed = Assert.Single(loaded);
        Assert.Equal("ArcadeRenamed", renamed.SystemName);
        Assert.False(SystemManagerService.SystemExists("Arcade", _configuration));
    }

    [Fact]
    public async Task DeleteSystemAsync_RemovesFromDatabase()
    {
        UnifiedTestDatabase.SeedSystems(
        [
            BuildSystem("Arcade", @"C:\roms\arcade"),
            BuildSystem("NES", @"C:\roms\nes", "Mesen")
        ]);

        await SystemManagerService.DeleteSystemAsync("Arcade", _logErrors, _configuration);

        var loaded = SystemManagerService.LoadSystemManagers(_configuration, _logErrors);
        var remaining = Assert.Single(loaded);
        Assert.Equal("NES", remaining.SystemName);
    }

    [Fact]
    public async Task DeleteSystemAsync_UnknownSystem_IsNoOp()
    {
        UnifiedTestDatabase.SeedSystems([BuildSystem("Arcade", @"C:\roms\arcade")]);

        await SystemManagerService.DeleteSystemAsync("Does Not Exist", _logErrors, _configuration);

        Assert.Single(SystemManagerService.LoadSystemManagers(_configuration, _logErrors));
        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(UnifiedSettingsDatabase.GetDatabasePath()));
    }
}
