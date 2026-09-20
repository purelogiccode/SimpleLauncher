using System.Security;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;

namespace SimpleLauncher.Avalonia.Updater.Services;

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
    ///     UPD-05: extraction is staged, never in-place. Every entry is validated and
    ///     written to a sibling staging directory first; only after the whole archive
    ///     is staged is each file swapped onto the live install with a <c>.updbak</c>
    ///     backup and rollback on failure — a mid-update failure (cancel, disk-full,
    ///     lock, power loss) can no longer leave half-old/half-new binaries behind.
    /// </summary>
    /// <param name="zipStream">The memory stream containing the ZIP archive.</param>
    /// <param name="cancellationToken">Token to cancel the extraction operation.</param>
    /// <returns>The number of files extracted.</returns>
    /// <exception cref="SecurityException">Thrown when a ZIP entry attempts to escape the target directory.</exception>
    /// <exception cref="IOException">Thrown when staging/swap fails (including insufficient disk space).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<int> ExtractFromStreamAsync(MemoryStream zipStream, CancellationToken cancellationToken = default)
    {
        LogMessage?.Invoke(this, new EventArgs<string>("Extracting update files..."));

        zipStream.Position = 0;
        var extractedCount = 0;

        var appDirectoryFullPath = EnsureTrailingSeparator(Path.GetFullPath(_appDirectory));

        // UPD-05: sibling staging directory — same volume as the install, so the later
        // per-file swaps are atomic moves. Leftovers from a crashed run are removed first.
        var stagingRoot = _appDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                          ".update-staging";
        var stagingRootFullPath = EnsureTrailingSeparator(Path.GetFullPath(stagingRoot));
        CleanupDirectoryQuietly(stagingRoot);
        CleanupStaleBackups(appDirectoryFullPath);
        Directory.CreateDirectory(stagingRoot);

        try
        {
            // Phase 1: validate every entry and extract into staging (live install untouched).
            var stagedFiles = new List<(string RelativePath, string StagedPath)>();

            // Use ZipReader for streaming extraction - no upfront indexing needed
            using (var reader = ZipReader.OpenReader(zipStream, new ReaderOptions { LeaveStreamOpen = true }))
            {
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
                        {
                            throw new SecurityException(
                                $"Zip entry is a symbolic link, refusing to extract: {entryKey}");
                        }

                        // UPD-04: reject Windows alternate data streams. Entry keys are relative,
                        // so any ':' can only be an ADS (e.g. "file.exe:hidden").
                        if (entryKey.Contains(':'))
                        {
                            throw new SecurityException(
                                $"Zip entry contains an alternate data stream, refusing to extract: {entryKey}");
                        }

                        // Validate and sanitize entry path to prevent path traversal attacks.
                        // The same relative path is resolved under BOTH roots — a ".." that
                        // escapes the install dir would escape staging too, so both are checked.
                        // Normalize: remove leading slashes, collapse "."/".." segments so a
                        // non-canonical entry cannot masquerade as a non-root path below.
                        var trimmedEntry = NormalizeEntryPath(entryKey.TrimStart('/', '\\'));

                        // Skip directory entries (created implicitly with their files)
                        if (reader.Entry.IsDirectory)
                            continue;

                        // UPD-23: only skip updater-owned files at the ARCHIVE ROOT.
                        // Matching on the bare file name also skipped e.g.
                        // "subdir/Updater.dll" (overbroad); matching is
                        // case-insensitive so case/extension variants at the root
                        // are still covered.
                        var isRootEntry = !trimmedEntry.Contains('/') && !trimmedEntry.Contains('\\');
                        var fileName = Path.GetFileName(trimmedEntry);
                        if (isRootEntry && !string.IsNullOrEmpty(fileName) &&
                            IgnoredFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                        {
                            LogMessage?.Invoke(this,
                                new EventArgs<string>($"Skipping self-update file: {entryKey}"));
                            continue;
                        }

                        var stagedPath = Path.GetFullPath(Path.Combine(stagingRoot, trimmedEntry));
                        var destinationPath = Path.GetFullPath(Path.Combine(_appDirectory, trimmedEntry));

                        // Security check: ensure the resolved destination path is within AppDirectory
                        // This is the actual guard — it catches all traversal attempts including encoded or multi-level ".."
                        if (!stagedPath.StartsWith(stagingRootFullPath, StringComparison.OrdinalIgnoreCase) ||
                            !destinationPath.StartsWith(appDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new SecurityException(
                                $"Zip entry attempts to escape target directory: {entryKey}");
                        }

                        // UPD-04: refuse to write through (or overwrite) a reparse point / symlink
                        // planted by an earlier entry of this same archive or a previous run —
                        // FileMode.Create would otherwise follow it outside AppDirectory even
                        // though this entry's own path passed the containment check above.
                        ThrowIfPathContainsLink(stagedPath, entryKey);
                        ThrowIfPathContainsLink(destinationPath, entryKey);

                        var stagingDirectory = Path.GetDirectoryName(stagedPath);

                        if (!string.IsNullOrEmpty(stagingDirectory) && !Directory.Exists(stagingDirectory))
                            Directory.CreateDirectory(stagingDirectory);

                        // UPD-21: fail fast on a full disk instead of burning five
                        // lock-retries per file and then blaming file locks.
                        ThrowIfInsufficientSpace(stagingRoot, reader.Entry.Size, entryKey);

                        // Extract with retry logic for locked files
                        await ExtractFileWithRetryAsync(reader, stagedPath, entryKey, cancellationToken);
                        stagedFiles.Add((trimmedEntry, stagedPath));

                        // UPD-22: count and announce only AFTER the bytes hit the disk —
                        // a failed/aborted write must not inflate the progress count.
                        extractedCount++;

                        // Report extraction progress (current file only, no percentage)
                        ProgressChanged?.Invoke(this, new EventArgs<ExtractionProgressInfo>(new ExtractionProgressInfo
                        {
                            CurrentFile = entryKey,
                            ExtractedCount = extractedCount
                        }));

                        LogMessage?.Invoke(this, new EventArgs<string>($"Extracted: {entryKey}"));
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // User cancel (Cancel button / window close) is not a bug and not an
                        // error: keep it below the bug-report sink's Warning threshold.
                        // Bugs #66949/#66950: user-cancelled extractions were reported as errors.
                        Log.Information("Extraction cancelled by user while processing: {EntryKey}", entryKey);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // UPD-24: log here with entry context, but report the bug ONCE
                        // at the UpdateService level — reporting here too spammed the
                        // bug API twice per file (including for malicious zips).
                        Log.Error(ex, "Error extracting file: {EntryKey}", entryKey);
                        throw;
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: swap the fully-staged tree onto the live install (UPD-05).
            await SwapStagedFilesAsync(stagedFiles, appDirectoryFullPath, cancellationToken);
        }
        finally
        {
            CleanupDirectoryQuietly(stagingRoot);
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
    ///     Swaps fully-staged files onto the live install (UPD-05). Each existing file is
    ///     moved to a <c>.updbak</c> backup first, then the staged file is moved into place
    ///     (same-volume move = atomic). If any swap fails, already-swapped files are rolled
    ///     back from their backups; on success the backups are deleted.
    /// </summary>
    private async Task SwapStagedFilesAsync(List<(string RelativePath, string StagedPath)> stagedFiles,
        string appDirectoryFullPath, CancellationToken cancellationToken)
    {
        // UPD-06 (disk-full): verify the staged payload plus a safety reserve fits on the
        // install drive before touching a single live file.
        long stagedBytes = 0;
        foreach (var (_, stagedPath) in stagedFiles) stagedBytes += new FileInfo(stagedPath).Length;

        var drive = new DriveInfo(Path.GetPathRoot(appDirectoryFullPath)!);
        const long reserveBytes = 64L * 1024 * 1024;
        if (drive.AvailableFreeSpace < stagedBytes + reserveBytes)
        {
            throw new IOException(
                $"Insufficient disk space for update: need {DownloadService.FormatBytes(stagedBytes + reserveBytes)}, " +
                $"only {DownloadService.FormatBytes(drive.AvailableFreeSpace)} free on {drive.Name}.");
        }

        LogMessage?.Invoke(this,
            new EventArgs<string>($"Staged {stagedFiles.Count} files — installing onto live application..."));
        // Track every file this swap touches so a failed install can be undone completely:
        // pre-existing files record their .updbak as soon as it is created (a failure of
        // the staged move itself must not orphan the original), and brand-new files are
        // recorded so rollback removes them again.
        var installed = new List<(string FinalPath, string? BackupPath)>();
        try
        {
            foreach (var (relativePath, stagedPath) in stagedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var finalPath = Path.GetFullPath(Path.Combine(_appDirectory, relativePath));

                if (!finalPath.StartsWith(appDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityException(
                        $"Zip entry attempts to escape target directory: {relativePath}");
                }

                ThrowIfPathContainsLink(finalPath, relativePath);

                var finalDirectory = Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrEmpty(finalDirectory) && !Directory.Exists(finalDirectory))
                    Directory.CreateDirectory(finalDirectory);

                string? backupPath = null;
                UnixFileMode? previousMode = null;
                if (File.Exists(finalPath))
                {
                    // UPD-04: archive mode bits are never applied, but on Unix the replaced
                    // file's mode must survive the swap — losing it would strip the
                    // executable bit from the app/updater and tools after an update.
                    previousMode = TryGetUnixFileMode(finalPath);
                    ClearReadOnlyAttribute(finalPath);
                    backupPath = finalPath + ".updbak";
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    await MoveFileWithRetryAsync(finalPath, backupPath, relativePath, cancellationToken);

                    // Record the backup before moving the staged file into place: if that
                    // move fails, the rollback below must still restore the original.
                    installed.Add((finalPath, backupPath));
                }

                await MoveFileWithRetryAsync(stagedPath, finalPath, relativePath, cancellationToken);
                TryApplyUnixFileMode(finalPath, previousMode);
                if (backupPath == null)
                    installed.Add((finalPath, null));

                ProgressChanged?.Invoke(this, new EventArgs<ExtractionProgressInfo>(new ExtractionProgressInfo
                {
                    CurrentFile = relativePath,
                    ExtractedCount = installed.Count
                }));
            }
        }
        catch
        {
            // Best-effort rollback: delete files that did not exist before the update and
            // restore every original from its backup (including backups whose staged move
            // never completed).
            foreach (var (finalPath, backupPath) in installed)
            {
                try
                {
                    if (File.Exists(finalPath))
                        File.Delete(finalPath);
                    if (backupPath != null && File.Exists(backupPath))
                        File.Move(backupPath, finalPath);
                }
                catch (Exception rollbackEx)
                {
                    Log.Warning(rollbackEx, "Failed to roll back file after failed update: {FinalPath}", finalPath);
                }
            }

            throw;
        }

        // Success — backups are no longer needed.
        foreach (var (_, backupPath) in installed)
        {
            if (backupPath == null) continue;
            try
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete update backup file: {BackupPath}", backupPath);
            }
        }

        LogMessage?.Invoke(this, new EventArgs<string>($"Installed {installed.Count} files onto live application."));
    }

    /// <summary>
    ///     Moves a file with retry logic for locked files (same-volume moves are atomic).
    ///     UPD-21: only transient file LOCK errors are retried — deterministic failures
    ///     (disk full, ACL, path too long) throw immediately with their real cause
    ///     instead of burning retries and blaming locks.
    /// </summary>
    private async Task MoveFileWithRetryAsync(string sourcePath, string destinationPath, string entryKey,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= FileWriteRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(sourcePath, destinationPath);
                return; // Success, exit the method
            }
            catch (IOException ex)
            {
                if (!IsFileLockError(ex) || attempt >= FileWriteRetryAttempts)
                {
                    throw new IOException(
                        IsFileLockError(ex)
                            ? $"Failed to move file after {FileWriteRetryAttempts} attempts: {entryKey}. " +
                              "The file is locked by another process."
                            : $"Failed to move file '{entryKey}': {ex.Message}",
                        ex);
                }

                // File is locked by another process, retry after delay
                LogMessage?.Invoke(this,
                    new EventArgs<string>(
                        $"File locked ({attempt}/{FileWriteRetryAttempts}): {entryKey} - retrying in {FileWriteRetryDelayMs * attempt}ms..."));
                await Task.Delay(FileWriteRetryDelayMs * attempt, cancellationToken);
            }
            catch (UnauthorizedAccessException ex)
            {
                // UPD-21: ACL denials are deterministic — fail fast with the real cause.
                throw new IOException(
                    $"Cannot move file '{entryKey}': access denied. " +
                    "Check the install folder permissions and run the updater with sufficient rights.",
                    ex);
            }
        }
    }

    /// <summary>
    ///     Reads a file's Unix permission mode (null on Windows or when it cannot be read).
    /// </summary>
    private static UnixFileMode? TryGetUnixFileMode(string path)
    {
        if (OperatingSystem.IsWindows()) return null;

        try
        {
            return File.GetUnixFileMode(path);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Re-applies a preserved Unix permission mode to a swapped file (best effort).
    /// </summary>
    private static void TryApplyUnixFileMode(string path, UnixFileMode? mode)
    {
        if (mode is null || OperatingSystem.IsWindows()) return;

        try
        {
            File.SetUnixFileMode(path, mode.Value);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to preserve Unix file mode after update: {Path}", path);
        }
    }

    /// <summary>
    ///     Clears the read-only attribute on an existing file (best effort).
    /// </summary>
    private static void ClearReadOnlyAttribute(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
        catch
        {
            // Best effort — the move will report the error if this fails
        }
    }

    private static string EnsureTrailingSeparator(string fullPath)
    {
        // UPD-03: the containment prefix must end in a separator — without it,
        // e.g. "C:\AppEvil\pwn.exe" passes StartsWith("C:\App") and escapes.
        return fullPath.EndsWith(Path.DirectorySeparatorChar) ? fullPath : fullPath + Path.DirectorySeparatorChar;
    }

    /// <summary>
    ///     Normalizes a ZIP entry path before the security checks: collapses "." and ".."
    ///     segments so entries like "./Updater.exe" or "sub/../Updater.exe" (which resolve
    ///     to the archive root) cannot bypass the updater self-protection check (UPD-23).
    ///     A leading ".." is kept so the containment check below still rejects it.
    /// </summary>
    private static string NormalizeEntryPath(string entry)
    {
        var segments = entry.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var normalized = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case ".":
                    continue;
                case "..":
                    if (normalized.Count > 0)
                        normalized.RemoveAt(normalized.Count - 1);
                    else
                        normalized.Add(segment);
                    continue;
                default:
                    normalized.Add(segment);
                    break;
            }
        }

        return string.Join(Path.DirectorySeparatorChar, normalized);
    }

    /// <summary>
    ///     Deletes a staging directory quietly (leftovers from a crashed run).
    /// </summary>
    private void CleanupDirectoryQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
                LogMessage?.Invoke(this, new EventArgs<string>("Removed stale update staging directory."));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clean staging directory: {Directory}", directory);
        }
    }

    /// <summary>
    ///     Recovers <c>.updbak</c> files left by a previously interrupted swap.
    ///     When the live file exists the backup is orphaned (the swap completed) and is
    ///     deleted; when the live file is missing the backup is the only remaining copy
    ///     of the original and is restored instead of being destroyed.
    /// </summary>
    private static void CleanupStaleBackups(string appDirectoryFullPath)
    {
        try
        {
            var root = appDirectoryFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root))
                return;

            foreach (var backup in Directory.EnumerateFiles(root, "*.updbak", SearchOption.AllDirectories))
            {
                try
                {
                    var livePath = backup[..^".updbak".Length];
                    if (!File.Exists(livePath))
                    {
                        File.Move(backup, livePath);
                        Log.Information("Restored a file left by an interrupted update: {LivePath}", livePath);
                        continue;
                    }

                    File.Delete(backup);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete stale update backup: {Backup}", backup);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate stale update backups");
        }
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
            {
                throw new SecurityException(
                    $"Zip entry path traverses a symbolic link, refusing to extract: {entryKey}");
            }

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
    ///     (including Unix setuid/setgid) are never applied (UPD-04). The swap
    ///     later restores the replaced file's previous Unix mode.
    ///     UPD-05: the destination is inside the staging directory; the live
    ///     install is only touched later by <see cref="SwapStagedFilesAsync" />.
    ///     UPD-20: written with <see cref="FileShare.None" /> — a concurrent reader
    ///     (Explorer, AV scanner, second updater, not-yet-exited app) must never
    ///     observe or load a half-written binary.
    ///     UPD-21: only transient file LOCK errors are retried — deterministic
    ///     failures (disk full, ACL, path too long) throw immediately with their
    ///     real cause instead of burning retries and blaming locks.
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
        for (var attempt = 1; attempt <= FileWriteRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Clear read-only attribute if the file already exists (e.g., from a previous installation)
            if (File.Exists(destinationPath))
                ClearReadOnlyAttribute(destinationPath);

            try
            {
                await using var destinationFileStream = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    FileBufferSize,
                    true);
                await using var entryStream = reader.OpenEntryStream();
                await entryStream.CopyToAsync(destinationFileStream, cancellationToken);
                return; // Success, exit the method
            }
            catch (IOException ex)
            {
                if (!IsFileLockError(ex) || attempt >= FileWriteRetryAttempts)
                {
                    throw new IOException(
                        IsFileLockError(ex)
                            ? $"Failed to extract file after {FileWriteRetryAttempts} attempts: {entryKey}. " +
                              "The file is locked by another process."
                            : $"Failed to write file '{entryKey}': {ex.Message}",
                        ex);
                }

                // File is locked by another process, retry after delay
                LogMessage?.Invoke(this,
                    new EventArgs<string>(
                        $"File locked ({attempt}/{FileWriteRetryAttempts}): {entryKey} - retrying in {FileWriteRetryDelayMs * attempt}ms..."));
                await Task.Delay(FileWriteRetryDelayMs * attempt,
                    cancellationToken); // Increasing delay for each attempt
            }
            catch (UnauthorizedAccessException ex)
            {
                // UPD-21: ACL/read-only denials are deterministic — fail fast with
                // the real cause instead of burning ~7.5s of retries.
                throw new IOException(
                    $"Cannot write file '{entryKey}': access denied. " +
                    "Check the install folder permissions and run the updater with sufficient rights.",
                    ex);
            }
        }
    }

    /// <summary>
    ///     True only for transient file-locking failures (another process holds the
    ///     file): Win32 ERROR_SHARING_VIOLATION / ERROR_LOCK_VIOLATION, surfaced as
    ///     IOException HResults. Everything else (disk full, ACL, path too long)
    ///     is deterministic and must fail fast (UPD-21).
    /// </summary>
    private static bool IsFileLockError(IOException ex)
    {
        return ex.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021);
    }

    /// <summary>
    ///     Throws an <see cref="IOException" /> with real numbers when the drive
    ///     holding <paramref name="directory" /> cannot fit
    ///     <paramref name="bytesNeeded" /> plus a safety reserve (UPD-21: fail fast
    ///     on disk-full instead of retrying every file as "locked").
    /// </summary>
    private static void ThrowIfInsufficientSpace(string directory, long bytesNeeded, string entryKey)
    {
        if (bytesNeeded <= 0)
            return; // Unknown size — the write itself will report ENOSPC fail-fast.

        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!);
        const long reserveBytes = 64L * 1024 * 1024;
        if (drive.AvailableFreeSpace < bytesNeeded + reserveBytes)
        {
            throw new IOException(
                $"Insufficient disk space to extract '{entryKey}': need " +
                $"{DownloadService.FormatBytes(bytesNeeded + reserveBytes)}, only " +
                $"{DownloadService.FormatBytes(drive.AvailableFreeSpace)} free on {drive.Name}.");
        }
    }
}