namespace SimpleLauncher.Avalonia.Services.SearchOrchestrator;

/// <summary>
///     Result of validating a search query.
/// </summary>
internal class SearchValidationResult
{
    public bool IsValid { get; init; }
    public string ValidatedQuery { get; init; } = "";

    public static SearchValidationResult Success(string query)
    {
        return new SearchValidationResult { IsValid = true, ValidatedQuery = query };
    }

    public static SearchValidationResult Failure()
    {
        return new SearchValidationResult { IsValid = false };
    }
}