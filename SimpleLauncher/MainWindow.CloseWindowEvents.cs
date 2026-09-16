using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher;

/// <summary>
///     Partial MainWindow containing window closing, disposal, and event unsubscription logic.
/// </summary>
public partial class MainWindow
{
    private bool _isCloseSaveDeferred;

    /// <summary>
    ///     Disposes all managed resources including cancellation tokens, event handlers, and background services.
    /// </summary>
    public void Dispose()
    {
        // Prevent double disposal
        if (_isDisposed)
            return;

        try
        {
            // Set flag to signal async handlers to bail out
            _isDisposed = true;

            // 1. Signal cancellation FIRST to help background threads release locks sooner.
            // We catch ObjectDisposedException to prevent errors if cancellation was already triggered.
            try
            {
                _cancellationSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already cancelled/disposed, ignore
            }

            // Kill any lingering CHDMounter processes as a safety net
            _mountChdFiles?.KillAllChdMounterProcesses(_logger);

            // Stop and release the status bar timer
            if (StatusBarTimer != null)
            {
                StatusBarTimer.Stop();
                StatusBarTimer = null;
            }

            // Dispose tray icon resources
            TrayIconManager?.Dispose();

            // Dispose F8 global hotkey
            _globalHotkeyService?.Dispose();

            // Unsubscribe singleton-held handlers even when Dispose runs without the
            // deferred Closing path (DI teardown, App.OnExit); otherwise singletons
            // keep this window alive (WPF-01). Safe to repeat after UnsubscribeEventHandlers.
            try
            {
                UnsubscribeEventHandlers();
            }
            catch (Exception ex)
            {
                Log.Debug($"Error unsubscribing handlers during Dispose: {ex.Message}");
            }

            // Clean up collections
            GameListItems?.Clear();
            GameDataGrid?.ItemsSource = null;

            // Clear game caches via the cache service
            _gameBrowser?.ClearCache();

            _systemManagers?.Clear();

            _cancellationSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Log but don't throw during disposal
            Log.Debug($"Error during Dispose: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
        {
            // Defer the actual close until the settings save completes; otherwise the
            // process can exit before the background write finishes and lose the last changes.
            if (!_isCloseSaveDeferred)
            {
                e.Cancel = true;
                _isCloseSaveDeferred = true;

                try
                {
                    // Bound the save so a hung write cannot strand the window forever (WPF-13).
                    await SaveApplicationSettings().WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException ex)
                {
                    Log.Error($"Timed out saving settings during close: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error saving settings during close: {ex.Message}");
                }

                try
                {
                    // Cancel any running hash scan here (async, UI still pumps) so
                    // App.OnExit's synchronous backstop rarely has anything to wait
                    // for and can never deadlock the exit thread (WPF-23).
                    var scanner = App.ServiceProvider
                        .GetService<IRetroAchievementsHashScanner>();
                    if (scanner is not null)
                        await scanner.CancelScanAndWaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (Exception ex)
                {
                    Log.Error($"Error canceling hash scan during close: {ex.Message}");
                }

                try
                {
                    // When the application is shutting down, WPF ignores the cancel above
                    // and has already closed this window while the save was awaited.
                    // Close() would throw on the closed window, so run the teardown here
                    // to still release the tray icon and other resources (WPF-01).
                    if (!IsLoaded)
                    {
                        UnsubscribeEventHandlers();
                        Dispose();
                        return;
                    }

                    Close();
                }
                catch (InvalidOperationException ex)
                {
                    // Expected when the window was already closing/closed (app shutdown).
                    Log.Information($"Window already closed after deferred save: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error closing window after deferred save: {ex.Message}");
                }

                return;
            }

            // Unsubscribe from events to prevent memory leaks
            UnsubscribeEventHandlers();

            Dispose();
        }
        catch (Exception ex)
        {
            Log.Error($"Error in Main Window_Closing. Error: {ex.Message}");
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        // Runs even when Application.Shutdown closes the window itself (where the
        // deferred Closing path may never complete): release the tray icon here so
        // it cannot remain clickable while the process winds down and crash on the
        // closed window (WPF-01). Dispose is idempotent.
        TrayIconManager?.Dispose();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    /// <summary>
    ///     Unsubscribes all event handlers to prevent memory leaks.
    /// </summary>
    private void UnsubscribeEventHandlers()
    {
        // Unsubscribe window-level event handlers
        Closing -= MainWindow_Closing;
        StateChanged -= MainWindow_StateChanged;
        Activated -= MainWindow_Activated;
        Deactivated -= MainWindow_Deactivated;

        // Unsubscribe the async Loaded handler if it was subscribed
        if (_asyncLoadedHandler != null) Loaded -= _asyncLoadedHandler;

        // Unsubscribe FilterMenu event handler
        if (_topLetterNumberMenu != null)
            _topLetterNumberMenu.OnLetterSelected -= TopLetterNumberMenu_OnLetterSelectedAsync;

        // Unsubscribe emergency button click handler if it was wired
        if (_emergencyButtonClickHandler != null && LoadingOverlay?.Template != null)
        {
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
                emergencyBtn.Click -= _emergencyButtonClickHandler;
        }

        // Unsubscribe and stop game file watcher
        _lifecycle.UnsubscribeGameFilesChanged(_gameFilesChangedHandler);
        _lifecycle.StopWatching();

        // Unsubscribe game played event
        _gameLauncherService.GamePlayed -= _gamePlayedHandler;
    }
}