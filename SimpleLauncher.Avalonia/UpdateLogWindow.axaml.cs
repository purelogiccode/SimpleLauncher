using Avalonia.Controls;
using Avalonia.Threading;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window displaying real-time update installation log messages.
/// </summary>
public partial class UpdateLogWindow : Window
{
    private readonly UpdateLogViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UpdateLogWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing update log logic.</param>
    public UpdateLogWindow(UpdateLogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    ///     Appends a log message to the update log display.
    ///     Awaitable (AV-25) so burst callers preserve ordering and observe
    ///     failures; messages for a closed window are dropped silently.
    /// </summary>
    /// <param name="message">The message to append.</param>
    public Task LogAsync(string message)
    {
        try
        {
            return Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    if (IsLoaded) _viewModel.AppendLog(message);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
                {
                    // Window closed mid-log — drop the message.
                }
            }).GetTask();
        }
        catch
        {
            // Dispatcher shut down — drop the message.
            return Task.CompletedTask;
        }
    }
}