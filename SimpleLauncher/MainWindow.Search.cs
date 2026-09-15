using System.Windows;
using System.Windows.Input;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher;

/// <summary>
///     Partial MainWindow containing search button handlers and search text box interaction logic.
/// </summary>
public partial class MainWindow
{
    private async void SearchButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("Searching") ??
                                                     "Searching...");
                _audioInput.PlayNotificationSound();
                await ExecuteSearchAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method SearchButtonClickAsync");
                await _messageBox.MainWindowSearchEngineErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method SearchButtonClickAsync");
        }
    }

    private async void SearchTextBoxKeyDownAsync(object sender, KeyEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (e.Key != Key.Enter) return;

                _audioInput.PlayNotificationSound(); // Play sound immediately
                await ExecuteSearchAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method SearchTextBoxKeyDownAsync");
                await _messageBox.MainWindowSearchEngineErrorMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method SearchTextBoxKeyDownAsync");
        }
    }

    private async Task ExecuteSearchAsync()
    {
        if (_isLoadingGames) return;

        UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("ExecutingSearch") ??
                                             "Executing search...");
        var searchingMsg = (string)Application.Current.TryFindResource("Searchingpleasewait") ??
                           "Searching... Please wait.";
        SetLoadingState(true, searchingMsg);

        // The CTS instance marks the load generation: only the latest load may hide
        // the overlay in the finally below (WPF-15).
        CancellationTokenSource? activeCts = null;

        try
        {
            CancelAndRecreateToken();
            activeCts = _cancellationSource;
            ResetPaginationButtons();

            var searchQuery = SearchTextBox.Text.Trim();
            ((IUiResetHost)this).ActiveSearchQueryOrMode = searchQuery;

            var selectedSystem = SystemComboBox.SelectedItem?.ToString();
            var result =
                await _gameBrowser.ValidateAndPrepareAsync(searchQuery, selectedSystem, _cancellationSource.Token);

            if (!result.IsValid)
            {
                if (SystemComboBox.SelectedItem == null)
                    await _messageBox.SelectSystemBeforeSearchMessageBoxAsync();
                else
                    await _messageBox.EnterSearchQueryMessageBoxAsync();

                return;
            }

            _topLetterNumberMenu.DeselectLetter();
            ((IUiResetHost)this).CurrentFilter = null;

            try
            {
                await _gameBrowser.LoadGameFilesAsync(null, result.ValidatedQuery, _cancellationSource.Token);
            }
            catch (Exception ex)
            {
                const string contextMessage = "Error during search execution.";
                _logger.Error(ex, contextMessage);

                await _messageBox.MainWindowSearchEngineErrorMessageBoxAsync();
            }
        }
        finally
        {
            // A stale search finishing after a newer pagination/system load started
            // must not clear that load's overlay (WPF-15).
            if (activeCts is not null && !_isDisposed && ReferenceEquals(activeCts, _cancellationSource))
                SetLoadingState(false);
        }
    }
}