using Avalonia.Threading;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Services.PlaySound;

namespace SimpleLauncher.Avalonia.Services.LoadingOverlay;

/// <summary>
///     Thread-safe loading overlay service with a reference-counted loading state.
///     Multiple concurrent operations can request loading state; the overlay stays
///     visible until all operations complete.
///     Extracted from the inline ILoadingState implementation on MainViewModel.
///     Mirrors the WPF LoadingOverlayService.
/// </summary>
public class AvaloniaLoadingOverlayService
{
    private readonly Lock _loadingStateLock = new();
    private readonly PlaySoundEffects _playSoundEffects;
    private IAvaloniaLoadingOverlayHost? _host;
    private int _loadingOperationsCount;

    public AvaloniaLoadingOverlayService(PlaySoundEffects playSoundEffects)
    {
        _playSoundEffects = playSoundEffects;
    }

    /// <summary>Initializes the service with the specified UI host.</summary>
    public void Initialize(IAvaloniaLoadingOverlayHost host)
    {
        _host = host;
    }

    /// <summary>
    ///     Sets the loading state. When <paramref name="isLoading" /> is true, increments
    ///     the loading counter and shows the overlay. When false, decrements the counter
    ///     and hides the overlay only when all operations have completed.
    /// </summary>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        var host = _host;
        if (host == null) return;

        bool shouldShowOverlay;

        lock (_loadingStateLock)
        {
            if (isLoading)
            {
                _loadingOperationsCount++;
            }
            else
            {
                if (_loadingOperationsCount > 0) _loadingOperationsCount--;
            }

            shouldShowOverlay = _loadingOperationsCount > 0;
        }

        Dispatcher.UIThread.Post(() =>
        {
            host.SetIsLoading(shouldShowOverlay);

            if (isLoading && shouldShowOverlay && message != null)
                host.SetLoadingMessage(message);
            else if (!shouldShowOverlay) host.SetLoadingMessage("Loading\u2026");
        });
    }

    /// <summary>
    ///     Force-resets the loading counter to 0 and hides the overlay.
    ///     Use as an emergency escape when the loading state is stuck.
    /// </summary>
    public void EmergencyRelease()
    {
        var host = _host;
        if (host == null) return;

        _playSoundEffects.PlayNotificationSound();

        lock (_loadingStateLock)
        {
            _loadingOperationsCount = 0;
        }

        // WPF parity: cancel in-flight background work and recreate the token
        host.CancelAndRecreateToken();

        Dispatcher.UIThread.Post(() =>
        {
            host.SetIsLoading(false);
            host.SetLoadingMessage("Loading\u2026");
            host.SetMainContentGridEnabled(true);
        });

        _ = host.ResetUiAsync();
        Log.Debug("[Emergency] User forced overlay dismissal via Return button");
    }
}