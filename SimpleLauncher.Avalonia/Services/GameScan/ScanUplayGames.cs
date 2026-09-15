using Microsoft.Win32;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed Ubisoft Connect (Uplay) games via the registry and creates shortcuts for them.
/// </summary>
public class ScanUplayGames : IGamePlatformScanner
{
    /// <summary>
    ///     Scans the Ubisoft Launcher registry keys for installed games and creates shortcuts and cover images.
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
            // Most apps checks both registry views
            var registryViews = new[] { RegistryView.Registry32, RegistryView.Registry64 };
            const string ubiRegKey = @"SOFTWARE\Ubisoft\Launcher\Installs";

            foreach (var view in registryViews)
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(ubiRegKey);
                if (baseKey == null) continue;

                foreach (var gameId in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var gameKey = baseKey.OpenSubKey(gameId);
                        if (gameKey == null) continue;

                        var installDir = gameKey.GetValue("InstallDir") as string;
                        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) continue;

                        var gameExe = gameKey.GetValue("ExecPath") as string;
                        // Clean up path separators
                        installDir = installDir.Replace('/', Path.DirectorySeparatorChar);

                        var gameName = new DirectoryInfo(installDir).Name.Replace(" Edition", "").Trim();
                        if (ignoredGameNames.Contains(gameName)) continue;

                        var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(gameName);
                        var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");

                        var shortcutContent = $"[InternetShortcut]\nURL=uplay://launch/{gameId}";
                        await File.WriteAllTextAsync(shortcutPath, shortcutContent);

                        string? fullExePath = null;
                        if (!string.IsNullOrEmpty(gameExe) && File.Exists(gameExe)) fullExePath = gameExe;

                        await gameScannerService.FindAndSaveGameImageAsync(logErrors, gameName, installDir,
                            sanitizedGameName, windowsImagesPath, fullExePath);
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex, $"Error processing Ubisoft game registry key: {gameId}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for Ubisoft games");
        }
    }
}