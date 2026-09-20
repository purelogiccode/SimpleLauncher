using System.Globalization;
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
///     a restore), the legacy data is merged into it instead: new entries are appended
///     and the <b>database wins on conflict</b>, so current settings are never reverted
///     by stale files. Both the portable folder and the AppData folder are drained per
///     file type (newest content wins across locations). All six tables are written in
///     a single transaction; the merge path takes a timestamped pre-merge backup first.
///     On success every successfully-read legacy file is renamed to a timestamped
///     <c>.bak</c> (never overwriting); files that failed to parse stay in place so
///     older versions can still read them. A database with a newer schema version is
///     never touched (the migration fails and retries on the next launch).
///     On failure the new database file (if newly created) is deleted and
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
    ///     but redirects the database path and the legacy-file folders so tests never touch
    ///     real user data. <paramref name="legacyFolderOverride" /> stands in for both the
    ///     portable folder and the AppData folder; the portable/app-data overrides allow
    ///     testing the dual-location drain separately.
    /// </summary>
    internal static MigrationResult EnsureMigrated(
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        string? dbPathOverride,
        string? legacyFolderOverride,
        string? portableFolderOverride = null,
        string? appDataFolderOverride = null)
    {
        var dbPath = dbPathOverride ?? UnifiedSettingsDatabase.GetDatabasePath();
        lock (MigrationLock)
        {
            if (_migrationAttempted)
            {
                var alreadyCurrent = UnifiedSettingsDatabase.IsValidDatabase(dbPath);
                if (!alreadyCurrent)
                {
                    // A previous attempt failed: allow the next in-process call to retry
                    // (the legacy files are still present).
                    _migrationAttempted = false;
                }

                return new MigrationResult(
                    alreadyCurrent ? MigrationStatus.AlreadyCurrent : MigrationStatus.Failed,
                    0, 0, 0);
            }

            _migrationAttempted = true;
        }

        var portableFolder = portableFolderOverride ?? legacyFolderOverride ?? AppDomain.CurrentDomain.BaseDirectory;
        var appDataFolder = appDataFolderOverride ?? legacyFolderOverride ?? AppDataPaths.SimpleLauncherDataFolder;
        var foldersOverridden = (portableFolderOverride ?? legacyFolderOverride) is not null;

        if (UnifiedSettingsDatabase.IsValidDatabase(dbPath))
            return MergeLegacyFilesIntoExistingDatabase(
                dbPath, configuration, logger, credentialProtector,
                portableFolder, appDataFolder, foldersOverridden);

        var dbExistedBefore = File.Exists(dbPath);
        var legacyFiles = ResolveLegacyFiles(configuration, portableFolder, appDataFolder, foldersOverridden);

        try
        {
            // ── Read legacy state (best effort; corrupt files become empty) ──
            // Canonicalize exactly the way the database keys the rows, so the round-trip
            // verification below cannot fail on duplicates that the database legitimately
            // collapses (the same file favorited twice, duplicate history file names).
            var favorites = DedupeByKey(
                ReadLegacyFavorites(legacyFiles.FavoritesPaths, logger, out var favoritesOk), [],
                static f => f.FileName + "\u0000" + f.SystemName);
            var history = DedupeByKey(
                ReadLegacyHistory(legacyFiles.HistoryPaths, logger, out var historyOk), [],
                static h => h.FileName + "\u0000" + h.SystemName);
            var (settings, _) = ReadLegacySettings(configuration, logger, credentialProtector,
                legacyFiles.SettingsPaths, out var settingsOk);
            var systems = ReadLegacySystems(logger, legacyFiles.SystemXmlPaths, out var systemsOk,
                out var systemsPartial);

            var anyLegacy = legacyFiles.FavoritesPaths.Count > 0
                            || legacyFiles.HistoryPaths.Count > 0
                            || legacyFiles.SettingsPaths.Count > 0
                            || legacyFiles.SystemXmlPaths.Count > 0;

            // ── Write the database (all six tables in one transaction) ──
            UnifiedSettingsDatabase.EnsureCreated(dbPath);

            // First wins on case-insensitive duplicate system names.
            var systemsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var system in systems)
            {
                if (!systemsByName.ContainsKey(system.SystemName))
                    systemsByName[system.SystemName] = SystemConfigStore.Serialize(system);
            }

            UnifiedSettingsDatabase.SaveAllTables(
                favorites,
                history,
                settings.ExportAppSettings(),
                settings.ExportEmulatorSettings(),
                settings.ExportSystemPlayTimes(),
                systemsByName,
                dbPath);

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

            // ── Shelve legacy files (timestamped .bak; unparseable files stay) ──
            var shelved = 0;
            if (favoritesOk)
                shelved += ShelveLegacyFiles(legacyFiles.FavoritesPaths, logger);
            if (historyOk)
                shelved += ShelveLegacyFiles(legacyFiles.HistoryPaths, logger);
            if (settingsOk)
                shelved += ShelveLegacyFiles(legacyFiles.SettingsPaths, logger);
            if (systemsOk)
                shelved += ShelveLegacyFiles(legacyFiles.SystemXmlPaths, logger, systemsPartial ? "partial" : null);
            if (systemsPartial)
                logger.Warning(
                    "[Migration] system.xml needed partial recovery; shelved as .partial.*.bak — verify all systems were imported ({Count})",
                    systemsByName.Count);
            CleanupTempLeftovers(logger, portableFolder, appDataFolder, foldersOverridden);

            logger.Information(
                "[Migration] Imported {Fav} favorites, {Hist} history entries, {Sys} systems into '{Path}'; shelved {N} legacy files as timestamped .bak",
                favorites.Count, history.Count, systemsByName.Count, dbPath, shelved);
            return new MigrationResult(MigrationStatus.Migrated, favorites.Count, history.Count, systemsByName.Count);
        }
        catch (NewerSchemaVersionException ex)
        {
            // Running an older build against a settings.dat written by a newer one is an
            // expected downgrade condition: leave the database and the legacy files untouched
            // and retry after upgrading. Information keeps it out of the bug-report API.
            logger.Information(ex,
                "[Migration] Database '{Path}' was written by a newer app build; leaving it and the legacy files untouched",
                dbPath);

            lock (MigrationLock)
            {
                _migrationAttempted = false;
            }

            return new MigrationResult(MigrationStatus.Failed, 0, 0, 0);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[Migration] Failed to migrate legacy files into '{Path}'. Legacy files left untouched",
                dbPath);

            // Allow the next in-process call (and the next launch) to retry.
            lock (MigrationLock)
            {
                _migrationAttempted = false;
            }

            // Remove the half-written database so the next launch retries cleanly.
            // (Unlike the old valid-only check, this also removes a valid-but-empty
            // database: EnsureCreated marks the schema valid before any data lands,
            // so validity alone cannot prove the migration completed.)
            try
            {
                if (!dbExistedBefore && File.Exists(dbPath))
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
    ///     folder or AppData): merge the legacy data into the existing database and shelve
    ///     the files. New entries are appended; on conflict the <b>database wins</b> so a
    ///     reappearing stale file can never revert current settings. Application/emulator
    ///     settings are only merged when a legacy <c>settings.xml</c> was actually read
    ///     and holds more than the defaults — otherwise the database values stay untouched
    ///     (merging the service defaults would wipe them). A timestamped pre-merge backup
    ///     of the database is taken first. On failure the database is left untouched and
    ///     the legacy files stay for the next launch to retry.
    /// </summary>
    private static MigrationResult MergeLegacyFilesIntoExistingDatabase(
        string dbPath,
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        string portableFolder,
        string appDataFolder,
        bool foldersOverridden)
    {
        var legacyFiles = ResolveLegacyFiles(configuration, portableFolder, appDataFolder, foldersOverridden);
        var anyLegacy = legacyFiles.FavoritesPaths.Count > 0
                        || legacyFiles.HistoryPaths.Count > 0
                        || legacyFiles.SettingsPaths.Count > 0
                        || legacyFiles.SystemXmlPaths.Count > 0;

        if (!anyLegacy)
            return new MigrationResult(MigrationStatus.AlreadyCurrent, 0, 0, 0);

        try
        {
            // ── Read legacy state (best effort; corrupt files become empty) ──
            var legacyFavorites = ReadLegacyFavorites(legacyFiles.FavoritesPaths, logger, out var favoritesOk);
            var legacyHistory = ReadLegacyHistory(legacyFiles.HistoryPaths, logger, out var historyOk);
            var (legacySettings, legacySettingsLoaded) =
                ReadLegacySettings(configuration, logger, credentialProtector, legacyFiles.SettingsPaths,
                    out var settingsOk);
            var legacySystems = ReadLegacySystems(logger, legacyFiles.SystemXmlPaths, out var systemsOk,
                out var systemsPartial);

            // A settings.xml that holds only default values was almost certainly
            // recreated by an older app version after the migration (downgrade): merging
            // it would revert the user's settings to defaults, so ignore it.
            var settingsEffectiveLoaded = legacySettingsLoaded &&
                                          !IsDefaultsOnlySettings(legacySettings, configuration, logger,
                                              credentialProtector);
            if (legacySettingsLoaded && !settingsEffectiveLoaded)
                logger.Information(
                    "[Migration] Legacy settings.xml holds only default values; keeping the database values");

            // Make sure the existing database has every table and structural upgrade before
            // the first write touches a possibly older schema. A newer-schema database
            // throws here: the files stay untouched and the next launch retries.
            UnifiedSettingsDatabase.EnsureCreated(dbPath);

            // ── Merge (database wins: existing rows come first, legacy fills the gaps) ──
            var existingFavorites = UnifiedSettingsDatabase.LoadFavorites(dbPath);
            var existingHistory = UnifiedSettingsDatabase.LoadPlayHistory(dbPath);
            var existingSystems = UnifiedSettingsDatabase.LoadSystems(dbPath);

            // Favorites/history are (FileName, SystemName) pairs, so key on both: the
            // same file name can exist in two systems and must not collapse into one entry.
            var mergedFavorites = DedupeByKey(existingFavorites, legacyFavorites,
                static f => f.FileName + "\u0000" + f.SystemName);
            var mergedHistory = DedupeByKey(existingHistory, legacyHistory,
                static h => h.FileName + "\u0000" + h.SystemName);

            var mergedSystems = new Dictionary<string, string>(existingSystems, StringComparer.OrdinalIgnoreCase);
            foreach (var system in legacySystems)
            {
                if (!mergedSystems.ContainsKey(system.SystemName))
                    mergedSystems[system.SystemName] = SystemConfigStore.Serialize(system);
            }

            // Application/emulator settings and play times are only merged when the
            // legacy settings file was actually read with real content: without it the
            // service carries defaults, and merging those over the database would wipe
            // the user's theme, language, RA credentials and every emulator configuration.
            // Database values win on conflict; legacy values only fill absent keys.
            var mergedPlayTimes = UnifiedSettingsDatabase.LoadSystemPlayTimes(dbPath)
                .Concat(settingsEffectiveLoaded
                    ? legacySettings.ExportSystemPlayTimes()
                    : [])
                .ToList();
            var mergedAppSettings = new Dictionary<string, string>(
                UnifiedSettingsDatabase.LoadAppSettings(dbPath), StringComparer.OrdinalIgnoreCase);
            var mergedEmulators = new Dictionary<string, string>(
                UnifiedSettingsDatabase.LoadEmulatorConfigs(dbPath), StringComparer.OrdinalIgnoreCase);
            if (settingsEffectiveLoaded)
            {
                foreach (var (key, value) in legacySettings.ExportAppSettings())
                {
                    if (!mergedAppSettings.ContainsKey(key))
                        mergedAppSettings[key] = value ?? "";
                }

                foreach (var (key, value) in legacySettings.ExportEmulatorSettings())
                {
                    if (!mergedEmulators.ContainsKey(key))
                        mergedEmulators[key] = value ?? "{}";
                }
            }

            // ── Pre-merge backup, then write all six tables in one transaction ──
            try
            {
                var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                UnifiedSettingsDatabase.BackupDatabase($"{dbPath}.premerge.{stamp}.bak", dbPath);
            }
            catch (Exception backupEx)
            {
                logger.Warning(backupEx, "[Migration] Could not back up the database before merging; continuing");
            }

            UnifiedSettingsDatabase.SaveAllTables(
                mergedFavorites, mergedHistory, mergedAppSettings, mergedEmulators, mergedPlayTimes, mergedSystems,
                dbPath);

            // ── Verify the round-trip before touching legacy files ──
            VerifyMigration(dbPath, mergedFavorites.Count, mergedHistory.Count, mergedSystems.Count,
                expectSettings: settingsEffectiveLoaded);

            var favoritesAdded = mergedFavorites.Count - existingFavorites.Count;
            var historyAdded = mergedHistory.Count - existingHistory.Count;
            var systemsAdded = mergedSystems.Count - existingSystems.Count;

            // ── Shelve legacy files (timestamped .bak; unparseable files stay) ──
            var shelved = 0;
            if (favoritesOk)
                shelved += ShelveLegacyFiles(legacyFiles.FavoritesPaths, logger);
            if (historyOk)
                shelved += ShelveLegacyFiles(legacyFiles.HistoryPaths, logger);
            if (settingsOk)
                shelved += ShelveLegacyFiles(legacyFiles.SettingsPaths, logger);
            if (systemsOk)
                shelved += ShelveLegacyFiles(legacyFiles.SystemXmlPaths, logger, systemsPartial ? "partial" : null);
            if (systemsPartial)
                logger.Warning(
                    "[Migration] system.xml needed partial recovery; shelved as .partial.*.bak — verify all systems were imported ({Count})",
                    mergedSystems.Count);
            CleanupTempLeftovers(logger, portableFolder, appDataFolder, foldersOverridden);

            logger.Information(
                "[Migration] Merged legacy files into the existing database '{Path}': added {Fav} favorites, {Hist} history entries, {Sys} systems; shelved {N} legacy files as timestamped .bak",
                dbPath, favoritesAdded, historyAdded, systemsAdded, shelved);
            return new MigrationResult(MigrationStatus.Merged, favoritesAdded, historyAdded, systemsAdded);
        }
        catch (NewerSchemaVersionException ex)
        {
            // The database was replaced by a newer build between the validity check and the
            // merge: expected downgrade condition, never a bug (Information).
            logger.Information(ex,
                "[Migration] Database '{Path}' was written by a newer app build; skipping the merge and leaving the legacy files untouched",
                dbPath);

            lock (MigrationLock)
            {
                _migrationAttempted = false;
            }

            return new MigrationResult(MigrationStatus.Failed, 0, 0, 0);
        }
        catch (Exception ex)
        {
            logger.Error(ex,
                "[Migration] Failed to merge legacy files into the existing database '{Path}'. Legacy files left untouched; the next launch retries",
                dbPath);

            // Allow the next in-process call (and the next launch) to retry.
            lock (MigrationLock)
            {
                _migrationAttempted = false;
            }

            return new MigrationResult(MigrationStatus.Failed, 0, 0, 0);
        }
    }

    /// <summary>
    ///     Concatenates the primary records with the secondary ones and removes duplicates
    ///     by key (case-insensitive, first wins), so the primary values win on conflict
    ///     and nothing is duplicated. Fresh migrations pass the legacy records as primary;
    ///     merges pass the existing database rows as primary (database wins).
    /// </summary>
    private static List<T> DedupeByKey<T>(
        IEnumerable<T> primaryRecords,
        IEnumerable<T> secondaryRecords,
        Func<T, string> keySelector)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<T>();
        foreach (var record in primaryRecords.Concat(secondaryRecords))
        {
            var key = keySelector(record);
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                continue;
            result.Add(record);
        }

        return result;
    }

    // ── Legacy file resolution ──────────────────────────────────────

    private sealed record LegacyFileSet(
        IReadOnlyList<string> FavoritesPaths,
        IReadOnlyList<string> HistoryPaths,
        IReadOnlyList<string> SettingsPaths,
        IReadOnlyList<string> SystemXmlPaths);

    private static LegacyFileSet ResolveLegacyFiles(
        IConfiguration configuration,
        string portableFolder,
        string appDataFolder,
        bool foldersOverridden)
    {
        var systemXmlName = configuration.GetValue<string>("SystemXmlPath") ?? "system.xml";
        // Production honors a custom SystemXmlPath (resolved against the app folder);
        // tests redirect with plain folders, so just take the file name there.
        var portableSystemXml = foldersOverridden
            ? Path.Combine(portableFolder, FileNameOrDefault(systemXmlName))
            : PathHelper.ResolveRelativeToAppDirectory(systemXmlName)
              ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system.xml");
        var systemXmlFileName = FileNameOrDefault(Path.GetFileName(portableSystemXml));

        return new LegacyFileSet(
            FindLegacyFiles(
                Path.Combine(portableFolder, "favorites.dat"),
                Path.Combine(appDataFolder, "favorites.dat")),
            FindLegacyFiles(
                Path.Combine(portableFolder, "playhistory.dat"),
                Path.Combine(appDataFolder, "playhistory.dat")),
            FindLegacyFiles(
                Path.Combine(portableFolder, "settings.xml"),
                Path.Combine(appDataFolder, "settings.xml")),
            FindLegacyFiles(portableSystemXml, Path.Combine(appDataFolder, systemXmlFileName)));
    }

    private static string FileNameOrDefault(string? name)
    {
        var fileName = string.IsNullOrEmpty(name) ? null : Path.GetFileName(name);
        return string.IsNullOrEmpty(fileName) ? "system.xml" : fileName;
    }

    /// <summary>
    ///     Returns every existing candidate (portable next to the exe and AppData),
    ///     newest first, so migration drains both locations instead of leaving a stale
    ///     shadow copy behind that would resurrect on the next launch.
    /// </summary>
    private static IReadOnlyList<string> FindLegacyFiles(string portablePath, string appDataPath)
    {
        if (portablePath.Equals(appDataPath, StringComparison.OrdinalIgnoreCase))
            return File.Exists(portablePath) ? [portablePath] : [];

        var portableExists = File.Exists(portablePath);
        var appDataExists = File.Exists(appDataPath);

        if (portableExists && appDataExists)
        {
            return new FileInfo(portablePath).LastWriteTimeUtc > new FileInfo(appDataPath).LastWriteTimeUtc
                ? [portablePath, appDataPath]
                : [appDataPath, portablePath];
        }

        if (portableExists)
            return [portablePath];
        return appDataExists ? [appDataPath] : [];
    }

    // ── Legacy readers ──────────────────────────────────────────────

    private static List<FavoriteRecord> ReadLegacyFavorites(
        IReadOnlyList<string> paths, ILogger logger, out bool readOk)
    {
        readOk = true;
        var result = new List<FavoriteRecord>();
        foreach (var path in paths)
        {
            try
            {
                var manager = MessagePackSerializer.Deserialize<FavoritesManager>(File.ReadAllBytes(path));
                logger.Information("[Migration] Legacy favorites '{Path}' format version {Version}", path,
                    manager.Version);
                if (manager.Version != 1)
                {
                    logger.Information(
                        "[Migration] Unknown favorites format version in '{Path}'; leaving the file in place",
                        path);
                    readOk = false;
                    continue;
                }

                result.AddRange(manager.FavoriteList
                    .Where(static f => !string.IsNullOrWhiteSpace(f.FileName))
                    .Select(static f => new FavoriteRecord(f.FileName, f.SystemName ?? "")));
            }
            catch (Exception ex)
            {
                // Expected user-data condition (a corrupt legacy file simply imports as
                // empty): Information level so it is never reported as a bug. The file
                // stays in place so older versions can still read it.
                logger.Information(ex, "[Migration] Could not read legacy favorites '{Path}'; importing none", path);
                readOk = false;
            }
        }

        return result;
    }

    private static List<PlayHistoryRecord> ReadLegacyHistory(
        IReadOnlyList<string> paths, ILogger logger, out bool readOk)
    {
        readOk = true;
        var result = new List<PlayHistoryRecord>();
        foreach (var path in paths)
        {
            try
            {
                var manager = MessagePackSerializer.Deserialize<PlayHistoryManager>(File.ReadAllBytes(path));
                logger.Information("[Migration] Legacy play history '{Path}' format version {Version}", path,
                    manager.Version);
                if (manager.Version != 1)
                {
                    logger.Information(
                        "[Migration] Unknown play-history format version in '{Path}'; leaving the file in place",
                        path);
                    readOk = false;
                    continue;
                }

                result.AddRange(manager.PlayHistoryList
                    .Where(static h => !string.IsNullOrWhiteSpace(h.FileName))
                    .Select(static h => new PlayHistoryRecord(
                        h.FileName,
                        h.SystemName ?? "",
                        h.TimesPlayed,
                        h.TotalPlayTime,
                        h.LastPlayDate ?? "",
                        h.LastPlayTime ?? "")));
            }
            catch (Exception ex)
            {
                // Expected user-data condition: Information level so it is never reported as a bug.
                logger.Information(ex, "[Migration] Could not read legacy play history '{Path}'; importing none",
                    path);
                readOk = false;
            }
        }

        return result;
    }

    private static (SettingsManagerService Settings, bool Loaded) ReadLegacySettings(
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        IReadOnlyList<string> settingsPaths,
        out bool readOk)
    {
        // Legacy (XML) mode: never opts into the unified database. Newest location
        // first: the first readable file wins. An existing-but-unreadable file marks
        // readOk=false so it stays in place for older versions (never shelved).
        readOk = true;
        foreach (var settingsPath in settingsPaths)
        {
            if (!File.Exists(settingsPath))
                continue;

            var candidate = new SettingsManagerService(configuration, logger, credentialProtector, null);

            // Explicit path (no DataFileLocation side effects): when the file is
            // corrupt the untouched defaults are returned with Loaded=false, and unlike
            // Load() nothing is ever written back.
            if (candidate.LoadFromLegacyFile(settingsPath))
                return (candidate, true);

            readOk = false;
        }

        return (new SettingsManagerService(configuration, logger, credentialProtector, null), false);
    }

    /// <summary>
    ///     Reports whether the loaded settings export is byte-identical to a fresh
    ///     defaults export: such a file was (re)created by an older version with no user
    ///     content, and merging it would revert the database to defaults.
    /// </summary>
    private static bool IsDefaultsOnlySettings(
        SettingsManagerService loaded,
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector)
    {
        var defaults = new SettingsManagerService(configuration, logger, credentialProtector, null);
        return DictionaryContentEqual(defaults.ExportAppSettings(), loaded.ExportAppSettings())
               && DictionaryContentEqual(defaults.ExportEmulatorSettings(), loaded.ExportEmulatorSettings())
               && defaults.ExportSystemPlayTimes().SequenceEqual(loaded.ExportSystemPlayTimes());
    }

    private static bool DictionaryContentEqual(
        IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other) ||
                !string.Equals(value, other, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static List<SystemManagerConfig> ReadLegacySystems(
        ILogger logger,
        IReadOnlyList<string> systemXmlPaths,
        out bool readOk,
        out bool recoveredPartial)
    {
        readOk = true;
        recoveredPartial = false;
        var result = new List<SystemManagerConfig>();
        foreach (var systemXmlPath in systemXmlPaths)
        {
            try
            {
                // Explicit path: no cache, no database reads, no file rewrites.
                var loaded = SystemManagerService.LoadSystemsFromPath(systemXmlPath, logger, out var partial)
                    .Select(SystemManagerService.ToSystemManagerConfig)
                    .ToList();
                if (partial)
                    recoveredPartial = true;
                result.AddRange(loaded);
            }
            catch (Exception ex)
            {
                // Expected user-data condition: Information level so it is never reported as a bug.
                logger.Information(ex, "[Migration] Could not read legacy systems '{Path}'; importing none",
                    systemXmlPath);
                readOk = false;
            }
        }

        return result;
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

    private static int ShelveLegacyFiles(IReadOnlyList<string> paths, ILogger logger, string? tag = null)
    {
        var shelved = 0;
        foreach (var path in paths)
            shelved += ShelveLegacyFile(path, logger, tag);
        return shelved;
    }

    /// <summary>
    ///     Renames a legacy file to a timestamped backup (never overwrites a previous
    ///     backup). An optional tag (e.g. <c>partial</c>) marks incomplete recoveries.
    /// </summary>
    /// <returns>1 when a file was shelved, 0 otherwise.</returns>
    private static int ShelveLegacyFile(string? path, ILogger logger, string? tag = null)
    {
        if (path is null || !File.Exists(path))
            return 0;
        try
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var suffix = tag is null ? $".{stamp}.bak" : $".{tag}.{stamp}.bak";
            var backup = path + suffix;
            for (var i = 2; File.Exists(backup); i++)
                backup = $"{path}{suffix}.{i}";
            File.Move(path, backup, false);
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
    private static void CleanupTempLeftovers(
        ILogger logger, string portableFolder, string appDataFolder, bool foldersOverridden)
    {
        var folders = foldersOverridden
            ? new[] { portableFolder, appDataFolder }
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