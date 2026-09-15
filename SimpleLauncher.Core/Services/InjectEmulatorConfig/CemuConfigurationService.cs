using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Injects user settings into the Cemu emulator's settings.xml configuration file.
/// </summary>
public static class CemuConfigurationService
{
    /// <summary>
    ///     Applies the saved Cemu settings to the emulator's settings.xml file,
    ///     creating the file from a bundled sample when it does not exist.
    /// </summary>
    /// <param name="emulatorPath">Path to the Cemu executable.</param>
    /// <param name="settings">The settings manager containing Cemu configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "settings.xml");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Cemu", "settings.xml");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[CemuConfig] Created new settings.xml from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[CemuConfig] Failed to create settings.xml from sample: {ex.Message}");
                    logger.Error(ex, $"[CemuConfig] Failed to create settings.xml from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("settings.xml not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[CemuConfig] Injecting configuration into: {configPath}");

        try
        {
            var doc = XDocument.Load(configPath);
            var content = doc.Element("content");

            if (content == null) throw new InvalidDataException("Invalid Cemu settings.xml format.");

            // Root level elements
            SetOrUpdateElement(content, "fullscreen", settings.Cemu.Fullscreen.ToString().ToLowerInvariant());
            SetOrUpdateElement(content, "use_discord_presence",
                settings.Cemu.DiscordPresence.ToString().ToLowerInvariant());
            SetOrUpdateElement(content, "console_language",
                settings.Cemu.ConsoleLanguage.ToString(CultureInfo.InvariantCulture));

            // Graphic Section
            var graphic = GetOrCreateElement(content, "Graphic");
            SetOrUpdateElement(graphic!, "api", settings.Cemu.GraphicApi.ToString(CultureInfo.InvariantCulture));
            SetOrUpdateElement(graphic!, "VSync", settings.Cemu.Vsync.ToString(CultureInfo.InvariantCulture));
            SetOrUpdateElement(graphic!, "AsyncCompile", settings.Cemu.AsyncCompile.ToString().ToLowerInvariant());

            // Audio Section
            var audio = GetOrCreateElement(content, "Audio");
            SetOrUpdateElement(audio!, "TVVolume", settings.Cemu.TvVolume.ToString(CultureInfo.InvariantCulture));

            // Preserve original formatting by using XmlWriter
            var writerSettings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                Encoding = Encoding.UTF8
            };
            using var writer = XmlWriter.Create(configPath, writerSettings);
            doc.Save(writer);

            logger.Debug("[CemuConfig] Injection successful");
        }
        catch (Exception ex)
        {
            logger.Debug($"[CemuConfig] Error: {ex.Message}");
            throw;
        }
    }

    private static XElement? GetOrCreateElement(XElement parent, string name)
    {
        if (name != null)
        {
            var el = parent.Element(name);
            if (el != null) return el;

            el = new XElement(name);
            parent.Add(el);
            return el;
        }

        return null;
    }

    private static void SetOrUpdateElement(XElement parent, string name, string value)
    {
        if (name != null)
        {
            var el = parent.Element(name);
            if (el != null)
                el.Value = value;
            else parent.Add(new XElement(name, value));
        }
    }
}