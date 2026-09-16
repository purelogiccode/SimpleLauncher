using System.Windows;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Services.Pagination;

/// <summary>
///     Manages file list pagination, handling page navigation, button states, and status labels.
/// </summary>
public class PaginationService : IPaginationService
{
    private IPaginationHost _host = null!;

    /// <summary>
    ///     Gets the current 1-based page number.
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    ///     Gets the total number of files across all pages.
    /// </summary>
    public int TotalFiles { get; private set; }

    /// <summary>
    ///     Gets or sets the number of files displayed per page.
    /// </summary>
    public int FilesPerPage { get; set; }

    /// <summary>
    ///     Gets or sets the file count threshold above which pagination is applied.
    /// </summary>
    public int PaginationThreshold { get; set; }

    /// <summary>
    ///     Initializes the service with a host that provides UI callbacks for pagination controls.
    /// </summary>
    public void Initialize(IPaginationHost host)
    {
        _host = host;
    }

    /// <summary>
    ///     Resets pagination to the first page and disables all navigation buttons.
    /// </summary>
    public void Reset()
    {
        CurrentPage = 1;
        _host?.SetPrevPageButtonEnabled(false);
        _host?.SetNextPageButtonEnabled(false);
        _host?.ScrollToTop();
        _host?.UpdateTotalFilesLabel(null);
    }

    /// <summary>
    ///     Determines whether navigation to the previous page is possible.
    /// </summary>
    public bool CanGoPrev()
    {
        return CurrentPage > 1;
    }

    /// <summary>
    ///     Determines whether navigation to the next page is possible.
    /// </summary>
    public bool CanGoNext()
    {
        var totalPages = (int)Math.Ceiling(TotalFiles / (double)FilesPerPage);
        return CurrentPage < totalPages;
    }

    /// <summary>
    ///     Navigates to the previous page if available.
    /// </summary>
    public void GoToPreviousPage()
    {
        if (CanGoPrev()) CurrentPage--;
    }

    /// <summary>
    ///     Navigates to the next page if available.
    /// </summary>
    public void GoToNextPage()
    {
        if (CanGoNext()) CurrentPage++;
    }

    /// <summary>
    ///     Applies pagination to the file list, returning only the current page's files when the threshold is exceeded.
    /// </summary>
    public IList<string> ApplyPagination(IList<string> allFiles)
    {
        TotalFiles = allFiles.Count;

        if (TotalFiles == 0)
        {
            _host?.AddNoFilesMessage();
            _host?.SetPrevPageButtonEnabled(false);
            _host?.SetNextPageButtonEnabled(false);
            _host?.UpdateTotalFilesLabel(BuildStatusLabel(0, 0, 0));
            return allFiles;
        }

        // The result set can shrink below the current page (filter change, delete,
        // watcher reload). Clamp before computing the slice so Skip() cannot return an
        // empty page and the status label cannot read e.g. "801 to 200 out of 200".
        var totalPages = FilesPerPage > 0 ? (int)Math.Ceiling(TotalFiles / (double)FilesPerPage) : 1;
        CurrentPage = Math.Clamp(CurrentPage, 1, Math.Max(totalPages, 1));

        var startIndex = ((CurrentPage - 1) * FilesPerPage) + 1;
        var endIndex = Math.Min(startIndex + FilesPerPage - 1, TotalFiles);

        if (TotalFiles > PaginationThreshold)
        {
            allFiles = allFiles.Skip((CurrentPage - 1) * FilesPerPage).Take(FilesPerPage).ToList();
            UpdateButtonStates();
        }
        else
        {
            _host?.SetPrevPageButtonEnabled(false);
            _host?.SetNextPageButtonEnabled(false);
        }

        _host?.UpdateTotalFilesLabel(BuildStatusLabel(startIndex, endIndex, TotalFiles));
        return allFiles;
    }

    private void UpdateButtonStates()
    {
        _host?.SetPrevPageButtonEnabled(CurrentPage > 1);
        _host?.SetNextPageButtonEnabled(CurrentPage * FilesPerPage < TotalFiles);
    }

    private static string BuildStatusLabel(int startIndex, int endIndex, int total)
    {
        var displayingfiles0To = Application.Current?.TryFindResource("Displayingfiles0to") as string ??
                                 "Displaying files 0 to";
        var outOf = Application.Current?.TryFindResource("outof") as string ?? "out of";
        var totalText = Application.Current?.TryFindResource("total") as string ?? "total";
        var displayingfiles = Application.Current?.TryFindResource("Displayingfiles") as string ?? "Displaying files";
        var to = Application.Current?.TryFindResource("to") as string ?? "to";

        return total == 0
            ? $"{displayingfiles0To} 0 {outOf} 0 {totalText}"
            : $"{displayingfiles} {(total > 0 ? startIndex : 0)} {to} {endIndex} {outOf} {total} {totalText}";
    }
}