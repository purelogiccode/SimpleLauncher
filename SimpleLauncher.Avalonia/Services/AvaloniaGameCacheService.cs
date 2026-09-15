using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Thread-safe in-memory cache of game file lists keyed by system name. Scanning a
///     system's folders is the most expensive part of a library load, so navigating
///     between systems reuses the cached file list instead of re-enumerating the disk.
///     Avalonia port of the WPF <c>GameCacheService</c> (per-system lists; no WPF types).
/// </summary>
public sealed class AvaloniaGameCacheService
{
    private readonly Dictionary<string, List<string>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>
    ///     Gets the number of systems currently cached.
    /// </summary>
    public int CachedSystemCount
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    ///     Returns a snapshot of the cached file list for the given system, or null when
    ///     the system is not cached yet.
    /// </summary>
    /// <param name="systemName">The system name (case-insensitive).</param>
    public List<string>? GetCachedFiles(string systemName)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(systemName, out var files))
            {
                Log.Debug("[AvaloniaGameCacheService] Cache hit for '{System}'. Count: {Count}", systemName,
                    files.Count);
                return [.. files];
            }

            Log.Debug("[AvaloniaGameCacheService] No cached files for '{System}'", systemName);
            return null;
        }
    }

    /// <summary>
    ///     Replaces the cached file list for the given system.
    /// </summary>
    /// <param name="systemName">The system name (case-insensitive).</param>
    /// <param name="files">The game file paths to cache.</param>
    public void SetCachedFiles(string systemName, List<string> files)
    {
        lock (_lock)
        {
            _cache[systemName] = [.. files];
            Log.Debug("[AvaloniaGameCacheService] SetAllGames for '{System}'. Count: {Count}", systemName,
                files.Count);
        }
    }

    /// <summary>
    ///     Determines whether the cache already contains a file list for the given system.
    /// </summary>
    /// <param name="systemName">The system name (case-insensitive).</param>
    public bool IsPopulated(string systemName)
    {
        lock (_lock)
        {
            return _cache.ContainsKey(systemName);
        }
    }

    /// <summary>
    ///     Returns the cached file list for the system, or scans it via the provided
    ///     enumerator and caches the result. The enumerator is only invoked when the
    ///     system is not cached yet.
    /// </summary>
    /// <param name="system">The system configuration.</param>
    /// <param name="enumerateFiles">The file enumeration function (invoked outside the cache lock — never re-enter the cache from it).</param>
    public List<string> GetCachedOrScan(SystemManagerConfig system,
        Func<SystemManagerConfig, IEnumerable<string>> enumerateFiles)
    {
        // AV-19: fast path under the lock, disk I/O outside of it. Holding the
        // lock across enumeration blocked every reader for the whole scan and
        // deadlocked when the enumerator re-entered the cache.
        lock (_lock)
        {
            if (_cache.TryGetValue(system.SystemName, out var cached))
            {
                Log.Debug("[AvaloniaGameCacheService] Reusing cached list for '{System}'. Count: {Count}",
                    system.SystemName, cached.Count);
                return [.. cached];
            }
        }

        var files = enumerateFiles(system).ToList();

        lock (_lock)
        {
            // A concurrent scan may have populated the entry while we enumerated —
            // prefer the winner instead of overwriting it.
            if (_cache.TryGetValue(system.SystemName, out var cached))
            {
                Log.Debug("[AvaloniaGameCacheService] Reusing cached list for '{System}'. Count: {Count}",
                    system.SystemName, cached.Count);
                return [.. cached];
            }

            _cache[system.SystemName] = [.. files];
            Log.Debug("[AvaloniaGameCacheService] Populated cache for '{System}'. Count: {Count}",
                system.SystemName, files.Count);
            return files;
        }
    }

    /// <summary>
    ///     Removes the cached file list for the given system (called when its files
    ///     change on disk, or when the system configuration changes).
    /// </summary>
    /// <param name="systemName">The system name (case-insensitive).</param>
    public void Invalidate(string systemName)
    {
        lock (_lock)
        {
            _cache.Remove(systemName);
        }
    }

    /// <summary>
    ///     Clears all cached file lists (called after a full library refresh).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }
}