using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Extended tests for <see cref="CleanTempFolder" /> covering nested directories, hidden files,
///     system files, and repeated cleanup operations.
/// </summary>
public class CleanTempFolderExtendedTests
{
    /// <summary>
    ///     Verifies that CleanupTempDirectoryAsync deletes deeply nested directory structures.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncWithNestedDirectoriesDeletesAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var nested1 = Path.Combine(tempDir, "level1");
        var nested2 = Path.Combine(nested1, "level2");
        var nested3 = Path.Combine(nested2, "level3");
        Directory.CreateDirectory(nested3);

        var file = Path.Combine(nested3, "deep.txt");
        await File.WriteAllTextAsync(file, "deep content");

        Assert.True(Directory.Exists(nested3));

        await CleanTempFolder.CleanupTempDirectoryAsync(tempDir);

        Assert.False(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupTempDirectoryAsync deletes all files when the directory contains many files.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncWithMultipleFilesDeletesAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        for (var i = 0; i < 100; i++)
            await File.WriteAllTextAsync(Path.Combine(tempDir, $"file{i}.txt"), $"content {i}");

        Assert.Equal(100, Directory.GetFiles(tempDir).Length);

        await CleanTempFolder.CleanupTempDirectoryAsync(tempDir);

        Assert.False(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync removes all files and subdirectories from multiple subdirectories.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncWithMultipleSubdirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sub1 = Path.Combine(tempDir, "sub1");
        var sub2 = Path.Combine(tempDir, "sub2");
        var sub3 = Path.Combine(tempDir, "sub3");
        Directory.CreateDirectory(sub1);
        Directory.CreateDirectory(sub2);
        Directory.CreateDirectory(sub3);
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".extraction_in_progress"), "in progress");

        await File.WriteAllTextAsync(Path.Combine(sub1, "file1.txt"), "content1");
        await File.WriteAllTextAsync(Path.Combine(sub2, "file2.txt"), "content2");
        await File.WriteAllTextAsync(Path.Combine(sub3, "file3.txt"), "content3");

        await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);

        Assert.False(Directory.Exists(sub1));
        Assert.False(Directory.Exists(sub2));
        Assert.False(Directory.Exists(sub3));
        Assert.True(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync removes the tracking file and all other files.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncWithTrackingFileAndOtherFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        await File.WriteAllTextAsync(Path.Combine(tempDir, ".extraction_in_progress"), "in progress");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "game.iso"), "iso content");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "readme.txt"), "readme");

        await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);

        Assert.False(File.Exists(Path.Combine(tempDir, ".extraction_in_progress")));
        Assert.False(File.Exists(Path.Combine(tempDir, "game.iso")));
        Assert.False(File.Exists(Path.Combine(tempDir, "readme.txt")));
        Assert.True(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync handles an empty directory without throwing.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncEmptyDirectoryDoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var ex = await Record.ExceptionAsync(() => CleanTempFolder.CleanupPartialExtractionAsync(tempDir));
        Assert.Null(ex);
        Assert.True(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupTempDirectoryAsync deletes hidden files.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncWithHiddenFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var hiddenFile = Path.Combine(tempDir, "hidden.txt");
        await File.WriteAllTextAsync(hiddenFile, "hidden content");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);

        await CleanTempFolder.CleanupTempDirectoryAsync(tempDir);

        Assert.False(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupTempDirectoryAsync deletes files with the System attribute.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncWithSystemFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var systemFile = Path.Combine(tempDir, "system.txt");
        await File.WriteAllTextAsync(systemFile, "system content");
        File.SetAttributes(systemFile, FileAttributes.System);

        await CleanTempFolder.CleanupTempDirectoryAsync(tempDir);

        Assert.False(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that CleanupPartialExtractionAsync handles a deeply nested directory structure.
    /// </summary>
    [Fact]
    public async Task CleanupPartialExtractionAsyncWithDeeplyNestedStructure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var current = tempDir;
        for (var i = 0; i < 10; i++) current = Path.Combine(current, $"level{i}");

        Directory.CreateDirectory(current);
        await File.WriteAllTextAsync(Path.Combine(tempDir, ".extraction_in_progress"), "in progress");

        await File.WriteAllTextAsync(Path.Combine(current, "deep.txt"), "deep content");

        await CleanTempFolder.CleanupPartialExtractionAsync(tempDir);

        Assert.False(Directory.Exists(Path.Combine(tempDir, "level0")));
        Assert.True(Directory.Exists(tempDir));
    }

    /// <summary>
    ///     Verifies that calling CleanupTempDirectoryAsync twice on the same directory does not throw.
    /// </summary>
    [Fact]
    public async Task CleanupTempDirectoryAsyncTwiceDoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "file.txt"), "content");

        await CleanTempFolder.CleanupTempDirectoryAsync(tempDir);
        Assert.False(Directory.Exists(tempDir));

        // Second call should not throw
        var ex = await Record.ExceptionAsync(() => CleanTempFolder.CleanupTempDirectoryAsync(tempDir));
        Assert.Null(ex);
    }
}