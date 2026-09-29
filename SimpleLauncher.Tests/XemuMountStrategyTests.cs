using System.Diagnostics;
using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using SimpleLauncher.Core.Services.GameLauncher.Strategies;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the <see cref="XemuMountStrategy" /> class.
/// </summary>
public class XemuMountStrategyTests
{
    private static XemuMountStrategy CreateStrategy(IMountXisoFiles? mountXisoFiles = null)
    {
        var logErrorsMock = new Mock<ILogger>();
        var messageBoxMock = new Mock<IMessageBoxLibraryService>();

        return new XemuMountStrategy(
            logErrorsMock.Object,
            messageBoxMock.Object,
            mountXisoFiles ?? new Mock<IMountXisoFiles>().Object);
    }

    private static LaunchContext CreateContext(string filePath, string emulatorName, string? emulatorLocation = null)
    {
        return new LaunchContext
        {
            ResolvedFilePath = filePath,
            EmulatorName = emulatorName,
            SystemName = "Microsoft Xbox",
            SystemManagerService = new SystemManagerConfig { SystemName = "Microsoft Xbox" },
            EmulatorManager = new Emulator
            {
                EmulatorName = emulatorName,
                EmulatorLocation = emulatorLocation ?? @"C:\Emulators\Xemu\xemu.exe",
                EmulatorParameters = "-full-screen -dvd_path"
            },
            Parameters = "-full-screen -dvd_path",
            WindowContext = new Mock<IWindowContext>().Object
        };
    }

    /// <summary>
    ///     Verifies that the strategy has a priority of 20.
    /// </summary>
    [Fact]
    public void PriorityIs20()
    {
        var strategy = CreateStrategy();
        Assert.Equal(20, strategy.Priority);
    }

    /// <summary>
    ///     Verifies that <see cref="XemuMountStrategy.IsMatch" /> returns false when the file path is empty.
    /// </summary>
    [Fact]
    public void IsMatchEmptyFilePathReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = CreateContext("", "Xemu");
        context.ResolvedFilePath = "";

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that <see cref="XemuMountStrategy.IsMatch" /> returns false when the emulator name is empty.
    /// </summary>
    [Fact]
    public void IsMatchEmptyEmulatorNameReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = CreateContext(@"C:\xbox\game.cso", "");
        context.EmulatorName = "";

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that <see cref="XemuMountStrategy.IsMatch" /> returns false for non-compressed file extensions.
    /// </summary>
    /// <param name="extension">The file extension to test.</param>
    [Theory]
    [InlineData(".iso")]
    [InlineData(".xiso")]
    [InlineData(".chd")]
    [InlineData(".zip")]
    [InlineData(".7z")]
    [InlineData(".rar")]
    [InlineData(".bin")]
    public void IsMatchNonCompressedExtensionReturnsFalse(string extension)
    {
        var strategy = CreateStrategy();
        var context = CreateContext($@"C:\xbox\game{extension}", "Xemu");

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that <see cref="XemuMountStrategy.IsMatch" /> returns false for non-xemu emulators.
    /// </summary>
    /// <param name="emulatorName">The emulator name to test.</param>
    [Theory]
    [InlineData("PPSSPP")]
    [InlineData("Cxbx-Reloaded")]
    [InlineData("RetroArch")]
    public void IsMatchNonXemuEmulatorReturnsFalse(string emulatorName)
    {
        var strategy = CreateStrategy();
        var context = CreateContext(@"C:\xbox\game.cso", emulatorName, @"C:\Emulators\Other\other.exe");

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that <see cref="XemuMountStrategy.IsMatch" /> returns true for xemu variants with .cso/.zar files,
    ///     case-insensitively.
    /// </summary>
    /// <param name="emulatorName">The xemu name variant to test.</param>
    /// <param name="filePath">The file path to test.</param>
    [Theory]
    [InlineData("Xemu", @"C:\xbox\game.cso")]
    [InlineData("xemu", @"C:\xbox\game.zar")]
    [InlineData("XEMU", @"C:\xbox\game.CSO")]
    [InlineData("Xemu 0.8.136", @"C:\xbox\game.ZAR")]
    public void IsMatchXemuWithCompressedXboxImageReturnsTrue(string emulatorName, string filePath)
    {
        var strategy = CreateStrategy();
        var context = CreateContext(filePath, emulatorName);

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that xemu is detected through the emulator location when the display name does not contain it.
    /// </summary>
    [Fact]
    public void IsMatchXemuLocationWithCompressedXboxImageReturnsTrue()
    {
        var strategy = CreateStrategy();
        var context = CreateContext(@"C:\xbox\game.cso", "My Emulator", @"C:\Emulators\Xemu\xemu.exe");

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that a successful image.iso mount launches the emulator with the mounted path and the
    ///     original file path for display.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncWhenMountedLaunchesEmulatorWithImageIsoPath()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var mountXisoFiles = new Mock<IMountXisoFiles>();
        var launcher = new Mock<ILauncherService>();

        using var dummyProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 60 127.0.0.1 > nul",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.NotNull(dummyProcess);

        var mountedDrive = new MountXisoDrive(dummyProcess, @"Z:\image.iso", logger.Object, logger.Object);
        mountXisoFiles
            .Setup(m => m.MountImageIsoAsync(@"C:\xbox\game.cso", logger.Object, messageBox.Object))
            .ReturnsAsync(mountedDrive);

        var context = CreateContext(@"C:\xbox\game.cso", "Xemu");
        var strategy = new XemuMountStrategy(logger.Object, messageBox.Object, mountXisoFiles.Object);

        await strategy.ExecuteAsync(context, launcher.Object);

        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            @"Z:\image.iso",
            "Xemu",
            context.SystemManagerService!,
            context.EmulatorManager!,
            "-full-screen -dvd_path",
            context.WindowContext!,
            context.LoadingState,
            @"C:\xbox\game.cso"), Times.Once);
    }

    /// <summary>
    ///     Verifies that a failed image.iso mount does not launch the emulator.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncWhenMountFailsDoesNotLaunchEmulator()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var mountXisoFiles = new Mock<IMountXisoFiles>();
        var launcher = new Mock<ILauncherService>();

        mountXisoFiles
            .Setup(m => m.MountImageIsoAsync(@"C:\xbox\game.zar", logger.Object, messageBox.Object))
            .ReturnsAsync(new MountXisoDrive(logger.Object, logger.Object));

        var context = CreateContext(@"C:\xbox\game.zar", "Xemu");
        var strategy = new XemuMountStrategy(logger.Object, messageBox.Object, mountXisoFiles.Object);

        await strategy.ExecuteAsync(context, launcher.Object);

        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
            It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()),
            Times.Never);
    }
}
