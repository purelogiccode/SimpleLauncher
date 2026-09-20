using System.Collections.ObjectModel;
using System.Globalization;
using MessagePack;
using Microsoft.Data.Sqlite;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.UnifiedSettings;
using ILogger = Serilog.ILogger;

namespace SimpleLauncher.Avalonia.Services.PlayHistory;

/// <summary>
///     Manages play history tracking and persistence.
///     The Avalonia app persists history in the unified SQLite database
///     (<c>settings.dat</c> in AppData); the legacy MessagePack <c>playhistory.dat</c>
///     format is only read during the one-time migration (and as a fallback when no
///     database exists yet).
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class PlayHistoryManager
{
    private const string IsoDateFormat = "yyyy-MM-dd";
    private const string IsoTimeFormat = "HH:mm:ss";
    [IgnoreMember] private static readonly DataFileLocation FileLocation = new("playhistory.dat");
    [IgnoreMember] private readonly Lock _historyLock = new();
    [IgnoreMember] private ILogger? _logger;

    [Key(0)] public ObservableCollection<PlayHistoryItem> PlayHistoryList { get; set; } = [];

    [Key(1)] public int Version { get; set; } = 1;

    private static string FilePath => FileLocation.FilePath;
    private static string TempFilePath => FileLocation.TempFilePath;
    public static bool IsPortableMode => FileLocation.IsPortableMode;

    /// <summary>Loads play history from the unified database (callers must have verified it is valid).</summary>
    private static PlayHistoryManager LoadFromDatabase(ILogger? logErrors)
    {
        var manager = new PlayHistoryManager { _logger = logErrors };
        foreach (var record in UnifiedSettingsDatabase.LoadPlayHistory())
        {
            if (string.IsNullOrWhiteSpace(record.FileName))
                continue;
            manager.PlayHistoryList.Add(new PlayHistoryItem
            {
                FileName = record.FileName,
                SystemName = record.SystemName ?? "",
                TimesPlayed = record.TimesPlayed,
                TotalPlayTime = record.TotalPlayTime,
                LastPlayDate = record.LastPlayDate ?? "",
                LastPlayTime = record.LastPlayTime ?? ""
            });
        }

        logErrors?.Information("Loaded {Count} play history item(s) from the unified database",
            manager.PlayHistoryList.Count);

        return manager;
    }

    /// <summary>Saves a snapshot of the list to the unified database.</summary>
    private void SaveToDatabase()
    {
        List<PlayHistoryRecord> snapshot;
        lock (_historyLock)
        {
            snapshot = PlayHistoryList
                .Where(static item => !string.IsNullOrWhiteSpace(item.FileName))
                .Select(static item => new PlayHistoryRecord(
                    item.FileName,
                    item.SystemName ?? "",
                    item.TimesPlayed,
                    item.TotalPlayTime,
                    item.LastPlayDate ?? "",
                    item.LastPlayTime ?? ""))
                .ToList();
        }

        UnifiedSettingsDatabase.EnsureCreated();
        UnifiedSettingsDatabase.SavePlayHistory(snapshot);
    }

    /// <summary>
    ///     Loads play history from the unified database when it exists, from the legacy
    ///     MessagePack file when it does not (pre-migration), or creates a new database-backed instance.
    /// </summary>
    public static PlayHistoryManager LoadPlayHistory(ILogger? logErrors = null)
    {
        if (UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                return LoadFromDatabase(logErrors);
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, "Error loading play history from the unified database; trying the legacy file");
            }
        }
        else if (!File.Exists(FilePath))
        {
            // Fresh start with neither a database nor a legacy file: create the
            // database instead of a legacy playhistory.dat.
            try
            {
                UnifiedSettingsDatabase.EnsureCreated();
                var fresh = new PlayHistoryManager { _logger = logErrors };
                fresh.SaveToDatabase();
                return fresh;
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, "Error creating play history in the unified database; using a legacy file");
            }
        }

        if (!File.Exists(FilePath))
        {
            var defaultManager = new PlayHistoryManager { _logger = logErrors };
            // Write the initial file synchronously. This runs on the UI thread at startup —
            // awaiting the async save via GetAwaiter().GetResult() would deadlock.
            defaultManager.SavePlayHistorySync();
            return defaultManager;
        }

        try
        {
            var bytes = File.ReadAllBytes(FilePath);
            var manager = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);
            manager._logger = logErrors;
            logErrors?.Information("Loaded {Count} play history item(s) from playhistory.dat",
                manager.PlayHistoryList.Count);
            return manager;
        }
        catch (Exception ex)
        {
            logErrors?.Error(ex, "Error loading playhistory.dat");
        }

        var newManager = new PlayHistoryManager { _logger = logErrors };
        // Synchronous recovery save (same UI-thread deadlock protection as above)
        newManager.SavePlayHistorySync();
        return newManager;
    }

    /// <summary>
    ///     Synchronous initial save (startup/recovery paths only). Writes to the unified
    ///     database when it exists; otherwise mirrors
    ///     <see cref="SavePlayHistoryAsync" /> with retry logic and AppData fallback,
    ///     but never awaits — safe to call on the UI thread.
    /// </summary>
    private void SavePlayHistorySync()
    {
        if (UnifiedSettingsDatabase.DatabaseFileExists())
        {
            try
            {
                SaveToDatabase();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException
                                           or NewerSchemaVersionException)
            {
                // Environment issue (locked/read-only/newer database): never resurrect a
                // legacy playhistory.dat once the unified database exists. Keep the
                // in-memory state instead.
                _logger?.Information(ex,
                    "Error saving play history to the unified database; keeping in-memory state");
                return;
            }
            catch (Exception ex)
            {
                // Unexpected errors stay visible but must not resurrect the legacy file.
                _logger?.Error(ex, "Error saving play history to the unified database");
                return;
            }
        }

        const int maxRetries = 3;
        var retryDelayMs = 100;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                var bytes = SerializeSnapshot();
                File.WriteAllBytes(TempFilePath, bytes);
                AtomicReplace(TempFilePath, FilePath);
                return;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                _logger?.Warning(ex, "Error saving playhistory.dat (attempt {Attempt})", attempt + 1);
                attempt++;

                // If in portable mode and retries exhausted, fall back to LocalAppData
                if (IsPortableMode && attempt >= maxRetries)
                {
                    try
                    {
                        if (FileLocation.TryFallbackToLocalAppData())
                        {
                            attempt = 0;
                            continue;
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger?.Debug("FallbackToLocalAppData failed: {Message}", fallbackEx.Message);
                    }
                }

                if (attempt < maxRetries)
                {
                    try
                    {
                        if (File.Exists(TempFilePath)) File.Delete(TempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }

                    Thread.Sleep(retryDelayMs);
                    retryDelayMs *= 2;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error saving playhistory.dat (attempt {Attempt})", attempt + 1);
                attempt++;
                if (attempt < maxRetries) Thread.Sleep(retryDelayMs);
            }
        }
    }

    /// <summary>
    ///     Serializes the legacy MessagePack snapshot while holding the history lock so a
    ///     concurrent play-record update cannot mutate the collection mid-serialization
    ///     (which threw "Collection was modified" or persisted a partial list).
    /// </summary>
    private byte[] SerializeSnapshot()
    {
        lock (_historyLock)
        {
            return MessagePackSerializer.Serialize(this);
        }
    }

    /// <summary>
    ///     Replaces the destination file atomically. Falls back to delete-then-move
    ///     when the OS refuses an in-place overwrite (e.g. file locked by another handle).
    /// </summary>
    private static void AtomicReplace(string tempPath, string destPath)
    {
        try
        {
            File.Move(tempPath, destPath, true);
        }
        catch (IOException)
        {
            // Destination may be locked — delete first, then move.
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(tempPath, destPath, false);
        }
    }

    /// <summary>
    ///     Saves play history atomically with retry logic and AppData fallback.
    ///     Writes to the unified database when it exists; otherwise falls back to the legacy file.
    ///     Once a valid database exists the legacy file is never rewritten; failures keep in-memory state.
    /// </summary>
    public async Task SavePlayHistoryAsync()
    {
        if (UnifiedSettingsDatabase.DatabaseFileExists())
        {
            try
            {
                SaveToDatabase();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException
                                           or NewerSchemaVersionException)
            {
                // Environment issue (locked/read-only/newer database): never resurrect a
                // legacy playhistory.dat once the unified database exists. Keep the
                // in-memory state instead.
                _logger?.Information(ex,
                    "Error saving play history to the unified database; keeping in-memory state");
                return;
            }
            catch (Exception ex)
            {
                // Unexpected errors stay visible but must not resurrect the legacy file.
                _logger?.Error(ex, "Error saving play history to the unified database");
                return;
            }
        }

        const int maxRetries = 3;
        var retryDelayMs = 100;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                var bytes = SerializeSnapshot();
                await File.WriteAllBytesAsync(TempFilePath, bytes);
                AtomicReplace(TempFilePath, FilePath);
                return;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                _logger?.Warning(ex, "Error saving playhistory.dat (attempt {Attempt})", attempt + 1);
                attempt++;

                // If in portable mode and retries exhausted, fall back to LocalAppData
                if (IsPortableMode && attempt >= maxRetries)
                {
                    try
                    {
                        if (FileLocation.TryFallbackToLocalAppData())
                        {
                            attempt = 0;
                            continue;
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger?.Debug("FallbackToLocalAppData failed: {Message}", fallbackEx.Message);
                    }
                }

                if (attempt < maxRetries)
                {
                    try
                    {
                        if (File.Exists(TempFilePath)) File.Delete(TempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }

                    await Task.Delay(retryDelayMs);
                    retryDelayMs *= 2;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error saving playhistory.dat (attempt {Attempt})", attempt + 1);
                attempt++;
                if (attempt < maxRetries) await Task.Delay(retryDelayMs);
            }
        }
    }

    /// <summary>
    ///     Renames the system in all play history entries (used when a system is renamed in Edit System).
    /// </summary>
    /// <param name="oldSystemName">The previous system name.</param>
    /// <param name="newSystemName">The new system name.</param>
    /// <returns>A task representing the save operation (no-op when nothing changed).</returns>
    public async Task RenameSystemAsync(string oldSystemName, string newSystemName)
    {
        var changed = false;
        lock (_historyLock)
        {
            foreach (var item in PlayHistoryList)
            {
                if (item.SystemName.Equals(oldSystemName, StringComparison.OrdinalIgnoreCase))
                {
                    item.SystemName = newSystemName;
                    changed = true;
                }
            }
        }

        if (changed) await SavePlayHistoryAsync();
    }

    /// <summary>
    ///     Records a play event for the given game. Increments play count and updates timestamps.
    /// </summary>
    public Task RecordPlayAsync(string filePath, string systemName, long playTimeSeconds = 0)
    {
        lock (_historyLock)
        {
            var entry = PlayHistoryList.FirstOrDefault(h =>
                string.Equals(h.FileName, filePath, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                entry = new PlayHistoryItem
                {
                    FileName = filePath,
                    SystemName = systemName
                };
                PlayHistoryList.Add(entry);
            }

            entry.TimesPlayed++;
            entry.TotalPlayTime += playTimeSeconds;
            entry.LastPlayDate = DateTime.Now.ToString(IsoDateFormat, CultureInfo.InvariantCulture);
            entry.LastPlayTime = DateTime.Now.ToString(IsoTimeFormat, CultureInfo.InvariantCulture);
        }

        return SavePlayHistoryAsync();
    }

    /// <summary>
    ///     Migrates old records that only contain filenames to full absolute paths
    ///     (legacy WPF history entries written before full-path recording). Runs once
    ///     at startup with the current system configuration.
    /// </summary>
    /// <param name="systemManagers">The configured systems used to resolve missing files.</param>
    /// <returns>A task representing the save operation (no-op when nothing changed).</returns>
    public async Task MigrateFilenamesToFullPathsAsync(List<SystemManagerConfig> systemManagers)
    {
        var needsSave = false;
        lock (_historyLock)
        {
            foreach (var item in PlayHistoryList)
            {
                // If the path is not rooted, it's an old "filename only" record
                if (Path.IsPathRooted(item.FileName)) continue;

                var system = systemManagers.FirstOrDefault(s =>
                    s.SystemName.Equals(item.SystemName, StringComparison.OrdinalIgnoreCase));
                if (system is null) continue;

                var resolvedPath = PathHelper.FindFileInSystemFolders(system.SystemFolders, item.FileName);
                if (!string.IsNullOrEmpty(resolvedPath))
                {
                    item.FileName = resolvedPath;
                    needsSave = true;
                }
            }
        }

        if (needsSave) await SavePlayHistoryAsync();
    }

    /// <summary>
    ///     Gets a dictionary of file path → PlayHistoryItem for quick lookup.
    ///     Safe against duplicate file names (e.g. corrupted playhistory.dat) — first entry wins.
    /// </summary>
    public Dictionary<string, PlayHistoryItem> GetHistoryLookup()
    {
        lock (_historyLock)
        {
            try
            {
                return PlayHistoryList
                    .GroupBy(h => h.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error building play history lookup; returning empty dictionary");
                return new Dictionary<string, PlayHistoryItem>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}