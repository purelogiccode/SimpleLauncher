using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;

namespace SimpleLauncher.Core.Models;

/// <summary>
///     Represents a user's favorite game entry, persisted via MessagePack.
/// </summary>
[MessagePackObject]
public class Favorite : INotifyPropertyChanged
{
    /// <summary>
    ///     Gets the full file path of the favorite game.
    /// </summary>
    [Key(0)]
    public required string FileName { get; init; }

    /// <summary>
    ///     Gets or sets the name of the system this game belongs to.
    ///     Settable so favorites can be migrated in place when a system is renamed.
    /// </summary>
    [Key(1)]
    public required string SystemName { get; set; }

    /// <summary>
    ///     Gets the machine description from the ROM database.
    /// </summary>
    [IgnoreMember]
    public string? MachineDescription { get; init; }

    /// <summary>
    ///     Gets the path to the cover image for this game.
    /// </summary>
    [IgnoreMember]
    public string? CoverImage
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets the default emulator name for this favorite.
    /// </summary>
    [IgnoreMember]
    public string? DefaultEmulator
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal)) return;

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