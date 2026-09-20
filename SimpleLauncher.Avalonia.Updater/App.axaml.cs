using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Serilog.Events;
using SimpleLauncher.Avalonia.Updater.Services;
using SimpleLauncher.Avalonia.Updater.Services.DebugAndBugReport;

namespace SimpleLauncher.Avalonia.Updater;

/// <summary>
///     Application entry point for the single updater (Updater.exe) shared by the WPF
///     and Avalonia apps — sets up logging, global exception handling, and shows the
///     update window.
/// </summary>
public class App : Application
{
    /// <summary>
    ///     Initializes the application: logging, bug-report sink and exception handlers.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        Dispatcher.UIThread.UnhandledException += App_DispatcherUnhandledException;
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

    /// <summary>
    ///     WPF updater parity: log the exception, report it, tell the user and close instead of
    ///     letting the UI thread crash the process with no explanation.
    /// </summary>
    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI thread exception");
        _ = BugReportService.ReportBugAsync(e.Exception, "Unhandled UI thread exception");

        // Mark it observed so the process survives long enough to show the dialog and shut down.
        e.Handled = true;

        var lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var window = lifetime?.MainWindow;

        // Post (never block inside the handler): show the WPF-parity dialog, then exit with the
        // WPF updater's failure code.
        Dispatcher.UIThread.Post(() => _ = ShowUnhandledExceptionDialogAsync(lifetime, window, e.Exception));
    }

    private static async Task ShowUnhandledExceptionDialogAsync(
        IClassicDesktopStyleApplicationLifetime? lifetime, Window? window, Exception exception)
    {
        try
        {
            if (window is { IsVisible: true })
            {
                await DialogHelper.ShowMessageAsync(window,
                    $"An unexpected error occurred: {exception.Message}\n\n" +
                    "This error has been reported. The application will now close.",
                    "Error");
            }
        }
        catch (Exception dialogException)
        {
            Log.Warning(dialogException, "Failed to show the unhandled-exception dialog");
        }
        finally
        {
            if (lifetime is null)
                Environment.Exit(1);
            else
                lifetime.Shutdown(1);
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        _ = BugReportService.ReportBugAsync(e.Exception, "Unobserved task exception in Updater");
        e.SetObserved();
    }
}