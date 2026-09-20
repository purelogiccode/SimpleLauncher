using System.Collections.Concurrent;
using Avalonia.Media.Imaging;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Loads remote (HTTP) images as <see cref="Bitmap" /> with a bounded weak cache.
///     Shared by the RetroAchievements UI, the image viewer, and the RemoteImage control.
/// </summary>
public static class RemoteImageLoader
{
    // Static HttpClient is intentional (socket reuse for the app lifetime); bounded by Timeout (AV-04).
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Weak values: entries die with their last view, so eviction/pruning never has to
    // dispose a bitmap a live view might still be showing (AV-04). The prune bound keeps
    // the dead-reference table itself from growing without limit.
    private const int WeakCachePruneThreshold = 1024;

    private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Loads an image from a URL (with cache). Returns null on failure or cancellation.
    /// </summary>
    public static async Task<Bitmap?> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (Cache.TryGetValue(url, out var weak) && weak.TryGetTarget(out var cached))
            return cached;

        try
        {
            var bytes = await Http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            await using var ms = new MemoryStream(bytes);
            var bitmap = Bitmap.DecodeToWidth(ms, 400);

            Cache[url] = new WeakReference<Bitmap>(bitmap);
            PruneWeakCacheIfNeeded();

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Gets a cached image, if present and still alive.
    /// </summary>
    public static Bitmap? GetCached(string? url)
    {
        return url is not null &&
               Cache.TryGetValue(url, out var weak) &&
               weak.TryGetTarget(out var cached)
            ? cached
            : null;
    }

    private static void PruneWeakCacheIfNeeded()
    {
        if (Cache.Count < WeakCachePruneThreshold) return;

        foreach (var entry in Cache)
        {
            if (!entry.Value.TryGetTarget(out _))
                Cache.TryRemove(entry.Key, out _);
        }
    }
}