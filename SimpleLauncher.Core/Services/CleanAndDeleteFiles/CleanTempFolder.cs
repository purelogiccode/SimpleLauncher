namespace SimpleLauncher.Core.Services.CleanAndDeleteFiles;

/// <summary>
///     Provides cleanup routines for the application's temporary directories.
/// </summary>
public static class CleanTempFolder
{
    /// <summary>
    ///     Cleans up the specified temporary directory by removing all its contents and the directory itself.
    /// </summary>
    /// <param name="directoryPath">The path to the temporary directory to be cleaned up.</param>
    public static async Task CleanupTempDirectoryAsync(string? directoryPath)
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
    ///     Cleans up partially extracted files from a failed extraction.
    ///     WARNING: only call this for isolated temporary directories created for a single
    ///     extraction run. Never call it on user content folders (ROM/download folders):
    ///     it deletes all files and subdirectories. <see cref="SimpleLauncher.Core.Services.ExtractFiles.ExtractionService"/>
    ///     no longer uses this for user destinations (CORE-01) and deletes only tracked files instead.
    ///     As a safeguard, this is a no-op when the <c>.extraction_in_progress</c> marker is absent.
    /// </summary>
    /// <param name="directoryPath">Isolated temp directory containing partial extraction</param>
    public static async Task CleanupPartialExtractionAsync(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return;

        try
        {
            // Safeguard (CORE-01): without the marker we cannot know this directory
            // belongs to a failed extraction run, so never wipe it.
            var trackingFile = Path.Combine(directoryPath, ".extraction_in_progress");
            if (!File.Exists(trackingFile)) return;

            await DeleteFiles.TryDeleteFileAsync(trackingFile);

            // Delete all files in the directory
            foreach (var file in Directory.GetFiles(directoryPath)) await DeleteFiles.TryDeleteFileAsync(file);

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