using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.UnifiedSettings;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.SystemManager;

/// <summary>
///     Represents a system (console/platform) configuration loaded from system.xml, including ROM folders, file formats,
///     and emulators. The app persists systems in the unified SQLite database
///     (<c>settings.dat</c> in AppData) when it exists; the legacy <c>system.xml</c>
///     format is only read during the one-time migration (and as a fallback when no
///     database exists yet).
/// </summary>
public partial class SystemManagerService : ISystemManager
{
    private static readonly Lock XmlLock = new();

    private static DataFileLocation? _fileLocation;
    private static readonly Lock FileLocationLock = new();

    /// <summary>
    ///     Static logger shared across static methods. Set by the last instance created.
    ///     This is intentional — static methods like SystemExists and LoadSystemManagers need logging.
    /// </summary>
    private static ILogger _logger = null!;

    // ReSharper disable once NotAccessedField.Local
    private readonly IMessageBoxLibraryService _messageBoxLibrary;

    /// <summary>
    ///     Initializes a new instance of the SystemManagerService with optional dependencies.
    /// </summary>
    public SystemManagerService(IMessageBoxLibraryService? messageBoxLibrary = null, ILogger? logger = null)
    {
        _messageBoxLibrary = messageBoxLibrary!;
        _logger = logger!;
    }

    /// <summary>Gets the list of configured emulators for this system.</summary>
    public IList<Emulator> Emulators { get; init; } = null!;

    /// <summary>Gets whether the system.xml file is stored in portable mode (next to the executable).</summary>
    public static bool IsPortableMode
    {
        get
        {
            lock (XmlLock)
            {
                return _fileLocation?.IsPortableMode ?? true;
            }
        }
    }

    /// <summary>Gets the name of the system (e.g., "NES", "SNES").</summary>
    public string SystemName { get; init; } = null!;

    /// <summary>Gets the list of ROM folder paths for this system.</summary>
    public IList<string> SystemFolders { get; init; } = null!;

    /// <summary>Gets the first (primary) system folder path.</summary>
    public string? PrimarySystemFolder => SystemFolders?.FirstOrDefault();

    /// <summary>Gets the path to the folder containing system images.</summary>
    public string SystemImageFolder { get; init; } = null!;

    /// <summary>Gets the list of file extensions to search for in ROM folders.</summary>
    public IList<string> FileFormatsToSearch { get; init; } = null!;

    /// <summary>Gets whether compressed files should be extracted before launching.</summary>
    public bool ExtractFileBeforeLaunch { get; init; }

    /// <summary>Gets the list of file extensions that can be launched directly.</summary>
    public IList<string> FileFormatsToLaunch { get; init; } = null!;

    IReadOnlyList<IEmulator> ISystemManager.Emulators => Emulators?.Cast<IEmulator>().ToList() ?? [];

    /// <summary>Gets whether games should be grouped by their parent folder.</summary>
    public bool GroupByFolder { get; init; }

    /// <summary>Gets whether recursive folder searching is disabled.</summary>
    public bool DisableRecursiveSearch { get; init; }

    /// <summary>
    ///     Gets the path to system.xml, determining the location based on portable mode availability.
    ///     This method ensures the path is initialized and returns the appropriate location.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The full path to system.xml.</returns>
    private static string GetSystemXmlPath(IConfiguration configuration)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (Volatile.Read(ref _fileLocation) == null)
        {
            lock (FileLocationLock)
            {
                _fileLocation ??= new DataFileLocation(configuration, "SystemXmlPath", "system.xml");
            }
        }

