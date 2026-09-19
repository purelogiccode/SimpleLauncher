using SimpleLauncher.Avalonia.Interfaces;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Partial MainWindow implementing <see cref="IAvaloniaLoadingOverlayHost" /> for loading
///     overlay coordination (WPF MainWindow.LoadingOverlayHost.cs parity).
/// </summary>
public partial class MainWindow : IAvaloniaLoadingOverlayHost
{
    void IAvaloniaLoadingOverlayHost.SetIsLoading(bool isLoading)
    {
        // Drive the ViewModel properties (the overlay XAML binds to them). Setting the
        // control property directly would replace the binding permanently, after which
        // every later state change (e.g. IsLoading=false in a finally) would be ignored
        // and the overlay could stay stuck on screen.
        _viewModel.IsLoading = isLoading;
    }

    void IAvaloniaLoadingOverlayHost.SetLoadingMessage(string message)
    {
        _viewModel.LoadingMessage = message;
    }

    Task IAvaloniaLoadingOverlayHost.ResetUiAsync()
    {
        return _uiResetService.ResetUiAsync();
    }

    void IAvaloniaLoadingOverlayHost.CancelAndRecreateToken()
    {
        _uiResetCancellationSource.Cancel();
        _uiResetCancellationSource.Dispose();
        _uiResetCancellationSource = new CancellationTokenSource();
    }

    void IAvaloniaLoadingOverlayHost.SetMainContentGridEnabled(bool enabled)
    {
        MainContentGrid.IsEnabled = enabled;
    }
}