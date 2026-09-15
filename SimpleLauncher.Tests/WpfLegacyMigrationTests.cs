using System.Collections.ObjectModel;
using MessagePack;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Core.Services.WpfServices;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.SettingsDatabase;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     End-to-end tests for the one-time legacy migration into settings.dat.
///     Everything runs inside an isolated temp folder (database + legacy files),
///     so the real AppData folder and the real application directory are never touched.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public sealed class WpfLegacyMigrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _legacyFolder;
    private readonly string _dbPath;

    public WpfLegacyMigrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SLWpfMigTest_" + Guid.NewGuid().ToString("N"));
        _legacyFolder = Path.Combine(_tempRoot, "legacy");
        Directory.CreateDirectory(_legacyFolder);
        _dbPath = Path.Combine(_tempRoot, "settings.dat");
        WpfLegacyMigrator.ResetForTests();
    }

    [Fact]
    public void Migrate_AllLegacyFiles_ImportsAndShelvesAsBak()
    {
        WriteLegacyFavorites(("game1.zip", "NES"), ("game2.iso", "PS1"));
        WriteLegacyHistory();
        WriteLegacySettingsXml();
        WriteLegacySystemXml();

        var logger = new NoOpLogger();
        var result = WpfLegacyMigrator.EnsureMigrated(
            BuildConfiguration(),
            logger,
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
        WpfLegacyMigrator.ResetForTests();
        var second = WpfLegacyMigrator.EnsureMigrated(
            BuildConfiguration(),
            logger,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);
        Assert.Equal(MigrationStatus.AlreadyCurrent, second.Status);
    }

    [Fact]
    public void Migrate_NoLegacyFiles_CreatesFreshDatabase()
    {
        var logger = new NoOpLogger();
        var result = WpfLegacyMigrator.EnsureMigrated(
            BuildConfiguration(),
            logger,
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

        var logger = new NoOpLogger();
        var result = WpfLegacyMigrator.EnsureMigrated(
            BuildConfiguration(),
            logger,
            new WindowsCredentialProtector(),
            _dbPath,
            _legacyFolder);

        Assert.Equal(MigrationStatus.Migrated, result.Status);
        Assert.Equal(0, result.Favorites);
        Assert.Equal(1, result.HistoryEntries);
        Assert.True(UnifiedSettingsDatabase.IsValidDatabase(_dbPath));
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SystemXmlPath"] = "system.xml"
            })
            .Build();
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

    private void WriteLegacySettingsXml()    {
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
                <SystemFolders>
                  <SystemFolder>D:\roms\snes</SystemFolder>
                </SystemFolders>
                <SystemImageFolder>D:\images\snes</SystemImageFolder>
                <FileFormatsToSearch>
                  <FormatToSearch>zip</FormatToSearch>
                  <FormatToSearch>sfc</FormatToSearch>
                </FileFormatsToSearch>
                <FileFormatsToLaunch>
                  <FormatToLaunch>zip</FormatToLaunch>
                </FileFormatsToLaunch>
                <Emulators>
                  <Emulator>
                    <EmulatorName>SNES9x</EmulatorName>
                    <EmulatorLocation>D:\emu\snes9x.exe</EmulatorLocation>
                    <EmulatorParameters>%ROM%</EmulatorParameters>
                  </Emulator>
                </Emulators>
              </SystemConfig>
            </SystemConfigs>
            """);
    }

    public void Dispose()
    {
        WpfLegacyMigrator.ResetForTests();
        UnifiedTestDatabase.ClearRedirect();
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
