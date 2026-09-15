using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed GOG games via the Windows uninstall registry and creates shortcuts for them.
/// </summary>
public class ScanGogGames : IGamePlatformScanner
{
    /// <summary>
    ///     Scans the registry for GOG.com installations and creates launch shortcuts and cover images.
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
            var uninstallKeys = new[]
            {
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var keyPath in uninstallKeys)
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(keyPath);
                if (baseKey == null) continue;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var publisher = subKey.GetValue("Publisher") as string;
                        // GOG entries usually have "GOG.com" as publisher
                        if (!string.Equals(publisher, "GOG.com", StringComparison.Ordinal)) continue;

                        var gameId = subKeyName.Replace("_is1", "");
                        if (!long.TryParse(gameId, CultureInfo.InvariantCulture, out _)) continue;

                        var installLocation = subKey.GetValue("InstallLocation") as string;
                        var displayName = subKey.GetValue("DisplayName") as string;

                        if (string.IsNullOrEmpty(installLocation) || !Directory.Exists(installLocation)) continue;
                        if (string.IsNullOrEmpty(displayName)) continue;

                        displayName = displayName.Replace("™", "").Replace("®", "").Trim();

                        if (ignoredGameNames.Contains(displayName)) continue;

                        // --- FIX: Check for DLC via goggame-*.info ---
                        string? mainExePath = null;
                        var infoFile = Path.Combine(installLocation, $"goggame-{gameId}.info");
                        var isDlc = false;

                        if (File.Exists(infoFile))
                        {
                            try
                            {
                                var json = await File.ReadAllTextAsync(infoFile);
                                var gameInfo = JsonSerializer.Deserialize<GogGameInfo>(json);

                                // If RootGameId exists and is different from GameId, this is a DLC
                                if (gameInfo != null && !string.IsNullOrEmpty(gameInfo.RootGameId) &&
                                    !string.Equals(gameInfo.RootGameId, gameInfo.GameId, StringComparison.Ordinal))
                                {
                                    isDlc = true;
                                }

                                if (!isDlc)
                                {
                                    var primaryTask = gameInfo?.PlayTasks?.FirstOrDefault(static t =>
                                        t.IsPrimary && string.Equals(t.Type, "FileTask", StringComparison.Ordinal));
                                    if (primaryTask != null && !string.IsNullOrEmpty(primaryTask.Path))
                                        mainExePath = Path.Combine(installLocation, primaryTask.Path);
                                }
                            }
                            catch
                            {
                                // Fallback to heuristics if JSON parsing fails
                            }
                        }

                        if (isDlc) continue;

                        // ---------------------------------------------
                        var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(displayName);

                        // Option B: Direct Launch (Bypasses Galaxy)
                        if (!string.IsNullOrEmpty(mainExePath) && File.Exists(mainExePath))
                        {
                            Directory.CreateDirectory(windowsRomsPath);
                            var batPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.bat");
                            var batContent =
                                $"@echo off\r\ncd /d \"{Path.GetDirectoryName(mainExePath)}\"\r\nstart \"\" \"{Path.GetFileName(mainExePath)}\"";
                            await File.WriteAllTextAsync(batPath, batContent);
                        }

                        await gameScannerService.FindAndSaveGameImageAsync(logErrors, displayName, installLocation,
                            sanitizedGameName, windowsImagesPath, mainExePath);
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex, $"Error processing GOG game registry key: {subKeyName}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for GOG games");
        }
    }
}