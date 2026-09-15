using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace SimpleLauncher.Updater.Services;

/// <summary>
///     Service for downloading files with progress reporting.
/// </summary>
internal class DownloadService
{
    private const int FileBufferSize = 81920; // 80KB buffer for efficient file I/O

    /// <summary>
    ///     UPD-06: hard cap on in-memory downloads (1 GiB). Release zips and the Dokan MSI
    ///     are two orders of magnitude smaller — anything larger is a hostile/compromised
    ///     payload, and buffering it unbounded would exhaust RAM (OOM / zip-bomb).
    /// </summary>
    internal const long MaxDownloadBytes = 1L << 30;

    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Initializes a new instance of the DownloadService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for downloads.</param>
    public DownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    ///     Event raised when download progress changes.
    /// </summary>
    public event EventHandler<EventArgs<DownloadProgressInfo>>? ProgressChanged;

    /// <summary>
    ///     Event raised when a log message needs to be displayed.
    /// </summary>
    public event EventHandler<EventArgs<string>>? LogMessage;

    /// <summary>
    ///     Downloads a file to a memory stream with progress reporting.
    ///     UPD-06: the download is bounded by <see cref="MaxDownloadBytes" /> — an oversize
    ///     Content-Length is rejected before reading, and the running total is enforced
    ///     while streaming, so a lying/missing Content-Length cannot exhaust memory.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="cancellationToken">Token to cancel the download operation.</param>
    /// <returns>A MemoryStream containing the downloaded file.</returns>
    /// <exception cref="HttpRequestException">Thrown when the download fails.</exception>
    /// <exception cref="IOException">Thrown when the payload exceeds <see cref="MaxDownloadBytes" />.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<MemoryStream> DownloadToMemoryAsync(string url, CancellationToken cancellationToken = default)
    {
        LogMessage?.Invoke(this, new EventArgs<string>("Downloading the update file..."));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                throw new HttpRequestException(
                    $"Failed to download the update file. Status Code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            // Expected condition: the update flow retries from the secondary server, so a
            // primary-source HTTP/network failure is not a bug — log at Information level only.
            Log.Information(ex, "HTTP request failed for URL: {Url}", url);
            throw;
        }

        using (response)
        {
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            // UPD-06: reject an oversize payload before buffering a single byte.
            // Thrown as IOException so callers treat it as an expected download failure
            // (fallback source / manual update) rather than a bug report.
            if (totalBytes > MaxDownloadBytes)
                throw new IOException(
                    $"Update file too large: {FormatBytes(totalBytes)} exceeds the {FormatBytes(MaxDownloadBytes)} limit.");

            var memoryStream = new MemoryStream();
            var buffer = new byte[FileBufferSize];
            var totalBytesRead = 0L;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                while (true)
                {
                    try
                    {
                        var bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
                        if (bytesRead == 0) break;

                        await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalBytesRead += bytesRead;

                        // UPD-06: enforce the cap while streaming — a missing or lying
                        // Content-Length must not allow unbounded memory growth.
                        if (totalBytesRead > MaxDownloadBytes)
                            throw new IOException(
                                $"Download exceeded the {FormatBytes(MaxDownloadBytes)} limit — aborting.");

                        // Calculate and report progress
                        if (totalBytes > 0)
                        {
                            var percentage = (double)totalBytesRead / totalBytes * 100;
                            var elapsedSeconds = Math.Max(stopwatch.ElapsedMilliseconds / 1000.0, 0.001);
                            var speed = totalBytesRead / elapsedSeconds;

                            ProgressChanged?.Invoke(this, new EventArgs<DownloadProgressInfo>(new DownloadProgressInfo
                            {
                                Percentage = percentage,
                                BytesRead = totalBytesRead,
                                TotalBytes = totalBytes,
                                BytesPerSecond = speed
                            }));
                        }
                        else
                        {
                            // Unknown file size - report bytes downloaded only
                            ProgressChanged?.Invoke(this, new EventArgs<DownloadProgressInfo>(new DownloadProgressInfo
                            {
                                Percentage = -1,
                                BytesRead = totalBytesRead,
                                TotalBytes = 0,
                                BytesPerSecond = 0
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Transport failures/timeouts mid-download are expected network conditions;
                        // report genuine bugs in the outer catch instead.
                        Log.Information(ex, "Error reading from download stream or writing to memory stream");
                        throw;
                    }
                }

                stopwatch.Stop();
                memoryStream.Position = 0;
                LogMessage?.Invoke(this, new EventArgs<string>("Download complete"));
                return memoryStream;
            }
            catch (Exception ex)
            {
                await memoryStream.DisposeAsync();
                // HTTP failures, transport aborts and timeouts (including user cancellation)
                // are expected network conditions — they are retried from the fallback source
                // and must not be reported as bugs. Oversize-payload rejections (IOException)
                // and allocation failures (OutOfMemoryException, UPD-06) are likewise
                // environmental, not bugs. Only unexpected exceptions are reported.
                if (ex is HttpRequestException or IOException or OperationCanceledException or OutOfMemoryException)
                {
                    Log.Information(ex, "Error downloading update file");
                }
                else
                {
                    Log.Error(ex, "Error downloading update file");
                    await BugReportService.ReportBugAsync(ex, "Error downloading update file (outer catch)");
                }

                throw;
            }
        }
    }

    /// <summary>
    ///     Formats a byte count into a human-readable string.
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    /// <returns>A formatted string (e.g., "1.5 MB").</returns>
    public static string FormatBytes(double bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;

        return bytes switch
        {
            >= gb => $"{bytes / gb:F2} GB",
            >= mb => $"{bytes / mb:F2} MB",
            >= kb => $"{bytes / kb:F2} KB",
            _ => $"{bytes:F0} B"
        };
    }
}