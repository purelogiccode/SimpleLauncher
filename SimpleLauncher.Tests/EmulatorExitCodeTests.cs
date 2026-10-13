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
            new StringBuilder(
                "Setting $HOME to /home/vm/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64.AppImage.home"),
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

    /// <summary>
    ///     Verifies that exit code 1113 (ERROR_NO_UNICODE_TRANSLATION — the emulator
    ///     could not convert a Unicode ROM path to its ANSI code page, bug 67583) is
    ///     classified as an expected path/environment condition: logged at Information
    ///     through the dedicated branch, never Warning/Error.
    /// </summary>
    [Fact]
    public async Task UnicodePathConversionExit1113LogsInformationNeverWarningOrError()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var service = CreateService(messageBox.Object, logger.Object);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 1113",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.NotNull(process);
        process.WaitForExit();

        var emulator = new Emulator
        {
            EmulatorName = "Ymir",
            EmulatorLocation = @"D:\Emulators\Saturn\ymir-sdl3.exe",
            ReceiveANotificationOnEmulatorError = false
        };

        await InvokeCheckForExitCodeWithErrorAnyAsync(service, process,
            new ProcessStartInfo { FileName = "ymir-sdl3.exe", Arguments = "game.cue" },
            new StringBuilder(
                "System error: No mapping for the Unicode character exists in the target multi-byte code page."),
            new StringBuilder(), emulator);

        Assert.Equal(1113, process.ExitCode);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Information(It.Is<string>(s => s.Contains("provided parameters"))), Times.Once);

        // The dedicated classification branch ran (not the generic fallback).
        logger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Unicode/ANSI code page conversion"))),
            Times.Once);
        messageBox.Verify(m => m.CouldNotLaunchGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    ///     Bugs 67522/67529: MAME echoing a full ROM path as the system name must log
    ///     the dedicated parameter hint at Information (never Warning/Error) and keep
    ///     the user-facing flow — MAME dialog first, then the AI parameter assist that
    ///     now receives the launch-failure context.
    /// </summary>
    [Fact]
    public async Task MameUnknownSystemWithFullPath_LogsParameterHintAndKeepsAiAssistFlow()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        messageBox.Setup(m => m.MameUnknownSystemErrorMessageBoxAsync()).Returns(Task.CompletedTask);
        messageBox.Setup(m => m.WouldYouLikeToOpenTheLogMessageBoxAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        messageBox.Setup(m => m.AskAiToFixParametersMessageBoxAsync()).ReturnsAsync(false);
        var service = CreateService(messageBox.Object, logger.Object);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 5",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.NotNull(process);
        process.WaitForExit();

        var emulator = new Emulator
        {
            EmulatorName = "MAME",
            EmulatorLocation = @"D:\MAME\mame.exe",
            ReceiveANotificationOnEmulatorError = true
        };

        var mameError = new StringBuilder(
            "Unknown system 'J:\\ROMS [Mame]\\MAME 0.289 ROMs (merged)\\a1cass.zip'\n" +
            "\"J:\\ROMS [Mame]\\MAME 0.289 ROMs (merged)\\a1cass.zip\" approximately matches the following\n" +
            "supported machines (best match first):\n" +
            "roma2             Mephisto Roma II                                      (Hegener + Glaser, 1989)");

        await InvokeCheckForExitCodeWithErrorAnyAsync(service, process,
            new ProcessStartInfo
                { FileName = "mame.exe", Arguments = "-rompath \"J:\\roms\" \"J:\\roms\\a1cass.zip\"" },
            new StringBuilder(), mameError, emulator);

        Assert.Equal(5, process.ExitCode);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Information(It.Is<string>(s => s.Contains("[MAME Parameter Hint]"))), Times.Once);
        logger.Verify(l => l.Information(It.Is<string>(s => s.Contains("provided parameters"))), Times.Once);

        // The MAME dialog and the AI assist prompt are both shown; the user declines
        // the AI suggestion here, so the flow stops before the resolver call.
        messageBox.Verify(m => m.MameUnknownSystemErrorMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.AskAiToFixParametersMessageBoxAsync(), Times.Once);
    }

    /// <summary>
    ///     The full-path diagnosis detects MAME's echoed directory-separated argument
    ///     and produces the bare-machine-name guidance (bugs 67522/67529).
    /// </summary>
    [Fact]
    public void DiagnoseMameFullPathArgument_FullPath_ReturnsHint()
    {
        var output =
            "Unknown system 'J:\\ROMS [Mame]\\a1cass.zip'\n" +
            "\"J:\\ROMS [Mame]\\a1cass.zip\" approximately matches the following";

        var result = InvokeDiagnoseMameFullPathArgument(output);

        Assert.NotNull(result);
        Assert.Contains("bare machine name", result, StringComparison.Ordinal);
        Assert.Contains("J:\\ROMS [Mame]\\a1cass.zip", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A bare machine name that MAME simply does not know (wrong ROM-set name)
    ///     gets no path hint — that is a ROM-set problem, not a parameter problem.
    /// </summary>
    [Fact]
    public void DiagnoseMameFullPathArgument_BareName_ReturnsNull()
    {
        Assert.Null(InvokeDiagnoseMameFullPathArgument("Unknown system 'a1cass' approximately matches"));
    }

    /// <summary>
    ///     Output without the "Unknown system '...'" marker yields no diagnosis.
    /// </summary>
    [Fact]
    public void DiagnoseMameFullPathArgument_NoMarker_ReturnsNull()
    {
        Assert.Null(InvokeDiagnoseMameFullPathArgument("some other mame output"));
    }

    private static string? InvokeDiagnoseMameFullPathArgument(string combinedOutput)
    {
        var method = typeof(GameLauncherService).GetMethod("DiagnoseMameFullPathArgument",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string?)method.Invoke(null, [combinedOutput]);
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