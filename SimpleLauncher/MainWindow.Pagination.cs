using System.Windows;
using Application = System.Windows.Application;

namespace SimpleLauncher;

/// <summary>
///     Partial MainWindow containing pagination button handlers and page navigation logic.
/// </summary>
public partial class MainWindow
{
    private void ResetPaginationButtons()
    {
        UiOrchestratorService.ResetPaginationButtons();
    }

    private async void PrevPageButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isLoadingGames) return;

            if (!UiOrchestratorService.CanGoToPrevPage()) return;

            CancelAndRecreateToken();
            UiOrchestratorService.GoToPreviousPage();

            SetLoadingState(true,
                (string)Application.Current.TryFindResource("LoadingPrevPage") ?? "Loading previous page...");
            _audioInput.PlayNotificationSound();

            var (sl, sq) = GetLoadGameFilesParams();
            await _gameBrowser.LoadGameFilesAsync(sl, sq, _cancellationSource.Token);
        }
        catch (Exception ex)
        {
            // Notify developer
            const string errorMessage = "Previous page button error.";
            _logger.Error(ex, errorMessage);

            // Notify user
            await _messageBox.NavigationButtonErrorMessageBoxAsync();
        }
    }

    private async void NextPageButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isLoadingGames) return;

            if (!UiOrchestratorService.CanGoToNextPage()) return;

            CancelAndRecreateToken();
            UiOrchestratorService.GoToNextPage();

            SetLoadingState(true,
                (string)Application.Current.TryFindResource("LoadingNextPage") ?? "Loading next page...");
            _audioInput.PlayNotificationSound();

            var (sl, sq) = GetLoadGameFilesParams();
            await _gameBrowser.LoadGameFilesAsync(sl, sq, _cancellationSource.Token);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the NextPageButtonClickAsync method");

            // Notify user
            await _messageBox.NavigationButtonErrorMessageBoxAsync();
        }
    }
}