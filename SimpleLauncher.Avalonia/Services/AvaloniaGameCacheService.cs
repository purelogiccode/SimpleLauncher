using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Thread-safe in-memory cache of game file lists keyed by system name. Scanning a
///     system's folders is the most expensive part of a library load, so navigating
///     between systems reuses the cached file list instead of re-enumerating the disk.
///     Avalonia port of the WPF <c>GameCacheService</c> (per-system lists; no WPF types).
///     Entries are validated against the system's folder/format configuration so a list
///     cached before an edit (same system name, different folder) is never served.
/// </summary>
public sealed class AvaloniaGameCacheService
{
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    // Incremented whenever the cache is invalidated/cleared/replaced. A scan that started
    // before the change must not repopulate the cache with its stale list afterwards.
    private long _generation;

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
    ///     the system is not cached yet. Does not validate the configuration signature —
    ///     callers that have the configuration should use <see cref="GetCachedOrScan" />.
    /// </summary>
    /// <param name="systemName">The system name (case-insensitive).</param>
    public List<string>? GetCachedFiles(string systemName)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(systemName, out var entry))
            {
                Log.Debug("[AvaloniaGameCacheService] Cache hit for '{System}'. Count: {Count}", systemName,
                    entry.Files.Count);
                return [.. entry.Files];
            }

            Log.Debug("[AvaloniaGameCacheService] No cached files for '{System}'", systemName);
            return null;
        }
    }

    /// <summary>
    ///     Replaces the cached file list for the given system. The optional signature
    ///     should be the value produced by the system configuration; when omitted the
    ///     entry is considered unvalidated and is never reused by
    ///     <see cref="GetCachedOrScan" />.
    /// </summary>
    /// <param name="systemName">The system name (case-insensitive).</param>
    /// <param name="files">The game file paths to cache.</param>
    /// <param name="signature">The configuration signature the file list belongs to.</param>
    public void SetCachedFiles(string systemName, List<string> files, string? signature = null)
    {
        lock (_lock)
        {
            _cache[systemName] = new CacheEntry(signature, [.. files]);
            _generation++;
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
    ///     system is not cached yet, or when its configured folders/formats changed
    ///     since the cached list was produced (the stale list is rescanned and replaced).
    /// </summary>
    /// <param name="system">The system configuration.</param>
    /// <param name="enumerateFiles">The file enumeration function (invoked outside the cache lock — never re-enter the cache from it).</param>
    public List<string> GetCachedOrScan(SystemManagerConfig system,
        Func<SystemManagerConfig, IEnumerable<string>> enumerateFiles)
    {
        var signature = BuildSignature(system);

        // AV-19: fast path under the lock, disk I/O outside of it. Holding the
        // lock across enumeration blocked every reader for the whole scan and
        // deadlocked when the enumerator re-entered the cache.
        long generationAtStart;
        lock (_lock)
        {
            if (_cache.TryGetValue(system.SystemName, out var cached) &&
                string.Equals(cached.Signature, signature, StringComparison.Ordinal))
            {
                Log.Debug("[AvaloniaGameCacheService] Reusing cached list for '{System}'. Count: {Count}",
                    system.SystemName, cached.Files.Count);
                return [.. cached.Files];
            }

            // A different signature means the system was edited (folder/formats):
            // fall through and rescan so the stale list is replaced by the current one.
            generationAtStart = _generation;
        }

        var files = enumerateFiles(system).ToList();

        lock (_lock)
        {
            // A concurrent scan may have populated the entry while we enumerated —
            // prefer the winner instead of overwriting it (only when it matches the
            // configuration we were asked for; a scan for a stale configuration must
            // not win over the current one).
            if (_cache.TryGetValue(system.SystemName, out var cached) &&
                string.Equals(cached.Signature, signature, StringComparison.Ordinal))
            {
                Log.Debug("[AvaloniaGameCacheService] Reusing cached list for '{System}'. Count: {Count}",
                    system.SystemName, cached.Files.Count);
                return [.. cached.Files];
            }

            // The cache was invalidated (system change / refresh) while we were scanning:
            // do not repopulate it with a list that predates the change — the next call
            // rescans instead of serving a stale list until the next invalidation.
            if (_generation != generationAtStart)
            {
                Log.Debug(
                    "[AvaloniaGameCacheService] Cache changed while scanning '{System}'; not caching the stale list",
                    system.SystemName);
                return files;
            }

            _cache[system.SystemName] = new CacheEntry(signature, [.. files]);
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
            _generation++;
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
            _generation++;
        }
    }

    /// <summary>
    ///     Builds the configuration fingerprint a cached file list belongs to. Anything
    ///     that changes which files are enumerated (folders, formats, recursion) is part
    ///     of the signature; the image folder and launch settings do not affect the list.
    /// </summary>
    private static string BuildSignature(SystemManagerConfig system)
    {
        var folders = string.Join('\u001E', system.SystemFolders ?? []);
        var formats = string.Join('\u001E', system.FileFormatsToSearch ?? []);
        // Same recursion rule as AvaloniaGameFileLoadingOrchestrator.EnumerateSystemFiles:
        // recursion stays on while GroupByFolder is enabled, even when disabled in settings.
        var recursion = system is { DisableRecursiveSearch: true, GroupByFolder: false } ? '0' : '1';
        return $"{folders}\u001F{formats}\u001F{recursion}";
    }

    /// <summary>
    ///     A cached file list together with the configuration signature it was produced
    ///     from (null for entries populated without a configuration).
    /// </summary>
    private sealed record CacheEntry(string? Signature, List<string> Files);
}