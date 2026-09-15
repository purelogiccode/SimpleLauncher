using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SimpleLauncher.Updater.Services;

/// <summary>
///     Service for interacting with the GitHub API to fetch release information,
///     with fallback to a secondary server.
/// </summary>
internal partial class GitHubService
{
    private const string RepoName = "SimpleLauncher";

    private const string SecondaryServerBaseUrl =
        "https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/";

    private const int GitHubTimeoutSeconds = 5;
    private static readonly string[] RepoOwners = ["purelogiccode"];

    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Initializes a new instance of the GitHubService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for API requests.</param>
    public GitHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    ///     Gets the current runtime identifier based on the process architecture.
    /// </summary>
    public static string CurrentRuntimeIdentifier
    {
        get
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            return arch switch
            {
                Architecture.Arm64 => "win-arm64",
                _ => "win-x64"
            };
        }
    }

    /// <summary>
    ///     Event raised when a log message needs to be displayed.
    /// </summary>
    public event EventHandler<EventArgs<string>>? LogMessage;

    /// <summary>
    ///     Fetches the latest release asset URL from GitHub with a timeout,
    ///     falling back to the secondary server if GitHub is not available.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    ///     A tuple containing the normalized version string, the asset download URL, and the
    ///     secondary-server fallback URL for the same asset (null when the fallback was already used).
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when both GitHub and fallback requests fail.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the release data is invalid or the asset is not found.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<(string version, string assetUrl, string? fallbackAssetUrl)> GetLatestReleaseAssetUrlAsync(
        CancellationToken cancellationToken = default)
    {
        // Try each GitHub repository in order (primary, then the transferred organization)
        foreach (var repoOwner in RepoOwners)
        {
            var gitHubResult = await TryGetGitHubReleaseAsync(repoOwner, cancellationToken);

            if (gitHubResult != null) return gitHubResult.Value;

            LogMessage?.Invoke(this,
                new EventArgs<string>(
                    $"GitHub repository '{repoOwner}/{RepoName}' not responding. Trying the next source..."));
        }

        // If GitHub failed, fall back to secondary server
        LogMessage?.Invoke(this,
            new EventArgs<string>(
                $"GitHub not responding after {GitHubTimeoutSeconds} seconds. Using secondary server..."));
        return await GetFallbackReleaseAsync(cancellationToken);
    }

    /// <summary>
    ///     Attempts to get the release from a GitHub repository with a 5-second timeout.
    /// </summary>
    /// <param name="repoOwner">The GitHub repository owner (organization or user) to query.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The version and asset URL if successful, null if timed out or failed.</returns>
    private async Task<(string version, string assetUrl, string? fallbackAssetUrl)?> TryGetGitHubReleaseAsync(
        string repoOwner, CancellationToken cancellationToken = default)
    {
        try
        {
            LogMessage?.Invoke(this,
                new EventArgs<string>($"Fetching the latest release from GitHub ({repoOwner}/{RepoName})..."));

            // Create a cancellation token that expires after 5 seconds, linked to the external token
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(GitHubTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            var apiUrl = $"https://api.github.com/repos/{repoOwner}/{RepoName}/releases/latest";
            // UPD-11: dispose the response (socket/stream leak otherwise).
            using var response = await _httpClient.GetAsync(apiUrl, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                LogMessage?.Invoke(this,
                    new EventArgs<string>($"GitHub API returned status code: {response.StatusCode}"));
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync(linkedCts.Token);
            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            var root = jsonDoc.RootElement;

            var versionTag = root.GetProperty("tag_name").GetString() ?? "";

            // Validate version tag format
            if (string.IsNullOrWhiteSpace(versionTag))
            {
                LogMessage?.Invoke(this, new EventArgs<string>("Release tag_name is null or empty."));
                return null;
            }

            // Extract version from tag (handle "release5.3.1" format)
            var rawVersionString = ExtractVersionFromTag(versionTag);
            if (string.IsNullOrEmpty(rawVersionString))
            {
                LogMessage?.Invoke(this, new EventArgs<string>($"Could not extract version from tag: '{versionTag}'"));
                return null;
            }

            // Validate that version has at least major.minor format
            var versionParts = rawVersionString.Split('.');
            if (versionParts.Length < 2)
            {
                LogMessage?.Invoke(this,
                    new EventArgs<string>(
                        $"Invalid version format: '{rawVersionString}'. Version must have at least major.minor components."));
                return null;
            }

            var normalizedVersion = NormalizeVersion(rawVersionString);
            var expectedAssetName = $"release_{rawVersionString}_{CurrentRuntimeIdentifier}.zip";

            LogMessage?.Invoke(this, new EventArgs<string>($"Searching for asset: {expectedAssetName}"));

            if (root.TryGetProperty("assets", out var assetsElement))
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    var assetName = asset.GetProperty("name").GetString();
                    if (assetName?.Equals(expectedAssetName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var assetUrl = asset.GetProperty("browser_download_url").GetString();
                        if (!string.IsNullOrEmpty(assetUrl))
                        {
                            LogMessage?.Invoke(this,
                                new EventArgs<string>($"Latest version found: {normalizedVersion}"));
                            LogMessage?.Invoke(this, new EventArgs<string>($"Release package URL: {assetUrl}"));
                            var fallbackAssetUrl = SecondaryServerBaseUrl +
                                                   $"release_{rawVersionString}_{CurrentRuntimeIdentifier}.zip";
                            return (normalizedVersion, assetUrl, fallbackAssetUrl);
                        }
                    }
                }
            }

            LogMessage?.Invoke(this,
                new EventArgs<string>(
                    $"Could not find the required asset '{expectedAssetName}' in the latest release."));
            return null;
        }
        catch (OperationCanceledException)
        {
            // UPD-19: a user cancel aborts immediately (no pointless secondary-server
            // attempt); a 5s-timeout OCE (user token not cancelled) falls through to
            // the silent secondary-server fallback below. Expected condition either
            // way — never a bug report.
            cancellationToken.ThrowIfCancellationRequested();
            Log.Information("GitHub request timed out after {Timeout} seconds", GitHubTimeoutSeconds);
            LogMessage?.Invoke(this,
                new EventArgs<string>($"GitHub request timed out after {GitHubTimeoutSeconds} seconds."));
            return null;
        }
        catch (Exception ex)
        {
            // Expected condition (network failure; the secondary-server fallback handles it): not a bug.
            Log.Information(ex, "Error fetching from GitHub");
            LogMessage?.Invoke(this, new EventArgs<string>($"Error fetching from GitHub: {ex.Message}"));
            return null;
        }
    }

    /// <summary>
    ///     Gets the release from the secondary server when GitHub is unavailable.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A tuple containing the normalized version string and the asset download URL.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the version file is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    private async Task<(string version, string assetUrl, string? fallbackAssetUrl)> GetFallbackReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogMessage?.Invoke(this, new EventArgs<string>("Checking secondary server for latest version..."));

            // The secondary server has a version.txt file with the current version
            const string versionUrl = SecondaryServerBaseUrl + "version.txt";

            // UPD-11: dispose the response (socket/stream leak otherwise).
            using var versionResponse = await _httpClient.GetAsync(versionUrl, cancellationToken);
            if (!versionResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to fetch version from secondary server. Status Code: {versionResponse.StatusCode}");
            }

            var versionText = (await versionResponse.Content.ReadAsStringAsync(cancellationToken)).Trim();

            // Remove "release" prefix if present
            var rawVersionString = ExtractVersionFromTag(versionText);
            if (string.IsNullOrEmpty(rawVersionString))
                throw new InvalidOperationException($"Invalid version format in version.txt: '{versionText}'");

            // Validate that version has at least major.minor format
            var versionParts = rawVersionString.Split('.');
            if (versionParts.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Invalid version format: '{rawVersionString}'. Version must have at least major.minor components.");
            }

            var normalizedVersion = NormalizeVersion(rawVersionString);
            var expectedAssetName = $"release_{rawVersionString}_{CurrentRuntimeIdentifier}.zip";
            var assetUrl = SecondaryServerBaseUrl + expectedAssetName;

            LogMessage?.Invoke(this, new EventArgs<string>($"Latest version found: {normalizedVersion}"));
            LogMessage?.Invoke(this, new EventArgs<string>($"Release package URL: {assetUrl}"));

            return (normalizedVersion, assetUrl, null);
        }
        catch (Exception ex)
        {
            // Both sources unreachable is an expected network condition (the caller falls back
            // to a manual update) — log at Information, not as a bug.
            Log.Information(ex, "Secondary server fallback failed");
            throw;
        }
    }

    /// <summary>
    ///     Extracts the version number from a tag or version string.
    ///     Handles formats like "release5.3.1", "v5.3.1", or just "5.3.1".
    /// </summary>
    /// <param name="tag">The tag or version string.</param>
    /// <returns>The extracted version string, or null if extraction failed.</returns>
    private static string? ExtractVersionFromTag(string tag)
    {
        // Try to match version pattern (digits separated by dots)
        var match = VersionRegex().Match(tag);
        if (match.Success) return match.Value;

        // Fallback: if tag starts with "release" or "v", try to extract after that
        var normalizedTag = tag.Trim().ToLowerInvariant();
        if (normalizedTag.StartsWith("release", StringComparison.Ordinal))
        {
            var versionPart = tag[7..]; // Remove "release" prefix
            match = VersionRegex().Match(versionPart);
            if (match.Success) return match.Value;
        }
        else if (normalizedTag.StartsWith('v'))
        {
            var versionPart = tag[1..]; // Remove "v" prefix
            match = VersionRegex().Match(versionPart);
            if (match.Success) return match.Value;
        }

        return null;
    }

    /// <summary>
    ///     Gets the GitHub releases page URL for manual downloads (uses the primary repository).
    /// </summary>
    public static string GetReleasesPageUrl()
    {
        return $"https://github.com/{RepoOwners[0]}/{RepoName}/releases/latest";
    }

    /// <summary>
    ///     Normalizes a version string to ensure it has exactly 4 version components (major.minor.build.revision).
    /// </summary>
    /// <param name="version">The version string to normalize.</param>
    /// <returns>A normalized version string with 4 components, or "0.0.0.0" if the input is null or empty.</returns>
    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return "0.0.0.0";

        var parts = new List<string>(version.Split('.'));
        while (parts.Count < 4) parts.Add("0");

        return string.Join(".", parts.Take(4));
    }

    [GeneratedRegex(@"(\d+(\.\d+){1,3})", RegexOptions.None | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex VersionRegex();
}
