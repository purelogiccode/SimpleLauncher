using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using SimpleLauncher.Avalonia.Services;

namespace SimpleLauncher.Avalonia.Controls;

/// <summary>
///     An <see cref="Image" /> that loads its bitmap asynchronously from a URL.
///     Binds a string URL (e.g. "https://retroachievements.org/...") to <see cref="Url" />.
///     Used in data templates (DataGrid cells, list items) where a converter cannot refresh.
/// </summary>
public class RemoteImage : Image
{
    /// <summary>
    ///     Defines the <see cref="Url" /> property.
    /// </summary>
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<RemoteImage, string?>(nameof(Url));

    private CancellationTokenSource? _loadCts;

    static RemoteImage()
    {
        UrlProperty.Changed.AddClassHandler<RemoteImage>(static (image, e) => image.OnUrlChanged(e));
    }

    public RemoteImage()
    {
        // Abort wasted downloads for recycled/scrolled-away items (AV-05).
        DetachedFromVisualTree += (_, _) => CancelPendingLoad();
    }

    /// <summary>
    ///     Gets or sets the image URL to load.
    /// </summary>
    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    private void OnUrlChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var url = e.GetNewValue<string?>();
        CancelPendingLoad();

        // NOTE: the previous Source is intentionally NOT disposed here: it may be a
        // loader-cached bitmap shared with other views, and disposing it would poison
        // the cache and crash those views. Unreferenced bitmaps are reclaimed via the
        // weak cache/finalization (AV-05).
        Source = RemoteImageLoader.GetCached(url);

        if (Source is not null || string.IsNullOrWhiteSpace(url))
            return;

        // Capture the token struct now: the CTS may be disposed by a later URL change
        // while this load is still in flight (AV-05).
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        _ = LoadAsync(url, cts.Token);
    }

    private async Task LoadAsync(string url, CancellationToken token)
    {
        try
        {
            var bitmap = await RemoteImageLoader.LoadAsync(url, token).ConfigureAwait(false);
            if (bitmap is null && !token.IsCancellationRequested)
                Log.Debug("Remote image load failed for {Url}", url);

            if (bitmap is null || token.IsCancellationRequested) return;

            // Only apply if the URL is still the current one (fast scrolling / reuse).
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested &&
                    string.Equals(Url, url, StringComparison.OrdinalIgnoreCase)) Source = bitmap;
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a URL change or detach; expected.
        }
        catch (InvalidOperationException)
        {
            // Dispatcher shut down mid-load; nothing left to update.
        }
    }

    private void CancelPendingLoad()
    {
        var cts = Interlocked.Exchange(ref _loadCts, null);
        if (cts is null) return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts.Dispose();
    }
}
