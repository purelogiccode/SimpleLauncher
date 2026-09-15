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

    /// <summary>Current schema version stored in the Meta table.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly Lock WriteLock = new();

    /// <summary>
    ///     Test seam: when set, every database operation defaults to this path instead of
    ///     the real AppData file. Lets tests run hermetically without touching user data.
    ///     Production code never sets this (stays null).
    /// </summary>
    internal static string? DatabasePathOverride { get; set; }

    /// <summary>Resolves the database path (always inside the AppData folder, unless overridden by tests).</summary>
    public static string GetDatabasePath()
    {
        return DatabasePathOverride ?? Path.Combine(AppDataPaths.SimpleLauncherDataFolder, DatabaseFileName);
    }

    /// <summary>Returns true when the database file exists on disk.</summary>
    public static bool DatabaseExists(string? dbPath = null)
    {
        return File.Exists(dbPath ?? GetDatabasePath());
    }

    /// <summary>
    ///     Returns true when the file exists, is a readable SQLite database and carries the current schema version.
    /// </summary>
    public static bool IsValidDatabase(string? dbPath = null)
    {
        var path = dbPath ?? GetDatabasePath();
        if (!File.Exists(path))
            return false;

        try
        {
            using var connection = CreateOpenConnection(path);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Meta WHERE Key = 'schema_version';";
            var value = Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            return string.Equals(value, CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[UnifiedSettings] Database validation failed for '{Path}'", path);
            return false;
        }
    }

    /// <summary>
    ///     Creates the database (and parent folder) when missing and ensures all tables exist.
    ///     A corrupt database file is quarantined to a <c>.corrupt.*.bak</c> sibling and recreated.
    /// </summary>
    public static void EnsureCreated(string? dbPath = null)
    {
        var path = dbPath ?? GetDatabasePath();
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        if (File.Exists(path) && !IsValidDatabase(path))
        {
            QuarantineCorruptDatabase(path);
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
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL DEFAULT ''
            );
            """);
        ExecuteNonQuery(connection, transaction, """
            CREATE TABLE IF NOT EXISTS EmulatorSettings (
                EmulatorName TEXT PRIMARY KEY,
                ConfigJson TEXT NOT NULL DEFAULT '{}'
            );
            """);
        ExecuteNonQuery(connection, transaction, """
            CREATE TABLE IF NOT EXISTS Favorites (
                FileName TEXT PRIMARY KEY COLLATE NOCASE,
                SystemName TEXT NOT NULL DEFAULT ''
            );
            """);
        ExecuteNonQuery(connection, transaction, """
            CREATE TABLE IF NOT EXISTS PlayHistory (
                FileName TEXT PRIMARY KEY COLLATE NOCASE,
                SystemName TEXT NOT NULL DEFAULT '',
                TimesPlayed INTEGER NOT NULL DEFAULT 0,
                TotalPlayTime INTEGER NOT NULL DEFAULT 0,
                LastPlayDate TEXT NOT NULL DEFAULT '',
                LastPlayTime TEXT NOT NULL DEFAULT ''
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

    internal static SqliteConnection CreateOpenConnection(string path)
    {
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
                throw new InvalidOperationException(
                    $"The corrupt database '{path}' could not be moved or deleted.", ex);
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

    // ── AppSettings (key/value) ─────────────────────────────────────

    /// <summary>Loads all application settings as a key/value dictionary.</summary>
    public static Dictionary<string, string> LoadAppSettings(string? dbPath = null)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
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
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
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
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
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
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
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

            // First wins on case-insensitive duplicates (the key is COLLATE NOCASE).
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fav in favorites)
            {
                if (string.IsNullOrWhiteSpace(fav.FileName) || !seen.Add(fav.FileName))
                    continue;
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
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FileName, SystemName, TimesPlayed, TotalPlayTime, LastPlayDate, LastPlayTime FROM PlayHistory;";
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

            // First wins on case-insensitive duplicates (the key is COLLATE NOCASE).
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in history)
            {
                // First wins on case-insensitive duplicates (the key is COLLATE NOCASE).
                if (string.IsNullOrWhiteSpace(item.FileName) || !seen.Add(item.FileName))
                    continue;
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
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
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
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
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
        using var connection = CreateOpenConnection(dbPath ?? GetDatabasePath());
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT SystemName, PlayTimeSeconds FROM SystemPlayTimes;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(new SystemPlayTimeRecord(
                reader.GetString(0),
                reader.IsDBNull(1) ? 0L : reader.GetInt64(1)));
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
}
