using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SimpleLauncher.Avalonia.Updater.Services;

namespace SimpleLauncher.Avalonia.Updater;

/// <summary>
///     Main window for the Avalonia Updater that manages the update process for SimpleLauncher.Avalonia.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string AppDirectory = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    ///     Shared HttpClient instance for the entire Updater application.
    /// </summary>
    internal static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    // Files to exclude during extraction to prevent self-destruction
    private static readonly string[] IgnoredFiles =
    [
        "SimpleLauncher.Avalonia.Updater",
        "SimpleLauncher.Avalonia.Updater.exe",
        "SimpleLauncher.Avalonia.Updater.pdb",
        "SimpleLauncher.Avalonia.Updater.dll",
        "SimpleLauncher.Avalonia.Updater.deps.json",
        "SimpleLauncher.Avalonia.Updater.runtimeconfig.json"
    ];

    private readonly string[] _args;
    private readonly CancellationTokenSource _cts;

    private readonly UpdateService _updateService;

    static MainWindow()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SimpleLauncher-Avalonia-Updater");
    }

    /// <summary>
    ///     Parameterless constructor for the Avalonia runtime resource loader.
    /// </summary>
    public MainWindow() : this([])
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindow" /> class.
    /// </summary>
    /// <param name="args">Command line arguments, typically containing the process ID of the main application.</param>
    public MainWindow(string[] args)
    {
        InitializeComponent();
        _args = args;

        // Initialize services
        _updateService = CreateUpdateService();
        WireUpServiceEvents();

        Opened += (_, _) =>
        {
            Activate();
            Topmost = false; // Release topmost after initial show so user can switch away if needed
        };

        var applicationVersion = GetApplicationVersion();
        Log($"Updater version: {applicationVersion}\n");

        // Start update process async when window is loaded
        _cts = new CancellationTokenSource();
        Opened += async (_, _) => await ExecuteUpdateAsync(_cts.Token);

        // UPD-13: release the update cancellation source with the window.
        Closed += (_, _) =>
        {
            _cts.Cancel();
            _cts.Dispose();
        };
    }

    /// <summary>
    ///     Disposes the HttpClient instance. Should be called when the application is shutting down.
    ///     UPD-13: WPF parity — the Avalonia updater previously never disposed it.
    /// </summary>
    public static void DisposeHttpClient()
    {
        HttpClient.Dispose();
    }

    /// <summary>
    ///     Creates and configures the UpdateService with all required dependencies.
    /// </summary>
    private static UpdateService CreateUpdateService()
    {
        var gitHubService = new GitHubService(HttpClient);
        var downloadService = new DownloadService(HttpClient);
        var zipService = new ZipService(AppDirectory);
        var processService = new ProcessService();
        var dokanService = new DokanService(downloadService);

        return new UpdateService(gitHubService, downloadService, zipService, processService, dokanService,
            AppDirectory);
    }

    /// <summary>
    ///     Wires up event handlers for the UpdateService events.
    /// </summary>
    private void WireUpServiceEvents()
    {
        _updateService.LogMessage += (_, e) => Log(e.Value);
        _updateService.DownloadProgressChanged += (_, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                DownloadProgressBar.Value = e.Percentage;
                ProgressStatusText.Text = e.StatusText;
            });
        };
        _updateService.DownloadProgressReset += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                DownloadProgressBar.Value = 0;
                ProgressStatusText.Text = "Preparing download...";
            });
        };
        _updateService.ExtractionStarted += (_, _) =>
            Dispatcher.UIThread.Post(() => ProgressStatusText.Text = "Extracting files...");
        _updateService.ExtractionProgressChanged +=
            (_, e) => Dispatcher.UIThread.Post(() => ProgressStatusText.Text = e.StatusText);
        _updateService.ExtractionCompleted += (_, _) =>
            Dispatcher.UIThread.Post(() => ProgressStatusText.Text = "Extraction complete");
        _updateService.DokanInstallationPrompt += () =>
        {
            if (!OperatingSystem.IsWindows()) return Task.FromResult(false); // Dokan is Windows-only

            return Dispatcher.UIThread.InvokeAsync(() => DialogHelper.ShowYesNoAsync(this,
                "Dokan library is not installed.\n\n" +
                "Dokan is required for mounting ZIP, CHD and disk image files.\n\n" +
                "Do you want to download and install it now?",
                "Dokan Not Found"));
        };
    }

    /// <summary>
    ///     Gets the version of the currently executing assembly.
    /// </summary>
    private static string GetApplicationVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "Version not available";
    }

    /// <summary>
    ///     Executes the update process asynchronously with error handling and bug reporting.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the update operation.</param>
    private async Task ExecuteUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Parse process ID from command line arguments. UPD-08: the PID is validated
            // against the expected main-app process name — an unrelated/reused PID is
            // ignored (falling back to the by-name wait) instead of stalling the update.
            int? processId = null;
            if (_args.Length > 0 &&
                int.TryParse(_args[0], CultureInfo.InvariantCulture, out var pid) && pid > 0)
            {
                processId = ProcessService.ValidateProcessId(pid);
                if (!processId.HasValue)
                    Log($"Ignoring invalid process ID argument: {_args[0]} — waiting by process name instead.");
            }

            CancelButton.IsEnabled = true;

            // Execute the update through the service
            var result = await _updateService.ExecuteUpdateAsync(processId, IgnoredFiles, cancellationToken);

            if (result.Success)
            {
                await DialogHelper.ShowMessageAsync(this, "Update installed successfully.", "Success");

                // Check if Dokan is installed and offer to install it if missing (Windows only)
                if (OperatingSystem.IsWindows()) await _updateService.CheckAndInstallDokanAsync();

                _updateService.RestartMainApplication();
                Close();
            }
            else if (result.RequiresManualUpdate)
            {
                await RedirectToDownloadPage(result.ErrorMessage ??
                                             "Automatic update failed.\n\nWould you like to update manually?");
            }
        }
        catch (OperationCanceledException)
        {
            Serilog.Log.Information("Update was cancelled by the user");
            Log("Update was cancelled by the user.");
            ProgressStatusText.Text = "Cancelled";
            CancelButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            // Report bug to the bug report API
            Serilog.Log.Error(ex, "Error during main update execution");
            await BugReportService.ReportBugAsync(ex, "Error during main update execution");

            Log($"An error occurred during update process: {ex.Message}");
            Log("Please update manually.");
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        CancelButton.IsEnabled = false;
        Log("Cancelling update...");
    }

    private async Task RedirectToDownloadPage(string message)
    {
        var result = await DialogHelper.ShowYesNoAsync(this, message, "Error");
        if (result) _updateService.OpenManualDownloadPage();

        Close();
    }

    private void Log(string message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Log(message));
            return;
        }

        if (IsLoaded)
        {
            try
            {
                LogTextBox.Text += $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}";
                LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
            }
            catch (InvalidOperationException ex)
            {
                // Window may have been closed, ignore logging but report bug (fire-and-forget)
                _ = ReportBugFireAndForgetAsync(ex, "Error logging message to UI");
            }
        }
    }

    /// <summary>
    ///     Fire-and-forget helper for reporting bugs from synchronous contexts.
    /// </summary>
    private static async Task ReportBugFireAndForgetAsync(Exception exception, string context)
    {
        try
        {
            await BugReportService.ReportBugAsync(exception, context);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to report bug for context: {Context}", context);
            Serilog.Log.Warning(exception, "Original exception");
        }
    }
}