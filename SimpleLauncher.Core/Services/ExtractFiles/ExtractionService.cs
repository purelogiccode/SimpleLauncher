using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using SharpCompress.Archives;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using FileLock = SimpleLauncher.Core.Services.CheckForFileLock.CheckForFileLockService;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Core.Services.ExtractFiles;

/// <summary>
///     Extracts compressed game archives (7z, ZIP, RAR) to temporary or permanent locations,
///     with support for path traversal protection, disk space checks, and a 7-Zip fallback.
/// </summary>
public class ExtractionService : IExtractionService
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly string _tempFolder = Path.Combine(Path.GetTempPath(), "SimpleLauncher");

    /// <summary>
    ///     Initializes a new instance of <see cref="ExtractionService" />.
    /// </summary>
    /// <param name="messageBoxLibrary">Service for displaying user-facing message boxes.</param>
    /// <param name="logger">Error logging service.</param>
    public ExtractionService(IMessageBoxLibraryService messageBoxLibrary, ILogger logger)
    {
        _messageBoxLibrary = messageBoxLibrary;
        _logger = logger;
    }

    /// <summary>
    ///     Extracts an archive to a temporary folder and returns the path of a suitable game file within it, if one is found.
    /// </summary>
    /// <param name="archivePath">The full path to the archive file.</param>
    /// <param name="fileFormatsToLaunch">The list of file formats to look for after extraction.</param>
    /// <returns>
    ///     A tuple containing the path of the game file to launch and the temporary extraction directory, or null values
    ///     on failure.
    /// </returns>
    public async Task<(string? gameFilePath, string? tempDirectoryPath)> ExtractToTempAndGetLaunchFileAsync(
        string archivePath, IList<string> fileFormatsToLaunch)
    {
        var pathToExtractionDirectory = await ExtractToTempAsync(archivePath);

        if (string.IsNullOrEmpty(pathToExtractionDirectory) || !Directory.Exists(pathToExtractionDirectory))
        {
            _logger.Debug(
                $"[ExtractionService] Extraction failed for {archivePath}. No temp directory created or invalid path returned.");
            return (null, null);
        }

        var extractedFileToLaunch = await ValidateAndFindGameFileAsync(pathToExtractionDirectory, fileFormatsToLaunch);
        if (!string.IsNullOrEmpty(extractedFileToLaunch)) return (extractedFileToLaunch, pathToExtractionDirectory);

        _logger.Debug(
            $"[ExtractionService] No suitable game file found in extracted directory {pathToExtractionDirectory}.");
        return (null, pathToExtractionDirectory);
    }

    /// <summary>
    ///     Extracts an archive to the specified destination folder with retry logic for file locks,
    ///     disk space validation, and path traversal protection.
    /// </summary>
    /// <param name="archivePath">The full path to the archive file.</param>
    /// <param name="destinationFolder">The target folder for extraction.</param>
    /// <returns>True if extraction succeeded; otherwise false.</returns>
    public async Task<bool> ExtractToFolderAsync(string archivePath, string destinationFolder)
    {
        if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath) || new FileInfo(archivePath).Length == 0)
        {
            // Notify developer
            const string contextMessage = "File path is invalid.";
            _logger.Warning(contextMessage);

            // Notify user
            await _messageBoxLibrary.DownloadedFileIsMissingMessageBoxAsync();

            return false;
        }

        // Resolve the destination folder using PathHelper
        var resolvedDestinationFolder = PathHelper.ResolveRelativeToAppDirectory(destinationFolder);

        if (string.IsNullOrEmpty(resolvedDestinationFolder))
        {
            // Notify developer
            const string contextMessage = "Destination folder path resolution failed.";
            _logger.Warning(contextMessage);

            // Notify user
            await _messageBoxLibrary.ExtractionFailedMessageBoxAsync();

            return false;
        }

        // Add a retry loop to handle transient file locks (e.g., from antivirus)
        const int maxRetries = 10;
        const int retryDelayMs = 1000;
        for (var i = 0; i < maxRetries; i++)
        {
            if (!FileLock.IsFileLocked(archivePath)) break; // File is not locked, proceed

            if (i == maxRetries - 1)
            {
                // Last attempt failed
                // Notify developer
                var contextMessage =
                    $"The downloaded file appears to be locked after {maxRetries} retries: {archivePath}";
                _logger.Warning(contextMessage);

                // Notify user, passing the directory of the locked archive
                await _messageBoxLibrary.FileIsLockedMessageBoxAsync(Path.GetDirectoryName(archivePath));

                return false;
            }

            await Task.Delay(retryDelayMs); // Wait before retrying
        }

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        if (!IsSupportedArchivePath(archivePath))
        {
            // Notify developer
            // Expected user-error condition (unsupported input): per repo policy it must not be
            // reported as a bug, so log it at Information, below the bug-report sink threshold
            // (bug #67537 reported this as a Warning).
            var contextMessage = $"Only 7z, ZIP, and RAR files are supported by this extraction method.\n" +
                                 $"File type: {extension}";
            _logger.Information(contextMessage);

            // Notify user
            await _messageBoxLibrary.FileNeedToBeCompressedMessageBoxAsync();

            return false;
        }

        // Tracks only files written by this extraction run so failure cleanup
        // never deletes pre-existing user files (CORE-01).
        var extractedFiles = new List<string>();

        // Set when one of this method's own guards (disk space / disk-space check)
        // aborts the run: the 7za fallback performs no such check, so it must not
        // run after a guard failure and bypass the guard (CORE-13).
        var guardFailure = false;

        try
        {
            try
            {
                // Use the resolved destination folder for creation
                Directory.CreateDirectory(resolvedDestinationFolder);
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex, $"Failed to create directory: {resolvedDestinationFolder}");

                // Do not continue when the destination cannot be created (CORE-01).
                await _messageBoxLibrary.ExtractionFailedMessageBoxAsync();

                return false;
            }

            // Create a tracking file in the resolved destination folder
            var extractionTrackingFile = Path.Combine(resolvedDestinationFolder, ".extraction_in_progress");
            await File.WriteAllTextAsync(extractionTrackingFile, DateTime.Now.ToString(CultureInfo.InvariantCulture));

            await Task.Run(async () =>
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                var entries = archive.Entries.ToList();

                if (entries.Count == 0) throw new InvalidDataException("The archive file contains no entries.");

                var estimatedSize = (long)(entries.Where(static e => !e.IsDirectory).Sum(static e => e.Size) * 1.2);

                // Check disk space using the resolved destination folder. UNC roots
                // (\\server\share) cannot be represented by DriveInfo — the constructor
                // throws and would abort a valid extraction — so the advisory pre-check
                // is skipped for network destinations.
                var rootPath = Path.GetPathRoot(resolvedDestinationFolder);
                var isUncDestination = !string.IsNullOrEmpty(rootPath) &&
                                       rootPath.StartsWith(@"\\", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(rootPath) && !isUncDestination)
                {
                    try
                    {
                        var drive = new DriveInfo(rootPath);
                        if (drive.IsReady && drive.AvailableFreeSpace < estimatedSize)
                        {
                            // Notify developer
                            var contextMessage =
                                $"Not enough disk space for extraction. Required: {estimatedSize / (1024 * 1024)} MB, Available: {drive.AvailableFreeSpace / (1024 * 1024)} MB";
                            _logger.Warning(contextMessage);

                            // Notify user
                            await _messageBoxLibrary.DiskSpaceErrorMessageBoxAsync();

                            guardFailure = true;
                            throw new IOException("Insufficient disk space.");
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        // Notify developer
                        _logger.Error(ex,
                            $"Unable to check disk space for path {resolvedDestinationFolder}: {ex.Message}");

                        // Notify user
                        await _messageBoxLibrary.CouldNotCheckForDiskSpaceMessageBoxAsync();

                        guardFailure = true;
                        throw new IOException($"Unable to check disk space for path {resolvedDestinationFolder}", ex);
                    }
                }

                // Path traversal check
                var fullResolvedDestFolder = PathHelper.ResolveRelativeToAppDirectory(resolvedDestinationFolder);
                if (fullResolvedDestFolder != null &&
                    !fullResolvedDestFolder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    fullResolvedDestFolder += Path.DirectorySeparatorChar;
                }

                foreach (var entry in entries)
                {
                    if (entry.Key != null)
                    {
                        // Fail closed: any unresolvable path is treated as dangerous (CORE-02).
                        var entryDestinationPath =
                            GetSafeEntryPath(resolvedDestinationFolder, fullResolvedDestFolder, entry.Key);
                        if (entryDestinationPath == null)
                        {
                            // Notify user
                            await _messageBoxLibrary.PotentialPathManipulationDetectedMessageBoxAsync(archivePath);
                            throw new SecurityException($"Potentially dangerous zip entry path: {entry.Key}");
                        }
                    }
                }

                // Extract all entries
                foreach (var entry in entries)
                {
                    if (entry.IsDirectory) continue;

                    if (entry.Key != null)
                    {
                        // Re-validate per entry: never trust the first pass alone (CORE-02).
                        var destinationPath =
                            GetSafeEntryPath(resolvedDestinationFolder, fullResolvedDestFolder, entry.Key);
                        if (destinationPath == null)
                        {
                            await _messageBoxLibrary.PotentialPathManipulationDetectedMessageBoxAsync(archivePath);
                            throw new SecurityException($"Potentially dangerous zip entry path: {entry.Key}");
                        }

                        var directory = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            Directory.CreateDirectory(directory);

                        await using (var entryStream = await entry.OpenEntryStreamAsync())
                        await using (var fileStream = File.Create(destinationPath))
                        {
                            // Track the file as soon as it exists on disk: if the copy fails
                            // halfway the partial file must still be cleaned up.
                            extractedFiles.Add(destinationPath);
                            await entryStream.CopyToAsync(fileStream);
                        }

                        // Preserve file time if available
                        if (entry.LastModifiedTime.HasValue)
                            File.SetLastWriteTime(destinationPath, entry.LastModifiedTime.Value);
                    }
                }
            });

            if (File.Exists(extractionTrackingFile)) await DeleteFiles.TryDeleteFileAsync(extractionTrackingFile);

            return true;
        }
        catch (Exception ex)
        {
            // For .7z files, try fallback extraction with 7za executable.
            // Never fall back after one of our own guards fired: 7za performs neither the
            // path traversal check nor the disk space check, so it would bypass them (CORE-13).
            if (string.Equals(extension, ".7z", StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(resolvedDestinationFolder) &&
                ex is not SecurityException &&
                !guardFailure)
            {
                _logger.Debug(
                    $"[ExtractionService] SharpCompress failed for .7z file, trying 7za fallback: {archivePath}");
                var fallbackSuccess = await ExtractWith7ZipAsync(archivePath, resolvedDestinationFolder);
                if (fallbackSuccess)
                {
                    _logger.Debug($"[ExtractionService] 7za fallback extraction succeeded for: {archivePath}");
                    var extractionTrackingFile = Path.Combine(resolvedDestinationFolder, ".extraction_in_progress");
                    if (File.Exists(extractionTrackingFile))
                        await DeleteFiles.TryDeleteFileAsync(extractionTrackingFile);

                    return true;
                }

                _logger.Debug($"[ExtractionService] 7za fallback extraction also failed for: {archivePath}");
            }

            if (!string.IsNullOrEmpty(resolvedDestinationFolder)) // Only attempt cleanup if resolution was successful
            {
                try
                {
                    // Only remove files written by this run plus the tracking marker.
                    // Never wipe the whole destination folder: it may contain pre-existing
                    // user files (CORE-01). The marker is removed last so a failed cleanup
                    // stays recognizable for CleanupPartialExtractionAsync.
                    foreach (var extractedFile in extractedFiles)
                    {
                        try
                        {
                            if (File.Exists(extractedFile))
                                await DeleteFiles.TryDeleteFileAsync(extractedFile);
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.Error(cleanupEx, $"Failed to clean up partial extraction file: {extractedFile}");
                        }
                    }

                    var extractionTrackingFile = Path.Combine(resolvedDestinationFolder, ".extraction_in_progress");
                    if (File.Exists(extractionTrackingFile))
                        await DeleteFiles.TryDeleteFileAsync(extractionTrackingFile);
                }
                catch (Exception cleanupEx)
                {
                    // Notify developer
                    var contextMessage = $"Failed to clean up partial extraction in: {resolvedDestinationFolder}";
                    _logger.Error(cleanupEx, contextMessage);
                }
            }

            // Notify developer
            var exceptionDetails = GetDetailedExceptionInfo(ex);
            _logger.Error(ex, $"Error extracting the file: {archivePath}\n{exceptionDetails}");

            // Notify user
            await _messageBoxLibrary.ExtractionFailedMessageBoxAsync();

            return false;
        }
    }

    private async Task<string?> ExtractToTempAsync(string archivePath)
    {
        if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
        {
            // Notify developer
            const string contextMessage = "Archive path cannot be null, empty, or file not found.";
            _logger.Warning(contextMessage);

            // Notify user
            await _messageBoxLibrary.ExtractionFailedMessageBoxAsync();

            return null;
        }

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        if (!IsSupportedArchivePath(archivePath))
        {
            // Expected user-error condition (unsupported input): log at Information so it is
            // never picked up by the bug-report service (bug #67537); the user already gets
            // a message box explaining the supported formats.
            _logger.Information(
                $"Only 7z, ZIP, and RAR files are supported by this extraction method. File type: {extension}");

            // Notify user
            await _messageBoxLibrary.FileNeedToBeCompressedMessageBoxAsync();

            return null;
        }

        string? tempDirectory = null;

        try
        {
            if (string.IsNullOrEmpty(_tempFolder))
            {
                // Notify developer
                const string contextMessage = "Temp folder resolution failed.";
                _logger.Warning(contextMessage);

                // Notify user
                await _messageBoxLibrary.ExtractionFailedMessageBoxAsync();

                return null;
            }

            var randomName = Path.GetRandomFileName();
            if (randomName.Contains("..", StringComparison.Ordinal) || randomName.Contains('/') ||
                randomName.Contains('\\'))
            {
                randomName = Guid.NewGuid().ToString("N");
            }

            tempDirectory = Path.Combine(_tempFolder, randomName);
            Directory.CreateDirectory(tempDirectory);

            await Task.Run(() =>
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);

                // First, validate for path traversal before extracting
                var fullTempDir = Path.GetFullPath(tempDirectory);
                if (!fullTempDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    fullTempDir += Path.DirectorySeparatorChar;

                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) continue;

                    if (entry.Key != null)
                    {
                        var fullDestPath = Path.GetFullPath(
                            Path.Combine(fullTempDir, NormalizeArchiveEntryName(entry.Key)));
                        if (!fullDestPath.StartsWith(fullTempDir, ArchivePathComparison))
                        {
                            throw new SecurityException(
                                $"Potential path traversal detected in archive entry: {entry.Key}");
                        }
                    }
                }

                // If validation passes, extract the archive.
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) continue;

                    if (entry.Key != null)
                    {
                        var destinationPath = Path.Combine(tempDirectory, NormalizeArchiveEntryName(entry.Key));
                        var directory = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            Directory.CreateDirectory(directory);

                        using (var entryStream = entry.OpenEntryStream())
                        using (var fileStream = File.Create(destinationPath))
                        {
                            entryStream.CopyTo(fileStream);
                        }

                        // Preserve file time if available
                        if (entry.LastModifiedTime.HasValue)
                            File.SetLastWriteTime(destinationPath, entry.LastModifiedTime.Value);
                    }
                }
            });

            return tempDirectory;
        }
        catch (Exception ex)
        {
            // For .7z files, try fallback extraction with 7za executable.
            // A path traversal rejection must not fall through to 7za: it performs no
            // such check, so the archive would be extracted anyway (CORE-13).
            if (string.Equals(extension, ".7z", StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(tempDirectory) &&
                ex is not SecurityException)
            {
                _logger.Debug(
                    $"[ExtractionService] SharpCompress failed for .7z file, trying 7za fallback: {archivePath}");
                var fallbackSuccess = await ExtractWith7ZipAsync(archivePath, tempDirectory);
                if (fallbackSuccess)
                {
                    _logger.Debug($"[ExtractionService] 7za fallback extraction succeeded for: {archivePath}");
                    return tempDirectory;
                }

                _logger.Debug($"[ExtractionService] 7za fallback extraction also failed for: {archivePath}");
            }

            await CleanTempFolder.CleanupTempDirectoryAsync(tempDirectory);

            // Notify developer
            const string contextMessage =
                "Extraction of the compressed file failed. The file may be corrupted or a security issue was detected.";
            _logger.Error(ex, contextMessage);

            // Notify user
            await _messageBoxLibrary.ExtractionFailedMessageBoxAsync();

            return null;
        }
    }

    internal static string GetSevenZipExecutableName(Architecture architecture, bool isWindows)
    {
        if (isWindows)
            return architecture == Architecture.Arm64 ? "7za_arm64.exe" : "7za.exe";

        return architecture == Architecture.Arm64 ? "7zz_arm64" : "7zz";
    }

    /// <summary>
    ///     Returns true when the path carries an archive extension this service can extract
    ///     (7z, ZIP, RAR). Everything else — e.g. a Linux .AppImage emulator download — must
    ///     not be routed through the extraction methods (bug #67537).
    /// </summary>
    internal static bool IsSupportedArchivePath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".rar", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Archive entry names authored on Windows use backslashes ("DIR\FILE.BIN"); on Unix
    ///     they are ordinary characters, so the entry would extract as a single flat file with
    ///     a literal backslash in its name. Normalize to the platform separator before
    ///     combining or validating destinations (LB-16).
    /// </summary>
    internal static string NormalizeArchiveEntryName(string entryKey)
    {
        return entryKey.Contains('\\') ? entryKey.Replace('\\', Path.DirectorySeparatorChar) : entryKey;
    }

    private static StringComparison ArchivePathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    ///     Adds the execute bits to a file on Unix (best effort): git stores the bundled 7zz
    ///     as 100644 and only release packaging restores it, so Debug/dotnet-run builds would
    ///     fail with EACCES — swallowed at Debug level, leaving only a generic extraction
    ///     error (LB-15).
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    internal static void EnsureExecuteBits(string exePath)
    {
        const UnixFileMode executeBits =
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        var mode = File.GetUnixFileMode(exePath);
        if ((mode & executeBits) != executeBits)
            File.SetUnixFileMode(exePath, mode | executeBits);
    }

    [UnsupportedOSPlatform("windows")]
    private void TryMakeExecutable(string exePath)
    {
        try
        {
            EnsureExecuteBits(exePath);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[ExtractionService] Could not set the execute bit on {exePath}: {ex.Message}");
        }
    }

    private async Task<bool> ExtractWith7ZipAsync(string archivePath, string destinationFolder)
    {
        var exeName = GetSevenZipExecutableName(RuntimeInformation.ProcessArchitecture, OperatingSystem.IsWindows());
        var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "SevenZip", exeName);

        if (!File.Exists(exePath))
        {
            _logger.Debug($"[ExtractionService] 7-Zip executable not found at: {exePath}");
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            TryMakeExecutable(exePath);
        }

        try
        {
            Directory.CreateDirectory(destinationFolder);

            var args = $"x -o\"{destinationFolder}\" -y \"{archivePath}\"";
            var processStartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            _logger.Debug($"[ExtractionService] Running 7za fallback for: {archivePath}");
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("[ExtractionService] 7za extraction timed out after 30 minutes");
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    Log.Debug($"[ExtractionService] process.Kill() failed: {ex.Message}");
                }

                return false;
            }

            if (process.ExitCode == 0)
            {
                _logger.Debug($"[ExtractionService] 7za extraction succeeded for: {archivePath}");
                return true;
            }

            _logger.Debug(
                $"[ExtractionService] 7za extraction failed. ExitCode: {process.ExitCode}. Error: {errorBuilder} Output: {outputBuilder}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[ExtractionService] 7za extraction exception: {ex.Message}");
            _logger.Error(ex, $"Error extracting with 7za: {archivePath}");
            return false;
        }
    }

    /// <summary>
    ///     Resolves an archive entry to a destination path contained in the destination folder.
    ///     Returns null when the entry is missing, unresolvable, or escapes the folder (fail closed).
    /// </summary>
    private static string? GetSafeEntryPath(string resolvedDestinationFolder, string? fullResolvedDestFolder,
        string entryKey)
    {
        if (string.IsNullOrEmpty(entryKey) || fullResolvedDestFolder == null)
            return null;

        string entryDestinationPath;
        try
        {
            entryDestinationPath = Path.GetFullPath(
                Path.Combine(resolvedDestinationFolder, NormalizeArchiveEntryName(entryKey)));
        }
        catch
        {
            return null;
        }

        var fullDestPath = PathHelper.ResolveRelativeToAppDirectory(entryDestinationPath);
        if (fullDestPath == null)
            return null;

        if (!fullDestPath.StartsWith(fullResolvedDestFolder, ArchivePathComparison))
            return null;

        return entryDestinationPath;
    }

    private static string GetDetailedExceptionInfo(Exception ex)
    {
        var sb = new StringBuilder();

        var currentEx = ex;
        var level = 0;

        while (currentEx != null)
        {
            // Corrected the ambiguous invocation by using FormattableString.Invariant
            sb.AppendLine(
                FormattableString.Invariant($"[Level {level}] {currentEx.GetType().Name}: {currentEx.Message}"));
            level++;
            currentEx = currentEx.InnerException;
        }

        return sb.ToString();
    }

    private async Task<string?> ValidateAndFindGameFileAsync(string tempExtractLocation,
        IList<string> fileFormatsToLaunch)
    {
        _logger.Debug($"[ValidateAndFindGameFileAsync] Validating extracted path: {tempExtractLocation}");
        if (string.IsNullOrEmpty(tempExtractLocation) || !Directory.Exists(tempExtractLocation))
        {
            // Notify developer
            var contextMessage = $"Extracted path is invalid: {tempExtractLocation}";
            _logger.Warning(contextMessage);
            _logger.Debug($"[ValidateAndFindGameFileAsync] Error: {contextMessage}");

            // Notify user
            await _messageBoxLibrary.ExtractionFailedMessageBoxAsync();

            return null;
        }

        string? foundFile = null;

        // First, try to find a file matching the specified formats, if any are provided.
        if (fileFormatsToLaunch is { Count: > 0 })
        {
            _logger.Debug(
                $"[ValidateAndFindGameFileAsync] Searching for formats: {string.Join(", ", fileFormatsToLaunch)} in {tempExtractLocation}");
            foreach (var formatToLaunch in fileFormatsToLaunch)
            {
                try
                {
                    var searchPattern = $"*{formatToLaunch}";
                    if (!formatToLaunch.StartsWith('.')) searchPattern = $"*.{formatToLaunch}";

                    // MatchCasing.CaseInsensitive: Windows-authored scene archives routinely use
                    // upper-case names (GAME.EXE, DISC.CUE), which a case-sensitive "*.cue" glob
                    // would miss on Linux. AttributesToSkip.None keeps hidden/system entries
                    // discoverable like the old SearchOption.AllDirectories overload did.
                    var files = Directory.EnumerateFiles(tempExtractLocation, searchPattern, new EnumerationOptions
                    {
                        MatchCasing = MatchCasing.CaseInsensitive,
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.None
                    }).ToList();
                    if (files.Count > 0)
                    {
                        foundFile = files[0]; // Take the first match
                        _logger.Debug(
                            $"[ValidateAndFindGameFileAsync] Found file matching format '{formatToLaunch}': {foundFile}");
                        return foundFile;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex,
                        $"Error searching for file format '{formatToLaunch}' in '{tempExtractLocation}'.");
                    _logger.Debug(
                        $"[ValidateAndFindGameFileAsync] Exception searching for {formatToLaunch}: {ex.Message}");
                    // Continue to next format or fallback if this one fails
                }
            }
        }
        else
        {
            _logger.Debug(
                $"[ValidateAndFindGameFileAsync] fileFormatsToLaunch is null or empty. Attempting to find any file in {tempExtractLocation}.");
        }

        // If no specific format was found, or no formats were specified, try to find any file.
        if (string.IsNullOrEmpty(foundFile))
        {
            try
            {
                var allFiles = Directory.EnumerateFiles(tempExtractLocation, "*", SearchOption.AllDirectories)
                    .OrderBy(static f => f, StringComparer.Ordinal).ToList();
                if (allFiles.Count > 0)
                {
                    foundFile = allFiles.First();
                    _logger.Debug(
                        $"[ValidateAndFindGameFileAsync] No specific format found/specified, picked first file: {foundFile}");
                    return foundFile;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error enumerating all files in {tempExtractLocation} as a fallback.");
                _logger.Debug($"[ValidateAndFindGameFileAsync] Error enumerating all files: {ex.Message}");
            }
        }

        // If still no file found after all attempts
        const string notFoundContext =
            "Could not find a file with any of the specified extensions (or any file at all) after extraction.";
        _logger.Error(new FileNotFoundException(notFoundContext), notFoundContext);
        _logger.Debug($"[ValidateAndFindGameFileAsync] Error: {notFoundContext}");

        await _messageBoxLibrary.CouldNotFindAFileMessageBoxAsync(); // This message is now more general.

        return null;
    }
}