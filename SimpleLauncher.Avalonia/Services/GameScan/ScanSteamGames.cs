using System.Diagnostics;
using Microsoft.Win32;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SanitizeInputString;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed Steam games by reading the Steam installation registry entry,
///     library folders VDF, and app manifests, and creates shortcuts for them.
/// </summary>
public class ScanSteamGames : IGamePlatformScanner
{
    private readonly ILogger _logger;
    private readonly ISteamVdfParser _vdfParser;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScanSteamGames" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="vdfParser">The VDF parser used to read Steam configuration files.</param>
    public ScanSteamGames(ILogger logger, ISteamVdfParser vdfParser)
    {
        _logger = logger;
        _vdfParser = vdfParser;
    }

    /// <summary>
    ///     Locates all Steam library folders, scans their app manifests and source mods,
    ///     and creates shortcuts for the installed games.
    /// </summary>
    /// <param name="gameScannerService">The scanner service providing shared helpers.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="windowsRomsPath">The directory where game shortcuts are created.</param>
    /// <param name="windowsImagesPath">The directory where game images are stored.</param>
    /// <param name="ignoredGameNames">The set of game names to skip.</param>
    public async Task ScanAsync(GameScannerService gameScannerService, ILogger logErrors, string windowsRomsPath,
        string windowsImagesPath, ISet<string> ignoredGameNames)
    {
        if (!OperatingSystem.IsWindows()) return;

        var libraryPaths = new List<string>();

        try
        {
            // Prioritize HKCU as it reflects the current user's installation
            var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;

            if (string.IsNullOrEmpty(steamPath))
            {
                steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath",
                    null) as string;
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath",
                    null) as string;
            }

            if (string.IsNullOrEmpty(steamPath)) steamPath = GetSteamPathFromProcess();

            if (string.IsNullOrEmpty(steamPath))
            {
                _logger.Debug("[GameScannerService] Steam installation not found");
                return;
            }

            // Fix separators
            steamPath = steamPath.Replace('/', '\\');

            // 1. Add Default Library
            libraryPaths.Add(Path.Combine(steamPath, "steamapps"));

