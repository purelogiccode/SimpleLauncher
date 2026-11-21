using SimpleLauncher.Avalonia.Services.GameScan;

namespace SimpleLauncher.Avalonia.Interfaces;

/// <summary>
///     Provides methods to scan for games available on a specific platform.
/// </summary>
internal interface IGamePlatformScanner
{
    /// <summary>
    ///     Asynchronously scans for games on the platform, logging errors and respecting ignored game names.
    /// </summary>
    /// <param name="gameScannerService">The game scanner service used for scanning.</param>
    /// <param name="logErrors">The logger for recording errors.</param>
    /// <param name="windowsRomsPath">The path to the ROM files on Windows.</param>
    /// <param name="windowsImagesPath">The path to the image files on Windows.</param>
    /// <param name="ignoredGameNames">A set of game names to exclude from scanning.</param>
    Task ScanAsync(GameScannerService gameScannerService, ILogger logErrors, string windowsRomsPath,
        string windowsImagesPath, ISet<string> ignoredGameNames);
}