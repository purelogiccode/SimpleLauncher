using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.SystemConfiguration;

/// <summary>
///     Handles reading and writing system configuration to the system.xml file, including save, delete, and existence
///     checks.
/// </summary>
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
public class SystemConfigurationWriterService : ISystemConfigurationWriterService
{
    private static readonly Lock XmlLock = new();
    private readonly IConfiguration _configuration;
    private readonly DataFileLocation _fileLocation;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the SystemConfigurationWriterService with the specified dependencies.
    /// </summary>
    public SystemConfigurationWriterService(IConfiguration configuration, ILogger logErrors)
    {
        _configuration = configuration;
        _logger = logErrors;
        _fileLocation = new DataFileLocation(configuration, "SystemXmlPath", "system.xml");
    }

    /// <summary>
    ///     Asynchronously saves a system configuration to the XML file, creating or updating the entry.
    /// </summary>
    public async Task SaveSystemAsync(ISystemManager systemConfig, string? originalSystemName = null)
    {
        try
        {
            await Task.Run(() =>
            {
                lock (XmlLock)
                {
                    var systemXmlPath = _fileLocation.FilePath;
                    XDocument xmlDoc;

                    try
                    {
                        if (File.Exists(systemXmlPath))
                        {
                            var xmlContent = File.ReadAllText(systemXmlPath);
                            xmlDoc = string.IsNullOrWhiteSpace(xmlContent)
                                ? new XDocument(new XElement("SystemConfigs"))
                                : XDocument.Parse(xmlContent);

                            if (xmlDoc.Root == null || xmlDoc.Root.Name != "SystemConfigs")
                                xmlDoc = new XDocument(new XElement("SystemConfigs"));
                        }
                        else
                        {
                            xmlDoc = new XDocument(new XElement("SystemConfigs"));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error(ex, "Error loading system.xml for saving.");
                        throw new InvalidOperationException("Failed to load system configuration for saving.", ex);
                    }

                    var root = xmlDoc.Root;
                    var systemIdentifier = originalSystemName ?? systemConfig.SystemName;

                    if (root != null)
                    {
                        // OrdinalIgnoreCase to match SystemExists: "nes" must find "NES"
                        // instead of adding a duplicate node (CORE-26).
                        var existingSystem = root.Elements("SystemConfig")
                            .FirstOrDefault(el => string.Equals(el.Element("SystemName")?.Value, systemIdentifier,
                                StringComparison.OrdinalIgnoreCase));

                        if (existingSystem != null)
                            UpdateSystemXElement(existingSystem, systemConfig);
                        else
                            root.Add(CreateSystemXElement(systemConfig));

                        // Sort alphabetically
                        var sortedSystems = root.Elements("SystemConfig")
                            .OrderBy(static s => s.Element("SystemName")?.Value, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        root.RemoveNodes();
                        root.Add(sortedSystems);
                    }

                    // Save with retry logic
                    const int maxRetries = 3;
                    var retryDelayMs = 500;
                    Exception? lastException = null;

                    for (var attempt = 0; attempt < maxRetries; attempt++)
                    {
                        try
                        {
                            var tempPath = systemXmlPath + ".tmp";
                            var settings = new XmlWriterSettings
                            {
                                Indent = true,
                                IndentChars = "  ",
                                NewLineHandling = NewLineHandling.Replace,
                                Encoding = Encoding.UTF8
                            };

                            byte[] xmlBytes;
                            using (var ms = new MemoryStream())
                            {
                                using (var writer = XmlWriter.Create(ms, settings))
                                {
                                    xmlDoc.Declaration ??= new XDeclaration("1.0", "utf-8", null);
                                    xmlDoc.Save(writer);
                                }

                                xmlBytes = ms.ToArray();
                            }

                            if (xmlBytes.Length == 0)
                                throw new InvalidOperationException("Generated system XML is empty.");

                            File.WriteAllBytes(tempPath, xmlBytes);
                            File.Move(tempPath, systemXmlPath, true);
                            return;
                        }
                        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                        {
                            lastException = ex;
                            if (attempt < maxRetries - 1)
                            {
                                try
                                {
                                    var tempPath = systemXmlPath + ".tmp";
                                    if (File.Exists(tempPath)) File.Delete(tempPath);
                                }
                                catch
                                {
                                    /* ignore cleanup errors */
                                }

                                Thread.Sleep(retryDelayMs);
                                retryDelayMs *= 2;
                            }
                        }
                    }

                    throw new InvalidOperationException("Failed to save system configuration.", lastException);
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error saving system configuration.");
            throw;
        }
    }

    /// <summary>
    ///     Asynchronously deletes a system configuration entry by name from the XML file.
    /// </summary>
    public async Task DeleteSystemAsync(string systemName)
    {
        try
        {
            await Task.Run(() =>
            {
                lock (XmlLock)
                {
                    var systemXmlPath = _fileLocation.FilePath;
                    if (!File.Exists(systemXmlPath)) return;

                    XDocument xmlDoc;
                    try
                    {
                        var settings = new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Prohibit,
                            XmlResolver = null
                        };
                        using var reader = XmlReader.Create(systemXmlPath, settings);
                        xmlDoc = XDocument.Load(reader, LoadOptions.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error(ex, $"Error loading system.xml for deleting system '{systemName}'.");
                        return;
                    }

                    // OrdinalIgnoreCase to match SystemExists, so Delete("nes") removes "NES" (CORE-26).
                    var systemNode = xmlDoc.Root?.Descendants("SystemConfig")
                        .FirstOrDefault(el =>
                            string.Equals(el.Element("SystemName")?.Value, systemName,
                                StringComparison.OrdinalIgnoreCase));

                    if (systemNode != null)
                    {
                        systemNode.Remove();

                        // Atomic write via temp file (WPF SystemManagerService parity)
                        var tempPath = systemXmlPath + ".tmp";
                        var settings = new XmlWriterSettings
                        {
                            Indent = true,
                            IndentChars = "  ",
                            NewLineHandling = NewLineHandling.Replace,
                            Encoding = Encoding.UTF8
                        };

                        byte[] xmlBytes;
                        using (var ms = new MemoryStream())
                        {
                            using (var writer = XmlWriter.Create(ms, settings))
                            {
                                xmlDoc.Declaration ??= new XDeclaration("1.0", "utf-8", null);
                                xmlDoc.Save(writer);
                            }

                            xmlBytes = ms.ToArray();
                        }

                        if (xmlBytes.Length == 0)
                            throw new InvalidOperationException("Generated system XML is empty after delete.");

                        File.WriteAllBytes(tempPath, xmlBytes);
                        File.Move(tempPath, systemXmlPath, true);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, $"Error deleting system '{systemName}'.");
        }
    }

    /// <summary>
    ///     Checks whether a system configuration with the specified name exists in the XML file.
    /// </summary>
    public bool SystemExists(string systemName)
    {
        lock (XmlLock)
        {
            var systemXmlPath = _fileLocation.FilePath;
            if (!File.Exists(systemXmlPath)) return false;

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using var reader = XmlReader.Create(systemXmlPath, settings);
                var doc = XDocument.Load(reader, LoadOptions.None);

                return doc.Root?.Elements("SystemConfig")
                    .Any(el => string.Equals(el.Element("SystemName")?.Value, systemName,
                        StringComparison.OrdinalIgnoreCase)) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    private static XElement CreateSystemXElement(ISystemManager config)
    {
        return new XElement("SystemConfig",
            new XElement("SystemName", config.SystemName),
            new XElement("SystemFolders", config.SystemFolders.Select(static f => new XElement("SystemFolder", f))),
            new XElement("SystemImageFolder", config.SystemImageFolder),
            new XElement("FileFormatsToSearch",
                config.FileFormatsToSearch.Select(static f => new XElement("FormatToSearch", f))),
            new XElement("GroupByFolder", config.GroupByFolder),
            new XElement("DisableRecursiveSearch", config.DisableRecursiveSearch),
            config.ExtractFileBeforeLaunch ? new XElement("ExtractFileBeforeLaunch", true) : null,
            new XElement("FileFormatsToLaunch",
                config.FileFormatsToLaunch.Select(static f => new XElement("FormatToLaunch", f))),
            new XElement("Emulators", config.Emulators.Select(CreateEmulatorXElement))
        );
    }

    private static void UpdateSystemXElement(XElement existingSystem, ISystemManager config)
    {
        existingSystem.SetElementValue("SystemName", config.SystemName);

        var foldersElement = existingSystem.Element("SystemFolders");
        if (foldersElement == null)
        {
            foldersElement = new XElement("SystemFolders");
            existingSystem.Element("SystemName")?.AddAfterSelf(foldersElement);
        }

        foldersElement.ReplaceNodes(config.SystemFolders.Select(static f => new XElement("SystemFolder", f)));

        existingSystem.SetElementValue("SystemImageFolder", config.SystemImageFolder);
        existingSystem.Element("FileFormatsToSearch")
            ?.ReplaceNodes(config.FileFormatsToSearch.Select(static f => new XElement("FormatToSearch", f)));
        existingSystem.SetElementValue("GroupByFolder", config.GroupByFolder);
        existingSystem.SetElementValue("DisableRecursiveSearch", config.DisableRecursiveSearch);
        existingSystem.SetElementValue("ExtractFileBeforeLaunch", config.ExtractFileBeforeLaunch ? true : null);
        existingSystem.Element("FileFormatsToLaunch")
            ?.ReplaceNodes(config.FileFormatsToLaunch.Select(static f => new XElement("FormatToLaunch", f)));

        existingSystem.Element("Emulators")?.Remove();
        existingSystem.Add(new XElement("Emulators", config.Emulators.Select(CreateEmulatorXElement)));
    }

    private static XElement CreateEmulatorXElement(IEmulator emulator)
    {
        var element = new XElement("Emulator",
            new XElement("EmulatorName", emulator.EmulatorName),
            new XElement("EmulatorLocation", emulator.EmulatorLocation),
            new XElement("EmulatorParameters", emulator.EmulatorParameters),
            new XElement("ReceiveANotificationOnEmulatorError", emulator.ReceiveANotificationOnEmulatorError)
        );

        if (!string.IsNullOrEmpty(emulator.ImagePackDownloadLink))
            element.Add(new XElement("ImagePackDownloadLink", emulator.ImagePackDownloadLink));
        if (!string.IsNullOrEmpty(emulator.ImagePackDownloadLink2))
            element.Add(new XElement("ImagePackDownloadLink2", emulator.ImagePackDownloadLink2));
        if (!string.IsNullOrEmpty(emulator.ImagePackDownloadLink3))
            element.Add(new XElement("ImagePackDownloadLink3", emulator.ImagePackDownloadLink3));
        if (!string.IsNullOrEmpty(emulator.ImagePackDownloadLink4))
            element.Add(new XElement("ImagePackDownloadLink4", emulator.ImagePackDownloadLink4));
        if (!string.IsNullOrEmpty(emulator.ImagePackDownloadLink5))
            element.Add(new XElement("ImagePackDownloadLink5", emulator.ImagePackDownloadLink5));
        if (!string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath))
            element.Add(new XElement("ImagePackDownloadExtractPath", emulator.ImagePackDownloadExtractPath));

        return element;
    }
}