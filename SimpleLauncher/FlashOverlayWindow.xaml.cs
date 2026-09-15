using System.Windows;
using System.Windows.Media.Animation;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Full-screen overlay window that displays a brief flash animation effect.
/// </summary>
public partial class FlashOverlayWindow : IDisposable
{
    private readonly EventHandler _closeRequestedHandler;
    private readonly FlashOverlayViewModel _viewModel;
    private CancellationTokenSource? _cts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FlashOverlayWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing flash overlay logic.</param>
    public FlashOverlayWindow(FlashOverlayViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        // Named handler so it can be removed on teardown; the anonymous version kept
        // this window alive via the longer-lived view model (WPF-20).
        _closeRequestedHandler = (_, _) => Close();
        _viewModel.CloseRequested += _closeRequestedHandler;

        DataContext = _viewModel;

        Closing += (_, _) => Dispose();
    }

    /// <summary>
    ///     Disposes the cancellation token source used by the flash animation and suppresses finalization.
    /// </summary>
    public void Dispose()
    {
        // Cancel before dispose so an in-flight delay observes cancellation
        // (OperationCanceledException) instead of disposal (WPF-20).
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cts.Dispose();
        }

        _viewModel.CloseRequested -= _closeRequestedHandler;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Displays the flash overlay with a fade-in/out animation and closes automatically.
    /// </summary>
    public async Task ShowFlashAsync()
    {
        // Replace (don't orphan) the previous CTS; a repeat call cancels the earlier
        // flash, which tolerates the disposal below via its catch filter (WPF-20).
        var cts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _cts, cts);
        if (previousCts is not null)
        {
            try
            {
                previousCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            previousCts.Dispose();
        }

        // Set the window size and position
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        // Create a fade-in animation
        var fadeInAnimation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
        {
            AutoReverse = true // Automatically fade out
        };

        // Apply the animation to the rectangle
        FlashRectangle.BeginAnimation(OpacityProperty, fadeInAnimation);

        // Show the window
        Show();

        try
        {
            // Wait for the animation to complete. ObjectDisposedException is expected
            // when a repeat ShowFlashAsync or Dispose retires this call's CTS mid-delay.
            await Task.Delay(600, cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        // Close the window after the flash
        _viewModel.OnAnimationCompleted();
    }
}