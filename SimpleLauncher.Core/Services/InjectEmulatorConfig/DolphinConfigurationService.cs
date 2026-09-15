using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Injects user settings into the Dolphin emulator's Dolphin.ini configuration file.
/// </summary>
public static class DolphinConfigurationService
{
    /// <summary>
    ///     Determines the active Dolphin configuration paths (portable or global) and
    ///     injects the saved settings into each of them.
    /// </summary>
    /// <param name="emulatorPath">Path to the Dolphin executable.</param>
    /// <param name="settings">The settings manager containing Dolphin configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPaths = new List<string>();

        // 1. Check for Portable Mode (portable.txt exists in emulator root)
        var portableMarker = Path.Combine(emuDir, "portable.txt");
        if (File.Exists(portableMarker))
        {
            var portableConfigDir = Path.Combine(emuDir, "User", "Config");
            configPaths.Add(Path.Combine(portableConfigDir, "Dolphin.ini"));
        }

        // 2. Check for Global/AppData Config
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var globalConfigDir = Path.Combine(appDataPath, "Dolphin Emulator", "Config");
        var globalConfigPath = Path.Combine(globalConfigDir, "Dolphin.ini");

        // Only add global path if the directory exists (Dolphin has been run in non-portable mode)
        if (Directory.Exists(globalConfigDir)) configPaths.Add(globalConfigPath);

        // If neither exists (first run scenario), default to creating the global configuration
        if (configPaths.Count == 0) configPaths.Add(globalConfigPath);

        // Inject into all determined paths
        foreach (var configPath in configPaths) InjectIntoConfigFile(configPath, settings, logger);
    }

    private static void InjectIntoConfigFile(string configPath, SettingsManagerService settings,
        ILogger logger)
    {
        var configDir = Path.GetDirectoryName(configPath);

        // Ensure the config file exists (copy from sample if needed)
        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Dolphin", "Dolphin.ini");
            if (File.Exists(samplePath))
            {
                try
                {
                    if (configDir != null) Directory.CreateDirectory(configDir);
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[DolphinConfig] Created new Dolphin.ini from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    var contextMessage = $"Failed to create Dolphin.ini from sample at {samplePath}";
                    logger.Error(ex, contextMessage);
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("Dolphin.ini not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[DolphinConfig] Injecting configuration into: {configPath}");

        var coreUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "GFXBackend", settings.Dolphin.GfxBackend },
            { "WiimoteContinuousScanning", settings.Dolphin.WiimoteContinuousScanning.ToString() },
            { "WiimoteEnableSpeaker", settings.Dolphin.WiimoteEnableSpeaker.ToString() }
        };

        var dspUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DSPThread", settings.Dolphin.DspThread.ToString() }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Debug($"[DolphinConfig] Access denied reading config: {configPath}");
            logger.Error(ex, $"[DolphinConfig] Access denied reading config: {configPath}");
            throw;
        }
        catch (IOException ex)
        {
            logger.Debug($"[DolphinConfig] I/O error reading config: {configPath}");
            logger.Error(ex, $"[DolphinConfig] I/O error reading config: {configPath}");
            throw;
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

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith('$')) continue;

            var parts = line.Split('=', 2);
            if (parts.Length < 2) continue;

            var key = parts[0].Trim();
            Dictionary<string, string>? currentUpdates = null;

            if (currentSection.Equals("[Core]", StringComparison.OrdinalIgnoreCase))
                currentUpdates = coreUpdates;
            else if (currentSection.Equals("[DSP]", StringComparison.OrdinalIgnoreCase)) currentUpdates = dspUpdates;

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
        if (coreUpdates.Count > 0)
        {
            ApplyUpdatesToSection(lines, "[Core]", coreUpdates);
            modified = true;
        }

        if (dspUpdates.Count > 0)
        {
            ApplyUpdatesToSection(lines, "[DSP]", dspUpdates);
            modified = true;
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[DolphinConfig] Injected configuration changes..");
            }
            catch (Exception ex)
            {
                logger.Debug($"[DolphinConfig] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[DolphinConfig] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
        else
        {
            logger.Debug("[DolphinConfig] No changes needed");
        }
    }

    private static void ApplyUpdatesToSection(List<string> lines, string sectionName,
        Dictionary<string, string> updates)
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
    }
}