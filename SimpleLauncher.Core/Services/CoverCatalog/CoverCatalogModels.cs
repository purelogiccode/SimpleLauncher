namespace SimpleLauncher.Core.Services.CoverCatalog;

/// <summary>
///     Root index of the per-game cover catalog. It is published as
///     <c>catalog.json</c> next to the per-system folders on the cover asset server.
/// </summary>
public sealed class CoverCatalogManifest
{
    /// <summary>Gets or sets the catalog schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the public base URL of the catalog (without a trailing slash).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Gets or sets the UTC timestamp of the catalog build.</summary>
    public string GeneratedUtc { get; set; } = "";

    /// <summary>Gets the systems included in this catalog.</summary>
    public List<CoverCatalogSystem> Systems { get; set; } = [];
}

/// <summary>Describes one system (folder of covers) in the catalog.</summary>
public sealed class CoverCatalogSystem
{
    /// <summary>Gets or sets the Simple Launcher system name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the URL-safe folder name used for this system.</summary>
    public string Slug { get; set; } = "";

    /// <summary>Gets or sets the number of canonical covers in this system.</summary>
    public int Count { get; set; }

    /// <summary>
    ///     Gets or sets the path of the system index, relative to <see cref="CoverCatalogManifest.BaseUrl" />.
    /// </summary>
    public string IndexFile { get; set; } = "";
}

/// <summary>Index of the canonical covers for one system.</summary>
public sealed class CoverCatalogIndex
{
    /// <summary>Gets or sets the index schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the Simple Launcher system name.</summary>
    public string System { get; set; } = "";

    /// <summary>Gets or sets the URL-safe folder name used for this system.</summary>
    public string Slug { get; set; } = "";

    /// <summary>Gets the canonical cover entries for this system.</summary>
    public List<CoverCatalogEntry> Images { get; set; } = [];
}

/// <summary>One canonical cover image and every filename variant it can satisfy.</summary>
public sealed class CoverCatalogEntry
{
    /// <summary>Gets or sets the canonical file name inside the system folder.</summary>
    public string File { get; set; } = "";

    /// <summary>Gets the game-name aliases (annotation-stripped variants) served by this image.</summary>
    public List<string> Names { get; set; } = [];

    /// <summary>Gets or sets the file size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Gets or sets the image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Gets or sets the image height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>
    ///     Gets or sets whether the source cover has actual transparent pixels. The client
    ///     uses this to replace a legacy white-flattened JPEG with the transparent PNG.
    /// </summary>
    public bool HasAlpha { get; set; }
}
