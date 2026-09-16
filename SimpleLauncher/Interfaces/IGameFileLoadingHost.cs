using System.Windows.Controls;
using System.Windows.Threading;
using SimpleLauncher.Services.SystemManager;

namespace SimpleLauncher.Interfaces;

/// <summary>
///     Provides UI elements and methods required by the game file loading orchestrator.
/// </summary>
public interface IGameFileLoadingHost
{
    /// <summary>
    ///     Gets the WPF dispatcher for marshaling calls to the UI thread.
    /// </summary>
    Dispatcher Dispatcher { get; }

    /// <summary>
    ///     Gets the system selection combo box.
    /// </summary>
    ComboBox SystemComboBox { get; }

    /// <summary>
    ///     Gets the WrapPanel used to display game file items.
    /// </summary>
    WrapPanel GameFileGrid { get; }

    /// <summary>
    ///     Gets the scroll viewer containing the game file grid.
    /// </summary>
    ScrollViewer Scroller { get; }

    /// <summary>
    ///     Gets the data grid used to display game files in list view mode.
    /// </summary>
    DataGrid GameDataGrid { get; }

    /// <summary>
    ///     Gets the grid area used for list view preview display.
    /// </summary>
    Grid ListViewPreviewArea { get; }

    /// <summary>
    ///     Gets the image control used to display game previews.
    /// </summary>
    Image PreviewImage { get; }

    /// <summary>
    ///     Gets the current view mode (e.g., "Grid" or "List").
    /// </summary>
    string ViewMode { get; }

    /// <summary>
    ///     Gets a value indicating whether the current operation is a re-sort rather than a fresh load.
    /// </summary>
    bool IsResortOperation { get; }

    /// <summary>
    ///     Gets the list of available system managers.
    /// </summary>
    /// <returns>The list of system managers.</returns>
    IList<SystemManagerService> GetSystemManagers();

    /// <summary>
    ///     Asynchronously displays the system selection screen.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DisplaySystemSelectionScreenAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Sets the loading state of the UI, optionally displaying a message.
    /// </summary>
    /// <param name="isLoading">True to indicate loading; false to indicate idle.</param>
    /// <param name="message">An optional message to display during loading.</param>
    void SetLoadingState(bool isLoading, string? message = null);

    /// <summary>
    ///     Asynchronously prepares the UI before loading game files.
    /// </summary>
    Task SetUiBeforeLoadGameFilesAsync();

    /// <summary>
    ///     Applies pagination to the list of game files.
    /// </summary>
    /// <param name="allFiles">The complete list of game files.</param>
    /// <returns>The paginated subset of files for the current page.</returns>
    IList<string> SetPaginationOfListOfFiles(IList<string> allFiles);

    /// <summary>
    ///     Gets the currently active filter string, or null if no filter is applied.
    /// </summary>
    /// <returns>The current filter string, or null.</returns>
    string? GetCurrentFilter();

    /// <summary>
    ///     Gets the active search query or search mode, or null if none is active.
    /// </summary>
    /// <returns>The search query or mode string, or null.</returns>
    string? GetActiveSearchQueryOrMode();

    /// <summary>
    ///     Gets the MAME sort order configuration.
    /// </summary>
    /// <returns>The sort order string.</returns>
    string GetMameSortOrder();

    /// <summary>
    ///     Gets the active letter filter and search query so a reload re-applies the same
    ///     view instead of silently replacing it with the unfiltered list.
    /// </summary>
    /// <returns>The current start letter (or null) and search query (or null).</returns>
    (string? startLetter, string? searchQuery) GetLoadGameFilesParams();
}