            // 2. Parse libraryfolders.vdf for external libraries
            var libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFoldersVdf))
            {
                try
                {
                    var vdfData = _vdfParser.Parse(libraryFoldersVdf, logErrors);

                    // Handle new VDF format (numeric keys at root or inside "libraryfolders")
                    var rootNode = vdfData.TryGetValue("libraryfolders", out var value)
                        ? value as Dictionary<string, object>
                        : vdfData;

                    if (rootNode != null)
                    {
                        foreach (var kvp in rootNode)
                        {
                            switch (kvp.Value)
                            {
                                // Modern format: "0" { "path" "C:\\Games" ... }
                                case Dictionary<string, object> libData when
                                    libData.TryGetValue("path", out var pathObj) &&
                                    pathObj is string pathStr:
                                {
                                    if (!string.Equals(pathStr, steamPath, StringComparison.OrdinalIgnoreCase))
                                        libraryPaths.Add(Path.Combine(pathStr, "steamapps"));

                                    break;
                                }
                                // Legacy format: "1" "C:\\Games"
                                case string legacyPath:
                                {
                                    if (!string.Equals(legacyPath, steamPath, StringComparison.OrdinalIgnoreCase))
                                        libraryPaths.Add(Path.Combine(legacyPath, "steamapps"));

                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logErrors.Error(ex, "Error parsing libraryfolders.vdf");
                }
            }

            // 3. Scan for Games in all libraries
            foreach (var libraryPath in libraryPaths.Distinct(StringComparer.Ordinal))
            {
                if (!Directory.Exists(libraryPath)) continue;

                string[] manifestFiles;
                try
                {
                    manifestFiles = Directory.GetFiles(libraryPath, "appmanifest_*.acf");
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible library paths
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var manifestFile in manifestFiles)
                {
                    await ProcessSteamManifestAsync(gameScannerService, manifestFile, libraryPath, steamPath, logErrors,
                        windowsRomsPath, windowsImagesPath, ignoredGameNames);
                }
            }

            // 4. Scan for Source Mods
            // Mods are usually in Steam\steamapps\sourcemods
            var sourceModsPath = Path.Combine(steamPath, "steamapps", "sourcemods");
            if (Directory.Exists(sourceModsPath))
            {
                string[] modDirectories;
                try
                {
                    modDirectories = Directory.GetDirectories(sourceModsPath);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible sourcemods path
                    modDirectories = [];
                }
                catch (IOException)
                {
                    modDirectories = [];
                }

                foreach (var modDir in modDirectories)
                {
                    // Pass windowsImagesPath here
                    await ProcessSourceModAsync(gameScannerService, modDir, windowsRomsPath, windowsImagesPath,
                        logErrors);
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for Steam games");
        }
    }

    private async Task ProcessSteamManifestAsync(GameScannerService gameScannerService, string manifestFile,
        string libraryPath, string steamPath, ILogger logErrors, string windowsRomsPath, string windowsImagesPath,
        ISet<string> ignoredGameNames)
    {
        try
        {
            var appData = _vdfParser.Parse(manifestFile, logErrors);
            if (appData.TryGetValue("AppState", out var appState) &&
                appState is Dictionary<string, object> appStateDict)
            {
                if (appStateDict.TryGetValue("name", out var nameObj) && nameObj is string gameName &&
                    appStateDict.TryGetValue("appid", out var appIdObj) && appIdObj is string appId &&
                    appStateDict.TryGetValue("installdir", out var installDirObj) && installDirObj is string installDir)
                {
                    if (ignoredGameNames.Contains(gameName)) return;

                    var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(gameName);
                    var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");
                    var gameInstallPath = Path.Combine(libraryPath, "common", installDir);

                    var shortcutContent = $"[InternetShortcut]\nURL=steam://run/{appId}";
                    await File.WriteAllTextAsync(shortcutPath, shortcutContent);

                    await TryCopySteamArtworkAsync(gameScannerService, logErrors, steamPath, appId, gameName,
                        sanitizedGameName, gameInstallPath, windowsImagesPath);
                }
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            // Expected condition: ROMs directory doesn't exist (e.g. app in protected location).
            // Log at Information level so the bug report API does not pick it up.
            logErrors.Information(ex,
                "Cannot create Steam shortcut: ROMs directory does not exist. Manifest: {ManifestFile}",
                manifestFile);
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, $"Error processing Steam manifest: {manifestFile}");
        }
    }

    private async Task ProcessSourceModAsync(GameScannerService gameScannerService, string modDir,
        string windowsRomsPath, string windowsImagesPath, ILogger logErrors)
    {
        try
        {
            var gameInfoPath = Path.Combine(modDir, "gameinfo.txt");
            if (!File.Exists(gameInfoPath)) return;

            // 1. Parse gameinfo.txt using the existing VDF parser
            var vdfData = _vdfParser.Parse(gameInfoPath, logErrors);

            string? gameName = null;
            string? baseAppId = null;

            // Source mods store info under a "GameInfo" root key
            if (vdfData.TryGetValue("GameInfo", out var gi) && gi is Dictionary<string, object> gameInfo)
            {
                // Get the Display Name
                if (gameInfo.TryGetValue("game", out var nameObj)) gameName = nameObj.ToString();

                // Get the Base AppID (e.g., 243730 for Source SDK 2013)
                if (gameInfo.TryGetValue("FileSystem", out var fs) && fs is Dictionary<string, object> fileSystem)
                {
                    if (fileSystem.TryGetValue("SteamAppId", out var appIdObj))
                        baseAppId = appIdObj.ToString();
                }
            }

            // Fallback for name if not found in VDF
            if (string.IsNullOrEmpty(gameName)) gameName = new DirectoryInfo(modDir).Name;

            if (string.IsNullOrEmpty(baseAppId))
            {
                _logger.Debug($"[GameScannerService] Could not resolve Base AppID for mod: {gameName}. Skipping.");
                return;
            }

            var modFolderName = new DirectoryInfo(modDir).Name;
            var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(gameName);
            var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");

            // 2. Create the Shortcut
            // The protocol for mods is: steam://run/<BaseAppID>//-game <ModFolderName>
            var shortcutContent = $"[InternetShortcut]\nURL=steam://run/{baseAppId}//-game \"{modFolderName}\"";
            await File.WriteAllTextAsync(shortcutPath, shortcutContent);

            // 3. Handle Icon/Image
            var destArtworkPath = Path.Combine(windowsImagesPath, $"{sanitizedGameName}.png");
            if (!File.Exists(destArtworkPath))
            {
                // Source mods usually have a game.ico in the root folder
#if WINDOWS
                var modIcon = Path.Combine(modDir, "game.ico");
                if (File.Exists(modIcon))
                    try
                    {
                        using var icon = new Icon(modIcon, 256, 256);
                        using var bmp = icon.ToBitmap();
                        bmp.Save(destArtworkPath, ImageFormat.Png);
                    }
                    catch
                    {
                        /* Fallback to generic scan */
                    }
#endif

                if (!File.Exists(destArtworkPath))
                {
                    await gameScannerService.FindAndSaveGameImageAsync(logErrors, gameName, modDir, sanitizedGameName,
                        windowsImagesPath);
                }
            }

            _logger.Debug($"[GameScannerService] Created shortcut for Source Mod: {gameName}");
        }
        catch (DirectoryNotFoundException ex)
        {
            // Expected condition: ROMs directory doesn't exist (e.g. app in protected location).
            // Log at Information level so the bug report API does not pick it up.
            logErrors.Information(ex, "Cannot create Source Mod shortcut: ROMs directory does not exist. Mod: {ModDir}",
                modDir);
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, $"Error processing Source Mod in {modDir}");
        }
    }

    private static string? GetSteamPathFromProcess()
    {
        try
        {
            var steamProcess = Process.GetProcessesByName("steam").FirstOrDefault();
            return steamProcess?.MainModule?.FileName != null
                ? Path.GetDirectoryName(steamProcess.MainModule.FileName)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task TryCopySteamArtworkAsync(GameScannerService gameScannerService, ILogger logErrors,
        string steamPath, string appId, string gameName, string sanitizedGameName, string gameInstallPath,
        string windowsImagesPath)
    {
        // steamPath/appId are used by the Windows-only Steam cache copy below; log them
        // unconditionally so the parameters stay referenced on every target platform.
        logErrors.Debug($"Steam artwork copy for '{gameName}' (app {appId}) using Steam path '{steamPath}'.");

        var destArtworkPath = Path.Combine(windowsImagesPath, $"{sanitizedGameName}.png");
        if (File.Exists(destArtworkPath)) return;

        // 1. Try API first
        if (await gameScannerService.TryDownloadImageFromApiAsync(gameName, destArtworkPath, logErrors)) return;

        // 2. Try Steam's local artwork cache (System.Drawing conversion is Windows-only)
#if WINDOWS
        var cachePath = Path.Combine(steamPath, "appcache", "librarycache");
        if (Directory.Exists(cachePath))
        {
            string[] searchPatterns =
            [
                $"{appId}_library_600x900.jpg",
                $"{appId}_header.jpg",
                $"{appId}_library_hero.jpg"
            ];

            foreach (var pattern in searchPatterns)
            {
                var sourcePath = Path.Combine(cachePath, pattern);
                if (File.Exists(sourcePath))
                    try
                    {
                        // Convert JPG to PNG
                        using var image = Image.FromFile(sourcePath);
                        image.Save(destArtworkPath, ImageFormat.Png);
                        return; // Successfully converted and saved
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        // Expected condition: images directory doesn't exist (e.g. app in protected location).
                        // Log at Information level so the bug report API does not pick it up.
                        logErrors.Information(ex,
                            "Cannot save Steam artwork: images directory does not exist. Game: {SanitizedGameName}",
                            sanitizedGameName);
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex,
                            $"Error converting Steam artwork from JPG to PNG for {sanitizedGameName} (Source: {sourcePath})");
                    }
            }
        }
#endif

        // 3. Fallback to EXE icon if no artwork was found
        await gameScannerService.ExtractIconFromGameFolderAsync(logErrors, gameInstallPath, sanitizedGameName,
            windowsImagesPath);
    }
}