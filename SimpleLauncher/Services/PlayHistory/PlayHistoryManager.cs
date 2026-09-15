using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using MessagePack;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Services.SystemManager;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.PlayHistory;

/// <summary>
///     Manages play history tracking, persistence, and date format migration.
///     The app persists history in the unified SQLite database
///     (<c>settings.dat</c> in AppData); the legacy MessagePack <c>playhistory.dat</c>
///     format is only read during the one-time migration (and as a fallback when no
///     database exists yet).
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class PlayHistoryManager
{
    // Constants for date and time formats
    private const string IsoDateFormat = "yyyy-MM-dd";
    private const string IsoTimeFormat = "HH:mm:ss";
    [IgnoreMember] private static readonly DataFileLocation FileLocation = new("playhistory.dat");
    [IgnoreMember] private readonly Lock _historyLock = new();
    [IgnoreMember] private ILogger _logger = null!;

    /// <summary>
    ///     Gets or sets the collection of play history entries.
    /// </summary>
    [Key(0)]
    public ObservableCollection<PlayHistoryItem> PlayHistoryList { get; set; } = [];

    /// <summary>
    ///     Gets or sets the data format version for migration purposes.
    /// </summary>
    [Key(1)]
    public int Version { get; set; } = 1;

    private static string FilePath => FileLocation.FilePath;
    private static string TempFilePath => FileLocation.TempFilePath;

    /// <summary>
    ///     Gets whether the application is running in portable mode.
    /// </summary>
    public static bool IsPortableMode => FileLocation.IsPortableMode;

    /// <summary>Loads play history from the unified database (callers must have verified it is valid).</summary>
    private static PlayHistoryManager LoadFromDatabase(ILogger? logErrors)
    {
        var manager = new PlayHistoryManager { _logger = logErrors! };
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

        // Migrate old date formats to the new ISO format if needed (legacy parity).
        manager.MigrateOldDateFormats();

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
    internal static PlayHistoryManager LoadPlayHistory(ILogger? logErrors = null)
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
                var fresh = new PlayHistoryManager { _logger = logErrors! };
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
            var defaultManager = new PlayHistoryManager { _logger = logErrors! };
            _ = defaultManager.SavePlayHistoryAsync();
            return defaultManager;
        }

        try
        {
            var bytes = File.ReadAllBytes(FilePath);
            var manager = MessagePackSerializer.Deserialize<PlayHistoryManager>(bytes);
            manager._logger = logErrors!;

            // Migrate old date formats to new ISO format if needed
            manager.MigrateOldDateFormats();

            return manager;
        }
        catch (Exception ex)
        {
            // Notify developer
            logErrors?.Error(ex, "Error loading play history");

            return new PlayHistoryManager { _logger = logErrors! }; // Return default instance if error occurs
        }
    }

    /// <summary>
    ///     Migrates any records with old date formats to the new culture-invariant ISO format.
    /// </summary>
    private void MigrateOldDateFormats()
    {
        var needsSaving = false;

        foreach (var item in PlayHistoryList)
        {
            // Check if date is already in ISO format
            var isIsoDate = IsIsoDateFormat(item.LastPlayDate);
            var isIsoTime = IsIsoTimeFormat(item.LastPlayTime);

            if (isIsoDate && isIsoTime) continue;

            // Convert the old format to ISO format
            if (!TryParseAndConvertDate(item.LastPlayDate, item.LastPlayTime,
                    out var newDate, out var newTime))
            {
                continue;
            }

            item.LastPlayDate = newDate;
            item.LastPlayTime = newTime;
            needsSaving = true;
        }

        if (needsSaving) SavePlayHistoryAsync();
    }

    /// <summary>
    ///     Checks if a date string is in ISO format (yyyy-MM-dd).
    /// </summary>
    private static bool IsIsoDateFormat(string dateStr)
    {
        return DateTime.TryParseExact(dateStr, IsoDateFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    /// <summary>
    ///     Checks if a time string is in ISO time format (HH:mm:ss).
    /// </summary>
    private static bool IsIsoTimeFormat(string timeStr)
    {
        return DateTime.TryParseExact(timeStr, IsoTimeFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    /// <summary>
    ///     Attempts to parse a date/time from any format and convert to ISO format.
    /// </summary>
    private bool TryParseAndConvertDate(string dateStr, string timeStr,
        out string newDateStr, out string newTimeStr)
    {
        // Initialize out parameters
        newDateStr = dateStr;
        newTimeStr = timeStr;

        try
        {
            // Try ISO format first (the target format we want to ensure is handled correctly)
            if (DateTime.TryParseExact($"{dateStr} {timeStr}", $"{IsoDateFormat} {IsoTimeFormat}",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                newDateStr = dateTime.ToString(IsoDateFormat, CultureInfo.InvariantCulture);
                newTimeStr = dateTime.ToString(IsoTimeFormat, CultureInfo.InvariantCulture);
                return true;
            }

            // Try explicit unambiguous formats using InvariantCulture only.
            // We avoid current culture parsing to prevent incorrect interpretation
            // when users switch OS region settings (e.g., US MM/dd/yyyy vs UK dd/MM/yyyy).
            // Note: For ambiguous dates like 01/02/2024, MM/dd/yyyy (US) will be attempted first.
            string[] dateFormats =
            [
                "yyyy/MM/dd", "yyyy.MM.dd", "dd.MM.yyyy",
                "MM/dd/yyyy", "dd/MM/yyyy", "d", "D"
            ];
            foreach (var df in dateFormats)
            {
                if (DateTime.TryParseExact($"{dateStr} {timeStr}",
                        $"{df} {IsoTimeFormat}", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out dateTime))
                {
                    newDateStr = dateTime.ToString(IsoDateFormat, CultureInfo.InvariantCulture);
                    newTimeStr = dateTime.ToString(IsoTimeFormat, CultureInfo.InvariantCulture);
                    return true;
                }
            }

            // Fallback: Try with InvariantCulture (assumes US format for ambiguous dates like 01/02/2024 -> Jan 2)
            if (DateTime.TryParse($"{dateStr} {timeStr}",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            {
                newDateStr = dateTime.ToString(IsoDateFormat, CultureInfo.InvariantCulture);
                newTimeStr = dateTime.ToString(IsoTimeFormat, CultureInfo.InvariantCulture);
                return true;
            }

            // If we can at least parse the date part
            foreach (var df in dateFormats)
            {
                if (!DateTime.TryParseExact(dateStr, df, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out dateTime))
                {
                    continue;
                }

                newDateStr = dateTime.ToString(IsoDateFormat, CultureInfo.InvariantCulture);

                // Try to parse time part separately
                if (TimeSpan.TryParse(timeStr, out var timeSpan))
                    newTimeStr = timeSpan.ToString(IsoTimeFormat, CultureInfo.InvariantCulture);
                else
                    newTimeStr = "00:00:00"; // Default if time can't be parsed

                return true;
            }

            // If everything fails, create a timestamp from current time
            // but only as a last resort since this loses the original timestamp
            dateTime = DateTime.Now;
            newDateStr = dateTime.ToString(IsoDateFormat, CultureInfo.InvariantCulture);
            newTimeStr = dateTime.ToString(IsoTimeFormat, CultureInfo.InvariantCulture);

            // Notify developer
            const string contextMessage = "Failed to parse date/time, using current time as fallback";
            _logger?.Warning(contextMessage);

            return true;
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error in date format migration";
            _logger?.Error(ex, contextMessage);

            return false;
        }
    }

    /// <summary>
    ///     Saves the play history: to the unified database when it exists, otherwise to
    ///     the legacy MessagePack file with retry logic.
    /// </summary>
    internal Task SavePlayHistoryAsync()
    {
        if (UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                SaveToDatabase();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error saving play history to the unified database; trying the legacy file");
            }
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
                    // Notify user
                    Application.Current?.Dispatcher.Invoke(static () =>
                        (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
                            (string)Application.Current.TryFindResource("SavingPlayHistory") ??
                            "Saving play history..."));

                    byte[] bytes;
                    lock (_historyLock)
                    {
                        bytes = MessagePackSerializer.Serialize(this);
                    }

                    // Write to a temporary file first to prevent data loss on crash
                    File.WriteAllBytes(TempFilePath, bytes);

                    // Atomically replace the main file with the temp file
                    File.Move(TempFilePath, FilePath, true);
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
                            Log.Debug($"[PlayHistoryManager] FallbackToLocalAppData failed: {fallbackEx.Message}");
                        }
                    }

                    if (attempt < maxRetries)
                    {
                        // Attempt to clean up temp file before retrying
                        try
                        {
                            if (File.Exists(TempFilePath)) File.Delete(TempFilePath);
                        }
                        catch (Exception cleanupEx)
                        {
                            Log.Debug($"[PlayHistoryManager] Temp file cleanup failed: {cleanupEx.Message}");
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
            _logger?.Error(lastException, "Error saving playhistory.dat");

            // Attempt to clean up temp file if it exists
            try
            {
                if (File.Exists(TempFilePath)) File.Delete(TempFilePath);
            }
            catch (Exception cleanupEx)
            {
                _logger?.Error(cleanupEx, "Error cleaning up temporary play history file after failed save");
            }
        });
    }

    /// <summary>
    ///     Renames the system in all play history entries (used when a system is renamed in Edit System).
    /// </summary>
    /// <param name="oldSystemName">The previous system name.</param>
    /// <param name="newSystemName">The new system name.</param>
    /// <returns>A task representing the save operation (no-op when nothing changed).</returns>
    internal Task RenameSystemAsync(string oldSystemName, string newSystemName)
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

        return changed ? SavePlayHistoryAsync() : Task.CompletedTask;
    }

    /// <summary>
    ///     Adds or updates a play history item based on the game info and play time.
    /// </summary>
    internal void AddOrUpdatePlayHistoryItem(string fullPath, string systemName, TimeSpan playTime)
    {
        try
        {
            // Skip if playtime is less than 5 seconds (likely just a quick launch/close)
            if (playTime.TotalSeconds < 5)
                return;

            // Notify user
            Application.Current.Dispatcher.Invoke(static () =>
                (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
                    (string)Application.Current.TryFindResource("UpdatingPlayHistory") ?? "Updating play history..."));

            // Get the current date and time in a culture-invariant format
            // This ensures it can be parsed regardless of the UI language
            var now = DateTime.Now;
            var dateStr = now.ToString(IsoDateFormat, CultureInfo.InvariantCulture);
            var timeStr = now.ToString(IsoTimeFormat, CultureInfo.InvariantCulture);

            PlayHistoryItem? itemToAdd = null;

            lock (_historyLock)
            {
                // Check if the game already exists in play history
                var existingItem = PlayHistoryList.FirstOrDefault(item =>
                    item.FileName.Equals(fullPath, StringComparison.OrdinalIgnoreCase) &&
                    item.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));

                if (existingItem != null)
                {
                    // Update existing record
                    existingItem.TotalPlayTime += (long)playTime.TotalSeconds;
                    existingItem.TimesPlayed++;
                    existingItem.LastPlayDate = dateStr;
                    existingItem.LastPlayTime = timeStr;
                }
                else
                {
                    // Create new record
                    itemToAdd = new PlayHistoryItem
                    {
                        FileName = fullPath,
                        SystemName = systemName,
                        TotalPlayTime = (long)playTime.TotalSeconds,
                        TimesPlayed = 1,
                        LastPlayDate = dateStr,
                        LastPlayTime = timeStr
                    };
                }
            }

            // Add to the ObservableCollection outside the lock to prevent deadlock
            if (itemToAdd != null) Application.Current.Dispatcher.Invoke(() => PlayHistoryList.Add(itemToAdd));

            // Save on a background thread (SavePlayHistoryAsync already uses Task.Run internally)
            _ = SavePlayHistoryAsync();
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error in AddOrUpdatePlayHistoryItem method.";
            _logger?.Error(ex, contextMessage);
        }
    }

    /// <summary>
    ///     Migrates old records that only contain filenames to full absolute paths.
    /// </summary>
    internal void MigrateFilenamesToFullPaths(List<SystemManagerService> systemManagers)
    {
        var needsSave = false;
        lock (_historyLock)
        {
            foreach (var item in PlayHistoryList)
            {
                // If the path is not rooted, it's an old "filename only" record
                if (!Path.IsPathRooted(item.FileName))
                {
                    var system = systemManagers.FirstOrDefault(s =>
                        s.SystemName.Equals(item.SystemName, StringComparison.OrdinalIgnoreCase));
                    if (system != null)
                    {
                        var resolvedPath = PathHelper.FindFileInSystemFolders(system.SystemFolders, item.FileName);
                        if (!string.IsNullOrEmpty(resolvedPath))
                        {
                            item.FileName = resolvedPath;
                            needsSave = true;
                        }
                    }
                }
            }
        }

        if (needsSave) SavePlayHistoryAsync();
    }
}