using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using SimpleLauncher.Services.GameLauncher.Strategies;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests the <see cref="ChdMountStrategy.IsMatch" /> method for CHD file detection with various emulators.
/// </summary>
public class ChdMountStrategyMatchTests
{
    private static ChdMountStrategy CreateStrategy(IMountXisoFiles? mountXisoFiles = null)
    {
        var configurationMock = new Mock<IConfiguration>();
        var messageBoxMock = new Mock<IMessageBoxLibraryService>();
        var mountChdMock = new Mock<IMountChdFiles>();
        var debugLoggerMock = new Mock<ILogger>();

        return new ChdMountStrategy(
            configurationMock.Object,
            messageBoxMock.Object,
            mountChdMock.Object,
            mountXisoFiles ?? new Mock<IMountXisoFiles>().Object,
            debugLoggerMock.Object);
    }

    private static LaunchContext CreateContext(string filePath, string emulatorName,
        string? emulatorLocation = null)
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
                EmulatorLocation = emulatorLocation ?? @"C:\Emulators\Xbox\emulator.exe",
                EmulatorParameters = "-full-screen -dvd_path"
            },
            Parameters = "-full-screen -dvd_path",
            WindowContext = new Mock<IWindowContext>().Object
        };
    }

    private static Process StartDummyProcess()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 60 127.0.0.1 > nul",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.NotNull(process);
        return process;
    }

    /// <summary>
    ///     Verifies that IsMatch returns false when the emulator name is empty.
    /// </summary>
    [Fact]
    public void IsMatchEmptyEmulatorNameReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = ""
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false when the file path is empty.
    /// </summary>
    [Fact]
    public void IsMatchEmptyFilePathReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = "",
            EmulatorName = "4DO"
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false for non-CHD file extensions.
    /// </summary>
    [Fact]
    public void IsMatchNonChdFileReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.zip",
            EmulatorName = "4DO"
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false when the emulator is RetroArch.
    /// </summary>
    [Fact]
    public void IsMatchRetroArchReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = "RetroArch"
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false when the emulator location points to a RetroArch executable.
    /// </summary>
    [Fact]
    public void IsMatchRetroArchByEmulatorLocationReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = "CustomEmu",
            EmulatorManager = new Emulator { EmulatorLocation = @"C:\emu\retroarch.exe" }
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false when the emulator is a DOSBox variant.
    /// </summary>
    [Fact]
    public void IsMatchDosBoxReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = "DOSBox-X",
            EmulatorManager = new Emulator { EmulatorLocation = @"C:\emu\dosbox-x.exe" }
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true for a CHD file combined with any of the supported emulators.
    /// </summary>
    /// <param name="emulatorName">The supported emulator name to test.</param>
    [Theory]
    [InlineData("4DO")]
    [InlineData("BlastEm")]
    [InlineData("CDiEmu")]
    [InlineData("Cxbx-Reloaded")]
    [InlineData("FB Alpha")]
    [InlineData("FBNeo")]
    [InlineData("Gens")]
    [InlineData("Mednafen")]
    [InlineData("Mesen")]
    [InlineData("Nebula")]
    [InlineData("PCSX-Redux")]
    [InlineData("PicoDrive")]
    [InlineData("Raine")]
    [InlineData("RPCS3")]
    [InlineData("Tsugaru")]
    [InlineData("Xemu")]
    [InlineData("Xenia")]
    [InlineData("Yabause")]
    [InlineData("Genesis Plus GX")]
    public void IsMatchChdWithSupportedEmulatorReturnsTrue(string emulatorName)
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = emulatorName
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false for an unsupported emulator name.
    /// </summary>
    [Fact]
    public void IsMatchUnsupportedEmulatorReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = "UnknownEmulator"
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true for every recognized CDiEmu emulator name variant.
    /// </summary>
    /// <param name="emulatorName">The CDiEmu name variant to test.</param>
    [Theory]
    [InlineData("CDiEmu")]
    [InlineData("CDi Emu")]
    [InlineData("CDi-Emu")]
    [InlineData("CDiEmulator")]
    [InlineData("CDi Emulator")]
    [InlineData("CDi-Emulator")]
    public void IsMatchCdiEmuNameVariants(string emulatorName)
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = emulatorName
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true when the emulator location points to a CDiEmu executable.
    /// </summary>
    [Fact]
    public void IsMatchCdiEmuByLocation()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = "SomeEmu",
            EmulatorManager = new Emulator { EmulatorLocation = @"C:\emu\wcdiemu-v053b9.exe" }
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true for every recognized FB Alpha emulator name variant.
    /// </summary>
    /// <param name="emulatorName">The FB Alpha name variant to test.</param>
    [Theory]
    [InlineData("FBAlpha")]
    [InlineData("FB Alpha")]
    [InlineData("FinalBurnAlpha")]
    [InlineData("Final Burn Alpha")]
    [InlineData("FinalBurn Alpha")]
    public void IsMatchFbAlphaNameVariants(string emulatorName)
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = emulatorName
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true for every recognized FBNeo emulator name variant.
    /// </summary>
    /// <param name="emulatorName">The FBNeo name variant to test.</param>
    [Theory]
    [InlineData("FBNeo")]
    [InlineData("FB Neo")]
    [InlineData("FinalBurnNeo")]
    [InlineData("Final Burn Neo")]
    [InlineData("FinalBurn Neo")]
    public void IsMatchFbNeoNameVariants(string emulatorName)
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = emulatorName
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true for PCSX-Redux and PCSX Redux name variants.
    /// </summary>
    [Fact]
    public void IsMatchPcsxReduxNameVariants()
    {
        var strategy = CreateStrategy();
        var context1 = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = "PCSX-Redux"
        };
        var context2 = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.chd",
            EmulatorName = "PCSX Redux"
        };

        Assert.True(strategy.IsMatch(context1));
        Assert.True(strategy.IsMatch(context2));
    }

    /// <summary>
    ///     Verifies that the strategy has a priority of 10.
    /// </summary>
    [Fact]
    public void PriorityIs10()
    {
        var strategy = CreateStrategy();
        Assert.Equal(10, strategy.Priority);
    }

    /// <summary>
    ///     Xbox CHD + mount backend available: the strategy prefers the bundled
    ///     SimpleXisoDrive and hands xemu the virtual image.iso (no CHDMounter call).
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncXemuChdMountsThroughSimpleXisoDriveWhenBackendAvailable()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var mountChd = new Mock<IMountChdFiles>();
        var mountXiso = new Mock<IMountXisoFiles>();
        mountXiso.SetupGet(m => m.IsMountBackendAvailable).Returns(true);
        var launcher = new Mock<ILauncherService>();

        using var dummyProcess = StartDummyProcess();
        mountXiso.Setup(m => m.MountImageIsoAsync(@"C:\roms\game.chd", logger.Object, messageBox.Object))
            .ReturnsAsync(new MountXisoDrive(dummyProcess, @"Z:\image.iso", logger.Object, logger.Object));

        var strategy = new ChdMountStrategy(new Mock<IConfiguration>().Object, messageBox.Object,
            mountChd.Object, mountXiso.Object, logger.Object);
        var context = CreateContext(@"C:\roms\game.chd", "Xemu");

        await strategy.ExecuteAsync(context, launcher.Object);

        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            @"Z:\image.iso", "Xemu", context.SystemManagerService!, context.EmulatorManager!,
            context.Parameters, context.WindowContext!, context.LoadingState, @"C:\roms\game.chd"), Times.Once);
        mountChd.Verify(m => m.MountAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ILogger>(),
            It.IsAny<IMessageBoxLibraryService>()), Times.Never);
    }

    /// <summary>
    ///     Xbox CHD + Cxbx-Reloaded + mount backend available: the strategy mounts through
    ///     SimpleXisoDrive and launches the emulator with the mounted default.xbe.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncCxbxChdMountsThroughSimpleXisoDriveWhenBackendAvailable()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var mountChd = new Mock<IMountChdFiles>();
        var mountXiso = new Mock<IMountXisoFiles>();
        mountXiso.SetupGet(m => m.IsMountBackendAvailable).Returns(true);
        var launcher = new Mock<ILauncherService>();

        using var dummyProcess = StartDummyProcess();
        mountXiso.Setup(m => m.MountAsync(@"C:\roms\game.chd", logger.Object, messageBox.Object))
            .ReturnsAsync(new MountXisoDrive(dummyProcess, @"Z:\default.xbe", logger.Object, logger.Object));

        var strategy = new ChdMountStrategy(new Mock<IConfiguration>().Object, messageBox.Object,
            mountChd.Object, mountXiso.Object, logger.Object);
        var context = CreateContext(@"C:\roms\game.chd", "Cxbx-Reloaded");

        await strategy.ExecuteAsync(context, launcher.Object);

        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            @"Z:\default.xbe", "Cxbx-Reloaded", context.SystemManagerService!, context.EmulatorManager!,
            context.Parameters, context.WindowContext!, context.LoadingState, @"C:\roms\game.chd"), Times.Once);
        mountChd.Verify(m => m.MountAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ILogger>(),
            It.IsAny<IMessageBoxLibraryService>()), Times.Never);
    }

    /// <summary>
    ///     Xbox CHD + no mount backend (no Dokan/FUSE): the strategy skips the SimpleXisoDrive
    ///     attempt (which would only show a driver dialog) and goes straight to CHDMounter.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncXboxChdFallsBackToChdMounterWhenBackendUnavailable()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var mountChd = new Mock<IMountChdFiles>();
        var mountXiso = new Mock<IMountXisoFiles>();
        mountXiso.SetupGet(m => m.IsMountBackendAvailable).Returns(false);
        var launcher = new Mock<ILauncherService>();

        mountChd.Setup(m => m.MountAsync(@"C:\roms\game.chd", It.IsAny<string?>(), logger.Object, messageBox.Object))
            .ReturnsAsync(new MountChdDrive(logger.Object, logger.Object));

        var strategy = new ChdMountStrategy(new Mock<IConfiguration>().Object, messageBox.Object,
            mountChd.Object, mountXiso.Object, logger.Object);
        var context = CreateContext(@"C:\roms\game.chd", "Xemu");

        await strategy.ExecuteAsync(context, launcher.Object);

        mountXiso.Verify(m => m.MountImageIsoAsync(It.IsAny<string>(), It.IsAny<ILogger>(),
            It.IsAny<IMessageBoxLibraryService>()), Times.Never);
        mountChd.Verify(m => m.MountAsync(@"C:\roms\game.chd", It.IsAny<string?>(), logger.Object,
            messageBox.Object), Times.Once);
        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
            It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()),
            Times.Never);
    }
}