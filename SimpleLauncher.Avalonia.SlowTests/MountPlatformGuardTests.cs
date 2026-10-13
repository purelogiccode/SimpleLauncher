using Moq;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;

namespace SimpleLauncher.Avalonia.SlowTests;

/// <summary>
///     LB-14 regression tests: on Linux/macOS the mount services must stay quiet (Information
///     level, no message boxes, no missing-tool Warning/bug-report noise) for the tools that are
///     not shipped or not usable there. MountChdFiles additionally falls back to the managed
///     CHDSharp conversion on Linux when the CHDMounter/FUSE mount is unavailable. On Linux with
///     FUSE available, MountXisoFiles attempts the bundled SimpleXisoDrive FUSE mount and reports
///     failures at Information level only (callers fall back or show the dialog themselves).
/// </summary>
public class MountPlatformGuardTests
{
    [Fact]
    public async Task MountChdFiles_OnUnix_SkipsMountWithoutWarningOrMessageBoxes()
    {
        if (OperatingSystem.IsWindows()) return;

        var logger = TestDependencies.Logger();
        var messageBox = TestDependencies.MessageBox();

        var mount = new MountChdFiles(logger.Object);
        await using var drive = await mount.MountAsync("game.chd", null, logger.Object, messageBox.Object);

        Assert.False(drive.IsMounted);
        messageBox.Verify(m => m.DokanDriverNotInstalledMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Information(It.IsAny<string>()), Times.AtLeastOnce);
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
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);

        if (FuseValidation.IsFuseAvailable())
        {
            // FUSE available: the service attempts the bundled SimpleXisoDrive mount; the bogus
            // relative path must fail quietly so callers can fall back / report it themselves.
            logger.Verify(l => l.Information(It.IsAny<string>()), Times.AtLeastOnce);
            messageBox.Verify(m => m.DokanDriverNotInstalledMessageBoxAsync(), Times.Never);
            messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
            return;
        }

        messageBox.Verify(m => m.DokanDriverNotInstalledMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        logger.Verify(l => l.Information("Mounting XISO is not supported on this platform."), Times.Once);
    }

    [Fact]
    public async Task MountXisoFiles_MountImageIsoAsync_OnUnix_ReportsPlatformSupportInsteadOfMissingTool()
    {
        if (OperatingSystem.IsWindows()) return;

        var logger = TestDependencies.Logger();
        var messageBox = TestDependencies.MessageBox();

        var mount = new MountXisoFiles(logger.Object);
        await using var drive = await mount.MountImageIsoAsync("game.cso", logger.Object, messageBox.Object);

        Assert.False(drive.IsMounted);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);

        if (FuseValidation.IsFuseAvailable())
        {
            logger.Verify(l => l.Information(It.IsAny<string>()), Times.AtLeastOnce);
            messageBox.Verify(m => m.DokanDriverNotInstalledMessageBoxAsync(), Times.Never);
            messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
            return;
        }

        messageBox.Verify(m => m.DokanDriverNotInstalledMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        logger.Verify(l => l.Information("Mounting XISO is not supported on this platform."), Times.Once);
    }
}