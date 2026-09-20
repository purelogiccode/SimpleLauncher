using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Injects user settings into the Azahar emulator's qt-config.ini configuration file.
/// </summary>
public static class AzaharConfigurationService
{
    /// <summary>
    ///     Injects settings into Azahar's qt-config.ini file.
    /// </summary>
    /// <param name="emulatorPath">Path to the Azahar executable.</param>
    /// <param name="settings">The settings manager containing Azahar configuration.</param>
    /// <param name="logger"></param>
    /// <exception cref="InvalidOperationException">Thrown when emulator directory is not found.</exception>
    /// <exception cref="FileNotFoundException">Thrown when config file and sample are both missing.</exception>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir)) throw new InvalidOperationException("Emulator directory not found.");

        // Azahar usually looks for qt-config.ini in the same folder or AppData.
        // We assume portable mode/local config first.
        var configPath = Path.Combine(emuDir, "qt-config.ini");

        // Check if we have write access to the emulator directory
        if (!IsDirectoryWritable(emuDir, logger))
        {
            logger.Debug($"[AzaharConfig] Directory is not writable: {emuDir}");
            throw new AzaharPermissionException($"Cannot write to emulator directory: {emuDir}");
        }

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Azahar", "qt-config.ini");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[AzaharConfig] Created new qt-config.ini from sample: {configPath}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Expected user-environment condition (read-only or ACL-protected emulator directory).
                    // The UI shows guidance and the game still launches, so keep it out of the bug report.
                    logger.Information(ex,
                        "[AzaharConfig] Access denied creating qt-config.ini from sample: {ConfigPath}",
                        configPath);
                    throw new AzaharPermissionException($"Cannot write to emulator directory: {emuDir}", ex);
                }
                catch (IOException ex)
                {
                    // Expected user-environment condition: the config cannot be created (e.g. a path
                    // blocked by a file, a locked file, or an unavailable drive). The UI shows guidance
                    // and the game still launches, so keep it out of the bug report.
                    logger.Information(ex, "[AzaharConfig] Failed to create qt-config.ini from sample: {ConfigPath}",
                        configPath);
                    throw new AzaharPermissionException($"Cannot create configuration file: {configPath}", ex);
                }
                catch (Exception ex)
                {
                    logger.Debug($"[AzaharConfig] Failed to create qt-config.ini from sample: {ex.Message}");
                    logger.Error(ex, $"[AzaharConfig] Failed to create qt-config.ini from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("qt-config.ini not found and sample is missing.");
            }
        }

        var updates = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Renderer"] = new(StringComparer.Ordinal)
            {
                { "graphics_api", settings.Azahar.GraphicsApi.ToString(CultureInfo.InvariantCulture) },
                { "resolution_factor", settings.Azahar.ResolutionFactor.ToString(CultureInfo.InvariantCulture) },
                { "use_vsync_new", BoolToString(settings.Azahar.UseVsync) },
                { "async_shader_compilation", BoolToString(settings.Azahar.AsyncShaderCompilation) }
            },
            ["UI"] = new(StringComparer.Ordinal)
            {
                { "fullscreen", BoolToString(settings.Azahar.Fullscreen) }
            },
            ["Audio"] = new(StringComparer.Ordinal)
            {
                { "volume", (settings.Azahar.Volume / 100.0).ToString("F2", CultureInfo.InvariantCulture) },
                { "enable_audio_stretching", BoolToString(settings.Azahar.EnableAudioStretching) }
            },
            ["System"] = new(StringComparer.Ordinal)
            {
                { "is_new_3ds", BoolToString(settings.Azahar.IsNew3Ds) }
            },
            ["Layout"] = new(StringComparer.Ordinal)
            {
                { "layout_option", settings.Azahar.LayoutOption.ToString(CultureInfo.InvariantCulture) }
            }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            // Expected user-environment condition: keep it out of the bug report.
            logger.Information(ex, "[AzaharConfig] Access denied reading qt-config.ini: {ConfigPath}", configPath);
            throw new AzaharPermissionException($"Cannot read configuration file: {configPath}", ex);
        }
        catch (IOException ex)
        {
            // Expected user-environment condition (locked, cloud-only, or otherwise unavailable config
            // file). The UI shows guidance and the game still launches, so keep it out of the bug report.
            logger.Information(ex, "[AzaharConfig] I/O error reading qt-config.ini: {ConfigPath}", configPath);
            throw new AzaharPermissionException($"Cannot read configuration file: {configPath}", ex);
        }

        var modified = false;

        foreach (var (sectionName, dictionary) in updates)
        {
            var sectionHeader = $"[{sectionName}]";

            // 1. Find or Create Section
            var sectionStartIndex =
                lines.FindIndex(l => l.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase));
            if (sectionStartIndex == -1)
            {
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1])) lines.Add("");
                lines.Add(sectionHeader);
                sectionStartIndex = lines.Count - 1;
                modified = true;
            }

            // 2. Determine Section Range
            var sectionEndIndex = lines.FindIndex(sectionStartIndex + 1, static l => l.Trim().StartsWith('['));
            if (sectionEndIndex == -1) sectionEndIndex = lines.Count;

            foreach (var (key, value) in dictionary)
            {
                var defaultKey = $"{key}\\default";
                var keyIndex = -1;
                var defaultKeyIndex = -1;

                // 3. Search for existing keys within this section only
                for (var i = sectionStartIndex + 1; i < sectionEndIndex; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
                        keyIndex = i;
                    else if (trimmed.StartsWith($"{defaultKey}=", StringComparison.OrdinalIgnoreCase))
                        defaultKeyIndex = i;
                }

                // 4. Update or Insert Main Key
                var keyLine = $"{key}={value}";
                if (keyIndex != -1)
                {
                    if (!string.Equals(lines[keyIndex], keyLine, StringComparison.Ordinal))
                    {
                        lines[keyIndex] = keyLine;
                        modified = true;
                    }
                }
                else
                {
                    lines.Insert(sectionEndIndex, keyLine);
                    keyIndex = sectionEndIndex;
                    sectionEndIndex++;
                    modified = true;
                }

                // 5. Update or Insert \default Key (always false when injected)
                var defaultLine = $"{defaultKey}=false";
                if (defaultKeyIndex != -1)
                {
                    if (!string.Equals(lines[defaultKeyIndex], defaultLine, StringComparison.Ordinal))
                    {
                        lines[defaultKeyIndex] = defaultLine;
                        modified = true;
                    }
                }
                else
                {
                    lines.Insert(keyIndex + 1, defaultLine);
                    sectionEndIndex++;
                    modified = true;
                }
            }
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[AzaharConfig] Injected configuration changes..");
            }
            catch (UnauthorizedAccessException ex)
            {
                // Expected user-environment condition (read-only or ACL-protected config file).
                // The UI shows guidance and the game still launches, so keep it out of the bug report.
                logger.Information(ex, "[AzaharConfig] Access denied writing qt-config.ini: {ConfigPath}", configPath);
                throw new AzaharPermissionException($"Cannot write to configuration file: {configPath}", ex);
            }
            catch (IOException ex)
            {
                // Expected user-environment condition (locked, cloud-only, or otherwise unavailable config
                // file). The UI shows guidance and the game still launches, so keep it out of the bug report.
                logger.Information(ex, "[AzaharConfig] Failed to inject configuration changes: {ConfigPath}",
                    configPath);
                throw new AzaharPermissionException($"Cannot write to configuration file: {configPath}", ex);
            }
            catch (Exception ex)
            {
                logger.Debug($"[AzaharConfig] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[AzaharConfig] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    ///     Checks if a directory is writable by attempting to create a temporary file.
    /// </summary>
    private static bool IsDirectoryWritable(string dirPath, ILogger logger)
    {
        try
        {
            var testFile = Path.Combine(dirPath, $".write_test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.Debug($"[AzaharConfig] Error checking directory writability: {ex.Message}");
            return false;
        }
    }

    private static string BoolToString(bool value)
    {
        return value ? "true" : "false";
    }
}