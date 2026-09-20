using Moq;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     LB-14 regression tests: on Linux/macOS the mount services must report "not supported on
///     this platform" at Information level instead of complaining that the pruned Windows
///     tools are missing (Warning + bug-report noise).
/// </summary>
public class MountPlatformGuardTests
{
    [Fact]
    public async Task MountChdFiles_OnUnix_ReportsPlatformSupportInsteadOfMissingTool()
    {
        if (OperatingSystem.IsWindows()) return;

        var logger = TestDependencies.Logger();
        var messageBox = TestDependencies.MessageBox();

        var mount = new MountChdFiles(logger.Object);
        await using var drive = await mount.MountAsync("game.chd", null, logger.Object, messageBox.Object);

        Assert.False(drive.IsMounted);
        messageBox.Verify(m => m.DokanDriverNotInstalledMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Information("Mounting CHD is not supported on this platform."), Times.Once);
    }

    [Fact]
    public async Task MountXisoFiles_OnUnix_ReportsPlatformSupportInsteadOfMissingTool()
    {
        if (OperatingSystem.IsWindows()) return;

        var logger = TestDependencies.Logger();
        var messageBox = TestDependencies.MessageBox();

        var mount = new MountXisoFiles(logger.Object);
        await using var drive = await mount.MountAsync("game.iso", logger.Object, messageBox.Object);

        Assert.False(drive.IsMounted);
        messageBox.Verify(m => m.DokanDriverNotInstalledMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Information("Mounting XISO is not supported on this platform."), Times.Once);
    }
}
