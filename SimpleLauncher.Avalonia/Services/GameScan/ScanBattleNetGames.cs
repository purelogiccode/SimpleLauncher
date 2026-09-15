using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed Battle.net games and creates shortcuts for them.
/// </summary>
public partial class ScanBattleNetGames : IGamePlatformScanner
{
    // Mapping from InternalId (found in registry) to Readable Name and Executable (for icons)
    private static readonly List<BNetAppDef> AppDefinitions =
    [
        new() { InternalId = "wow", Name = "World of Warcraft" },
        new() { InternalId = "diablo3", Name = "Diablo III" },
        new() { InternalId = "s2", Name = "StarCraft II" },
        new() { InternalId = "s1", Name = "StarCraft" },
        new() { InternalId = "hs_beta", Name = "Hearthstone" },
        new() { InternalId = "heroes", Name = "Heroes of the Storm" },
        new() { InternalId = "prometheus", Name = "Overwatch 2" },
        new() { InternalId = "viper", Name = "Call of Duty Black Ops 4" },
        new() { InternalId = "odin", Name = "Call of Duty Modern Warfare" },
        new() { InternalId = "w3", Name = "Warcraft III Reforged" },
        new() { InternalId = "lazarus", Name = "Call of Duty MW2 Campaign Remastered" },
        new() { InternalId = "zeus", Name = "Call of Duty Black Ops Cold War" },
        new() { InternalId = "wlby", Name = "Crash Bandicoot 4" },
        new() { InternalId = "osi", Name = "Diablo II Resurrected" },
        new() { InternalId = "rtro", Name = "Blizzard Arcade Collection" },
        new() { InternalId = "fore", Name = "Call of Duty Vanguard" },
        new() { InternalId = "anbs", Name = "Diablo Immortal" },
        new() { InternalId = "auks", Name = "Call of Duty Modern Warfare II" },
        new() { InternalId = "Fen", Name = "Diablo IV" },
        new() { InternalId = "gryphon", Name = "Warcraft Rumble" },
        new() { InternalId = "w1", Name = "Warcraft: Orcs & Humans" },
        new() { InternalId = "w2", Name = "Warcraft II: Battle.net Edition" },
        new() { InternalId = "w1r", Name = "Warcraft: Remastered" },
        new() { InternalId = "w2r", Name = "Warcraft II: Remastered" },
        new() { InternalId = "d1", Name = "Diablo" },
        // Classics
        new()
        {
            InternalId = "Diablo II", Name = "Diablo II", IsClassic = true, Exe = "Diablo II.exe", ProductId = "D2"
        },
        new()
        {
            InternalId = "Warcraft III", Name = "Warcraft III", IsClassic = true, Exe = "Warcraft III.exe",
            ProductId = "W3"
        }
    ];

    /// <inheritdoc />
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

                        var displayName = subKey.GetValue("DisplayName") as string;
                        var uninstallString = subKey.GetValue("UninstallString") as string;
                        var installLocation = subKey.GetValue("InstallLocation") as string;

                        if (string.IsNullOrEmpty(uninstallString)) continue;

                        // Check for Battle.net UID in uninstall string
                        var match = MyRegex().Match(uninstallString);

                        if (match.Success)
                        {
                            var uid = match.Groups[1].Value;
                            var def = AppDefinitions.FirstOrDefault(a =>
                                uid.StartsWith(a.InternalId, StringComparison.OrdinalIgnoreCase));

                            if (def != null)
                            {
                                if (ignoredGameNames.Contains(def.Name)) continue;

                                var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(def.Name);
                                var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");

                                // Launch via Battle.net executable arguments to ensure login
                                // Most apps uses: Battle.net.exe --exec="launch {uid}"
                                // We can try protocol: battlenet://{uid} (works for some, but exec is safer for login)
                                // Let's use protocol for simplicity in .url file, or create .bat if needed.
                                // Protocol: battlenet://{InternalId} usually works.
                                var launchId = !string.IsNullOrEmpty(def.ProductId) ? def.ProductId : def.InternalId;
                                var shortcutContent = $"[InternetShortcut]\nURL=battlenet://{launchId}";
                                await File.WriteAllTextAsync(shortcutPath, shortcutContent);

                                if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                {
                                    await gameScannerService.FindAndSaveGameImageAsync(logErrors, def.Name,
                                        installLocation, sanitizedGameName, windowsImagesPath);
                                }
                            }
                        }
                        else if (displayName != null && AppDefinitions.Any(a =>
                                     a.IsClassic && displayName.Equals(a.InternalId,
                                         StringComparison.OrdinalIgnoreCase)))
                        {
                            // Classic Games
                            var def = AppDefinitions.First(a =>
                                a.IsClassic && displayName.Equals(a.InternalId, StringComparison.OrdinalIgnoreCase));
                            if (ignoredGameNames.Contains(def.Name)) continue;

                            var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(def.Name);
                            var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.bat");

                            if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                            {
                                var exePath = Path.Combine(installLocation, def.Exe);
                                var batContent =
                                    $"@echo off\r\ncd /d \"{installLocation}\"\r\nstart \"\" \"{def.Exe}\"";
                                await File.WriteAllTextAsync(shortcutPath, batContent);
                                await gameScannerService.FindAndSaveGameImageAsync(logErrors, def.Name, installLocation,
                                    sanitizedGameName, windowsImagesPath, exePath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex, $"Error processing Battle.net game registry key: {subKeyName}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for Battle.net games");
        }
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the Battle.net uid")]
    [GeneratedRegex(@"Battle\.net.*--uid=(.*?)(?:\s|$)", RegexOptions.None, 1000)]
    private static partial Regex MyRegex();
}