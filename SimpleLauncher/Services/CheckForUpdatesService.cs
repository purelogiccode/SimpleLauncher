using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Services.QuitOrReinstall;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.Services;

/// <summary>
///     Checks for new application releases on GitHub and orchestrates the update process.
/// </summary>
public partial class CheckForUpdatesService
{
    private const string RepoName = "SimpleLauncher";

    private const string SecondaryServerBaseUrl =
        "https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/";

    private static readonly string[] RepoOwners = ["purelogiccode"];

    private static readonly char[] Separator = ['.'];

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly QuitSimpleLauncher _quitSimpleLauncher;
    private readonly IResourceProvider _resourceProvider;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CheckForUpdatesService" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create the HTTP client for GitHub API requests.</param>
    /// <param name="messageBoxLibrary">The message box service used to prompt the user about updates.</param>
    /// <param name="resourceProvider">The resource provider used to resolve localized strings.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="quitSimpleLauncher">The service used to shut down the application for an update.</param>
    /// <param name="serviceProvider">The dependency injection service provider.</param>
    public CheckForUpdatesService(IHttpClientFactory httpClientFactory, IMessageBoxLibraryService messageBoxLibrary,
        IResourceProvider resourceProvider, ILogger logger, QuitSimpleLauncher quitSimpleLauncher,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient("UpdateCheckerClient");
        _messageBoxLibrary = messageBoxLibrary;
        _resourceProvider = resourceProvider;
        _logger = logger;
        _quitSimpleLauncher = quitSimpleLauncher;
        _serviceProvider = serviceProvider;
    }

