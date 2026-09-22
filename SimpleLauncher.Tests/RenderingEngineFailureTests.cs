using System.Reflection;
using System.Runtime.InteropServices;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     This collection must never run in parallel with other test classes: its tests
///     swap the global Serilog <c>Log.Logger</c>, which leaks across test classes while
///     the static lives.
/// </summary>
[CollectionDefinition(nameof(GlobalLogMutating), DisableParallelization = true)]
public sealed class GlobalLogMutating;

/// <summary>
///     Regression tests for bug #67295: a WPF rendering-thread failure
///     (UCEERR_RENDERTHREADFAILURE, 0x88980406) was logged at Error, so every GPU-driver
///     crash / remote-desktop session filed a bug report. It is an expected external
///     condition the app cannot fix — it must be logged at Information only.
/// </summary>
[Collection(nameof(GlobalLogMutating))]
public class RenderingEngineFailureTests
{
    [Fact]
    public void UceErrRenderThreadFailure_IsLoggedAtInformation_NotAsBug()
    {
        var sink = new CaptureSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var exception = new COMException("UCEERR_RENDERTHREADFAILURE (0x88980406)",
                unchecked((int)0x88980406));
            InvokeReportException(exception, "Unhandled dispatcher exception.");

            Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
            Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public void OtherUnhandledExceptions_AreStillLoggedAtError()
    {
        var sink = new CaptureSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        try
        {
            InvokeReportException(new InvalidOperationException("real bug"), "Unhandled dispatcher exception.");

            Assert.Contains(sink.Events, static e => e.Level >= LogEventLevel.Error);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    private static void InvokeReportException(Exception exception, string contextMessage)
    {
        var method = typeof(App).GetMethod("ReportException", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(null, [exception, contextMessage]);
    }

    /// <summary>Serilog sink that records every emitted event for level assertions.</summary>
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            lock (Events)
            {
                Events.Add(logEvent);
            }
        }
    }
}