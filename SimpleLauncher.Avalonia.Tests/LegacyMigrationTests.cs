using System.Collections.ObjectModel;
using MessagePack;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SettingsDatabase;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Core.Services.WpfServices;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     End-to-end tests for the one-time legacy migration into settings.dat.
///     Everything runs inside an isolated temp folder (database + legacy files),
///     so the real AppData folder and the real application directory are never touched.
/// </summary>
public sealed class LegacyMigrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _legacyFolder;
    private readonly string _dbPath;

    public LegacyMigrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SLMigTest_" + Guid.NewGuid().ToString("N"));
        _legacyFolder = Path.Combine(_tempRoot, "legacy");
        Directory.CreateDirectory(_legacyFolder);
        _dbPath = Path.Combine(_tempRoot, "settings.dat");
        AvaloniaLegacyMigrator.ResetForTests();
    }

    [Fact]
    public void Migrate_AllLegacyFiles_ImportsAndShelvesAsBak()
    {
        WriteLegacyFavorites(("game1.zip", "NES"), ("game2.iso", "PS1"));
        WriteLegacyHistory();
        WriteLegacySettingsXml();
        WriteLegacySystemXml();

        var logger = TestDependencies.Logger();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Migrated, result.Status);
        Assert.Equal(2, result.Favorites);
        Assert.Equal(1, result.HistoryEntries);
        Assert.Equal(2, result.Systems);

        // Database contents.
        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));

        var favorites = UnifiedSettingsDatabase.LoadFavorites(_dbPath);
        Assert.Equal(2, favorites.Count);
        Assert.Contains(favorites,
            f => string.Equals(f.FileName, "game1.zip", StringComparison.Ordinal) &&
                 string.Equals(f.SystemName, "NES", StringComparison.Ordinal));

        var history = UnifiedSettingsDatabase.LoadPlayHistory(_dbPath);
        var entry = Assert.Single(history);
        Assert.Equal("game2.iso", entry.FileName);
        Assert.Equal(5, entry.TimesPlayed);

        var app = UnifiedSettingsDatabase.LoadAppSettings(_dbPath);
        Assert.Equal("fr", app["Language"]);
        Assert.Equal("300", app["ThumbnailSize"]);

        var emulators = UnifiedSettingsDatabase.LoadEmulatorConfigs(_dbPath);
        Assert.Contains("vulkan", emulators["Mame"], StringComparison.Ordinal);

        var systems = UnifiedSettingsDatabase.LoadSystems(_dbPath);
        Assert.Equal(2, systems.Count);

        // Legacy originals are gone; .bak backups remain.
        foreach (var name in new[] { "favorites.dat", "playhistory.dat", "settings.xml", "system.xml" })
        {
            Assert.False(File.Exists(Path.Combine(_legacyFolder, name)));
            Assert.True(File.Exists(Path.Combine(_legacyFolder, name + ".bak")));
        }

        // Second call is a no-op.
        AvaloniaLegacyMigrator.ResetForTests();
        var second = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.AlreadyCurrent, second.Status);
    }

    [Fact]
    public void Migrate_NoLegacyFiles_CreatesFreshDatabase()
    {
        var logger = TestDependencies.Logger();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.FreshCreated, result.Status);
        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));
        Assert.Empty(UnifiedSettingsDatabase.LoadFavorites(_dbPath));
        Assert.Empty(UnifiedSettingsDatabase.LoadSystems(_dbPath));
        Assert.NotEmpty(UnifiedSettingsDatabase.LoadAppSettings(_dbPath));
    }

    [Fact]
    public void Migrate_CorruptFavorites_ImportsTheRest()
    {
        File.WriteAllBytes(Path.Combine(_legacyFolder, "favorites.dat"), "corrupt-bytes"u8.ToArray());
        WriteLegacyHistory();

        var logger = TestDependencies.Logger();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Migrated, result.Status);
        Assert.Equal(0, result.Favorites);
        Assert.Equal(1, result.HistoryEntries);
        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));
    }

    [Fact]
    public void Merge_ValidDatabasePlusLegacyFiles_UpsertsAndShelvesWithoutDuplicates()
    {
        // First run: fresh database (no legacy files present yet).
        var logger = TestDependencies.Logger();
        var first = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.FreshCreated, first.Status);

        // Seed the database with data that conflicts and data that does not.
        UnifiedSettingsDatabase.SaveFavorites(
        [
            new FavoriteRecord("GAME1.ZIP", "NES"),
            new FavoriteRecord("game9.zip", "Genesis")
        ], _dbPath);
        UnifiedSettingsDatabase.SavePlayHistory(
        [
            new PlayHistoryRecord("game2.iso", "PS1", 9, 9000, "2026-01-01", "10:00:00"),
            new PlayHistoryRecord("game8.iso", "MegaDrive", 2, 100, "2026-02-02", "11:00:00")
        ], _dbPath);
        UnifiedSettingsDatabase.SaveAppSettings(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Language"] = "de",
            ["DbOnlyKey"] = "keepme"
        }, _dbPath);
        UnifiedSettingsDatabase.SaveAllEmulatorConfigs(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Mame"] = """{"Video":"opengl"}""",
            ["RetroArch"] = "{}"
        }, _dbPath);
        UnifiedSettingsDatabase.SaveSystemPlayTimes([new SystemPlayTimeRecord("NES", 111)], _dbPath);
        UnifiedSettingsDatabase.SaveAllSystems(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NES"] = SystemConfigStore.Serialize(BuildSystemConfig("NES", "old-image-folder")),
                ["GBA"] = SystemConfigStore.Serialize(BuildSystemConfig("GBA", "old-image-folder"))
            }, _dbPath);

        // Legacy files reappear (restored folder): GAME1.zip conflicts with GAME1.ZIP
        // (same system) case-insensitively; game9.zip is the same file name in a different
        // system and must survive as a second entry; game2.iso conflicts with the database
        // history entry.
        WriteLegacyFavorites(("GAME1.zip", "NES"), ("game3.bin", "Saturn"), ("game9.zip", "NES"));
        WriteLegacyHistory();
        WriteLegacySettingsXml();
        WriteLegacySystemXml();

        // The once-per-process guard must be reset: a fresh launch would now find
        // the legacy files again and merge them into the existing database.
        AvaloniaLegacyMigrator.ResetForTests();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Merged, result.Status);
        Assert.Equal(2, result.Favorites);
        Assert.Equal(0, result.HistoryEntries);
        Assert.Equal(1, result.Systems);

        // Favorites: appended, no duplicate rows for the same (file, system) pair; the
        // legacy value won on conflict, and the same file name in another system survived.
        var favorites = UnifiedSettingsDatabase.LoadFavorites(_dbPath);
        Assert.Equal(4, favorites.Count);
        var game1 = favorites.Single(f => f.FileName.Equals("GAME1.zip", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("NES", game1.SystemName);
        Assert.Contains(favorites, f => string.Equals(f.FileName, "game9.zip", StringComparison.Ordinal));
        Assert.Contains(favorites, f => string.Equals(f.FileName, "game9.zip", StringComparison.Ordinal) &&
                                        string.Equals(f.SystemName, "NES", StringComparison.Ordinal));
        Assert.Contains(favorites, f => string.Equals(f.FileName, "game3.bin", StringComparison.Ordinal));

        // History: legacy overwrote game2.iso; the database-only entry survived.
        var history = UnifiedSettingsDatabase.LoadPlayHistory(_dbPath);
        Assert.Equal(2, history.Count);
        var game2 = history.Single(h => h.FileName.Equals("game2.iso", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(5, game2.TimesPlayed);
        Assert.Contains(history, h => string.Equals(h.FileName, "game8.iso", StringComparison.Ordinal));

        // App settings: legacy values win; database-only keys survive.
        var app = UnifiedSettingsDatabase.LoadAppSettings(_dbPath);
        Assert.Equal("fr", app["Language"]);
        Assert.Equal("300", app["ThumbnailSize"]);
        Assert.Equal("keepme", app["DbOnlyKey"]);

        // Emulators: legacy config overwrote Mame; RetroArch survived. The legacy
        // export also seeds defaults for every other known emulator, so the table
        // grows — assert on the merged rows, not the total count.
        var emulators = UnifiedSettingsDatabase.LoadEmulatorConfigs(_dbPath);
        Assert.Contains("vulkan", emulators["Mame"], StringComparison.Ordinal);
        Assert.True(emulators.ContainsKey("RetroArch"));

        // System play times: legacy value overwrote NES.
        var playTimes = UnifiedSettingsDatabase.LoadSystemPlayTimes(_dbPath);
        Assert.Equal(3600,
            playTimes.Single(p => p.SystemName.Equals("NES", StringComparison.OrdinalIgnoreCase)).PlayTimeSeconds);

        // Systems: legacy NES overwrote, GBA survived, SNES appended.
        var systems = UnifiedSettingsDatabase.LoadSystems(_dbPath);
        Assert.Equal(3, systems.Count);
        var nes = SystemConfigStore.Deserialize("NES", systems["NES"]);
        Assert.Equal("C:\\images\\nes", nes?.SystemImageFolder);
        Assert.True(systems.ContainsKey("GBA"));
        Assert.True(systems.ContainsKey("SNES"));

        // Legacy originals are gone; .bak backups remain.
        foreach (var name in new[] { "favorites.dat", "playhistory.dat", "settings.xml", "system.xml" })
        {
            Assert.False(File.Exists(Path.Combine(_legacyFolder, name)));
            Assert.True(File.Exists(Path.Combine(_legacyFolder, name + ".bak")));
        }

        // The once-per-process guard is set: a second call is a no-op.
        AvaloniaLegacyMigrator.ResetForTests();
        var second = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.AlreadyCurrent, second.Status);
    }

    [Fact]
    public void Merge_ValidDatabaseNoLegacyFiles_AlreadyCurrent()
    {
        var logger = TestDependencies.Logger();
        var first = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.FreshCreated, first.Status);

        AvaloniaLegacyMigrator.ResetForTests();
        var second = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.AlreadyCurrent, second.Status);
        Assert.False(File.Exists(Path.Combine(_legacyFolder, "settings.dat")));
    }

    private static SystemManagerConfig BuildSystemConfig(string systemName, string imageFolder)
    {
        return new SystemManagerConfig
        {
            SystemName = systemName,
            SystemFolders = [$"C:\\roms\\{systemName.ToLowerInvariant()}"],
            SystemImageFolder = imageFolder,
            FileFormatsToSearch = ["zip"],
            FileFormatsToLaunch = ["zip"],
            Emulators = []
        };
    }


    private void WriteLegacyFavorites(params (string File, string System)[] entries)
    {
        var manager = new FavoritesManager
        {
            FavoriteList = new ObservableCollection<Favorite>(
                entries.Select(e => new Favorite { FileName = e.File, SystemName = e.System }))
        };
        File.WriteAllBytes(
            Path.Combine(_legacyFolder, "favorites.dat"),
            MessagePackSerializer.Serialize(manager));
    }

    private void WriteLegacyHistory()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList =
            [
                new PlayHistoryItem
                {
                    FileName = "game2.iso",
                    SystemName = "PS1",
                    TimesPlayed = 5,
                    TotalPlayTime = 7200,
                    LastPlayDate = "2026-09-10",
                    LastPlayTime = "21:30:00"
                }
            ]
        };
        File.WriteAllBytes(
            Path.Combine(_legacyFolder, "playhistory.dat"),
            MessagePackSerializer.Serialize(manager));
    }

    private void WriteLegacySettingsXml()
    {
        File.WriteAllText(Path.Combine(_legacyFolder, "settings.xml"), """
            <Settings>
              <Application>
                <ThumbnailSize>300</ThumbnailSize>
                <Language>fr</Language>
                <BaseTheme>Light</BaseTheme>
              </Application>
              <Mame>
                <Video>vulkan</Video>
              </Mame>
              <SystemPlayTimes>
                <SystemPlayTime>
                  <SystemName>NES</SystemName>
                  <PlayTime>3600</PlayTime>
                </SystemPlayTime>
              </SystemPlayTimes>
            </Settings>
            """);
    }

    private void WriteLegacySystemXml()
    {
        File.WriteAllText(Path.Combine(_legacyFolder, "system.xml"), """
            <SystemConfigs>
              <SystemConfig>
                <SystemName>NES</SystemName>
                <SystemFolders>
                  <SystemFolder>C:\roms\nes</SystemFolder>
                </SystemFolders>
                <SystemImageFolder>C:\images\nes</SystemImageFolder>
                <FileFormatsToSearch>
                  <FormatToSearch>zip</FormatToSearch>
                </FileFormatsToSearch>
                <FileFormatsToLaunch>
                  <FormatToLaunch>zip</FormatToLaunch>
                </FileFormatsToLaunch>
                <Emulators>
                  <Emulator>
                    <EmulatorName>Mesen</EmulatorName>
                    <EmulatorLocation>C:\emu\mesen.exe</EmulatorLocation>
                    <EmulatorParameters></EmulatorParameters>
                    <ReceiveANotificationOnEmulatorError>true</ReceiveANotificationOnEmulatorError>
                  </Emulator>
                </Emulators>
              </SystemConfig>
              <SystemConfig>
                <SystemName>SNES</SystemName>
                <SystemFolder>D:\roms\snes</SystemFolder>
                <SystemImageFolder>D:\images\snes</SystemImageFolder>
                <FileFormatsToSearch>zip,sfc</FileFormatsToSearch>
                <FileFormatsToLaunch>zip</FileFormatsToLaunch>
              </SystemConfig>
            </SystemConfigs>
            """);
    }

    public void Dispose()
    {
        AvaloniaLegacyMigrator.ResetForTests();
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
