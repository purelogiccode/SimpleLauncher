using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SimpleLauncher.Core.Services.UnifiedSettings;

/// <summary>
///     Unified SQLite store for SimpleLauncher user data (used by the WPF and Avalonia apps).
///     Replaces favorites.dat, playhistory.dat, settings.xml and system.xml with a
///     single <c>settings.dat</c> SQLite database inside the AppData folder.
/// </summary>
/// <remarks>
///     <para>
///     The file keeps the legacy <c>.dat</c> extension for continuity, but the content
///     is a standard SQLite database (SQLitePCLRaw bundles ship native libraries for
///     Windows, Linux and macOS, so this works on all three platforms).
///     </para>
///     <para>
///     Design notes:
///     <list type="bullet">
///         <item>Short-lived connections per operation (Microsoft.Data.Sqlite connections are not thread-safe).</item>
///         <item>WAL journal mode so readers never block writers.</item>
///         <item>Systems and emulator configs are stored as opaque JSON blobs — the database does not need
///         schema changes when a setting is added.</item>
///         <item>Case-insensitive primary keys (COLLATE NOCASE) match the existing
///         OrdinalIgnoreCase semantics for file names and system names.</item>
///     </list>
///     </para>
/// </remarks>
public static class UnifiedSettingsDatabase
{
    /// <summary>File name of the unified database (SQLite content, legacy extension).</summary>
    public const string DatabaseFileName = "settings.dat";

    /// <summary>
    ///     Current schema version stored in the Meta table. Older databases (version 1+)
    ///     stay valid and are upgraded in place by <see cref="EnsureCreated" />; structural
    ///     upgrades are detected by the stored version (and, for the pre-versioning
    ///     Favorites composite key, structurally via PRAGMA table_info). A database with
    ///     a NEWER version is never opened read-write (see
    ///     <see cref="NewerSchemaVersionException" />): it belongs to a newer app build.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    private static readonly Lock WriteLock = new();

    /// <summary>
    ///     Test seam: when set, every database operation defaults to this path instead of
    ///     the real AppData file. Lets tests run hermetically without touching user data.
    ///     Production code never sets this (stays null).
    /// </summary>
    internal static string? DatabasePathOverride { get; set; }

    /// <summary>
    ///     Resolves the database path, mirroring the legacy <c>DataFileLocation</c>
    ///     precedence so portable installs keep working: an existing portable database
    ///     wins over AppData (newest wins when both exist); otherwise a writable exe
    ///     folder means portable mode, else AppData. Tests redirect via
    ///     <see cref="DatabasePathOverride" />.
    /// </summary>
    public static string GetDatabasePath()
    {
        if (DatabasePathOverride is not null)
            return DatabasePathOverride;

        var portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);
        var appDataPath = Path.Combine(AppDataPaths.SimpleLauncherDataFolder, DatabaseFileName);
        var portableExists = File.Exists(portablePath);
        var appDataExists = File.Exists(appDataPath);

        if (portableExists && !appDataExists)
            return portablePath;
        if (appDataExists && !portableExists)
            return appDataPath;
        if (portableExists)
        {
            // Both exist (e.g. a copy was restored): newest wins, like the legacy files.
            return new FileInfo(portablePath).LastWriteTimeUtc > new FileInfo(appDataPath).LastWriteTimeUtc
                ? portablePath
                : appDataPath;
        }

