using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the <see cref="CleanSimpleLauncherFolderService" /> class.
/// </summary>
public class CleanSimpleLauncherFolderServiceTests
{
    /// <summary>
    ///     Verifies that the constructor does not throw when given a valid delete service.
    /// </summary>
    [Fact]
    public void ConstructorDoesNotThrow()
    {
        var service = new CleanSimpleLauncherFolderService(new NoOpDeleteFilesService());
        Assert.NotNull(service);
    }

    /// <summary>
    ///     Verifies that the service implements the ICleanSimpleLauncherFolderService interface.
    /// </summary>
    [Fact]
    public void ImplementsICleanSimpleLauncherFolderService()
    {
        var service = new CleanSimpleLauncherFolderService(new NoOpDeleteFilesService());
        Assert.IsType<ICleanSimpleLauncherFolderService>(service, exactMatch: false);
    }

    /// <summary>
    ///     Verifies that CleanupTrash does not throw when called on a fresh instance.
    /// </summary>
    [Fact]
    public void CleanupTrashDoesNotThrow()
    {
        var service = new CleanSimpleLauncherFolderService(new NoOpDeleteFilesService());
        var exception = Record.Exception(service.CleanupTrash);
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that CleanupTempFiles does not throw when called on a fresh instance.
    /// </summary>
    [Fact]
    public void CleanupTempFilesDoesNotThrow()
    {
        var service = new CleanSimpleLauncherFolderService(new NoOpDeleteFilesService());
        var exception = Record.Exception(service.CleanupTempFiles);
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that calling CleanupTrash twice does not throw.
    /// </summary>
    [Fact]
    public void CleanupTrashCalledTwiceDoesNotThrow()
    {
        var service = new CleanSimpleLauncherFolderService(new NoOpDeleteFilesService());
        service.CleanupTrash();
        var exception = Record.Exception(service.CleanupTrash);
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that calling CleanupTempFiles twice does not throw.
    /// </summary>
    [Fact]
    public void CleanupTempFilesCalledTwiceDoesNotThrow()
    {
        var service = new CleanSimpleLauncherFolderService(new NoOpDeleteFilesService());
        service.CleanupTempFiles();
        var exception = Record.Exception(service.CleanupTempFiles);
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that CleanupTempFiles deletes the SimpleLauncher temp directory and its contents.
    /// </summary>
    [Fact]
    public void CleanupTempFilesCallsDeleteForTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SimpleLauncher");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.txt");
        File.WriteAllText(tempFile, "test");

        try
        {
            var deleteService = new TrackingDeleteFilesService();
            var service = new CleanSimpleLauncherFolderService(deleteService);

            // CleanupTempFiles should delete the temp directory
            service.CleanupTempFiles();

            // The directory should be gone (if it was accessible)
            Assert.False(Directory.Exists(tempDir));
            Assert.NotEmpty(deleteService.DeletedFiles);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    ///     Verifies that CleanupTempFiles does not throw when target directories do not exist.
    /// </summary>
    [Fact]
    public void CleanupTempFilesDoesNotThrowWhenDirectoriesDoNotExist()
    {
        // Ensure cleanup works even when target dirs are absent
        var service = new CleanSimpleLauncherFolderService(new TrackingDeleteFilesService());
        var exception = Record.Exception(service.CleanupTempFiles);
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that CleanupTempFiles removes the SimpleZipDrive temp directory.
    /// </summary>
    [Fact]
    public void CleanupTempFilesRemovesSimpleZipDriveTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SimpleZipDrive");
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = new CleanSimpleLauncherFolderService(new TrackingDeleteFilesService());
            service.CleanupTempFiles();

            Assert.False(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    ///     Verifies that CleanupTempFiles removes the SimpleXisoDrive temp directory.
    /// </summary>
    [Fact]
    public void CleanupTempFilesRemovesSimpleXisoDriveTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SimpleXisoDrive");
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = new CleanSimpleLauncherFolderService(new TrackingDeleteFilesService());
            service.CleanupTempFiles();

            Assert.False(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    ///     Verifies that no architecture-specific folder cleanup deletes the converter tool
    ///     folders: both ship native ARM64 variants that are resolved at runtime.
    /// </summary>
    [Fact]
    public void ArchitectureSpecificFolderCleanupKeepsConverterToolFolders()
    {
        string[] converterFolders =
        [
            Path.Combine("tools", "BatchConvertIsoToXiso"),
            Path.Combine("tools", "BatchConvertToCHD")
        ];

        Assert.DoesNotContain(
            CleanSimpleLauncherFolderService.DirectoriesToDeleteIfCurrentArchitectureIsArm64,
            directory => converterFolders.Any(folder =>
                directory.EndsWith(folder, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            CleanSimpleLauncherFolderService.DirectoriesToDeleteIfCurrentArchitectureIsX64,
            directory => converterFolders.Any(folder =>
                directory.EndsWith(folder, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class NoOpDeleteFilesService : IDeleteFilesService
    {
        /// <summary>
        ///     Does nothing; the no-op implementation ignores file deletion requests.
        /// </summary>
        /// <param name="filePath">The path of the file that would be deleted.</param>
        public void TryDeleteFile(string filePath)
        {
        }

        /// <summary>
        ///     Does nothing; the no-op implementation ignores asynchronous file deletion requests.
        /// </summary>
        /// <param name="filePath">The path of the file that would be deleted.</param>
        /// <returns>A completed <see cref="Task" />.</returns>
        public Task TryDeleteFileAsync(string filePath)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Does nothing; the no-op implementation ignores directory deletion requests.
        /// </summary>
        /// <param name="directoryPath">The path of the directory that would be deleted.</param>
        public void TryDeleteDirectory(string directoryPath)
        {
        }
    }

    private sealed class TrackingDeleteFilesService : IDeleteFilesService
    {
        /// <summary>
        ///     Gets the file and directory paths that deletion was requested for, in call order.
        /// </summary>
        public List<string> DeletedFiles { get; } = [];

        /// <summary>
        ///     Records the requested path and deletes the file if it exists, swallowing any errors.
        /// </summary>
        /// <param name="filePath">The path of the file to delete.</param>
        public void TryDeleteFile(string filePath)
        {
            DeletedFiles.Add(filePath);
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        ///     Records the requested path and deletes the file synchronously, then returns a completed task.
        /// </summary>
        /// <param name="filePath">The path of the file to delete.</param>
        /// <returns>A completed <see cref="Task" />.</returns>
        public Task TryDeleteFileAsync(string filePath)
        {
            TryDeleteFile(filePath);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Records the requested path and recursively deletes the directory if it exists, swallowing any errors.
        /// </summary>
        /// <param name="directoryPath">The path of the directory to delete.</param>
        public void TryDeleteDirectory(string directoryPath)
        {
            DeletedFiles.Add(directoryPath);
            try
            {
                if (Directory.Exists(directoryPath))
                    Directory.Delete(directoryPath, true);
            }
            catch
            {
                // ignore
            }
        }
    }
}