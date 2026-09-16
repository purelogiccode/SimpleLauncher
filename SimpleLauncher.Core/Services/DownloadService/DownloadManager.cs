using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;

namespace SimpleLauncher.Core.Services.DownloadService;

/// <inheritdoc />
/// <summary>
///     Manages the download and extraction of files with progress reporting and cancellation support.
/// </summary>
public class DownloadManager : IDisposable
{
    // Constants
    private const int RetryMaxAttempts = 3;
    private const int RetryBaseDelayMs = 1000;


    private const long DefaultRequiredSpaceBytes = 5L * 1024 * 1024 * 1024; // 5 GB
    private readonly IDispatcherService _dispatcherService;
    private readonly IExtractionService _extractionService;

    // Private fields
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Lock _lock = new();

    // Serializes DownloadFileAsync so a second call cannot Reset/dispose the
    // CancellationTokenSource while a first download is still using it (CORE-08).
    // Only one download owns the shared CTS and the shared status flags at a time.
    private readonly SemaphoreSlim _downloadGate = new(1, 1);
    private readonly ILogger _logger;
    private readonly IResourceProvider _resourceProvider;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    // Volatile backing fields for thread-safe state access across async/thread boundaries
    private volatile bool _isDownloadCompleted;
    private volatile bool _isFileLockedDuringDownload;
    private volatile bool _isUserCancellation;

    // Incremented by CancelDownload. A download snapshots it when the call starts: if the
    // value changed before the download's state reset, the stop request belongs to this
    // download and must not be discarded. Guarded by _lock.
    private long _cancelEpoch;

