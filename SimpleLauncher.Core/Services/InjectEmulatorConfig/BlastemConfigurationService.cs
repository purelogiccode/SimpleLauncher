using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Injects user settings into the Blastem emulator's default.cfg configuration file.
/// </summary>
public static partial class BlastemConfigurationService
{
    private static readonly char[] Separator = [' ', '\t'];

    /// <summary>
    ///     Applies the saved Blastem settings to the emulator's default.cfg file,
    ///     creating the file from a bundled sample when it does not exist.
    /// </summary>
    /// <param name="emulatorPath">Path to the Blastem executable.</param>
    /// <param name="settings">The settings manager containing Blastem configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "default.cfg");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Blastem", "default.cfg");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[BlastemConfig] Created new default.cfg from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[BlastemConfig] Failed to create default.cfg from sample: {ex.Message}");
                    logger.Error(ex, $"[BlastemConfig] Failed to create default.cfg from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("default.cfg not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[BlastemConfig] Injecting configuration into: {configPath}");

        var updates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "fullscreen", settings.Blastem.Fullscreen ? "on" : "off" },
            { "vsync", settings.Blastem.Vsync ? "on" : "off" },
            { "aspect", settings.Blastem.Aspect },
            { "scaling", settings.Blastem.Scaling },
            { "scanlines", settings.Blastem.Scanlines ? "on" : "off" },
            { "rate", settings.Blastem.AudioRate.ToString(CultureInfo.InvariantCulture) },
            { "sync_source", settings.Blastem.SyncSource }
        };

        List<string> lines;
        try
        {
            lines = File.ReadAllLines(configPath, new UTF8Encoding(false)).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Debug($"[BlastemConfig] Access denied reading config: {configPath}");
            logger.Error(ex, $"[BlastemConfig] Access denied reading config: {configPath}");
            throw;
        }
        catch (IOException ex)
        {
            logger.Debug($"[BlastemConfig] I/O error reading config: {configPath}");
            logger.Error(ex, $"[BlastemConfig] I/O error reading config: {configPath}");
            throw;
        }

        var modified = false;

        // Map configuration keys to their expected parent blocks for scope validation
        var keyBlocks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "fullscreen", "video" },
            { "vsync", "video" },
            { "aspect", "video" },
            { "scaling", "video" },
            { "scanlines", "video" },
            { "rate", "audio" },
            { "sync_source", "system" }
        };

        // Use a stack to properly track nested block scopes
        var blockStack = new Stack<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var originalLine = lines[i];
            var trimmedLine = originalLine.Trim();

            switch (trimmedLine)
            {
                // Track block scope for hierarchical config format
                case "}":
                    if (blockStack.Count > 0) blockStack.Pop();

                    continue;
            }

            // Detect block start (e.g., "video {")
            if (trimmedLine.EndsWith('{') && !trimmedLine.StartsWith('#'))
            {
                var blockName = trimmedLine.Substring(0, trimmedLine.Length - 1).Trim();
                blockStack.Push(blockName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#')) continue;

            var parts = trimmedLine.Split(Separator, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var key = parts[0];
            if (!updates.TryGetValue(key, out var newValue)) continue;

            // Validate scope: only update keys when inside their expected block
            var currentBlock = blockStack.Count > 0 ? blockStack.Peek() : "";
            if (keyBlocks.TryGetValue(key, out var expectedBlock) &&
                !string.Equals(currentBlock, expectedBlock, StringComparison.Ordinal))
            {
                continue; // Key found in wrong scope (e.g., comment or user custom section), skip it
            }

            // Preserve original indentation and trailing comments
            var indentMatch = MyRegex().Match(originalLine);
            var indent = indentMatch.Value;

            var commentIndex = originalLine.IndexOf('#');
            var comment = commentIndex >= 0 ? originalLine.Substring(commentIndex) : "";

            var newLine = $"{indent}{key} {newValue}{(string.IsNullOrEmpty(comment) ? "" : " " + comment)}";
            if (string.Equals(originalLine, newLine, StringComparison.Ordinal)) continue;

            lines[i] = newLine;
            modified = true;
        }

        if (modified)
        {
            try
            {
                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
                logger.Debug("[BlastemConfig] Injected configuration changes..");
            }
            catch (Exception ex)
            {
                logger.Debug($"[BlastemConfig] Failed to inject configuration changes: {ex.Message}");
                logger.Error(ex, $"[BlastemConfig] Failed to inject configuration changes: {ex.Message}");
                throw;
            }
        }
        else
        {
            logger.Debug("[BlastemConfig] No changes needed for Blastem configuration");
        }
    }

    [GeneratedRegex(@"^\s*", RegexOptions.None, 1000)]
    private static partial Regex MyRegex();
}