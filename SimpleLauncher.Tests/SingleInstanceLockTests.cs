using SimpleLauncher.Core;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Regression tests for the single-instance lock file (shared by the WPF and Avalonia
///     apps): contention must be reported as <c>null</c>, while an unusable lock path must
///     throw so callers can fall back to the named-mutex verdict instead of misreading an
///     environment error as "another instance is running".
/// </summary>
public class SingleInstanceLockTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "SimpleLauncherTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        }
        catch
        {
            // Best effort only.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TryAcquireLockFile_CreatesMissingDirectory_AndAcquiresLock()
    {
        var lockFilePath = Path.Combine(_tempDirectory, "nested", SingleInstance.LockFileName);

        using var acquired = SingleInstance.TryAcquireLockFile(lockFilePath);

        Assert.NotNull(acquired);
        Assert.True(File.Exists(lockFilePath));
    }

    [Fact]
    public void TryAcquireLockFile_ReturnsNull_WhenAnotherHandleHoldsTheLock()
    {
        var lockFilePath = Path.Combine(_tempDirectory, SingleInstance.LockFileName);

        using var first = SingleInstance.TryAcquireLockFile(lockFilePath);
        Assert.NotNull(first);

        var second = SingleInstance.TryAcquireLockFile(lockFilePath);

        Assert.Null(second);
    }

    [Fact]
    public void TryAcquireLockFile_AcquiresAgain_AfterTheLockIsReleased()
    {
        var lockFilePath = Path.Combine(_tempDirectory, SingleInstance.LockFileName);

        using (var first = SingleInstance.TryAcquireLockFile(lockFilePath))
        {
            Assert.NotNull(first);
        }

        using var second = SingleInstance.TryAcquireLockFile(lockFilePath);

        Assert.NotNull(second);
    }

    [Fact]
    public void TryAcquireLockFile_Throws_WhenThePathIsNotAFile()
    {
        // A directory path cannot be opened as a lock file: the caller must see this as an
        // environment error (not contention) and fall back to the named mutex.
        Directory.CreateDirectory(_tempDirectory);

        Assert.Throws<UnauthorizedAccessException>(() =>
            SingleInstance.TryAcquireLockFile(_tempDirectory));
    }
}