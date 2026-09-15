using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CheckPaths;

namespace SimpleLauncher.Core.Services.GameFileWatcher;

/// <summary>
///     Monitors ROM system folders for file changes (create, delete, rename, change)
///     and raises an event when changes are detected. Uses debouncing to avoid
///     rapid re-scans during batch file operations.
/// </summary>
public sealed class GameFileWatcherService : IDisposable
{
    private readonly Lock _lock = new();
    private readonly ILogger _logger;
    private readonly Dictionary<FileSystemWatcher, WatcherTag> _watchers = new();
    private CancellationTokenSource? _debounceCts;
    private volatile bool _disposed;

    // Bumped every time the pending debounce is cancelled or replaced.
    // The delayed task only raises GameFilesChanged when its captured generation
    // is still current, so a debounce can never fire after StopWatching/Dispose (CORE-13).
    // Guarded by _lock.
    private int _debounceGeneration;

    /// <summary>
    ///     Initializes a new instance of <see cref="GameFileWatcherService" />.
    /// </summary>
    /// <param name="logger">Debug logging service.</param>
    public GameFileWatcherService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     The debounce delay before raising the GameFilesChanged event.
    ///     Prevents rapid re-scans during batch file operations (e.g., extracting archives).
    /// </summary>
    public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;

            _disposed = true;
        }

        StopWatching();
    }

    /// <summary>
    ///     Raised when a file change is detected in any monitored folder.
    ///     The string parameter is the system name that was being monitored.
    /// </summary>
    public event EventHandler<EventArgs<string>> GameFilesChanged = null!;

    /// <summary>
    ///     Starts monitoring the specified folders for file changes.
    ///     By default stops monitoring any previously monitored folders first.
    /// </summary>
    /// <param name="folders">The folder paths to monitor (can be relative or contain %BASEFOLDER%).</param>
    /// <param name="systemName">The system name associated with these folders.</param>
    /// <param name="fileExtensions">
    ///     Optional list of file extensions to filter (e.g., ["zip", "tap"]). If null, all files are
    ///     monitored.
    /// </param>
    /// <param name="reset">
    ///     When true (default), any previously monitored folders are stopped first. When false, the new
    ///     folders are added without clearing existing watchers (used to watch multiple systems at once).
    /// </param>
    public void StartWatching(IEnumerable<string> folders, string systemName,
        IEnumerable<string>? fileExtensions = null, bool reset = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (reset) StopWatching();

        var extensionFilter = fileExtensions?.Select(static e => e.TrimStart('.').ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
        var resolvedFolders = folders
            .Select(static f => PathHelper.TryGetExistingDirectory(f))
            .Where(static f => f != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resolvedFolders.Count == 0)
        {
            _logger.Debug($"[GameFileWatcherService] No valid folders to watch for system '{systemName}'.");
            return;
        }

        var tag = new WatcherTag(systemName, extensionFilter);

        lock (_lock)
        {
            foreach (var folder in resolvedFolders)
            {
                try
                {
                    if (folder != null)
                    {
                        var watcher = new FileSystemWatcher(folder)
                        {
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                            IncludeSubdirectories = true,
                            EnableRaisingEvents = false
                        };

                        watcher.Created += OnFileChanged;
                        watcher.Deleted += OnFileChanged;
                        watcher.Renamed += OnFileChanged;
                        watcher.Error += OnWatcherError;

                        _watchers[watcher] = tag;
                        watcher.EnableRaisingEvents = true;
                    }

                    _logger.Debug($"[GameFileWatcherService] Watching '{folder}' for system '{systemName}'.");
                }
                catch (Exception ex)
                {
                    _logger.Debug($"[GameFileWatcherService] Failed to watch '{folder}': {ex.Message}");
                }
            }
        }

        lock (_lock)
        {
            _logger.Debug(
                $"[GameFileWatcherService] Started watching {_watchers.Count} folder(s) for system '{systemName}'.");
        }
    }

    /// <summary>
    ///     Stops monitoring all currently monitored folders.
    /// </summary>
    public void StopWatching()
    {
        lock (_lock)
        {
            foreach (var (watcher, _) in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= OnFileChanged;
                    watcher.Deleted -= OnFileChanged;
                    watcher.Renamed -= OnFileChanged;
                    watcher.Error -= OnWatcherError;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Debug($"[GameFileWatcherService] Error disposing watcher: {ex.Message}");
                }
            }

            _watchers.Clear();

            // Cancel while still holding _lock so a concurrent OnFileChanged cannot
            // publish a new debounce between the clear and the cancel (CORE-13).
            CancelPendingDebounceLocked();
        }

        _logger.Debug("[GameFileWatcherService] Stopped all watchers.");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;

        if (sender is not FileSystemWatcher watcher) return;

        // Look up the tag for this watcher
        WatcherTag? tag;
        lock (_lock)
        {
            if (!_watchers.TryGetValue(watcher, out tag)) return;
        }

        // Check extension filter if applicable
        if (tag.Extensions is { Count: > 0 })
        {
            var ext = Path.GetExtension(e.Name)?.TrimStart('.').ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext) &&
                !tag.Extensions.Contains(ext))
            {
                return; // Ignore files with non-matching extensions
            }
        }

        _logger.Debug(
            $"[GameFileWatcherService] File change detected: {e.ChangeType} - {e.FullPath} (System: {tag.SystemName})");

        DebounceAndRaiseEvent(tag.SystemName);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // GetException() is null for buffer-overflow notifications — never dereference it (CORE-12).
        var message = e.GetException()?.Message ?? "unknown error (no exception details)";
        _logger.Debug($"[GameFileWatcherService] Watcher error: {message}");
    }

    private void DebounceAndRaiseEvent(string systemName)
    {
        CancellationToken token;
        int generation;
        TimeSpan delay;

        lock (_lock)
        {
            if (_disposed) return;

            CancelPendingDebounceLocked();

            var cts = new CancellationTokenSource();
            _debounceCts = cts;
            token = cts.Token;
            generation = ++_debounceGeneration;
            delay = DebounceDelay;
        }

        // Run outside _lock: the delay outlives the file event, and invoking
        // subscriber handlers under the lock would risk deadlocks.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token);

                lock (_lock)
                {
                    // Suppress stale debounces: cancelled, superseded by a newer change,
                    // or orphaned by StopWatching/Dispose (CORE-13).
                    if (_disposed || token.IsCancellationRequested || generation != _debounceGeneration)
                        return;
                }

                _logger.Debug(
                    $"[GameFileWatcherService] Debounce complete. Raising GameFilesChanged for system '{systemName}'.");
                GameFilesChanged?.Invoke(this, new EventArgs<string>(systemName));
            }
            catch (TaskCanceledException)
            {
                // Expected when debounce is reset by another file change
            }
            catch (ObjectDisposedException)
            {
                // Expected when the CTS is disposed by StopWatching/Dispose mid-delay.
            }
            catch (Exception ex)
            {
                _logger.Debug($"[GameFileWatcherService] Debounce task failed: {ex.Message}");
            }
        }, CancellationToken.None);
    }

    private void CancelPendingDebounce()
    {
        lock (_lock)
        {
            CancelPendingDebounceLocked();
        }
    }

    /// <summary>
    ///     Cancels and disposes the pending debounce. Caller must hold <see cref="_lock" />
    ///     (System.Threading.Lock is non-reentrant, so this never locks itself).
    /// </summary>
    private void CancelPendingDebounceLocked()
    {
        // Invalidate any in-flight debounce task before cancelling, so it can never
        // raise GameFilesChanged after this point even if the cancel lands late (CORE-13).
        _debounceGeneration++;

        var oldCts = _debounceCts;
        _debounceCts = null;

        try
        {
            oldCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing to cancel.
        }

        oldCts?.Dispose();
    }

    /// <summary>
    ///     Stores the system name and optional extension filter for a FileSystemWatcher.
    /// </summary>
    private sealed record WatcherTag(string SystemName, HashSet<string>? Extensions);
}