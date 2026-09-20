using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Cross-platform regression tests for the Core path helpers used by the Avalonia app on
///     Linux/macOS. The Windows-only suites cannot catch these: <see cref="PathHelper.GetLongPath" />
///     must not apply the Windows extended-length prefix on Unix, and stored
///     <c>%BASEFOLDER%\...</c> configurations with backslash separators must still resolve.
/// </summary>
public class PathHandlingCrossPlatformTests
{
    [Fact]
    public void GetLongPath_UsesExtendedPrefixOnlyOnWindows()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "sl-longpath-test.txt");

        var result = PathHelper.GetLongPath(absolutePath);

        if (OperatingSystem.IsWindows())
            Assert.Equal(@"\\?\" + absolutePath, result);
        else
            Assert.Equal(absolutePath, result);

        // Relative paths are never prefixed on any platform.
        Assert.Equal(Path.Combine("images", "default.png"), PathHelper.GetLongPath(Path.Combine("images", "default.png")));
    }

    [Fact]
    public void ResolveRelativeToAppDirectory_PlaceholderWithWindowsSeparatorsResolvesInsideAppDirectory()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory(@"%BASEFOLDER%\roms\SimpleLauncherTest");

        Assert.Equal(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roms", "SimpleLauncherTest"),
            result);
    }

    [Fact]
    public void CheckPath_IsValidPath_TrueForExistingDirectoryAndFile()
    {
        var folder = Directory.CreateTempSubdirectory("sl-checkpath-");
        try
        {
            var file = Path.Combine(folder.FullName, "file.txt");
            File.WriteAllText(file, "x");

            Assert.True(CheckPath.IsValidPath(folder.FullName));
            Assert.True(CheckPath.IsValidPath(file));
            Assert.False(CheckPath.IsValidPath(Path.Combine(folder.FullName, "missing.txt")));
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Fact]
    public void CheckPath_IsValidEmulatorExecutablePath_AcceptsExtensionlessBinaryOnUnix()
    {
        var folder = Directory.CreateTempSubdirectory("sl-checkpath-");
        try
        {
            var binary = Path.Combine(folder.FullName, "retroarch");
            File.WriteAllText(binary, "");

            // Native emulator binaries are extensionless outside Windows (e.g. /usr/bin/retroarch).
            Assert.Equal(!OperatingSystem.IsWindows(), CheckPath.IsValidEmulatorExecutablePath(binary));
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Fact]
    public async Task DeleteFiles_TryDeleteFileAsync_DeletesExistingFile()
    {
        var folder = Directory.CreateTempSubdirectory("sl-deletefiles-");
        try
        {
            var file = Path.Combine(folder.FullName, "delete-me.txt");
            File.WriteAllText(file, "x");

            await DeleteFiles.TryDeleteFileAsync(file);

            Assert.False(File.Exists(file));
        }
        finally
        {
            folder.Delete(true);
        }
    }
}
