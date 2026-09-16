using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests the <see cref="CleanTempFolder" /> utility for cleaning temporary directories and partial extractions.
/// </summary>
public class CleanTempFolderTests
{
    /// <summary>
    ///     Verifies that passing a null path to CleanupTempDirectoryAsync does not throw.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncNullPathDoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(static () => CleanTempFolder.CleanupTempDirectoryAsync(null));
        Assert.Null(ex);
    }

    /// <summary>
    ///     Verifies that passing an empty path to CleanupTempDirectoryAsync does not throw.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncEmptyPathDoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(static () => CleanTempFolder.CleanupTempDirectoryAsync(""));
        Assert.Null(ex);
    }

    /// <summary>
    ///     Verifies that passing a non-existent path to CleanupTempDirectoryAsync does not throw.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncNonExistentPathDoesNotThrow()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent");
        var ex = await Record.ExceptionAsync(() => CleanTempFolder.CleanupTempDirectoryAsync(fakePath));
        Assert.Null(ex);
    }

    /// <summary>
    ///     Verifies that CleanupTempDirectoryAsync deletes an existing directory and its contents.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncExistingDirectoryDeletesDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var nestedFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(nestedFile, "test");

        Assert.True(Directory.Exists(tempDir));

        await CleanTempFolder.CleanupTempDirectoryAsync(tempDir);

        Assert.False(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that passing a null path to CleanupPartialExtractionAsync does not throw.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncNullPathDoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(static () => CleanTempFolder.CleanupPartialExtractionAsync(null!));
        Assert.Null(ex);
    }

    /// <summary>
    ///     Verifies that passing an empty path to CleanupPartialExtractionAsync does not throw.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncEmptyPathDoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(static () => CleanTempFolder.CleanupPartialExtractionAsync(""));
        Assert.Null(ex);
    }

    /// <summary>
    ///     Verifies that passing a non-existent path to CleanupPartialExtractionAsync does not throw.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncNonExistentPathDoesNotThrow()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent");
        var ex = await Record.ExceptionAsync(() => CleanTempFolder.CleanupPartialExtractionAsync(fakePath));
        Assert.Null(ex);
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync deletes the tracking file and all other files.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncDeletesTrackingFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var trackingFile = Path.Combine(tempDir, ".extraction_in_progress");
        await File.WriteAllTextAsync(trackingFile, "in progress");

        Assert.True(File.Exists(trackingFile));

        await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);

        Assert.False(File.Exists(trackingFile));
        Assert.True(Directory.Exists(tempDir)); // Directory itself remains
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync deletes files and subdirectories while preserving the root directory.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncDeletesFilesAndSubdirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".extraction_in_progress"), "in progress");

        var file1 = Path.Combine(tempDir, "file1.txt");
        var file2 = Path.Combine(subDir, "file2.txt");
        await File.WriteAllTextAsync(file1, "content1");
        await File.WriteAllTextAsync(file2, "content2");

        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));
        Assert.True(Directory.Exists(subDir));

        await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);

        // Files should be deleted, subdirectories too, but the root dir stays
        Assert.False(File.Exists(file1));
        Assert.False(File.Exists(file2));
        Assert.False(Directory.Exists(subDir));
        Assert.True(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync deletes read-only files.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncReadOnlyFilesDeletesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".extraction_in_progress"), "in progress");
        var readOnlyFile = Path.Combine(tempDir, "readonly.txt");
        await File.WriteAllTextAsync(readOnlyFile, "content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        Assert.True(File.Exists(readOnlyFile));

        await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);

        Assert.False(File.Exists(readOnlyFile), "Read-only file should be deleted after cleanup.");
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync is a no-op without the tracking marker (CORE-01 safeguard).
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncWithoutTrackingFilePreservesFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var file1 = Path.Combine(tempDir, "keep.txt");
        await File.WriteAllTextAsync(file1, "content");

        await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);

        Assert.True(File.Exists(file1));
        Assert.True(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that the tracking marker is deleted LAST: when a subdirectory cannot be deleted
    ///     (a file inside it is locked), the marker must survive so the directory is still
    ///     recognizable as a partial extraction on the next cleanup attempt. Windows-only:
    ///     FileShare.None locks are enforced only there.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncKeepsTrackingFileWhenCleanupFails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var subDir = Path.Combine(tempDir, "locked");
        Directory.CreateDirectory(subDir);
        var trackingFile = Path.Combine(tempDir, ".extraction_in_progress");
        await File.WriteAllTextAsync(trackingFile, "in progress");

        var lockedFile = Path.Combine(subDir, "locked.bin");
        await File.WriteAllTextAsync(lockedFile, "content");

        using (new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);
        }

        Assert.True(File.Exists(trackingFile),
            "The safeguard marker must be deleted last so a failed cleanup stays recognizable.");
    }
}