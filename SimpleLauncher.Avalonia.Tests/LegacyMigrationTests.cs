using System.Collections.ObjectModel;
using System.Globalization;
using MessagePack;
using Microsoft.Data.Sqlite;
using Moq;
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

        // Legacy originals are gone; timestamped .bak backups remain (never overwriting).
        foreach (var name in new[] { "favorites.dat", "playhistory.dat", "settings.xml", "system.xml" })
        {
            Assert.False(File.Exists(Path.Combine(_legacyFolder, name)));
            Assert.Single(Directory.GetFiles(_legacyFolder, name + ".*.bak"));
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

        // The corrupt file stays in place for older versions; the rest still migrates.
        Assert.True(File.Exists(Path.Combine(_legacyFolder, "favorites.dat")));
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
        // database value won on conflict, and the same file name in another system survived.
        var favorites = UnifiedSettingsDatabase.LoadFavorites(_dbPath);
        Assert.Equal(4, favorites.Count);
        var game1 = favorites.Single(f => f.FileName.Equals("GAME1.zip", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("NES", game1.SystemName);
        Assert.Contains(favorites, f => string.Equals(f.FileName, "game9.zip", StringComparison.Ordinal));
        Assert.Contains(favorites, f => string.Equals(f.FileName, "game9.zip", StringComparison.Ordinal) &&
                                        string.Equals(f.SystemName, "NES", StringComparison.Ordinal));
        Assert.Contains(favorites, f => string.Equals(f.FileName, "game3.bin", StringComparison.Ordinal));

        // History: the database kept game2.iso (database wins); the database-only entry survived.
        var history = UnifiedSettingsDatabase.LoadPlayHistory(_dbPath);
        Assert.Equal(2, history.Count);
        var game2 = history.Single(h => h.FileName.Equals("game2.iso", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(9, game2.TimesPlayed);
        Assert.Contains(history, h => string.Equals(h.FileName, "game8.iso", StringComparison.Ordinal));

        // App settings: database values win; legacy values only fill absent keys; database-only keys survive.
        var app = UnifiedSettingsDatabase.LoadAppSettings(_dbPath);
        Assert.Equal("de", app["Language"]);
        Assert.Equal("300", app["ThumbnailSize"]);
        Assert.Equal("keepme", app["DbOnlyKey"]);

        // Emulators: database config kept Mame; RetroArch survived. The legacy
        // export also seeds defaults for every other known emulator, so the table
        // grows — assert on the merged rows, not the total count.
        var emulators = UnifiedSettingsDatabase.LoadEmulatorConfigs(_dbPath);
        Assert.Contains("opengl", emulators["Mame"], StringComparison.Ordinal);
        Assert.True(emulators.ContainsKey("RetroArch"));

        // System play times: database value kept NES.
        var playTimes = UnifiedSettingsDatabase.LoadSystemPlayTimes(_dbPath);
        Assert.Equal(111,
            playTimes.Single(p => p.SystemName.Equals("NES", StringComparison.OrdinalIgnoreCase)).PlayTimeSeconds);

        // Systems: database NES kept, GBA survived, SNES appended.
        var systems = UnifiedSettingsDatabase.LoadSystems(_dbPath);
        Assert.Equal(3, systems.Count);
        var nes = SystemConfigStore.Deserialize("NES", systems["NES"]);
        Assert.Equal("old-image-folder", nes?.SystemImageFolder);
        Assert.True(systems.ContainsKey("GBA"));
        Assert.True(systems.ContainsKey("SNES"));

        // Legacy originals are gone; timestamped .bak backups remain (never overwriting).
        foreach (var name in new[] { "favorites.dat", "playhistory.dat", "settings.xml", "system.xml" })
        {
            Assert.False(File.Exists(Path.Combine(_legacyFolder, name)));
            Assert.Single(Directory.GetFiles(_legacyFolder, name + ".*.bak"));
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

    [Fact]
    public void Merge_OnlyFavoritesLegacyFile_KeepsDatabaseAppAndEmulatorSettings()
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

        UnifiedSettingsDatabase.SaveAppSettings(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Language"] = "de",
            ["ThumbnailSize"] = "500"
        }, _dbPath);
        UnifiedSettingsDatabase.SaveAllEmulatorConfigs(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mame"] = """{"Video":"bgfx"}"""
        }, _dbPath);

        // Only favorites.dat reappears — no settings.xml. The defaults carried by the freshly
        // created SettingsManagerService must never be merged over the database (regression:
        // that wiped theme, language, RA credentials and every emulator configuration).
        WriteLegacyFavorites(("game1.zip", "NES"));

        AvaloniaLegacyMigrator.ResetForTests();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Merged, result.Status);

        var app = UnifiedSettingsDatabase.LoadAppSettings(_dbPath);
        Assert.Equal("de", app["Language"]);
        Assert.Equal("500", app["ThumbnailSize"]);
        Assert.Contains("bgfx", UnifiedSettingsDatabase.LoadEmulatorConfigs(_dbPath)["Mame"], StringComparison.Ordinal);
        Assert.Single(UnifiedSettingsDatabase.LoadFavorites(_dbPath));
    }

    [Fact]
    public void Merge_CorruptSettingsXml_KeepsDatabaseAppSettings()
    {
        var logger = TestDependencies.Logger();
        var first = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.FreshCreated, first.Status);

        UnifiedSettingsDatabase.SaveAppSettings(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Language"] = "de"
        }, _dbPath);

        // A corrupt settings.xml cannot be read: the untouched service defaults must not
        // be merged over the database (Loaded=false), and the corrupt file is left in
        // place (not shelved) so older versions can still read it.
        File.WriteAllText(Path.Combine(_legacyFolder, "settings.xml"), "<Settings><Application>");
        WriteLegacyFavorites(("game1.zip", "NES"));

        AvaloniaLegacyMigrator.ResetForTests();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Merged, result.Status);
        Assert.Equal("de", UnifiedSettingsDatabase.LoadAppSettings(_dbPath)["Language"]);
        Assert.True(File.Exists(Path.Combine(_legacyFolder, "settings.xml")));
        Assert.Empty(Directory.GetFiles(_legacyFolder, "settings.xml.*.bak"));
    }

    [Fact]
    public void Migrate_DuplicateLegacyEntries_AreDeduplicatedAndSucceed()
    {
        // Same favorite twice (system differs only by case), two history entries with the
        // same file name, and two system blocks whose names differ only by case. The database
        // collapses each duplicate; the migration must verify against the canonicalized
        // counts instead of failing (and retrying) forever.
        WriteLegacyFavorites(("game1.zip", "NES"), ("GAME1.ZIP", "nes"), ("game2.zip", "SNES"));
        WriteLegacyHistoryDuplicates();
        WriteLegacySystemXmlWithCaseDuplicateNames();

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
        Assert.Equal(1, result.Systems);

        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));
        Assert.Equal(2, UnifiedSettingsDatabase.LoadFavorites(_dbPath).Count);
        Assert.Single(UnifiedSettingsDatabase.LoadPlayHistory(_dbPath));
        Assert.Single(UnifiedSettingsDatabase.LoadSystems(_dbPath));
    }

    [Fact]
    public void Migrate_DualLocationFiles_MergesBothAndShelvesBoth()
    {
        // The same legacy file in the portable folder and AppData (stale shadow copy):
        // both copies are drained, so the older one cannot resurrect on the next launch.
        var portableFolder = Path.Combine(_tempRoot, "portable");
        var appDataFolder = Path.Combine(_tempRoot, "appdata");
        Directory.CreateDirectory(portableFolder);
        Directory.CreateDirectory(appDataFolder);

        WriteLegacyFavorites(portableFolder, ("new-game.zip", "NES"));
        WriteLegacyFavorites(appDataFolder, ("old-game.zip", "NES"));
        File.SetLastWriteTimeUtc(
            Path.Combine(portableFolder, "favorites.dat"), DateTime.UtcNow);
        File.SetLastWriteTimeUtc(
            Path.Combine(appDataFolder, "favorites.dat"), DateTime.UtcNow.AddHours(-1));

        var logger = TestDependencies.Logger();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            null,
            portableFolder,
            appDataFolder);

        Assert.Equal(MigrationStatus.Migrated, result.Status);
        Assert.Equal(2, result.Favorites);
        Assert.Equal(2, UnifiedSettingsDatabase.LoadFavorites(_dbPath).Count);

        // Both copies are gone; each folder keeps its own timestamped backup.
        Assert.False(File.Exists(Path.Combine(portableFolder, "favorites.dat")));
        Assert.False(File.Exists(Path.Combine(appDataFolder, "favorites.dat")));
        Assert.Single(Directory.GetFiles(portableFolder, "favorites.dat.*.bak"));
        Assert.Single(Directory.GetFiles(appDataFolder, "favorites.dat.*.bak"));

        // The next launch is a no-op: nothing reappears, nothing is reverted.
        AvaloniaLegacyMigrator.ResetForTests();
        var second = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            null,
            portableFolder,
            appDataFolder);
        Assert.Equal(MigrationStatus.AlreadyCurrent, second.Status);
        Assert.Equal(2, UnifiedSettingsDatabase.LoadFavorites(_dbPath).Count);
    }

    [Fact]
    public void Merge_DefaultsOnlySettingsXml_KeepsDatabaseAppSettings()
    {
        var logger = TestDependencies.Logger();
        var first = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.FreshCreated, first.Status);

        UnifiedSettingsDatabase.SaveAppSettings(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Language"] = "de"
        }, _dbPath);

        // A settings.xml holding only default values (recreated by an older version
        // after a downgrade) must not revert the database to defaults.
        File.WriteAllText(Path.Combine(_legacyFolder, "settings.xml"), """
                                                                       <Settings>
                                                                         <Application>
                                                                           <Language>en</Language>
                                                                         </Application>
                                                                       </Settings>
                                                                       """);
        WriteLegacyFavorites(("game1.zip", "NES"));

        AvaloniaLegacyMigrator.ResetForTests();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Merged, result.Status);
        Assert.Equal("de", UnifiedSettingsDatabase.LoadAppSettings(_dbPath)["Language"]);
        Assert.Single(UnifiedSettingsDatabase.LoadFavorites(_dbPath));
    }

    [Fact]
    public void Migrate_NewerSchemaDatabase_FailsAndLeavesEverythingUntouched()
    {
        // Simulate a database written by a newer app build: migration must fail safe —
        // no quarantine, no overwrite, legacy files untouched for the next launch.
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);
        StampSchemaVersion(_dbPath, UnifiedSettingsDatabase.CurrentSchemaVersion + 99);
        WriteLegacyFavorites(("game1.zip", "NES"));

        var logger = TestDependencies.Logger();
        var result = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Failed, result.Status);
        Assert.True(File.Exists(Path.Combine(_legacyFolder, "favorites.dat")));
        Assert.False(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));
        Assert.Empty(Directory.GetFiles(_tempRoot, "*.corrupt.*.bak", SearchOption.AllDirectories));

        // The database file itself is untouched (still carries the newer version).
        Assert.Equal(UnifiedSettingsDatabase.CurrentSchemaVersion + 99, ReadSchemaVersion(_dbPath));

        // A retry in a new "launch" still fails safe instead of destroying data.
        AvaloniaLegacyMigrator.ResetForTests();
        var retry = AvaloniaLegacyMigrator.EnsureMigrated(
            TestEnvironment.ConfigurationFromJson("{\"SystemXmlPath\": \"system.xml\"}"),
            logger.Object,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.Failed, retry.Status);
        Assert.True(File.Exists(Path.Combine(_legacyFolder, "favorites.dat")));
        Assert.Equal(UnifiedSettingsDatabase.CurrentSchemaVersion + 99, ReadSchemaVersion(_dbPath));

        // The downgrade is an expected environment condition: it is logged at Information
        // (never Error) so the bug-report API never sees it. The specific catch logs with
        // the database path as a property value, which binds to Serilog's generic overload.
        logger.Verify(l => l.Information<string>(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.AtLeastOnce);
        logger.Verify(l => l.Error<string>(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
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
        WriteLegacyFavorites(_legacyFolder, entries);
    }

    private static void WriteLegacyFavorites(string folder, params (string File, string System)[] entries)
    {
        var manager = new FavoritesManager
        {
            FavoriteList = new ObservableCollection<Favorite>(
                entries.Select(e => new Favorite { FileName = e.File, SystemName = e.System }))
        };
        File.WriteAllBytes(
            Path.Combine(folder, "favorites.dat"),
            MessagePackSerializer.Serialize(manager));
    }

    private static void StampSchemaVersion(string dbPath, int version)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Meta SET Value = $v WHERE Key = 'schema_version';";
        cmd.Parameters.AddWithValue("$v", version.ToString(CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    private static int ReadSchemaVersion(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Meta WHERE Key = 'schema_version';";
        return int.Parse(Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture)!,
            CultureInfo.InvariantCulture);
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

    private void WriteLegacyHistoryDuplicates()
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
                },
                new PlayHistoryItem
                {
                    FileName = "GAME2.ISO",
                    SystemName = "PS1",
                    TimesPlayed = 1,
                    TotalPlayTime = 10,
                    LastPlayDate = "2026-09-11",
                    LastPlayTime = "10:00:00"
                }
            ]
        };
        File.WriteAllBytes(
            Path.Combine(_legacyFolder, "playhistory.dat"),
            MessagePackSerializer.Serialize(manager));
    }

    private void WriteLegacySystemXmlWithCaseDuplicateNames()
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
                                                                       </SystemConfig>
                                                                       <SystemConfig>
                                                                         <SystemName>nes</SystemName>
                                                                         <SystemFolders>
                                                                           <SystemFolder>D:\roms\nes</SystemFolder>
                                                                         </SystemFolders>
                                                                         <SystemImageFolder>D:\images\nes</SystemImageFolder>
                                                                         <FileFormatsToSearch>
                                                                           <FormatToSearch>nes</FormatToSearch>
                                                                         </FileFormatsToSearch>
                                                                         <FileFormatsToLaunch>
                                                                           <FormatToLaunch>nes</FormatToLaunch>
                                                                         </FileFormatsToLaunch>
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