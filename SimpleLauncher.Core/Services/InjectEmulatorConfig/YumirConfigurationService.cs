using SimpleLauncher.Core.Services.SettingsManager;
using Tomlyn;
using Tomlyn.Model;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Provides functionality to inject Simple Launcher settings into the Ymir emulator configuration file (Ymir.toml).
/// </summary>
public static class YumirConfigurationService
{
    /// <summary>
    ///     Injects Simple Launcher configuration settings into the Ymir emulator's Ymir.toml file.
    ///     Creates the config from a sample if it does not exist, then updates video, audio, system, and general settings.
    /// </summary>
    /// <param name="emulatorPath">The full path to the Ymir emulator executable.</param>
    /// <param name="settings">The settings manager containing Ymir configuration values.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "Ymir.toml");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Yumir", "Ymir.toml");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[YumirConfig] Created new Ymir.toml from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[YumirConfig] Failed to create Ymir.toml from sample: {ex.Message}");
                    logger.Error(ex, $"[YumirConfig] Failed to create Ymir.toml from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("Ymir.toml not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[YumirConfig] Injecting configuration into: {configPath}");

        var tomlContent = File.ReadAllText(configPath);
        var model = TomlSerializer.Deserialize<TomlTable>(tomlContent) ?? new TomlTable();

        // [Video]
        var video = GetOrCreateTable(model, "Video");
        video["FullScreen"] = settings.Yumir.Fullscreen;
        video["ForceAspectRatio"] = settings.Yumir.ForceAspectRatio;
        video["ForcedAspect"] = settings.Yumir.ForcedAspect;
        video["ReduceLatency"] = settings.Yumir.ReduceLatency;

        // [Audio]
        var audio = GetOrCreateTable(model, "Audio");
        audio["Volume"] = settings.Yumir.Volume;
        audio["Mute"] = settings.Yumir.Mute;

        // [System]
        var system = GetOrCreateTable(model, "System");
        system["VideoStandard"] = settings.Yumir.VideoStandard;
        system["AutoDetectRegion"] = settings.Yumir.AutoDetectRegion;

        // [General]
        var general = GetOrCreateTable(model, "General");
        general["PauseWhenUnfocused"] = settings.Yumir.PauseWhenUnfocused;

        var updatedToml = TomlSerializer.Serialize(model);
        try
        {
            File.WriteAllText(configPath, updatedToml);
            logger.Debug("[YumirConfig] Injected configuration changes");
        }
        catch (Exception ex)
        {
            logger.Debug($"[YumirConfig] Failed to inject configuration changes: {ex.Message}");
            logger.Error(ex, $"[YumirConfig] Failed to inject configuration changes: {ex.Message}");
            throw;
        }
    }

    private static TomlTable GetOrCreateTable(TomlTable model, string key)
    {
        if (model.ContainsKey(key) && model[key] is TomlTable table)
            return table;

        var newTable = new TomlTable();
        model[key] = newTable;
        return newTable;
    }
}