using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SimpleLauncher.ViewModels;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;

namespace SimpleLauncher;

/// <summary>
///     Window that displays global statistics across all systems.
/// </summary>
internal partial class GlobalStatsWindow : IDisposable
{
    private readonly GlobalStatsViewModel _viewModel;
    private EventHandler? _closeRequestedHandler;
    private Button? _emergencyReturnButton;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GlobalStatsWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing global statistics logic.</param>
    public GlobalStatsWindow(GlobalStatsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _closeRequestedHandler = (_, _) => Application.Current.Dispatcher.InvokeAsync(Close);
        _viewModel.CloseRequested += _closeRequestedHandler;

        DataContext = _viewModel;
        App.ApplyThemeToWindow(this);

        Closing += GlobalStatsWindow_Closing;

        Loaded += (_, _) =>
        {
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
            {
                _emergencyReturnButton = emergencyBtn;
                emergencyBtn.Click += EmergencyOverlayRelease_Click;
            }
        };
    }

    private bool _disposed;

    /// <summary>
    ///     Disposes of resources used by the window.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        if (_emergencyReturnButton != null)
        {
            _emergencyReturnButton.Click -= EmergencyOverlayRelease_Click;
            _emergencyReturnButton = null;
        }

        _viewModel.Dispose();
    }

    /// <summary>
    ///     Initializes the window with the list of system managers for statistics calculation.
    /// </summary>
    /// <param name="systemManagers">The list of system manager configurations.</param>
    internal void Initialize(List<SystemManager> systemManagers)
    {
        _viewModel.Initialize(systemManagers);
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.EmergencyOverlayRelease();
    }

    private void GlobalStatsWindow_Closing(object? sender, CancelEventArgs e)
    {
        // When idle the view model lets the close through (it never cancels e): tear
        // down exactly once here. While processing, the command cancels e and prompts;
        // a confirmed close returns via CloseRequested -> Close() -> this handler, at
        // which point IsProcessing is false and no re-prompt occurs (WPF-21).
        if (!_viewModel.IsProcessing)
        {
            // Unsubscribe events
            if (_closeRequestedHandler != null)
            {
                _viewModel.CloseRequested -= _closeRequestedHandler;
                _closeRequestedHandler = null;
            }

            Dispose();
            return;
        }

        // Execute the closing command (async prompt; must not Dispose yet)
        if (_viewModel.ClosingCommand.CanExecute(e)) _viewModel.ClosingCommand.Execute(e);
    }
}