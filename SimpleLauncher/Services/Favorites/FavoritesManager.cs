using System.Collections.ObjectModel;
using System.Windows;
using MessagePack;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Services.Favorites;

/// <summary>
///     Manages the user's favorite games list with MessagePack serialization,
///     supporting load, save with atomic file replacement, and retry logic.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class FavoritesManager
{
    [IgnoreMember] private static readonly Lock ListLock = new();
    [IgnoreMember] private static readonly DataFileLocation FileLocation = new("favorites.dat");
    [IgnoreMember] private ILogger? _logger;

    /// <summary>
    ///     Gets or sets the collection of favorite game entries.
    /// </summary>
    [Key(0)]
    public ObservableCollection<Favorite> FavoriteList { get; set; } = [];

    /// <summary>
    ///     Gets or sets the data format version for forward-compatible deserialization.
    /// </summary>
    [Key(1)]
    public int Version { get; set; } = 1;

    private static string DatFilePath => FileLocation.FilePath;
    private static string TempDatFilePath => FileLocation.TempFilePath;

    /// <summary>
    ///     Gets a value indicating whether the application is running in portable mode.
    /// </summary>
    public static bool IsPortableMode => FileLocation.IsPortableMode;

    /// <summary>
    ///     Loads favorites from the DAT file. If the DAT file doesn't exist, will create a new instance.
    /// </summary>
    public static FavoritesManager LoadFavorites(ILogger? logErrors = null)
    {
        if (File.Exists(DatFilePath))
        {
            try
            {
                var bytes = File.ReadAllBytes(DatFilePath);
                var manager = MessagePackSerializer.Deserialize<FavoritesManager>(bytes);
                manager._logger = logErrors;
                return manager;
            }
            catch (Exception ex)
            {
                // Notify developer
                const string contextMessage = "Error loading favorites.dat";
                logErrors?.Error(ex, contextMessage);
            }
        }

        // If no files exist, create a new instance
        var defaultManager = new FavoritesManager { _logger = logErrors };
        _ = defaultManager.SaveFavoritesAsync().ContinueWith(static (task, state) =>
        {
            if (task.IsFaulted) (state as ILogger)?.Error(task.Exception, "Error saving default favorites");
        }, logErrors, TaskContinuationOptions.OnlyOnFaulted);
        return defaultManager; // Return default instance if error occurs
    }

    /// <summary>
    ///     Renames the system in all favorites (used when a system is renamed in Edit System).
    ///     Favorites store the system name as a plain string, so without this migration they would
    ///     keep the old name and fail to launch with a missing system manager.
    /// </summary>
    /// <param name="oldSystemName">The previous system name.</param>
    /// <param name="newSystemName">The new system name.</param>
    /// <returns>A task representing the save operation (no-op when nothing changed).</returns>
    public Task RenameSystemAsync(string oldSystemName, string newSystemName)
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

        return changed ? SaveFavoritesAsync() : Task.CompletedTask;
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
    ///     Saves the provided favorites to the DAT file.
    ///     The favorites are ordered by FileName before saving.
    /// </summary>
    public Task SaveFavoritesAsync()
    {
        // Notify user outside of any lock to prevent potential deadlock
        Application.Current.Dispatcher.Invoke(static () =>
            (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("SavingFavorites") ?? "Saving favorites..."));

        // Take a sorted snapshot for serialization without modifying the live collection.
        // This avoids the UI seeing an empty list during Clear()+Add().
        List<Favorite> sortedSnapshot;
        lock (ListLock)
        {
            sortedSnapshot = FavoriteList
                .OrderBy(static fav => fav.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Serialize and write on a background thread so Thread.Sleep in the
        // retry loop does not block the UI thread.
        return Task.Run(() =>
        {
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

                    // Write to temporary file first to prevent corruption on crash
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
                    if (FileLocation.IsPortableMode && attempt >= maxRetries)
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
        });
    }
}