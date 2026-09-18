using MessagePack;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.SystemManager;
using ILogger = Serilog.ILogger;

namespace SimpleLauncher.Services.SettingsDatabase;

/// <summary>
///     One-time migration from the legacy files (<c>favorites.dat</c>, <c>playhistory.dat</c>,
///     <c>settings.xml</c>, <c>system.xml</c>) into the unified SQLite database
///     (<c>settings.dat</c> in AppData).
/// </summary>
/// <remarks>
///     Runs once per process (idempotent). When no valid database exists, the legacy
///     files are imported into a fresh <c>settings.dat</c>. When a valid database already
///     exists (e.g. the other app variant migrated already, or the files reappeared after
///     a restore), the legacy data is merged into it instead: entries are appended and
///     existing rows with the same key are overwritten, so no duplicates appear.
///     On success every legacy file that was read is renamed to <c>&lt;name&gt;.bak</c>
///     (overwriting any previous backup) so the old paths disappear but the data is
///     recoverable. On failure the new database file (if newly created) is deleted and
///     legacy files are left untouched, so the app falls back to the legacy readers and
///     the next launch retries. An existing valid database is never deleted.
///     Mirrors <c>AvaloniaLegacyMigrator</c>; both apps share the same database format.
/// </remarks>
public static class WpfLegacyMigrator
{
    private static readonly Lock MigrationLock = new();
    private static bool _migrationAttempted;

    /// <summary>
    ///     Ensures the unified database exists and contains the migrated legacy data.
    ///     Safe to call multiple times and from any thread; only the first call does work.
    /// </summary>
    public static MigrationResult EnsureMigrated(
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector)
    {
        return EnsureMigrated(configuration, logger, credentialProtector, null, null);
    }

