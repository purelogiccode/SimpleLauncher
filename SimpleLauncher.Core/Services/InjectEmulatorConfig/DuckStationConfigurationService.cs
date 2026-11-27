using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Injects user settings into the DuckStation emulator's settings.ini configuration file.
/// </summary>
public static class DuckStationConfigurationService
{
    /// <summary>
    ///     Applies the saved DuckStation settings to the emulator's settings.ini file,
    ///     creating the file from a bundled sample when it does not exist.
    /// </summary>
    /// <param name="emulatorPath">Path to the DuckStation executable.</param>
    /// <param name="settings">The settings manager containing DuckStation configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        // DuckStation reads '<exeDir>\settings.ini' only in portable mode (portable.txt);
        // installed copies read 'Documents\DuckStation\settings.ini'.
        var isPortable = File.Exists(Path.Combine(emuDir, "portable.txt"));
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!isPortable && string.IsNullOrWhiteSpace(documentsPath))
        {
            // The shell did not resolve a Documents folder (rare, but possible on stripped or
            // redirected profiles). Fall back to the emulator folder instead of writing to the
            // current working directory.
            logger.Debug("[DuckStationConfig] Documents folder unavailable; using the emulator directory.");
            documentsPath = emuDir;
        }

        var configDir = isPortable
            ? emuDir
            : Path.Combine(documentsPath, "DuckStation");

        var configPath = Path.Combine(configDir, "settings.ini");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "DuckStation",
                "settings.ini");
            if (File.Exists(samplePath))
            {
                try
                {
                    // The installed-mode directory may not exist yet.
                    Directory.CreateDirectory(configDir);
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[DuckStationConfig] Created new settings.ini from sample: {configPath}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Expected user-environment condition (read-only, ACL-protected, or OneDrive-redirected
                    // Documents folder). The UI shows guidance and the game still launches, so keep it out
                    // of the bug report.
                    logger.Information(ex,
                        "[DuckStationConfig] Access denied creating settings.ini from sample: {ConfigPath}",
                        configPath);
                    throw new DuckStationPermissionException(
                        $"Cannot write to configuration directory: {configDir}", ex);
                }
                catch (IOException ex)
                {
                    // Expected user-environment condition: the config folder cannot be created (e.g. a
                    // stale or cloud-only OneDrive-redirected Documents folder, a Documents component
                    // blocked by a file, or an unavailable drive). Report file-not-found as an access
                    // condition instead of aborting the game launch.
                    logger.Information(ex,
                        "[DuckStationConfig] Failed to create settings.ini from sample: {ConfigPath}",
                        configPath);
                    throw new DuckStationPermissionException(
                        $"Cannot create configuration directory: {configDir}", ex);
                }
                catch (Exception ex)
                {
                    logger.Debug($"[DuckStationConfig] Failed to create settings.ini from sample: {ex.Message}");
                    logger.Error(ex, $"[DuckStationConfig] Failed to create settings.ini from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("settings.ini not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[DuckStationConfig] Injecting configuration into: {configPath}");

        var mainUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "StartFullscreen", settings.DuckStation.StartFullscreen.ToString().ToLowerInvariant() },
            { "PauseOnFocusLoss", settings.DuckStation.PauseOnFocusLoss.ToString().ToLowerInvariant() },
            { "SaveStateOnExit", settings.DuckStation.SaveStateOnExit.ToString().ToLowerInvariant() },
            { "RewindEnable", settings.DuckStation.RewindEnable.ToString().ToLowerInvariant() },
            { "RunaheadFrameCount", settings.DuckStation.RunaheadFrameCount.ToString(CultureInfo.InvariantCulture) }
        };

        var gpuUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Renderer", settings.DuckStation.Renderer },
            { "ResolutionScale", settings.DuckStation.ResolutionScale.ToString(CultureInfo.InvariantCulture) },
            { "TextureFilter", settings.DuckStation.TextureFilter },
            { "WidescreenHack", settings.DuckStation.WidescreenHack.ToString().ToLowerInvariant() },
            { "PGXPEnable", settings.DuckStation.PgxpEnable.ToString().ToLowerInvariant() }
        };

        var displayUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AspectRatio", settings.DuckStation.AspectRatio },
            { "VSync", settings.DuckStation.Vsync.ToString().ToLowerInvariant() }
        };

        var audioUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "OutputVolume", settings.DuckStation.OutputVolume.ToString(CultureInfo.InvariantCulture) },
            { "OutputMuted", settings.DuckStation.OutputMuted.ToString().ToLowerInvariant() }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            // Expected user-environment condition: keep it out of the bug report.
            logger.Information(ex, "[DuckStationConfig] Access denied reading config: {ConfigPath}", configPath);
            throw new DuckStationPermissionException($"Cannot read configuration file: {configPath}", ex);
        }
        catch (IOException ex)
        {
            // Expected user-environment condition (locked, cloud-only, or otherwise unavailable config
            // file). The UI shows guidance and the game still launches, so keep it out of the bug report.
            logger.Information(ex, "[DuckStationConfig] I/O error reading config: {ConfigPath}", configPath);
            throw new DuckStationPermissionException($"Cannot read configuration file: {configPath}", ex);
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
            Dictionary<string, string>? currentUpdates = null;

            if (currentSection.Equals("[Main]", StringComparison.OrdinalIgnoreCase))
                currentUpdates = mainUpdates;
            else if (currentSection.Equals("[GPU]", StringComparison.OrdinalIgnoreCase))
                currentUpdates = gpuUpdates;
            else if (currentSection.Equals("[Display]", StringComparison.OrdinalIgnoreCase))
                currentUpdates = displayUpdates;
            else if (currentSection.Equals("[Audio]", StringComparison.OrdinalIgnoreCase))
                currentUpdates = audioUpdates;

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
        if (mainUpdates.Count > 0)
        {
            ApplyUpdatesToSection(lines, "[Main]", mainUpdates, out var sectionModified);
            modified |= sectionModified;
        }

        if (gpuUpdates.Count > 0)
        {
            ApplyUpdatesToSection(lines, "[GPU]", gpuUpdates, out var sectionModified);
            modified |= sectionModified;
        }

        if (displayUpdates.Count > 0)
        {
            ApplyUpdatesToSection(lines, "[Display]", displayUpdates, out var sectionModified);
            modified |= sectionModified;
        }

        if (audioUpdates.Count > 0)
        {
            ApplyUpdatesToSection(lines, "[Audio]", audioUpdates, out var sectionModified);
            modified |= sectionModified;
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[DuckStationConfig] Injected configuration changes..");
            }
            catch (UnauthorizedAccessException ex)
            {
                // Expected user-environment condition (read-only or ACL-protected config file).
                logger.Information(ex,
                    "[DuckStationConfig] Access denied writing config: {ConfigPath}", configPath);
                throw new DuckStationPermissionException($"Cannot write to configuration file: {configPath}", ex);
            }
            catch (IOException ex)
            {
                // Expected user-environment condition (locked, cloud-only, or unavailable config file).
                logger.Information(ex, "[DuckStationConfig] I/O error writing config: {ConfigPath}", configPath);
                throw new DuckStationPermissionException($"Cannot write to configuration file: {configPath}", ex);
            }
            catch (Exception ex)
            {
                logger.Debug($"[DuckStationConfig] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[DuckStationConfig] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
        else
        {
            logger.Debug("[DuckStationConfig] No changes needed");
        }
    }

    private static void ApplyUpdatesToSection(List<string> lines, string sectionName,
        Dictionary<string, string> updates, out bool modified)
    {
        if (updates.Count == 0)
        {
            modified = false;
            return;
        }

        var sectionIndex = lines.FindIndex(l => l.Trim().Equals(sectionName, StringComparison.OrdinalIgnoreCase));
        if (sectionIndex == -1)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add("");
            lines.Add(sectionName);
            sectionIndex = lines.Count - 1;
        }

        // Find the end of this section (start of next section or end of file)
        var insertIndex = sectionIndex + 1;
        for (var i = insertIndex; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                // Found next section, insert before it
                insertIndex = i;
                break;
            }

            insertIndex = i + 1;
        }

        foreach (var kvp in updates) lines.Insert(insertIndex++, $"{kvp.Key} = {kvp.Value}");

        modified = true;
    }
}