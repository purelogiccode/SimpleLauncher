using System.Net.NetworkInformation;

namespace SimpleLauncher.Avalonia.Updater.Services;

/// <summary>
///     Service that orchestrates the entire update process using other specialized services.
/// </summary>
internal class UpdateService
{
    private readonly string _appDirectory;
    private readonly DokanService _dokanService;
    private readonly DownloadService _downloadService;
    private readonly GitHubService _gitHubService;
    private readonly ProcessService _processService;
    private readonly ZipService _zipService;

    /// <summary>
    ///     Initializes a new instance of the UpdateService class.
    /// </summary>
    public UpdateService(
        GitHubService gitHubService,
        DownloadService downloadService,
        ZipService zipService,
        ProcessService processService,
        DokanService dokanService,
        string appDirectory)
    {
        _gitHubService = gitHubService;
        _downloadService = downloadService;
        _zipService = zipService;
        _processService = processService;
        _dokanService = dokanService;
        _appDirectory = appDirectory;

        // Wire up service events
        _gitHubService.LogMessage += (_, e) => LogMessage?.Invoke(this, e);
        _downloadService.LogMessage += (_, e) => LogMessage?.Invoke(this, e);
        _downloadService.ProgressChanged += OnDownloadProgressChanged;
        _zipService.LogMessage += (_, e) => LogMessage?.Invoke(this, e);
        _zipService.ProgressChanged += OnExtractionProgressChanged;
        _processService.LogMessage += (_, e) => LogMessage?.Invoke(this, e);
        _dokanService.LogMessage += (_, e) => LogMessage?.Invoke(this, e);
        _dokanService.ProgressChanged += OnDownloadProgressChanged;
    }

    /// <summary>
    ///     Event raised when Dokan is not installed and the user should be prompted.
    ///     Returns true if the user wants to install Dokan, false otherwise.
    /// </summary>
    public Func<Task<bool>>? DokanInstallationPrompt { get; set; }

    /// <summary>
    ///     Event raised when a log message should be displayed.
    /// </summary>
    public event EventHandler<EventArgs<string>>? LogMessage;

    /// <summary>
    ///     Event raised when download progress changes.
    /// </summary>
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;

    /// <summary>
    ///     Event raised when download progress should be reset.
    /// </summary>
    public event EventHandler? DownloadProgressReset;

    /// <summary>
    ///     Event raised when extraction progress changes.
    /// </summary>
    public event EventHandler<ExtractionProgressEventArgs>? ExtractionProgressChanged;

    /// <summary>
    ///     Event raised when extraction starts (to configure progress bar).
    /// </summary>
    public event EventHandler? ExtractionStarted;

    /// <summary>
    ///     Event raised when extraction completes.
    /// </summary>
    public event EventHandler? ExtractionCompleted;

