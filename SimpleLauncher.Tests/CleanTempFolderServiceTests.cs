using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="CleanTempFolderService" /> using real temp directories
///     and a mocked <see cref="IDeleteFilesService" />.
/// </summary>
public class CleanTempFolderServiceTests : IDisposable
{
    private readonly Mock<IDeleteFilesService> _deleteFilesMock = new();
    private readonly CleanTempFolderService _service;
    private readonly string _tempDir;

    public CleanTempFolderServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SL_CleanTemp_{Guid.NewGuid():N}");
        _service = new CleanTempFolderService(_deleteFilesMock.Object);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    [Fact]
    public async Task CleanupTempDirectoryAsync_DeletesDirectoryRecursively()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub", "nested"));
        File.WriteAllText(Path.Combine(_tempDir, "file1.bin"), "data");
        File.WriteAllText(Path.Combine(_tempDir, "sub", "file2.bin"), "data");

        await _service.CleanupTempDirectoryAsync(_tempDir);

        Assert.False(Directory.Exists(_tempDir));
    }

    [Fact]
    public async Task CleanupTempDirectoryAsync_MissingDirectory_IsNoOp()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");
        await _service.CleanupTempDirectoryAsync(missing);
        Assert.False(Directory.Exists(missing));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public Task CleanupTempDirectoryAsync_NullOrEmptyPath_IsNoOp(string? path)
    {
        return _service.CleanupTempDirectoryAsync(path!);
        // No exception expected
    }

    [Fact]
    public async Task CleanupTempDirectoryAsync_LockedDirectory_DoesNotThrow()
    {
        Directory.CreateDirectory(_tempDir);
        // Simulate a locked directory that cannot be deleted
        await using (File.Open(Path.Combine(_tempDir, "locked.bin"), FileMode.Create, FileAccess.ReadWrite,
                         FileShare.None))
        {
            await _service.CleanupTempDirectoryAsync(_tempDir);
        }

        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public async Task CleanupPartialExtractionAsync_DeletesTrackingFileAndFiles()
    {
        Directory.CreateDirectory(_tempDir);
        var trackingFile = Path.Combine(_tempDir, ".extraction_in_progress");
        var file1 = Path.Combine(_tempDir, "game.iso");
        var file2 = Path.Combine(_tempDir, "game.cue");
        File.WriteAllText(trackingFile, "partial");
        File.WriteAllText(file1, "data");
        File.WriteAllText(file2, "data");
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);

        await _service.CleanupPartialExtractionAsync(_tempDir);

        _deleteFilesMock.Verify(x => x.TryDeleteFileAsync(trackingFile),
            Times.Exactly(2)); // explicit call + the file loop
        _deleteFilesMock.Verify(x => x.TryDeleteFileAsync(file1), Times.Once);
        _deleteFilesMock.Verify(x => x.TryDeleteFileAsync(file2), Times.Once);
        Assert.False(Directory.Exists(subDir));
    }

    [Fact]
    public async Task CleanupPartialExtractionAsync_NoTrackingFile_DoesNotCallDeleteForIt()
    {
        Directory.CreateDirectory(_tempDir);
        var file = Path.Combine(_tempDir, "game.iso");
        File.WriteAllText(file, "data");

        await _service.CleanupPartialExtractionAsync(_tempDir);

        // CORE-01 safeguard: without the marker nothing is deleted.
        _deleteFilesMock.Verify(
            x => x.TryDeleteFileAsync(
                It.Is<string>(p => p.EndsWith(".extraction_in_progress", StringComparison.Ordinal))), Times.Never);
        _deleteFilesMock.Verify(x => x.TryDeleteFileAsync(file), Times.Never);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task CleanupPartialExtractionAsync_MissingDirectory_IsNoOp()
    {
        await _service.CleanupPartialExtractionAsync(Path.Combine(_tempDir, "missing"));
        _deleteFilesMock.Verify(x => x.TryDeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public Task CleanupPartialExtractionAsync_DeleteServiceThrows_DoesNotThrow()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, ".extraction_in_progress"), "partial");
        File.WriteAllText(Path.Combine(_tempDir, "game.iso"), "data");
        _deleteFilesMock
            .Setup(x => x.TryDeleteFileAsync(It.IsAny<string>()))
            .ThrowsAsync(new IOException("File is locked"));

        return _service.CleanupPartialExtractionAsync(_tempDir);
        // No exception expected; cleanup is best-effort
    }
}