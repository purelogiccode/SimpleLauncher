using SimpleLauncher.Avalonia.Services.PlayHistory;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression tests for the play-history composite key: the unified database keys
///     history by <c>(FileName, SystemName)</c>, so the in-memory manager must match on
///     both parts (WPF parity). A path-only match merged the two systems' counters.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class PlayHistoryManagerCompositeKeyTests : IDisposable
{
    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), $"SL_PlayHistoryKeyTest_{Guid.NewGuid():N}");

    public PlayHistoryManagerCompositeKeyTests()
    {
        Directory.CreateDirectory(_tempRoot);
        UnifiedTestDatabase.RedirectToTempDb(_tempRoot);
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

    [Fact]
    public async Task RecordPlayAsync_SamePathInTwoSystems_KeepsSeparateEntriesAndCounters()
    {
        var manager = new PlayHistoryManager();

        await manager.RecordPlayAsync("/roms/game.zip", "NES", 120);
        await manager.RecordPlayAsync("/roms/game.zip", "SNES", 60);
        await manager.RecordPlayAsync("/roms/game.zip", "NES", 30);

        Assert.Equal(2, manager.PlayHistoryList.Count);

        var lookup = manager.GetHistoryLookup();
        Assert.True(lookup.TryGetValue(("/roms/game.zip", "NES"), out var nes));
        Assert.Equal(2, nes.TimesPlayed);
        Assert.Equal(150, nes.TotalPlayTime);

        Assert.True(lookup.TryGetValue(("/roms/game.zip", "SNES"), out var snes));
        Assert.Equal(1, snes.TimesPlayed);
        Assert.Equal(60, snes.TotalPlayTime);
    }

    [Fact]
    public async Task GetHistoryLookup_IsCaseInsensitiveForBothKeyParts()
    {
        var manager = new PlayHistoryManager();

        await manager.RecordPlayAsync("/roms/GAME.ZIP", "Nes", 10);

        var lookup = manager.GetHistoryLookup();
        Assert.True(lookup.TryGetValue(("/roms/game.zip", "NES"), out var history));
        Assert.Equal(1, history.TimesPlayed);
    }
}