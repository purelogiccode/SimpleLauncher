using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.Strategies;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the <see cref="DefaultLaunchStrategy" /> class.
/// </summary>
public class DefaultLaunchStrategyTests
{
    private static DefaultLaunchStrategy CreateStrategy()
    {
        return new DefaultLaunchStrategy();
    }

    /// <summary>
    ///     Verifies that the default launch strategy has a priority of 999.
    /// </summary>
    [Fact]
    public void PriorityIs999()
    {
        var strategy = CreateStrategy();
        Assert.Equal(999, strategy.Priority);
    }

    /// <summary>
    ///     Verifies that IsMatch always returns true regardless of context.
    /// </summary>
    [Fact]
    public void IsMatchAlwaysReturnsTrue()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.zip",
            EmulatorName = "Mesen"
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true even with an empty LaunchContext.
    /// </summary>
    [Fact]
    public void IsMatchWithEmptyContextReturnsTrue()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext();

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that executing a .bat file calls RunBatchFileAsync on the launcher service.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncBatFileCallsRunBatchFileAsync()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.bat",
            EmulatorName = "DOSBox-X",
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await strategy.ExecuteAsync(context, launcherMock.Object);

        launcherMock.Verify(l => l.RunBatchFileAsync(
            context.ResolvedFilePath,
            context.EmulatorManager!,
            context.WindowContext!), Times.Once);
    }

    /// <summary>
    ///     Verifies that executing a .lnk file calls LaunchShortcutFileAsync on the launcher service.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncLnkFileCallsLaunchShortcutFileAsync()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.lnk",
            EmulatorName = "Mesen",
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await strategy.ExecuteAsync(context, launcherMock.Object);

        launcherMock.Verify(l => l.LaunchShortcutFileAsync(
            context.ResolvedFilePath,
            context.EmulatorManager!,
            context.WindowContext!), Times.Once);
    }

    /// <summary>
    ///     Verifies that executing a .url file calls LaunchShortcutFileAsync on the launcher service.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncUrlFileCallsLaunchShortcutFileAsync()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.url",
            EmulatorName = "Mesen",
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await strategy.ExecuteAsync(context, launcherMock.Object);

        launcherMock.Verify(l => l.LaunchShortcutFileAsync(
            context.ResolvedFilePath,
            context.EmulatorManager!,
            context.WindowContext!), Times.Once);
    }

    /// <summary>
    ///     Verifies that executing an .exe file calls LaunchExecutableAsync on the launcher service.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncExeFileCallsLaunchExecutableAsync()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.exe",
            EmulatorName = "Mesen",
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await strategy.ExecuteAsync(context, launcherMock.Object);

        launcherMock.Verify(l => l.LaunchExecutableAsync(
            context.ResolvedFilePath,
            context.EmulatorManager!,
            context.WindowContext!), Times.Once);
    }

    /// <summary>
    ///     Verifies that executing a ROM file calls LaunchRegularEmulatorAsync on the launcher service.
    /// </summary>
    /// <param name="extension">The ROM file extension to test.</param>
    [Theory]
    [InlineData(".nes")]
    [InlineData(".sfc")]
    [InlineData(".smc")]
    [InlineData(".gba")]
    [InlineData(".gb")]
    [InlineData(".gbc")]
    [InlineData(".n64")]
    [InlineData(".z64")]
    [InlineData(".v64")]
    [InlineData(".md")]
    [InlineData(".gen")]
    [InlineData(".smd")]
    [InlineData(".psx")]
    [InlineData(".iso")]
    [InlineData(".bin")]
    [InlineData(".img")]
    public async Task ExecuteAsyncRomFileCallsLaunchRegularEmulatorAsync(string extension)
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = $@"C:\roms\game{extension}",
            EmulatorName = "Mesen",
            SystemName = "NES",
            Parameters = "--fullscreen",
            SystemManagerService = new Mock<ISystemManager>().Object,
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await strategy.ExecuteAsync(context, launcherMock.Object);

        launcherMock.Verify(l => l.LaunchRegularEmulatorAsync(
            context.ResolvedFilePath,
            context.EmulatorName,
            context.SystemManagerService!,
            context.EmulatorManager!,
            context.Parameters,
            context.WindowContext!,
            context.LoadingState,
            null), Times.Once);
    }

    /// <summary>
    ///     Verifies that executing a file with an uppercase extension calls the correct launcher method.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncUpperCaseExtensionCallsCorrectMethod()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\GAME.BAT",
            EmulatorName = "DOSBox-X",
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await strategy.ExecuteAsync(context, launcherMock.Object);

        launcherMock.Verify(l => l.RunBatchFileAsync(
            context.ResolvedFilePath,
            context.EmulatorManager!,
            context.WindowContext!), Times.Once);
    }

    /// <summary>
    ///     Verifies that executing a file with a mixed-case extension calls the correct launcher method.
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncMixedCaseExtensionCallsCorrectMethod()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\Game.Exe",
            EmulatorName = "Mesen",
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await strategy.ExecuteAsync(context, launcherMock.Object);

        launcherMock.Verify(l => l.LaunchExecutableAsync(
            context.ResolvedFilePath,
            context.EmulatorManager!,
            context.WindowContext!), Times.Once);
    }

    /// <summary>
    ///     Verifies that a null EmulatorManager fails with an explicit error instead of a
    ///     NullReferenceException (CORE-30).
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncNullEmulatorManagerThrowsInvalidOperationException()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.bat",
            EmulatorName = "DOSBox-X",
            WindowContext = new Mock<IWindowContext>().Object
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.ExecuteAsync(context, launcherMock.Object));
    }

    /// <summary>
    ///     Verifies that a null WindowContext fails with an explicit error instead of a
    ///     NullReferenceException (CORE-30).
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncNullWindowContextThrowsInvalidOperationException()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.exe",
            EmulatorName = "Mesen",
            EmulatorManager = new Emulator()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.ExecuteAsync(context, launcherMock.Object));
    }

    /// <summary>
    ///     Verifies that a ROM launch with a null SystemManagerService fails with an explicit
    ///     error instead of a NullReferenceException (CORE-30).
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncNullSystemManagerServiceThrowsInvalidOperationException()
    {
        var strategy = CreateStrategy();
        var launcherMock = new Mock<ILauncherService>();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\roms\game.nes",
            EmulatorName = "Mesen",
            EmulatorManager = new Emulator(),
            WindowContext = new Mock<IWindowContext>().Object
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.ExecuteAsync(context, launcherMock.Object));
    }
}