namespace SimpleLauncher.Core.Models;

/// <summary>
///     Represents a file entry displayed in the DOSBox file selection dialog.
/// </summary>
public class DosBoxFileItem
{
    /// <summary>
    ///     Gets the absolute path to the file.
    /// </summary>
    public string FullPath { get; init; } = "";

    /// <summary>
    ///     Gets the file name shown to the user.
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    ///     Gets the path relative to the base directory.
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    ///     Returns the display name so accessibility trees and any fallback rendering never
    ///     expose the type name (the DOSBox file-selection list items use this as their name).
    /// </summary>
    public override string ToString()
    {
        return DisplayName;
    }
}