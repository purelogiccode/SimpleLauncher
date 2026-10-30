using System.Collections.ObjectModel;
using MessagePack;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="SimpleLauncher.Services.Favorites.FavoritesManager" /> MessagePack serialization and basic
///     properties.
/// </summary>
public class FavoritesManagerTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FavoritesManagerTests" /> class, setting up a temporary test
    ///     directory.
    /// </summary>
    public FavoritesManagerTests()
    {
        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_FavTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the temporary test directory and restores the original service provider.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        ServiceProviderMock.Restore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Verifies that FavoritesManager can be serialized and deserialized via MessagePack preserving all data.
    /// </summary>
    [Fact]
    public void FavoritesManagerCanBeSerializedAndDeserialized()
    {
        var manager = new FavoritesManager
        {
            FavoriteList =
            [
                new Favorite { FileName = "C:\\roms\\game1.zip", SystemName = "Arcade" },
                new Favorite { FileName = "C:\\roms\\game2.nes", SystemName = "NES" }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.FavoriteList.Count);
        Assert.Equal("C:\\roms\\game1.zip", deserialized.FavoriteList[0].FileName);
        Assert.Equal("Arcade", deserialized.FavoriteList[0].SystemName);
        Assert.Equal("C:\\roms\\game2.nes", deserialized.FavoriteList[1].FileName);
        Assert.Equal("NES", deserialized.FavoriteList[1].SystemName);
    }

    /// <summary>
    ///     Verifies that an empty FavoritesManager serializes and deserializes correctly.
    /// </summary>
    [Fact]
    public void FavoritesManagerEmptyListSerializesCorrectly()
    {
        var manager = new FavoritesManager
        {
            FavoriteList = [],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.FavoriteList);
    }

    /// <summary>
    ///     Verifies that the default Version value is 1.
    /// </summary>
    [Fact]
    public void FavoritesManagerDefaultVersionIsOne()
    {
        var manager = new FavoritesManager();
        Assert.Equal(1, manager.Version);
    }

    /// <summary>
    ///     Verifies that the default FavoriteList is empty.
    /// </summary>
    [Fact]
    public void FavoritesManagerDefaultListIsEmpty()
    {
        var manager = new FavoritesManager();
        Assert.Empty(manager.FavoriteList);
    }

    /// <summary>
    ///     Verifies that deserializing corrupted bytes throws a MessagePackSerializationException.
    /// </summary>
    [Fact]
    public void FavoritesManagerCorruptedBytesThrowsException()
    {
        var corruptedBytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        Assert.Throws<MessagePackSerializationException>(() =>
            MessagePackSerializer.Deserialize<FavoritesManager>(corruptedBytes));
    }

    /// <summary>
    ///     Verifies that a Favorite with optional properties serializes the required fields correctly.
    /// </summary>
    [Fact]
    public void FavoriteWithOptionalPropertiesSerializesCorrectly()
    {
        var manager = new FavoritesManager
        {
            FavoriteList =
            [
                new Favorite
                {
                    FileName = "game.zip",
                    SystemName = "Arcade",
                    MachineDescription = "Pac-Man",
                    CoverImage = "cover.png"
                }
            ]
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        // Note: MachineDescription and CoverImage are [IgnoreMember] so won't survive serialization
        Assert.Equal("game.zip", deserialized.FavoriteList[0].FileName);
        Assert.Equal("Arcade", deserialized.FavoriteList[0].SystemName);
    }

    /// <summary>
    ///     Verifies that a FavoritesManager with duplicate entries serializes and deserializes correctly.
    /// </summary>
    [Fact]
    public void FavoritesManagerWithDuplicateEntriesSerializesCorrectly()
    {
        var manager = new FavoritesManager
        {
            FavoriteList =
            [
                new Favorite { FileName = "game.zip", SystemName = "Arcade" },
                new Favorite { FileName = "game.zip", SystemName = "Arcade" }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.Equal(2, deserialized.FavoriteList.Count);
    }

    /// <summary>
    ///     Verifies that special characters in file names are preserved through serialization.
    /// </summary>
    [Fact]
    public void FavoritesManagerWithSpecialCharactersInFileName()
    {
        var manager = new FavoritesManager
        {
            FavoriteList =
            [
                new Favorite { FileName = "C:\\roms\\[BIOS] PlayStation.zip", SystemName = "PS1" },
                new Favorite { FileName = "C:\\roms\\game (v1.0).zip", SystemName = "NES" },
                new Favorite { FileName = "C:\\roms\\game & friends.zip", SystemName = "SNES" }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.Equal(3, deserialized.FavoriteList.Count);
        Assert.Equal("C:\\roms\\[BIOS] PlayStation.zip", deserialized.FavoriteList[0].FileName);
        Assert.Equal("C:\\roms\\game (v1.0).zip", deserialized.FavoriteList[1].FileName);
        Assert.Equal("C:\\roms\\game & friends.zip", deserialized.FavoriteList[2].FileName);
    }

    /// <summary>
    ///     Verifies that Unicode characters in file names are preserved through serialization.
    /// </summary>
    [Fact]
    public void FavoritesManagerWithUnicodeCharacters()
    {
        var manager = new FavoritesManager
        {
            FavoriteList =
            [
                new Favorite { FileName = "C:\\roms\\ポケモン.zip", SystemName = "GBA" },
                new Favorite { FileName = "C:\\roms\\Jürgen's Game.zip", SystemName = "NES" }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.Equal(2, deserialized.FavoriteList.Count);
        Assert.Equal("C:\\roms\\ポケモン.zip", deserialized.FavoriteList[0].FileName);
        Assert.Equal("C:\\roms\\Jürgen's Game.zip", deserialized.FavoriteList[1].FileName);
    }

    /// <summary>
    ///     Verifies that the Favorite model's CoverImage property defaults to null.
    /// </summary>
    [Fact]
    public void FavoriteModelDefaultCoverImageIsNull()
    {
        var favorite = new Favorite { FileName = "game.zip", SystemName = "NES" };
        Assert.Null(favorite.CoverImage);
    }

    /// <summary>
    ///     Verifies that the Favorite model's MachineDescription property defaults to null.
    /// </summary>
    [Fact]
    public void FavoriteModelDefaultMachineDescriptionIsNull()
    {
        var favorite = new Favorite { FileName = "game.zip", SystemName = "NES" };
        Assert.Null(favorite.MachineDescription);
    }

    /// <summary>
    ///     Verifies that the Favorite model's DefaultEmulator property defaults to null.
    /// </summary>
    [Fact]
    public void FavoriteModelDefaultDefaultEmulatorIsNull()
    {
        var favorite = new Favorite { FileName = "game.zip", SystemName = "NES" };
        Assert.Null(favorite.DefaultEmulator);
    }

    /// <summary>
    ///     Verifies that the Favorite model's CoverImage property can be set and retrieved.
    /// </summary>
    [Fact]
    public void FavoriteModelCoverImageCanBeSet()
    {
        var favorite = new Favorite { FileName = "game.zip", SystemName = "NES", CoverImage = "cover.png" };
        Assert.Equal("cover.png", favorite.CoverImage);
    }

    /// <summary>
    ///     Verifies that the Favorite model's DefaultEmulator property can be set and retrieved.
    /// </summary>
    [Fact]
    public void FavoriteModelDefaultEmulatorCanBeSet()
    {
        var favorite = new Favorite { FileName = "game.zip", SystemName = "NES", DefaultEmulator = "RetroArch" };
        Assert.Equal("RetroArch", favorite.DefaultEmulator);
    }

    /// <summary>
    ///     Verifies that the Version property is preserved after serialization and deserialization.
    /// </summary>
    [Fact]
    public void FavoritesManagerVersionPreservedAfterSerialization()
    {
        var manager = new FavoritesManager
        {
            FavoriteList = [],
            Version = 42
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.Equal(42, deserialized.Version);
    }

    /// <summary>
    ///     Verifies that a large list of 1000 favorites serializes and deserializes correctly.
    /// </summary>
    [Fact]
    public void FavoritesManagerLargeListSerializesCorrectly()
    {
        var favorites = Enumerable.Range(1, 1000)
            .Select(static i => new Favorite { FileName = $"game{i}.zip", SystemName = "NES" })
            .ToList();

        var manager = new FavoritesManager
        {
            FavoriteList = new ObservableCollection<Favorite>(favorites),
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.Equal(1000, deserialized.FavoriteList.Count);
        Assert.Equal("game1.zip", deserialized.FavoriteList[0].FileName);
        Assert.Equal("game1000.zip", deserialized.FavoriteList[999].FileName);
    }

    /// <summary>
    ///     Verifies that long file paths are preserved through serialization.
    /// </summary>
    [Fact]
    public void FavoritesManagerLongPathsSerializesCorrectly()
    {
        var longPath = "C:\\" + string.Join("\\", Enumerable.Repeat("verylongfoldername", 10)) + "\\game.zip";
        var manager = new FavoritesManager
        {
            FavoriteList =
            [
                new Favorite { FileName = longPath, SystemName = "NES" }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);

        Assert.Equal(longPath, deserialized.FavoriteList[0].FileName);
    }

    /// <summary>
    ///     Verifies that renaming a system tolerates legacy entries with a null system name
    ///     instead of throwing a NullReferenceException (bug #68900).
    /// </summary>
    [Fact]
    public async Task RenameSystemAsyncIgnoresNullSystemNames()
    {
        var manager = new FavoritesManager
        {
            FavoriteList =
            [
                new Favorite { FileName = "game1.zip", SystemName = null! },
                null!
            ]
        };

        var exception = await Record.ExceptionAsync(() => manager.RenameSystemAsync("Arcade", "MAME"));

        Assert.Null(exception);
    }
}