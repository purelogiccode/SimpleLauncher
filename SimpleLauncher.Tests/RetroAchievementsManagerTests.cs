using MessagePack;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsManager" /> covering loading from .dat files,
///     hash-based game lookups, and graceful handling of missing or corrupted data.
/// </summary>
public class RetroAchievementsManagerTests : IDisposable
{
    private readonly string _datFilePath;
    private readonly ILogger _logErrors = new NoOpLogger();


    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsManagerTests" /> class,
    ///     installing the service provider mock and setting up the .dat file path.
    /// </summary>
    public RetroAchievementsManagerTests()
    {
        ServiceProviderMock.Install();
        _datFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RetroAchievements.dat");
    }

    /// <summary>
    ///     Cleans up the test .dat file and restores the service provider mock.
    /// </summary>
    public void Dispose()
    {
        // Clean up the dat file if it was created during a test
        if (File.Exists(_datFilePath)) File.Delete(_datFilePath);

        ServiceProviderMock.Restore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Verifies that LoadRetroAchievement returns a manager with games when given a valid .dat file.
    /// </summary>
    [Fact]
    public void LoadRetroAchievementValidDatFileReturnsManagerWithGames()
    {
        var games = new List<RaGameInfo>
        {
            new()
            {
                Id = 1,
                Title = "Super Mario Bros.",
                ConsoleId = 7,
                ConsoleName = "NES",
                ImageIcon = "image.png",
                NumAchievements = 10,
                Points = 100,
                DateModified = "2024-01-01",
                Hashes = ["abc123"]
            }
        };

        var bytes = MessagePackSerializer.Serialize(games);
        File.WriteAllBytes(_datFilePath, bytes);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);

        Assert.NotNull(manager);
        Assert.Single(manager.AllGames);
        Assert.Equal("Super Mario Bros.", manager.AllGames[0].Title);
    }

    /// <summary>
    ///     Verifies that LoadRetroAchievement returns an empty manager when the .dat file contains corrupted data.
    /// </summary>
    [Fact]
    public void LoadRetroAchievementCorruptedMessagePackReturnsEmptyManager()
    {
        File.WriteAllBytes(_datFilePath, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);

        Assert.NotNull(manager);
        Assert.Empty(manager.AllGames);
    }

    /// <summary>
    ///     Verifies that LoadRetroAchievement returns an empty manager when the .dat file contains XML content.
    /// </summary>
    [Fact]
    public void LoadRetroAchievementXmlContentReturnsEmptyManager()
    {
        File.WriteAllText(_datFilePath, "<xml><item>test</item></xml>");

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);

        Assert.NotNull(manager);
        Assert.Empty(manager.AllGames);
    }

    /// <summary>
    ///     Verifies that LoadRetroAchievement returns an empty manager when the .dat file is empty.
    /// </summary>
    [Fact]
    public void LoadRetroAchievementEmptyFileReturnsEmptyManager()
    {
        File.WriteAllBytes(_datFilePath, []);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);

        Assert.NotNull(manager);
        Assert.Empty(manager.AllGames);
    }

    /// <summary>
    ///     Verifies that LoadRetroAchievement returns an empty manager when the .dat file does not exist.
    /// </summary>
    [Fact]
    public void LoadRetroAchievementMissingFileReturnsEmptyManager()
    {
        // Ensure the file does not exist
        if (File.Exists(_datFilePath)) File.Delete(_datFilePath);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);

        Assert.NotNull(manager);
        Assert.Empty(manager.AllGames);
    }

    /// <summary>
    ///     Verifies that GetGameInfoByHash returns the correct game info for a known hash.
    /// </summary>
    [Fact]
    public void GetGameInfoByHashKnownHashReturnsGameInfo()
    {
        var games = new List<RaGameInfo>
        {
            new()
            {
                Id = 1,
                Title = "Super Mario Bros.",
                ConsoleId = 7,
                ConsoleName = "NES",
                ImageIcon = "image.png",
                NumAchievements = 10,
                Points = 100,
                DateModified = "2024-01-01",
                Hashes = ["abc123", "def456"]
            }
        };

        var bytes = MessagePackSerializer.Serialize(games);
        File.WriteAllBytes(_datFilePath, bytes);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);
        var result = manager.GetGameInfoByHash("abc123");

        Assert.NotNull(result);
        Assert.Equal("Super Mario Bros.", result.Title);
    }

    /// <summary>
    ///     Verifies that GetGameInfoByHash returns null for an unknown hash.
    /// </summary>
    [Fact]
    public void GetGameInfoByHashUnknownHashReturnsNull()
    {
        var games = new List<RaGameInfo>
        {
            new()
            {
                Id = 1,
                Title = "Super Mario Bros.",
                ConsoleId = 7,
                ConsoleName = "NES",
                ImageIcon = "image.png",
                NumAchievements = 10,
                Points = 100,
                DateModified = "2024-01-01",
                Hashes = ["abc123"]
            }
        };

        var bytes = MessagePackSerializer.Serialize(games);
        File.WriteAllBytes(_datFilePath, bytes);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);
        var result = manager.GetGameInfoByHash("unknown_hash");

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that GetGameInfoByHash returns null for an empty string hash.
    /// </summary>
    [Fact]
    public void GetGameInfoByHashEmptyStringReturnsNull()
    {
        var games = new List<RaGameInfo>
        {
            new()
            {
                Id = 1,
                Title = "Super Mario Bros.",
                ConsoleId = 7,
                ConsoleName = "NES",
                ImageIcon = "image.png",
                NumAchievements = 10,
                Points = 100,
                DateModified = "2024-01-01",
                Hashes = ["abc123"]
            }
        };

        var bytes = MessagePackSerializer.Serialize(games);
        File.WriteAllBytes(_datFilePath, bytes);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);
        var result = manager.GetGameInfoByHash("");

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that GetGameInfoByHash returns the first matching game when multiple games share a hash.
    /// </summary>
    [Fact]
    public void GetGameInfoByHashOverlappingHashesReturnsFirstMatch()
    {
        var games = new List<RaGameInfo>
        {
            new()
            {
                Id = 1,
                Title = "First Game",
                ConsoleId = 7,
                ConsoleName = "NES",
                ImageIcon = "image1.png",
                NumAchievements = 5,
                Points = 50,
                DateModified = "2024-01-01",
                Hashes = ["shared_hash", "unique1"]
            },
            new()
            {
                Id = 2,
                Title = "Second Game",
                ConsoleId = 7,
                ConsoleName = "NES",
                ImageIcon = "image2.png",
                NumAchievements = 10,
                Points = 100,
                DateModified = "2024-02-01",
                Hashes = ["shared_hash", "unique2"]
            }
        };

        var bytes = MessagePackSerializer.Serialize(games);
        File.WriteAllBytes(_datFilePath, bytes);

        var manager = RetroAchievementsManager.LoadRetroAchievement(_logErrors, Log.Logger, _datFilePath);
        var result = manager.GetGameInfoByHash("shared_hash");

        Assert.NotNull(result);
        Assert.Equal("First Game", result.Title);
    }
}