    /// <summary>
    ///     Test seam: like <see cref="EnsureMigrated(IConfiguration,ILogger,ICredentialProtector)" />,
    ///     but redirects the database path and the legacy-file folders (both the portable
    ///     folder and the AppData folder) so tests never touch real user data.
    /// </summary>
    internal static MigrationResult EnsureMigrated(
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        string? dbPathOverride,
        string? legacyFolderOverride)
    {
        var dbPath = dbPathOverride ?? UnifiedSettingsDatabase.GetDatabasePath();
        lock (MigrationLock)
        {
            if (_migrationAttempted)
                return new MigrationResult(
                    UnifiedSettingsDatabase.IsValidDatabase(dbPath)
                        ? MigrationStatus.AlreadyCurrent
                        : MigrationStatus.Failed,
                    0, 0, 0);
            _migrationAttempted = true;
        }

        if (UnifiedSettingsDatabase.IsValidDatabase(dbPath))
            return MergeLegacyFilesIntoExistingDatabase(dbPath, configuration, logger, credentialProtector, legacyFolderOverride);

        var dbExistedBefore = File.Exists(dbPath);
        var legacyFiles = ResolveLegacyFiles(configuration, legacyFolderOverride);

        try
        {
            // ── Read legacy state (best effort; corrupt files become empty) ──
            // Canonicalize exactly the way the database keys the rows, so the round-trip
            // verification below cannot fail on duplicates that the database legitimately
            // collapses (the same file favorited twice, duplicate history file names).
            var favorites = DedupeByFileName(
                ReadLegacyFavorites(legacyFiles.FavoritesPath, logger), [],
                static f => f.FileName + "\u0000" + f.SystemName);
            var history = DedupeByFileName(
                ReadLegacyHistory(legacyFiles.HistoryPath, logger), [],
                static h => h.FileName);
            var (settings, _) = ReadLegacySettings(configuration, logger, credentialProtector, legacyFiles.SettingsPath);
            var systems = ReadLegacySystems(logger, legacyFiles.SystemXmlPath);

            var anyLegacy = legacyFiles.FavoritesPath is not null
                            || legacyFiles.HistoryPath is not null
                            || legacyFiles.SettingsPath is not null
                            || legacyFiles.SystemXmlPath is not null;

            // ── Write the database ──
            UnifiedSettingsDatabase.EnsureCreated(dbPath);
            UnifiedSettingsDatabase.SaveFavorites(favorites, dbPath);
            UnifiedSettingsDatabase.SavePlayHistory(history, dbPath);
            UnifiedSettingsDatabase.SaveAppSettings(settings.ExportAppSettings(), dbPath);
            UnifiedSettingsDatabase.SaveAllEmulatorConfigs(settings.ExportEmulatorSettings(), dbPath);
            UnifiedSettingsDatabase.SaveSystemPlayTimes(settings.ExportSystemPlayTimes(), dbPath);

            // First wins on case-insensitive duplicate system names.
            var systemsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var system in systems)
            {
                if (!systemsByName.ContainsKey(system.SystemName))
                    systemsByName[system.SystemName] = SystemConfigStore.Serialize(system);
            }

            UnifiedSettingsDatabase.SaveAllSystems(systemsByName, dbPath);

            // ── Verify the round-trip before touching legacy files ──
            // A freshly created database is always seeded with the (default) application
            // and emulator settings, so expect them here.
            VerifyMigration(dbPath, favorites.Count, history.Count, systemsByName.Count, expectSettings: true);

            if (!anyLegacy)
            {
                logger.Information(
                    "[Migration] No legacy files found; created a fresh unified database at '{Path}'", dbPath);
                return new MigrationResult(MigrationStatus.FreshCreated, 0, 0, 0);
            }

            // ── Shelve legacy files as .bak (originals disappear) ──
            var shelved = 0;
            shelved += ShelveLegacyFile(legacyFiles.FavoritesPath, logger);
            shelved += ShelveLegacyFile(legacyFiles.HistoryPath, logger);
            shelved += ShelveLegacyFile(legacyFiles.SettingsPath, logger);
            shelved += ShelveLegacyFile(legacyFiles.SystemXmlPath, logger);
            CleanupTempLeftovers(logger, legacyFolderOverride);

            logger.Information(
                "[Migration] Imported {Fav} favorites, {Hist} history entries, {Sys} systems into '{Path}'; shelved {N} legacy files as .bak",
                favorites.Count, history.Count, systemsByName.Count, dbPath, shelved);
            return new MigrationResult(MigrationStatus.Migrated, favorites.Count, history.Count, systemsByName.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[Migration] Failed to migrate legacy files into '{Path}'. Legacy files left untouched",
                dbPath);

            // Remove the half-written database so the next launch retries (unless a
            // valid database somehow exists now, e.g. created concurrently).
            try
            {
                if (!dbExistedBefore && File.Exists(dbPath) && !UnifiedSettingsDatabase.IsValidDatabase(dbPath))
                {
                    File.Delete(dbPath);
                    UnifiedSettingsDatabase.DeleteJournalSiblings(dbPath);
                }
            }
            catch (Exception cleanupEx)
            {
                logger.Debug(cleanupEx, "[Migration] Failed to delete the incomplete database '{Path}'", dbPath);
            }

            return new MigrationResult(MigrationStatus.Failed, 0, 0, 0);
        }
    }

    /// <summary>Resets the once-per-process guard (unit tests only).</summary>
    internal static void ResetForTests()
    {
        lock (MigrationLock)
        {
            _migrationAttempted = false;
        }
    }

    /// <summary>
    ///     A valid settings.dat already exists, but legacy files were found (application
    ///     folder or AppData): upsert the legacy data into the existing database and shelve
    ///     the files. New entries are appended; rows with the same key are overwritten by
    ///     the legacy values (first wins on the case-insensitive bulk saves), so nothing
    ///     is duplicated and database-only data is kept. Application/emulator settings are
    ///     only merged when a legacy <c>settings.xml</c> was actually read — otherwise the
    ///     database values stay untouched (merging the service defaults would wipe them).
    ///     On failure the database is left untouched and the legacy files stay for the next
    ///     launch to retry.
    /// </summary>
    private static MigrationResult MergeLegacyFilesIntoExistingDatabase(
        string dbPath,
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        string? legacyFolderOverride)
    {
        var legacyFiles = ResolveLegacyFiles(configuration, legacyFolderOverride);
        var anyLegacy = legacyFiles.FavoritesPath is not null
                        || legacyFiles.HistoryPath is not null
                        || legacyFiles.SettingsPath is not null
                        || legacyFiles.SystemXmlPath is not null;

        if (!anyLegacy)
            return new MigrationResult(MigrationStatus.AlreadyCurrent, 0, 0, 0);

        try
        {
            // ── Read legacy state (best effort; corrupt files become empty) ──
            var legacyFavorites = ReadLegacyFavorites(legacyFiles.FavoritesPath, logger);
            var legacyHistory = ReadLegacyHistory(legacyFiles.HistoryPath, logger);
            var (legacySettings, legacySettingsLoaded) =
                ReadLegacySettings(configuration, logger, credentialProtector, legacyFiles.SettingsPath);
            var legacySystems = ReadLegacySystems(logger, legacyFiles.SystemXmlPath);

            // Make sure the existing database has every table and structural upgrade before
            // the first write touches a possibly older schema.
            UnifiedSettingsDatabase.EnsureCreated(dbPath);

            // ── Merge (legacy first: the first-wins dedupe makes legacy overwrite existing rows) ──
            var existingFavorites = UnifiedSettingsDatabase.LoadFavorites(dbPath);
            var existingHistory = UnifiedSettingsDatabase.LoadPlayHistory(dbPath);
            var existingSystems = UnifiedSettingsDatabase.LoadSystems(dbPath);

            // Favorites are (bare FileName, SystemName) pairs, so key on both: the same file
            // name can exist in two systems and must not collapse into one entry.
            var mergedFavorites = DedupeByFileName(legacyFavorites, existingFavorites,
                static f => f.FileName + "\u0000" + f.SystemName);
            var mergedHistory = DedupeByFileName(legacyHistory, existingHistory,
                static h => h.FileName);

            var mergedSystems = new Dictionary<string, string>(existingSystems, StringComparer.OrdinalIgnoreCase);
            foreach (var system in legacySystems)
                mergedSystems[system.SystemName] = SystemConfigStore.Serialize(system);

            // Application/emulator settings are only merged when the legacy settings file was
            // actually read: without it the service carries defaults, and merging those over
            // the database would wipe the user's theme, language, RA credentials and every
            // emulator configuration.
            var mergedPlayTimes = (legacySettingsLoaded
                    ? legacySettings.ExportSystemPlayTimes()
                    : new List<SystemPlayTimeRecord>())
                .Concat(UnifiedSettingsDatabase.LoadSystemPlayTimes(dbPath))
                .ToList();
            var mergedAppSettings = new Dictionary<string, string>(
                UnifiedSettingsDatabase.LoadAppSettings(dbPath), StringComparer.OrdinalIgnoreCase);
            var mergedEmulators = new Dictionary<string, string>(
                UnifiedSettingsDatabase.LoadEmulatorConfigs(dbPath), StringComparer.OrdinalIgnoreCase);
            if (legacySettingsLoaded)
            {
                foreach (var (key, value) in legacySettings.ExportAppSettings())
                    mergedAppSettings[key] = value ?? "";
                foreach (var (key, value) in legacySettings.ExportEmulatorSettings())
                    mergedEmulators[key] = value ?? "{}";
            }

            // ── Write the database ──
            UnifiedSettingsDatabase.SaveFavorites(mergedFavorites, dbPath);
            UnifiedSettingsDatabase.SavePlayHistory(mergedHistory, dbPath);
            UnifiedSettingsDatabase.SaveAppSettings(mergedAppSettings, dbPath);
            UnifiedSettingsDatabase.SaveAllEmulatorConfigs(mergedEmulators, dbPath);
            UnifiedSettingsDatabase.SaveSystemPlayTimes(mergedPlayTimes, dbPath);
            UnifiedSettingsDatabase.SaveAllSystems(mergedSystems, dbPath);

            // ── Verify the round-trip before touching legacy files ──
            VerifyMigration(dbPath, mergedFavorites.Count, mergedHistory.Count, mergedSystems.Count,
                expectSettings: legacySettingsLoaded);

            var favoritesAdded = mergedFavorites.Count - existingFavorites.Count;
            var historyAdded = mergedHistory.Count - existingHistory.Count;
            var systemsAdded = mergedSystems.Count - existingSystems.Count;

            // ── Shelve legacy files as .bak (originals disappear) ──
            var shelved = 0;
            shelved += ShelveLegacyFile(legacyFiles.FavoritesPath, logger);
            shelved += ShelveLegacyFile(legacyFiles.HistoryPath, logger);
            shelved += ShelveLegacyFile(legacyFiles.SettingsPath, logger);
            shelved += ShelveLegacyFile(legacyFiles.SystemXmlPath, logger);
            CleanupTempLeftovers(logger, legacyFolderOverride);

            logger.Information(
                "[Migration] Merged legacy files into the existing database '{Path}': added {Fav} favorites, {Hist} history entries, {Sys} systems; shelved {N} legacy files as .bak",
                dbPath, favoritesAdded, historyAdded, systemsAdded, shelved);
            return new MigrationResult(MigrationStatus.Merged, favoritesAdded, historyAdded, systemsAdded);
        }
        catch (Exception ex)
        {
            logger.Error(ex,
                "[Migration] Failed to merge legacy files into the existing database '{Path}'. Legacy files left untouched; the next launch retries",
                dbPath);
            return new MigrationResult(MigrationStatus.Failed, 0, 0, 0);
        }
    }

    /// <summary>
    ///     Concatenates the legacy records with the existing ones and removes duplicates by
    ///     key (case-insensitive, first wins), so the legacy values overwrite matching
    ///     database rows and nothing is duplicated.
    /// </summary>
    private static List<T> DedupeByFileName<T>(
        IEnumerable<T> legacyRecords,
        IEnumerable<T> existingRecords,
        Func<T, string> fileNameSelector)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<T>();
        foreach (var record in legacyRecords.Concat(existingRecords))
        {
            var fileName = fileNameSelector(record);
            if (string.IsNullOrWhiteSpace(fileName) || !seen.Add(fileName))
                continue;
            result.Add(record);
        }

        return result;
    }

    // ── Legacy file resolution ──────────────────────────────────────

    private sealed record LegacyFileSet(
        string? FavoritesPath,
        string? HistoryPath,
        string? SettingsPath,
        string? SystemXmlPath);

    private static LegacyFileSet ResolveLegacyFiles(IConfiguration configuration, string? legacyFolderOverride)
    {
        var systemXmlName = configuration.GetValue<string>("SystemXmlPath") ?? "system.xml";
        var portableSystemXml = PathHelper.ResolveRelativeToAppDirectory(systemXmlName)
                                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system.xml");

        string appDataFolder = legacyFolderOverride ?? AppDataPaths.SimpleLauncherDataFolder;
        string portableFolder = legacyFolderOverride ?? AppDomain.CurrentDomain.BaseDirectory;

        return new LegacyFileSet(
            FindNewestLegacyFile(Path.Combine(portableFolder, "favorites.dat"),
                Path.Combine(appDataFolder, "favorites.dat")),
            FindNewestLegacyFile(Path.Combine(portableFolder, "playhistory.dat"),
                Path.Combine(appDataFolder, "playhistory.dat")),
            FindNewestLegacyFile(Path.Combine(portableFolder, "settings.xml"),
                Path.Combine(appDataFolder, "settings.xml")),
            FindNewestLegacyFile(
                legacyFolderOverride is not null
                    ? Path.Combine(legacyFolderOverride, Path.GetFileName(portableSystemXml))
                    : portableSystemXml,
                Path.Combine(appDataFolder, "system.xml")));
    }

    /// <summary>
    ///     Returns the newest existing candidate (portable next to the exe vs. AppData),
    ///     mirroring <see cref="DataFileLocation" /> precedence, or null when neither exists.
    /// </summary>
    private static string? FindNewestLegacyFile(string portablePath, string appDataPath)
    {
        var portableExists = File.Exists(portablePath);
        var appDataExists = File.Exists(appDataPath);

        if (portableExists && appDataExists)
        {
            return new FileInfo(portablePath).LastWriteTimeUtc > new FileInfo(appDataPath).LastWriteTimeUtc
                ? portablePath
                : appDataPath;
        }

        if (portableExists)
            return portablePath;
        return appDataExists ? appDataPath : null;
    }

    // ── Legacy readers ──────────────────────────────────────────────

    private static List<FavoriteRecord> ReadLegacyFavorites(string? path, ILogger logger)
    {
        if (path is null)
            return [];
        try
        {
            var manager = MessagePackSerializer.Deserialize<FavoritesManager>(File.ReadAllBytes(path));
            return manager.FavoriteList
                .Where(static f => !string.IsNullOrWhiteSpace(f.FileName))
                .Select(static f => new FavoriteRecord(f.FileName, f.SystemName ?? ""))
                .ToList();
        }
        catch (Exception ex)
        {
            // Expected user-data condition (a corrupt legacy file simply imports as
            // empty): Information level so it is never reported as a bug.
            logger.Information(ex, "[Migration] Could not read legacy favorites '{Path}'; importing none", path);
            return [];
        }
    }

    private static List<PlayHistoryRecord> ReadLegacyHistory(string? path, ILogger logger)
    {
        if (path is null)
            return [];
        try
        {
            var manager = MessagePackSerializer.Deserialize<PlayHistoryManager>(File.ReadAllBytes(path));
            return manager.PlayHistoryList
                .Where(static h => !string.IsNullOrWhiteSpace(h.FileName))
                .Select(static h => new PlayHistoryRecord(
                    h.FileName,
                    h.SystemName ?? "",
                    h.TimesPlayed,
                    h.TotalPlayTime,
                    h.LastPlayDate ?? "",
                    h.LastPlayTime ?? ""))
                .ToList();
        }
        catch (Exception ex)
        {
            // Expected user-data condition: Information level so it is never reported as a bug.
            logger.Information(ex, "[Migration] Could not read legacy play history '{Path}'; importing none", path);
            return [];
        }
    }

    private static (SettingsManagerService Settings, bool Loaded) ReadLegacySettings(
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        string? settingsPath)
    {
        // Legacy (XML) mode: never opts into the unified database.
        var settings = new SettingsManagerService(configuration, logger, credentialProtector, null);
        if (settingsPath is null)
            return (settings, false);

        // Explicit path (no DataFileLocation side effects): when the file is missing or
        // corrupt the untouched defaults are returned with Loaded=false, and unlike Load()
        // nothing is ever written back.
        var loaded = settings.LoadFromLegacyFile(settingsPath);
        return (settings, loaded);
    }

    private static List<SystemManagerConfig> ReadLegacySystems(
        ILogger logger,
        string? systemXmlPath)
    {
        if (systemXmlPath is null)
            return [];
        try
        {
            // Explicit path: no cache, no database reads, no file rewrites.
            return SystemManagerService.LoadSystemsFromPath(systemXmlPath, logger)
                .Select(SystemManagerService.ToSystemManagerConfig)
                .ToList();
        }
        catch (Exception ex)
        {
            // Expected user-data condition: Information level so it is never reported as a bug.
            logger.Information(ex, "[Migration] Could not read legacy systems '{Path}'; importing none", systemXmlPath);
            return [];
        }
    }

    // ── Verification ────────────────────────────────────────────────

    private static void VerifyMigration(string dbPath, int favorites, int history, int systems,
        bool expectSettings)
    {
        if (!UnifiedSettingsDatabase.IsValidDatabase(dbPath))
            throw new InvalidOperationException("The migrated database failed validation.");

        var backFavorites = UnifiedSettingsDatabase.LoadFavorites(dbPath);
        var backHistory = UnifiedSettingsDatabase.LoadPlayHistory(dbPath);
        var backSystems = UnifiedSettingsDatabase.LoadSystems(dbPath);

        if (backFavorites.Count != favorites)
            throw new InvalidOperationException(
                $"Favorites count mismatch after migration (expected {favorites}, got {backFavorites.Count}).");
        if (backHistory.Count != history)
            throw new InvalidOperationException(
                $"Play-history count mismatch after migration (expected {history}, got {backHistory.Count}).");
        if (backSystems.Count != systems)
            throw new InvalidOperationException(
                $"Systems count mismatch after migration (expected {systems}, got {backSystems.Count}).");

        // Application/emulator settings are only verified when the migration actually
        // wrote them (a merge without a legacy settings file must keep the database rows
        // untouched instead of failing when they happen to be empty).
        if (!expectSettings) return;

        if (UnifiedSettingsDatabase.LoadAppSettings(dbPath).Count == 0)
            throw new InvalidOperationException("Application settings are missing after migration.");
        if (UnifiedSettingsDatabase.LoadEmulatorConfigs(dbPath).Count == 0)
            throw new InvalidOperationException("Emulator settings are missing after migration.");
    }

    // ── Legacy shelving ─────────────────────────────────────────────

    /// <summary>Renames a legacy file to &lt;path&gt;.bak (overwrites any previous backup).</summary>
    /// <returns>1 when a file was shelved, 0 otherwise.</returns>
    private static int ShelveLegacyFile(string? path, ILogger logger)
    {
        if (path is null || !File.Exists(path))
            return 0;
        try
        {
            var backup = path + ".bak";
            File.Move(path, backup, true);
            return 1;
        }
        catch (Exception ex)
        {
            // Shelving is best effort, but a leftover legacy file would be re-read by
            // older app versions — warn loudly instead of only debugging.
            logger.Warning(ex, "[Migration] Migrated but could not shelve legacy file '{Path}'", path);
            return 0;
        }
    }

    /// <summary>Deletes atomic-write leftovers (favorites.dat.tmp, …) next to legacy locations.</summary>
    private static void CleanupTempLeftovers(ILogger logger, string? legacyFolderOverride)
    {
        var folders = legacyFolderOverride is not null
            ? new[] { legacyFolderOverride }
            : new[] { AppDomain.CurrentDomain.BaseDirectory, AppDataPaths.SimpleLauncherDataFolder };

        foreach (var name in new[]
                 {
                     "favorites.dat.tmp", "playhistory.dat.tmp", "settings.xml.tmp", "system.xml.tmp"
                 })
        {
            foreach (var dir in folders)
            {
                try
                {
                    var tmp = Path.Combine(dir, name);
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "[Migration] Could not delete temp leftover '{Name}'", name);
                }
            }
        }
    }
}
