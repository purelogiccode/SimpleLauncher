using System.Diagnostics;
using System.Reflection;
using System.Text;
using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Services.GameLauncher;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Regression tests for the post-exit error analysis: an emulator the app launched
///     successfully but which then died is an emulator-side/environment outcome, not an app
///     bug, so it must be logged at Information level (never Warning+, which reaches the
///     bug-report sink). Bugs 67748/67749 filed reports for a RetroArch AppImage that died
///     with exit code 135 before producing any output. The user-facing dialogs are unchanged.
/// </summary>
public class EmulatorExitCodeTests
{
    /// <summary>
    ///     Verifies that an unclassified RetroArch exit (AppImage dying with code 135 and no
    ///     output, bugs 67748/67749) is logged at Information, never Warning/Error.
    /// </summary>
    [Fact]
    public async Task RetroArchUnclassifiedExit135LogsInformationNeverWarningOrError()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var service = CreateService(messageBox.Object, logger.Object);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 135",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.NotNull(process);
        process.WaitForExit();

        var emulator = new Emulator
        {
            EmulatorName = "RetroArch",
            EmulatorLocation = "/home/vm/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64.AppImage",
            ReceiveANotificationOnEmulatorError = false
        };

        await InvokeCheckForExitCodeWithErrorAnyAsync(service, process,
            new ProcessStartInfo { FileName = "retroarch.exe", Arguments = "-L core -f game.zip" },
            new StringBuilder(),
            new StringBuilder("Setting $HOME to /home/vm/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64.AppImage.home"),
            emulator);

        Assert.Equal(135, process.ExitCode);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Information(It.Is<string>(s => s.Contains("provided parameters"))), Times.Once);
        messageBox.Verify(m => m.RetroArchParameterIssueMessageBoxAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    ///     Verifies that the generic fallback for any other unclassified non-zero exit also
    ///     logs at Information, never Warning/Error.
    /// </summary>
    [Fact]
    public async Task GenericUnclassifiedExitLogsInformationNeverWarningOrError()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var service = CreateService(messageBox.Object, logger.Object);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 135",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.NotNull(process);
        process.WaitForExit();

        var emulator = new Emulator
        {
            EmulatorName = "Dolphin",
            EmulatorLocation = @"C:\Emulators\Dolphin\dolphin.exe",
            ReceiveANotificationOnEmulatorError = false
        };

        await InvokeCheckForExitCodeWithErrorAnyAsync(service, process,
            new ProcessStartInfo { FileName = "dolphin.exe", Arguments = "game.iso" },
            new StringBuilder(), new StringBuilder("some output"), emulator);

        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Information(It.Is<string>(s => s.Contains("provided parameters"))), Times.Once);
        messageBox.Verify(m => m.CouldNotLaunchGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
    }

    private static GameLauncherService CreateService(
        IMessageBoxLibraryService messageBox, ILogger logger)
    {
        return new GameLauncherService([], [], null!, null!, null!, null!, null!, messageBox, null!, logger,
            null!, null!, null!);
    }

    private static Task InvokeCheckForExitCodeWithErrorAnyAsync(GameLauncherService service, Process process,
        ProcessStartInfo psi, StringBuilder output, StringBuilder error, Emulator emulator)
    {
        var method = typeof(GameLauncherService).GetMethod("CheckForExitCodeWithErrorAnyAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task?)method.Invoke(service,
            [process, psi, output, error, emulator, Mock.Of<ISystemManager>()]);
        Assert.NotNull(task);
        return task;
    }
}
