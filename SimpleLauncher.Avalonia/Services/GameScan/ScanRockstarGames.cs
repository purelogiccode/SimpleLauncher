using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed Rockstar Games by matching uninstall strings against known title IDs
///     and creates shortcuts for them.
/// </summary>
public partial class ScanRockstarGames : IGamePlatformScanner
{
    // Mapping from TitleId to Name and Executable
    private static readonly List<RockstarGameDef> Games =
    [
        new() { TitleId = "gta5", Name = "Grand Theft Auto V", Exe = "PlayGTAV.exe" },
        new() { TitleId = "gta5_gen9", Name = "Grand Theft Auto V Enhanced", Exe = "GTA5_Enhanced_BE.exe" },
        new() { TitleId = "rdr2", Name = "Red Dead Redemption 2", Exe = "RDR2.exe" },
        new() { TitleId = "rdr", Name = "Red Dead Redemption", Exe = "RDR.exe" },
        new() { TitleId = "lanoire", Name = "L.A. Noire", Exe = "LANoire.exe" },
        new() { TitleId = "lanoirevr", Name = "L.A. Noire: The VR Case Files", Exe = "LANoireVR.exe" },
        new() { TitleId = "mp3", Name = "Max Payne 3", Exe = "MaxPayne3.exe" },
        new() { TitleId = "gtasa", Name = "Grand Theft Auto San Andreas", Exe = "gta_sa.exe" },
        new() { TitleId = "gta3", Name = "Grand Theft Auto III", Exe = "gta3.exe" },
        new() { TitleId = "gtavc", Name = "Grand Theft Auto Vice City", Exe = "gta-vc.exe" },
        new() { TitleId = "bully", Name = "Bully Scholarship Edition", Exe = "Bully.exe" },
        new() { TitleId = "gta4", Name = "Grand Theft Auto IV", Exe = "GTAIV.exe" },
        new()
        {
            TitleId = "gta3unreal", Name = "GTA III Definitive Edition", Exe = "Gameface/Binaries/Win64/LibertyCity.exe"
        },
        new()
        {
            TitleId = "gtavcunreal", Name = "GTA Vice City Definitive Edition",
            Exe = "Gameface/Binaries/Win64/ViceCity.exe"
        },
        new()
        {
            TitleId = "gtasaunreal", Name = "GTA San Andreas Definitive Edition",
            Exe = "Gameface/Binaries/Win64/SanAndreas.exe"
        }
    ];

    /// <summary>
    ///     Scans the registry uninstall entries for known Rockstar title IDs and creates shortcuts and cover images.
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

                        var uninstallString = subKey.GetValue("UninstallString") as string;
                        var installLocation = subKey.GetValue("InstallLocation") as string;

                        if (string.IsNullOrEmpty(uninstallString)) continue;

                        var match = MyRegex().Match(uninstallString);

                        if (match.Success)
                        {
                            var titleId = match.Groups[1].Value.Trim();
                            var gameDef = Games.FirstOrDefault(g =>
                                g.TitleId.Equals(titleId, StringComparison.OrdinalIgnoreCase));

                            if (gameDef != null)
                            {
                                if (ignoredGameNames.Contains(gameDef.Name)) continue;

                                var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(gameDef.Name);
                                var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");

                                // Rockstar Launcher Protocol
                                // rockstargames://launch/{titleId} usually works.
                                // We'll use the protocol for the .url file.
                                var shortcutContent = $"[InternetShortcut]\nURL=rockstargames://launch/{titleId}";
                                await File.WriteAllTextAsync(shortcutPath, shortcutContent);

                                if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                {
                                    var exePath = Path.Combine(installLocation, gameDef.Exe);
                                    await gameScannerService.FindAndSaveGameImageAsync(logErrors, gameDef.Name,
                                        installLocation, sanitizedGameName, windowsImagesPath, exePath);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex, $"Error processing Rockstar game registry key: {subKeyName}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for Rockstar games");
        }
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the Rockstar title id")]
    [GeneratedRegex(@"(?:Launcher|uninstall)\.exe.+uninstall=(.+)$", RegexOptions.IgnoreCase, 1000, "pt-BR")]
    private static partial Regex MyRegex();
}