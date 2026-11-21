using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleLauncher.Core.Models;

/// <summary>
///     Represents a single item in the game list view, supporting data binding for live UI updates.
/// </summary>
public class GameListViewItem : INotifyPropertyChanged
{
    /// <summary>
    ///     Gets the full file path of the game ROM.
    /// </summary>
    public string FilePath { get; init; } = "";

    /// <summary>
    ///     Gets the folder path containing the game ROM.
    /// </summary>
    public string FolderPath { get; init; } = "";

    /// <summary>
    ///     Gets the system name associated with this game.
    /// </summary>
    public string SystemName { get; init; } = "";

    /// <summary>
    ///     Gets or sets whether this game is marked as a favorite.
    /// </summary>
    public bool IsFavorite
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
    ///     Gets the file name of the game ROM (set once at creation).
    /// </summary>
    public string FileName { get; init; } = "";

    /// <summary>
    ///     Gets or sets the machine description from the ROM database.
    /// </summary>
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
    ///     Gets or sets the number of times this game has been played.
    /// </summary>
    public string TimesPlayed
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    } = "0";

    /// <summary>
    ///     Gets or sets the total play time formatted as a string.
    /// </summary>
    public string PlayTime
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    } = "0m 0s";

    /// <summary>
    ///     Gets or sets whether this game has RetroAchievements data.
    /// </summary>
    public bool HasAchievements
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
    ///     Gets or sets the number of achievements earned for this game.
    /// </summary>
    public int AchievementsEarned
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
    ///     Gets or sets the total number of achievements available for this game.
    /// </summary>
    public int AchievementsTotal
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
    ///     Occurs when a property value changes, supporting data binding updates.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged" /> for the changed property.</summary>
    /// <param name="propertyName">Name of the changed property; filled by the compiler when omitted.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}