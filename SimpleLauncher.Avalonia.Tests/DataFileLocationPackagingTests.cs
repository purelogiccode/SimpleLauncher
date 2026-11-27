using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Diagnostics;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Guards the AppImage/Flatpak data-location rule: packaged installs must always use the
///     per-user data folder, even when <c>APPIMAGE_EXTRACT_AND_RUN</c> makes the extracted
///     application directory writable for the lifetime of the process.
/// </summary>
public class DataFileLocationPackagingTests
{
    private static readonly Lock EnvironmentLock = new();

    /// <summary>
    ///     <c>%BASEFOLDER%</c> must resolve to the per-user data folder in an AppImage; the
    ///     mounted squashfs (and the extract-and-run temp folder) is not writable storage.
    /// </summary>
    [Fact]
    public void AppImageBaseFolderUsesTheUserDataFolder()
    {
        if (!OperatingSystem.IsLinux()) return;

        lock (EnvironmentLock)
        {
            var previous = Environment.GetEnvironmentVariable("APPIMAGE");
            try
            {
                Environment.SetEnvironmentVariable("APPIMAGE", "/tmp/SimpleLauncher.AppImage");
                Assert.True(PackagingEnvironment.HasReadOnlyInstall);
                Assert.Equal(AppDataPaths.SimpleLauncherDataFolder, PathHelper.BaseFolder);
            }
            finally
            {
                Environment.SetEnvironmentVariable("APPIMAGE", previous);
            }
        }
    }

    /// <summary>An AppImage process ignores portable mode and uses the XDG data folder.</summary>
    [Fact]
    public void AppImageAlwaysUsesTheUserDataFolder()
    {
        if (!OperatingSystem.IsLinux()) return;

        lock (EnvironmentLock)
        {
            var previous = Environment.GetEnvironmentVariable("APPIMAGE");
            try
            {
                Environment.SetEnvironmentVariable("APPIMAGE", "/tmp/SimpleLauncher.AppImage");
                var location = new DataFileLocation($"appimage-{Guid.NewGuid():N}.xml");

                Assert.False(location.IsPortableMode);
                Assert.Equal(location.GetLocalAppDataPath(), location.FilePath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("APPIMAGE", previous);
            }
        }
    }
}
