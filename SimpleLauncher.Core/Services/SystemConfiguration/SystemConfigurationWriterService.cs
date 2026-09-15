using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.UnifiedSettings;

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
    ///     Gets whether this instance persists to the unified SQLite database
    ///     (<c>settings.dat</c> in AppData) instead of the legacy <c>system.xml</c>.
    ///     Opt-in per app: the Avalonia app passes <c>true</c>, the WPF app keeps the default <c>false</c>.
    /// </summary>
    public bool UseUnifiedDatabase { get; }

    /// <summary>
    ///     Initializes a new instance of the SystemConfigurationWriterService with the specified dependencies.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logErrors">The logger for error reporting.</param>
    /// <param name="useUnifiedDatabase">
    ///     When true, systems persist to the unified SQLite database instead of <c>system.xml</c>.
    ///     Only the Avalonia app opts in; the WPF app keeps XML.
    /// </param>
    public SystemConfigurationWriterService(IConfiguration configuration, ILogger logErrors,
        bool useUnifiedDatabase = false)
    {
        _configuration = configuration;
        _logger = logErrors;
        UseUnifiedDatabase = useUnifiedDatabase;
        _fileLocation = new DataFileLocation(configuration, "SystemXmlPath", "system.xml");
    }

    /// <summary>
    ///     Asynchronously saves a system configuration, creating or updating the entry.
    ///     In unified-database mode the system is upserted into <c>settings.dat</c>;
    ///     otherwise the legacy XML file path is used exactly as before.
    /// </summary>
    public async Task SaveSystemAsync(ISystemManager systemConfig, string? originalSystemName = null)
    {
        if (UseUnifiedDatabase)
        {
            await Task.Run(() =>
            {
                lock (XmlLock)
                {
                    try
                    {
                        UnifiedSettingsDatabase.EnsureCreated();
                        var config = ToSystemManagerConfig(systemConfig);

                        // Handle renames: remove the old key when the name changed.
                        if (!string.IsNullOrWhiteSpace(originalSystemName) &&
                            !string.Equals(originalSystemName, config.SystemName, StringComparison.OrdinalIgnoreCase))
                        {
                            UnifiedSettingsDatabase.DeleteSystem(originalSystemName);
                        }

                        UnifiedSettingsDatabase.SaveSystem(config.SystemName, SystemConfigStore.Serialize(config));
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error(ex, "Error saving system configuration to the unified database");
                        throw;
                    }
                }
            });
            return;
        }

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
                        _logger?.Error(ex, "Error loading system.xml for saving");
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
            _logger?.Error(ex, "Error saving system configuration");
            throw;
        }
    }

    /// <summary>
    ///     Asynchronously deletes a system configuration entry by name.
    ///     In unified-database mode the row is deleted from <c>settings.dat</c>;
    ///     otherwise the legacy XML file path is used exactly as before.
    /// </summary>
    public async Task DeleteSystemAsync(string systemName)
    {
        if (UseUnifiedDatabase)
        {
            await Task.Run(() =>
            {
                lock (XmlLock)
                {
                    try
                    {
                        UnifiedSettingsDatabase.EnsureCreated();
                        UnifiedSettingsDatabase.DeleteSystem(systemName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error(ex, $"Error deleting system '{systemName}' from the unified database.");
                    }
                }
            });
            return;
        }

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
    ///     Checks whether a system configuration with the specified name exists.
    ///     In unified-database mode the check runs against <c>settings.dat</c>;
    ///     otherwise the legacy XML file path is used exactly as before.
    /// </summary>
    public bool SystemExists(string systemName)
    {
        if (UseUnifiedDatabase)
        {
            lock (XmlLock)
            {
                try
                {
                    if (!UnifiedSettingsDatabase.IsValidDatabase())
                        return false;
                    return UnifiedSettingsDatabase.SystemExists(systemName);
                }
                catch
                {
                    return false;
                }
            }
        }

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

    /// <summary>Converts an <see cref="ISystemManager" /> to a concrete config for database serialization.</summary>
    private static SystemManagerConfig ToSystemManagerConfig(ISystemManager config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new SystemManagerConfig
        {
            SystemName = config.SystemName,
            SystemFolders = config.SystemFolders?.ToList() ?? [],
            SystemImageFolder = config.SystemImageFolder ?? "",
            FileFormatsToSearch = config.FileFormatsToSearch?.ToList() ?? [],
            FileFormatsToLaunch = config.FileFormatsToLaunch?.ToList() ?? [],
            ExtractFileBeforeLaunch = config.ExtractFileBeforeLaunch,
            GroupByFolder = config.GroupByFolder,
            DisableRecursiveSearch = config.DisableRecursiveSearch,
            Emulators = config.Emulators?.Select(static e => new Emulator
            {
                EmulatorName = e.EmulatorName ?? "",
                EmulatorLocation = e.EmulatorLocation ?? "",
                EmulatorParameters = e.EmulatorParameters ?? "",
                ReceiveANotificationOnEmulatorError = e.ReceiveANotificationOnEmulatorError,
                ImagePackDownloadLink = e.ImagePackDownloadLink ?? "",
                ImagePackDownloadLink2 = e.ImagePackDownloadLink2 ?? "",
                ImagePackDownloadLink3 = e.ImagePackDownloadLink3 ?? "",
                ImagePackDownloadLink4 = e.ImagePackDownloadLink4 ?? "",
                ImagePackDownloadLink5 = e.ImagePackDownloadLink5 ?? "",
                ImagePackDownloadExtractPath = e.ImagePackDownloadExtractPath ?? ""
            }).ToList() ?? []
        };
    }
}