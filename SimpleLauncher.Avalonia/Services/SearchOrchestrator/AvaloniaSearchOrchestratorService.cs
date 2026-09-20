namespace SimpleLauncher.Avalonia.Services.SearchOrchestrator;

/// <summary>
///     Validates search queries and coordinates search execution.
///     Extracted from the inline search logic in MainViewModel.
///     Mirrors the WPF SearchOrchestratorService: fails when the selected system is
///     empty/null OR the query is blank, and (via the caller) clears prior search
///     results so stale results never persist.
/// </summary>
public static class AvaloniaSearchOrchestratorService
{
    /// <summary>
    ///     Validates a search query before execution.
    /// </summary>
    /// <param name="searchQuery">The raw search query from the UI.</param>
    /// <param name="selectedSystem">The currently selected system (null or empty = invalid, WPF parity).</param>
    /// <returns>A validation result indicating whether the search should proceed.</returns>
    public static SearchValidationResult ValidateAndPrepare(string searchQuery, string? selectedSystem)
    {
        if (string.IsNullOrEmpty(selectedSystem) || string.IsNullOrWhiteSpace(searchQuery))
            return SearchValidationResult.Failure();

        return SearchValidationResult.Success(searchQuery.Trim());
    }
}