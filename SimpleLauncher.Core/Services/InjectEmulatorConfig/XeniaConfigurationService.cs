using SimpleLauncher.Core.Services.SettingsManager;
using Tomlyn;
using Tomlyn.Model;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Provides functionality to inject Simple Launcher settings into the Xenia emulator configuration files (TOML
///     format).
/// </summary>
public static class XeniaConfigurationService
{
    /// <summary>
    ///     Injects Simple Launcher configuration settings into the Xenia emulator's TOML config files.
    ///     Processes both xenia-canary.config.toml and xenia.config.toml if found, creating them from samples if missing.
    ///     Updates APU, GPU, display, HID, general, storage, and language settings.
    /// </summary>
    /// <param name="emulatorPath">The full path to the Xenia emulator executable.</param>
    /// <param name="settings">The settings manager containing Xenia configuration values.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory is null or empty.");

        // Define all possible config filenames
        string[] configFiles = ["xenia-canary.config.toml", "xenia.config.toml"];
        var processedCount = 0;

        foreach (var fileName in configFiles)
        {
            // 1. Try portable path first (emulator directory)
            var configPath = Path.Combine(emuDir, fileName);

            // 2. If not found in portable location, try Documents folder (standard Xenia location)
            if (!File.Exists(configPath))
            {
                var documentsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Xenia",
                    fileName);
                if (File.Exists(documentsPath)) configPath = documentsPath;
            }

            // The UpdateSingleConfigFile now handles creation from sample if missing.
            // So we don't need to check File.Exists(configPath) here anymore,
            // as it will attempt to create it if not found.
            if (UpdateSingleConfigFile(configPath, settings, logger)) processedCount++;
        }

        if (processedCount == 0)
        {
            // Log the issue instead of throwing to prevent crash when samples are missing
            // or no config files exist. Xenia will use its default settings.
            logger.Debug(
                "[XeniaConfig] WARNING: No configuration files found to inject into. Expected xenia.config.toml or xenia-canary.config.toml in emulator directory or Documents\\Xenia. Xenia will use default settings");
        }
    }

    private static bool UpdateSingleConfigFile(string configPath, SettingsManagerService settings,
        ILogger logger)
    {
        // Backup logic: Create from sample if missing
        if (!File.Exists(configPath))
        {
            var fileName = Path.GetFileName(configPath);
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", fileName);
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[XeniaConfig] Created new {fileName} from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[XeniaConfig] Failed to create {fileName} from sample: {ex.Message}");
                    logger.Error(ex, $"[XeniaConfig] Failed to create {fileName} from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                logger.Debug($"[XeniaConfig] Sample not found for {fileName}, skipping: {samplePath}");
                return false;
            }
        }

        logger.Debug($"[XeniaConfig] Injecting into: {Path.GetFileName(configPath)}");

        var tomlContent = File.ReadAllText(configPath);
        var model = TomlSerializer.Deserialize<TomlTable>(tomlContent) ?? new TomlTable();

        // [APU]
        var apu = GetOrCreateTable("APU");
        apu["apu"] = settings.Xenia.Apu;
        apu["mute"] = settings.Xenia.Mute;

        // [GPU]
        var gpu = GetOrCreateTable("GPU");
        gpu["gpu"] = settings.Xenia.Gpu;
        gpu["vsync"] = settings.Xenia.Vsync;
        gpu["draw_resolution_scale_x"] = settings.Xenia.ResScaleX;
        gpu["draw_resolution_scale_y"] = settings.Xenia.ResScaleY;
        gpu["readback_resolve"] = settings.Xenia.ReadbackResolve;
        gpu["gamma_render_target_as_srgb"] = settings.Xenia.GammaSrgb;

        // [Display]
        var display = GetOrCreateTable("Display");
        display["fullscreen"] = settings.Xenia.Fullscreen;
        display["postprocess_antialiasing"] = settings.Xenia.Aa;
        display["postprocess_scaling_and_sharpening"] = settings.Xenia.Scaling;

        // [HID]
        var hid = GetOrCreateTable("HID");
        hid["hid"] = settings.Xenia.Hid;
        hid["vibration"] = settings.Xenia.Vibration;

        // [General]
        var general = GetOrCreateTable("General");
        general["discord"] = settings.Xenia.DiscordPresence;
        general["apply_patches"] = settings.Xenia.ApplyPatches;

        // [Logging]
        var logging = GetOrCreateTable("Logging");
        logging["enable_console"] = false;

        // [Storage]
        var storage = GetOrCreateTable("Storage");
        storage["mount_cache"] = settings.Xenia.MountCache;

        // [XConfig]
        var xconfig = GetOrCreateTable("XConfig");
        xconfig["user_language"] = settings.Xenia.UserLanguage;

        // Write back
        var updatedToml = TomlSerializer.Serialize(model);
        try
        {
            File.WriteAllText(configPath, updatedToml);
            logger.Debug("[XeniaConfig] Injected configuration changes");
            return true;
        }
        catch (Exception ex)
        {
            logger.Debug($"[XeniaConfig] Failed to inject configuration changes: {ex.Message}");
            logger.Error(ex, $"[XeniaConfig] Failed to inject configuration changes: {ex.Message}");
            throw;
        }

        // Helper to get or create a table (section)
        TomlTable GetOrCreateTable(string key)
        {
            if (model.ContainsKey(key) && model[key] is TomlTable table)
                return table;

            var newTable = new TomlTable();
            model[key] = newTable;
            return newTable;
        }
    }
}