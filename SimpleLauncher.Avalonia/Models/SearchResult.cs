using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Models;

/// <summary>
///     Represents a search result from the global game search, including file details, system info, and relevance score.
/// </summary>
internal class SearchResult
{
    /// <summary>
    ///     Gets the file name of the game without its extension.
    /// </summary>
    public string FileName { get; init; } = null!;

    /// <summary>
    ///     Gets the file name of the game including its extension.
    /// </summary>
    public string FileNameWithExtension { get; init; } = null!;

    /// <summary>
    ///     Gets the MAME machine name associated with this game, if applicable.
    /// </summary>
    public string MachineName { get; init; } = "";

    /// <summary>
    ///     Gets the folder name where the game file is located.
    /// </summary>
    public string FolderName { get; init; } = "";

    /// <summary>
    ///     Gets the full file path to the game file.
    /// </summary>
    public string FilePath { get; init; } = null!;

    /// <summary>
    ///     Gets the name of the system this game belongs to.
    /// </summary>
    public string SystemName { get; init; } = null!;

    /// <summary>
    ///     Gets the emulator configuration used to launch this game.
    /// </summary>
    public Emulator? EmulatorManager { get; init; }

    /// <summary>
    ///     Gets or sets the relevance score of this search result used for ranking.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    ///     Gets the URL or path to the cover image for this game.
    /// </summary>
    public string CoverImage { get; init; } = "";

    /// <summary>
    ///     Gets the display name of the default emulator, or a fallback message if none is configured.
    /// </summary>
    public string DefaultEmulator => EmulatorManager?.EmulatorName ?? "No Default Emulator";
}