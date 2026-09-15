using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Provides functionality to inject Simple Launcher settings into the Raine emulator configuration file
///     (raine32_sdl.cfg).
/// </summary>
public static class RaineConfigurationService
{
    /// <summary>
    ///     Injects Simple Launcher configuration settings into the Raine emulator's raine32_sdl.cfg file.
    ///     Creates the config from a sample if it does not exist, then updates display, sound, general, and directory
    ///     settings.
    ///     Also handles Neo Geo CD specific settings when a disc image is detected.
    /// </summary>
    /// <param name="emulatorPath">The full path to the Raine emulator executable.</param>
    /// <param name="settings">The settings manager containing Raine configuration values.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="gameFilePath">
    ///     The optional path to the game file being launched, used to detect Neo Geo CD mode and
    ///     determine the ROM directory.
    /// </param>
    /// <param name="systemRomPath">The optional system-level ROM directory path as a fallback for the ROM directory setting.</param>
    /// <param name="raineCustomRomDirectory">The optional custom ROM directory configured specifically for Raine.</param>
    public static void InjectSettings(
        string emulatorPath,
        SettingsManagerService settings,
        ILogger logger,
        string? gameFilePath = null,
        string? systemRomPath = null,
        string? raineCustomRomDirectory = null)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir)) throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "config", "raine32_sdl.cfg");
        var configDir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

        // If config is missing, copy from sample
        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Raine", "raine32_sdl.cfg");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[RaineConfig] Created new config from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to create Raine config from sample");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("Raine configuration file not found and sample is missing.",
                    samplePath);
            }
        }

        // Determine if we are in NeoGeo CD mode
        var ext = !string.IsNullOrEmpty(gameFilePath) ? Path.GetExtension(gameFilePath).ToLowerInvariant() : "";
        var isNeoGeoCd = ext is ".cue" or ".iso" or ".bin" or ".chd";
        var gameDir = !string.IsNullOrEmpty(gameFilePath) ? Path.GetDirectoryName(gameFilePath) : null;

        var updates = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Display"] = new(StringComparer.OrdinalIgnoreCase)
            {
                { "fullscreen", settings.Raine.Fullscreen ? "1" : "0" },
                { "screen_x", settings.Raine.ResX.ToString(CultureInfo.InvariantCulture) },
                { "screen_y", settings.Raine.ResY.ToString(CultureInfo.InvariantCulture) },
                { "fix_aspect_ratio", settings.Raine.FixAspectRatio ? "1" : "0" },
                { "ogl_dbuf", settings.Raine.Vsync ? "2" : "0" }
            },
            ["Sound"] = new(StringComparer.OrdinalIgnoreCase)
            {
                { "driver", settings.Raine.SoundDriver },
                { "sample_rate", settings.Raine.SampleRate.ToString(CultureInfo.InvariantCulture) }
            },
            ["General"] = new(StringComparer.OrdinalIgnoreCase)
            {
                { "frame_skip", settings.Raine.FrameSkip.ToString(CultureInfo.InvariantCulture) },
                { "ShowFPS", settings.Raine.ShowFps ? "1" : "0" }
            },
            ["Directories"] = new(StringComparer.OrdinalIgnoreCase),
            ["neocd"] = new(StringComparer.OrdinalIgnoreCase)
        };

        // Inject rom_dir_0
        // Priority: 1. Custom RaineRomDirectory from settings, 2. Game directory (if arcade), 3. System PrimarySystemFolder
        string? effectiveRomDir = null;
        if (!string.IsNullOrEmpty(raineCustomRomDirectory) && Directory.Exists(raineCustomRomDirectory))
            effectiveRomDir = raineCustomRomDirectory;
        else if (!isNeoGeoCd && !string.IsNullOrEmpty(gameDir))
            effectiveRomDir = gameDir;
        else if (!string.IsNullOrEmpty(systemRomPath)) effectiveRomDir = systemRomPath;

        if (!string.IsNullOrEmpty(effectiveRomDir))
        {
            updates["Directories"]["rom_dir_0"] = effectiveRomDir.EndsWith(Path.DirectorySeparatorChar)
                ? effectiveRomDir
                : effectiveRomDir + Path.DirectorySeparatorChar;
        }

        // Inject NeoGeo CD specific settings
        if (isNeoGeoCd)
        {
            if (!string.IsNullOrEmpty(gameDir))
            {
                updates["neocd"]["neocd_dir"] = gameDir.EndsWith(Path.DirectorySeparatorChar)
                    ? gameDir
                    : gameDir + Path.DirectorySeparatorChar;
            }

            updates["neocd"]["neocd_bios"] = settings.Raine.NeoCdBios;
            updates["neocd"]["music_volume"] = settings.Raine.MusicVolume.ToString(CultureInfo.InvariantCulture);
            updates["neocd"]["sfx_volume"] = settings.Raine.SfxVolume.ToString(CultureInfo.InvariantCulture);
            updates["neocd"]["mute_sfx"] = settings.Raine.MuteSfx ? "1" : "0";
            updates["neocd"]["mute_music"] = settings.Raine.MuteMusic ? "1" : "0";
        }

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Debug($"[RaineConfig] Access denied reading config: {configPath}");
            logger.Error(ex, $"[RaineConfig] Access denied reading config: {configPath}");
            throw;
        }
        catch (IOException ex)
        {
            logger.Debug($"[RaineConfig] I/O error reading config: {configPath}");
            logger.Error(ex, $"[RaineConfig] I/O error reading config: {configPath}");
            throw;
        }

        var modified = false;
        string? currentSection = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmedLine = line.Trim(); // Trim once for logic checks
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            // Robust section header detection
            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                currentSection = trimmedLine.Trim('[', ']').Trim();
                continue;
            }

            if (currentSection != null && updates.TryGetValue(currentSection, out var sectionUpdates))
            {
                var parts = trimmedLine.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    if (sectionUpdates.TryGetValue(key, out var newValue))
                    {
                        var newLine = $"{key} = {newValue}";
                        if (!string.Equals(lines[i].Trim(), newLine,
                                StringComparison.Ordinal)) // Compare trimmed to avoid false positives on indentation
                        {
                            lines[i] = newLine;
                            modified = true;
                        }

                        sectionUpdates.Remove(key);
                    }
                }
            }
        }

        // Add missing keys/sections
        foreach (var section in updates)
        {
            if (section.Value.Count > 0)
            {
                modified = true;
                var sectionHeader = $"[{section.Key}]";
                var sectionIndex =
                    lines.FindIndex(l => l.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase));
                if (sectionIndex == -1)
                {
                    lines.Add("");
                    lines.Add(sectionHeader);
                    sectionIndex = lines.Count - 1;
                }

                foreach (var kvp in section.Value) lines.Insert(sectionIndex + 1, $"{kvp.Key} = {kvp.Value}");
            }
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[RaineConfig] Configuration injected");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to inject Raine configuration");
                throw;
            }
        }
    }
}