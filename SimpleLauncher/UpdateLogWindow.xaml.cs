using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Window displaying real-time update installation log messages.
/// </summary>
public partial class UpdateLogWindow
{
    private readonly UpdateLogViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UpdateLogWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing update log logic.</param>
    public UpdateLogWindow(UpdateLogViewModel viewModel)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    ///     Appends a log message to the update log display.
    /// </summary>
    /// <param name="message">The message to append.</param>
    public void Log(string message)
    {
        // BeginInvoke (not Invoke): Log is called from background update threads and
        // must never block on the UI thread or throw during shutdown (WPF-17).
        try
        {
            Dispatcher.BeginInvoke(() => _viewModel.AppendLog(message));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            Serilog.Log.Debug($"UpdateLogWindow Log dropped during shutdown: {ex.Message}");
        }
    }
}