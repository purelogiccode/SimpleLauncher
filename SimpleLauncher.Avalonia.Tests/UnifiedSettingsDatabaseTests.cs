using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Core.Services.WpfServices;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the unified SQLite database (settings.dat). Every test uses an isolated
///     temp database file so the real AppData folder is never touched.
/// </summary>
public sealed class UnifiedSettingsDatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly List<string> _cleanup = [];

    public UnifiedSettingsDatabaseTests()
    {
        _dbPath = NewTempDbPath();
    }

    [Fact]
    public void EnsureCreated_CreatesValidDatabase()
    {
        Assert.False(UnifiedSettingsDatabase.DatabaseExists(_dbPath));

        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        Assert.True(UnifiedSettingsDatabase.DatabaseExists(_dbPath));
        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));
    }

    [Fact]
    public void IsValidDatabase_MissingFile_ReturnsFalse()
    {
        Assert.False(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));
    }

    [Fact]
    public void CorruptFile_IsQuarantinedAndRecreated()
    {
        var corrupt = NewTempFile();
        File.WriteAllText(corrupt, "not a sqlite database");

        UnifiedSettingsDatabase.EnsureCreated(corrupt);

        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(corrupt));
        Assert.NotEmpty(Directory.GetFiles(
            Path.GetDirectoryName(corrupt)!,
            Path.GetFileName(corrupt) + ".corrupt.*.bak"));
    }

    [Fact]
    public void AppSettings_RoundTrip()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        UnifiedSettingsDatabase.SaveAppSettings(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Language"] = "fr",
            ["ThumbnailSize"] = "300"
        }, _dbPath);

        var loaded = UnifiedSettingsDatabase.LoadAppSettings(_dbPath);
        Assert.Equal("fr", loaded["Language"]);
        Assert.Equal("300", loaded["ThumbnailSize"]);

        Assert.Equal("fr", UnifiedSettingsDatabase.GetAppSetting("Language", _dbPath));
        Assert.Null(UnifiedSettingsDatabase.GetAppSetting("Missing", _dbPath));

        UnifiedSettingsDatabase.SetAppSetting("Language", "de", _dbPath);
        Assert.Equal("de", UnifiedSettingsDatabase.GetAppSetting("Language", _dbPath));
    }

    [Fact]
    public void EmulatorConfigs_RoundTrip()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        UnifiedSettingsDatabase.SaveAllEmulatorConfigs(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Mame"] = """{"Video":"vulkan"}""",
            ["RetroArch"] = """{"Fullscreen":true}"""
        }, _dbPath);

        var loaded = UnifiedSettingsDatabase.LoadEmulatorConfigs(_dbPath);
        Assert.Equal(2, loaded.Count);
        Assert.Contains("vulkan", loaded["Mame"], StringComparison.Ordinal);

        UnifiedSettingsDatabase.SaveEmulatorConfig("Mame", """{"Video":"bgfx"}""", _dbPath);
        Assert.Contains("bgfx", UnifiedSettingsDatabase.LoadEmulatorConfigs(_dbPath)["Mame"], StringComparison.Ordinal);
    }

    [Fact]
    public void Favorites_RoundTrip_SortedAndCaseInsensitive()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        UnifiedSettingsDatabase.SaveFavorites(
        [
            new FavoriteRecord("zebra.zip", "NES"),
            new FavoriteRecord("apple.iso", "PS1"),
            // Same file name in another system = a distinct favorite.
            new FavoriteRecord("ZEBRA.zip", "SNES"),
            // Case-insensitive duplicate of zebra.zip/NES: first wins, no PK violation.
            new FavoriteRecord("ZEBRA.ZIP", "nes")
        ], _dbPath);

        var loaded = UnifiedSettingsDatabase.LoadFavorites(_dbPath);
        Assert.Equal(3, loaded.Count);
        Assert.Equal("apple.iso", loaded[0].FileName);
        Assert.Contains(loaded, f =>
            string.Equals(f.FileName, "zebra.zip", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.SystemName, "NES", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(loaded, f =>
            string.Equals(f.FileName, "zebra.zip", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.SystemName, "SNES", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Favorites_LegacySingleColumnPrimaryKey_IsUpgradedInPlace()
    {
        // Recreate the pre-composite-key schema with a favorite already stored.
        using (var connection = UnifiedSettingsDatabase.CreateOpenConnection(_dbPath))
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE Meta (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
                INSERT INTO Meta (Key, Value) VALUES ('schema_version', '1');
                CREATE TABLE Favorites (FileName TEXT PRIMARY KEY COLLATE NOCASE, SystemName TEXT NOT NULL DEFAULT '');
                INSERT INTO Favorites (FileName, SystemName) VALUES ('game.zip', 'NES');
                """;
            cmd.ExecuteNonQuery();
        }

        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));

        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        // The upgrade preserved the existing row and allows the same file name in another system.
        UnifiedSettingsDatabase.SaveFavorites(
        [
            new FavoriteRecord("game.zip", "NES"),
            new FavoriteRecord("game.zip", "SNES")
        ], _dbPath);

        var loaded = UnifiedSettingsDatabase.LoadFavorites(_dbPath);
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, f =>
            string.Equals(f.FileName, "game.zip", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.SystemName, "NES", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(loaded, f =>
            string.Equals(f.FileName, "game.zip", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.SystemName, "SNES", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlayHistory_RoundTrip()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        UnifiedSettingsDatabase.SavePlayHistory(
        [
            new PlayHistoryRecord("/roms/game.zip", "NES", 3, 3600, "2026-09-01", "20:15:00")
        ], _dbPath);

        var loaded = UnifiedSettingsDatabase.LoadPlayHistory(_dbPath);
        var item = Assert.Single(loaded);
        Assert.Equal("/roms/game.zip", item.FileName);
        Assert.Equal("NES", item.SystemName);
        Assert.Equal(3, item.TimesPlayed);
        Assert.Equal(3600L, item.TotalPlayTime);
        Assert.Equal("2026-09-01", item.LastPlayDate);
        Assert.Equal("20:15:00", item.LastPlayTime);
    }

    [Fact]
    public void Systems_RoundTrip_Delete_Rename_Exists()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        var config = new SystemManagerConfig
        {
            SystemName = "NES",
            SystemFolders = ["/roms/nes"],
            SystemImageFolder = "/img/nes",
            FileFormatsToSearch = ["zip", "nes"],
            FileFormatsToLaunch = ["zip"],
            ExtractFileBeforeLaunch = true,
            GroupByFolder = true,
            DisableRecursiveSearch = false,
            Emulators =
            [
                new Emulator
                {
                    EmulatorName = "Mesen",
                    EmulatorLocation = "/emu/mesen",
                    EmulatorParameters = "--fullscreen",
                    ReceiveANotificationOnEmulatorError = true,
                    ImagePackDownloadLink = "https://example.com/pack.zip",
                    ImagePackDownloadLink2 = "",
                    ImagePackDownloadLink3 = "",
                    ImagePackDownloadLink4 = "",
                    ImagePackDownloadLink5 = "",
                    ImagePackDownloadExtractPath = "/img"
                }
            ]
        };

        UnifiedSettingsDatabase.SaveAllSystems(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["NES"] = SystemConfigStore.Serialize(config) },
            _dbPath);

        Assert.True(UnifiedSettingsDatabase.SystemExists("nes", _dbPath));
        Assert.False(UnifiedSettingsDatabase.SystemExists("SNES", _dbPath));

        var loaded = UnifiedSettingsDatabase.LoadSystems(_dbPath);
        var back = SystemConfigStore.Deserialize("NES", loaded["NES"]);
        Assert.NotNull(back);
        Assert.Equal("NES", back.SystemName);
        Assert.Equal(["/roms/nes"], back.SystemFolders, StringComparer.Ordinal);
        Assert.Equal("/img/nes", back.SystemImageFolder);
        Assert.Equal(["zip", "nes"], back.FileFormatsToSearch, StringComparer.Ordinal);
        Assert.True(back.ExtractFileBeforeLaunch);
        Assert.True(back.GroupByFolder);
        var emulator = Assert.Single(back.Emulators);
        Assert.Equal("Mesen", emulator.EmulatorName);
        Assert.Equal("/emu/mesen", emulator.EmulatorLocation);
        Assert.Equal("https://example.com/pack.zip", emulator.ImagePackDownloadLink);

        UnifiedSettingsDatabase.RenameSystem("NES", "Nintendo", _dbPath);
        Assert.False(UnifiedSettingsDatabase.SystemExists("NES", _dbPath));
        Assert.True(UnifiedSettingsDatabase.SystemExists("Nintendo", _dbPath));

        UnifiedSettingsDatabase.DeleteSystem("NINTENDO", _dbPath);
        Assert.False(UnifiedSettingsDatabase.SystemExists("Nintendo", _dbPath));
    }

    [Fact]
    public void SystemConfigStore_CorruptJson_ReturnsNull()
    {
        Assert.Null(SystemConfigStore.Deserialize("NES", "this is not json{{{"));
    }

    [Fact]
    public void SystemPlayTimes_RoundTrip()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);

        UnifiedSettingsDatabase.SaveSystemPlayTimes(
            [new SystemPlayTimeRecord("NES", 3600)], _dbPath);

        var loaded = UnifiedSettingsDatabase.LoadSystemPlayTimes(_dbPath);
        var item = Assert.Single(loaded);
        Assert.Equal("NES", item.SystemName);
        Assert.Equal(3600L, item.PlayTimeSeconds);
    }

    [Fact]
    public void SettingsManager_ExportImport_RoundTrip()
    {
        var config = new ConfigurationBuilder().Build();
        var logger = TestDependencies.Logger();
        var protector = new WindowsCredentialProtector();

        var source = new SettingsManagerService(config, logger.Object, protector);
        source.ThumbnailSize = 300;
        source.Language = "fr";
        source.BaseTheme = "Light";
        source.Mame.Video = "vulkan";
        source.RetroArch.Fullscreen = true;
        source.RaUsername = "player1";
        source.RaApiKey = "secret-key";
        source.UpdateSystemPlayTime("NES", TimeSpan.FromHours(1));

        var target = new SettingsManagerService(config, logger.Object, protector);
        target.ImportAppSettings(source.ExportAppSettings());
        target.ImportEmulatorSettings(source.ExportEmulatorSettings());
        target.ImportSystemPlayTimes(source.ExportSystemPlayTimes());

        Assert.Equal(300, target.ThumbnailSize);
        Assert.Equal("fr", target.Language);
        Assert.Equal("Light", target.BaseTheme);
        Assert.Equal("vulkan", target.Mame.Video);
        Assert.True(target.RetroArch.Fullscreen);
        Assert.Equal("player1", target.RaUsername);
        Assert.Equal("secret-key", target.RaApiKey);
        var playTime = Assert.Single(target.SystemPlayTimes);
        Assert.Equal("NES", playTime.SystemName);
        Assert.Equal(3600L, playTime.PlayTimeSeconds);
    }

    [Fact]
    public void SettingsManager_SaveLoadDatabase_RoundTrip()
    {
        var config = new ConfigurationBuilder().Build();
        var logger = TestDependencies.Logger();
        var protector = new WindowsCredentialProtector();

        var source = new SettingsManagerService(config, logger.Object, protector, null, useUnifiedDatabase: true);
        source.ThumbnailSize = 500;
        source.Language = "de";
        source.Mame.Video = "bgfx";
        source.UpdateSystemPlayTime("SNES", TimeSpan.FromMinutes(30));
        source.SaveToDatabase(_dbPath);

        var target = new SettingsManagerService(config, logger.Object, protector, null, useUnifiedDatabase: true);
        target.LoadFromDatabase(_dbPath);

        Assert.Equal(500, target.ThumbnailSize);
        Assert.Equal("de", target.Language);
        Assert.Equal("bgfx", target.Mame.Video);
        var playTime = Assert.Single(target.SystemPlayTimes);
        Assert.Equal("SNES", playTime.SystemName);
        Assert.Equal(1800L, playTime.PlayTimeSeconds);
    }

    /// <summary>
    ///     Bug #67097: a settings.dat that carries the read-only attribute made SQLite
    ///     fail with 'attempt to write a readonly database'. Opening the database must
    ///     clear the attribute so saves keep working.
    /// </summary>
    [Fact]
    public void ReadOnlyDatabaseFile_IsMadeWritableAndSaveSucceeds()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);
        File.SetAttributes(_dbPath, File.GetAttributes(_dbPath) | FileAttributes.ReadOnly);

        try
        {
            UnifiedSettingsDatabase.SaveAppSettings(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Language"] = "fr" }, _dbPath);

            Assert.Equal("fr", UnifiedSettingsDatabase.GetAppSetting("Language", _dbPath));
            Assert.False(File.GetAttributes(_dbPath).HasFlag(FileAttributes.ReadOnly));
        }
        finally
        {
            if (File.Exists(_dbPath))
                File.SetAttributes(_dbPath, File.GetAttributes(_dbPath) & ~FileAttributes.ReadOnly);
        }
    }

    [Fact]
    public void BackupDatabase_CapturesLatestWritesAndIsIndependent()
    {
        UnifiedSettingsDatabase.EnsureCreated(_dbPath);
        UnifiedSettingsDatabase.SaveAppSettings(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Language"] = "fr" }, _dbPath);

        var backupPath = NewTempDbPath();
        UnifiedSettingsDatabase.BackupDatabase(backupPath, _dbPath);

        // The snapshot carries the latest write (including WAL content).
        Assert.Equal("fr", UnifiedSettingsDatabase.GetAppSetting("Language", backupPath));

        // Later writes to the source do not leak into the snapshot.
        UnifiedSettingsDatabase.SetAppSetting("Language", "de", _dbPath);
        Assert.Equal("fr", UnifiedSettingsDatabase.GetAppSetting("Language", backupPath));
        Assert.Equal("de", UnifiedSettingsDatabase.GetAppSetting("Language", _dbPath));
    }

    private string NewTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "SLDbTest_" + Guid.NewGuid().ToString("N") + ".dat");
        _cleanup.Add(path);
        return path;
    }

    private string NewTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "SLDbTest_" + Guid.NewGuid().ToString("N") + ".dat");
        _cleanup.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _cleanup)
        {
            foreach (var candidate in new[]
                     {
                         path, path + "-wal", path + "-shm", path + "-journal"
                     })
            {
                try
                {
                    if (File.Exists(candidate))
                        File.Delete(candidate);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (dir is not null)
                {
                    foreach (var bak in Directory.GetFiles(dir, Path.GetFileName(path) + ".corrupt.*.bak"))
                        File.Delete(bak);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
