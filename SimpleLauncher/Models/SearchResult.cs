using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Models;

/// <summary>
///     Represents a search result from a global game search, including file details, system info, and relevance score.
/// </summary>
public class SearchResult : INotifyPropertyChanged
{
    /// <summary>
    ///     The file name of the game without its extension.
    /// </summary>
    public string FileName { get; init; } = null!;

    /// <summary>
    ///     The file name of the game including its extension.
    /// </summary>
    public string FileNameWithExtension { get; init; } = null!;

    /// <summary>
    ///     The MAME machine name associated with this game, if applicable.
    /// </summary>
    public string MachineName { get; init; } = null!;

    /// <summary>
    ///     The folder name where the game file is located.
    /// </summary>
    public string FolderName { get; init; } = null!;

    /// <summary>
    ///     The full file path to the game file.
    /// </summary>
    public string FilePath { get; init; } = null!;

    /// <summary>
    ///     The name of the system this game belongs to.
    /// </summary>
    public string SystemName { get; init; } = null!;

    /// <summary>
    ///     The emulator configuration used to launch this game.
    /// </summary>
    public Emulator EmulatorManager { get; init; } = null!;

    /// <summary>
    ///     The relevance score of this search result used for ranking.
    /// </summary>
    public int Score
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
    ///     The URL or path to the cover image for this game.
    /// </summary>
    public string CoverImage { get; init; } = null!;

    /// <summary>
    ///     Gets the display name of the default emulator, or a localized fallback message if none is configured.
    /// </summary>
    public string DefaultEmulator
    {
        get
        {
            if (EmulatorManager?.EmulatorName != null)
                return EmulatorManager.EmulatorName;

            // Use Dispatcher to safely access Application.Current resources
            if (Application.Current?.TryFindResource("NoDefaultEmulator") is string localized)
                return localized;

            return "No Default Emulator";
        }
    }

    /// <summary>
    ///     Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged" /> for the changed property.</summary>
    /// <param name="propertyName">Name of the changed property; filled by the compiler when omitted.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}