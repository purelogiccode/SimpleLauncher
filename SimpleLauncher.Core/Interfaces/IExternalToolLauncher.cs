namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Provides methods to launch external tools such as batch file creators, converters, and ROM utilities.
/// </summary>
public interface IExternalToolLauncher
{
    /// <summary>
    ///     Launches the external tool for batch converting files to compressed archive format.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BatchConvertToCompressedFileAsync();

    /// <summary>
    ///     Launches the external tool for creating batch files for PS3 games.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateBatchFilesForPs3GamesAsync();

    /// <summary>
    ///     Launches the external tool for creating batch files for ScummVM games.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateBatchFilesForScummVmGamesAsync();

    /// <summary>
    ///     Launches the external tool for creating batch files for Windows games.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateBatchFilesForWindowsGamesAsync();

    /// <summary>
    ///     Launches the external tool for creating batch files for Xbox 360 XBLA games.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateBatchFilesForXbox360XblaGamesAsync();

    /// <summary>
    ///     Launches the ROM cover finder tool with the specified image and ROM folders.
    /// </summary>
    /// <param name="selectedImageFolder">The optional path to the image folder to search for covers.</param>
    /// <param name="selectedRomFolder">The optional path to the ROM folder to match covers against.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FindRomCoverLaunchAsync(string? selectedImageFolder, string? selectedRomFolder);

    /// <summary>
    ///     Launches CHDStudio (the cross-platform CHD utility that replaces BatchConvertToCHD)
    ///     for the running platform.
    /// </summary>
    /// <param name="selectedRomFolder">The optional path to the folder containing ROMs to convert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchChdStudioAsync(string? selectedRomFolder);

    /// <summary>
    ///     Launches RVZStudio (the cross-platform GameCube/Wii disc-image utility that replaces
    ///     BatchConvertToRVZ) for the running platform.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchRvzStudioAsync();

    /// <summary>
    ///     Launches XISOStudio (the cross-platform ISO/XISO/Xbox image utility that replaces
    ///     BatchConvertIsoToXiso) for the running platform.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchXisoStudioAsync();

    /// <summary>
    ///     Launches the retro game cover downloader tool with the specified ROM and image folders.
    /// </summary>
    /// <param name="selectedImageFolder">The optional path to the folder where cover images will be saved.</param>
    /// <param name="selectedRomFolder">The optional path to the folder containing ROMs to download covers for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RetroGameCoverDownloaderAsync(string? selectedImageFolder, string? selectedRomFolder);

    /// <summary>
    ///     Launches the ROM validator tool to verify ROM file integrity.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RomValidatorAsync();
}