        return IsDirectoryWritable(AppDomain.CurrentDomain.BaseDirectory) ? portablePath : appDataPath;
    }

    private static bool IsDirectoryWritable(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                return false;

            var testFilePath = Path.Combine(directoryPath, $".write_test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFilePath, "test");
            File.Delete(testFilePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[UnifiedSettings] IsDirectoryWritable failed for '{Path}'", directoryPath);
            return false;
        }
    }

    /// <summary>Returns true when the database file exists on disk.</summary>
    public static bool DatabaseExists(string? dbPath = null)
    {
        return File.Exists(dbPath ?? GetDatabasePath());
    }

    /// <summary>
    ///     Returns true when the file exists, is a readable SQLite database and carries a
    ///     schema version this build understands (1..CurrentSchemaVersion). Read-only:
    ///     validating never creates the file and never clears the read-only attribute.
    /// </summary>
    public static bool IsValidDatabase(string? dbPath = null)
    {
        return TryGetSchemaVersion(dbPath ?? GetDatabasePath()) is { } schemaVersion &&
               schemaVersion >= 1 &&
               schemaVersion <= CurrentSchemaVersion;
    }

    /// <summary>
    ///     Reads the stored schema version without side effects (read-only open: no file
    ///     creation, no read-only-attribute clearing). Returns null when the file is
    ///     missing or unreadable.
    /// </summary>
    internal static int? TryGetSchemaVersion(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var connection = OpenReadConnection(path);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Meta WHERE Key = 'schema_version';";
            var value = Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            // Any readable schema from version 1 up: EnsureCreated upgrades older
            // databases in place, so treating a known-old schema as corrupt would
            // quarantine the user's data instead of migrating it. Newer-than-current
            // schemas are reported (not valid) but never quarantined — see EnsureCreated.
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var schemaVersion)
                ? schemaVersion
                : null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[UnifiedSettings] Database validation failed for '{Path}'", path);
            return null;
        }
    }

    /// <summary>
    ///     Creates the database (and parent folder) when missing and ensures all tables exist.
    ///     A corrupt database file is quarantined to a <c>.corrupt.*.bak</c> sibling and recreated.
    ///     A database with a NEWER schema version is never touched: a
    ///     <see cref="NewerSchemaVersionException" /> is thrown so callers fail safe
    ///     (migration leaves legacy files for the next launch; runtime saves keep state
    ///     in memory) instead of wiping the newer build's data.
    /// </summary>
    public static void EnsureCreated(string? dbPath = null)
    {
        var path = dbPath ?? GetDatabasePath();
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        if (File.Exists(path))
        {
            var storedVersion = TryGetSchemaVersion(path);
            if (storedVersion is null)
            {
                QuarantineCorruptDatabase(path);
            }
            else if (storedVersion > CurrentSchemaVersion)
            {
                Log.Warning(
                    "[UnifiedSettings] Database '{Path}' uses schema version {Version}, newer than this build (version {Current}). Leaving it untouched; upgrade the app to use it.",
                    path, storedVersion, CurrentSchemaVersion);
                throw new NewerSchemaVersionException(path, storedVersion.Value, CurrentSchemaVersion);
            }
        }

        using var connection = CreateOpenConnection(path);
        using var transaction = connection.BeginTransaction();

        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE IF NOT EXISTS Meta (
                                                     Key TEXT PRIMARY KEY,
                                                     Value TEXT NOT NULL
                                                 );
                                                 """);
        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE IF NOT EXISTS AppSettings (
                                                     Key TEXT PRIMARY KEY COLLATE NOCASE,
                                                     Value TEXT NOT NULL DEFAULT ''
                                                 );
                                                 """);
        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE IF NOT EXISTS EmulatorSettings (
                                                     EmulatorName TEXT PRIMARY KEY COLLATE NOCASE,
                                                     ConfigJson TEXT NOT NULL DEFAULT '{}'
                                                 );
                                                 """);
        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE IF NOT EXISTS Favorites (
                                                     FileName TEXT NOT NULL COLLATE NOCASE,
                                                     SystemName TEXT NOT NULL DEFAULT '' COLLATE NOCASE,
                                                     PRIMARY KEY (FileName, SystemName)
                                                 );
                                                 """);
        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE IF NOT EXISTS PlayHistory (
                                                     FileName TEXT NOT NULL COLLATE NOCASE,
                                                     SystemName TEXT NOT NULL DEFAULT '' COLLATE NOCASE,
                                                     TimesPlayed INTEGER NOT NULL DEFAULT 0,
                                                     TotalPlayTime INTEGER NOT NULL DEFAULT 0,
                                                     LastPlayDate TEXT NOT NULL DEFAULT '',
                                                     LastPlayTime TEXT NOT NULL DEFAULT '',
                                                     PRIMARY KEY (FileName, SystemName)
                                                 );
                                                 """);
        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE IF NOT EXISTS Systems (
                                                     SystemName TEXT PRIMARY KEY COLLATE NOCASE,
                                                     ConfigJson TEXT NOT NULL
                                                 );
                                                 """);
        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE IF NOT EXISTS SystemPlayTimes (
                                                     SystemName TEXT PRIMARY KEY COLLATE NOCASE,
                                                     PlayTimeSeconds INTEGER NOT NULL DEFAULT 0
                                                 );
                                                 """);

        // Databases created before the composite key carry a single-column Favorites PK
        // (CREATE TABLE IF NOT EXISTS left them untouched above): rebuild them in place.
        UpgradeFavoritesToCompositeKey(connection, transaction);

        // Schema-version-gated structural upgrades (schema 1 -> 2): case-insensitive
        // keys and the composite play-history key. Fresh databases (no stored version)
        // already use the canonical DDL above and skip the rebuilds.
        if (ReadStoredSchemaVersion(connection, transaction) is { } existingVersion &&
            existingVersion < CurrentSchemaVersion)
        {
            UpgradeSchemaToVersion2(connection, transaction);
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO Meta (Key, Value) VALUES ('schema_version', $v) " +
                              "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
            cmd.Parameters.AddWithValue("$v", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    // ── Connection helpers ──────────────────────────────────────────

    /// <summary>
    ///     Opens a read-only connection: no file creation, no read-only-attribute
    ///     clearing. All pure-read paths (validation, loads, single getters) use this
    ///     so reads never mutate the user's files.
    /// </summary>
    internal static SqliteConnection OpenReadConnection(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            // No pooling: quarantine/delete-then-recreate flows must never reuse a
            // native handle that still points at the moved/deleted file.
            Pooling = false
        }.ToString());

        connection.Open();

        try
        {
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA busy_timeout = 5000;";
            pragma.ExecuteNonQuery();
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }

    internal static SqliteConnection CreateOpenConnection(string path)
    {
        // A settings.dat carrying the Windows read-only attribute (e.g. after a copy,
        // sync or restore that preserved it) makes SQLite fail with SQLITE_READONLY
        // ('attempt to write a readonly database') even for the application that owns
        // it. Clear the attribute (best effort) before opening so reads and writes work.
        // NOTE: only write paths use this method; pure reads use OpenReadConnection
        // and never clear user attributes.
        ClearReadOnlyAttributes(path);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // No pooling: quarantine/delete-then-recreate flows (corrupt database
            // recovery, failed-migration cleanup) must never reuse a native handle
            // that still points at the moved/deleted file.
            Pooling = false
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        try
        {
            using (var pragma = connection.CreateCommand())
            {
                // Transient cross-process locks (restart overlap, AV/indexer, backup)
                // wait instead of failing immediately with SQLITE_BUSY.
                pragma.CommandText = "PRAGMA busy_timeout = 5000;";
                pragma.ExecuteNonQuery();
            }

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                pragma.ExecuteNonQuery();
            }

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA synchronous=NORMAL;";
                pragma.ExecuteNonQuery();
            }

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys=ON;";
                pragma.ExecuteNonQuery();
            }
        }
        catch
        {
            // The caller never receives the connection on this path, so dispose it
            // here — otherwise the native handle (and its file lock) leaks until
            // finalization, which breaks quarantine/delete on Windows.
            connection.Dispose();
            throw;
        }

        return connection;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    ///     Rebuilds a legacy single-column-PK Favorites table as a (FileName, SystemName)
    ///     composite key. Databases created before the composite key silently dropped a
    ///     favorite when the same file name was already favorited in another system.
    /// </summary>
    private static void UpgradeFavoritesToCompositeKey(SqliteConnection connection, SqliteTransaction transaction)
    {
        var hasSystemNameKey = false;
        using (var pragma = connection.CreateCommand())
        {
            pragma.Transaction = transaction;
            pragma.CommandText = "PRAGMA table_info(Favorites);";
            using var reader = pragma.ExecuteReader();
            while (reader.Read())
            {
                // Columns: cid, name, type, notnull, dflt_value, pk (0 = not part of the key).
                if (reader.GetString(1).Equals("SystemName", StringComparison.OrdinalIgnoreCase) &&
                    reader.GetInt32(5) > 0)
                {
                    hasSystemNameKey = true;
                    break;
                }
            }
        }

        if (hasSystemNameKey) return;

        ExecuteNonQuery(connection, transaction, "ALTER TABLE Favorites RENAME TO Favorites_legacy_single_key;");
        ExecuteNonQuery(connection, transaction, """
                                                 CREATE TABLE Favorites (
                                                     FileName TEXT NOT NULL COLLATE NOCASE,
                                                     SystemName TEXT NOT NULL DEFAULT '' COLLATE NOCASE,
                                                     PRIMARY KEY (FileName, SystemName)
                                                 );
                                                 """);
        ExecuteNonQuery(connection, transaction, """
                                                 INSERT OR IGNORE INTO Favorites (FileName, SystemName)
                                                 SELECT FileName, SystemName FROM Favorites_legacy_single_key;
                                                 """);
        ExecuteNonQuery(connection, transaction, "DROP TABLE Favorites_legacy_single_key;");
    }

    private static int? ReadStoredSchemaVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT Value FROM Meta WHERE Key = 'schema_version';";
        var value = Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;
    }

    /// <summary>
    ///     Schema 1 -&gt; 2 structural upgrades: case-insensitive keys
    ///     (<c>AppSettings.Key</c>, <c>EmulatorSettings.EmulatorName</c>,
    ///     <c>Favorites.SystemName</c>) and the composite
    ///     <c>PlayHistory(FileName, SystemName)</c> key, so the database agrees with the
    ///     case-insensitive in-memory dedupe everywhere. Duplicate rows collapse via
    ///     INSERT OR IGNORE (oldest rowid wins); this runs once per database.
    /// </summary>
    private static void UpgradeSchemaToVersion2(SqliteConnection connection, SqliteTransaction transaction)
    {
        RebuildWithCanonicalDdl(
            connection,
            transaction,
            "AppSettings",
            """
            CREATE TABLE AppSettings (
                Key TEXT PRIMARY KEY COLLATE NOCASE,
                Value TEXT NOT NULL DEFAULT ''
            );
            """,
            "Key, Value");

        RebuildWithCanonicalDdl(
            connection,
            transaction,
            "EmulatorSettings",
            """
            CREATE TABLE EmulatorSettings (
                EmulatorName TEXT PRIMARY KEY COLLATE NOCASE,
                ConfigJson TEXT NOT NULL DEFAULT '{}'
            );
            """,
            "EmulatorName, ConfigJson");

        RebuildWithCanonicalDdl(
            connection,
            transaction,
            "Favorites",
            """
            CREATE TABLE Favorites (
                FileName TEXT NOT NULL COLLATE NOCASE,
                SystemName TEXT NOT NULL DEFAULT '' COLLATE NOCASE,
                PRIMARY KEY (FileName, SystemName)
            );
            """,
            "FileName, SystemName");

        RebuildWithCanonicalDdl(
            connection,
            transaction,
            "PlayHistory",
            """
            CREATE TABLE PlayHistory (
                FileName TEXT NOT NULL COLLATE NOCASE,
                SystemName TEXT NOT NULL DEFAULT '' COLLATE NOCASE,
                TimesPlayed INTEGER NOT NULL DEFAULT 0,
                TotalPlayTime INTEGER NOT NULL DEFAULT 0,
                LastPlayDate TEXT NOT NULL DEFAULT '',
                LastPlayTime TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (FileName, SystemName)
            );
            """,
            "FileName, SystemName, TimesPlayed, TotalPlayTime, LastPlayDate, LastPlayTime");
    }

    private static void RebuildWithCanonicalDdl(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string createTableSql,
        string columns)
    {
        ExecuteNonQuery(connection, transaction, $"ALTER TABLE {tableName} RENAME TO {tableName}_upgrade_bak;");
        ExecuteNonQuery(connection, transaction, createTableSql);
        ExecuteNonQuery(connection, transaction,
            $"INSERT OR IGNORE INTO {tableName} ({columns}) SELECT {columns} FROM {tableName}_upgrade_bak;");
        ExecuteNonQuery(connection, transaction, $"DROP TABLE {tableName}_upgrade_bak;");
    }

    /// <summary>
    ///     Clears the read-only attribute from the database file and its journal siblings.
    ///     SQLite reports <c>SQLITE_READONLY</c> when the file attribute is set, which would
    ///     otherwise make every settings save fail until the user fixes the attribute by hand.
    /// </summary>
    internal static void ClearReadOnlyAttributes(string path)
    {
        foreach (var candidate in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var attributes = File.GetAttributes(candidate);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.None) continue;

                File.SetAttributes(candidate, attributes & ~FileAttributes.ReadOnly);
                Log.Information("[UnifiedSettings] Cleared the read-only attribute on '{Path}'", candidate);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[UnifiedSettings] Failed to clear the read-only attribute on '{Path}'", candidate);
            }
        }
    }

    private static void QuarantineCorruptDatabase(string path)
    {
        try
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var backup = $"{path}.corrupt.{stamp}.bak";
            File.Move(path, backup);
            // A corrupt database may leave -wal/-shm/-journal siblings behind; they
            // would poison the recreated database, so remove them as well.
            DeleteJournalSiblings(path);

            if (File.Exists(path))
                throw new IOException($"The corrupt database '{path}' could not be moved (still locked).");

            Log.Warning("[UnifiedSettings] Quarantined corrupt database '{Path}' to '{Backup}'", path, backup);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[UnifiedSettings] Failed to quarantine corrupt database '{Path}'", path);
            try
            {
                File.Delete(path);
            }
            catch (Exception deleteEx)
            {
                Log.Debug(deleteEx, "[UnifiedSettings] Failed to delete corrupt database '{Path}'", path);
            }

            DeleteJournalSiblings(path);

            if (File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"The corrupt database '{path}' could not be moved or deleted.", ex);
            }
        }
    }

    /// <summary>Deletes SQLite journal siblings (-wal/-shm/-journal) next to a database path (best effort).</summary>
    internal static void DeleteJournalSiblings(string path)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            try
            {
                var sibling = path + suffix;
                if (File.Exists(sibling))
                    File.Delete(sibling);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[UnifiedSettings] Failed to delete journal sibling '{Path}'", path + suffix);
            }
        }
    }

    /// <summary>
    ///     Creates a consistent copy of the database (including any content still in the
    ///     write-ahead log) at <paramref name="destinationPath" /> using SQLite's
    ///     <c>VACUUM INTO</c>. A plain <c>File.Copy</c> of a WAL database can silently omit
    ///     the most recent transactions.
    /// </summary>
    public static void BackupDatabase(string destinationPath, string? dbPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var sourcePath = dbPath ?? GetDatabasePath();
        lock (WriteLock)
        {
            // VACUUM INTO refuses to overwrite an existing file.
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            using var connection = CreateOpenConnection(sourcePath);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "VACUUM INTO $dest;";
            cmd.Parameters.AddWithValue("$dest", destinationPath);
            cmd.ExecuteNonQuery();
        }
    }

    // ── AppSettings (key/value) ─────────────────────────────────────

    /// <summary>Loads all application settings as a key/value dictionary.</summary>
    public static Dictionary<string, string> LoadAppSettings(string? dbPath = null)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM AppSettings;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
        return result;
    }

    /// <summary>Replaces the whole AppSettings table with the supplied values.</summary>
    public static void SaveAppSettings(IReadOnlyDictionary<string, string> values, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM AppSettings;";
                clear.ExecuteNonQuery();
            }

            foreach (var kvp in values)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ($k, $v);";
                cmd.Parameters.AddWithValue("$k", kvp.Key);
                cmd.Parameters.AddWithValue("$v", kvp.Value ?? "");
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>Reads a single application setting (null when absent).</summary>
    public static string? GetAppSetting(string key, string? dbPath = null)
    {
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar()?.ToString();
    }

    /// <summary>Upserts a single application setting.</summary>
    public static void SetAppSetting(string key, string value, string? dbPath = null)
    {
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ($k, $v) " +
                              "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value ?? "");
            cmd.ExecuteNonQuery();
        }
    }

    // ── EmulatorSettings (opaque JSON per emulator) ─────────────────

    /// <summary>Loads all emulator configs (emulator name → JSON blob).</summary>
    public static Dictionary<string, string> LoadEmulatorConfigs(string? dbPath = null)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EmulatorName, ConfigJson FROM EmulatorSettings;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result[reader.GetString(0)] = reader.IsDBNull(1) ? "{}" : reader.GetString(1);
        return result;
    }

    /// <summary>Upserts one emulator config blob.</summary>
    public static void SaveEmulatorConfig(string emulatorName, string configJson, string? dbPath = null)
    {
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO EmulatorSettings (EmulatorName, ConfigJson) VALUES ($n, $j) " +
                              "ON CONFLICT(EmulatorName) DO UPDATE SET ConfigJson = excluded.ConfigJson;";
            cmd.Parameters.AddWithValue("$n", emulatorName);
            cmd.Parameters.AddWithValue("$j", configJson ?? "{}");
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Replaces all emulator config blobs.</summary>
    public static void SaveAllEmulatorConfigs(IReadOnlyDictionary<string, string> configs, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(configs);
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM EmulatorSettings;";
                clear.ExecuteNonQuery();
            }

            foreach (var kvp in configs)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO EmulatorSettings (EmulatorName, ConfigJson) VALUES ($n, $j);";
                cmd.Parameters.AddWithValue("$n", kvp.Key);
                cmd.Parameters.AddWithValue("$j", kvp.Value ?? "{}");
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    // ── Favorites ───────────────────────────────────────────────────

    /// <summary>Loads all favorites ordered by file name (matches legacy sorted-write behavior).</summary>
    public static List<FavoriteRecord> LoadFavorites(string? dbPath = null)
    {
        var result = new List<FavoriteRecord>();
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FileName, SystemName FROM Favorites ORDER BY FileName COLLATE NOCASE;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(new FavoriteRecord(reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));
        return result;
    }

    /// <summary>Replaces the whole Favorites table.</summary>
    public static void SaveFavorites(IReadOnlyList<FavoriteRecord> favorites, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM Favorites;";
                clear.ExecuteNonQuery();
            }

            // First wins on case-insensitive (FileName, SystemName) duplicates — the same
            // file name may be a favorite in more than one system.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fav in favorites)
            {
                if (string.IsNullOrWhiteSpace(fav.FileName) ||
                    !seen.Add(fav.FileName + "\u0000" + fav.SystemName))
                {
                    continue;
                }

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO Favorites (FileName, SystemName) VALUES ($f, $s);";
                cmd.Parameters.AddWithValue("$f", fav.FileName);
                cmd.Parameters.AddWithValue("$s", fav.SystemName ?? "");
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    // ── PlayHistory ─────────────────────────────────────────────────

    /// <summary>Loads the full play-history table.</summary>
    public static List<PlayHistoryRecord> LoadPlayHistory(string? dbPath = null)
    {
        var result = new List<PlayHistoryRecord>();
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT FileName, SystemName, TimesPlayed, TotalPlayTime, LastPlayDate, LastPlayTime FROM PlayHistory;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new PlayHistoryRecord(
                reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                reader.IsDBNull(3) ? 0L : reader.GetInt64(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4),
                reader.IsDBNull(5) ? "" : reader.GetString(5)));
        }

        return result;
    }

    /// <summary>Replaces the whole PlayHistory table.</summary>
    public static void SavePlayHistory(IReadOnlyList<PlayHistoryRecord> history, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(history);
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM PlayHistory;";
                clear.ExecuteNonQuery();
            }

            // First wins on case-insensitive (FileName, SystemName) duplicates — the
            // same file path may have history in more than one system.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in history)
            {
                if (string.IsNullOrWhiteSpace(item.FileName) ||
                    !seen.Add(item.FileName + "\u0000" + item.SystemName))
                {
                    continue;
                }

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = """
                                  INSERT INTO PlayHistory (FileName, SystemName, TimesPlayed, TotalPlayTime, LastPlayDate, LastPlayTime)
                                  VALUES ($f, $s, $t, $p, $d, $ti);
                                  """;
                cmd.Parameters.AddWithValue("$f", item.FileName);
                cmd.Parameters.AddWithValue("$s", item.SystemName ?? "");
                cmd.Parameters.AddWithValue("$t", item.TimesPlayed);
                cmd.Parameters.AddWithValue("$p", item.TotalPlayTime);
                cmd.Parameters.AddWithValue("$d", item.LastPlayDate ?? "");
                cmd.Parameters.AddWithValue("$ti", item.LastPlayTime ?? "");
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    // ── Systems (opaque JSON per system) ────────────────────────────

    /// <summary>Loads all systems (system name → JSON blob).</summary>
    public static Dictionary<string, string> LoadSystems(string? dbPath = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT SystemName, ConfigJson FROM Systems;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result[reader.GetString(0)] = reader.IsDBNull(1) ? "{}" : reader.GetString(1);
        return result;
    }

    /// <summary>Upserts one system blob.</summary>
    public static void SaveSystem(string systemName, string configJson, string? dbPath = null)
    {
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Systems (SystemName, ConfigJson) VALUES ($n, $j) " +
                              "ON CONFLICT(SystemName) DO UPDATE SET ConfigJson = excluded.ConfigJson;";
            cmd.Parameters.AddWithValue("$n", systemName);
            cmd.Parameters.AddWithValue("$j", configJson);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Replaces all system blobs (used by migration and full rewrites).</summary>
    public static void SaveAllSystems(IReadOnlyDictionary<string, string> systems, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(systems);
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM Systems;";
                clear.ExecuteNonQuery();
            }

            foreach (var kvp in systems)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO Systems (SystemName, ConfigJson) VALUES ($n, $j);";
                cmd.Parameters.AddWithValue("$n", kvp.Key);
                cmd.Parameters.AddWithValue("$j", kvp.Value);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>Deletes one system by name (case-insensitive).</summary>
    public static void DeleteSystem(string systemName, string? dbPath = null)
    {
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Systems WHERE SystemName = $n COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("$n", systemName);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Renames a system (case-insensitive match).</summary>
    public static void RenameSystem(string oldName, string newName, string? dbPath = null)
    {
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Systems SET SystemName = $new WHERE SystemName = $old COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("$new", newName);
            cmd.Parameters.AddWithValue("$old", oldName);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Returns true when a system with the given name exists (case-insensitive).</summary>
    public static bool SystemExists(string systemName, string? dbPath = null)
    {
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Systems WHERE SystemName = $n COLLATE NOCASE LIMIT 1;";
        cmd.Parameters.AddWithValue("$n", systemName);
        return cmd.ExecuteScalar() is not null;
    }

    // ── SystemPlayTimes ─────────────────────────────────────────────

    /// <summary>Loads all per-system play-time records.</summary>
    public static List<SystemPlayTimeRecord> LoadSystemPlayTimes(string? dbPath = null)
    {
        var result = new List<SystemPlayTimeRecord>();
        using var connection = OpenReadConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT SystemName, PlayTimeSeconds FROM SystemPlayTimes;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SystemPlayTimeRecord(
                reader.GetString(0),
                reader.IsDBNull(1) ? 0L : reader.GetInt64(1)));
        }

        return result;
    }

    /// <summary>Replaces the whole SystemPlayTimes table.</summary>
    public static void SaveSystemPlayTimes(IReadOnlyList<SystemPlayTimeRecord> playTimes, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(playTimes);
        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var transaction = connection.BeginTransaction();
            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM SystemPlayTimes;";
                clear.ExecuteNonQuery();
            }

            // First wins on case-insensitive duplicates (the key is COLLATE NOCASE).
            var seenSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pt in playTimes)
            {
                // First wins on case-insensitive duplicates (the key is COLLATE NOCASE).
                if (string.IsNullOrWhiteSpace(pt.SystemName) || !seenSystems.Add(pt.SystemName))
                    continue;
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO SystemPlayTimes (SystemName, PlayTimeSeconds) VALUES ($n, $s);";
                cmd.Parameters.AddWithValue("$n", pt.SystemName);
                cmd.Parameters.AddWithValue("$s", pt.PlayTimeSeconds);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>
    ///     Replaces all six tables in a single connection and a single transaction, so a
    ///     migration either commits fully or not at all (no torn database is ever
    ///     visible to other launches). Uses the same first-wins dedupe as the per-table
    ///     saves, so counts stay consistent with the migration verification.
    /// </summary>
    public static void SaveAllTables(
        IReadOnlyList<FavoriteRecord> favorites,
        IReadOnlyList<PlayHistoryRecord> history,
        IReadOnlyDictionary<string, string> appSettings,
        IReadOnlyDictionary<string, string> emulatorConfigs,
        IReadOnlyList<SystemPlayTimeRecord> playTimes,
        IReadOnlyDictionary<string, string> systems,
        string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(emulatorConfigs);
        ArgumentNullException.ThrowIfNull(playTimes);
        ArgumentNullException.ThrowIfNull(systems);

        lock (WriteLock)
        {
            using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
            using var transaction = connection.BeginTransaction();

            ExecuteNonQuery(connection, transaction, "DELETE FROM Favorites;");
            var seenFavorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fav in favorites)
            {
                if (string.IsNullOrWhiteSpace(fav.FileName) ||
                    !seenFavorites.Add(fav.FileName + "\u0000" + fav.SystemName))
                {
                    continue;
                }

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO Favorites (FileName, SystemName) VALUES ($f, $s);";
                cmd.Parameters.AddWithValue("$f", fav.FileName);
                cmd.Parameters.AddWithValue("$s", fav.SystemName ?? "");
                cmd.ExecuteNonQuery();
            }

            ExecuteNonQuery(connection, transaction, "DELETE FROM PlayHistory;");
            var seenHistory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in history)
            {
                if (string.IsNullOrWhiteSpace(item.FileName) ||
                    !seenHistory.Add(item.FileName + "\u0000" + item.SystemName))
                {
                    continue;
                }

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = """
                                  INSERT INTO PlayHistory (FileName, SystemName, TimesPlayed, TotalPlayTime, LastPlayDate, LastPlayTime)
                                  VALUES ($f, $s, $t, $p, $d, $ti);
                                  """;
                cmd.Parameters.AddWithValue("$f", item.FileName);
                cmd.Parameters.AddWithValue("$s", item.SystemName ?? "");
                cmd.Parameters.AddWithValue("$t", item.TimesPlayed);
                cmd.Parameters.AddWithValue("$p", item.TotalPlayTime);
                cmd.Parameters.AddWithValue("$d", item.LastPlayDate ?? "");
                cmd.Parameters.AddWithValue("$ti", item.LastPlayTime ?? "");
                cmd.ExecuteNonQuery();
            }

            ExecuteNonQuery(connection, transaction, "DELETE FROM AppSettings;");
            foreach (var kvp in appSettings)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ($k, $v);";
                cmd.Parameters.AddWithValue("$k", kvp.Key);
                cmd.Parameters.AddWithValue("$v", kvp.Value ?? "");
                cmd.ExecuteNonQuery();
            }

            ExecuteNonQuery(connection, transaction, "DELETE FROM EmulatorSettings;");
            foreach (var kvp in emulatorConfigs)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO EmulatorSettings (EmulatorName, ConfigJson) VALUES ($n, $j);";
                cmd.Parameters.AddWithValue("$n", kvp.Key);
                cmd.Parameters.AddWithValue("$j", kvp.Value ?? "{}");
                cmd.ExecuteNonQuery();
            }

            ExecuteNonQuery(connection, transaction, "DELETE FROM SystemPlayTimes;");
            var seenSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pt in playTimes)
            {
                if (string.IsNullOrWhiteSpace(pt.SystemName) || !seenSystems.Add(pt.SystemName))
                    continue;
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO SystemPlayTimes (SystemName, PlayTimeSeconds) VALUES ($n, $s);";
                cmd.Parameters.AddWithValue("$n", pt.SystemName);
                cmd.Parameters.AddWithValue("$s", pt.PlayTimeSeconds);
                cmd.ExecuteNonQuery();
            }

            ExecuteNonQuery(connection, transaction, "DELETE FROM Systems;");
            foreach (var kvp in systems)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO Systems (SystemName, ConfigJson) VALUES ($n, $j);";
                cmd.Parameters.AddWithValue("$n", kvp.Key);
                cmd.Parameters.AddWithValue("$j", kvp.Value);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }
}