using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Provides functionality to inject Simple Launcher settings into the PCSX2 emulator configuration file (PCSX2.ini).
/// </summary>
public static class Pcsx2ConfigurationService
{
    /// <summary>
    ///     Injects Simple Launcher configuration settings into the PCSX2 emulator's PCSX2.ini file.
    ///     Handles portable and installed modes, creates the config from a sample if missing, and updates UI, graphics, audio,
    ///     and achievement settings.
    /// </summary>
    /// <param name="emulatorPath">The full path to the PCSX2 emulator executable.</param>
    /// <param name="settings">The settings manager containing PCSX2 configuration values.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = ResolveConfigPath(emuDir, logger);

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "PCSX2", "PCSX2.ini");
            if (File.Exists(samplePath))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath) ??
                                              throw new InvalidOperationException(
                                                  "Could not create directory for PCSX2.ini"));
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[PCSX2Config] Created new PCSX2.ini from sample: {configPath}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Expected user-environment condition (read-only, ACL-protected, or OneDrive-redirected
                    // Documents folder). The UI shows guidance and the game still launches, so keep it out
                    // of the bug report.
                    logger.Information(ex,
                        "[PCSX2Config] Access denied creating PCSX2.ini from sample: {ConfigPath}", configPath);
                    throw new Pcsx2PermissionException(
                        $"Cannot write to configuration directory: {Path.GetDirectoryName(configPath)}", ex);
                }
                catch (IOException ex)
                {
                    // Expected user-environment condition: the config folder cannot be created (e.g. a
                    // stale or cloud-only OneDrive-redirected Documents folder, a path blocked by a file,
                    // or an unavailable drive). The UI shows guidance and the game still launches, so keep
                    // it out of the bug report.
                    logger.Information(ex, "[PCSX2Config] Failed to create PCSX2.ini from sample: {ConfigPath}",
                        configPath);
                    throw new Pcsx2PermissionException(
                        $"Cannot create configuration directory: {Path.GetDirectoryName(configPath)}", ex);
                }
                catch (Exception ex)
                {
                    logger.Debug($"[PCSX2Config] Failed to create PCSX2.ini from sample: {ex.Message}");
                    logger.Error(ex, $"[PCSX2Config] Failed to create PCSX2.ini from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("PCSX2.ini not found and sample is missing.", samplePath);
            }
        }

        var uiUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "StartFullscreen", settings.Pcsx2.StartFullscreen.ToString().ToLowerInvariant() }
        };

        var emuCoreUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "EnableCheats", settings.Pcsx2.EnableCheats.ToString().ToLowerInvariant() },
            { "EnableWideScreenPatches", settings.Pcsx2.EnableWidescreenPatches.ToString().ToLowerInvariant() }
        };

        var gsUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Renderer", settings.Pcsx2.Renderer.ToString(CultureInfo.InvariantCulture) },
            { "upscale_multiplier", settings.Pcsx2.UpscaleMultiplier.ToString(CultureInfo.InvariantCulture) },
            { "AspectRatio", settings.Pcsx2.AspectRatio },
            { "VsyncEnable", settings.Pcsx2.Vsync.ToString().ToLowerInvariant() }
        };

        var audioUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FinalVolume", settings.Pcsx2.Volume.ToString(CultureInfo.InvariantCulture) }
        };

        var achUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Enabled", settings.Pcsx2.AchievementsEnabled.ToString().ToLowerInvariant() },
            { "Hardcore", settings.Pcsx2.AchievementsHardcore.ToString().ToLowerInvariant() }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            // Expected user-environment condition: keep it out of the bug report.
            logger.Information(ex, "[PCSX2Config] Access denied reading PCSX2.ini: {ConfigPath}", configPath);
            throw new Pcsx2PermissionException($"Cannot read configuration file: {configPath}", ex);
        }
        catch (IOException ex)
        {
            // Expected user-environment condition (locked, cloud-only, or otherwise unavailable config
            // file). The UI shows guidance and the game still launches, so keep it out of the bug report.
            logger.Information(ex, "[PCSX2Config] I/O error reading PCSX2.ini: {ConfigPath}", configPath);
            throw new Pcsx2PermissionException($"Cannot read configuration file: {configPath}", ex);
        }

        var modified = false;
        var currentSection = "";

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';')) continue;

            var parts = line.Split('=', 2);
            if (parts.Length < 2) continue;

            var key = parts[0].Trim();
            var currentUpdates = currentSection switch
            {
                "[UI]" => uiUpdates,
                "[EmuCore]" => emuCoreUpdates,
                "[EmuCore/GS]" => gsUpdates,
                "[SPU2/Mixing]" => audioUpdates,
                "[Achievements]" => achUpdates,
                _ => null
            };

            if (currentUpdates != null && currentUpdates.Remove(key, out var newValue))
            {
                var newLine = $"{key} = {newValue}";
                if (!string.Equals(lines[i], newLine, StringComparison.Ordinal))
                {
                    lines[i] = newLine;
                    modified = true;
                }
            }
        }

        // Add missing keys/sections
        if (uiUpdates.Count > 0) ApplyUpdatesToSection(lines, "[UI]", uiUpdates, ref modified);

        if (emuCoreUpdates.Count > 0) ApplyUpdatesToSection(lines, "[EmuCore]", emuCoreUpdates, ref modified);

        if (gsUpdates.Count > 0) ApplyUpdatesToSection(lines, "[EmuCore/GS]", gsUpdates, ref modified);

        if (audioUpdates.Count > 0) ApplyUpdatesToSection(lines, "[SPU2/Mixing]", audioUpdates, ref modified);

        if (achUpdates.Count > 0) ApplyUpdatesToSection(lines, "[Achievements]", achUpdates, ref modified);

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[PCSX2Config] Injected configuration changes..");
            }
            catch (UnauthorizedAccessException ex)
            {
                // Expected user-environment condition (read-only, ACL-protected, or OneDrive-synced config
                // file). The UI shows guidance and the game still launches, so keep it out of the bug report.
                logger.Information(ex, "[PCSX2Config] Access denied writing PCSX2.ini: {ConfigPath}", configPath);
                throw new Pcsx2PermissionException($"Cannot write to configuration file: {configPath}", ex);
            }
            catch (IOException ex)
            {
                // Expected user-environment condition (locked, cloud-only, or otherwise unavailable config
                // file). The UI shows guidance and the game still launches, so keep it out of the bug report.
                logger.Information(ex, "[PCSX2Config] Failed to inject configuration changes: {ConfigPath}",
                    configPath);
                throw new Pcsx2PermissionException($"Cannot write to configuration file: {configPath}", ex);
            }
            catch (Exception ex)
            {
                logger.Debug($"[PCSX2Config] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[PCSX2Config] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
    }

    private static string ResolveConfigPath(string emuDir, ILogger logger)
    {
        // 1. Portable mode: PCSX2 uses a 'portable.ini' marker next to the executable.
        //    In this mode, config lives in '<emuDir>\inis\PCSX2.ini'.
        var portableMarker = Path.Combine(emuDir, "portable.ini");
        if (File.Exists(portableMarker))
        {
            var portableConfigPath = Path.Combine(emuDir, "inis", "PCSX2.ini");
            logger.Debug($"[PCSX2Config] Portable mode detected (portable.ini found). Using: {portableConfigPath}");
            return portableConfigPath;
        }

        // 2. Existing config in the emulator directory (legacy/portable setups without marker)
        var localInisPath = Path.Combine(emuDir, "inis", "PCSX2.ini");
        if (File.Exists(localInisPath))
        {
            logger.Debug($"[PCSX2Config] Using existing config in emulator directory: {localInisPath}");
            return localInisPath;
        }

        var localRootPath = Path.Combine(emuDir, "PCSX2.ini");
        if (File.Exists(localRootPath))
        {
            logger.Debug($"[PCSX2Config] Using existing config in emulator directory: {localRootPath}");
            return localRootPath;
        }

        // 3. Non-portable (installed) mode: PCSX2 stores config in 'Documents\PCSX2\inis\PCSX2.ini'.
        //    This is also the creation target when no config exists, since the emulator
        //    directory may not be writable (e.g., 'C:\Program Files\PCSX2').
        var documentsConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PCSX2",
            "inis",
            "PCSX2.ini");
        logger.Debug($"[PCSX2Config] No portable config found. Using standard install location: {documentsConfigPath}");
        return documentsConfigPath;
    }

    private static void ApplyUpdatesToSection(List<string> lines, string sectionName,
        Dictionary<string, string> updates, ref bool modified)
    {
        var sectionIndex = lines.FindIndex(l => l.Trim().Equals(sectionName, StringComparison.OrdinalIgnoreCase));

        if (sectionIndex == -1)
        {
            lines.Add("");
            lines.Add(sectionName);
            sectionIndex = lines.Count - 1;
        }

        var insertIndex = sectionIndex + 1;
        foreach (var kvp in updates) lines.Insert(insertIndex++, $"{kvp.Key} = {kvp.Value}");

        if (updates.Count > 0) modified = true;
    }
}