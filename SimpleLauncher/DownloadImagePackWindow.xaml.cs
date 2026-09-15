using System.Windows;
using System.Windows.Controls;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Window that downloads and installs image packs for game systems.
/// </summary>
public partial class DownloadImagePackWindow : IDisposable
{
    private readonly ILogger _logger;
    private readonly DownloadImagePackViewModel _viewModel;
    private Button? _emergencyReturnButton;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DownloadImagePackWindow" /> class.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="viewModel">The view model providing download and extraction logic.</param>
    public DownloadImagePackWindow(ILogger logErrors, DownloadImagePackViewModel viewModel)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);
        _logger = logErrors;

        _viewModel = viewModel;
        DataContext = _viewModel;

        Closing += CloseWindowRoutineAsync;
        Loaded += DownloadImagePackWindowLoadedAsync;

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

    /// <summary>
    ///     Disposes of resources used by the window.
    /// </summary>
    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void DownloadImagePackWindowLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[DownloadImagePackWindowLoadedAsync] Error initializing EasyModeManager");
        }
    }

    private async void CloseWindowRoutineAsync(object? sender, EventArgs e)
    {
        try
        {
            if (_emergencyReturnButton != null)
            {
                _emergencyReturnButton.Click -= EmergencyOverlayRelease_Click;
                _emergencyReturnButton = null;
            }

            await _viewModel.CloseWindowRoutineAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method CloseWindowRoutineAsync");
        }
        finally
        {
            Dispose();
        }
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.EmergencyOverlayRelease();
    }
}