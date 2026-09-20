using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.PlayHistory;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests the unified-database round-trips of the WPF <see cref="FavoritesManager" /> and
///     <see cref="PlayHistoryManager" /> against a redirected temp <c>settings.dat</c>,
///     so the real user data is never touched.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class FavoritesPlayHistoryDatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ILogger _logErrors = new NoOpLogger();
    private readonly string _testDirectory;

    public FavoritesPlayHistoryDatabaseTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_FavHistDbTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _dbPath = UnifiedTestDatabase.RedirectToTempDb(_testDirectory);
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

    [Fact]
    public async Task FavoritesManager_LoadsAndSavesThroughDatabase()
    {
        UnifiedSettingsDatabase.SaveFavorites(
        [
            new FavoriteRecord("game1.zip", "NES")
        ], _dbPath);

        var manager = FavoritesManager.LoadFavorites(_logErrors);
        var first = Assert.Single(manager.FavoriteList);
        Assert.Equal("game1.zip", first.FileName);
        Assert.Equal("NES", first.SystemName);

        manager.FavoriteList.Add(new Favorite { FileName = "game2.iso", SystemName = "PS1" });
        await manager.SaveFavoritesAsync();

        var stored = UnifiedSettingsDatabase.LoadFavorites(_dbPath);
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, static f =>
            string.Equals(f.FileName, "game2.iso", StringComparison.Ordinal) &&
            string.Equals(f.SystemName, "PS1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlayHistoryManager_LoadsAndSavesThroughDatabase()
    {
        UnifiedSettingsDatabase.SavePlayHistory(
        [
            new PlayHistoryRecord("game1.iso", "PS1", 2, 120, "2026-09-10", "10:00:00")
        ], _dbPath);

        var manager = PlayHistoryManager.LoadPlayHistory(_logErrors);
        var first = Assert.Single(manager.PlayHistoryList);
        Assert.Equal("game1.iso", first.FileName);
        Assert.Equal(2, first.TimesPlayed);

        first.TimesPlayed = 3;
        first.TotalPlayTime = 300;
        await manager.SavePlayHistoryAsync();

        var stored = UnifiedSettingsDatabase.LoadPlayHistory(_dbPath);
        var item = Assert.Single(stored);
        Assert.Equal(3, item.TimesPlayed);
        Assert.Equal(300, item.TotalPlayTime);
    }
}