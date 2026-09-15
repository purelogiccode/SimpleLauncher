using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Styling;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Full-screen overlay window that displays a brief flash animation effect.
/// </summary>
public partial class FlashOverlayWindow : Window, IDisposable
{
    private readonly EventHandler _closeRequestedHandler;
    private readonly FlashOverlayViewModel _viewModel;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FlashOverlayWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing flash overlay logic.</param>
    public FlashOverlayWindow(FlashOverlayViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _closeRequestedHandler = (_, _) => Close();
        _viewModel.CloseRequested += _closeRequestedHandler;

        DataContext = _viewModel;

        // AV-22: Closed only cancels — it must never dispose the CTS while the
        // animation task is still awaiting its token (dispose race). Ownership
        // of disposal stays with ShowFlashAsync / Dispose.
        Closed += (_, _) =>
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed — nothing to cancel.
            }
        };
    }

    /// <summary>
    ///     Disposes the cancellation token source used by the flash animation and suppresses finalization.
    ///     Call only after <see cref="ShowFlashAsync" /> has completed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _viewModel.CloseRequested -= _closeRequestedHandler;
        CancelAndDisposeCts();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Cancels and disposes the current CTS (if any) and clears the field —
    ///     the AV-22 fix for the leak where each <see cref="ShowFlashAsync" />
    ///     orphaned the previous source undisposed.
    /// </summary>
    private void CancelAndDisposeCts()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts is null) return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed — nothing to cancel.
        }

        cts.Dispose();
    }

    /// <summary>
    ///     Displays the flash overlay with a fade-in/out animation and closes automatically.
    ///     Safe to call again while a previous flash is running — the previous
    ///     animation is cancelled and its CTS disposed (AV-22).
    /// </summary>
    public async Task ShowFlashAsync()
    {
        // Dispose any previous source before replacing it (CTS leak fix);
        // a still-running previous flash observes cancellation and returns.
        CancelAndDisposeCts();
        var cts = _cts = new CancellationTokenSource();

        try
        {
            // Size/position before Show so the window never flashes at the
            // default location for a frame (AV-22: Show-before-layout).
            var primaryScreen = Screens.Primary ?? Screens.All.FirstOrDefault(static s => s.IsPrimary);
            if (primaryScreen != null)
            {
                Position = primaryScreen.Bounds.Position;
                Width = primaryScreen.Bounds.Width;
                Height = primaryScreen.Bounds.Height;
            }

            // Show the window before running the animation
            Show();

            // Create a fade-in/fade-out animation (0% → 100% → 0% opacity over 600ms)
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(600),
                IterationCount = new IterationCount(1),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(OpacityProperty, 0.0) } },
                    new KeyFrame { Cue = new Cue(0.5), Setters = { new Setter(OpacityProperty, 1.0) } },
                    new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(OpacityProperty, 0.0) } }
                }
            };

            try
            {
                await animation.RunAsync(FlashRectangle, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                // Superseded by a newer flash (or disposed mid-animation) — stop quietly.
                return;
            }
            finally
            {
                // Release our source only if a newer flash has not replaced it.
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                    cts.Dispose();
                }
            }

            // Close the window after the flash
            _viewModel.OnAnimationCompleted();
        }
        catch
        {
            // Never leak the source on Show/layout failures either.
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
                cts.Dispose();
            }

            throw;
        }
    }
}