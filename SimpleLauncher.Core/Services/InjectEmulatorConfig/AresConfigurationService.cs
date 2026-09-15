using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Injects user settings into the Ares emulator's settings.bml configuration file.
/// </summary>
public static class AresConfigurationService
{
    /// <summary>
    ///     Applies the saved Ares settings to the emulator's settings.bml file,
    ///     creating the file from a bundled sample when it does not exist.
    /// </summary>
    /// <param name="emulatorPath">Path to the Ares executable.</param>
    /// <param name="settings">The settings manager containing Ares configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "settings.bml");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Ares", "settings.bml");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[AresConfig] Created new settings.bml from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[AresConfig] Failed to create settings.bml from sample: {ex.Message}");
                    logger.Error(ex, $"[AresConfig] Failed to create settings.bml from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("settings.bml not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[AresConfig] Injecting configuration into: {configPath}");

        // Section-aware updates to prevent cross-section key collisions (e.g., Driver: appears in Video, Audio, Input)
        var updates = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Video"] = new(StringComparer.OrdinalIgnoreCase)
            {
                { "Driver:", settings.Ares.VideoDriver },
                { "Exclusive:", settings.Ares.Exclusive.ToString().ToLowerInvariant() },
                { "Shader:", settings.Ares.Shader },
                { "Multiplier:", settings.Ares.Multiplier.ToString(CultureInfo.InvariantCulture) },
                { "Output:", settings.Ares.AspectCorrection }
            },
            ["Audio"] = new(StringComparer.OrdinalIgnoreCase)
            {
                { "Mute:", settings.Ares.Mute.ToString().ToLowerInvariant() },
                { "Volume:", settings.Ares.Volume.ToString("F1", CultureInfo.InvariantCulture) }
            },
            ["Boot"] = new(StringComparer.OrdinalIgnoreCase)
            {
                { "Fast:", settings.Ares.FastBoot.ToString().ToLowerInvariant() }
            },
            ["General"] = new(StringComparer.OrdinalIgnoreCase)
            {
                { "Rewind:", settings.Ares.Rewind.ToString().ToLowerInvariant() },
                { "RunAhead:", settings.Ares.RunAhead.ToString().ToLowerInvariant() },
                { "AutoSaveMemory:", settings.Ares.AutoSaveMemory.ToString().ToLowerInvariant() }
            }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Debug($"[AresConfig] Access denied reading config: {configPath}");
            logger.Error(ex, $"[AresConfig] Access denied reading config: {configPath}");
            throw;
        }
        catch (IOException ex)
        {
            logger.Debug($"[AresConfig] I/O error reading config: {configPath}");
            logger.Error(ex, $"[AresConfig] I/O error reading config: {configPath}");
            throw;
        }

        var modified = false;
        string? currentSection = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmedLine = line.TrimStart();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            // Detect section headers: not indented and contains no colon (e.g., "Video", "Audio", "Input")
            if (!char.IsWhiteSpace(line[0]) && !trimmedLine.Contains(':'))
            {
                currentSection = trimmedLine;
                continue;
            }

            // Only process if we're in a section with pending updates
            if (currentSection == null || !updates.TryGetValue(currentSection, out var sectionUpdates))
                continue;

            // Parse property lines (e.g., "  Driver: OpenGL 3.2")
            var lineParts = trimmedLine.Split(':', 2);
            if (lineParts.Length < 1) continue;

            var lineKey = lineParts[0] + ":";

            if (sectionUpdates.TryGetValue(lineKey, out var newValue))
            {
                var indent = line[..^trimmedLine.Length];
                var newLine = $"{indent}{lineKey} {newValue}";
                if (!string.Equals(lines[i], newLine, StringComparison.Ordinal))
                {
                    lines[i] = newLine;
                    modified = true;
                }
            }
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[AresConfig] Injected configuration changes..");
            }
            catch (Exception ex)
            {
                logger.Debug($"[AresConfig] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[AresConfig] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
        else
        {
            logger.Debug("[AresConfig] No changes needed");
        }
    }
}