        return Volatile.Read(ref _fileLocation)!.FilePath;
    }

    /// <summary>
    ///     Checks whether a system configuration with the specified name exists in
    ///     <c>settings.dat</c> (unified database) or system.xml.
    /// </summary>
    public static bool SystemExists(string systemName, IConfiguration configuration)
    {
        lock (XmlLock)
        {
            if (UnifiedSettingsDatabase.IsValidDatabase())
            {
                try
                {
                    return UnifiedSettingsDatabase.SystemExists(systemName);
                }
                catch (Exception ex)
                {
                    _logger?.Debug($"[SystemManagerService.SystemExists] Could not check settings.dat: {ex.Message}");
                    return false;
                }
            }

            var systemXmlPath = GetSystemXmlPath(configuration);
            if (!File.Exists(systemXmlPath)) return false;

            try
            {
                // Use settings to prevent DTD processing for security
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using var reader = XmlReader.Create(systemXmlPath, settings);
                var doc = XDocument.Load(reader, LoadOptions.None);

                return doc.Root?.Elements("SystemConfig")
                    .Any(el => el.Element("SystemName")?.Value.Equals(systemName, StringComparison.OrdinalIgnoreCase) ??
                               false) ?? false;
            }
            catch (Exception ex) // Catch XmlException, IOException, etc.
            {
                // If file is corrupt or locked, we can't check.
                _logger?.Debug($"[SystemManagerService.SystemExists] Could not check system.xml: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    ///     Loads all system manager configurations from system.xml, validating and cleaning invalid entries.
    /// </summary>
    public static IList<SystemManagerService> LoadSystemManagers(IConfiguration configuration,
        ILogger? logErrors = null, IMessageBoxLibraryService? messageBoxLibrary = null)
    {
        // Synchronous facade (used by non-async callers such as MainWindow ctor, EmulatorPathResolver and tests).
        // The lambda is executed on the thread pool so that any internal awaits never capture the UI
        // SynchronizationContext. Dialogs shown from the pool thread run on that thread
        // (WpfMessageDialogService never hops to the dispatcher), so a UI thread blocked
        // here cannot deadlock against a pool thread waiting for the UI (WPF-02).
        return Task.Run(() =>
                LoadSystemManagersInternalAsync(configuration, logErrors, messageBoxLibrary))
            .GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Asynchronously loads all system manager configurations from system.xml, validating and cleaning invalid entries.
    /// </summary>
    public static Task<IList<SystemManagerService>> LoadSystemManagersAsync(IConfiguration configuration,
        ILogger? logErrors = null, IMessageBoxLibraryService? messageBoxLibrary = null)
    {
        return LoadSystemManagersInternalAsync(configuration, logErrors, messageBoxLibrary);
    }

    private static async Task<IList<SystemManagerService>> LoadSystemManagersInternalAsync(IConfiguration configuration,
        ILogger? logErrors = null, IMessageBoxLibraryService? messageBoxLibrary = null)
    {
        // Unified-database path: single source of truth once migrated.
        if (UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                return await Task.Run(LoadSystemsFromDatabase);
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, "Error loading systems from the unified database; trying the legacy file");
            }
        }

        var systemXmlPath = GetSystemXmlPath(configuration);

        if (!File.Exists(systemXmlPath))
        {
            var directoryPath = Path.GetDirectoryName(systemXmlPath);

            if (directoryPath != null)
                await RestoreBackupFileAsync(directoryPath, systemXmlPath, logErrors!, messageBoxLibrary!);

            // If no backup was restored, create a new empty system.xml file
            if (!File.Exists(systemXmlPath))
            {
                try
                {
                    // Create a new XDocument with the root element
                    var emptyDoc = new XDocument(new XElement("SystemConfigs"));
                    emptyDoc.Save(systemXmlPath);
                    // No user notification needed for creating an expected empty file
                }
                catch (Exception createEx)
                {
                    // Notify developer
                    const string contextMessage = "Error creating empty 'system.xml'.";
                    logErrors?.Error(createEx, contextMessage);

                    // Notify user
                    if (messageBoxLibrary != null)
                    {
                        _ = messageBoxLibrary.SystemXmlIsCorruptedMessageBoxAsync(
                            PathHelper.ResolveLogFilePath(configuration));
                    }

                    return []; // Return an empty list
                }
            }
        }

        lock (XmlLock)
        {
            try
            {
                var systemManagers = new List<SystemManagerService>();
                var invalidManagers = new Dictionary<XElement, string>();
                XDocument? doc = null;

                try
                {
                    var settings = new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null
                    };

                    using var reader = XmlReader.Create(systemXmlPath, settings);
                    doc = XDocument.Load(reader, LoadOptions.None);

                    if (doc.Root != null)
                    {
                        foreach (var sysConfigElement in doc.Root.Elements("SystemConfig"))
                        {
                            try
                            {
                                systemManagers.Add(ParseSystemConfiguration(sysConfigElement));
                            }
                            catch (Exception ex)
                            {
                                var systemName = sysConfigElement.Element("SystemName")?.Value ?? "Unnamed System";
                                if (!invalidManagers.ContainsKey(sysConfigElement))
                                {
                                    invalidManagers[sysConfigElement] =
                                        $"The system '{systemName}' was removed due to the following error(s):\n";
                                }

                                invalidManagers[sysConfigElement] += $"- {ex.Message}\n";
                            }
                        }
                    }
                }
                catch (XmlException ex)
                {
                    logErrors?.Error(ex, "Structural corruption in 'system.xml'. Attempting partial recovery");

                    // Create a fresh document for rebuilding
                    doc = new XDocument(new XElement("SystemConfigs"));
                    var structuralErrorKey = new XElement("StructuralCorruption");

                    try
                    {
                        var rawXml = File.ReadAllText(systemXmlPath);
                        var matches = MyRegex().Matches(rawXml);
                        foreach (Match match in matches)
                        {
                            try
                            {
                                var sysConfigElement = XElement.Parse(match.Value);
                                systemManagers.Add(ParseSystemConfiguration(sysConfigElement));
                            }
                            catch (Exception innerEx)
                            {
                                var nameMatch = MyRegex1().Match(match.Value);
                                var sysName = nameMatch.Success ? nameMatch.Groups[1].Value : "Unknown";

                                invalidManagers.TryAdd(structuralErrorKey,
                                    "The following systems were removed due to structural corruption in the XML:\n");

                                invalidManagers[structuralErrorKey] += $"- {sysName} (Unrecoverable XML block)\n";

                                _logger?.Debug($"Failed to validate system configuration for '{sysName}'");
                                logErrors?.Error(innerEx, $"Failed to validate system configuration for '{sysName}'");
                            }
                        }
                    }
                    catch (Exception fatalEx)
                    {
                        _logger?.Debug($"Failed to perform regex recovery on system.xml: {fatalEx.Message}");
                        logErrors?.Error(fatalEx, "Failed to perform regex recovery on system.xml");
                    }

                    // If no systems could be recovered, the file is completely corrupted
                    if (systemManagers.Count == 0 && invalidManagers.Count == 0)
                    {
                        logErrors?.Error(ex,
                            "No systems could be recovered from 'system.xml'. The file is completely corrupted");

                        if (messageBoxLibrary != null)
                        {
                            _ = messageBoxLibrary.SystemXmlIsCorruptedMessageBoxAsync(
                                PathHelper.ResolveLogFilePath(configuration));
                        }

                        return [];
                    }
                }
                catch (IOException ex)
                {
                    logErrors?.Error(ex, "The file 'system.xml' is locked");
                    if (messageBoxLibrary != null) _ = messageBoxLibrary.FileSystemXmlIsLockedMessageBoxAsync();
                }

                if (doc?.Root == null) return systemManagers;

                // Rebuild the XML document from the valid, in-memory configurations
                var newRoot = new XElement("SystemConfigs");
                foreach (var config in systemManagers.OrderBy(static c => c.SystemName,
                             StringComparer.OrdinalIgnoreCase))
                {
                    newRoot.Add(new XElement("SystemConfig",
                        new XElement("SystemName", config.SystemName),
                        new XElement("SystemFolders",
                            config.SystemFolders.Select(static f => new XElement("SystemFolder", f))),
                        new XElement("SystemImageFolder", config.SystemImageFolder),
                        new XElement("FileFormatsToSearch",
                            config.FileFormatsToSearch.Select(static f => new XElement("FormatToSearch", f))),
                        new XElement("GroupByFolder", config.GroupByFolder),
                        new XElement("DisableRecursiveSearch", config.DisableRecursiveSearch),
                        config.ExtractFileBeforeLaunch ? new XElement("ExtractFileBeforeLaunch", true) : null,
                        new XElement("FileFormatsToLaunch",
                            config.FileFormatsToLaunch.Select(static f => new XElement("FormatToLaunch", f))),
                        new XElement("Emulators", config.Emulators.Select(static e =>
                            new XElement("Emulator",
                                new XElement("EmulatorName", e.EmulatorName),
                                new XElement("EmulatorLocation", e.EmulatorLocation),
                                new XElement("EmulatorParameters", e.EmulatorParameters),
                                new XElement("ReceiveANotificationOnEmulatorError",
                                    e.ReceiveANotificationOnEmulatorError),
                                string.IsNullOrEmpty(e.ImagePackDownloadLink)
                                    ? null
                                    : new XElement("ImagePackDownloadLink", e.ImagePackDownloadLink),
                                string.IsNullOrEmpty(e.ImagePackDownloadLink2)
                                    ? null
                                    : new XElement("ImagePackDownloadLink2", e.ImagePackDownloadLink2),
                                string.IsNullOrEmpty(e.ImagePackDownloadLink3)
                                    ? null
                                    : new XElement("ImagePackDownloadLink3", e.ImagePackDownloadLink3),
                                string.IsNullOrEmpty(e.ImagePackDownloadLink4)
                                    ? null
                                    : new XElement("ImagePackDownloadLink4", e.ImagePackDownloadLink4),
                                string.IsNullOrEmpty(e.ImagePackDownloadLink5)
                                    ? null
                                    : new XElement("ImagePackDownloadLink5", e.ImagePackDownloadLink5),
                                string.IsNullOrEmpty(e.ImagePackDownloadExtractPath)
                                    ? null
                                    : new XElement("ImagePackDownloadExtractPath", e.ImagePackDownloadExtractPath)
                            )
                        ))
                    ));
                }

                doc.Root.ReplaceNodes(newRoot.Nodes());

                // Notify user about each invalid configuration that was removed
                foreach (var error in invalidManagers.Values)
                {
                    // Notify user
                    if (messageBoxLibrary != null)
                        _ = messageBoxLibrary.InvalidSystemConfigurationMessageBoxAsync(error);
                }

                // Save the cleaned, sorted, and reformatted document back to disk.
                try
                {
                    // Save the XML document with indentation and new lines on attributes
                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        NewLineOnAttributes = false
                    };
                    using var writer = XmlWriter.Create(systemXmlPath, settings);
                    doc.Save(writer);
                }
                catch (Exception saveEx)
                {
                    // Notify developer
                    const string contextMessage = "Error saving 'system.xml' after loading, cleaning, and sorting.";
                    logErrors?.Error(saveEx, contextMessage);
                }

                // Return the list of valid system configurations (could be empty)
                return systemManagers;
            }
            catch (Exception ex)
            {
                // Notify developer
                const string contextMessage = "Error loading system configurations from 'system.xml'.";
                logErrors?.Error(ex, contextMessage);

                // Notify user
                if (messageBoxLibrary != null)
                {
                    _ = messageBoxLibrary.SystemXmlIsCorruptedMessageBoxAsync(
                        PathHelper.ResolveLogFilePath(configuration));
                }

                return []; // Return an empty list
            }
        }
    }

    /// <summary>
    ///     Parses and validates a single &lt;SystemConfig&gt; element. Throws when the
    ///     configuration is invalid (callers log and skip the entry).
    ///     Shared by the XML loader and the one-time legacy migration, so both paths
    ///     enforce identical validation.
    /// </summary>
    private static SystemManagerService ParseSystemConfiguration(XElement sysConfigElement)
    {
        // Attempt to parse each system configuration.
        // These validations will only run if SystemManagerService elements exist.
        var systemName = sysConfigElement.Element("SystemName")?.Value;
        if (string.IsNullOrEmpty(systemName))
            throw new InvalidOperationException("Missing or empty 'System Name' in XML.");

        List<string> systemFolders;
        var systemFoldersElement = sysConfigElement.Element("SystemFolders");
        if (systemFoldersElement != null)
        {
            systemFolders = systemFoldersElement.Elements("SystemFolder")
                .Select(static f => f.Value)
                .Where(static f => !string.IsNullOrWhiteSpace(f))
                .ToList();
        }
        else
        {
            var singleFolder = sysConfigElement.Element("SystemFolder")?.Value;
            systemFolders = !string.IsNullOrWhiteSpace(singleFolder) ? [singleFolder] : new List<string>();
        }

        if (systemFolders.Count == 0)
        {
            throw new InvalidOperationException(
                $"System '{systemName}': At least one 'System Folder' is required in XML.");
        }

        var systemImageFolder = sysConfigElement.Element("SystemImageFolder")?.Value;
        if (string.IsNullOrEmpty(systemImageFolder))
        {
            throw new InvalidOperationException(
                $"System '{systemName}': Missing or empty 'System Image Folder' in XML.");
        }

        // Validate FileFormatsToSearch
        var formatsToSearch = sysConfigElement.Element("FileFormatsToSearch")
            ?.Elements("FormatToSearch")
            .Select(static e => e.Value.Trim())
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value)) // Ensure no empty or whitespace-only entries
            .ToList();
        if (formatsToSearch == null || formatsToSearch.Count == 0)
        {
            throw new InvalidOperationException(
                $"System '{systemName}': 'File Extension To Search' should have at least one value.");
        }

        // Validate ExtractFileBeforeLaunch
        var extractFileBeforeLaunch = false;
        var extractElement = sysConfigElement.Element("ExtractFileBeforeLaunch");
        if (extractElement != null)
        {
            if (!bool.TryParse(extractElement.Value, out extractFileBeforeLaunch))
            {
                // If parsing fails, we could either throw or default to false.
                // Given we want it to be optional, defaulting to false is safer.
                extractFileBeforeLaunch = false;
            }
        }

        if (extractFileBeforeLaunch && (!formatsToSearch.All(static f =>
                f.Equals("zip", StringComparison.OrdinalIgnoreCase) ||
                f.Equals("7z", StringComparison.OrdinalIgnoreCase) ||
                f.Equals("rar", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException(
                $"System '{systemName}': When 'Extract File Before Launch' is set to true, 'Extension to Search in the System Folder' must ONLY contain 'zip', '7z', or 'rar'.");
        }

        // Validate FileFormatsToLaunch
        var formatsToLaunch = sysConfigElement.Element("FileFormatsToLaunch")
            ?.Elements("FormatToLaunch")
            .Select(static e => e.Value.Trim())
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value)) // Ensure no empty or whitespace-only entries
            .ToList();
        // If ExtractFileBeforeLaunch is true, FileFormatsToLaunch must have values.
        if (extractFileBeforeLaunch && (formatsToLaunch == null || formatsToLaunch.Count == 0))
        {
            throw new InvalidOperationException(
                $"System '{systemName}': 'File Extension To Launch' should have at least one value when 'Extract File Before Launch' is set to true.");
        }

        // Parse GroupByFolder
        if (!bool.TryParse(sysConfigElement.Element("GroupByFolder")?.Value, out var groupByFolder))
            groupByFolder = false;

        // Parse DisableRecursiveSearch
        if (!bool.TryParse(sysConfigElement.Element("DisableRecursiveSearch")?.Value,
                out var disableRecursiveSearch))
        {
            disableRecursiveSearch = false;
        }

        // Validate emulator configurations
        var emulators = new List<Emulator>();
        var emulatorElements = sysConfigElement.Element("Emulators")?.Elements("Emulator").ToList();

        if (emulatorElements == null || emulatorElements.Count == 0)
        {
            throw new InvalidOperationException(
                $"System '{systemName}': Emulators list should not be empty or null."); // Need at least one EmulatorName element
        }

        foreach (var emulatorElement in emulatorElements)
        {
            var emulatorName = emulatorElement.Element("EmulatorName")?.Value;
            if (string.IsNullOrEmpty(emulatorName))
            {
                throw new InvalidOperationException(
                    $"System '{systemName}': An 'Emulator Name' should not be empty or null.");
            }

            var emulatorLocation = emulatorElement.Element("EmulatorLocation")?.Value ?? ""; // can be empty
            var emulatorParameters = emulatorElement.Element("EmulatorParameters")?.Value ?? ""; // can be empty

            // Parse the ReceiveANotificationOnEmulatorError value with default = true
            // If the element is missing or parsing fails, it defaults to true.
            var receiveNotification = true; // Default value
            if (emulatorElement.Element("ReceiveANotificationOnEmulatorError") != null)
            {
                if (!bool.TryParse(emulatorElement.Element("ReceiveANotificationOnEmulatorError")?.Value,
                        out receiveNotification))
                {
                    receiveNotification = true; // Reset to default if parsing fails
                }
            }

            emulators.Add(new Emulator
            {
                EmulatorName = emulatorName,
                EmulatorLocation = emulatorLocation, // Store the raw string
                EmulatorParameters = emulatorParameters, // Store the raw string
                ReceiveANotificationOnEmulatorError = receiveNotification,
                ImagePackDownloadLink = emulatorElement.Element("ImagePackDownloadLink")?.Value ?? "",
                ImagePackDownloadLink2 = emulatorElement.Element("ImagePackDownloadLink2")?.Value ?? "",
                ImagePackDownloadLink3 = emulatorElement.Element("ImagePackDownloadLink3")?.Value ?? "",
                ImagePackDownloadLink4 = emulatorElement.Element("ImagePackDownloadLink4")?.Value ?? "",
                ImagePackDownloadLink5 = emulatorElement.Element("ImagePackDownloadLink5")?.Value ?? "",
                ImagePackDownloadExtractPath = emulatorElement.Element("ImagePackDownloadExtractPath")?.Value ?? ""
            });
        }

        return new SystemManagerService
        {
            SystemName = systemName,
            SystemFolders = systemFolders, // Store the raw string
            SystemImageFolder = systemImageFolder, // Store the raw string
            ExtractFileBeforeLaunch = extractFileBeforeLaunch,
            FileFormatsToSearch = formatsToSearch,
            FileFormatsToLaunch = formatsToLaunch ?? [],
            Emulators = emulators,
            GroupByFolder = groupByFolder,
            DisableRecursiveSearch = disableRecursiveSearch
        };
    }

    /// <summary>Loads systems from the unified database (callers must have verified it is valid).</summary>
    private static List<SystemManagerService> LoadSystemsFromDatabase()
    {
        var result = new List<SystemManagerService>();
        foreach (var kvp in UnifiedSettingsDatabase.LoadSystems())
        {
            var config = SystemConfigStore.Deserialize(kvp.Key, kvp.Value);
            if (config is not null)
                result.Add(FromSystemManagerConfig(config));
        }

        result.Sort(static (a, b) =>
            string.Compare(a.SystemName, b.SystemName, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Maps a database system config to a runtime manager instance.</summary>
    internal static SystemManagerService FromSystemManagerConfig(SystemManagerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new SystemManagerService
        {
            SystemName = config.SystemName,
            SystemFolders = config.SystemFolders?.ToList() ?? [],
            SystemImageFolder = config.SystemImageFolder ?? "",
            FileFormatsToSearch = config.FileFormatsToSearch?.ToList() ?? [],
            FileFormatsToLaunch = config.FileFormatsToLaunch?.ToList() ?? [],
            ExtractFileBeforeLaunch = config.ExtractFileBeforeLaunch,
            GroupByFolder = config.GroupByFolder,
            DisableRecursiveSearch = config.DisableRecursiveSearch,
            Emulators = config.Emulators?.ToList() ?? []
        };
    }

    /// <summary>Maps a runtime manager instance to a database system config.</summary>
    internal static SystemManagerConfig ToSystemManagerConfig(SystemManagerService systemConfig)
    {
        ArgumentNullException.ThrowIfNull(systemConfig);
        return new SystemManagerConfig
        {
            SystemName = systemConfig.SystemName,
            SystemFolders = systemConfig.SystemFolders?.ToList() ?? [],
            SystemImageFolder = systemConfig.SystemImageFolder ?? "",
            FileFormatsToSearch = systemConfig.FileFormatsToSearch?.ToList() ?? [],
            FileFormatsToLaunch = systemConfig.FileFormatsToLaunch?.ToList() ?? [],
            ExtractFileBeforeLaunch = systemConfig.ExtractFileBeforeLaunch,
            GroupByFolder = systemConfig.GroupByFolder,
            DisableRecursiveSearch = systemConfig.DisableRecursiveSearch,
            Emulators = systemConfig.Emulators?.Select(static e => new Emulator
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

    /// <summary>
    ///     Loads systems from an explicit XML file path without any side effects
    ///     (no database read, no static cache, no user notifications, no file rewrite).
    ///     Used by the one-time legacy migration and tests.
    /// </summary>
    internal static List<SystemManagerService> LoadSystemsFromPath(string xmlPath, ILogger? logErrors = null)
    {
        var result = new List<SystemManagerService>();
        if (!File.Exists(xmlPath)) return result;

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var reader = XmlReader.Create(xmlPath, settings);
            var doc = XDocument.Load(reader, LoadOptions.None);
            if (doc.Root is null) return result;

            foreach (var sysConfigElement in doc.Root.Elements("SystemConfig"))
            {
                try
                {
                    result.Add(ParseSystemConfiguration(sysConfigElement));
                }
                catch (Exception ex)
                {
                    var systemName = sysConfigElement.Element("SystemName")?.Value ?? "Unnamed System";
                    logErrors?.Error(ex, "Invalid system configuration '{SystemName}' in '{Path}'", systemName,
                        xmlPath);
                }
            }
        }
        catch (XmlException ex)
        {
            logErrors?.Error(ex, "Structural corruption in '{Path}'. Attempting partial recovery", xmlPath);

            try
            {
                var rawXml = File.ReadAllText(xmlPath);
                foreach (Match match in MyRegex().Matches(rawXml))
                {
                    try
                    {
                        result.Add(ParseSystemConfiguration(XElement.Parse(match.Value)));
                    }
                    catch (Exception innerEx)
                    {
                        var nameMatch = MyRegex1().Match(match.Value);
                        var sysName = nameMatch.Success ? nameMatch.Groups[1].Value : "Unknown";
                        logErrors?.Error(innerEx, "Failed to validate system configuration for '{SystemName}'", sysName);
                    }
                }
            }
            catch (Exception recoveryEx)
            {
                logErrors?.Error(recoveryEx, "Failed to perform regex recovery on '{Path}'", xmlPath);
            }
        }
        catch (Exception ex)
        {
            logErrors?.Error(ex, "Failed to parse '{Path}'", xmlPath);
        }

        return result;
    }

    private static async Task RestoreBackupFileAsync(string directoryPath, string systemXmlPath, ILogger logErrors,
        IMessageBoxLibraryService messageBoxLibrary)
    {
        try
        {
            // Search for backup files in the application directory
            var backupFiles = Directory.GetFiles(directoryPath, "system_backup*.xml").ToList();
            if (backupFiles.Count > 0)
            {
                // Sort the backup files by their last write time to find the most recent one
                var mostRecentBackupFile = backupFiles.MaxBy(File.GetLastWriteTime);

                // Notify user and ask to restore
                var restoreResult = messageBoxLibrary != null
                    ? await messageBoxLibrary.WouldYouLikeToRestoreTheLastBackupMessageBoxAsync()
                    : MessageBoxResult.No;
                if (restoreResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Copy the most recent backup file to system.xml, overwriting if a dummy file exists
                        if (mostRecentBackupFile != null) File.Copy(mostRecentBackupFile, systemXmlPath, true);
                    }
                    catch (Exception ex)
                    {
                        // Notify developer
                        const string contextMessage = "'Simple Launcher' was unable to restore the last backup.";
                        logErrors?.Error(ex, contextMessage);

                        // Notify user
                        if (messageBoxLibrary != null)
                            _ = messageBoxLibrary.SimpleLauncherWasUnableToRestoreBackupMessageBoxAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            // Error during backup search/restore attempt (e.g., directory access issues)
            const string contextMessage = "Error during backup file handling.";
            logErrors?.Error(ex, contextMessage);
        }
    }

    /// <summary>
    ///     Adds or updates a system configuration from an Easy Mode preset.
    /// </summary>
    public static Task AddOrUpdateSystemFromEasyModeAsync(EasyModeSystemConfig selectedSystem, string systemFolder)
    {
        var systemToSave = new SystemManagerService
        {
            SystemName = selectedSystem.SystemName,
            SystemFolders = [systemFolder],
            SystemImageFolder = selectedSystem.SystemImageFolder,
            FileFormatsToSearch = selectedSystem.FileFormatsToSearch,
            GroupByFolder = false, // Default to false for new EasyMode systems
            ExtractFileBeforeLaunch = selectedSystem.ExtractFileBeforeLaunch,
            FileFormatsToLaunch = selectedSystem.FileFormatsToLaunch,
            Emulators = [ConvertEasyModeEmulator(selectedSystem.Emulators.Emulator)]
        };

        // When called from Easy Mode, we always want to add or update, so the original name is the same as the new name.
        return SaveSystemConfigurationAsync(systemToSave, selectedSystem.SystemName);
    }

    private static Emulator ConvertEasyModeEmulator(EmulatorConfig emulatorConfig)
    {
        return new Emulator
        {
            EmulatorName = emulatorConfig.EmulatorName,
            EmulatorLocation = emulatorConfig.EmulatorLocation,
            EmulatorParameters = emulatorConfig.EmulatorParameters,
            ReceiveANotificationOnEmulatorError = true, // Default to true for EasyMode systems
            ImagePackDownloadLink = emulatorConfig.ImagePackDownloadLink,
            ImagePackDownloadLink2 = emulatorConfig.ImagePackDownloadLink2,
            ImagePackDownloadLink3 = emulatorConfig.ImagePackDownloadLink3,
            ImagePackDownloadLink4 = emulatorConfig.ImagePackDownloadLink4,
            ImagePackDownloadLink5 = emulatorConfig.ImagePackDownloadLink5,
            ImagePackDownloadExtractPath = emulatorConfig.ImagePackDownloadExtractPath
        };
    }

    /// <summary>
    ///     Asynchronously saves a system configuration, creating or updating the entry.
    ///     In unified-database mode the system is upserted into <c>settings.dat</c>;
    ///     otherwise system.xml is used with retry logic.
    /// </summary>
    public static async Task SaveSystemConfigurationAsync(SystemManagerService systemConfig,
        string? originalSystemName = null, ILogger? logErrors = null, IConfiguration? configuration = null)
    {
        // Unified-database path: upsert the system into settings.dat.
        if (UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                await Task.Run(() =>
                {
                    lock (XmlLock)
                    {
                        UnifiedSettingsDatabase.EnsureCreated();
                        var identifier = originalSystemName ?? systemConfig.SystemName;

                        // Handle renames: remove the old key when the name changed.
                        if (!string.Equals(identifier, systemConfig.SystemName, StringComparison.OrdinalIgnoreCase))
                            UnifiedSettingsDatabase.DeleteSystem(identifier);

                        var config = ToSystemManagerConfig(systemConfig);
                        UnifiedSettingsDatabase.SaveSystem(systemConfig.SystemName, SystemConfigStore.Serialize(config));
                    }
                });
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, "Error saving system configuration to the unified database");
            }

            return;
        }

        try
        {
            await Task.Run(() =>
            {
                lock (XmlLock)
                {
                    var systemXmlPath =
                        GetSystemXmlPath(configuration ?? App.ServiceProvider.GetRequiredService<IConfiguration>());
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
                    catch (UnauthorizedAccessException) when (_fileLocation is { IsPortableMode: true })
                    {
                        var fallbackPath = _fileLocation.GetLocalAppDataPath();
                        if (File.Exists(fallbackPath))
                        {
                            try
                            {
                                var xmlContent = File.ReadAllText(fallbackPath);
                                xmlDoc = string.IsNullOrWhiteSpace(xmlContent)
                                    ? new XDocument(new XElement("SystemConfigs"))
                                    : XDocument.Parse(xmlContent);
                                if (xmlDoc.Root == null || xmlDoc.Root.Name != "SystemConfigs")
                                    xmlDoc = new XDocument(new XElement("SystemConfigs"));

                                _fileLocation.TryFallbackToLocalAppData();
                                systemXmlPath = _fileLocation.FilePath;
                            }
                            catch
                            {
                                xmlDoc = new XDocument(new XElement("SystemConfigs"));
                            }
                        }
                        else
                        {
                            xmlDoc = new XDocument(new XElement("SystemConfigs"));
                        }
                    }
                    catch (Exception ex)
                    {
                        logErrors?.Error(ex, "Error loading/parsing system.xml for saving");
                        throw new InvalidOperationException("Failed to load system configuration for saving.", ex);
                    }

                    var root = xmlDoc.Root;
                    var systemIdentifier = originalSystemName ?? systemConfig.SystemName;
                    if (root != null)
                    {
                        // OrdinalIgnoreCase to match SystemExists: saving "nes" must update
                        // the existing "NES" row instead of appending a duplicate.
                        var existingSystem = root.Elements("SystemConfig")
                            .FirstOrDefault(el => string.Equals(el.Element("SystemName")?.Value, systemIdentifier,
                                StringComparison.OrdinalIgnoreCase));

                        if (existingSystem != null)
                            UpdateSystemXElement(existingSystem, systemConfig);
                        else
                            root.Add(CreateSystemXElement(systemConfig));
                    }

                    if (root != null)
                    {
                        var sortedSystems = root.Elements("SystemConfig")
                            .OrderBy(static system => system.Element("SystemName")?.Value,
                                StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        root.RemoveNodes();
                        root.Add(sortedSystems);
                    }

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
                                Indent = true, IndentChars = "  ", NewLineHandling = NewLineHandling.Replace,
                                Encoding = Encoding.UTF8
                            };

                            // Serialize to memory first to ensure we have valid data before touching the disk.
                            // This prevents creating a 0-byte file if serialization fails or the process crashes mid-save.
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

                            // Write the entire buffer at once to the temporary file.
                            File.WriteAllBytes(tempPath, xmlBytes);

                            // Atomically replace the main file with the temp file
                            File.Move(tempPath, systemXmlPath, true);
                            return; // Success
                        }
                        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                        {
                            lastException = ex;

                            // If in portable mode and this is the last attempt, try falling back to LocalAppData
                            if (_fileLocation is { IsPortableMode: true } && attempt == maxRetries - 1)
                            {
                                try
                                {
                                    // Store the old path for cleanup
                                    var oldSystemXmlPath = systemXmlPath;

                                    // Attempt fallback
                                    if (_fileLocation.TryFallbackToLocalAppData())
                                    {
                                        systemXmlPath = _fileLocation.FilePath;

                                        // If we successfully switched paths, retry the save
                                        if (!string.Equals(systemXmlPath, oldSystemXmlPath, StringComparison.Ordinal))
                                        {
                                            attempt--; // Don't count this as an attempt, retry with new path
                                            continue;
                                        }
                                    }
                                }
                                catch (Exception fallbackEx)
                                {
                                    Log.Debug(
                                        $"[SystemManagerService] FallbackToLocalAppData failed: {fallbackEx.Message}");
                                }
                            }

                            if (attempt < maxRetries - 1)
                            {
                                // Attempt to clean up temp file before retrying
                                try
                                {
                                    var tempPath = systemXmlPath + ".tmp";
                                    if (File.Exists(tempPath)) File.Delete(tempPath);
                                }
                                catch (Exception cleanupEx)
                                {
                                    Log.Debug($"[SystemManagerService] Temp file cleanup failed: {cleanupEx.Message}");
                                }

                                Thread.Sleep(retryDelayMs);
                                retryDelayMs *= 2; // Exponential backoff
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            break; // Don't retry non-transient errors
                        }
                    }

                    // All retries exhausted or non-transient error
                    logErrors?.Error(lastException, "Error saving system.xml");

                    // Attempt to clean up temp file if it exists
                    try
                    {
                        var tempPath = systemXmlPath + ".tmp";
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }

                    throw new InvalidOperationException("Failed to save system configuration.", lastException);
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.Debug($"Error saving system configuration: {ex.Message}");
            logErrors?.Error(ex, "Error saving system configuration");
        }
    }

    /// <summary>
    ///     Asynchronously deletes a system configuration entry by name.
    ///     In unified-database mode the row is deleted from <c>settings.dat</c>;
    ///     otherwise the entry is removed from system.xml.
    /// </summary>
    public static async Task DeleteSystemAsync(string systemNameToDelete, ILogger? logErrors = null,
        IConfiguration? configuration = null)
    {
        if (UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                await Task.Run(() =>
                {
                    lock (XmlLock)
                    {
                        UnifiedSettingsDatabase.EnsureCreated();
                        UnifiedSettingsDatabase.DeleteSystem(systemNameToDelete);
                    }
                });
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, $"Error deleting system '{systemNameToDelete}' from the unified database");
            }

            return;
        }

        try
        {
            await Task.Run(() =>
            {
                lock (XmlLock)
                {
                    var systemXmlPath =
                        GetSystemXmlPath(configuration ?? App.ServiceProvider.GetRequiredService<IConfiguration>());
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
                        logErrors?.Error(ex, $"Error loading system.xml for deleting system '{systemNameToDelete}'.");
                        throw new InvalidOperationException("Failed to load system configuration for deletion.", ex);
                    }

                    // OrdinalIgnoreCase to match SystemExists, so deleting "nes" removes "NES".
                    var systemNode = xmlDoc.Root?.Descendants("SystemConfig")
                        .FirstOrDefault(element => string.Equals(element.Element("SystemName")?.Value,
                            systemNameToDelete, StringComparison.OrdinalIgnoreCase));

                    if (systemNode != null)
                    {
                        systemNode.Remove();
                        var tempPath = systemXmlPath + ".tmp";
                        xmlDoc.Save(tempPath);
                        File.Move(tempPath, systemXmlPath, true);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.Debug($"Error deleting system '{systemNameToDelete}': {ex.Message}");
            logErrors?.Error(ex, $"Error deleting system '{systemNameToDelete}'.");
        }
    }

    private static XElement CreateSystemXElement(SystemManagerService config)
    {
        var element = new XElement("SystemConfig",
            new XElement("SystemName", config.SystemName),
            new XElement("SystemFolders",
                config.SystemFolders.Select(static folder => new XElement("SystemFolder", folder))),
            new XElement("SystemImageFolder", config.SystemImageFolder),
            new XElement("FileFormatsToSearch",
                config.FileFormatsToSearch.Select(static format => new XElement("FormatToSearch", format))),
            new XElement("GroupByFolder", config.GroupByFolder),
            config.ExtractFileBeforeLaunch ? new XElement("ExtractFileBeforeLaunch", true) : null,
            new XElement("FileFormatsToLaunch",
                config.FileFormatsToLaunch.Select(static format => new XElement("FormatToLaunch", format))),
            new XElement("Emulators", config.Emulators.Select(CreateEmulatorXElement))
        );
        return element;
    }

    private static void UpdateSystemXElement(XElement existingSystem, SystemManagerService config)
    {
        existingSystem.SetElementValue("SystemName", config.SystemName);

        var foldersElement = existingSystem.Element("SystemFolders");
        if (foldersElement == null)
        {
            foldersElement = new XElement("SystemFolders");
            existingSystem.Element("SystemName")?.AddAfterSelf(foldersElement);
        }

        foldersElement.ReplaceNodes(config.SystemFolders.Select(static folder => new XElement("SystemFolder", folder)));

        existingSystem.SetElementValue("SystemImageFolder", config.SystemImageFolder);
        existingSystem.Element("FileFormatsToSearch")
            ?.ReplaceNodes(config.FileFormatsToSearch.Select(static format => new XElement("FormatToSearch", format)));
        existingSystem.SetElementValue("GroupByFolder", config.GroupByFolder);
        existingSystem.SetElementValue("DisableRecursiveSearch", config.DisableRecursiveSearch);
        existingSystem.SetElementValue("ExtractFileBeforeLaunch", config.ExtractFileBeforeLaunch ? true : null);
        existingSystem.Element("FileFormatsToLaunch")
            ?.ReplaceNodes(config.FileFormatsToLaunch.Select(static format => new XElement("FormatToLaunch", format)));

        existingSystem.Element("Emulators")?.Remove();
        existingSystem.Add(new XElement("Emulators", config.Emulators.Select(CreateEmulatorXElement)));
    }

    private static XElement CreateEmulatorXElement(Emulator emulatorConfig)
    {
        var emulatorElement = new XElement("Emulator",
            new XElement("EmulatorName", emulatorConfig.EmulatorName),
            new XElement("EmulatorLocation", emulatorConfig.EmulatorLocation),
            new XElement("EmulatorParameters", emulatorConfig.EmulatorParameters),
            new XElement("ReceiveANotificationOnEmulatorError", emulatorConfig.ReceiveANotificationOnEmulatorError)
        );

        if (!string.IsNullOrEmpty(emulatorConfig.ImagePackDownloadLink))
            emulatorElement.Add(new XElement("ImagePackDownloadLink", emulatorConfig.ImagePackDownloadLink));
        if (!string.IsNullOrEmpty(emulatorConfig.ImagePackDownloadLink2))
            emulatorElement.Add(new XElement("ImagePackDownloadLink2", emulatorConfig.ImagePackDownloadLink2));
        if (!string.IsNullOrEmpty(emulatorConfig.ImagePackDownloadLink3))
            emulatorElement.Add(new XElement("ImagePackDownloadLink3", emulatorConfig.ImagePackDownloadLink3));
        if (!string.IsNullOrEmpty(emulatorConfig.ImagePackDownloadLink4))
            emulatorElement.Add(new XElement("ImagePackDownloadLink4", emulatorConfig.ImagePackDownloadLink4));
        if (!string.IsNullOrEmpty(emulatorConfig.ImagePackDownloadLink5))
            emulatorElement.Add(new XElement("ImagePackDownloadLink5", emulatorConfig.ImagePackDownloadLink5));
        if (!string.IsNullOrEmpty(emulatorConfig.ImagePackDownloadExtractPath))
        {
            emulatorElement.Add(new XElement("ImagePackDownloadExtractPath",
                emulatorConfig.ImagePackDownloadExtractPath));
        }

        return emulatorElement;
    }

    [GeneratedRegex(@"<SystemConfig[^>]*>[\s\S]*?<\/SystemConfig>", RegexOptions.None, 1000)]
    private static partial Regex MyRegex();

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the system name")]
    [GeneratedRegex(@"<SystemName>(.*?)<\/SystemName>", RegexOptions.None, 1000)]
    private static partial Regex MyRegex1();
}
