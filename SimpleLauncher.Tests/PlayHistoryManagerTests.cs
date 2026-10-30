using System.Collections.ObjectModel;
using MessagePack;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="Services.PlayHistory.PlayHistoryManager" /> covering MessagePack
///     serialization/deserialization, default values, corruption handling, and item ordering.
/// </summary>
public class PlayHistoryManagerTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PlayHistoryManagerTests" /> class,
    ///     installing the service provider mock and creating a temporary test directory.
    /// </summary>
    public PlayHistoryManagerTests()
    {
        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_HistoryTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the temporary test directory and restores the service provider mock.
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
    ///     Verifies that PlayHistoryManager can be serialized and deserialized via MessagePack
    ///     while preserving all item properties.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerCanBeSerializedAndDeserialized()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList =
            [
                new PlayHistoryItem
                {
                    FileName = "C:\\roms\\game.zip",
                    SystemName = "Arcade",
                    TotalPlayTime = 3600,
                    TimesPlayed = 5,
                    LastPlayDate = "2024-01-15",
                    LastPlayTime = "14:30:00"
                }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized.PlayHistoryList);
        Assert.Equal("C:\\roms\\game.zip", deserialized.PlayHistoryList[0].FileName);
        Assert.Equal("Arcade", deserialized.PlayHistoryList[0].SystemName);
        Assert.Equal(3600, deserialized.PlayHistoryList[0].TotalPlayTime);
        Assert.Equal(5, deserialized.PlayHistoryList[0].TimesPlayed);
        Assert.Equal("2024-01-15", deserialized.PlayHistoryList[0].LastPlayDate);
        Assert.Equal("14:30:00", deserialized.PlayHistoryList[0].LastPlayTime);
    }

    /// <summary>
    ///     Verifies that an empty PlayHistoryManager serializes and deserializes correctly.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerEmptyListSerializesCorrectly()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList = [],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.PlayHistoryList);
    }

    /// <summary>
    ///     Verifies that the default Version of a new PlayHistoryManager is 1.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerDefaultVersionIsOne()
    {
        var manager = new PlayHistoryManager();
        Assert.Equal(1, manager.Version);
    }

    /// <summary>
    ///     Verifies that the default PlayHistoryList of a new PlayHistoryManager is empty.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerDefaultListIsEmpty()
    {
        var manager = new PlayHistoryManager();
        Assert.Empty(manager.PlayHistoryList);
    }

    /// <summary>
    ///     Verifies that deserializing corrupted MessagePack bytes throws a MessagePackSerializationException.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerCorruptedBytesThrowsException()
    {
        var corruptedBytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        Assert.Throws<MessagePackSerializationException>(() =>
            MessagePackSerializer.Deserialize<PlayHistoryManager>(corruptedBytes));
    }

    /// <summary>
    ///     Verifies that multiple play history items preserve their order after serialization and deserialization.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerMultipleItemsPreservesOrder()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList =
            [
                new PlayHistoryItem
                    { FileName = "game1.zip", SystemName = "Arcade", TotalPlayTime = 100, TimesPlayed = 1 },
                new PlayHistoryItem
                    { FileName = "game2.nes", SystemName = "NES", TotalPlayTime = 200, TimesPlayed = 2 },
                new PlayHistoryItem
                    { FileName = "game3.smc", SystemName = "SNES", TotalPlayTime = 300, TimesPlayed = 3 }
            ]
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.Equal(3, deserialized.PlayHistoryList.Count);
        Assert.Equal("game1.zip", deserialized.PlayHistoryList[0].FileName);
        Assert.Equal("game2.nes", deserialized.PlayHistoryList[1].FileName);
        Assert.Equal("game3.smc", deserialized.PlayHistoryList[2].FileName);
    }

    /// <summary>
    ///     Verifies that ISO date and time format strings are preserved after serialization.
    /// </summary>
    [Fact]
    public void PlayHistoryItemIsoDateFormatIsPreserved()
    {
        var item = new PlayHistoryItem
        {
            FileName = "game.zip",
            SystemName = "Arcade",
            LastPlayDate = "2024-12-25",
            LastPlayTime = "23:59:59"
        };

        var manager = new PlayHistoryManager
        {
            PlayHistoryList = [item]
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.Equal("2024-12-25", deserialized.PlayHistoryList[0].LastPlayDate);
        Assert.Equal("23:59:59", deserialized.PlayHistoryList[0].LastPlayTime);
    }

    /// <summary>
    ///     Verifies that large play time values serialize and deserialize correctly.
    /// </summary>
    [Fact]
    public void PlayHistoryItemLargePlayTimeSerializesCorrectly()
    {
        var item = new PlayHistoryItem
        {
            FileName = "game.zip",
            SystemName = "Arcade",
            TotalPlayTime = 86400, // 24 hours
            TimesPlayed = 100
        };

        var manager = new PlayHistoryManager
        {
            PlayHistoryList = [item]
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.Equal(86400, deserialized.PlayHistoryList[0].TotalPlayTime);
        Assert.Equal(100, deserialized.PlayHistoryList[0].TimesPlayed);
    }

    /// <summary>
    ///     Verifies that DisplayName returns just the file name when given only a file name without a path.
    /// </summary>
    [Fact]
    public void PlayHistoryItemDisplayNameHandlesJustFileName()
    {
        var item = new PlayHistoryItem { FileName = "game.zip", SystemName = "NES" };
        Assert.Equal("game.zip", item.DisplayName);
    }

    /// <summary>
    ///     Verifies that FormattedPlayTime displays correctly for seconds-only durations.
    /// </summary>
    [Fact]
    public void PlayHistoryItemFormattedPlayTimeSecondsOnly()
    {
        var item = new PlayHistoryItem { TotalPlayTime = 45 };
        Assert.Equal("0m 45s", item.FormattedPlayTime);
    }

    /// <summary>
    ///     Verifies that FormattedPlayTime displays minutes and seconds correctly.
    /// </summary>
    [Fact]
    public void PlayHistoryItemFormattedPlayTimeMinutesAndSeconds()
    {
        var item = new PlayHistoryItem { TotalPlayTime = 150 }; // 2m 30s
        Assert.Equal("2m 30s", item.FormattedPlayTime);
    }

    /// <summary>
    ///     Verifies that FormattedPlayTime displays correctly for large play time values.
    /// </summary>
    [Fact]
    public void PlayHistoryItemFormattedPlayTimeLargeValue()
    {
        var item = new PlayHistoryItem { TotalPlayTime = 86400 }; // 24h
        Assert.Equal("24h 0m 0s", item.FormattedPlayTime);
    }

    /// <summary>
    ///     Verifies that special characters in file names are preserved through serialization.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerWithSpecialCharactersInFileName()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList =
            [
                new PlayHistoryItem
                {
                    FileName = "C:\\roms\\[BIOS] Test.zip", SystemName = "PS1", TotalPlayTime = 100, TimesPlayed = 1
                },
                new PlayHistoryItem
                    { FileName = "C:\\roms\\game (v2.0).zip", SystemName = "NES", TotalPlayTime = 200, TimesPlayed = 2 }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.Equal(2, deserialized.PlayHistoryList.Count);
        Assert.Equal("C:\\roms\\[BIOS] Test.zip", deserialized.PlayHistoryList[0].FileName);
        Assert.Equal("C:\\roms\\game (v2.0).zip", deserialized.PlayHistoryList[1].FileName);
    }

    /// <summary>
    ///     Verifies that Unicode characters in file names are preserved through serialization.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerWithUnicodeCharacters()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList =
            [
                new PlayHistoryItem
                    { FileName = "C:\\roms\\ポケモン.zip", SystemName = "GBA", TotalPlayTime = 300, TimesPlayed = 5 }
            ],
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.Equal("C:\\roms\\ポケモン.zip", deserialized.PlayHistoryList[0].FileName);
    }

    /// <summary>
    ///     Verifies that the Version property is preserved through serialization.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerVersionPreservedAfterSerialization()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList = [],
            Version = 99
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.Equal(99, deserialized.Version);
    }

    /// <summary>
    ///     Verifies that a large list of 1000 play history items serializes and deserializes correctly.
    /// </summary>
    [Fact]
    public void PlayHistoryManagerLargeListSerializesCorrectly()
    {
        var items = Enumerable.Range(1, 1000)
            .Select(static i => new PlayHistoryItem
            {
                FileName = $"game{i}.zip",
                SystemName = "NES",
                TotalPlayTime = i * 10,
                TimesPlayed = i,
                LastPlayDate = "2024-01-01",
                LastPlayTime = "12:00:00"
            })
            .ToList();

        var manager = new PlayHistoryManager
        {
            PlayHistoryList = new ObservableCollection<PlayHistoryItem>(items),
            Version = 1
        };

        var bytes = MessagePackSerializer.Serialize(manager);
        var deserialized = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);

        Assert.Equal(1000, deserialized.PlayHistoryList.Count);
        Assert.Equal(10, deserialized.PlayHistoryList[0].TotalPlayTime);
        Assert.Equal(10000, deserialized.PlayHistoryList[999].TotalPlayTime);
    }

    /// <summary>
    ///     Verifies that a PlayHistoryItem with zero TimesPlayed and TotalPlayTime stores those values correctly.
    /// </summary>
    [Fact]
    public void PlayHistoryItemZeroTimesPlayed()
    {
        var item = new PlayHistoryItem
            { FileName = "game.zip", SystemName = "NES", TotalPlayTime = 0, TimesPlayed = 0 };
        Assert.Equal(0, item.TimesPlayed);
        Assert.Equal(0, item.TotalPlayTime);
    }

    /// <summary>
    ///     Verifies that empty date and time strings are preserved correctly.
    /// </summary>
    [Fact]
    public void PlayHistoryItemEmptyDateAndTime()
    {
        var item = new PlayHistoryItem
            { FileName = "game.zip", SystemName = "NES", LastPlayDate = "", LastPlayTime = "" };
        Assert.Equal("", item.LastPlayDate);
        Assert.Equal("", item.LastPlayTime);
    }

    /// <summary>
    ///     Verifies that PropertyChanged fires for TotalPlayTime after deserialization.
    /// </summary>
    [Fact]
    public void PlayHistoryItemPropertyChangedOnTotalPlayTime()
    {
        var item = new PlayHistoryItem { FileName = "game.zip", SystemName = "NES", TotalPlayTime = 100 };
        var propertyChanged = false;
        item.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(PlayHistoryItem.TotalPlayTime), StringComparison.Ordinal))
                propertyChanged = true;
        };

        item.TotalPlayTime = 200;
        Assert.True(propertyChanged);
    }

    /// <summary>
    ///     Verifies that renaming a system tolerates legacy entries with a null system name
    ///     instead of throwing a NullReferenceException (bug #68900).
    /// </summary>
    [Fact]
    public async Task RenameSystemAsyncIgnoresNullSystemNames()
    {
        var manager = new PlayHistoryManager
        {
            PlayHistoryList =
            [
                new PlayHistoryItem { FileName = "game.zip", SystemName = null! },
                null!
            ]
        };

        var exception = await Record.ExceptionAsync(() => manager.RenameSystemAsync("Arcade", "MAME"));

        Assert.Null(exception);
    }
}