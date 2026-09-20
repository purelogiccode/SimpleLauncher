using System.Collections.ObjectModel;
using MessagePack;
using Microsoft.Data.Sqlite;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.UnifiedSettings;
using ILogger = Serilog.ILogger;

namespace SimpleLauncher.Avalonia.Services.Favorites;

/// <summary>
///     Manages the user's favorite games list.
///     The Avalonia app persists favorites in the unified SQLite database
///     (<c>settings.dat</c> in AppData); the legacy MessagePack <c>favorites.dat</c>
///     format is only read during the one-time migration (and as a fallback when no
///     database exists yet). Mirrors the WPF FavoritesManager save/load logic: favorites
///     are sorted by file name before writing, serialization uses a snapshot, and writes
///     retry with exponential backoff on transient IO errors, falling back to the
///     LocalAppData folder when a portable-mode write fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class FavoritesManager
{
    [IgnoreMember] private static readonly Lock ListLock = new();
    [IgnoreMember] private static readonly DataFileLocation FileLocation = new("favorites.dat");
    [IgnoreMember] private ILogger? _logger;

    [Key(0)] public ObservableCollection<Favorite> FavoriteList { get; set; } = [];

    [Key(1)] public int Version { get; set; } = 1;

    private static string DatFilePath => FileLocation.FilePath;
    private static string TempDatFilePath => FileLocation.TempFilePath;
    public static bool IsPortableMode => FileLocation.IsPortableMode;

    /// <summary>Loads favorites from the unified database (callers must have verified it is valid).</summary>
    private static FavoritesManager LoadFromDatabase(ILogger? logErrors)
    {
        var manager = new FavoritesManager { _logger = logErrors };
        foreach (var record in UnifiedSettingsDatabase.LoadFavorites())
        {
            if (string.IsNullOrWhiteSpace(record.FileName))
                continue;
            manager.FavoriteList.Add(new Favorite
            {
                FileName = record.FileName,
                SystemName = record.SystemName ?? ""
            });
        }

        logErrors?.Information("Loaded {Count} favorite(s) from the unified database",
            manager.FavoriteList.Count);

        return manager;
    }

    /// <summary>Saves a sorted snapshot of the list to the unified database.</summary>
    private void SaveToDatabase()
    {
        List<FavoriteRecord> snapshot;
        lock (ListLock)
        {
            snapshot = FavoriteList
                .Where(static fav => !string.IsNullOrWhiteSpace(fav.FileName))
                .OrderBy(static fav => fav.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(static fav => new FavoriteRecord(fav.FileName, fav.SystemName ?? ""))
                .ToList();
        }

        UnifiedSettingsDatabase.EnsureCreated();
        UnifiedSettingsDatabase.SaveFavorites(snapshot);
    }

    /// <summary>
    ///     Loads favorites from the unified database when it exists, from the legacy DAT
    ///     file when it does not (pre-migration), or creates a new database-backed instance.
    /// </summary>
    public static FavoritesManager LoadFavorites(ILogger? logErrors = null)
    {
        if (UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                return LoadFromDatabase(logErrors);
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, "Error loading favorites from the unified database; trying the legacy file");
            }
        }
        else if (!File.Exists(DatFilePath))
        {
            // Fresh start with neither a database nor a legacy file: create the
            // database instead of a legacy favorites.dat.
            try
            {
                UnifiedSettingsDatabase.EnsureCreated();
                var fresh = new FavoritesManager { _logger = logErrors };
                fresh.SaveToDatabase();
                return fresh;
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, "Error creating favorites in the unified database; using a legacy file");
            }
        }

        if (File.Exists(DatFilePath))
        {
            try
            {
                var bytes = File.ReadAllBytes(DatFilePath);
                var manager = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);
                manager._logger = logErrors;
                logErrors?.Information("Loaded {Count} favorite(s) from favorites.dat",
                    manager.FavoriteList.Count);
                return manager;
            }
            catch (Exception ex)
            {
                logErrors?.Error(ex, "Error loading favorites.dat");
            }
        }

        var newManager = new FavoritesManager { _logger = logErrors };
        // Write the initial file synchronously. This runs on the UI thread at startup —
        // awaiting the async save via GetAwaiter().GetResult() would deadlock (the async
        // continuation needs the UI thread that is blocked waiting).
        newManager.SaveFavoritesSync();
        return newManager;
    }

    /// <summary>
    ///     Synchronous initial save (startup path only). Writes to the unified database
    ///     when it exists; otherwise mirrors <see cref="SaveFavoritesAsync" />
    ///     with retry logic, but never awaits — safe to call on the UI thread.
    /// </summary>
    private void SaveFavoritesSync()
    {
        if (UnifiedSettingsDatabase.IsValidDatabase())
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
                // legacy favorites.dat once the unified database exists. Keep the
                // in-memory state instead.
                _logger?.Information(ex,
                    "Error saving favorites to the unified database; keeping in-memory state");
                return;
            }
            catch (Exception ex)
            {
                // Unexpected errors stay visible but must not resurrect the legacy file.
                _logger?.Error(ex, "Error saving favorites to the unified database");
                return;
            }
        }

        // Take a sorted snapshot for serialization without modifying the live collection.
        List<Favorite> sortedSnapshot;
        lock (ListLock)
        {
            sortedSnapshot = FavoriteList
                .OrderBy(static fav => fav.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        const int maxRetries = 3;
        var retryDelayMs = 500;
        Exception? lastException = null;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                // Serialize the sorted snapshot
                byte[] bytes;
                lock (ListLock)
                {
                    var snapshotManager = new FavoritesManager
                        { FavoriteList = new ObservableCollection<Favorite>(sortedSnapshot), Version = Version };
                    bytes = MessagePackSerializer.Serialize(snapshotManager);
                }

                // Write to a temporary file first to prevent corruption on crash
                File.WriteAllBytes(TempDatFilePath, bytes);

                // Atomically replace the main file with the temp file
                File.Move(TempDatFilePath, DatFilePath, true);
                return; // Success
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                lastException = ex;
                attempt++;

                // If in portable mode, try falling back to LocalAppData and reset retries
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
                        Log.Debug($"[FavoritesManager] FallbackToLocalAppData failed: {fallbackEx.Message}");
                    }
                }

                if (attempt < maxRetries)
                {
                    // Attempt to clean up temp file before retrying
                    try
                    {
                        if (File.Exists(TempDatFilePath)) File.Delete(TempDatFilePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        Log.Debug($"[FavoritesManager] Temp file cleanup failed: {cleanupEx.Message}");
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
        _logger?.Error(lastException, "Error saving favorites.dat");

        // Attempt to clean up temp file if it exists
        try
        {
            if (File.Exists(TempDatFilePath)) File.Delete(TempDatFilePath);
        }
        catch (Exception cleanupEx)
        {
            _logger?.Error(cleanupEx, "Error cleaning up temporary favorites file after failed save");
        }
    }

    /// <summary>
    ///     Saves favorites atomically with retry logic. Writes to the unified database
    ///     when it exists; otherwise falls back to the legacy DAT file. Once a valid
    ///     database exists the legacy file is never rewritten; failures keep in-memory state.
    /// </summary>
    public async Task SaveFavoritesAsync()
    {
        if (UnifiedSettingsDatabase.IsValidDatabase())
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
                // legacy favorites.dat once the unified database exists. Keep the
                // in-memory state instead.
                _logger?.Information(ex,
                    "Error saving favorites to the unified database; keeping in-memory state");
                return;
            }
            catch (Exception ex)
            {
                // Unexpected errors stay visible but must not resurrect the legacy file.
                _logger?.Error(ex, "Error saving favorites to the unified database");
                return;
            }
        }

        // Take a sorted snapshot for serialization without modifying the live collection.
        List<Favorite> sortedSnapshot;
        lock (ListLock)
        {
            sortedSnapshot = FavoriteList
                .OrderBy(static fav => fav.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        const int maxRetries = 3;
        var retryDelayMs = 500;
        Exception? lastException = null;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                // Serialize using the sorted snapshot
                byte[] bytes;
                lock (ListLock)
                {
                    var snapshotManager = new FavoritesManager
                        { FavoriteList = new ObservableCollection<Favorite>(sortedSnapshot), Version = Version };
                    bytes = MessagePackSerializer.Serialize(snapshotManager);
                }

                await File.WriteAllBytesAsync(TempDatFilePath, bytes);

                File.Move(TempDatFilePath, DatFilePath, true);
                return; // Success
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                lastException = ex;
                attempt++;

                // If in portable mode, try falling back to LocalAppData and reset retries
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
                        Log.Debug($"[FavoritesManager] FallbackToLocalAppData failed: {fallbackEx.Message}");
                    }
                }

                if (attempt < maxRetries)
                {
                    // Attempt to clean up temp file before retrying
                    try
                    {
                        if (File.Exists(TempDatFilePath)) File.Delete(TempDatFilePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        Log.Debug($"[FavoritesManager] Temp file cleanup failed: {cleanupEx.Message}");
                    }

                    await Task.Delay(retryDelayMs);
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
        _logger?.Error(lastException, "Error saving favorites.dat");

        // Attempt to clean up temp file if it exists
        try
        {
            if (File.Exists(TempDatFilePath)) File.Delete(TempDatFilePath);
        }
        catch (Exception cleanupEx)
        {
            _logger?.Error(cleanupEx, "Error cleaning up temporary favorites file after failed save");
        }
    }

    /// <summary>
    ///     Gets the bare file name of a stored favorite (legacy entries may hold
    ///     a full path; matching in the WPF app is always by bare file name).
    /// </summary>
    private static string ToBareName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "";

        return Path.GetFileName(fileName) ?? fileName;
    }

    /// <summary>
    ///     Checks whether a game is in the favorites list (WPF compares bare file names).
    /// </summary>
    public bool IsFavorite(string filePath)
    {
        var bareName = ToBareName(filePath);
        lock (ListLock)
        {
            return FavoriteList.Any(f =>
                string.Equals(ToBareName(f.FileName), bareName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Adds a game to favorites. Returns true if added, false if already present.
    ///     Stores the bare file name (WPF parity); legacy full-path entries still match.
    /// </summary>
    public async Task<bool> AddFavoriteAsync(string filePath, string systemName)
    {
        var bareName = ToBareName(filePath);
        lock (ListLock)
        {
            if (FavoriteList.Any(f =>
                    string.Equals(ToBareName(f.FileName), bareName, StringComparison.OrdinalIgnoreCase)))
            {
                _logger?.Debug("Favorite already present, skipping: {FileName}", bareName);
                return false;
            }

            FavoriteList.Add(new Favorite
            {
                FileName = bareName,
                SystemName = systemName
            });
        }

        await SaveFavoritesAsync();
        _logger?.Information("Favorite added: {FileName} ({System})", bareName, systemName);
        return true;
    }

    /// <summary>
    ///     Removes a game from favorites. Returns true if removed, false if not found.
    ///     Matches by bare file name so legacy full-path entries are found too.
    /// </summary>
    public async Task<bool> RemoveFavoriteAsync(string filePath)
    {
        var bareName = ToBareName(filePath);
        lock (ListLock)
        {
            var toRemove = FavoriteList.FirstOrDefault(f =>
                string.Equals(ToBareName(f.FileName), bareName, StringComparison.OrdinalIgnoreCase));
            if (toRemove is null)
            {
                _logger?.Debug("Favorite not found for removal: {FileName}", bareName);
                return false;
            }

            FavoriteList.Remove(toRemove);
        }

        await SaveFavoritesAsync();
        _logger?.Information("Favorite removed: {FileName}", bareName);
        return true;
    }

    /// <summary>
    ///     Toggles favorite status for a game. Returns the new state (true = favorited).
    /// </summary>
    public async Task<bool> ToggleAsync(string filePath, string systemName)
    {
        if (IsFavorite(filePath))
        {
            await RemoveFavoriteAsync(filePath);
            return false;
        }

        await AddFavoriteAsync(filePath, systemName);
        return true;
    }

    /// <summary>
    ///     Renames the system in all favorites (used when a system is renamed in Edit System).
    ///     Favorites store the system name as a plain string, so without this migration they
    ///     would keep the old name and point at a system that no longer exists.
    /// </summary>
    public async Task RenameSystemAsync(string oldSystemName, string newSystemName)
    {
        var changed = false;
        lock (ListLock)
        {
            foreach (var favorite in FavoriteList)
            {
                if (favorite.SystemName.Equals(oldSystemName, StringComparison.OrdinalIgnoreCase))
                {
                    favorite.SystemName = newSystemName;
                    changed = true;
                }
            }
        }

        if (changed) await SaveFavoritesAsync();
    }

    /// <summary>
    ///     Removes favorites whose system no longer exists in the current configuration
    ///     (e.g., the system was renamed without migration, or deleted).
    /// </summary>
    /// <param name="validSystemNames">The system names that currently exist in the configuration.</param>
    /// <returns>The number of removed favorites.</returns>
    public async Task<int> RemoveFavoritesForMissingSystemsAsync(IEnumerable<string> validSystemNames)
    {
        var validNames = validSystemNames.ToList();
        var toRemove = new List<Favorite>();

        lock (ListLock)
        {
            foreach (var favorite in FavoriteList)
            {
                if (!validNames.Any(name => name.Equals(favorite.SystemName, StringComparison.OrdinalIgnoreCase)))
                    toRemove.Add(favorite);
            }

            foreach (var favorite in toRemove) FavoriteList.Remove(favorite);
        }

        if (toRemove.Count > 0) await SaveFavoritesAsync();

        return toRemove.Count;
    }

    /// <summary>
    ///     Gets all stored favorite file names as bare names (legacy full-path entries
    ///     are normalized so the game grid can match against Path.GetFileName).
    /// </summary>
    public HashSet<string> GetFavoritePaths()
    {
        lock (ListLock)
        {
            return FavoriteList.Select(f => ToBareName(f.FileName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}