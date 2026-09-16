using SimpleLauncher.Core.Services.CheckPaths;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests the <see cref="CheckPath" /> utility for validating file system paths and emulator executable paths.
/// </summary>
public class CheckPathTests
{
    /// <summary>
    ///     Verifies that empty or whitespace-only paths return false for <see cref="CheckPath.IsValidPath" />.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidPathInvalidInputReturnsFalse(string path)
    {
        var result = CheckPath.IsValidPath(path);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that a null path returns false for <see cref="CheckPath.IsValidPath" />.
    /// </summary>
    [Fact]
    public void IsValidPathNullReturnsFalse()
    {
        var result = CheckPath.IsValidPath(null!);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that empty or whitespace-only paths return false for
    ///     <see cref="CheckPath.IsValidEmulatorExecutablePath" />.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidEmulatorExecutablePathInvalidInputReturnsFalse(string path)
    {
        var result = CheckPath.IsValidEmulatorExecutablePath(path);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that a null path returns false for <see cref="CheckPath.IsValidEmulatorExecutablePath" />.
    /// </summary>
    [Fact]
    public void IsValidEmulatorExecutablePathNullReturnsFalse()
    {
        var result = CheckPath.IsValidEmulatorExecutablePath(null!);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that an existing file path returns true for <see cref="CheckPath.IsValidPath" />.
    /// </summary>
    [Fact]
    public void IsValidPathExistingFileReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var result = CheckPath.IsValidPath(tempFile);
            Assert.True(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that an existing directory path returns true for <see cref="CheckPath.IsValidPath" />.
    /// </summary>
    [Fact]
    public void IsValidPathExistingDirectoryReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = CheckPath.IsValidPath(tempDir);
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    /// <summary>
    ///     Verifies that a non-existent path returns false for <see cref="CheckPath.IsValidPath" />.
    /// </summary>
    [Fact]
    public void IsValidPathNonExistentPathReturnsFalse()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "fake.txt");
        var result = CheckPath.IsValidPath(fakePath);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that a non-.exe file returns false for <see cref="CheckPath.IsValidEmulatorExecutablePath" />.
    /// </summary>
    [Fact]
    public void IsValidEmulatorExecutablePathNonExeFileReturnsFalse()
    {
        var tempFile = Path.GetTempFileName(); // .tmp extension
        try
        {
            var result = CheckPath.IsValidEmulatorExecutablePath(tempFile);
            Assert.False(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that an .exe file returns true for <see cref="CheckPath.IsValidEmulatorExecutablePath" />.
    /// </summary>
    [Fact]
    public void IsValidEmulatorExecutablePathExeFileReturnsTrue()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.exe");
        File.WriteAllText(tempFile, "fake exe");
        try
        {
            var result = CheckPath.IsValidEmulatorExecutablePath(tempFile);
            Assert.True(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that a .bat file returns true for <see cref="CheckPath.IsValidEmulatorExecutablePath" />.
    /// </summary>
    [Fact]
    public void IsValidEmulatorExecutablePathBatFileReturnsTrue()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bat");
        File.WriteAllText(tempFile, "@echo off");
        try
        {
            var result = CheckPath.IsValidEmulatorExecutablePath(tempFile);
            Assert.True(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that a .lnk file returns true for <see cref="CheckPath.IsValidEmulatorExecutablePath" />.
    /// </summary>
    [Fact]
    public void IsValidEmulatorExecutablePathLnkFileReturnsTrue()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.lnk");
        File.WriteAllText(tempFile, "fake shortcut");
        try
        {
            var result = CheckPath.IsValidEmulatorExecutablePath(tempFile);
            Assert.True(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that an extensionless file is accepted only on non-Windows platforms,
    ///     where native emulator binaries (e.g. /usr/bin/retroarch) have no extension.
    /// </summary>
    [Fact]
    public void IsValidEmulatorExecutablePathExtensionlessFileMatchesPlatform()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllText(tempFile, "fake binary");
        try
        {
            var result = CheckPath.IsValidEmulatorExecutablePath(tempFile);
            Assert.Equal(!OperatingSystem.IsWindows(), result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that %BASEFOLDER% resolves to the app directory and the sub-path is checked correctly.
    /// </summary>
    [Fact]
    public void IsValidPathWithBaseFolderPlaceholderResolvesAndChecks()
    {
        // %BASEFOLDER% should resolve to the app directory
        var result = CheckPath.IsValidPath("%BASEFOLDER%\\nonexistent_path_12345");
        Assert.False(result);
    }
}