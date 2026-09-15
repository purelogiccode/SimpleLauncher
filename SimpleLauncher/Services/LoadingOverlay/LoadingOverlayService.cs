using System.Windows;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Interfaces;

namespace SimpleLauncher.Services.LoadingOverlay;

/// <summary>
///     Manages the loading overlay UI state, coordinating visibility and content updates across concurrent loading
///     operations.
/// </summary>
public class LoadingOverlayService
{
    private readonly Lock _loadingStateLock = new();
    private readonly ILogger _logger;
    private readonly PlaySoundEffects _playSoundEffects;
    private ILoadingOverlayHost _host = null!;
    private int _loadingOperationsCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadingOverlayService" /> class.
    /// </summary>
    /// <param name="playSoundEffects">The sound effects service for playing notification sounds.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public LoadingOverlayService(PlaySoundEffects playSoundEffects, ILogger logger)
    {
        _playSoundEffects = playSoundEffects;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Initializes the loading overlay service with the specified host.
    /// </summary>
    /// <param name="host">The host that provides UI controls and dispatcher access.</param>
    public void Initialize(ILoadingOverlayHost host)
    {
        _host = host;
    }

    /// <summary>
    ///     Updates the loading state by incrementing or decrementing the loading operation counter.
    ///     Shows or hides the loading overlay and optionally updates the overlay message.
    /// </summary>
    /// <param name="isLoading">True to increment the loading counter; false to decrement it.</param>
    /// <param name="message">The optional message to display on the loading overlay.</param>
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
                if (_loadingOperationsCount > 0)
                    _loadingOperationsCount--;
                else
                    _logger.Debug("[SetLoadingState] Warning: Attempted to decrement loading count when already at 0");
            }

            shouldShowOverlay = _loadingOperationsCount > 0;
        }

        host.SetIsLoadingGamesInternal(shouldShowOverlay);

        host.Dispatcher.Invoke(() =>
        {
            host.SetLoadingOverlayVisible(shouldShowOverlay);
            host.SetMainContentGridEnabled(!shouldShowOverlay);

            if (isLoading && shouldShowOverlay && message != null)
                host.SetLoadingOverlayContent(message);
            else if (!shouldShowOverlay)
                host.SetLoadingOverlayContent((string)Application.Current.TryFindResource("Loading") ?? "Loading...");
        });
    }

    /// <summary>
    ///     Forces release of the loading overlay regardless of the current loading operation count.
    ///     Resets the loading state, cancels any active tokens, and restores the UI to an interactive state.
    /// </summary>
    public void EmergencyRelease()
    {
        var host = _host;
        if (host == null) return;

        _playSoundEffects?.PlayNotificationSound();

        lock (_loadingStateLock)
        {
            _loadingOperationsCount = 0;
        }

        host.SetIsLoadingGamesInternal(false);
        host.CancelAndRecreateToken();

        host.Dispatcher.Invoke(() =>
        {
            host.SetLoadingOverlayVisible(false);
            host.SetMainContentGridEnabled(true);
        });

        _ = host.ResetUiAsync();
        host.UpdateStatusBarService.UpdateContent("Emergency reset performed.");
        _logger.Debug("[Emergency] User forced overlay dismissal via Return button");
    }
}