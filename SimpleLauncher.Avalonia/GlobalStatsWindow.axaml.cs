using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window that displays global statistics across all systems.
/// </summary>
public partial class GlobalStatsWindow : Window, IDisposable
{
    private readonly ILogger _logger;
    private readonly GlobalStatsViewModel _viewModel;
    private EventHandler? _closeRequestedHandler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GlobalStatsWindow" /> class.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="viewModel">The view model providing global statistics logic.</param>
    public GlobalStatsWindow(ILogger logErrors, GlobalStatsViewModel viewModel)
    {
        InitializeComponent();

        _logger = logErrors;
        _viewModel = viewModel;
        _closeRequestedHandler = (_, _) => Close();
        _viewModel.CloseRequested += _closeRequestedHandler;

        DataContext = _viewModel;

        Closing += GlobalStatsWindow_Closing;
        Closed += (_, _) =>
        {
            if (_closeRequestedHandler != null)
            {
                _viewModel.CloseRequested -= _closeRequestedHandler;
                _closeRequestedHandler = null;
            }

            Dispose();
        };
    }

    /// <summary>
    ///     Disposes of resources used by the window.
    /// </summary>
    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initializes the window with the list of system manager configurations for statistics calculation.
    /// </summary>
    /// <param name="systemManagers">The list of system manager configurations.</param>
    public void Initialize(List<SystemManagerConfig> systemManagers)
    {
        _viewModel.Initialize(systemManagers);
    }

    // Set once the user confirms closing during processing: the re-entrant Close()
    // below then proceeds without prompting again (AV-09).
    private bool _forceClose;

    private async void GlobalStatsWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose) return;

        try
        {
            // Processing is active - cancel the close and ask the user to confirm
            if (_viewModel.IsProcessing)
            {
                e.Cancel = true;

                var allowClose = await _viewModel.RequestCloseAsync();
                if (allowClose)
                {
                    _forceClose = true;
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method GlobalStatsWindow_Closing.");
        }
    }
}