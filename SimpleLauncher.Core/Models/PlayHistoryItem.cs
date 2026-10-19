using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;

namespace SimpleLauncher.Core.Models;

/// <summary>
///     Represents a play history entry for a game, persisted via MessagePack.
/// </summary>
[MessagePackObject]
public class PlayHistoryItem : INotifyPropertyChanged
{
    /// <summary>
    ///     Gets or sets the full file path of the played game.
    /// </summary>
    [Key(0)]
    public string FileName { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the name of the system this game belongs to.
    /// </summary>
    [Key(1)]
    public string SystemName { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the total play time in seconds.
    /// </summary>
    [Key(2)]
    public long TotalPlayTime
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedPlayTime));
            }
        }
    }

    /// <summary>
    ///     Gets or sets the number of times this game has been played.
    /// </summary>
    [Key(3)]
    public int TimesPlayed
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets the date of the last play session in ISO format (yyyy-MM-dd).
    /// </summary>
    [Key(4)]
    public string LastPlayDate
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    } = null!;

    /// <summary>
    ///     Gets or sets the time of the last play session in ISO format (HH:mm:ss).
    /// </summary>
    [Key(5)]
    public string LastPlayTime
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    } = null!;

    /// <summary>
    ///     Gets or sets the machine description from the ROM database.
    /// </summary>
    [IgnoreMember]
    public string MachineDescription
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    } = "";

    /// <summary>
    ///     Gets the file name portion of the game path for display.
    /// </summary>
    [IgnoreMember]
    public string DisplayName => !string.IsNullOrEmpty(FileName) ? Path.GetFileName(FileName) : "";

    /// <summary>
    ///     Gets or sets the path to the cover image for this game.
    /// </summary>
    [IgnoreMember]
    public string CoverImage
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    } = "";

    /// <summary>
    ///     Gets or sets the default emulator name for this game.
    /// </summary>
    [IgnoreMember]
    public string DefaultEmulator
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    } = "";

    /// <summary>
    ///     Gets the total play time formatted as a human-readable string (e.g. "2h 30m 15s").
    /// </summary>
    [IgnoreMember]
    public string FormattedPlayTime
    {
        get
        {
            var timeSpan = TimeSpan.FromSeconds(TotalPlayTime);
            return timeSpan.TotalHours >= 1
                ? $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s"
                : $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
    }

    /// <summary>
    ///     Occurs when a property value changes, supporting data binding updates.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}