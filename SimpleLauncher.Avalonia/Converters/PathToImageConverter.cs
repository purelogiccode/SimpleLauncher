using System.Collections.Concurrent;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Two-tier image cache converter: weak ConcurrentDictionary + LRU strong tier (1500 entries).
///     Ported from Emutastic pattern. Loads cover art images asynchronously.
/// </summary>
public class PathToImageConverter : IValueConverter
{
    // Prune the weak cache when it grows past this size (dead weak references accumulate
    // until GC runs; this bounds the weak-reference table without blocking the UI thread).
    private const int WeakCachePruneThreshold = 4096;

    private const int LruCapacity = 1500;

    // Path comparer: Windows/macOS file systems are (usually) case-insensitive, but Linux
    // is case-sensitive — OrdinalIgnoreCase there would collide "Game.PNG" with a
    // different file "game.png" and serve the wrong cover (AV-03).
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    // Weak cache: allows GC to reclaim unused images
    private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> WeakCache =
        new(PathComparer);

    // Strong LRU cache: keeps recent images alive
    private static readonly LinkedList<(string Path, Bitmap Img)> LruList = new();

    private static readonly Dictionary<string, LinkedListNode<(string Path, Bitmap Img)>> LruIndex =
        new(PathComparer);

    private static readonly Lock LruLock = new();

    // Default placeholder
    private static Bitmap? _placeholder;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return GetPlaceholder();

        // Check weak cache
        if (WeakCache.TryGetValue(path, out var weakRef) && weakRef.TryGetTarget(out var cached))
            return cached;

        // Check strong LRU cache
        lock (LruLock)
        {
            if (LruIndex.TryGetValue(path, out var node))
            {
                TouchLru(node);
                return node.Value.Img;
            }
        }

        // NOTE: IValueConverter is synchronous, so the first load of each image does
        // disk I/O + decode on the UI thread. Covers are small local files decoded at
        // 300px, and every later bind is served from the caches above (AV-03).
        var image = LoadImage(path);
        return image ?? GetPlaceholder();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    ///     Clears all caches, promptly releasing native memory of images nothing else
    ///     references. Bitmaps still shown by live views are left valid for the GC (AV-03).
    /// </summary>
    public static void ClearCache()
    {
        lock (LruLock)
        {
            foreach (var entry in LruList) DisposeIfUnreferenced(entry.Path, entry.Img);

            LruList.Clear();
            LruIndex.Clear();
        }

        WeakCache.Clear();
    }

    private static Bitmap? LoadImage(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            // Avalonia Bitmap is immutable and safe for cross-thread use once created.
            using var stream = File.OpenRead(path);
            var image = Bitmap.DecodeToWidth(stream, 300);

            // Add to caches
            WeakCache[path] = new WeakReference<Bitmap>(image);
            PruneWeakCacheIfNeeded();

            lock (LruLock)
            {
                if (LruIndex.TryGetValue(path, out var existing))
                {
                    LruList.Remove(existing);
                    LruIndex.Remove(path);
                    DisposeIfUnreferenced(path, existing.Value.Img);
                }
                else if (LruList.Count >= LruCapacity)
                {
                    var oldest = LruList.Last!;
                    LruIndex.Remove(oldest.Value.Path);
                    LruList.RemoveLast();
                    DisposeIfUnreferenced(oldest.Value.Path, oldest.Value.Img);
                }

                var node = LruList.AddFirst((path, image));
                LruIndex[path] = node;
            }

            return image;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to load image in PathToImageConverter");
            return null;
        }
    }

    private static void TouchLru(LinkedListNode<(string, Bitmap)> node)
    {
        LruList.Remove(node);
        LruList.AddFirst(node);
    }

    /// <summary>
    ///     Disposes an evicted bitmap only when nothing else can reach it. If the weak-cache
    ///     entry is already dead, no live view holds a reference, so prompt disposal releases
    ///     native memory now instead of waiting for finalization. Bitmaps with a live weak
    ///     target are left alone: disposing them would crash views still showing them with
    ///     ObjectDisposedException on the next render (the grid is not virtualized) — the GC
    ///     reclaims those once views drop them (AV-03). Callers must hold <see cref="LruLock" />.
    /// </summary>
    private static void DisposeIfUnreferenced(string path, Bitmap bitmap)
    {
        try
        {
            if (WeakCache.TryGetValue(path, out var weak) && !weak.TryGetTarget(out _))
            {
                WeakCache.TryRemove(path, out _);
                bitmap.Dispose();
            }
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing to do.
        }
    }

    /// <summary>
    ///     Removes dead entries (GC-collected images) from the weak cache once it grows
    ///     past the threshold, so the weak-reference table stays bounded.
    /// </summary>
    private static void PruneWeakCacheIfNeeded()
    {
        if (WeakCache.Count < WeakCachePruneThreshold) return;

        foreach (var entry in WeakCache)
        {
            if (!entry.Value.TryGetTarget(out _))
                WeakCache.TryRemove(entry.Key, out _);
        }
    }

    private static Bitmap? GetPlaceholder()
    {
        if (_placeholder is not null) return _placeholder;

        // Use the bundled default image when available
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "images", "default.png");
        try
        {
            if (File.Exists(defaultPath))
            {
                using var stream = File.OpenRead(defaultPath);
                _placeholder = Bitmap.DecodeToWidth(stream, 300);
            }
            else
            {
                // No default image available — 1x1 transparent placeholder (zeroed pixels = transparent)
                _placeholder = new WriteableBitmap(
                    new PixelSize(1, 1),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to create placeholder image");
        }

        return _placeholder;
    }
}