using System.ComponentModel;
using SimpleLauncher.Core.Services;
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
    public void CheckPath_IsValidEmulatorExecutablePath_AcceptsAppImageAndWrapperScriptsOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        var folder = Directory.CreateTempSubdirectory("sl-checkpath-");
        try
        {
            // LB-06: common Linux emulator distributions must be selectable/saveable without
            // renaming or wrapping: AppImages, shell launchers and versioned .run installers.
            foreach (var name in new[] { "RetroArch.AppImage", "retroarch.sh", "duckstation.run" })
            {
                var path = Path.Combine(folder.FullName, name);
                File.WriteAllText(path, "");

                Assert.True(CheckPath.IsValidEmulatorExecutablePath(path), name);
            }

            // The execute bit is not required: it is unreliable on FAT/network mounts and the
            // OS reports an unusable binary at launch time.
            var notExecutable = Path.Combine(folder.FullName, "RetroArch.AppImage");
            Assert.False(File.GetUnixFileMode(notExecutable).HasFlag(UnixFileMode.UserExecute));
            Assert.True(CheckPath.IsValidEmulatorExecutablePath(notExecutable));
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

    [Fact]
    public void ResolveRelativeToAppDirectory_BareRelativeWindowsSeparatorsResolveInsideAppDirectory()
    {
        if (OperatingSystem.IsWindows()) return;

        // LB-13: a migrated/hand-edited value like "tools\retroarch\retroarch" must not become
        // one file name containing backslashes — it resolves inside the app directory.
        var result = PathHelper.ResolveRelativeToAppDirectory(@"tools\retroarch\retroarch");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "retroarch", "retroarch")),
            result);
    }

    [Fact]
    public void IsInvalidExecutableFormat_MapsUnixErrnoOnNonWindows()
    {
        if (OperatingSystem.IsWindows()) return;

        // LB-18: .NET on Unix surfaces the errno through Win32Exception.NativeErrorCode —
        // 8 = ENOEXEC, 13 = EACCES. Windows error codes must not be mapped here (8 and 13
        // mean different things on Windows).
        Assert.True(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(new Win32Exception(8)));
        Assert.True(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(new Win32Exception(13)));
        Assert.False(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(new Win32Exception(2)));
        Assert.False(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(new InvalidOperationException()));
    }
}
