namespace CoverCatalogBuilder;

/// <summary>Build statistics for one system folder.</summary>
/// <param name="Name">The Simple Launcher system name.</param>
/// <param name="Slug">The URL-safe output folder.</param>
/// <param name="SourceFiles">Supported source images found.</param>
/// <param name="Groups">Canonical cover groups.</param>
/// <param name="Merged">Variant images merged into canonical groups.</param>
/// <param name="Written">Derivatives written in this run.</param>
/// <param name="Unsupported">Non-image files skipped.</param>
/// <param name="Unreadable">Images that could not be decoded.</param>
/// <param name="Duration">Elapsed build time.</param>
/// <param name="FatalError">A fatal error message, or null.</param>
internal sealed record SystemBuildResult(
    string Name,
    string Slug,
    int SourceFiles,
    int Groups,
    int Merged,
    int Written,
    int Unsupported,
    int Unreadable,
    TimeSpan Duration,
    string? FatalError)
{
    /// <summary>Gets the number of existing derivatives reused from a previous run.</summary>
    public int Skipped { get; init; }

    /// <summary>Gets the bytes written for new derivatives in this run.</summary>
    public long BytesWritten { get; init; }

    /// <summary>Gets the number of stale derivatives removed from the output folder.</summary>
    public int RemovedStale { get; init; }
}
