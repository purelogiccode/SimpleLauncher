using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     Configures RetroAchievements settings in various emulator configuration files (INI, CFG, JSON).
/// </summary>
public class RetroAchievementsEmulatorConfiguratorService : IRetroAchievementsEmulatorConfiguratorService
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsEmulatorConfiguratorService" /> class.
    /// </summary>
    public RetroAchievementsEmulatorConfiguratorService(ILogger logErrors, ILogger logger)
    {
        _logger = logErrors;
        _logger = logger;
    }

    /// <summary>
    ///     Configures RetroArch's retroarch.cfg with RetroAchievements credentials.
    /// </summary>
    public bool ConfigureRetroArch(string exePath, string username, string password)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        if (exeDir != null)
        {
            var configPath = Path.Combine(exeDir, "retroarch.cfg");

            // Restore from sample if missing
            if (!RestoreConfigFromSample("retroarch", configPath))
            {
                _logger.Warning($"RetroArch config not found and no sample available at {configPath}");
                return false;
            }

            var settingsToUpdate = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "cheevos_enable", "true" },
                { "cheevos_username", username },
                { "cheevos_password", password },
                { "cheevos_hardcore_mode_enable", "true" }
            };

            return UpdateSimpleIniFile(configPath, settingsToUpdate, " = ");
        }

        return false;
    }

    /// <summary>
    ///     Configures PCSX2's PCSX2.ini with RetroAchievements credentials.
    /// </summary>
    public bool ConfigurePcsx2(string exePath, string username, string token)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        var configDir = ResolvePcsx2ConfigDirectory(exeDir);

        var configPath = Path.Combine(configDir, "PCSX2.ini");

        // Restore from sample if missing
        if (!RestoreConfigFromSample("pcsx2", configPath))
        {
            _logger.Warning($"PCSX2 config not found and no sample available at {configPath}");
            return false;
        }

        var settingsToUpdate = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Enabled", "true" },
            { "Username", username },
            { "Token", token },
            { "Hardcore", "true" }
        };

        return UpdateIniFile(configPath, "Achievements", settingsToUpdate);
    }

    /// <summary>
    ///     Configures DuckStation's settings.ini with RetroAchievements credentials and encrypted token.
    /// </summary>
    public bool ConfigureDuckStation(string exePath, string username, string token)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        var isPortable = exeDir != null && File.Exists(Path.Combine(exeDir, "portable.txt"));
        var configDir = isPortable
            ? exeDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DuckStation");
        if (configDir != null)
        {
            var configPath = Path.Combine(configDir, "settings.ini");

            // Restore from sample if missing
            if (!RestoreConfigFromSample("duckstation", configPath))
            {
                _logger.Warning($"DuckStation config not found and no sample available at {configPath}");
                return false;
            }

            // Encrypt the token using DuckStation's logic
            var encryptedToken =
                EncryptDuckStationToken.EncryptDuckStationTokenMethod(token, username, isPortable, Log.Logger);

            // Validate encryption succeeded
            if (string.IsNullOrEmpty(encryptedToken))
            {
                _logger.Warning("Failed to encrypt DuckStation token - cannot configure emulator");
                return false;
            }

            var settingsToUpdate = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Enabled", "true" },
                { "ChallengeMode", "true" },
                { "EncoreMode", "false" },
                { "SpectatorMode", "false" },
                { "UnofficialTestMode", "false" },
                { "UseRAIntegration", "false" },
                { "Notifications", "true" },
                { "LeaderboardNotifications", "true" },
                { "LeaderboardTrackers", "true" },
                { "SoundEffects", "true" },
                { "ProgressIndicators", "true" },
                { "NotificationLocation", "TopLeft" },
                { "IndicatorLocation", "BottomRight" },
                { "ChallengeIndicatorMode", "Notification" },
                { "NotificationsDuration", "5" },
                { "LeaderboardsDuration", "10" },
                { "Username", username },
                { "Token", encryptedToken }
            };
            return UpdateIniFile(configPath, "Cheevos", settingsToUpdate);
        }

        return false;
    }

    /// <summary>
    ///     Configures PPSSPP's ppsspp.ini and session key file with RetroAchievements credentials.
    /// </summary>
    public bool ConfigurePpspp(string exePath, string username, string token)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        if (exeDir != null)
        {
            var configDir = Path.Combine(exeDir, "memstick", "PSP", "SYSTEM");
            var configPath = Path.Combine(configDir, "ppsspp.ini");

            // Restore from sample if missing
            if (!RestoreConfigFromSample("ppsspp", configPath))
            {
                _logger.Warning($"PPSSPP config not found and no sample available at {configPath}");
                return false;
            }

            // 1. Update the main INI file
            var settingsToUpdate = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "AchievementsEnable", "True" }, // PPSSPP uses TitleCase
                { "AchievementsChallengeMode", "True" },
                { "AchievementsUserName", username }
            };

            var iniUpdated = UpdateIniFile(configPath, "Achievements", settingsToUpdate);

            // 2. Save the session key to the separate .dat file
            try
            {
                var datFilePath = Path.Combine(configDir, "ppsspp_retroachievements.dat");
                File.WriteAllText(datFilePath, token);
                return iniUpdated;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create PPSSPP session file at {configDir}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Configures Dolphin's RetroAchievements.ini with RetroAchievements credentials.
    /// </summary>
    public bool ConfigureDolphin(string exePath, string username, string token)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        string configDir;

        if (exeDir != null && File.Exists(Path.Combine(exeDir, "portable.txt")))
        {
            configDir = Path.Combine(exeDir, "User", "Config");
        }
        else
        {
            // Standard Dolphin user path is in Documents
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Dolphin Emulator", "Config");
        }

        var configPath = Path.Combine(configDir, "RetroAchievements.ini");

        try
        {
            // Restore from sample if missing (before creating empty file)
            if (!RestoreConfigFromSample("dolphin", configPath))
            {
                // If no sample, create directory and empty file as fallback
                _logger.Debug($"[RA Configurator] No sample found for Dolphin, creating empty config at {configPath}");
            }

            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            if (!File.Exists(configPath)) File.WriteAllText(configPath, ""); // Create empty file if missing
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Failed to prepare Dolphin config path at {configDir}");
            return false;
        }

        var settingsToUpdate = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ChallengeIndicatorsEnabled", "True" }, // Dolphin uses TitleCase
            { "DiscordPresenceEnabled", "False" },
            { "Enabled", "True" },
            { "EncoreEnabled", "False" },
            { "HardcoreEnabled", "True" },
            { "LeaderboardTrackerEnabled", "True" },
            { "ProgressEnabled", "True" },
            { "SpectatorEnabled", "False" },
            { "UnofficialEnabled", "False" },
            { "Username", username },
            { "ApiToken", token }
        };

        return UpdateIniFile(configPath, "Achievements", settingsToUpdate);
    }

    /// <summary>
    ///     Configures Flycast's emu.cfg with RetroAchievements credentials.
    /// </summary>
    public bool ConfigureFlycast(string exePath, string username, string token)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        if (exeDir != null)
        {
            var configPath = Path.Combine(exeDir, "emu.cfg");

            // Try portable path first, then appdata
            if (!File.Exists(configPath) && !RestoreConfigFromSample("flycast", configPath))
            {
                configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "flycast", "emu.cfg");

                // Try restoring to appdata if portable failed
                if (!RestoreConfigFromSample("flycast", configPath))
                {
                    _logger.Warning("Flycast config not found and no sample available");
                    return false;
                }
            }

            var settingsToUpdate = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Enabled", "yes" }, // Flycast uses yes/no
                { "HardcoreMode", "yes" },
                { "Token", token },
                { "UserName", username }
            };

            return UpdateIniFile(configPath, "achievements", settingsToUpdate);
        }

        return false;
    }

    /// <summary>
    ///     Configures BizHawk's config.ini with RetroAchievements credentials.
    /// </summary>
    public bool ConfigureBizHawk(string exePath, string username, string token)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        if (exeDir != null)
        {
            var configPath = Path.Combine(exeDir, "config.ini");

            // Restore from sample if missing
            if (!RestoreConfigFromSample("bizhawk", configPath))
            {
                _logger.Warning($"BizHawk config not found and no sample available at {configPath}");
                return false;
            }

            try
            {
                var jsonContent = File.ReadAllText(configPath);

                if (JsonNode.Parse(jsonContent) is not JsonObject jsonNode) return false;

                // BizHawk uses flat root-level keys for RA settings
                jsonNode["SkipRATelemetryWarning"] = true;
                jsonNode["RAUsername"] = username;
                jsonNode["RAToken"] = token;
                jsonNode["RACheevosActive"] = true;
                jsonNode["RALBoardsActive"] = true;
                jsonNode["RARichPresenceActive"] = true;
                jsonNode["RAHardcoreMode"] = true;
                jsonNode["RASoundEffects"] = true;
                jsonNode["RAAllowUnofficialCheevos"] = false;
                jsonNode["RAAutostart"] = true;

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(configPath, jsonNode.ToJsonString(options));
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to configure BizHawk at {configPath}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Resolves PCSX2's config directory: portable mode is marked by <c>portable.ini</c>
    ///     next to the executable (config in <c>inis\</c>), otherwise the installed copy reads
    ///     <c>Documents\PCSX2\inis</c>. Mirrors the generic PCSX2 injector.
    /// </summary>
    private static string ResolvePcsx2ConfigDirectory(string? exeDir)
    {
        if (exeDir != null)
        {
            var localInis = Path.Combine(exeDir, "inis");
            if (File.Exists(Path.Combine(exeDir, "portable.ini")) || Directory.Exists(localInis))
                return localInis;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2", "inis");
    }

    /// <summary>
    ///     Restores a sample configuration file from the samples folder if the target config is missing.
    /// </summary>
    private bool RestoreConfigFromSample(string emulatorFolderName, string targetConfigPath)
    {
        // Treat missing or 0-byte files as candidates for restoration
        if (File.Exists(targetConfigPath) && new FileInfo(targetConfigPath).Length > 0) return true;

        var samplePath = ResolveSamplePath(emulatorFolderName, Path.GetFileName(targetConfigPath));

        if (samplePath == null) return false;

        try
        {
            var targetDir = Path.GetDirectoryName(targetConfigPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Copy(samplePath, targetConfigPath, false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex,
                $"Failed to restore {emulatorFolderName} config from sample: {samplePath} -> {targetConfigPath}");
            return false;
        }
    }

    /// <summary>
    ///     Resolves a bundled sample file under <c>samples\</c>. The folder name is matched
    ///     case-insensitively because the bundled folders use exact casing
    ///     (<c>samples\DuckStation</c>) and Linux/macOS file systems are case-sensitive.
    /// </summary>
    private static string? ResolveSamplePath(string emulatorFolderName, string fileName)
    {
        var samplesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");

        var exactPath = Path.Combine(samplesRoot, emulatorFolderName, fileName);
        if (File.Exists(exactPath)) return exactPath;

        if (!Directory.Exists(samplesRoot)) return null;

        var folder = Directory.EnumerateDirectories(samplesRoot)
            .FirstOrDefault(d => Path.GetFileName(d).Equals(emulatorFolderName, StringComparison.OrdinalIgnoreCase));
        if (folder == null) return null;

        var path = Path.Combine(folder, fileName);
        return File.Exists(path) ? path : null;
    }

    // Helper for simple INI files
    private bool UpdateSimpleIniFile(string filePath, Dictionary<string, string> settingsToUpdate, string separator)
    {
        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            var updatedSettings = new HashSet<string>(settingsToUpdate.Keys, StringComparer.Ordinal);

            for (var i = 0; i < lines.Count; i++)
            {
                var trimmedLine = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#')) continue;

                var parts = trimmedLine.Split([separator], 2, StringSplitOptions.None);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                if (settingsToUpdate.TryGetValue(key, out var newValue))
                {
                    lines[i] = $"{key}{separator}{FormatString(newValue)}";
                    updatedSettings.Remove(key);
                }
            }

            // Add any settings that were not found
            foreach (var key in updatedSettings) lines.Add($"{key}{separator}{FormatString(settingsToUpdate[key])}");

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Failed to update simple INI file: {filePath}");
            return false;
        }

        // Helper to properly format string values for RetroArch config
        // RetroArch requires string values to be wrapped in double quotes
        // and internal quotes must be escaped
        static string FormatString(string val)
        {
            if (string.IsNullOrEmpty(val))
                return "\"\"";

            // Strip existing surrounding quotes to prevent double-quoting
            val = val.Trim();
            if (val.Length >= 2 && val.StartsWith('"') && val.EndsWith('"')) val = val.Substring(1, val.Length - 2);

            // Escape any internal quotes and wrap in quotes
            val = val.Replace("\"", "\\\"");
            return $"\"{val}\"";
        }
    }

    // Helper for standard INI files with [Sections]
    private bool UpdateIniFile(string filePath, string section, Dictionary<string, string> settingsToUpdate)
    {
        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            var inSection = false;
            var sectionFound = false;
            var updatedSettings = new HashSet<string>(settingsToUpdate.Keys, StringComparer.OrdinalIgnoreCase);
            var sectionLineIndex = -1;

            for (var i = 0; i < lines.Count; i++)
            {
                var trimmedLine = lines[i].Trim();
                if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
                {
                    if (inSection)
                    {
                        // We've left the target section
                        break;
                    }

                    if (trimmedLine.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = true;
                        sectionFound = true;
                        sectionLineIndex = i;
                    }
                }
                else if (inSection)
                {
                    var parts = trimmedLine.Split(['='], 2);
                    if (parts.Length != 2) continue;

                    var key = parts[0].Trim();
                    if (settingsToUpdate.TryGetValue(key, out var newValue))
                    {
                        lines[i] = $"{key} = {newValue}";
                        updatedSettings.Remove(key);
                    }
                }
            }

            // Add settings that were not found
            if (sectionFound)
            {
                // Find the end of the section to insert new keys
                var insertIndex = sectionLineIndex + 1;
                while (insertIndex < lines.Count && !string.IsNullOrWhiteSpace(lines[insertIndex]) &&
                       !lines[insertIndex].Trim().StartsWith('['))
                {
                    insertIndex++;
                }

                foreach (var key in updatedSettings.Reverse()) // Insert in reverse to maintain order
                    lines.Insert(insertIndex, $"{key} = {settingsToUpdate[key]}");
            }
            else // Section not found, add it at the end
            {
                lines.Add("");
                lines.Add($"[{section}]");
                foreach (var kvp in settingsToUpdate) lines.Add($"{kvp.Key} = {kvp.Value}");
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Failed to update INI file: {filePath}");
            return false;
        }
    }
}