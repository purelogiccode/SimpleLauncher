namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Provides methods to convert disc image formats such as CHD, PBP, and ISO.
/// </summary>
public interface IDiscConverter
{
    /// <summary>
    ///     Converts a CHD disc image to ISO format.
    /// </summary>
    /// <param name="chdPath">The path to the CHD file.</param>
    /// <returns>The path to the converted ISO file, or null if conversion failed.</returns>
    Task<string?> ConvertChdToIsoAsync(string chdPath);

    /// <summary>
    ///     Converts a CHD disc image to CUE/BIN format.
    /// </summary>
    /// <param name="chdPath">The path to the CHD file.</param>
    /// <returns>The path to the generated CUE file, or null if conversion failed.</returns>
    Task<string?> ConvertChdToCueBinAsync(string chdPath);

    /// <summary>
    ///     Converts a PBP (PSP compressed) disc image to CUE/BIN format.
    /// </summary>
    /// <param name="pbpPath">The path to the PBP file.</param>
    /// <returns>The path to the generated CUE file, or null if conversion failed.</returns>
    Task<string?> ConvertPbpToCueBinAsync(string pbpPath);
}