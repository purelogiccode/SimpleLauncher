using System;
using System.IO;
using System.Windows;
using Serilog.Events;
using XmlToBinaryConverter.Services.DebugAndBugReport;

namespace XmlToBinaryConverter;

/// <summary>
///     Application entry point for the XML to Binary Converter.
/// </summary>
public partial class App
{
    /// <summary>Configures Serilog and the bug-report sink before the window opens.</summary>
    /// <param name="e">The startup event arguments.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        var appDataLogFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(appDataLogFolder);

        var bugReportSink = new BugReportApiSink(appDataLogFolder);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: "[{Level}] {Timestamp:HH:mm:ss.fff} - {Message}{NewLine}{Exception}")
            .WriteTo.Async(a => a.File(
                Path.Combine(appDataLogFolder, "error_user.log"),
                LogEventLevel.Warning,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
            .WriteTo.Sink(bugReportSink)
            .CreateLogger();

        base.OnStartup(e);
    }

    /// <summary>Flushes Serilog before the process exits.</summary>
    /// <param name="e">The exit event arguments.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}