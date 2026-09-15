using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services.AvaloniaServices;
using SimpleLauncher.Core.Interfaces;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using IApplicationLifetime = SimpleLauncher.Core.Interfaces.IApplicationLifetime;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Checks for new application releases on GitHub, falling back to the secondary
///     server when GitHub is unreachable. Avalonia port of the WPF CheckForUpdatesService.
///     When the user accepts an update, the Avalonia updater
///     (SimpleLauncher.Avalonia.Updater) is re-downloaded from the release assets
///     (falling back to the local copy when offline) and launched; the application
///     then shuts down.
/// </summary>
public partial class AvaloniaCheckForUpdatesService
{
    private const string RepoName = "SimpleLauncher";

    private const string SecondaryServerBaseUrl =
        "https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/";

    private const string UpdaterFileName = "SimpleLauncher.Avalonia.Updater";

    /// <summary>
    ///     Distinguishes the Avalonia release assets from the WPF ones. Both apps are
    ///     attached to the same GitHub release, so the package names must not collide.
    /// </summary>
    private const string AssetPrefix = "avalonia_";

    private static readonly string[] RepoOwners = ["purelogiccode"];
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly string _updaterDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AvaloniaCheckForUpdatesService" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create the HTTP client for GitHub API requests.</param>
    /// <param name="messageBoxLibrary">The message box service used to prompt the user about updates.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="applicationLifetime">The application lifetime used to shut down the app after launching the updater.</param>
    public AvaloniaCheckForUpdatesService(IHttpClientFactory httpClientFactory,
        IMessageBoxLibraryService messageBoxLibrary, ILogger logger, IApplicationLifetime applicationLifetime)
        : this(httpClientFactory, messageBoxLibrary, logger, applicationLifetime, AppDomain.CurrentDomain.BaseDirectory)
    {
    }

    /// <summary>
    ///     Test seam: resolves the updater against an isolated directory so tests never
    ///     touch (or launch) the real updater shipped in the application output.
    /// </summary>
    internal AvaloniaCheckForUpdatesService(IHttpClientFactory httpClientFactory,
        IMessageBoxLibraryService messageBoxLibrary, ILogger logger, IApplicationLifetime applicationLifetime,
        string updaterDirectory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient("UpdateCheckerClient");
        _messageBoxLibrary = messageBoxLibrary;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
        _updaterDirectory = updaterDirectory;
    }

    /// <summary>
    ///     Name of the updater executable shipped next to the application.
    /// </summary>
    internal static string UpdaterExecutableName => OperatingSystem.IsWindows()
        ? $"{UpdaterFileName}.exe"
        : UpdaterFileName;