    /// <summary>
    ///     Executes the complete update process.
    /// </summary>
    /// <param name="processId">The process ID of the main application to wait for, or null.</param>
    /// <param name="targetAppProcessName">The application process name to wait for and restart (WPF or Avalonia).</param>
    /// <param name="ignoredFiles">Files to exclude during extraction (typically updater files).</param>
    /// <param name="cancellationToken">Token to cancel the update operation.</param>
    /// <returns>The result of the update operation.</returns>
    public async Task<UpdateResult> ExecuteUpdateAsync(int? processId, string targetAppProcessName,
        string[] ignoredFiles,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appDirectory))
        {
            return new UpdateResult
            {
                Success = false,
                ErrorMessage = "Could not determine the application directory.",
                RequiresManualUpdate = true
            };
        }

        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return new UpdateResult
            {
                Success = false,
                ErrorMessage = "No network connection available. Please check your internet connection and try again.",
                RequiresManualUpdate = true
            };
        }

        try
        {
            // Wait for the main application to exit
            try
            {
                await _processService.WaitForProcessExitAsync(processId, targetAppProcessName, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "Error waiting for main application to exit");
                await BugReportService.ReportBugAsync(ex, "Error waiting for main application to exit");
                throw;
            }

            // Fetch the latest release from GitHub (with secondary-server fallback)
            string assetUrl;
            string latestVersion;
            string? fallbackAssetUrl;
            try
            {
                (latestVersion, assetUrl, fallbackAssetUrl) =
                    await _gitHubService.GetLatestReleaseAssetUrlAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // HTTP/transport failures while fetching the release are expected network
                // conditions (the secondary-server fallback already failed) — log at
                // Information, not as a bug.
                if (ex is HttpRequestException or IOException)
                {
                    Log.Information(ex, "Error fetching latest release from GitHub");
                }
                else
                {
                    Log.Error(ex, "Error fetching latest release from GitHub");
                    await BugReportService.ReportBugAsync(ex, "Error fetching latest release from GitHub");
                }

                throw;
            }

            // Download the update file to memory (retrying from the secondary server if the primary download fails)
            MemoryStream updateFileStream;
            try
            {
                DownloadProgressReset?.Invoke(this, EventArgs.Empty);
                updateFileStream = await DownloadWithFallbackAsync(assetUrl, fallbackAssetUrl, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // HTTP/transport failures are expected user conditions (both the primary and
                // the secondary source already failed) — log at Information, not as a bug.
                if (ex is HttpRequestException or IOException)
                {
                    Log.Information(ex, "Error downloading update file from: {AssetUrl}", assetUrl);
                }
                else
                {
                    Log.Error(ex, "Error downloading update file from: {AssetUrl}", assetUrl);
                    await BugReportService.ReportBugAsync(ex, $"Error downloading update file from: {assetUrl}");
                }

                throw;
            }

            // Configure ignored files for ZIP extraction
            _zipService.IgnoredFiles = ignoredFiles;

            // Extract the ZIP file
            try
            {
                ExtractionStarted?.Invoke(this, EventArgs.Empty);
                await _zipService.ExtractFromStreamAsync(updateFileStream, cancellationToken);
                ExtractionCompleted?.Invoke(this, EventArgs.Empty);
            }
            // InvalidOperationException is excluded here because it typically indicates the UI (Dispatcher)
            // has been shut down (e.g., window closed), which is a normal application lifecycle event
            // during update and not a bug that needs reporting. Other exceptions during extraction
            // are genuine errors that should be reported.
            catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException)
            {
                Log.Error(ex, "Error extracting ZIP archive");
                await BugReportService.ReportBugAsync(ex, "Error extracting ZIP archive");
                throw;
            }
            finally
            {
                await updateFileStream.DisposeAsync();
            }

            LogMessage?.Invoke(this, new EventArgs<string>("Update installed successfully."));

            return new UpdateResult
            {
                Success = true,
                Version = latestVersion
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Network/transport failures (both sources already retried) are expected
            // user conditions — log at Information, not as a bug. Only unexpected
            // exceptions are errors.
            if (ex is HttpRequestException or IOException)
                Log.Information(ex, "Automatic update failed");
            else
                Log.Error(ex, "Automatic update failed");

            LogMessage?.Invoke(this, new EventArgs<string>($"Automatic update failed: {ex.Message}"));
            return new UpdateResult
            {
                Success = false,
                ErrorMessage = "Automatic update failed.",
                RequiresManualUpdate = true
            };
        }
    }

    /// <summary>
    ///     Downloads the update file: one immediate retry of the primary source with a
    ///     short cancellable backoff, then the secondary server.
    ///     UPD-19: HttpClient.Timeout surfaces as TaskCanceledException with the user
    ///     token NOT cancelled, so a timeout is correctly treated as a transport
    ///     failure here — only a genuinely user-cancelled token skips the
    ///     retry/fallback and rethrows.
    /// </summary>
    private async Task<MemoryStream> DownloadWithFallbackAsync(string assetUrl, string? fallbackAssetUrl,
        CancellationToken cancellationToken)
    {
        var hasFallback = !string.IsNullOrEmpty(fallbackAssetUrl) &&
                          !string.Equals(assetUrl, fallbackAssetUrl, StringComparison.OrdinalIgnoreCase);

        Exception? lastPrimaryError = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await _downloadService.DownloadToMemoryAsync(assetUrl, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User actually cancelled — don't retry or fall back
                throw;
            }
            catch (Exception ex)
            {
                lastPrimaryError = ex;
                Log.Information(ex, "Download from the primary source failed (attempt {Attempt}/2)", attempt);

                if (attempt == 1)
                {
                    LogMessage?.Invoke(this,
                        new EventArgs<string>(
                            $"Download from the primary source failed ({ex.Message}). Retrying..."));
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
        }

        if (hasFallback)
        {
            LogMessage?.Invoke(this,
                new EventArgs<string>(
                    "Download from the primary source failed. Retrying from the secondary server..."));
            Log.Information(lastPrimaryError,
                "Download from the primary source failed twice; retrying from the secondary server: {FallbackUrl}",
                fallbackAssetUrl);
            DownloadProgressReset?.Invoke(this, EventArgs.Empty);
            return await _downloadService.DownloadToMemoryAsync(fallbackAssetUrl!, cancellationToken);
        }

        throw lastPrimaryError!;
    }

    /// <summary>
    ///     Restarts the main application after a successful update.
    /// </summary>
    /// <param name="targetAppProcessName">The application process name to restart (WPF or Avalonia).</param>
    /// <returns>True if the restart was successful, false otherwise.</returns>
    public bool RestartMainApplication(string targetAppProcessName)
    {
        // ProcessService appends the platform-appropriate extension: passing the ".exe"
        // suffix here produced "SimpleLauncher.Avalonia.exe.exe" on Windows.
        return _processService.RestartApplication(_appDirectory, targetAppProcessName, "-whatsnew");
    }

    /// <summary>
    ///     Opens the manual download page for the user to download the update manually.
    /// </summary>
    public void OpenManualDownloadPage()
    {
        _processService.OpenUrl(GitHubService.GetReleasesPageUrl());
    }

    /// <summary>
    ///     Checks if Dokan is installed and offers to install it if missing.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the Dokan download/install wait.</param>
    public async Task CheckAndInstallDokanAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_dokanService.IsDokanInstalled())
            {
                LogMessage?.Invoke(this, new EventArgs<string>("Dokan is already installed. No action needed."));
                return;
            }

            // Dokan is not installed - ask the user
            LogMessage?.Invoke(this, new EventArgs<string>("Dokan library is not installed."));

            if (DokanInstallationPrompt != null)
            {
                var shouldInstall = await DokanInstallationPrompt.Invoke();
                if (!shouldInstall)
                {
                    LogMessage?.Invoke(this, new EventArgs<string>("Skipping Dokan installation."));
                    return;
                }
            }
            else
            {
                LogMessage?.Invoke(this,
                    new EventArgs<string>("No prompt handler configured. Skipping Dokan installation."));
                return;
            }

            // User chose to install Dokan
            LogMessage?.Invoke(this, new EventArgs<string>("Starting Dokan download and installation..."));
            await _dokanService.DownloadAndInstallDokanAsync(cancellationToken);
            LogMessage?.Invoke(this, new EventArgs<string>("Dokan installer has been launched."));
        }
        catch (OperationCanceledException)
        {
            // UPD-19: user cancellation is not an error — propagate without a bug report.
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Dokan installation check");
            await BugReportService.ReportBugAsync(ex, "Error during Dokan installation check");
            LogMessage?.Invoke(this, new EventArgs<string>($"Error during Dokan check/installation: {ex.Message}"));
        }
    }

    private void OnDownloadProgressChanged(object? sender, EventArgs<DownloadProgressInfo> e)
    {
        var info = e.Value;
        string statusText;
        if (info.Percentage >= 0)
        {
            var speedText = DownloadService.FormatBytes(info.BytesPerSecond) + "/s";
            statusText =
                $"{DownloadService.FormatBytes(info.BytesRead)} / {DownloadService.FormatBytes(info.TotalBytes)} ({info.Percentage:F1}%) - {speedText}";
        }
        else
        {
            statusText = $"Downloaded: {DownloadService.FormatBytes(info.BytesRead)}";
        }

        DownloadProgressChanged?.Invoke(this, new DownloadProgressEventArgs
        {
            Percentage = info.Percentage,
            BytesRead = info.BytesRead,
            TotalBytes = info.TotalBytes,
            StatusText = statusText
        });
    }

    private void OnExtractionProgressChanged(object? sender, EventArgs<ExtractionProgressInfo> e)
    {
        var info = e.Value;
        string statusText;
        if (info.CurrentFile != null)
            statusText = $"Extracting ({info.ExtractedCount}): {info.CurrentFile}";
        else
            statusText = "Extraction complete";

        ExtractionProgressChanged?.Invoke(this, new ExtractionProgressEventArgs
        {
            CurrentFile = info.CurrentFile,
            ExtractedCount = info.ExtractedCount,
            StatusText = statusText
        });
    }
}