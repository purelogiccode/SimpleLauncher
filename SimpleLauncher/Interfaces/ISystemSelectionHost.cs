using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Threading;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Services.SystemManager;

namespace SimpleLauncher.Interfaces;

/// <summary>
///     Provides access to the UI elements and state used by the system selection screen.
/// </summary>
public interface ISystemSelectionHost
{
    /// <summary>
    ///     Gets the dispatcher for the host window, used to marshal calls to the UI thread.
    /// </summary>
    Dispatcher Dispatcher { get; }

    /// <summary>
    ///     Gets the wrap panel that displays the game file buttons.
    /// </summary>
    WrapPanel GameFileGrid { get; }

    /// <summary>
    ///     Gets the top system selection area.
    /// </summary>
    Border TopSystemSelection { get; }

    /// <summary>
    ///     Gets the status bar area grid.
    /// </summary>
    Grid StatusBarArea { get; }

    /// <summary>
    ///     Gets the list view preview area grid.
    /// </summary>
    Grid ListViewPreviewArea { get; }

    /// <summary>
    ///     Gets the preview image control.
    /// </summary>
    Image PreviewImage { get; }

    /// <summary>
    ///     Gets the total files label.
    /// </summary>
    Label TotalFilesLabel { get; }

    /// <summary>
    ///     Gets the previous page navigation button.
    /// </summary>
    Button PrevPageButton2 { get; }

    /// <summary>
    ///     Gets the next page navigation button.
    /// </summary>
    Button NextPageButton2 { get; }

    /// <summary>
    ///     Gets the search text box.
    /// </summary>
    TextBox SearchTextBox { get; }

    /// <summary>
    ///     Gets the system combo box.
    /// </summary>
    ComboBox SystemComboBox { get; }

    /// <summary>
    ///     Gets the emulator combo box.
    /// </summary>
    ComboBox EmulatorComboBox { get; }

    /// <summary>
    ///     Gets the sort order toggle button.
    /// </summary>
    Button SortOrderToggleButton { get; }

    /// <summary>
    ///     Gets the collection of game list view items.
    /// </summary>
    ObservableCollection<GameListViewItem> GameListItems { get; }

    /// <summary>
    ///     Gets or sets the name of the currently selected system.
    /// </summary>
    string? SelectedSystem { get; set; }

    /// <summary>
    ///     Gets or sets the play time string shown in the status bar.
    /// </summary>
    string PlayTime { get; set; }

    /// <summary>
    ///     Gets or sets whether the play time display is visible.
    /// </summary>
    bool IsPlayTimeVisible { get; set; }

    /// <summary>
    ///     Gets the current cancellation token for the active operation.
    /// </summary>
    CancellationToken CurrentCancellationToken { get; }

    /// <summary>
    ///     Sets the loading state and optionally displays a loading message.
    /// </summary>
    /// <param name="isLoading">True to show loading; false to hide it.</param>
    /// <param name="message">Optional message to display during loading.</param>
    void SetLoadingState(bool isLoading, string? message = null);

    /// <summary>
    ///     Cancels the current operation and recreates the cancellation token.
    /// </summary>
    void CancelAndRecreateToken();

    /// <summary>
    ///     Resets the UI to its default state asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetUiAsync();

    /// <summary>
    ///     Resets the pagination buttons to their default state.
    /// </summary>
    void ResetPaginationButtons();

    /// <summary>
    ///     Loads and renders every game of the currently selected system (the "All"
    ///     letter-button path), so a system selection never stops at the summary screen.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LoadAllGamesAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Updates the sort order toggle button UI to reflect the current sort order.
    /// </summary>
    void UpdateSortOrderButtonUi();

    /// <summary>
    ///     Clears all images from the game file grid buttons.
    /// </summary>
    /// <param name="panel">The panel containing game button images to clear.</param>
    void ClearGameButtonImages(Panel panel);

    /// <summary>
    ///     Gets the list of available system managers.
    /// </summary>
    /// <returns>The list of system managers.</returns>
    IList<SystemManagerService> GetSystemManagers();

    /// <summary>
    ///     Sets the list of system managers.
    /// </summary>
    /// <param name="managers">The list of system managers to set.</param>
    void SetSystemManagers(IList<SystemManagerService> managers);

}