    /// <summary>
    ///     Initializes a new instance of the DownloadManager.
    /// </summary>
    public DownloadManager(IHttpClientFactory httpClientFactory, IExtractionService extractionService,
        ILogger logErrors, IResourceProvider resourceProvider, IDispatcherService dispatcherService)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _extractionService = extractionService ?? throw new ArgumentNullException(nameof(extractionService));
        _logger = logErrors ?? throw new ArgumentNullException(nameof(logErrors));
        _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));

        // Initialize temp folder
        TempFolder = Path.Combine(Path.GetTempPath(), "SimpleLauncher");

        try
        {
            Directory.CreateDirectory(TempFolder);
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, $"Error creating temp folder: {TempFolder}");
        }

        // Get HttpClient from the factory
        _httpClient = _httpClientFactory.CreateClient("DownloadClient")
                      ?? throw new InvalidOperationException(
                          "IHttpClientFactory.CreateClient returned null for 'DownloadClient'.");

        // Initialize cancellation token source
        _cancellationTokenSource = new CancellationTokenSource();
    }

    // Properties
    /// <summary>
    ///     Gets a value indicating whether the download was completed successfully.
    /// </summary>
    internal bool IsDownloadCompleted
    {
        get => _isDownloadCompleted;
        private set => _isDownloadCompleted = value;
    }

    /// <summary>
    ///     Gets a value indicating whether the download was canceled by the user.
    /// </summary>
    internal bool IsUserCancellation
    {
        get => _isUserCancellation;
        private set => _isUserCancellation = value;
    }

    /// <summary>
    ///     Gets the temporary folder used for downloads.
    /// </summary>
    internal string TempFolder { get; }

    /// <summary>
    ///     Gets a value indicating whether the file was locked during the download attempt.
    /// </summary>
    internal bool IsFileLockedDuringDownload
    {
        get => _isFileLockedDuringDownload;
        private set => _isFileLockedDuringDownload = value;
    }

    /// <summary>
    ///     Releases all resources used by the DownloadManager, canceling any ongoing download.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        CancellationTokenSource? cts;
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            cts = _cancellationTokenSource;
            _cancellationTokenSource = null;
        }

        // Cancel and dispose outside the lock to prevent deadlock
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore
        }

        cts?.Dispose();

        _httpClient?.Dispose();

        try
        {
            _downloadGate.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore - already disposed.
        }

        GC.SuppressFinalize(this);
    }

    // Events
    /// <summary>
    ///     Event raised when download progress changes.
    /// </summary>
    public event EventHandler<DownloadProgressEventArgs> DownloadProgressChanged = null!;

    // Methods
    /// <summary>
    ///     Cancels any ongoing download operation.
    /// </summary>
    internal void CancelDownload()
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            if (_disposed)
                return;

            IsUserCancellation = true;
            _cancelEpoch++;
            cts = _cancellationTokenSource;
        }

        // Cancel outside the lock to prevent blocking and handle disposal races
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore - the CTS was already disposed
        }
    }

    private void ResetCancellationToken()
    {
        CancellationTokenSource? oldCts;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(DownloadManager));

            oldCts = _cancellationTokenSource;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        // Dispose outside the lock to prevent deadlock and ObjectDisposedException races
        try
        {
            oldCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        oldCts?.Dispose();
    }

    /// <summary>
    ///     Downloads a file from the specified URL to a temporary location.
    /// </summary>
    /// <param name="downloadUrl">The URL to download from.</param>
    /// <param name="fileName">Optional custom file name to use.</param>
    /// <returns>The path to the downloaded file, or null if the download failed.</returns>
    internal async Task<string?> DownloadFileAsync(string downloadUrl, string? fileName = null)
    {
        // Snapshot the cancel epoch at invocation: a stop request that arrives after this
        // point (while the download is queued on the gate or mid-reset) must cancel this
        // download. A stop request from an earlier session does not change this value.
        long cancelEpochAtStart;
        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DownloadManager));

            cancelEpochAtStart = _cancelEpoch;
        }

        // Serialize downloads so a second call can never Reset/dispose the shared
        // CancellationTokenSource while a first download still owns its token (CORE-08).
        try
        {
            await _downloadGate.WaitAsync();
        }
        catch (ObjectDisposedException)
        {
            throw new ObjectDisposedException(nameof(DownloadManager));
        }

        try
        {
            bool canceledWhileQueued;
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed || _cancellationTokenSource == null,
                    nameof(DownloadManager));

                // Ordered against CancelDownload: either the stop request is seen here
                // (and honored) or CancelDownload runs after the reset and cancels the new
                // CTS — the flag clearing below can never swallow a pending stop request.
                canceledWhileQueued = _cancelEpoch != cancelEpochAtStart;
                if (canceledWhileQueued)
                {
                    IsUserCancellation = true;
                }
                else
                {
                    IsDownloadCompleted = false;
                    IsUserCancellation = false;
                    IsFileLockedDuringDownload = false;
                }
            }

            if (canceledWhileQueued)
            {
                _logger.Debug(
                    $"Download start skipped because a stop request arrived while it was queued: {downloadUrl}");
                return null;
            }

            // Reset the cancellation token source at the beginning of every download attempt.
            // Safe here: the gate guarantees no other download is using the old CTS.
            ResetCancellationToken();

        // Determine a safe file name confined to TempFolder (CORE-05).
        // Both the caller-supplied fileName and the URL-derived name are untrusted:
        // strip directories, query strings, and invalid chars, then verify containment.
        fileName = GetSafeDownloadFileName(fileName, downloadUrl);

        // Create temp file path (guaranteed inside TempFolder by GetSafeDownloadFileName)
        var downloadFilePath = Path.Combine(TempFolder, fileName);

        // Attempt to delete any existing file to avoid file-lock issues
        // from a previous failed or cancelled download
        if (File.Exists(downloadFilePath))
        {
            try
            {
                File.Delete(downloadFilePath);
            }
            catch
            {
                // File may be locked by another process; proceed and let FileStream report the error
            }
        }

        // Check disk space
        var diskSpaceCheckResult = CheckAvailableDiskSpace(TempFolder);
        switch (diskSpaceCheckResult)
        {
            case false:
                await RaiseProgressChangedAsync(new DownloadProgressEventArgs
                {
                    ProgressPercentage = 0,
                    StatusMessage = GetResourceString("InsufficientdiskspaceinSimpleLauncherHDD",
                        "Insufficient disk space.")
                });
                throw new IOException("Insufficient disk space in 'Simple Launcher' HDD.");
            case null:
                await RaiseProgressChangedAsync(new DownloadProgressEventArgs
                {
                    ProgressPercentage = 0,
                    StatusMessage = GetResourceString("CannotCheckDiskSpace",
                        "Cannot check available disk space. The path may be inaccessible or you may lack permissions.")
                });
                throw new IOException(
                    "Cannot check disk space for 'Simple Launcher' HDD. The path may be inaccessible or you may lack permissions.");
        }

        CancellationToken token;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed || _cancellationTokenSource == null, nameof(DownloadManager));

            token = _cancellationTokenSource.Token;
        }

        var currentRetry = 0;

        while (currentRetry <= RetryMaxAttempts && !IsUserCancellation)
        {
            try
            {
                await DownloadWithProgressAsync(downloadUrl, downloadFilePath, token);

                if (IsDownloadCompleted) return downloadFilePath;

                currentRetry++;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException
                                           or Polly.Timeout.TimeoutRejectedException)
            {
                if (IsUserCancellation) return null;

                // Check for file lock specifically
                if (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
                {
                    IsFileLockedDuringDownload = true;
                    await DeleteFiles.TryDeleteFileAsync(downloadFilePath);
                    return null;
                }

                currentRetry++;

                // Cleanup partial file before retry
                await DeleteFiles.TryDeleteFileAsync(downloadFilePath);

                if (currentRetry > RetryMaxAttempts)
                {
                    // Expected condition (download failed after all retries — slow/unreachable
                    // network, resilience timeouts): not a bug, keep it out of the bug report
                    // service (see bug 65965).
                    _logger.Information(ex, $"Download failed after all retries for {downloadUrl}");
                    break;
                }

                var delay = RetryBaseDelayMs * (int)Math.Pow(2, currentRetry - 1);
                await RaiseProgressChangedAsync(new DownloadProgressEventArgs
                {
                    ProgressPercentage = 0,
                    StatusMessage = $"Download error. Retrying ({currentRetry}/{RetryMaxAttempts})..."
                });

                try
                {
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

            return null;
        }
        finally
        {
            try
            {
                _downloadGate.Release();
            }
            catch (ObjectDisposedException)
            {
                // Manager was disposed while the download was finishing; nothing to release.
            }
        }
    }


    /// <summary>
    ///     Extracts a compressed file to the specified destination.
    /// </summary>
    /// <param name="filePath">The path to the compressed file.</param>
    /// <param name="destinationPath">The destination path to extract to.</param>
    /// <returns>True if the extraction was successful, otherwise false.</returns>
    internal async Task<bool> ExtractFileAsync(string filePath, string destinationPath)
    {
        try
        {
            await _dispatcherService.InvokeAsync(() =>
            {
                OnProgressChanged(new DownloadProgressEventArgs
                {
                    ProgressPercentage = 0,
                    StatusMessage = GetResourceString("Extracting",
                        $"Extracting to {destinationPath}...")
                });
            });

            var result = await _extractionService.ExtractToFolderAsync(filePath, destinationPath);

            if (result)
            {
                await _dispatcherService.InvokeAsync(() =>
                {
                    OnProgressChanged(new DownloadProgressEventArgs
                    {
                        ProgressPercentage = 100,
                        StatusMessage = GetResourceString("ExtractionCompleted", "Extraction completed successfully.")
                    });
                });
            }
            else
            {
                await _dispatcherService.InvokeAsync(() =>
                {
                    OnProgressChanged(new DownloadProgressEventArgs
                    {
                        ProgressPercentage = 0,
                        StatusMessage = GetResourceString("ExtractionFailed", "Extraction failed.")
                    });
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            await _dispatcherService.InvokeAsync(() =>
            {
                OnProgressChanged(new DownloadProgressEventArgs
                {
                    ProgressPercentage = 0,
                    StatusMessage = GetResourceString("ExtractionError", $"Extraction error: {ex.Message}")
                });
            });

            // Notify developer
            _logger.Error(ex, $"Error extracting file: {filePath} to {destinationPath}");

            return false;
        }
    }

    private async Task DownloadWithProgressAsync(string downloadUrl, string destinationPath,
        CancellationToken cancellationToken)
    {
        // Create request
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

        using var response =
            await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        // Ensure the temp folder exists before opening the file. The folder is created in the
        // constructor but the startup cleanup (CleanSimpleLauncherFolderService) deletes it in the
        // background while the app runs — without this, FileMode.Create throws
        // DirectoryNotFoundException and the download fails without ever starting.
        Directory.CreateDirectory(TempFolder);

        // Open stream: always start fresh (retries are handled by the resilience pipeline)
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write,
            FileShare.ReadWrite, 8192, true);
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[8192];
        var totalBytesRead = 0L;
        int bytesRead;
        var lastProgressUpdate = DateTime.Now;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (IsUserCancellation) throw new TaskCanceledException();

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            // Throttle UI updates to 10fps
            if ((DateTime.Now - lastProgressUpdate).TotalMilliseconds >= 100)
            {
                // Content-Length 0 (or missing) must not produce NaN/Infinity percentages.
                var progressPercentage = totalBytes is > 0 ? (double)totalBytesRead / totalBytes.Value * 100 : 0;
                var sizeStatus = totalBytes.HasValue
                    ? $"{FormatFileSize.FormatToHumanReadable(totalBytesRead)} of {FormatFileSize.FormatToHumanReadable(totalBytes.Value)}"
                    : $"{FormatFileSize.FormatToHumanReadable(totalBytesRead)}";

                await RaiseProgressChangedAsync(new DownloadProgressEventArgs
                {
                    BytesReceived = totalBytesRead,
                    TotalBytesToReceive = totalBytes,
                    ProgressPercentage = progressPercentage,
                    StatusMessage =
                        $"{GetResourceString("Downloading", "Downloading")}: {sizeStatus} ({progressPercentage:F1}%)"
                });
                lastProgressUpdate = DateTime.Now;
            }
        }

        // Final validation
        if (!totalBytes.HasValue || totalBytesRead >= totalBytes.Value)
        {
            IsDownloadCompleted = true;
            await RaiseProgressChangedAsync(new DownloadProgressEventArgs
            {
                BytesReceived = totalBytesRead,
                TotalBytesToReceive = totalBytes,
                ProgressPercentage = 100,
                StatusMessage =
                    $"{GetResourceString("Downloadcomplete2", "Download complete")}: {FormatFileSize.FormatToHumanReadable(totalBytesRead)}"
            });
        }
    }

    /// <summary>
    ///     Checks if there is enough disk space available in the specified folder.
    /// </summary>
    private static bool? CheckAvailableDiskSpace(string folderPath, long requiredSpace = DefaultRequiredSpaceBytes)
    {
        try
        {
            folderPath = Path.GetFullPath(folderPath);
            var driveInfo = new DriveInfo(Path.GetPathRoot(folderPath) ??
                                          throw new InvalidOperationException("Could not get the drive info"));
            return driveInfo.AvailableFreeSpace > requiredSpace;
        }
        catch
        {
            // If we can't check disk space (e.g., network drive issues, permissions),
            // return null to indicate inability to check rather than insufficient space.
            return null;
        }
    }

    /// <summary>
    ///     Derives a safe file name confined to <see cref="TempFolder" /> (CORE-05).
    ///     Strips directories, URL query/fragment, and invalid chars; falls back to a GUID
    ///     name when the result is empty or would escape <see cref="TempFolder" />.
    /// </summary>
    private string GetSafeDownloadFileName(string? fileName, string downloadUrl)
    {
        if (string.IsNullOrEmpty(fileName))
            fileName = DeriveFileNameFromUrl(downloadUrl);

        // Strip any directory components (handles "../../evil.exe", "C:\evil", "a/b").
        fileName = Path.GetFileName(fileName);

        // Remove invalid file-name chars.
        var invalid = Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalid) >= 0)
        {
            var builder = new System.Text.StringBuilder(fileName.Length);
            foreach (var c in fileName)
            {
                if (Array.IndexOf(invalid, c) < 0)
                    builder.Append(c);
            }

            fileName = builder.ToString();
        }

        fileName = fileName.Trim().Trim('.');
        if (string.IsNullOrEmpty(fileName))
            fileName = "download_" + Guid.NewGuid().ToString("N");

        // Containment check: never allow escape from TempFolder.
        try
        {
            var fullTemp = Path.GetFullPath(TempFolder);
            if (!fullTemp.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                fullTemp += Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(fullTemp, fileName));
            if (!fullPath.StartsWith(fullTemp, StringComparison.OrdinalIgnoreCase))
                return "download_" + Guid.NewGuid().ToString("N");
        }
        catch
        {
            return "download_" + Guid.NewGuid().ToString("N");
        }

        return fileName;
    }

    private static string DeriveFileNameFromUrl(string downloadUrl)
    {
        try
        {
            // Prefer URI parsing so "?x=1" / "#frag" never end up in the file name.
            if (Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
            {
                var name = Path.GetFileName(uri.AbsolutePath);
                name = Uri.UnescapeDataString(name ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            else
            {
                var cut = downloadUrl.Split(['?', '#'], 2)[0];
                var name = Path.GetFileName(cut).Trim();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
        }
        catch
        {
            // Fall through to GUID fallback below.
        }

        return "download_" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    ///     Gets a localized string from resources.
    /// </summary>
    /// <param name="resourceKey">The resource key.</param>
    /// <param name="defaultValue">The default value if the resource is not found.</param>
    /// <returns>The localized string or the default value.</returns>
    private string GetResourceString(string resourceKey, string defaultValue)
    {
        return _resourceProvider.GetString(resourceKey, defaultValue);
    }

    /// <summary>
    ///     Raises the DownloadProgressChanged event.
    ///     NOTE: invoked on the caller's thread. Download-path callers must go through
    ///     <see cref="RaiseProgressChangedAsync" /> so UI subscribers always run on the
    ///     dispatcher thread (CORE-09). ExtractFileAsync already marshals explicitly.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected virtual void OnProgressChanged(DownloadProgressEventArgs e)
    {
        DownloadProgressChanged?.Invoke(this, e);
    }

    /// <summary>
    ///     Marshals a progress event to the UI dispatcher thread (CORE-09).
    ///     Progress reporting is best-effort: a dispatcher failure (e.g. shutdown)
    ///     must never fail the download itself, so it is logged and swallowed.
    /// </summary>
    private async Task RaiseProgressChangedAsync(DownloadProgressEventArgs e)
    {
        try
        {
            await _dispatcherService.InvokeAsync(() => OnProgressChanged(e));
        }
        catch (Exception ex)
        {
            _logger.Debug($"[DownloadManager] Progress dispatch failed: {ex.Message}");
        }
    }
}