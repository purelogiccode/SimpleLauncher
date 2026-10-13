using System.Reflection;
using Microsoft.Extensions.Configuration;
using Moq;
using Serilog.Core;
using Serilog.Events;
using SimpleLauncher.Avalonia.Services.GameLauncher;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression tests for the post-exit error analysis: an emulator the app launched
///     successfully but which then died is an emulator-side/environment outcome, not an app
///     bug, so it must be logged at Information level (never Warning+, which reaches the
///     bug-report sink). Bugs 67748/67749 filed reports for a RetroArch AppImage that died
///     with exit code 135 before producing any output. The user-facing dialogs are unchanged.
///     The analysis logs through the static Serilog sink, so the tests install a recording
///     sink and filter by message text (no other test emits these messages).
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
        var sink = new ListSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var messageBox = new Mock<IMessageBoxLibraryService>();
            var service = CreateService(messageBox.Object);
            var emulator = new Emulator
            {
                EmulatorName = "RetroArch",
                EmulatorLocation = "/home/vm/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64.AppImage",
                ReceiveANotificationOnEmulatorError = false
            };

            await InvokeAnalyzeProcessExitAsync(service, 135, "",
                "Setting $HOME to /home/vm/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64.AppImage.home",
                "/home/vm/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64.AppImage",
                "-L cores/snes9x_libretro.so -f game.zip", "RetroArch",
                "/home/vm/SimpleLauncher/emulators/RetroArch", emulator);

            var relevant = sink.EventsMatching("RetroArch parameter issue.");
            Assert.Single(relevant);
            Assert.Equal(LogEventLevel.Information, relevant[0].Level);
            messageBox.Verify(m => m.RetroArchParameterIssueMessageBoxAsync(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    /// <summary>
    ///     Verifies that the generic fallback for any other unclassified non-zero exit also
    ///     logs at Information, never Warning/Error.
    /// </summary>
    [Fact]
    public async Task GenericUnclassifiedExitLogsInformationNeverWarningOrError()
    {
        var sink = new ListSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var messageBox = new Mock<IMessageBoxLibraryService>();
            var service = CreateService(messageBox.Object);
            var emulator = new Emulator
            {
                EmulatorName = "Dolphin",
                EmulatorLocation = "/home/vm/SimpleLauncher/emulators/Dolphin/dolphin",
                ReceiveANotificationOnEmulatorError = false
            };

            await InvokeAnalyzeProcessExitAsync(service, 135, "some output", "",
                "/home/vm/SimpleLauncher/emulators/Dolphin/dolphin", "game.iso", "Dolphin",
                "/home/vm/SimpleLauncher/emulators/Dolphin", emulator);

            var relevant = sink.EventsMatching("Emulator exited with error.");
            Assert.Single(relevant);
            Assert.Equal(LogEventLevel.Information, relevant[0].Level);
            messageBox.Verify(m => m.CouldNotLaunchGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    /// <summary>
    ///     Verifies that exit code 1113 (ERROR_NO_UNICODE_TRANSLATION — the emulator
    ///     could not convert a Unicode ROM path to its ANSI code page, bug 67583) is
    ///     classified as an expected path/environment condition: its dedicated branch
    ///     logs at Information, never Warning/Error.
    /// </summary>
    [Fact]
    public async Task UnicodePathConversionExit1113LogsInformationNeverWarningOrError()
    {
        var sink = new ListSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var messageBox = new Mock<IMessageBoxLibraryService>();
            var service = CreateService(messageBox.Object);
            var emulator = new Emulator
            {
                EmulatorName = "Ymir",
                EmulatorLocation = @"D:\Emulators\Saturn\ymir-sdl3.exe",
                ReceiveANotificationOnEmulatorError = false
            };

            await InvokeAnalyzeProcessExitAsync(service, 1113, "",
                "System error: No mapping for the Unicode character exists in the target multi-byte code page.",
                @"D:\Emulators\Saturn\ymir-sdl3.exe",
                "\"D:\\ROMS\\Dの食卓\\Dの食卓Disc1.cue\"", "Ymir", @"D:\Emulators\Saturn", emulator);

            var relevant = sink.EventsMatching("Emulator Unicode/ANSI path conversion error.");
            Assert.Single(relevant);
            Assert.Equal(LogEventLevel.Information, relevant[0].Level);
            messageBox.Verify(m => m.CouldNotLaunchGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    /// <summary>
    ///     Bugs 67522/67529: MAME echoing a full ROM path as the system name logs the
    ///     dedicated parameter hint and the unknown-system error, both at Information —
    ///     never Warning/Error (notifications disabled: dialogs are covered by the WPF
    ///     suite, this pins the log classification).
    /// </summary>
    [Fact]
    public async Task MameUnknownSystemWithFullPath_LogsParameterHintAtInformation()
    {
        var sink = new ListSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var messageBox = new Mock<IMessageBoxLibraryService>();
            var service = CreateService(messageBox.Object);
            var emulator = new Emulator
            {
                EmulatorName = "MAME",
                EmulatorLocation = @"D:\MAME\mame.exe",
                ReceiveANotificationOnEmulatorError = false
            };

            var mameError =
                "Unknown system 'J:\\ROMS [Mame]\\MAME 0.289 ROMs (merged)\\a1cass.zip'\n" +
                "\"J:\\ROMS [Mame]\\MAME 0.289 ROMs (merged)\\a1cass.zip\" approximately matches the following\n" +
                "supported machines (best match first):\n" +
                "roma2             Mephisto Roma II";

            await InvokeAnalyzeProcessExitAsync(service, 5, "", mameError,
                @"D:\MAME\mame.exe", "-rompath \"J:\\roms\" \"J:\\roms\\a1cass.zip\"",
                "MAME", @"D:\MAME", emulator);

            var hints = sink.EventsMatching("[MAME Parameter Hint]");
            Assert.Single(hints);
            Assert.Equal(LogEventLevel.Information, hints[0].Level);

            var unknownSystem = sink.EventsMatching("MAME Unknown system error.");
            Assert.Single(unknownSystem);
            Assert.Equal(LogEventLevel.Information, unknownSystem[0].Level);

            Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
            messageBox.Verify(m => m.CouldNotLaunchGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            Log.Logger = previous;
        }
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

    private static string? InvokeDiagnoseMameFullPathArgument(string combinedOutput)
    {
        var method = typeof(LauncherService).GetMethod("DiagnoseMameFullPathArgument",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string?)method.Invoke(null, [combinedOutput]);
    }

    private static LauncherService CreateService(IMessageBoxLibraryService messageBox)
    {
        return new LauncherService(messageBox, [], new ConfigurationBuilder().Build(), null!, null!, null!,
            null!, null!, null!, [], null!, null!, null!, null!);
    }

    private static Task InvokeAnalyzeProcessExitAsync(LauncherService service, int exitCode, string stdout,
        string stderr, string emulatorPath, string arguments, string emulatorName, string? emulatorLocation,
        Emulator emulator)
    {
        var method = typeof(LauncherService).GetMethod("AnalyzeProcessExitAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task?)method.Invoke(service,
            [exitCode, stdout, stderr, emulatorPath, arguments, emulatorName, emulatorLocation, emulator,
                Mock.Of<ISystemManager>(), null]);
        Assert.NotNull(task);
        return task;
    }

    private sealed class ListSink : ILogEventSink
    {
        private readonly Lock _sync = new();
        private readonly List<LogEvent> _events = new();

        public void Emit(LogEvent logEvent)
        {
            lock (_sync)
            {
                _events.Add(logEvent);
            }
        }

        public IReadOnlyList<LogEvent> Events
        {
            get
            {
                lock (_sync)
                {
                    return _events.ToList();
                }
            }
        }

        public IReadOnlyList<LogEvent> EventsMatching(string text)
        {
            lock (_sync)
            {
                return _events.Where(e => e.RenderMessage().Contains(text, StringComparison.Ordinal)).ToList();
            }
        }
    }
}
