using MessagePack;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UnifiedSettings;
using ILogger = Serilog.ILogger;

namespace SimpleLauncher.Avalonia.Services.SettingsDatabase;

/// <summary>Outcome of the one-time legacy migration into the unified database.</summary>
public enum MigrationStatus
{
    /// <summary>A valid settings.dat already existed; nothing was done.</summary>
    AlreadyCurrent,

    /// <summary>Legacy files were imported into a fresh settings.dat and shelved as .bak.</summary>
    Migrated,

    /// <summary>No legacy files existed; a fresh settings.dat was seeded with defaults.</summary>
    FreshCreated,

    /// <summary>Migration failed; legacy files were left untouched.</summary>
    Failed
}

/// <summary>Result of <see cref="AvaloniaLegacyMigrator.EnsureMigrated" />.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Favorites">Number of favorites imported.</param>
/// <param name="HistoryEntries">Number of play-history entries imported.</param>
/// <param name="Systems">Number of systems imported.</param>
public sealed record MigrationResult(MigrationStatus Status, int Favorites, int HistoryEntries, int Systems);

/// <summary>
///     One-time migration from the legacy files (<c>favorites.dat</c>, <c>playhistory.dat</c>,
///     <c>settings.xml</c>, <c>system.xml</c>) into the unified SQLite database
///     (<c>settings.dat</c> in AppData).
/// </summary>
/// <remarks>
///     Runs once per process (idempotent). On success every legacy file that was read is
///     renamed to <c>&lt;name&gt;.bak</c> (overwriting any previous backup) so the old
///     paths disappear but the data is recoverable. On failure the database file (if newly
///     created) is deleted and legacy files are left untouched, so the app falls back to
///     the legacy readers and the next launch retries.
/// </remarks>
public static class AvaloniaLegacyMigrator
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
        ICredentialProtector credentialProtector,
        IMessageBoxLibraryService? messageBox = null)
    {
        return EnsureMigrated(configuration, logger, credentialProtector, messageBox, null, null);
    }

    /// <summary>
    ///     Test seam: like <see cref="EnsureMigrated(IConfiguration,ILogger,ICredentialProtector,IMessageBoxLibraryService)" />,
    ///     but redirects the database path and the legacy-file folders (both the portable
    ///     folder and the AppData folder) so tests never touch real user data.
    /// </summary>
    internal static MigrationResult EnsureMigrated(
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        IMessageBoxLibraryService? messageBox,
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
            return new MigrationResult(MigrationStatus.AlreadyCurrent, 0, 0, 0);

        var dbExistedBefore = File.Exists(dbPath);
        var legacyFiles = ResolveLegacyFiles(configuration, legacyFolderOverride);

        try
        {
            // ── Read legacy state (best effort; corrupt files become empty) ──
            var favorites = ReadLegacyFavorites(legacyFiles.FavoritesPath, logger);
            var history = ReadLegacyHistory(legacyFiles.HistoryPath, logger);
            var settings = ReadLegacySettings(configuration, logger, credentialProtector, legacyFiles.SettingsPath);
            var systems = ReadLegacySystems(configuration, logger, legacyFiles.SystemXmlPath);

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
            VerifyMigration(dbPath, favorites.Count, history.Count, systems.Count);

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
                favorites.Count, history.Count, systems.Count, dbPath, shelved);
            return new MigrationResult(MigrationStatus.Migrated, favorites.Count, history.Count, systems.Count);
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
                legacyFolderOverride is not null ? Path.Combine(legacyFolderOverride, Path.GetFileName(portableSystemXml)) : portableSystemXml,
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

    private static SettingsManagerService ReadLegacySettings(
        IConfiguration configuration,
        ILogger logger,
        ICredentialProtector credentialProtector,
        string? settingsPath)
    {
        // Legacy (XML) mode: never opts into the unified database.
        var settings = new SettingsManagerService(configuration, logger, credentialProtector, null);
        if (settingsPath is not null)
        {
            // Explicit path (no DataFileLocation side effects): a missing file keeps
            // the defaults, and unlike Load() nothing is ever written back.
            settings.LoadFromLegacyFile(settingsPath);
        }

        return settings;
    }

    private static List<SystemManagerConfig> ReadLegacySystems(
        IConfiguration configuration,
        ILogger logger,
        string? systemXmlPath)
    {
        if (systemXmlPath is null)
            return [];
        try
        {
            // Null message box: corruption dialogs are skipped during migration.
            // Explicit path: no cache, no database reads, no file rewrites.
            var service = new SystemManagerService(configuration, null);
            return service.LoadSystemsFromPath(systemXmlPath);
        }
        catch (Exception ex)
        {
            // Expected user-data condition: Information level so it is never reported as a bug.
            logger.Information(ex, "[Migration] Could not read legacy systems '{Path}'; importing none", systemXmlPath);
            return [];
        }
    }

    // ── Verification ────────────────────────────────────────────────

    private static void VerifyMigration(string dbPath, int favorites, int history, int systems)
    {
        if (!UnifiedSettingsDatabase.IsValidDatabase(dbPath))
            throw new InvalidOperationException("The migrated database failed validation.");

        var backFavorites = UnifiedSettingsDatabase.LoadFavorites(dbPath);
        var backHistory = UnifiedSettingsDatabase.LoadPlayHistory(dbPath);
        var backSystems = UnifiedSettingsDatabase.LoadSystems(dbPath);
        var backApp = UnifiedSettingsDatabase.LoadAppSettings(dbPath);
        var backEmulators = UnifiedSettingsDatabase.LoadEmulatorConfigs(dbPath);

        if (backFavorites.Count != favorites)
            throw new InvalidOperationException(
                $"Favorites count mismatch after migration (expected {favorites}, got {backFavorites.Count}).");
        if (backHistory.Count != history)
            throw new InvalidOperationException(
                $"Play-history count mismatch after migration (expected {history}, got {backHistory.Count}).");
        if (backSystems.Count != systems)
            throw new InvalidOperationException(
                $"Systems count mismatch after migration (expected {systems}, got {backSystems.Count}).");
        if (backApp.Count == 0)
            throw new InvalidOperationException("Application settings are missing after migration.");
        if (backEmulators.Count == 0)
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
