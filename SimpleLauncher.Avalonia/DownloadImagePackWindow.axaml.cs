using Avalonia.Controls;
using Avalonia.Interactivity;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window that downloads and installs image packs for game systems.
/// </summary>
public partial class DownloadImagePackWindow : Window, IDisposable
{
    private readonly ILogger _logger;
    private readonly DownloadImagePackViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DownloadImagePackWindow" /> class.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="viewModel">The view model providing download and extraction logic.</param>
    /// <param name="localization">The localization service used to set localized UI strings.</param>
    public DownloadImagePackWindow(ILogger logErrors, DownloadImagePackViewModel viewModel,
        LocalizationService localization)
    {
        InitializeComponent();
        _logger = logErrors;

        _viewModel = viewModel;
        DataContext = _viewModel;

        // Localize the emergency return button (WPF DynamicResource ReturnButton parity)
        EmergencyButton.Content = localization.GetString("ReturnButton");
        ToolTip.SetTip(EmergencyButton,
            localization.GetString("ClickHereIfTheLoadingScreenIsStuckToReturnToTheMainMenu"));

        Closing += CloseWindowRoutineAsync;
        Loaded += DownloadImagePackWindowLoadedAsync;
    }

    /// <summary>
    ///     Disposes of resources used by the window.
    /// </summary>
    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void DownloadImagePackWindowLoadedAsync(object? sender, EventArgs e)
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

    // Set once the pre-close routine has run: the re-entrant Close() below then proceeds.
    private bool _closeConfirmed;

    private async void CloseWindowRoutineAsync(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            if (_closeConfirmed)
            {
                Dispose();
                return;
            }

            // No deferral on this event: cancel the close, await the in-flight save/cancel
            // routine first, then re-close. Closing immediately would dispose the
            // DownloadManager/scope while the routine still uses them (AV-08).
            e.Cancel = true;

            try
            {
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

            _closeConfirmed = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error closing DownloadImagePackWindow after pre-close routine");
        }
    }

    private void EmergencyOverlayRelease_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.EmergencyOverlayRelease();
    }
}