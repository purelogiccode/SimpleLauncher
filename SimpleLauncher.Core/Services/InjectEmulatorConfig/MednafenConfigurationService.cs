using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Provides functionality to inject Simple Launcher settings into the Mednafen emulator configuration file
///     (mednafen.cfg).
/// </summary>
public static class MednafenConfigurationService
{
    // List of common system prefixes used by Mednafen for per-system settings
    private static readonly string[] SystemPrefixes =
    [
        "apple2", "gb", "gba", "gg", "lynx", "md", "nes", "ngp", "pce", "pce_fast",
        "pcfx", "psx", "sms", "snes", "snes_faust", "ss", "vb", "wswan"
    ];

    private static readonly char[] Separator = [' ', '\t'];

    /// <summary>
    ///     Injects Simple Launcher configuration settings into the Mednafen emulator's mednafen.cfg file.
    ///     Creates the config from a sample if it does not exist, then updates or appends key-value pairs.
    /// </summary>
    /// <param name="emulatorPath">The full path to the Mednafen emulator executable.</param>
    /// <param name="settings">The settings manager containing Mednafen configuration values.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "mednafen.cfg");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Mednafen", "mednafen.cfg");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[MednafenConfig] Created new mednafen.cfg from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[MednafenConfig] Failed to create mednafen.cfg from sample: {ex.Message}");
                    logger.Error(ex, $"[MednafenConfig] Failed to create mednafen.cfg from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                // If no config and no sample, we can't proceed.
                throw new FileNotFoundException("mednafen.cfg not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[MednafenConfig] Injecting configuration into: {configPath}");

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Global settings
            { "video.driver", settings.Mednafen.VideoDriver },
            { "video.fs", settings.Mednafen.Fullscreen ? "1" : "0" },
            { "video.glvsync", settings.Mednafen.Vsync ? "1" : "0" },
            { "video.blit_timesync", settings.Mednafen.Vsync ? "1" : "0" },
            { "sound.volume", settings.Mednafen.Volume.ToString(CultureInfo.InvariantCulture) },
            { "cheats", settings.Mednafen.Cheats ? "1" : "0" },
            { "state_rewind", settings.Mednafen.Rewind ? "1" : "0" }
        };

        // Add per-system settings for all common systems
        foreach (var prefix in SystemPrefixes)
        {
            updates[$"{prefix}.stretch"] = settings.Mednafen.Stretch;
            updates[$"{prefix}.videoip"] = settings.Mednafen.Bilinear ? "1" : "0";
            updates[$"{prefix}.scanlines"] = settings.Mednafen.Scanlines.ToString(CultureInfo.InvariantCulture);
            updates[$"{prefix}.shader"] = settings.Mednafen.Shader;
            updates[$"{prefix}.special"] = settings.Mednafen.Special;
        }

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Debug($"[MednafenConfig] Access denied reading config: {configPath}");
            logger.Error(ex, $"[MednafenConfig] Access denied reading config: {configPath}");
            throw;
        }
        catch (IOException ex)
        {
            logger.Debug($"[MednafenConfig] I/O error reading config: {configPath}");
            logger.Error(ex, $"[MednafenConfig] I/O error reading config: {configPath}");
            throw;
        }

        var keysFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modified = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';')) continue;

            var parts = line.Split(Separator, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var key = parts[0];
            if (updates.TryGetValue(key, out var newValue))
            {
                var newLine = $"{key} {newValue}";
                if (!string.Equals(lines[i], newLine, StringComparison.Ordinal))
                {
                    lines[i] = newLine;
                    modified = true;
                }

                keysFound.Add(key);
            }
        }

        // Append any keys that were not found in the file
        foreach (var kvp in updates)
        {
            if (!keysFound.Contains(kvp.Key))
            {
                lines.Add($"{kvp.Key} {kvp.Value}");
                modified = true;
            }
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[MednafenConfig] Injected configuration changes..");
            }
            catch (Exception ex)
            {
                logger.Debug($"[MednafenConfig] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[MednafenConfig] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
        else
        {
            logger.Debug("[MednafenConfig] No changes needed");
        }
    }
}