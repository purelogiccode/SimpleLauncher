namespace SimpleLauncher.Core.Services.FileCopy;

/// <summary>
///     Copies a file over an existing destination in the most lock-tolerant way:
///     the data is written to a temporary file in the destination directory first,
///     the old destination is renamed aside, and the temporary file takes its place.
/// </summary>
/// <remarks>
///     <see cref="File.Copy(string, string, bool)" /> and replace-by-rename
///     (<see cref="File.Move(string, string, bool)" /> with <c>overwrite: true</c>)
///     both open the destination for writing, so they fail with a sharing violation or
///     ACCESS_DENIED while any process merely has that file open — an image viewer, the
///     Explorer preview pane or a thumbnail generator (bug 67779). Renaming the old file
///     aside only needs delete access, which common readers grant via delete sharing, so
///     the replacement succeeds in those cases. When the destination is locked without
///     delete sharing, nothing can replace it in place either: the operation falls back to
///     the previous in-place overwrite and its exception reaches the caller's retry loop.
/// </remarks>
public static class SafeFileCopy
{
    /// <summary>
    ///     Copies <paramref name="sourceFilePath" /> over <paramref name="destinationFilePath" />,
    ///     creating the destination directory when needed and never leaving a temporary or
    ///     backup file behind.
    /// </summary>
    /// <param name="sourceFilePath">The file to copy from.</param>
    /// <param name="destinationFilePath">The file to replace (or create) with the source content.</param>
    /// <exception cref="IOException">
    ///     The destination is locked without delete sharing and cannot be overwritten in place.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     The destination is locked without delete sharing or is not writable.
    /// </exception>
    public static void CopyOverwrite(string sourceFilePath, string destinationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationFilePath));
        if (string.IsNullOrEmpty(destinationDirectory))
            throw new ArgumentException("The destination path has no directory.", nameof(destinationFilePath));

        Directory.CreateDirectory(destinationDirectory);

        var tempFilePath = Path.Combine(destinationDirectory,
            $".{Path.GetFileName(destinationFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(sourceFilePath, tempFilePath, true);

            if (!File.Exists(destinationFilePath))
            {
                File.Move(tempFilePath, destinationFilePath);
                return;
            }

            // Rename the old destination aside: only needs delete access, so it succeeds
            // while a viewer holds the file open with delete sharing (unlike a direct copy
            // or a replace-by-rename, both of which need write access).
            var backupFilePath = $"{destinationFilePath}.{Guid.NewGuid():N}.old";
            try
            {
                File.Move(destinationFilePath, backupFilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The destination is locked without delete sharing: it cannot be renamed
                // aside. Fall back to the previous in-place overwrite; if the lock denies
                // writing as well, its exception reaches the caller's retry loop.
                File.Copy(tempFilePath, destinationFilePath, true);
                return;
            }

            try
            {
                File.Move(tempFilePath, destinationFilePath);
            }
            catch
            {
                // A failed replace must never lose the old image: put it back. If even
                // the restore fails, the backup file is the only copy left, so it is
                // deliberately not deleted.
                TryMoveFile(backupFilePath, destinationFilePath);
                throw;
            }

            TryDeleteFile(backupFilePath);
        }
        finally
        {
            TryDeleteFile(tempFilePath);
        }
    }

    private static void TryMoveFile(string sourceFilePath, string destinationFilePath)
    {
        try
        {
            File.Move(sourceFilePath, destinationFilePath);
        }
        catch
        {
            // Best effort restore.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort: a leftover temp file must never mask the real error.
        }
    }
}