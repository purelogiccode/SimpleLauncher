using SimpleLauncher.Core.Services.FileCopy;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="SafeFileCopy" />: the temp-file + rename overwrite used to replace
///     system images that another process has open (bug 67779).
/// </summary>
public sealed class SafeFileCopyTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>Creates an isolated directory for each test.</summary>
    public SafeFileCopyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SafeFileCopy_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>Removes the test directory.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    /// <summary>Replacing an existing destination replaces its content.</summary>
    [Fact]
    public void CopyOverwriteWhenDestinationExistsReplacesContent()
    {
        var source = Path.Combine(_testDirectory, "new.png");
        var destination = Path.Combine(_testDirectory, "old.png");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        SafeFileCopy.CopyOverwrite(source, destination);

        Assert.Equal("new", File.ReadAllText(destination));
    }

    /// <summary>A missing destination is created (and its directory when needed).</summary>
    [Fact]
    public void CopyOverwriteWhenDestinationMissingCreatesIt()
    {
        var source = Path.Combine(_testDirectory, "new.png");
        var destination = Path.Combine(_testDirectory, "sub", "created.png");
        File.WriteAllText(source, "content");

        SafeFileCopy.CopyOverwrite(source, destination);

        Assert.True(File.Exists(destination));
        Assert.Equal("content", File.ReadAllText(destination));
    }

    /// <summary>No temporary file is left behind after a successful replace.</summary>
    [Fact]
    public void CopyOverwriteLeavesNoTemporaryFilesBehind()
    {
        var source = Path.Combine(_testDirectory, "new.png");
        var destination = Path.Combine(_testDirectory, "old.png");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        SafeFileCopy.CopyOverwrite(source, destination);

        Assert.Empty(Directory.EnumerateFiles(_testDirectory, "*.tmp", SearchOption.AllDirectories));
    }

    /// <summary>A missing source throws and leaves the destination untouched.</summary>
    [Fact]
    public void CopyOverwriteWhenSourceMissingKeepsDestination()
    {
        var missingSource = Path.Combine(_testDirectory, "missing.png");
        var destination = Path.Combine(_testDirectory, "old.png");
        File.WriteAllText(destination, "old");

        Assert.Throws<FileNotFoundException>(() => SafeFileCopy.CopyOverwrite(missingSource, destination));

        Assert.Equal("old", File.ReadAllText(destination));
        Assert.Empty(Directory.EnumerateFiles(_testDirectory, "*.tmp", SearchOption.AllDirectories));
    }

    /// <summary>
    ///     A reader that allows deletion (the common case for image viewers/thumbnails) blocks
    ///     <see cref="File.Copy(string, string, bool)" /> but not the temp-file + rename replace.
    /// </summary>
    [Fact]
    public void CopyOverwriteWhenDestinationOpenWithDeleteSharingStillReplaces()
    {
        var source = Path.Combine(_testDirectory, "new.png");
        var destination = Path.Combine(_testDirectory, "old.png");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        using var openDestination = new FileStream(destination, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete);

        if (OperatingSystem.IsWindows())
        {
            // Prove the lock blocks the old implementation on Windows. (On Unix an open
            // file does not block a copy, so there is nothing to prove there.) Windows
            // reports the denial as a sharing violation (IOException) or ACCESS_DENIED
            // (UnauthorizedAccessException), depending on the locking process.
            var oldImplementationFailure = Record.Exception(() => File.Copy(source, destination, true));
            Assert.NotNull(oldImplementationFailure);
            Assert.True(oldImplementationFailure is IOException or UnauthorizedAccessException,
                $"Unexpected exception type: {oldImplementationFailure.GetType().FullName}");
        }

        SafeFileCopy.CopyOverwrite(source, destination);

        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Empty(Directory.EnumerateFiles(_testDirectory, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(_testDirectory, "*.old", SearchOption.AllDirectories));
    }

    /// <summary>
    ///     A reader that denies deletion cannot be replaced by either strategy; the failure is
    ///     reported and the destination stays intact (no partial copy, no temp litter).
    /// </summary>
    [Fact]
    public void CopyOverwriteWhenDestinationOpenWithoutDeleteSharingThrowsAndKeepsDestination()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Unix rename semantics do not honor a non-delete sharing mode; the scenario
            // is Windows-specific.
            return;
        }

        var source = Path.Combine(_testDirectory, "new.png");
        var destination = Path.Combine(_testDirectory, "old.png");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        using var openDestination = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);

        var failure = Record.Exception(() => SafeFileCopy.CopyOverwrite(source, destination));
        Assert.NotNull(failure);
        Assert.True(failure is IOException or UnauthorizedAccessException,
            $"Unexpected exception type: {failure.GetType().FullName}");

        Assert.Equal("old", File.ReadAllText(destination));
        Assert.Empty(Directory.EnumerateFiles(_testDirectory, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(_testDirectory, "*.old", SearchOption.AllDirectories));
    }
}