    private static string CurrentRuntimeIdentifier
    {
        get
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            return arch switch
            {
                Architecture.Arm64 => "win-arm64",
                Architecture.X64 => "win-x64",
                _ => throw new NotSupportedException(
                    $"Unsupported runtime architecture '{arch}'. Only win-x64 and win-arm64 are supported.")
            };
        }
    }

    private string CurrentVersion
    {
        get
        {
            try
            {
                return NormalizeVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex, "Error getting CurrentVersion");

                return _resourceProvider.GetString("UnknownString", "Unknown");
            }
        }
    }

    /// <summary>
    ///     Checks for updates silently in the background and shows the update window if a new version is available.
    /// </summary>
    /// <param name="mainWindow">The main application window used as the owner for update dialogs.</param>
    /// <returns>A task representing the asynchronous update check operation.</returns>
    internal async Task SilentCheckForUpdatesAsync(Window mainWindow)
    {
        try
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException(
                    "HttpClientFactory is not initialized. Update check cannot proceed.");
            }

            var (latestVersion, releasePackageUrl, updaterZipAssetUrl, fromFallback) =
                await GetLatestReleaseInfoAsync();

            if (latestVersion == null) return;

            if (IsNewVersionAvailable(CurrentVersion, latestVersion))
            {
                if (updaterZipAssetUrl != null || fromFallback)
                {
                    await ShowUpdateWindowAsync(releasePackageUrl, CurrentVersion, latestVersion, mainWindow);
                }
                else
                {
                    // Notify developer
                    var expectedUpdaterFileName = $"updater_{CurrentRuntimeIdentifier}.zip";
                    _logger.Error(
                        new FileNotFoundException(
                            $"'{expectedUpdaterFileName}' not found for version {latestVersion}. Automatic update of updater not possible.",
                            expectedUpdaterFileName), "Update Check Info");
                }
            }
        }
        catch (TaskCanceledException)
        {
            _logger.Debug("Silent update check canceled (network timeout or user canceled)");
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Silent update check canceled");
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error checking for updates (silent).";
            _logger.Error(ex, contextMessage);
        }
    }

    /// <summary>
    ///     Checks for updates manually and notifies the user whether an update is available or not.
    /// </summary>
    /// <param name="mainWindow">The main application window used as the owner for update dialogs.</param>
    /// <returns>A task representing the asynchronous update check operation.</returns>
    internal async Task ManualCheckForUpdatesAsync(Window mainWindow)
    {
        try
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException(
                    "HttpClientFactory is not initialized. Update check cannot proceed.");
            }

            var (latestVersion, releasePackageAssetUrl, updaterZipAssetUrl, fromFallback) =
                await GetLatestReleaseInfoAsync();

            if (latestVersion == null)
            {
                // Expected condition (both sources unreachable / offline); the user is
                // already notified via the message box below — not a bug report.
                _logger.Information(
                    "Could not determine the latest version (GitHub and the secondary server are unreachable)");
                await _messageBoxLibrary.ErrorCheckingForUpdatesMessageBoxAsync();
                return;
            }

            if (IsNewVersionAvailable(CurrentVersion, latestVersion))
            {
                if (updaterZipAssetUrl != null || fromFallback)
                {
                    await ShowUpdateWindowAsync(releasePackageAssetUrl, CurrentVersion, latestVersion, mainWindow);
                }
                else
                {
                    var expectedUpdaterFileName = $"updater_{CurrentRuntimeIdentifier}.zip";
                    var message =
                        $"A new version ({latestVersion}) is available, but the required '{expectedUpdaterFileName}' for automatic updater update was not found. ";
                    message += releasePackageAssetUrl != null
                        ? $"You can try to download the main package '{Path.GetFileName(releasePackageAssetUrl)}' manually from the releases page."
                        : "The main release package was also not found. Please check the GitHub releases page.";

                    // Notify developer
                    _logger.Error(new FileNotFoundException(message, expectedUpdaterFileName), "Update Process Info");

                    // Notify user
                    await _messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
                }
            }
            else
            {
                // Notify user
                await _messageBoxLibrary.ThereIsNoUpdateAvailableMessageBoxAsync(CurrentVersion);
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error checking for updates (variant).";
            _logger.Error(ex, contextMessage);

            // Notify user
            await _messageBoxLibrary.ErrorCheckingForUpdatesMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Retrieves the download URL of the latest updater package and the latest available version.
    /// </summary>
    /// <returns>A tuple containing the updater ZIP URL and the latest version, or null values if unavailable.</returns>
    internal async Task<(string? UpdaterZipUrl, string? LatestVersion)> GetLatestUpdaterInfoAsync()
    {
        try
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException(
                    "HttpClientFactory is not initialized. Update check cannot proceed.");
            }

            var (latestVersion, _, updaterZipAssetUrl, _) = await GetLatestReleaseInfoAsync();
            return (updaterZipAssetUrl, latestVersion);
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error fetching latest updater info");
            return (null, null);
        }
    }

    /// <summary>
    ///     Gets the latest release info from the GitHub API (trying each repository in order),
    ///     falling back to the secondary server (assets.purelogiccode.com) when GitHub is
    ///     unreachable. The secondary server hosts the release package and the updater
    ///     package (updater_{rid}.zip).
    /// </summary>
    /// <returns>A tuple with the latest version, release package URL, updater zip URL, and whether the fallback was used.</returns>
    private async
        Task<(string? latestVersion, string? releasePackageUrl, string? updaterZipAssetUrl, bool fromFallback)>
        GetLatestReleaseInfoAsync()
    {
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "request");

        foreach (var repoOwner in RepoOwners)
        {
            try
            {
                var response =
                    await _httpClient.GetAsync($"https://api.github.com/repos/{repoOwner}/{RepoName}/releases/latest");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Debug(
                        $"[UpdateChecker] GitHub API check for '{repoOwner}/{RepoName}' failed with status {response.StatusCode}; trying the next source.");
                    continue;
                }

                _logger.Debug("Check for Updates Success");

                var content = await response.Content.ReadAsStringAsync();
                var (latestVersion, releasePackageUrl, updaterZipAssetUrl) =
                    ParseVersionAndAssetUrlsFromResponse(content);
                if (latestVersion is null)
                {
                    _logger.Debug(
                        $"[UpdateChecker] GitHub API response from '{repoOwner}/{RepoName}' could not be parsed; trying the next source.");
                    continue;
                }

                return (latestVersion, releasePackageUrl, updaterZipAssetUrl, false);
            }
            catch (Exception ex)
            {
                _logger.Debug(
                    $"[UpdateChecker] GitHub API check for '{repoOwner}/{RepoName}' failed: {ex.Message}; trying the next source.");
            }
        }

        // Fallback: the secondary server hosts a version.txt file and the release packages.
        try
        {
            var versionResponse = await _httpClient.GetAsync(SecondaryServerBaseUrl + "version.txt");
            if (!versionResponse.IsSuccessStatusCode)
            {
                _logger.Debug(
                    $"[UpdateChecker] Secondary server check failed with status {versionResponse.StatusCode}.");
                return (null, null, null, false);
            }

            var versionText = (await versionResponse.Content.ReadAsStringAsync()).Trim();
            var versionMatch = MyRegex2().Match(versionText);
            if (!versionMatch.Success)
            {
                _logger.Debug($"[UpdateChecker] Secondary server version.txt has no valid version: '{versionText}'.");
                return (null, null, null, false);
            }

            var rawVersion = versionMatch.Value;
            var latestVersion = NormalizeVersion(rawVersion);
            var releasePackageUrl = SecondaryServerBaseUrl + $"release_{rawVersion}_{CurrentRuntimeIdentifier}.zip";
            var updaterZipAssetUrl = SecondaryServerBaseUrl + $"updater_{CurrentRuntimeIdentifier}.zip";

            _logger.Information("GitHub API unavailable. Using the secondary server: version {LatestVersion}",
                latestVersion);
            return (latestVersion, releasePackageUrl, updaterZipAssetUrl, true);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[UpdateChecker] Secondary server check failed: {ex.Message}.");
            return (null, null, null, false);
        }
    }

    private async Task ShowUpdateWindowAsync(string? releasePackageUrl, string currentVersion, string latestVersion,
        Window owner)
    {
        UpdateLogWindow? logWindow = null;

        try
        {
            var result = await _messageBoxLibrary.DoYouWantToUpdateMessageBoxAsync(currentVersion, latestVersion);
            if (result != CoreMessageBoxResult.Yes) return;

            logWindow = _serviceProvider.GetRequiredService<UpdateLogWindow>();
            logWindow.Show();
            logWindow.Log("Starting update process...");

            owner?.Hide();

            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var updaterExePath = Path.Combine(appDirectory, "Updater.exe");

            logWindow.Log("Launching Updater.exe (auto-downloads from GitHub if needed)...");
            await Task.Delay(500);
            await _quitSimpleLauncher.ShutdownForUpdateAsync(updaterExePath, _messageBoxLibrary);
            // If we reach here, ShutdownForUpdateAsync returned without killing the process
            // (the update failed — an error was already shown to the user)
            logWindow.Log("Updater.exe launch failed.");
            if (!string.IsNullOrEmpty(releasePackageUrl))
            {
                logWindow.Log($"Please download the update package manually from: {releasePackageUrl}");
            }
            else
            {
                logWindow.Log(
                    $"The update package URL was not found. Please visit the GitHub releases page for {RepoOwners[0]}/{RepoName}.");
            }
        }
        catch (Exception ex)
        {
            const string contextMessage = "There was an error preparing for the application update.";
            _logger.Error(ex, contextMessage);
            logWindow?.Log($"An unexpected error occurred during the update process: {ex.Message}");
            await _messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
        }
        finally
        {
            // This finally block will now only be reached if the update process fails before shutdown.
            logWindow?.Close();
            owner?.Show();
        }
    }

    /// <summary>
    ///     Downloads the update file from the given URL into the provided memory stream.
    /// </summary>
    /// <param name="url">The URL of the update file to download.</param>
    /// <param name="memoryStream">The memory stream that receives the downloaded file content.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    internal async Task DownloadUpdateFileToMemoryAsync(string url, MemoryStream memoryStream)
    {
        if (_httpClient == null)
            throw new InvalidOperationException("HttpClientFactory is not initialized. Cannot download update file.");

        // Use the pre-initialized HttpClient instance
        if (_httpClient != null)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(memoryStream);
        }

        memoryStream.Position = 0;
    }

    /// <summary>
    ///     Extracts all entries of a ZIP archive from a memory stream into the destination path, protecting against path
    ///     traversal attacks.
    /// </summary>
    /// <param name="zipStream">The memory stream containing the ZIP archive.</param>
    /// <param name="destinationPath">The directory where the archive entries are extracted.</param>
    /// <param name="logWindow">The update log window used to report extraction progress, or null.</param>
    /// <param name="logErrors">The logger used to record extraction failures.</param>
    /// <returns>True if the archive was extracted successfully, false otherwise.</returns>
    internal static bool ExtractAllFromZip(MemoryStream zipStream, string destinationPath, UpdateLogWindow? logWindow,
        ILogger logErrors)
    {
        try
        {
            zipStream.Position = 0;

            // Ensure destination directory exists
            if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath);

            var fullDestinationPath = Path.GetFullPath(destinationPath);
            if (!fullDestinationPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                fullDestinationPath += Path.DirectorySeparatorChar;

            using var archive = ZipArchive.OpenArchive(zipStream);
            var hasEntries = false;

            foreach (var entry in archive.Entries)
            {
                hasEntries = true;

                if (entry.IsDirectory) continue;

                // Security check: prevent path traversal attacks (zip slip)
                if (entry.Key != null)
                {
                    var destinationFileFullPath = Path.GetFullPath(Path.Combine(fullDestinationPath, entry.Key));
                    if (!destinationFileFullPath.StartsWith(fullDestinationPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var errorMessage =
                            $"Security Warning: Path traversal attempt detected for entry '{entry.Key}'. Aborting update.";
                        logWindow?.Log(errorMessage);

                        // Notify developer
                        logErrors.Error(new SecurityException("Zip Slip vulnerability detected in update package."),
                            errorMessage);
                        return false;
                    }

                    // Ensure the directory exists
                    var entryDirectory = Path.GetDirectoryName(destinationFileFullPath);
                    if (!string.IsNullOrEmpty(entryDirectory) && !Directory.Exists(entryDirectory))
                        Directory.CreateDirectory(entryDirectory);

                    // Extract the entry
                    using (var entryStream = entry.OpenEntryStream())
                    using (var fileStream = File.Create(destinationFileFullPath))
                    {
                        entryStream.CopyTo(fileStream);
                    }

                    // Preserve file time if available
                    if (entry.LastModifiedTime.HasValue)
                        File.SetLastWriteTime(destinationFileFullPath, entry.LastModifiedTime.Value);
                }
            }

            if (!hasEntries)
            {
                logWindow?.Log("Warning: The downloaded ZIP archive is empty or corrupted.");
                return false;
            }

            logWindow?.Log("All files from the updater package extracted successfully.");
            return true;
        }
        catch (Exception ex)
        {
            // Notify developer
            logErrors.Error(ex, "Error processing the update ZIP archive");
            logWindow?.Log($"Failed to process the update ZIP archive. Error: {ex.Message}");

            return false;
        }
    }

    private bool IsNewVersionAvailable(string currentVersion, string latestVersion)
    {
        try
        {
            if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(latestVersion))
            {
                // Notify developer
                _logger.Error(
                    new ArgumentException(@"Current or latest version string is null or empty.",
                        nameof(currentVersion)), "Invalid version string for comparison");
                return false;
            }

            var currentNormalized = MyRegex1().Replace(currentVersion, "");
            var latestNormalized = MyRegex1().Replace(latestVersion, "");

            if (string.IsNullOrEmpty(currentNormalized) || string.IsNullOrEmpty(latestNormalized))
            {
                // Notify developer
                _logger.Error(
                    new ArgumentException(@"Normalized version string is null or empty after regex replace.",
                        nameof(latestVersion)), "Invalid version string after normalization");
                return false;
            }

            var current = new Version(currentNormalized);
            var latest = new Version(latestNormalized);
            return latest.CompareTo(current) > 0;
        }
        catch (ArgumentException ex)
        {
            if (currentVersion == null) return false;

            if (latestVersion != null)
            {
                // Notify developer
                _logger.Error(ex,
                    $"Invalid version number format after normalization. Current: '{currentVersion}' (Normalized: '{MyRegex1().Replace(currentVersion, "")}'), Latest: '{latestVersion}' (Normalized: '{MyRegex1().Replace(latestVersion, "")}').");
            }

            return false;
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Unexpected error in IsNewVersionAvailable");
            return false;
        }
    }

    private (string? version, string? releasePackageUrl, string? updaterZipUrl) ParseVersionAndAssetUrlsFromResponse(
        string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            string? versionTag;
            if (root.TryGetProperty("tag_name", out var tagNameElement))
            {
                versionTag = tagNameElement.GetString();
            }
            else
            {
                // Notify developer
                _logger.Error(new KeyNotFoundException("'tag_name' not found in GitHub API response."),
                    "GitHub API Response Error");
                return (null, null, null);
            }

            string? rawVersionStringFromTag = null;
            string? extractedNormalizedVersion = null;

            if (!string.IsNullOrEmpty(versionTag))
            {
                var versionMatch = MyRegex2().Match(versionTag);
                if (versionMatch.Success)
                {
                    rawVersionStringFromTag = versionMatch.Value;
                    extractedNormalizedVersion = NormalizeVersion(rawVersionStringFromTag);
                }
            }

            if (extractedNormalizedVersion == null)
            {
                // Notify developer
                _logger.Error(
                    new FormatException(
                        $"Could not extract or normalize a valid version from tag_name: '{versionTag}'."),
                    "GitHub API Response Error");
                return (null, null, null);
            }

            string? foundReleasePackageUrl = null;
            string? foundUpdaterZipUrl = null;

            var runtimeIdentifier = CurrentRuntimeIdentifier;
            var expectedReleaseFileName = $"release_{rawVersionStringFromTag}_{runtimeIdentifier}.zip";
            var expectedUpdaterFileName = $"updater_{runtimeIdentifier}.zip";

            _logger.Debug($"Searching for assets: '{expectedReleaseFileName}' and '{expectedUpdaterFileName}'");

            if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameElement))
                    {
                        var assetName = nameElement.GetString();
                        if (assetName?.Equals(expectedUpdaterFileName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (asset.TryGetProperty("browser_download_url", out var downloadUrlElement))
                                foundUpdaterZipUrl = downloadUrlElement.GetString();
                        }
                        else if (assetName?.Equals(expectedReleaseFileName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (asset.TryGetProperty("browser_download_url", out var downloadUrlElement))
                                foundReleasePackageUrl = downloadUrlElement.GetString();
                        }
                    }

                    if (foundUpdaterZipUrl != null && foundReleasePackageUrl != null) break;
                }

                if (foundUpdaterZipUrl == null)
                {
                    // Notify developer
                    _logger.Error(
                        new FileNotFoundException(
                            $"'{expectedUpdaterFileName}' asset not found in release '{versionTag}'.",
                            expectedUpdaterFileName), "GitHub API Asset Info");
                }

                if (foundReleasePackageUrl == null)
                {
                    // Notify developer
                    _logger.Error(
                        new FileNotFoundException(
                            $"Expected release package '{expectedReleaseFileName}' not found in release '{versionTag}'.",
                            expectedReleaseFileName), "GitHub API Asset Info");
                }

                return (extractedNormalizedVersion, foundReleasePackageUrl, foundUpdaterZipUrl);
            }

            // Notify developer
            _logger.Error(new KeyNotFoundException("'assets' array not found or invalid in GitHub API response."),
                "GitHub API Response Error");
        }
        catch (JsonException jsonEx)
        {
            // Notify developer
            _logger.Error(jsonEx, "Failed to parse JSON response from GitHub API");
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Unexpected error in ParseVersionAndAssetUrlsFromResponse");
        }

        return (null, null, null);
    }

    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrEmpty(version)) return "0.0.0.0";

        var numericVersion = MyRegex1().Replace(version, "");
        numericVersion = MyRegex().Replace(numericVersion, ".").Trim('.');
        if (string.IsNullOrEmpty(numericVersion)) return "0.0.0.0";

        var parts = new List<string>(numericVersion.Split(Separator, StringSplitOptions.RemoveEmptyEntries));

        while (parts.Count < 4) parts.Add("0");

        if (parts.Count > 4) parts = parts.GetRange(0, 4);

        return string.Join(".", parts);
    }

    [GeneratedRegex(@"[^\d\.]", RegexOptions.None, 1000)]
    private static partial Regex MyRegex1();

    [GeneratedRegex(@"(\d+(\.\d+){1,3})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex MyRegex2();

    [GeneratedRegex(@"\.{2,}", RegexOptions.None, 1000)]
    private static partial Regex MyRegex();
}