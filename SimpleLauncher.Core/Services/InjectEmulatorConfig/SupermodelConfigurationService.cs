using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Provides functionality to inject Simple Launcher settings into the Supermodel emulator configuration file
///     (Supermodel.ini).
/// </summary>
public static class SupermodelConfigurationService
{
    /// <summary>
    ///     Injects Simple Launcher configuration settings into the Supermodel emulator's Supermodel.ini file under the
    ///     [Global] section.
    ///     Creates the config from a sample if it does not exist, then updates rendering, display, audio, and input settings.
    /// </summary>
    /// <param name="emulatorPath">The full path to the Supermodel emulator executable.</param>
    /// <param name="settings">The settings manager containing Supermodel configuration values.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir)) throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "Config", "Supermodel.ini");

        // Supermodel often puts the ini in a 'Config' subfolder, but check root too
        if (!File.Exists(configPath))
        {
            var rootPath = Path.Combine(emuDir, "Supermodel.ini");
            if (File.Exists(rootPath)) configPath = rootPath;
        }

        // Backup logic: Create from your sample if missing
        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Supermodel",
                "Supermodel.ini");
            if (File.Exists(samplePath))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath) ??
                                              throw new InvalidOperationException(
                                                  "Could not create directory for Supermodel.ini"));
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[SupermodelConfig] Created Supermodel.ini from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[SupermodelConfig] Failed to create Supermodel.ini from sample: {ex.Message}");
                    logger.Error(ex, $"[SupermodelConfig] Failed to create Supermodel.ini from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("Supermodel.ini not found and sample missing.");
            }
        }

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "New3DEngine", settings.Supermodel.New3DEngine ? "1" : "0" },
            { "QuadRendering", settings.Supermodel.QuadRendering ? "1" : "0" },
            { "FullScreen", settings.Supermodel.Fullscreen ? "1" : "0" },
            { "XResolution", settings.Supermodel.ResX.ToString(CultureInfo.InvariantCulture) },
            { "YResolution", settings.Supermodel.ResY.ToString(CultureInfo.InvariantCulture) },
            { "WideScreen", settings.Supermodel.WideScreen ? "1" : "0" },
            { "Stretch", settings.Supermodel.Stretch ? "1" : "0" },
            { "VSync", settings.Supermodel.Vsync ? "1" : "0" },
            { "Throttle", settings.Supermodel.Throttle ? "1" : "0" },
            { "MusicVolume", settings.Supermodel.MusicVolume.ToString(CultureInfo.InvariantCulture) },
            { "SoundVolume", settings.Supermodel.SoundVolume.ToString(CultureInfo.InvariantCulture) },
            { "InputSystem", GetValidInputSystem(settings.Supermodel.InputSystem) },
            { "MultiThreaded", settings.Supermodel.MultiThreaded ? "1" : "0" },
            { "PowerPCFrequency", settings.Supermodel.PowerPcFrequency.ToString(CultureInfo.InvariantCulture) }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Debug($"[SupermodelConfig] Access denied reading config: {configPath}");
            logger.Error(ex, $"[SupermodelConfig] Access denied reading config: {configPath}");
            throw;
        }
        catch (IOException ex)
        {
            logger.Debug($"[SupermodelConfig] I/O error reading config: {configPath}");
            logger.Error(ex, $"[SupermodelConfig] I/O error reading config: {configPath}");
            throw;
        }

        var keysFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inGlobalSection = false;
        var globalSectionIndex = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();

            if (line.Equals("[Global]", StringComparison.OrdinalIgnoreCase))
            {
                inGlobalSection = true;
                globalSectionIndex = i;
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inGlobalSection = false;
                continue;
            }

            if (!inGlobalSection || string.IsNullOrWhiteSpace(line) || line.StartsWith(';')) continue;

            var parts = line.Split('=', 2);
            if (parts.Length < 2) continue;

            var key = parts[0].Trim();
            if (updates.TryGetValue(key, out var newValue))
            {
                lines[i] = $"{key} = {newValue}";
                keysFound.Add(key);
            }
        }

        // If keys were not found, add them to the [Global] section
        var keysToAdd = updates.Keys.Where(k => !keysFound.Contains(k)).ToList();
        if (keysToAdd.Count != 0)
        {
            if (globalSectionIndex != -1)
            {
                // Find the end of the [Global] section or end of file
                var insertIndex = globalSectionIndex + 1;
                while (insertIndex < lines.Count && !lines[insertIndex].Trim().StartsWith('[')) insertIndex++;

                foreach (var key in keysToAdd) lines.Insert(insertIndex++, $"{key} = {updates[key]}");
            }
            else // If [Global] section doesn't exist, add it with the missing keys
            {
                lines.Add("");
                lines.Add("[Global]");
                foreach (var key in keysToAdd) lines.Add($"{key} = {updates[key]}");
            }
        }

        try
        {
            File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
            logger.Debug("[SupermodelConfig] Injected configuration changes");
        }
        catch (Exception ex)
        {
            logger.Debug($"[SupermodelConfig] Fail to inject configuration changes: {ex.Message}");
            logger.Error(ex, $"[SupermodelConfig] Fail to inject configuration changes: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Validates and returns a valid InputSystem value.
    ///     Defaults to "xinput" if the provided value is null, empty, or invalid.
    /// </summary>
    private static string GetValidInputSystem(string inputSystem)
    {
        // Valid Supermodel input systems: xinput, dinput, rawinput
        if (string.IsNullOrWhiteSpace(inputSystem))
            return "xinput";

        var normalized = inputSystem.Trim().ToLowerInvariant();
        return normalized is "xinput" or "dinput" or "rawinput" ? normalized : "xinput";
    }
}