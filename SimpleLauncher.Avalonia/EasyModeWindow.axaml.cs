using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     OpenEmu-themed window for the EasyMode "Add System" workflow. The content lives in the
///     reusable <see cref="Views.EasyModeView" /> (also embedded by the first-run wizard);
///     this shell owns the ViewModel lifetime and the window chrome.
/// </summary>
internal partial class EasyModeWindow : Window, IDisposable
{
    private readonly EasyModeViewModel _viewModel;
    private bool _disposed;

    public EasyModeWindow(EasyModeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        // Set up the close callback so the ViewModel can request window close after successful add
        _viewModel.RequestClose = () => Dispatcher.UIThread.InvokeAsync(Close);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _viewModel.RequestClose = null;
        _viewModel.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async void Window_Opened(object? sender, EventArgs e)
    {
        try
        {
            Log.Debug("EasyModeWindow opened; loading EasyMode systems");
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EasyModeWindow load failed");
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Log.Debug("EasyModeWindow closed");
        Dispose();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.Debug("EasyModeWindow close button clicked by the user");
        Close();
    }
}
