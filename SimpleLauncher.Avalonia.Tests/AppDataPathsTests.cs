using SimpleLauncher.Core.Services;
using SimpleLauncher.Diagnostics;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests the cross-platform app-data folder resolution (the Linux XDG fallback fix).
/// </summary>
public class AppDataPathsTests
{
    [Fact]
    public void Resolve_UsesLocalAppData_WhenRooted()
    {
        // Platform-appropriate rooted inputs (C:\ on Windows, /home/... on Linux)
        var localAppData = OperatingSystem.IsWindows()
            ? @"C:\Users\Test\AppData\Local"
            : Path.Combine("/home/test", ".local", "share");
        var userProfile = OperatingSystem.IsWindows() ? @"C:\Users\Test" : "/home/test";

        var result = AppDataPaths.Resolve(localAppData, userProfile, OperatingSystem.IsWindows());

        Assert.Equal(Path.Combine(localAppData, "SimpleLauncher"), result);
    }

    [Fact]
    public void Resolve_WindowsFallback_UsesAppDataLocalUnderProfile()
    {
        var userProfile = OperatingSystem.IsWindows() ? @"C:\Users\Test" : "/home/test";

        var result = AppDataPaths.Resolve(null, userProfile, OperatingSystem.IsWindows());

        var expected = OperatingSystem.IsWindows()
            ? Path.Combine(userProfile, "AppData", "Local", "SimpleLauncher")
            : Path.Combine(userProfile, ".local", "share", "SimpleLauncher");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_LinuxFallback_UsesDotLocalShareUnderHome()
    {
        var result = AppDataPaths.Resolve(null, "/home/test", false);

        Assert.Equal(Path.Combine("/home/test", ".local", "share", "SimpleLauncher"), result);
    }

    [Fact]
    public void Resolve_RelativeLocalAppData_IsIgnored()
    {
        // The Linux quirk: LocalApplicationData returns a relative/empty value.
        var result = AppDataPaths.Resolve("SimpleLauncher", "/home/test", false);

        Assert.Equal(Path.Combine("/home/test", ".local", "share", "SimpleLauncher"), result);
    }

    [Fact]
    public void Resolve_EmptyEverything_FallsBackToBaseDirectory()
    {
        var result = AppDataPaths.Resolve(null, null, false);

        Assert.True(Path.IsPathRooted(result));
        Assert.EndsWith("SimpleLauncher", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSimpleLauncherDataFolder_ReturnsRootedPath()
    {
        var result = AppDataPaths.GetSimpleLauncherDataFolder();

        Assert.True(Path.IsPathRooted(result));
        Assert.EndsWith("SimpleLauncher", result, StringComparison.OrdinalIgnoreCase);
    }
}