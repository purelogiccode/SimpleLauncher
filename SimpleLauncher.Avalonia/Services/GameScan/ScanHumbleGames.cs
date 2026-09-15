using System.Text.Json;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed Humble App games by reading its configuration file and creates shortcuts for them.
/// </summary>
public class ScanHumbleGames : IGamePlatformScanner
{
    /// <summary>
    ///     Reads the Humble App configuration and creates shortcuts for installed or downloaded games.
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
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Humble App", "config.json");
            if (!File.Exists(configPath)) return;

            var jsonContent = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty("game-collection-4", out var collection))
            {
                foreach (var game in collection.EnumerateArray())
                {
                    try
                    {
                        var status = game.GetProperty("status").GetString();
                        if (!string.Equals(status, "installed", StringComparison.Ordinal) &&
                            !string.Equals(status, "downloaded", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!game.TryGetProperty("machineName", out var machineNameProp)) continue;

                        var machineName = machineNameProp.GetString();
                        var gameName = game.GetProperty("gameName").GetString();

                        if (string.IsNullOrEmpty(gameName) || ignoredGameNames.Contains(gameName)) continue;

                        // Determine install path
                        string? installDir = null;
                        string? exePath = null;

                        // Try 'filePath' first
                        if (game.TryGetProperty("filePath", out var fp) && !string.IsNullOrEmpty(fp.GetString()))
                        {
                            installDir = fp.GetString();
                        }
                        // Fallback to downloadFilePath + machineName
                        else if (game.TryGetProperty("downloadFilePath", out var dfp) &&
                                 !string.IsNullOrEmpty(dfp.GetString()))
                        {
                            var downloadPath = dfp.GetString();
                            if (!string.IsNullOrEmpty(downloadPath) && !string.IsNullOrEmpty(machineName))
                                installDir = Path.Combine(downloadPath, machineName);
                        }

                        if (game.TryGetProperty("executablePath", out var ep)) exePath = ep.GetString();

                        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) continue;

                        var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(gameName);
                        var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");

                        // Humble Protocol
                        var shortcutContent = $"[InternetShortcut]\nURL=humble://launch/{machineName}";
                        await File.WriteAllTextAsync(shortcutPath, shortcutContent);

                        string? fullExePath = null;
                        if (!string.IsNullOrEmpty(installDir) && !string.IsNullOrEmpty(exePath))
                            fullExePath = Path.Combine(installDir, exePath);

                        await gameScannerService.FindAndSaveGameImageAsync(logErrors, gameName, installDir,
                            sanitizedGameName, windowsImagesPath, fullExePath);
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex, "Error processing Humble game entry");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for Humble games");
        }
    }
}