using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Serilog.Events;
using SimpleLauncher.Avalonia.Updater.Services;
using SimpleLauncher.Avalonia.Updater.Services.DebugAndBugReport;

namespace SimpleLauncher.Avalonia.Updater;

/// <summary>
///     Application entry point for the Avalonia Updater — sets up logging, global
///     exception handling, and shows the update window.
/// </summary>
public class App : Application
{
    /// <summary>
    ///     Initializes the application: logging, bug-report sink and exception handlers.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

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

        Log.Information("Updater starting...");

        // WPF updater parity: report a launch event.
        ApplicationStats.SendLaunchStats();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var mainWindow = new MainWindow(args);
            desktop.MainWindow = mainWindow;

            // UPD-13: WPF parity — dispose the shared HttpClient, the bug-report sink
            // resources and flush Serilog on exit (previously leaked every run).
            desktop.Exit += (_, _) =>
            {
                MainWindow.DisposeHttpClient();
                BugReportService.Dispose();
                Log.CloseAndFlush();
            };

            mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
            _ = BugReportService.ReportBugAsync(ex, "Unhandled exception in Updater");
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}