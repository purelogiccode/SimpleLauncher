namespace SimpleLauncher.Core.Services.CoverCatalog;

/// <summary>Request for downloading the missing covers of one system.</summary>
public sealed class CoverDownloadRequest
{
    /// <summary>Gets or sets the Simple Launcher system name.</summary>
    public string SystemName { get; set; } = "";

    /// <summary>Gets or sets the configured system image folder (relative or absolute).</summary>
    public string SystemImageFolder { get; set; } = "";

    /// <summary>Gets the game file paths (files or grouped-folder paths) to check.</summary>
    public List<string> GameFilePaths { get; set; } = [];
}

/// <summary>Progress of a missing-cover batch.</summary>
/// <param name="TotalGames">Distinct games in the batch.</param>
/// <param name="Completed">Games processed so far.</param>
/// <param name="CurrentGame">The game currently being processed.</param>
public sealed record CoverDownloadProgress(int TotalGames, int Completed, string CurrentGame);

/// <summary>Final status of a missing-cover batch.</summary>
public enum CoverDownloadStatus
{
    /// <summary>The batch finished (possibly with per-file failures).</summary>
    Completed,

    /// <summary>The selected system has no games to check.</summary>
    NoGames,

    /// <summary>Every game already had a cover or no catalog match was needed.</summary>
    NoMissingCovers,

    /// <summary>The catalog has no entry for the selected system.</summary>
    SystemNotAvailable,

    /// <summary>The catalog or its index could not be loaded and no cache existed.</summary>
    CatalogUnavailable
}

/// <summary>Result of a missing-cover batch.</summary>
public sealed class CoverDownloadResult
{
    /// <summary>Gets or sets the final status.</summary>
    public CoverDownloadStatus Status { get; set; }

    /// <summary>Gets or sets the distinct games in the batch.</summary>
    public int TotalGames { get; set; }

    /// <summary>Gets or sets the games that already had a local cover.</summary>
    public int AlreadyPresent { get; set; }

    /// <summary>Gets or sets the games with a catalog entry.</summary>
    public int Matched { get; set; }

    /// <summary>Gets or sets the covers downloaded successfully.</summary>
    public int Downloaded { get; set; }

    /// <summary>Gets or sets the games without a local or catalog cover.</summary>
    public int NotFound { get; set; }

    /// <summary>Gets or sets the downloads that failed for a non-404 reason.</summary>
    public int Failed { get; set; }

    /// <summary>Gets or sets an optional diagnostic message.</summary>
    public string? ErrorMessage { get; set; }
}
