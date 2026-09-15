using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.CleanAndDeleteFiles;

/// <summary>
///     Provides operations to clean up temporary directories used during file extraction.
/// </summary>
public class CleanTempFolderService : ICleanTempFolderService
{
    private readonly IDeleteFilesService _deleteFilesService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CleanTempFolderService" /> class.
    /// </summary>
    /// <param name="deleteFilesService">The service used to delete individual files.</param>
    public CleanTempFolderService(IDeleteFilesService deleteFilesService)
    {
        _deleteFilesService = deleteFilesService;
    }

    /// <summary>
    ///     Deletes the specified temporary directory and all its contents.
    /// </summary>
    /// <param name="directoryPath">The path of the temporary directory to delete.</param>
    public async Task CleanupTempDirectoryAsync(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return;

        try
        {
            await Task.Run(() => Directory.Delete(directoryPath, true));
        }
#pragma warning disable RCS1075
        catch (Exception)
#pragma warning restore RCS1075
        {
            // Ignore - this is cleanup code
        }
    }

    /// <summary>
    ///     Cleans up a partially extracted isolated temp directory by removing the tracking file, all files, and subdirectories.
    ///     WARNING: only for isolated temp directories. Never call on user content folders (CORE-01).
    ///     No-op when the <c>.extraction_in_progress</c> marker is absent.
    /// </summary>
    /// <param name="directoryPath">Isolated temp directory to clean up.</param>
    public async Task CleanupPartialExtractionAsync(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return;

        try
        {
            // Safeguard (CORE-01): without the marker we cannot know this directory
            // belongs to a failed extraction run, so never wipe it.
            var trackingFile = Path.Combine(directoryPath, ".extraction_in_progress");
            if (!File.Exists(trackingFile)) return;

            await _deleteFilesService.TryDeleteFileAsync(trackingFile);

            // Delete all files in the directory
            foreach (var file in Directory.GetFiles(directoryPath)) await _deleteFilesService.TryDeleteFileAsync(file);

            // Recursively delete subdirectories
            foreach (var subDir in Directory.GetDirectories(directoryPath))
                await Task.Run(() => Directory.Delete(subDir, true));
        }
#pragma warning disable RCS1075
        catch (Exception)
#pragma warning restore RCS1075
        {
            // Ignore - this is cleanup code
        }
    }
}