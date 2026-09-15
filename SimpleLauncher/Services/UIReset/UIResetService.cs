using System.Windows;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Services.UIReset;

/// <summary>
///     Handles resetting the UI to its initial state, clearing filters, selections, and pagination.
/// </summary>
public class UiResetService : IUiResetService
{
    private readonly ILogger _logger;
    private IUiResetHost _host = null!;

    /// <summary>Initializes a new instance of the UiResetService with error logging.</summary>
    public UiResetService(ILogger logErrors)
    {
        _logger = logErrors;
    }

    /// <summary>Initializes the service with the specified UI host.</summary>
    public void Initialize(IUiResetHost host)
    {
        _host = host;
    }

    /// <summary>Asynchronously resets the UI, clearing all filters, selections, and returning to the system selection screen.</summary>
    public async Task ResetUiAsync()
    {
        try
        {
            _host.CancelAndRecreateToken();

            if (_host.IsUiUpdating) return;

            _host.IsUiUpdating = true;

            if (_host.IsLoadingGames)
            {
                _host.IsLoadingGames = false;
                _host.SetLoadingOverlayVisible(false);
            }

            try
            {
                _host.ResetPaginationButtons();

                _host.SetSearchTextBoxText("");

                _host.CurrentFilter = null;
                _host.ActiveSearchQueryOrMode = null;

                _host.SelectedSystem = null;
                _host.ClearPreviewImage();
                _host.SetSystemComboBoxSelectedItem(null);
                _host.SetEmulatorComboBoxSelectedItem(null);
                _host.MameSortOrder = AppConstants.MameSortOrderFileName;

                var nosystemselected = (string)Application.Current.TryFindResource("Nosystemselected") ??
                                       "No system selected";
                _host.SelectedSystem = nosystemselected;
                _host.PlayTime = "00:00:00";

                await _host.DisplaySystemSelectionScreenAsync(_host.CurrentCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Do nothing.
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method ResetUiAsync");
            }
            finally
            {
                _host.IsUiUpdating = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Do nothing - cancellation is expected when the UI is reset multiple times
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ResetUiAsync");
        }
    }
}