    /// <summary>
    ///     Gets the current runtime identifier used for release/updater asset names,
    ///     mirroring the updater's GitHubService (win-x64/win-arm64/linux-x64/linux-arm64).
    /// </summary>
    internal static string CurrentRuntimeIdentifier
    {
        get
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            if (OperatingSystem.IsWindows()) return arch == Architecture.Arm64 ? "win-arm64" : "win-x64";

            return arch == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }
    }

    /// <summary>
    ///     Name of the release package asset for a raw version and runtime identifier,
    ///     e.g. release_avalonia_5.7.0_win-x64.zip.
    /// </summary>
    internal static string ReleaseAssetName(string rawVersion, string rid)
    {
        return $"release_{AssetPrefix}{rawVersion}_{rid}.zip";
    }

    /// <summary>
    ///     Name of the updater package asset for a runtime identifier,
    ///     e.g. updater_avalonia_win-x64.zip.
    /// </summary>
    internal static string UpdaterAssetName(string rid)
    {
        return $"updater_{AssetPrefix}{rid}.zip";
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
                _logger.Error(ex, "Error getting CurrentVersion.");
                return "0.0.0.0";
            }
        }
    }

    /// <summary>
    ///     Checks for updates silently and prompts the user if an update is available.
    ///     WPF parity: always shows the update window when a newer version is found.
    /// </summary>
    /// <returns>A task representing the asynchronous update check operation.</returns>
    public async Task SilentCheckForUpdatesAsync()
    {
        try
        {
            var (latestVersion, releasePackageUrl, updaterZipAssetUrl, _) = await GetLatestReleaseInfoAsync();

            if (latestVersion == null)
            {
                _logger.Information(
                    "Silent update check: could not determine the latest version (GitHub and the secondary server are unreachable).");
                return;
            }

            if (!IsNewVersionAvailable(CurrentVersion, latestVersion))
            {
                _logger.Information(
                    "Silent update check: no update available (current {CurrentVersion}, latest {LatestVersion}).",
                    CurrentVersion, latestVersion);
                return;
            }

            _logger.Information("Silent update check: update {LatestVersion} available (current {CurrentVersion}).",
                latestVersion, CurrentVersion);

            // WPF parity: prompt the user directly instead of just raising an event
            var result = await _messageBoxLibrary.DoYouWantToUpdateMessageBoxAsync(CurrentVersion, latestVersion);
            if (result == CoreMessageBoxResult.Yes)
            {
                _logger.Information("Update to {LatestVersion} confirmed by user; launching the updater.",
                    latestVersion);
                await ShowUpdateWindowAsync(releasePackageUrl, updaterZipAssetUrl, TryGetMainWindow());
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for updates (silent).");
        }
    }

    /// <summary>
    ///     Checks for updates manually and notifies the user whether an update is available or not.
    /// </summary>
    /// <param name="owner">The owner window for update dialogs (may be null).</param>
    /// <returns>A task representing the asynchronous update check operation.</returns>
    internal async Task ManualCheckForUpdatesAsync(Window? owner)
    {
        try
        {
            var (latestVersion, releasePackageUrl, updaterZipAssetUrl, _) = await GetLatestReleaseInfoAsync();

            if (latestVersion == null)
            {
                // Expected condition (both sources unreachable / offline); the user is
                // already notified via the message box below — not a bug report.
                _logger.Information(
                    "Could not determine the latest version (GitHub and the secondary server are unreachable).");
                await _messageBoxLibrary.ErrorCheckingForUpdatesMessageBoxAsync();
                return;
            }

            if (IsNewVersionAvailable(CurrentVersion, latestVersion))
            {
                var result = await _messageBoxLibrary.DoYouWantToUpdateMessageBoxAsync(CurrentVersion, latestVersion);
                if (result == CoreMessageBoxResult.Yes)
                {
                    _logger.Information("Update to {LatestVersion} confirmed by user; launching the updater.",
                        latestVersion);
                    await ShowUpdateWindowAsync(releasePackageUrl, updaterZipAssetUrl, owner);
                }
            }
            else
            {
                await _messageBoxLibrary.ThereIsNoUpdateAvailableMessageBoxAsync(CurrentVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for updates (manual).");
            await _messageBoxLibrary.ErrorCheckingForUpdatesMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Launches the updater (downloading it from the release assets first when it is
    ///     not shipped next to the app) and shuts the application down. Port of the WPF
    ///     ReinstallSimpleLauncher flow — the updater replaces the app files while the
    ///     application process is exiting, then restarts it.
    /// </summary>
    /// <param name="updaterZipAssetUrl">URL of the updater package, or null when unknown.</param>
    public async Task ReinstallAndShutdownAsync(string? updaterZipAssetUrl = null)
    {
        if (string.IsNullOrWhiteSpace(updaterZipAssetUrl))
        {
            try
            {
                var (_, _, foundUpdaterUrl, _) = await GetLatestReleaseInfoAsync();
                updaterZipAssetUrl = foundUpdaterUrl;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to resolve the updater package URL for reinstall.");
            }
        }

        await LaunchUpdaterAndShutdownAsync(updaterZipAssetUrl);
    }

    private async Task LaunchUpdaterAndShutdownAsync(string? updaterZipAssetUrl)
    {
        var updaterPath = Path.Combine(_updaterDirectory, UpdaterExecutableName);

        try
        {
            // WPF parity (QuitSimpleLauncher.ShutdownForUpdateAsync): always download a
            // fresh updater when a URL is known — overwriting any existing copy — and fall
            // back to the local copy only when the download fails or no URL is available.
            if (!string.IsNullOrWhiteSpace(updaterZipAssetUrl))
            {
                _logger.Information("Downloading a fresh updater package from the release assets.");
                if (!await DownloadAndExtractUpdaterAsync(updaterZipAssetUrl, _updaterDirectory))
                    _logger.Information("Updater download failed; falling back to the local copy.");
            }

            if (!File.Exists(updaterPath))
            {
                // Expected condition (offline / missing asset); the user is already
                // notified via the message box below — not a bug report.
                _logger.Information("Could not obtain the updater package; guiding the user to a manual update.");
                await _messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo(updaterPath)
                {
                    Arguments = Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                    UseShellExecute = true,
                    WorkingDirectory = _updaterDirectory
                };
                Process.Start(startInfo);

                _logger.Information("Updater launched (PID {ProcessId}); shutting down for the update.",
                    Environment.ProcessId);
                _applicationLifetime.Shutdown();
            }
            catch (Exception ex)
            {
                // Expected condition (broken/corrupt updater, access denied); the user is
                // already notified via the message box below — not a bug report.
                _logger.Information(ex, "Failed to launch the updater at {UpdaterPath}", updaterPath);
                await _messageBoxLibrary.UpdaterLaunchFailedMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to prepare the updater.");
            await _messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
        }
    }

    /// <summary>
    ///     WPF parity (<c>CheckForUpdatesService.ShowUpdateWindowAsync</c>): shows the
    ///     update log window, hides the owner window while the updater runs, and logs
    ///     manual-download guidance when the updater fails to launch. On success the
    ///     process exits, so the finally block only runs on failure paths.
    /// </summary>
    private async Task ShowUpdateWindowAsync(string? releasePackageUrl, string? updaterZipAssetUrl, Window? owner)
    {
        UpdateLogWindow? logWindow = null;

        try
        {
            logWindow = TryResolveUpdateLogWindow();
            if (logWindow is not null)
            {
                await RunOnUiThreadAsync(logWindow.Show);
                // AV-25: awaited — burst callers keep ordering and observe failures.
                await logWindow.LogAsync("Starting update process...");
            }

            if (owner is not null)
                await RunOnUiThreadAsync(owner.Hide);

            if (logWindow is not null)
                await logWindow.LogAsync(
                    $"Launching {UpdaterExecutableName} (auto-downloads from the release assets if needed)...");

            // Give the log window a moment to paint before the process exits (WPF parity).
            if (logWindow is not null) await Task.Delay(500);

            await LaunchUpdaterAndShutdownAsync(updaterZipAssetUrl);

            // If we reach here, LaunchUpdaterAndShutdownAsync returned without shutting the
            // application down (the update failed — an error was already shown to the user).
            if (logWindow is not null)
            {
                await logWindow.LogAsync("Updater launch failed.");
                if (!string.IsNullOrEmpty(releasePackageUrl))
                {
                    await logWindow.LogAsync(
                        $"Please download the update package manually from: {releasePackageUrl}");
                }
                else
                {
                    await logWindow.LogAsync(
                        $"The update package URL was not found. Please visit the GitHub releases page for {RepoOwners[0]}/{RepoName}.");
                }
            }
        }
        catch (Exception ex)
        {
            const string contextMessage = "There was an error preparing for the application update.";
            _logger.Error(ex, contextMessage);
            if (logWindow is not null)
                await logWindow.LogAsync($"An unexpected error occurred during the update process: {ex.Message}");
            await _messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
        }
        finally
        {
            // Failure paths only — a successful launch shuts the process down before this runs.
            if (logWindow is not null) await RunOnUiThreadAsync(logWindow.Close);
            if (owner is not null) await RunOnUiThreadAsync(owner.Show);
        }
    }

    /// <summary>
    ///     Resolves the update log window from the static service provider (same pattern
    ///     as <see cref="MessageBoxLibraryService" />). Returns null when the container is
    ///     not available (unit tests) — the log window is optional UX, never a hard
    ///     dependency of the update flow.
    /// </summary>
    private UpdateLogWindow? TryResolveUpdateLogWindow()
    {
        try
        {
            return App.ServiceProvider?.GetService<UpdateLogWindow>();
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Update log window unavailable; continuing the update without it.");
            return null;
        }
    }

    private static Window? TryGetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private static async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    ///     Downloads and extracts the updater package into the application directory,
    ///     guarding against zip-slip path traversal. Expected network/IO failures are
    ///     logged at Information level (the caller falls back to a manual update).
    /// </summary>
    private async Task<bool> DownloadAndExtractUpdaterAsync(string url, string destinationPath)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var fullDestinationPath = Path.GetFullPath(destinationPath);
            if (!fullDestinationPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                fullDestinationPath += Path.DirectorySeparatorChar;

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

                var destinationFileFullPath = Path.GetFullPath(Path.Combine(fullDestinationPath, entry.FullName));
                if (!destinationFileFullPath.StartsWith(fullDestinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Information(
                        "Security warning: path traversal attempt in updater package entry '{Entry}'. Aborting.",
                        entry.FullName);
                    return false;
                }

                var entryDirectory = Path.GetDirectoryName(destinationFileFullPath);
                if (!string.IsNullOrEmpty(entryDirectory) && !Directory.Exists(entryDirectory))
                    Directory.CreateDirectory(entryDirectory);

                entry.ExtractToFile(destinationFileFullPath, true);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "Failed to download or extract the updater package.");
            return false;
        }
    }

    /// <summary>
    ///     Gets the latest release info from the GitHub API (trying each repository in order),
    ///     falling back to the secondary server (assets.purelogiccode.com) when GitHub is
    ///     unreachable.
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
            var versionMatch = VersionRegex().Match(versionText);
            if (!versionMatch.Success)
            {
                _logger.Debug($"[UpdateChecker] Secondary server version.txt has no valid version: '{versionText}'.");
                return (null, null, null, false);
            }

            var rawVersion = versionMatch.Value;
            var latestVersion = NormalizeVersion(rawVersion);
            var releasePackageUrl = SecondaryServerBaseUrl + ReleaseAssetName(rawVersion, CurrentRuntimeIdentifier);
            var updaterZipAssetUrl = SecondaryServerBaseUrl + UpdaterAssetName(CurrentRuntimeIdentifier);

            _logger.Information("GitHub API unavailable. Using the secondary server: version {LatestVersion}.",
                latestVersion);
            return (latestVersion, releasePackageUrl, updaterZipAssetUrl, true);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[UpdateChecker] Secondary server check failed: {ex.Message}.");
            return (null, null, null, false);
        }
    }

    private bool IsNewVersionAvailable(string currentVersion, string latestVersion)
    {
        try
        {
            if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(latestVersion))
            {
                _logger.Error(
                    new ArgumentException("Current or latest version string is null or empty.", nameof(currentVersion)),
                    "Invalid version string for comparison.");
                return false;
            }

            var currentNormalized = NonNumericRegex().Replace(currentVersion, "");
            var latestNormalized = NonNumericRegex().Replace(latestVersion, "");

            if (string.IsNullOrEmpty(currentNormalized) || string.IsNullOrEmpty(latestNormalized))
            {
                _logger.Error(
                    new ArgumentException("Normalized version string is null or empty after regex replace.",
                        nameof(latestVersion)), "Invalid version string after normalization.");
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
                _logger.Error(ex,
                    $"Invalid version number format after normalization. Current: '{currentVersion}', Latest: '{latestVersion}'.");
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error in IsNewVersionAvailable.");
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

            if (!root.TryGetProperty("tag_name", out var tagNameElement))
            {
                _logger.Error(new KeyNotFoundException("'tag_name' not found in GitHub API response."),
                    "GitHub API Response Error");
                return (null, null, null);
            }

            var versionTag = tagNameElement.GetString();
            string? rawVersionStringFromTag = null;
            string? extractedNormalizedVersion = null;

            if (!string.IsNullOrEmpty(versionTag))
            {
                var versionMatch = VersionRegex().Match(versionTag);
                if (versionMatch.Success)
                {
                    rawVersionStringFromTag = versionMatch.Value;
                    extractedNormalizedVersion = NormalizeVersion(rawVersionStringFromTag);
                }
            }

            if (extractedNormalizedVersion == null)
            {
                _logger.Error(
                    new FormatException(
                        $"Could not extract or normalize a valid version from tag_name: '{versionTag}'."),
                    "GitHub API Response Error");
                return (null, null, null);
            }

            string? foundReleasePackageUrl = null;
            string? foundUpdaterZipUrl = null;

            var expectedUpdaterFileName = UpdaterAssetName(CurrentRuntimeIdentifier);
            var expectedReleaseFileName = ReleaseAssetName(rawVersionStringFromTag!, CurrentRuntimeIdentifier);

            _logger.Debug($"Searching for assets: '{expectedReleaseFileName}' and '{expectedUpdaterFileName}'");

            if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameElement)) continue;

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

                    if (foundUpdaterZipUrl != null && foundReleasePackageUrl != null) break;
                }

                if (foundUpdaterZipUrl == null)
                {
                    _logger.Error(
                        new FileNotFoundException(
                            $"'{expectedUpdaterFileName}' asset not found in release '{versionTag}'.",
                            expectedUpdaterFileName), "GitHub API Asset Info");
                }

                if (foundReleasePackageUrl == null)
                {
                    _logger.Error(
                        new FileNotFoundException(
                            $"Expected release package '{expectedReleaseFileName}' not found in release '{versionTag}'.",
                            expectedReleaseFileName), "GitHub API Asset Info");
                }

                return (extractedNormalizedVersion, foundReleasePackageUrl, foundUpdaterZipUrl);
            }

            _logger.Error(new KeyNotFoundException("'assets' array not found or invalid in GitHub API response."),
                "GitHub API Response Error");
        }
        catch (JsonException jsonEx)
        {
            _logger.Error(jsonEx, "Failed to parse JSON response from GitHub API.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error in ParseVersionAndAssetUrlsFromResponse.");
        }

        return (null, null, null);
    }

    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrEmpty(version)) return "0.0.0.0";

        var numericVersion = NonNumericRegex().Replace(version, "");
        numericVersion = MultiDotRegex().Replace(numericVersion, ".").Trim('.');
        if (string.IsNullOrEmpty(numericVersion)) return "0.0.0.0";

        var parts = new List<string>(numericVersion.Split('.', StringSplitOptions.RemoveEmptyEntries));

        while (parts.Count < 4) parts.Add("0");

        if (parts.Count > 4) parts = parts.GetRange(0, 4);

        return string.Join(".", parts);
    }

    [GeneratedRegex(@"[^\d\.]", RegexOptions.None, 1000)]
    private static partial Regex NonNumericRegex();

    [GeneratedRegex(@"(\d+(\.\d+){1,3})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"\.{2,}", RegexOptions.None, 1000)]
    private static partial Regex MultiDotRegex();
}
