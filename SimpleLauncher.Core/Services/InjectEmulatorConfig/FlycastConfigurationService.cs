using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Injects user settings into the Flycast emulator's emu.cfg configuration file.
/// </summary>
public static class FlycastConfigurationService
{
    /// <summary>
    ///     Applies the saved Flycast settings to the emulator's emu.cfg file,
    ///     creating the file from a bundled sample when it does not exist.
    /// </summary>
    /// <param name="emulatorPath">Path to the Flycast executable.</param>
    /// <param name="settings">The settings manager containing Flycast configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "emu.cfg");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Flycast", "emu.cfg");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[FlycastConfig] Created new emu.cfg from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[FlycastConfig] Failed to create emu.cfg from sample: {ex.Message}");
                    logger.Error(ex, $"[FlycastConfig] Failed to create emu.cfg from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("emu.cfg not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[FlycastConfig] Injecting configuration into: {configPath}");

        var windowUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "fullscreen", settings.Flycast.Fullscreen ? "yes" : "no" },
            { "width", settings.Flycast.Width.ToString(CultureInfo.InvariantCulture) },
            { "height", settings.Flycast.Height.ToString(CultureInfo.InvariantCulture) },
            { "maximized", settings.Flycast.Maximized ? "yes" : "no" }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Debug($"[FlycastConfig] Access denied reading config: {configPath}");
            logger.Error(ex, $"[FlycastConfig] Access denied reading config: {configPath}");
            throw;
        }
        catch (IOException ex)
        {
            logger.Debug($"[FlycastConfig] I/O error reading config: {configPath}");
            logger.Error(ex, $"[FlycastConfig] I/O error reading config: {configPath}");
            throw;
        }

        var modified = false;
        var inWindowSection = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inWindowSection = line.Equals("[window]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inWindowSection || string.IsNullOrWhiteSpace(line) || line.StartsWith(';')) continue;

            var parts = line.Split('=', 2);
            if (parts.Length < 2) continue;

            var key = parts[0].Trim();
            if (windowUpdates.TryGetValue(key, out var newValue))
            {
                var newLine = $"{key} = {newValue}";
                if (!string.Equals(lines[i], newLine, StringComparison.Ordinal))
                {
                    lines[i] = newLine;
                    modified = true;
                }

                windowUpdates.Remove(key); // Mark as found
            }
        }

        // Add any missing keys to the [window] section
        if (windowUpdates.Count > 0)
        {
            modified = true;
            var windowIndex =
                lines.FindIndex(static l => l.Trim().Equals("[window]", StringComparison.OrdinalIgnoreCase));
            if (windowIndex != -1)
            {
                var insertIndex = windowIndex + 1;
                while (insertIndex < lines.Count && !string.IsNullOrWhiteSpace(lines[insertIndex]) &&
                       !lines[insertIndex].Trim().StartsWith('['))
                {
                    insertIndex++;
                }

                foreach (var kvp in windowUpdates) lines.Insert(insertIndex++, $"{kvp.Key} = {kvp.Value}");
            }
            else // If [window] section doesn't exist, add it
            {
                lines.Add("");
                lines.Add("[window]");
                foreach (var kvp in windowUpdates) lines.Add($"{kvp.Key} = {kvp.Value}");
            }
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[FlycastConfig] Injected configuration changes..");
            }
            catch (Exception ex)
            {
                logger.Debug($"[FlycastConfig] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[FlycastConfig] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
        else
        {
            logger.Debug("[FlycastConfig] No changes needed");
        }
    }
}