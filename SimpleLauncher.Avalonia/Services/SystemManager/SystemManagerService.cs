using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.UnifiedSettings;

namespace SimpleLauncher.Avalonia.Services.SystemManager;

/// <summary>
///     Reads and writes system configurations.
///     The Avalonia app persists systems in the unified SQLite database
///     (<c>settings.dat</c> in AppData) when it exists; the legacy <c>system.xml</c>
///     format is only read during the one-time migration (and as a fallback when no
///     database exists yet). XML read/write supports both legacy (nested child-element)
///     and simplified (semicolon/comma-delimited) formats for reading, and writes in the
///     legacy format for compatibility with the original SimpleLauncher.
/// </summary>
public class SystemManagerService
{
    private static readonly Lock XmlLock = new();

    private static readonly Regex SystemConfigBlockRegexInstance =
        new(@"<SystemConfig\b[^>]*>.*?</SystemConfig>", RegexOptions.Singleline | RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));

    private static readonly Regex SystemNameRegexInstance =
        new(@"<SystemName>\s*(.*?)\s*</SystemName>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));

    private readonly IConfiguration _configuration;
    private readonly IMessageBoxLibraryService? _messageBox;
    private List<SystemManagerConfig>? _cachedSystems;

    public SystemManagerService(IConfiguration configuration, IMessageBoxLibraryService? messageBox = null)
    {
        _configuration = configuration;
        _messageBox = messageBox;
    }

    // ── Read ──────────────────────────────────────────────────────────

    /// <summary>
    ///     Loads all system configurations from system.xml. Mirrors the WPF
    ///     LoadSystemManagersInternalAsync: validates each system, removes invalid
    ///     ones (notifying the user), attempts partial recovery on structural
    ///     corruption via regex, offers to restore the last backup when the file is
    ///     unrecoverable, and rewrites a cleaned, sorted copy back to disk.
    /// </summary>
    public List<SystemManagerConfig> LoadSystems()
    {
        if (_cachedSystems is not null) return _cachedSystems;

        _cachedSystems = LoadSystemsFromPath(null);
        return _cachedSystems;
    }

    /// <summary>
    ///     Loads systems from the unified database without touching the cache.
    ///     Test seam: accepts an explicit database path so tests never touch real user data.
    /// </summary>
    internal List<SystemManagerConfig> LoadSystemsFromDatabase(string? dbPathOverride)
    {
        var result = new List<SystemManagerConfig>();

        foreach (var kvp in UnifiedSettingsDatabase.LoadSystems(dbPathOverride))
        {
            var dbConfig = SystemConfigStore.Deserialize(kvp.Key, kvp.Value);
            if (dbConfig is not null)
                result.Add(dbConfig);
        }

        result.Sort(static (a, b) =>
            string.Compare(a.SystemName, b.SystemName, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>
    ///     Loads systems from an explicit file path without touching the cache.
    ///     Used by the one-time legacy migration (test seam: arbitrary folders).
    ///     Unlike <see cref="LoadSystems" />, this never reads the database and never
    ///     rewrites the file or offers backup restores.
    /// </summary>
    internal List<SystemManagerConfig> LoadSystemsFromPath(string? explicitPath)
    {
        var result = new List<SystemManagerConfig>();

        // Unified-database path: single source of truth once migrated.
        // Skipped for explicit paths (the migrator reads the legacy file itself).
        if (explicitPath is null && UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                return LoadSystemsFromDatabase(null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading systems from the unified database; trying the legacy file");
            }
        }

        var path = explicitPath ?? GetSystemXmlPath();
        if (!File.Exists(path)) return result;

        var invalidErrors = new List<string>();
        var dirty = false;

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var reader = XmlReader.Create(path, settings);
            var doc = XDocument.Load(reader, LoadOptions.None);

            if (doc.Root != null)
            {
                foreach (var element in doc.Root.Elements("SystemConfig"))
                {
                    try
                    {
                        var config = ParseSystemElement(element);
                        result.Add(config);
                    }
                    catch (Exception ex)
                    {
                        var name = element.Element("SystemName")?.Value ?? "Unnamed System";
                        invalidErrors.Add(
                            $"The system '{name}' was removed due to the following error(s):\n- {ex.Message}");
                        dirty = true;
                    }
                }
            }
        }
        catch (XmlException ex)
        {
            Log.Error(ex, "Structural corruption in 'system.xml'. Attempting partial recovery");
            dirty = true;

            try
            {
                var rawXml = File.ReadAllText(path);
                foreach (Match match in SystemConfigBlockRegexInstance.Matches(rawXml))
                {
                    try
                    {
                        var sysConfigElement = XElement.Parse(match.Value);
                        var config = ParseSystemElement(sysConfigElement);
                        result.Add(config);
                    }
                    catch (Exception innerEx)
                    {
                        var nameMatch = SystemNameRegexInstance.Match(match.Value);
                        var sysName = nameMatch.Success ? nameMatch.Groups[1].Value : "Unknown";
                        invalidErrors.Add(
                            $"The system '{sysName}' was removed due to structural corruption in the XML.");
                        Log.Error(innerEx, "Failed to validate system configuration during recovery for '{SysName}'",
                            sysName);
                    }
                }
            }
            catch (Exception recoveryEx)
            {
                Log.Error(recoveryEx, "Failed to perform regex recovery on system.xml");
            }

            if (result.Count == 0 && invalidErrors.Count == 0 && explicitPath is null)
                _ = NotifyCorruptedAndMaybeRestoreAsync(path);
        }
        catch (IOException ex)
        {
            Log.Error(ex, "The file 'system.xml' is locked");
            _ = _messageBox?.FileSystemXmlIsLockedMessageBoxAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse system.xml at {Path}", path);
            if (explicitPath is null)
                _ = NotifyCorruptedAndMaybeRestoreAsync(path);
        }

        // Notify the user about each invalid system that was removed.
        foreach (var error in invalidErrors) _ = _messageBox?.InvalidSystemConfigurationMessageBoxAsync(error);

        // Rewrite a cleaned, sorted copy so future loads don't re-corrupt.
        // Skipped for explicit paths (migration must not modify the source file).
        if (explicitPath is null && dirty && result.Count > 0)
        {
            try
            {
                SaveCleanedSystems(result, path);
            }
            catch (Exception saveEx)
            {
                Log.Error(saveEx, "Error saving cleaned 'system.xml' after loading");
            }
        }

        return result;
    }

    /// <summary>Informs the user the file is corrupted and offers to restore the last backup.</summary>
    private async Task NotifyCorruptedAndMaybeRestoreAsync(string path)
    {
        try
        {
            _ = _messageBox?.SystemXmlIsCorruptedMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            var backup = FindLatestBackup(path);
            if (backup is null) return;

            if (_messageBox is null) return;
            var result = await _messageBox.WouldYouLikeToRestoreTheLastBackupMessageBoxAsync();
            if (result != MessageBoxResult.Yes) return;

            try
            {
                File.Copy(backup, path, true);
                _cachedSystems = null;
                InvalidateCache();
                _cachedSystems = LoadSystems();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore 'system.xml' from backup '{Backup}'", backup);
                _ = _messageBox.SimpleLauncherWasUnableToRestoreBackupMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while handling a corrupted 'system.xml'");
        }
    }

    private static string? FindLatestBackup(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

            return Directory.EnumerateFiles(dir, "system_backup*.xml", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching for system.xml backups");
            return null;
        }
    }

    /// <summary>Parses a single SystemConfig element and throws if it is invalid.</summary>
    private static SystemManagerConfig ParseSystemElement(XElement element)
    {
        var systemName = element.Element("SystemName")?.Value?.Trim();
        if (string.IsNullOrEmpty(systemName))
            throw new InvalidOperationException("SystemName is missing or empty.");

        return new SystemManagerConfig
        {
            SystemName = systemName,
            SystemFolders = ParseFoldersCompat(element),
            SystemImageFolder = element.Element("SystemImageFolder")?.Value ?? "",
            FileFormatsToSearch = ParseListCompat(element, "FileFormatsToSearch", "FormatToSearch"),
            FileFormatsToLaunch = ParseListCompat(element, "FileFormatsToLaunch", "FormatToLaunch"),
            ExtractFileBeforeLaunch = bool.TryParse(element.Element("ExtractFileBeforeLaunch")?.Value, out var b) && b,
            GroupByFolder = bool.TryParse(element.Element("GroupByFolder")?.Value, out var g) && g,
            DisableRecursiveSearch = bool.TryParse(element.Element("DisableRecursiveSearch")?.Value, out var d) && d,
            Emulators = ParseEmulators(element.Element("Emulators"))
        };
    }

    /// <summary>Rewrites system.xml with the supplied (validated) systems, sorted by name.</summary>
    private static void SaveCleanedSystems(List<SystemManagerConfig> systems, string path)
    {
        var root = new XElement("SystemConfigs");
        foreach (var config in systems.OrderBy(static c => c.SystemName, StringComparer.OrdinalIgnoreCase))
        {
            root.Add(BuildSystemConfigElement(
                config.SystemName,
                config.SystemFolders,
                config.SystemImageFolder,
                config.FileFormatsToSearch,
                config.FileFormatsToLaunch,
                config.ExtractFileBeforeLaunch,
                config.Emulators,
                config.GroupByFolder,
                config.DisableRecursiveSearch));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);

        const int maxRetries = 3;
        var retryDelayMs = 500;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var tempPath = path + ".tmp";
                var writerSettings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineHandling = NewLineHandling.Replace,
                    Encoding = Encoding.UTF8
                };

                using (var ms = new MemoryStream())
                {
                    using var writer = XmlWriter.Create(ms, writerSettings);
                    doc.Save(writer);
                    File.WriteAllBytes(tempPath, ms.ToArray());
                }

                File.Move(tempPath, path, true);
                return;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                if (attempt < maxRetries - 1)
                {
                    try
                    {
                        File.Delete(path + ".tmp");
                    }
                    catch
                    {
                        /* ignore */
                    }

                    Thread.Sleep(retryDelayMs);
                    retryDelayMs *= 2;
                }
                else
                {
                    Log.Error(ex, "Error saving cleaned 'system.xml'");
                }
            }
        }
    }

    /// <summary>
    ///     Gets a single system by name.
    /// </summary>
    public SystemManagerConfig? GetSystem(string systemName)
    {
        return LoadSystems().FirstOrDefault(s =>
            string.Equals(s.SystemName, systemName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Invalidates the cache — call after system.xml changes.
    /// </summary>
    public void InvalidateCache()
    {
        _cachedSystems = null;
    }

    // ── Write (EasyMode) ──────────────────────────────────────────────

    /// <summary>
    ///     Adds or updates a system configuration from an EasyMode preset.
    ///     Mirrors the original SimpleLauncher's <c>AddOrUpdateSystemFromEasyModeAsync</c>.
    /// </summary>
    public static Task AddOrUpdateSystemFromEasyModeAsync(
        EasyModeSystemConfig selectedSystem,
        string systemFolder,
        IConfiguration configuration,
        ILogger? logErrors = null,
        SystemManagerService? cacheOwner = null,
        string? dbPathOverride = null)
    {
        return SaveSystemConfigurationAsync(
            selectedSystem.SystemName,
            [systemFolder],
            selectedSystem.SystemImageFolder,
            selectedSystem.FileFormatsToSearch ?? [],
            selectedSystem.FileFormatsToLaunch ?? [],
            selectedSystem.ExtractFileBeforeLaunch,
            ConvertEasyModeEmulator(selectedSystem.Emulators?.Emulator),
            selectedSystem.SystemName,
            configuration,
            logErrors,
            cacheOwner,
            dbPathOverride: dbPathOverride);
    }

    /// <summary>
    ///     Persists a system configuration to system.xml (legacy format with nested child elements),
    ///     creating or updating the entry with retry logic and atomic writes.
    /// </summary>
    internal static async Task SaveSystemConfigurationAsync(
        string systemName,
        IEnumerable<string> systemFolders,
        string systemImageFolder,
        IEnumerable<string> fileFormatsToSearch,
        IEnumerable<string> fileFormatsToLaunch,
        bool extractFileBeforeLaunch,
        Emulator? emulator,
        string? originalSystemName,
        IConfiguration configuration,
        ILogger? logErrors = null,
        SystemManagerService? cacheOwner = null,
        bool groupByFolder = false,
        bool disableRecursiveSearch = false,
        string? dbPathOverride = null)
    {
        // Unified-database path: upsert the system into settings.dat.
        // A test override forces the database branch against an isolated file.
        if (dbPathOverride is not null || UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                await Task.Run(() =>
                {
                    lock (XmlLock)
                    {
                        UnifiedSettingsDatabase.EnsureCreated(dbPathOverride);
                        var identifier = originalSystemName ?? systemName;

                        // Legacy parity: when updating an existing system through this path,
                        // preserve its GroupByFolder/DisableRecursiveSearch flags (the XML merge
                        // only sets them when absent).
                        var stored = UnifiedSettingsDatabase.LoadSystems(dbPathOverride);
                        if (stored.TryGetValue(identifier, out var existingJson) &&
                            SystemConfigStore.Deserialize(identifier, existingJson) is { } existing)
                        {
                            groupByFolder = existing.GroupByFolder;
                            disableRecursiveSearch = existing.DisableRecursiveSearch;
                        }

                        if (!string.Equals(identifier, systemName, StringComparison.OrdinalIgnoreCase))
                            UnifiedSettingsDatabase.DeleteSystem(identifier, dbPathOverride);

                        var config = new SystemManagerConfig
                        {
                            SystemName = systemName,
                            SystemFolders = systemFolders.ToList(),
                            SystemImageFolder = systemImageFolder,
                            FileFormatsToSearch = fileFormatsToSearch.ToList(),
                            FileFormatsToLaunch = fileFormatsToLaunch.ToList(),
                            ExtractFileBeforeLaunch = extractFileBeforeLaunch,
                            GroupByFolder = groupByFolder,
                            DisableRecursiveSearch = disableRecursiveSearch,
                            Emulators = emulator is not null ? [emulator] : []
                        };
                        UnifiedSettingsDatabase.SaveSystem(systemName, SystemConfigStore.Serialize(config),
                            dbPathOverride);
                    }
                });
                cacheOwner?.InvalidateCache();
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
                    var fileLocation = new DataFileLocation(configuration, "SystemXmlPath", "system.xml");
                    var systemXmlPath = fileLocation.FilePath;
                    XDocument xmlDoc;

                    try
                    {
                        if (File.Exists(systemXmlPath))
                        {
                            var xmlContent = File.ReadAllText(systemXmlPath);
                            xmlDoc = string.IsNullOrWhiteSpace(xmlContent)
                                ? new XDocument(new XElement("SystemConfigs"))
                                : XDocument.Parse(xmlContent);
                            if (xmlDoc.Root?.Name != "SystemConfigs")
                                xmlDoc = new XDocument(new XElement("SystemConfigs"));
                        }
                        else
                        {
                            xmlDoc = new XDocument(new XElement("SystemConfigs"));
                        }
                    }
                    catch (UnauthorizedAccessException) when (fileLocation.IsPortableMode)
                    {
                        // WPF parity: in portable mode, fall back to LocalAppData when system.xml
                        // cannot be read at the portable path (read-only/blocked deployment).
                        var fallbackPath = fileLocation.GetLocalAppDataPath();
                        if (File.Exists(fallbackPath))
                        {
                            try
                            {
                                var xmlContent = File.ReadAllText(fallbackPath);
                                xmlDoc = string.IsNullOrWhiteSpace(xmlContent)
                                    ? new XDocument(new XElement("SystemConfigs"))
                                    : XDocument.Parse(xmlContent);
                                if (xmlDoc.Root?.Name != "SystemConfigs")
                                    xmlDoc = new XDocument(new XElement("SystemConfigs"));

                                if (fileLocation.TryFallbackToLocalAppData()) systemXmlPath = fileLocation.FilePath;
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

                    var root = xmlDoc.Root!;
                    var identifier = originalSystemName ?? systemName;

                    var existingSystem = root.Elements("SystemConfig")
                        .FirstOrDefault(el => string.Equals(
                            el.Element("SystemName")?.Value, identifier, StringComparison.Ordinal));

                    if (existingSystem != null)
                    {
                        // Merge in-place: preserve any child elements we don't explicitly set
                        MergeSystemConfigElement(existingSystem,
                            systemName, systemFolders, systemImageFolder,
                            fileFormatsToSearch, fileFormatsToLaunch,
                            extractFileBeforeLaunch, emulator);
                    }
                    else
                    {
                        var newElement = BuildSystemConfigElement(
                            systemName, systemFolders, systemImageFolder,
                            fileFormatsToSearch, fileFormatsToLaunch,
                            extractFileBeforeLaunch, emulator is null ? null : [emulator],
                            groupByFolder, disableRecursiveSearch);
                        root.Add(newElement);
                    }

                    // Sort alphabetically
                    var sorted = root.Elements("SystemConfig")
                        .OrderBy(static s => s.Element("SystemName")?.Value, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    root.RemoveNodes();
                    root.Add(sorted);

                    // Atomic write with retry
                    const int maxRetries = 3;
                    var retryDelayMs = 500;
                    Exception? lastException = null;

                    for (var attempt = 0; attempt < maxRetries; attempt++)
                    {
                        try
                        {
                            var tempPath = systemXmlPath + ".tmp";
                            var writerSettings = new XmlWriterSettings
                            {
                                Indent = true,
                                IndentChars = "  ",
                                NewLineHandling = NewLineHandling.Replace,
                                Encoding = Encoding.UTF8
                            };

                            byte[] xmlBytes;
                            using (var ms = new MemoryStream())
                            {
                                using (var writer = XmlWriter.Create(ms, writerSettings))
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

                            // WPF parity: in portable mode, when the final in-place attempt fails
                            // (read-only/blocked deployment), fall back to LocalAppData and retry.
                            if (fileLocation.IsPortableMode && attempt == maxRetries - 1)
                            {
                                try
                                {
                                    var oldSystemXmlPath = systemXmlPath;
                                    if (fileLocation.TryFallbackToLocalAppData())
                                    {
                                        systemXmlPath = fileLocation.FilePath;
                                        if (!string.Equals(systemXmlPath, oldSystemXmlPath, StringComparison.Ordinal))
                                        {
                                            attempt--;
                                            continue;
                                        }
                                    }
                                }
                                catch (Exception fallbackEx)
                                {
                                    Log.Debug(fallbackEx, "Fallback to LocalAppData failed while saving system.xml");
                                }
                            }

                            if (attempt < maxRetries - 1)
                            {
                                try
                                {
                                    File.Delete(systemXmlPath + ".tmp");
                                }
                                catch (Exception cleanupEx)
                                {
                                    Log.Debug(cleanupEx, "Failed to delete stale system.xml temp file {Path}",
                                        systemXmlPath + ".tmp");
                                }

                                Thread.Sleep(retryDelayMs);
                                retryDelayMs *= 2;
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            break;
                        }
                    }

                    logErrors?.Error(lastException, "Error saving system.xml");
                    throw new InvalidOperationException("Failed to save system configuration.", lastException);
                }
            });
            cacheOwner?.InvalidateCache();
        }
        catch (Exception ex)
        {
            logErrors?.Error(ex, "Error saving system configuration");
        }
    }

    // ── Path helpers ──────────────────────────────────────────────────

    private string GetSystemXmlPath()
    {
        return GetSystemXmlPathStatic(_configuration);
    }

    private static string GetSystemXmlPathStatic(IConfiguration configuration)
    {
        var fileLocation = new DataFileLocation(configuration, "SystemXmlPath", "system.xml");
        return fileLocation.FilePath;
    }

    // ── Parsers (read: backward-compatible with legacy + simplified formats) ──

    /// <summary>
    ///     Parses system folders from either:
    ///     (A) Legacy:   &lt;SystemFolders&gt;&lt;SystemFolder&gt;...&lt;/SystemFolder&gt;...&lt;/SystemFolders&gt;
    ///     (B) Simplified: &lt;SystemFolder&gt;path1;path2&lt;/SystemFolder&gt;  (direct child)
    /// </summary>
    private static List<string> ParseFoldersCompat(XElement systemConfigElement)
    {
        // Legacy format: <SystemFolders> containing <SystemFolder> children
        var foldersElement = systemConfigElement.Element("SystemFolders");
        if (foldersElement != null)
        {
            return
            [
                .. foldersElement.Elements("SystemFolder")
                    .Select(f => f.Value.Trim())
                    .Where(f => !string.IsNullOrEmpty(f))
            ];
        }

        // Simplified format: direct <SystemFolder> child, semicolon-separated
        var directValue = systemConfigElement.Element("SystemFolder")?.Value;
        return ParseSemicolonList(directValue);
    }

    /// <summary>
    ///     Parses a list from either:
    ///     (A) Legacy:   &lt;container&gt;&lt;itemElement&gt;...&lt;/itemElement&gt;...&lt;/container&gt;
    ///     (B) Simplified: &lt;container&gt;item1,item2&lt;/container&gt;  (direct child, comma-separated)
    /// </summary>
    private static List<string> ParseListCompat(XElement systemConfigElement, string containerName,
        string itemElementName)
    {
        var container = systemConfigElement.Element(containerName);
        if (container == null) return [];

        // Legacy format: child elements
        var childItems = container.Elements(itemElementName).ToList();
        if (childItems.Count > 0)
        {
            return
            [
                .. childItems
                    .Select(e => e.Value.Trim())
                    .Where(v => !string.IsNullOrEmpty(v))
            ];
        }

        // Simplified format: comma-separated value
        return ParseCommaList(container.Value);
    }

    private static List<string> ParseSemicolonList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return
        [
            .. value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
        ];
    }

    private static List<string> ParseCommaList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return
        [
            .. value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
        ];
    }

    // ── Emulator parsing ──────────────────────────────────────────────

    private static List<Emulator> ParseEmulators(XElement? emulatorsElement)
    {
        var emulators = new List<Emulator>();
        if (emulatorsElement is null) return emulators;

        foreach (var el in emulatorsElement.Elements("Emulator"))
        {
            emulators.Add(new Emulator
            {
                EmulatorName = el.Element("EmulatorName")?.Value ?? "",
                EmulatorLocation = el.Element("EmulatorPath")?.Value
                                   ?? el.Element("EmulatorLocation")?.Value ?? "",
                EmulatorParameters = el.Element("EmulatorParameters")?.Value ?? "",
                ReceiveANotificationOnEmulatorError =
                    !bool.TryParse(el.Element("ReceiveANotificationOnEmulatorError")?.Value, out var notify) || notify,
                ImagePackDownloadLink = el.Element("ImagePackDownloadLink")?.Value ?? "",
                ImagePackDownloadLink2 = el.Element("ImagePackDownloadLink2")?.Value ?? "",
                ImagePackDownloadLink3 = el.Element("ImagePackDownloadLink3")?.Value ?? "",
                ImagePackDownloadLink4 = el.Element("ImagePackDownloadLink4")?.Value ?? "",
                ImagePackDownloadLink5 = el.Element("ImagePackDownloadLink5")?.Value ?? "",
                ImagePackDownloadExtractPath = el.Element("ImagePackDownloadExtractPath")?.Value ?? ""
            });
        }

        return emulators;
    }

    // ── XML builders (legacy format for backward compatibility) ───────

    private static XElement BuildSystemConfigElement(
        string systemName,
        IEnumerable<string> systemFolders,
        string systemImageFolder,
        IEnumerable<string> fileFormatsToSearch,
        IEnumerable<string> fileFormatsToLaunch,
        bool extractFileBeforeLaunch,
        IEnumerable<Emulator>? emulators,
        bool groupByFolder = false,
        bool disableRecursiveSearch = false)
    {
        var element = new XElement("SystemConfig",
            new XElement("SystemName", systemName),
            new XElement("SystemFolders",
                systemFolders.Select(f => new XElement("SystemFolder", f))),
            new XElement("SystemImageFolder", systemImageFolder),
            new XElement("FileFormatsToSearch",
                fileFormatsToSearch.Select(f => new XElement("FormatToSearch", f))),
            new XElement("GroupByFolder", groupByFolder),
            new XElement("DisableRecursiveSearch", disableRecursiveSearch),
            extractFileBeforeLaunch ? new XElement("ExtractFileBeforeLaunch", true) : null,
            new XElement("FileFormatsToLaunch",
                fileFormatsToLaunch.Select(f => new XElement("FormatToLaunch", f))));

        if (emulators != null)
        {
            var emulatorList = emulators.ToList();
            if (emulatorList.Count > 0) element.Add(BuildEmulatorsElement(emulatorList));
        }

        return element;
    }

    private static XElement BuildEmulatorsElement(Emulator emu)
    {
        return BuildEmulatorsElement([emu]);
    }

    private static XElement BuildEmulatorsElement(IEnumerable<Emulator> emulators)
    {
        var emulatorsElement = new XElement("Emulators");
        foreach (var emu in emulators) emulatorsElement.Add(BuildEmulatorElement(emu));
        return emulatorsElement;
    }

    private static XElement BuildEmulatorElement(Emulator emu)
    {
        var emuEl = new XElement("Emulator",
            new XElement("EmulatorName", emu.EmulatorName),
            new XElement("EmulatorLocation", emu.EmulatorLocation ?? ""),
            new XElement("EmulatorParameters", emu.EmulatorParameters ?? ""),
            new XElement("ReceiveANotificationOnEmulatorError",
                emu.ReceiveANotificationOnEmulatorError));

        // Preserve image pack links when present (for round-trip fidelity with old SimpleLauncher)
        AppendIfNotEmpty(emuEl, "ImagePackDownloadLink", emu.ImagePackDownloadLink);
        AppendIfNotEmpty(emuEl, "ImagePackDownloadLink2", emu.ImagePackDownloadLink2);
        AppendIfNotEmpty(emuEl, "ImagePackDownloadLink3", emu.ImagePackDownloadLink3);
        AppendIfNotEmpty(emuEl, "ImagePackDownloadLink4", emu.ImagePackDownloadLink4);
        AppendIfNotEmpty(emuEl, "ImagePackDownloadLink5", emu.ImagePackDownloadLink5);
        AppendIfNotEmpty(emuEl, "ImagePackDownloadExtractPath", emu.ImagePackDownloadExtractPath);

        return emuEl;
    }

    private static void AppendIfNotEmpty(XElement parent, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            parent.Add(new XElement(name, value));
    }

    /// <summary>
    ///     Merges EasyMode-supplied fields into an existing SystemConfig element in-place,
    ///     preserving any child elements not explicitly set (e.g. custom user additions).
    /// </summary>
    private static void MergeSystemConfigElement(
        XElement existing,
        string systemName,
        IEnumerable<string> systemFolders,
        string systemImageFolder,
        IEnumerable<string> fileFormatsToSearch,
        IEnumerable<string> fileFormatsToLaunch,
        bool extractFileBeforeLaunch,
        Emulator? emulator)
    {
        existing.SetElementValue("SystemName", systemName);

        // Merge SystemFolders
        var foldersEl = existing.Element("SystemFolders");
        if (foldersEl == null)
        {
            foldersEl = new XElement("SystemFolders");
            existing.Element("SystemName")?.AddAfterSelf(foldersEl);
        }

        foldersEl.ReplaceNodes(systemFolders.Select(f => new XElement("SystemFolder", f)));

        existing.SetElementValue("SystemImageFolder", systemImageFolder);

        // Merge FileFormatsToSearch
        var searchEl = existing.Element("FileFormatsToSearch");
        searchEl?.ReplaceNodes(fileFormatsToSearch.Select(f => new XElement("FormatToSearch", f)));

        // Merge FileFormatsToLaunch
        var launchEl = existing.Element("FileFormatsToLaunch");
        launchEl?.ReplaceNodes(fileFormatsToLaunch.Select(f => new XElement("FormatToLaunch", f)));

        // Only set GroupByFolder if not already present (preserve user preference)
        if (existing.Element("GroupByFolder") == null)
            existing.Add(new XElement("GroupByFolder", false));

        // Only set DisableRecursiveSearch if not already present
        if (existing.Element("DisableRecursiveSearch") == null)
            existing.Add(new XElement("DisableRecursiveSearch", false));

        // ExtractFileBeforeLaunch
        existing.SetElementValue("ExtractFileBeforeLaunch", extractFileBeforeLaunch ? true : null);

        // Merge Emulators: replace if present, add if absent
        if (emulator != null)
        {
            var existingEmulators = existing.Element("Emulators");
            if (existingEmulators != null)
                existingEmulators.ReplaceWith(BuildEmulatorsElement(emulator));
            else
                existing.Add(BuildEmulatorsElement(emulator));
        }
    }

    // ── EasyMode conversion ───────────────────────────────────────────

    private static Emulator ConvertEasyModeEmulator(EmulatorConfig? emulatorConfig)
    {
        if (emulatorConfig == null)
            return new Emulator { EmulatorName = "", EmulatorLocation = "", EmulatorParameters = "" };

        return new Emulator
        {
            EmulatorName = emulatorConfig.EmulatorName,
            EmulatorLocation = emulatorConfig.EmulatorLocation,
            EmulatorParameters = emulatorConfig.EmulatorParameters,
            ReceiveANotificationOnEmulatorError = true,
            ImagePackDownloadLink = emulatorConfig.ImagePackDownloadLink ?? "",
            ImagePackDownloadLink2 = emulatorConfig.ImagePackDownloadLink2 ?? "",
            ImagePackDownloadLink3 = emulatorConfig.ImagePackDownloadLink3 ?? "",
            ImagePackDownloadLink4 = emulatorConfig.ImagePackDownloadLink4 ?? "",
            ImagePackDownloadLink5 = emulatorConfig.ImagePackDownloadLink5 ?? "",
            ImagePackDownloadExtractPath = emulatorConfig.ImagePackDownloadExtractPath ?? ""
        };
    }
}