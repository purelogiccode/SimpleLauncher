using System.IO;
using System.Security;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;

namespace SimpleLauncher.Updater.Services;

/// <summary>
///     Service for extracting ZIP archives with security checks and progress reporting.
/// </summary>
internal class ZipService
{
    private const int FileBufferSize = 81920; // 80KB buffer for efficient file I/O
    private const int FileWriteRetryAttempts = 5; // Number of retry attempts for locked files
    private const int FileWriteRetryDelayMs = 500; // Delay between retry attempts

    private readonly string _appDirectory;

    /// <summary>
    ///     Initializes a new instance of the ZipService class.
    /// </summary>
    /// <param name="appDirectory">The directory where files should be extracted.</param>
    public ZipService(string appDirectory)
    {
        _appDirectory = appDirectory;
    }

    /// <summary>
    ///     Gets or sets the array of filenames to exclude from extraction.
    ///     These are typically files that should not be overwritten (like the updater itself).
    /// </summary>
    public string[] IgnoredFiles { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Event raised when extraction progress changes.
    /// </summary>
    public event EventHandler<EventArgs<ExtractionProgressInfo>>? ProgressChanged;

    /// <summary>
    ///     Event raised when a log message needs to be displayed.
    /// </summary>
    public event EventHandler<EventArgs<string>>? LogMessage;

    /// <summary>
    ///     Extracts a ZIP archive from a memory stream to the application directory.
    ///     Uses streaming extraction without upfront indexing for faster start.
    /// </summary>
    /// <param name="zipStream">The memory stream containing the ZIP archive.</param>
    /// <param name="cancellationToken">Token to cancel the extraction operation.</param>
    /// <returns>The number of files extracted.</returns>
    /// <exception cref="SecurityException">Thrown when a ZIP entry attempts to escape the target directory.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<int> ExtractFromStreamAsync(MemoryStream zipStream, CancellationToken cancellationToken = default)
    {
        LogMessage?.Invoke(this, new EventArgs<string>("Extracting update files..."));

        zipStream.Position = 0;
        var extractedCount = 0;

        // Use ZipReader for streaming extraction - no upfront indexing needed
        using var reader = ZipReader.OpenReader(zipStream, new ReaderOptions { LeaveStreamOpen = true });

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryKey = reader.Entry.Key;

            try
            {
                // Skip entries without keys
                if (string.IsNullOrEmpty(entryKey))
                    continue;

                // UPD-04: reject symbolic-link entries at the archive level. SharpCompress
                // surfaces the link target; materializing links would let a later entry
                // (or a FileMode.Create write) escape AppDirectory through the link.
                if (!string.IsNullOrEmpty(reader.Entry.LinkTarget))
                    throw new SecurityException($"Zip entry is a symbolic link, refusing to extract: {entryKey}");

                // UPD-04: reject Windows alternate data streams. Entry keys are relative,
                // so any ':' can only be an ADS (e.g. "file.exe:hidden").
                if (entryKey.Contains(':'))
                    throw new SecurityException(
                        $"Zip entry contains an alternate data stream, refusing to extract: {entryKey}");

                // Skip directory entries
                if (reader.Entry.IsDirectory)
                    continue;

                var fileName = Path.GetFileName(entryKey);
                if (!string.IsNullOrEmpty(fileName) &&
                    IgnoredFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    LogMessage?.Invoke(this, new EventArgs<string>($"Skipping self-update file: {entryKey}"));
                    continue;
                }

                // Validate and sanitize entry path to prevent path traversal attacks
                // Normalize: remove leading slashes, then combine and resolve
                var trimmedEntry = entryKey.TrimStart('/', '\\');
                var destinationPath = Path.GetFullPath(Path.Combine(_appDirectory, trimmedEntry));
                var appDirectoryFullPath = Path.GetFullPath(_appDirectory);

                // UPD-03: the containment prefix must end in a separator — without it,
                // e.g. "C:\AppEvil\pwn.exe" passes StartsWith("C:\App") and escapes.
                if (!appDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar))
                    appDirectoryFullPath += Path.DirectorySeparatorChar;

                // Security check: ensure the resolved destination path is within AppDirectory
                // This is the actual guard — it catches all traversal attempts including encoded or multi-level ".."
                if (!destinationPath.StartsWith(appDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                    throw new SecurityException($"Zip entry attempts to escape target directory: {entryKey}");

                // UPD-04: refuse to write through (or overwrite) a reparse point / symlink
                // planted by an earlier entry of this same archive or a previous run —
                // FileMode.Create would otherwise follow it outside AppDirectory even
                // though this entry's own path passed the containment check above.
                ThrowIfPathContainsLink(destinationPath, entryKey);

                var destinationDirectory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                extractedCount++;

                // Report extraction progress (current file only, no percentage)
                ProgressChanged?.Invoke(this, new EventArgs<ExtractionProgressInfo>(new ExtractionProgressInfo
                {
                    CurrentFile = entryKey,
                    ExtractedCount = extractedCount
                }));

                // Extract with retry logic for locked files
                await ExtractFileWithRetryAsync(reader, destinationPath, entryKey, cancellationToken);

                LogMessage?.Invoke(this, new EventArgs<string>($"Extracted: {entryKey}"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting file: {EntryKey}", entryKey);
                await BugReportService.ReportBugAsync(ex, $"Error extracting file: {entryKey}");
                throw;
            }
        }

        // Report completion
        ProgressChanged?.Invoke(this, new EventArgs<ExtractionProgressInfo>(new ExtractionProgressInfo
        {
            CurrentFile = null,
            ExtractedCount = extractedCount
        }));

        LogMessage?.Invoke(this, new EventArgs<string>($"Extraction complete ({extractedCount} files extracted)"));
        return extractedCount;
    }

    /// <summary>
    ///     Throws when <paramref name="destinationPath" /> itself, or any existing
    ///     component of its directory chain, is a symbolic link, junction, or other
    ///     reparse point (UPD-04). Checked immediately before the write so an entry
    ///     extracted earlier in this same pass cannot redirect a later one.
    /// </summary>
    /// <exception cref="SecurityException">A link was found on the destination path.</exception>
    private static void ThrowIfPathContainsLink(string destinationPath, string entryKey)
    {
        // The final component (may exist from a previous run).
        if (IsLink(destinationPath))
            throw new SecurityException($"Zip entry targets a symbolic link, refusing to extract: {entryKey}");

        // Each existing ancestor directory.
        var directory = Path.GetDirectoryName(destinationPath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (IsLink(directory))
                throw new SecurityException(
                    $"Zip entry path traverses a symbolic link, refusing to extract: {entryKey}");

            directory = Path.GetDirectoryName(directory);
        }
    }

    private static bool IsLink(string path)
    {
        try
        {
            // LinkTarget uses lstat semantics: detects symlinks/junctions on both
            // platforms, including dangling ones that Exists checks would miss.
            if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
                return true;

            // Unix GetAttributes stats the link target, so this only fires on Windows —
            // kept for junctions/mount points LinkTarget may not report.
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch
        {
            // Path does not exist (yet) — nothing to follow.
            return false;
        }
    }

    /// <summary>
    ///     Extracts a file from the ZIP reader with retry logic for locked files.
    ///     Files are created with default ACLs/mode — archive permission bits
    ///     (including Unix setuid/setgid) are never applied (UPD-04).
    /// </summary>
    /// <param name="reader">The ZIP reader positioned at the entry to extract.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="entryKey">The ZIP entry key for logging purposes.</param>
    /// <param name="cancellationToken">Token to cancel the extraction operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be written after all retry attempts.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    private async Task ExtractFileWithRetryAsync(IReader reader, string destinationPath, string entryKey,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= FileWriteRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Clear read-only attribute if the file already exists (e.g., from a previous installation)
            if (File.Exists(destinationPath))
            {
                try
                {
                    var attributes = File.GetAttributes(destinationPath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        File.SetAttributes(destinationPath, attributes & ~FileAttributes.ReadOnly);
                }
                catch
                {
                    // Best effort — extraction will report the error if this fails
                }
            }

            try
            {
                await using var destinationFileStream = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete,
                    FileBufferSize,
                    true);
                await using var entryStream = reader.OpenEntryStream();
                await entryStream.CopyToAsync(destinationFileStream, cancellationToken);
                return; // Success, exit the method
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException &&
                                       attempt < FileWriteRetryAttempts)
            {
                // File is likely locked by another process or has permission issues, retry after delay
                lastException = ex;
                LogMessage?.Invoke(this,
                    new EventArgs<string>(
                        $"File locked or access denied ({attempt}/{FileWriteRetryAttempts}): {entryKey} - retrying in {FileWriteRetryDelayMs}ms..."));
                await Task.Delay(FileWriteRetryDelayMs * attempt,
                    cancellationToken); // Increasing delay for each attempt
            }
        }

        // All retry attempts failed
        if (lastException != null)
        {
            throw new IOException(
                $"Failed to extract file after {FileWriteRetryAttempts} attempts: {entryKey}. " +
                "The file may be locked by another process or has restricted permissions.", lastException);
        }
    }
}