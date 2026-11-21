namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     Display row for a favorite game in the Favorites section table.
/// </summary>
internal class FavoriteRowViewModel
{
    /// <summary>
    ///     Gets the stored favorite file name exactly as persisted in favorites.dat
    ///     (a bare file name, or a legacy full path). Used for manager matching.
    /// </summary>
    public string StoredFileName { get; init; } = null!;

    /// <summary>
    ///     Gets the full file path of the favorite game (stored name resolved against
    ///     the system folders; legacy full-path entries are kept as-is).
    /// </summary>
    public string FilePath { get; init; } = null!;

    /// <summary>
    ///     Gets the file name portion of the path for display.
    /// </summary>
    public string DisplayName => !string.IsNullOrEmpty(FilePath) ? Path.GetFileName(FilePath) : "";

    /// <summary>
    ///     Gets the machine description from the ROM database (empty when unknown).
    /// </summary>
    public string MachineDescription { get; init; } = "";

    /// <summary>
    ///     Gets the default emulator name for this system (fallback message when none is configured).
    /// </summary>
    public string DefaultEmulator { get; init; } = "No Default Emulator";

    /// <summary>
    ///     Gets the name of the system this game belongs to.
    /// </summary>
    public string SystemName { get; init; } = null!;

    /// <summary>
    ///     Gets the path to the cover image for this game.
    /// </summary>
    public string CoverImage { get; init; } = "";
}