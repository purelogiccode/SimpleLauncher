using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Diagnostics;
using DiagnosticsSink = SimpleLauncher.Diagnostics.BugReportApiSink;

namespace SimpleLauncher.Core.Services.DebugAndBugReport;

/// <summary>
///     Core-facing adapter for the shared <see cref="DiagnosticsSink" />. Keeps the existing
///     dependency-injection initialization contract used by the Avalonia and WPF applications.
/// </summary>
public class BugReportApiSink : DiagnosticsSink
{
    /// <summary>
    ///     Initializes the sink with the services and log folder needed to submit bug reports.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create the HTTP client for bug report submissions.</param>
    /// <param name="configuration">The application configuration containing API and log path settings.</param>
    /// <param name="deleteFilesService">The service used to delete log files after successful submission.</param>
    /// <param name="logFolder">The folder where log files are written.</param>
    public void Initialize(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IDeleteFilesService deleteFilesService,
        string logFolder)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(deleteFilesService);

        var options = new BugReportSinkOptions
        {
            LogFolder = logFolder,
            ApiKey = AppConstants.GetApiKey(),
            // The main application must not silently use the embedded key when its configured
            // key is missing; a misconfigured app should stop reporting instead.
            UseDefaultApiKey = false,
            ApiUrl = configuration.GetValue<string>("BugReportApiUrl") ??
                     "https://www.purelogiccode.com/bugreport/api/send-bug-report",
            ErrorLogFileName = configuration.GetValue<string>("LogPathForAdmin") ?? "error.log",
            UserLogFileName = configuration.GetValue<string>("LogPath") ?? "error_user.log",
            CriticalLogFileName = configuration.GetValue<string>("LogPathCritical") ?? "critical_error.log",
            HttpClientFactory = () => httpClientFactory.CreateClient("LogErrorsClient"),
            DeleteFileAsync = deleteFilesService.TryDeleteFileAsync
        };

        Initialize(options);
    }
}
