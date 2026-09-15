using System.Text.Json;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed Epic Games Store games and creates shortcuts for them.
/// </summary>
public class ScanEpicGames : IGamePlatformScanner
{
    /// <summary>
    ///     Scans the Epic Games launcher installation data (LauncherInstalled.dat or manifests)
    ///     and creates shortcuts for installed games.
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

        try
        {
            // Method 1: LauncherInstalled.dat (Preferred/Faster)
            var allUsersPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic");
            var installedDatPath = Path.Combine(allUsersPath, "UnrealEngineLauncher", "LauncherInstalled.dat");

            if (File.Exists(installedDatPath))
            {
                try
                {
                    var jsonString = await File.ReadAllTextAsync(installedDatPath);
                    var installedApps = JsonSerializer.Deserialize<EpicInstalledAppList>(jsonString);

                    if (installedApps?.InstallationList != null)
                    {
                        foreach (var app in installedApps.InstallationList)
                        {
                            // Filter out Unreal Engine tools (UE_...) and other non-games
                            if (string.IsNullOrEmpty(app.AppName) ||
                                app.AppName.StartsWith("UE_", StringComparison.Ordinal) ||
                                app.AppName.StartsWith("Falcon", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            // We need the display name. LauncherInstalled.dat usually has AppName as the ID (e.g., "Codename").
                            // We might need to cross-reference with Manifests to get the pretty DisplayName.
                            // So we use the InstallLocation to find the manifest or just use the folder name as fallback.
                            string? displayName = null;
                            string? launchExe = null;

                            // Try to find matching manifest to get DisplayName and Executable
                            var manifestsPath = Path.Combine(allUsersPath, "EpicGamesLauncher", "Data", "Manifests");
                            if (Directory.Exists(manifestsPath))
                            {
                                var manifestFiles = Directory.GetFiles(manifestsPath, "*.item");
                                foreach (var mFile in manifestFiles)
                                {
                                    // Simple check to avoid full parse if possible, or just parse
                                    var content = await File.ReadAllTextAsync(mFile);
                                    if (content.Contains($"\"AppName\": \"{app.AppName}\"", StringComparison.Ordinal))
                                    {
                                        using var doc = JsonDocument.Parse(content);
                                        if (doc.RootElement.TryGetProperty("DisplayName", out var dn))
                                            displayName = dn.GetString();

                                        if (doc.RootElement.TryGetProperty("LaunchExecutable", out var le))
                                            launchExe = le.GetString();

                                        break;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(displayName))
                                displayName = new DirectoryInfo(app.InstallLocation).Name;

                            if (ignoredGameNames.Contains(displayName)) continue;

                            await CreateEpicShortcutAsync(gameScannerService, logErrors, displayName, app.AppName,
                                app.InstallLocation, launchExe, windowsRomsPath, windowsImagesPath);
                        }

                        return; // Successfully processed via DAT file
                    }
                }
                catch (Exception ex)
                {
                    logErrors.Error(ex, "Error reading Epic LauncherInstalled.dat. Falling back to manifests");
                }
            }

            // Method 2: Fallback to scanning Manifests directly
            var manifestsDir = Path.Combine(allUsersPath, "EpicGamesLauncher", "Data", "Manifests");
            if (Directory.Exists(manifestsDir))
            {
                var manifestFiles = Directory.GetFiles(manifestsDir, "*.item");
                foreach (var manifestFile in manifestFiles)
                {
                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(manifestFile);
                        using var doc = JsonDocument.Parse(jsonContent);
                        var root = doc.RootElement;

                        if (!root.TryGetProperty("DisplayName", out var nameProp) ||
                            !root.TryGetProperty("AppName", out var appNameProp))
                        {
                            continue;
                        }

                        var displayName = nameProp.GetString();
                        var appName = appNameProp.GetString();

                        // Filter UE stuff and ignored games
                        if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(displayName) ||
                            appName.StartsWith("UE_", StringComparison.InvariantCulture) ||
                            ignoredGameNames.Contains(displayName))
                        {
                            continue;
                        }

                        // Filter DLCs: If MainGameAppName exists and is different from AppName, it's likely a DLC
                        if (root.TryGetProperty("MainGameAppName", out var mainGameAppName) &&
                            !string.IsNullOrEmpty(mainGameAppName.GetString()))
                        {
                            if (!string.Equals(appName, mainGameAppName.GetString(), StringComparison.Ordinal))
                                continue;
                        }

                        // Filter by Category (exclude plugins, editors, etc.)
                        if (root.TryGetProperty("AppCategories", out var cats))
                        {
                            var isGame = false;
                            foreach (var cat in cats.EnumerateArray())
                            {
                                var s = cat.GetString();
                                if (string.Equals(s, "games", StringComparison.Ordinal)) isGame = true;

                                if (s is "plugins" or "editors" or "engines")
                                {
                                    isGame = false;
                                    break;
                                }
                            }

                            if (!isGame) continue;
                        }

                        var installLoc = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : "";
                        var launchExe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() : "";

                        await CreateEpicShortcutAsync(gameScannerService, logErrors, displayName, appName, installLoc,
                            launchExe, windowsRomsPath, windowsImagesPath);
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex, $"Error processing Epic manifest: {manifestFile}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for Epic games");
        }
    }

    private static async Task CreateEpicShortcutAsync(GameScannerService gameScannerService, ILogger logErrors,
        string displayName, string? appName, string? installLocation, string? launchExecutable, string? windowsRomsPath,
        string? windowsImagesPath)
    {
        var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(displayName);
        var shortcutPath = Path.Combine(windowsRomsPath!, $"{sanitizedGameName}.url");

        var shortcutContent =
            $"[InternetShortcut]\nURL=com.epicgames.launcher://apps/{appName}?action=launch&silent=true";
        await File.WriteAllTextAsync(shortcutPath, shortcutContent);

        switch (string.IsNullOrEmpty(installLocation))
        {
            // Extract icon
            case false when !string.IsNullOrEmpty(launchExecutable):
            {
                var fullExePath = Path.Combine(installLocation!, launchExecutable);
                await gameScannerService.FindAndSaveGameImageAsync(logErrors, displayName, installLocation!,
                    sanitizedGameName, windowsImagesPath!, fullExePath);
                break;
            }
            case false:
                await gameScannerService.FindAndSaveGameImageAsync(logErrors, displayName, installLocation!,
                    sanitizedGameName, windowsImagesPath!);
                break;
        }
    }
}