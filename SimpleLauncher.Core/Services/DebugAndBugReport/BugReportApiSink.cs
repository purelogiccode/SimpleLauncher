using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.DebugAndBugReport;

/// <summary>
///     A Serilog sink that collects warning and error log events, writes them to log files, and submits them to the bug
///     report API.
/// </summary>
public class BugReportApiSink : ILogEventSink, IDisposable
{
    /// <summary>
    ///     Environment variable that disables bug report submission when set to "1". Automated tests set it so test
    ///     runs — including application processes they launch — never contact the live bug report API.
    /// </summary>
    public const string DisableBugReportsEnvironmentVariable = "SIMPLELAUNCHER_BUGREPORT_DISABLE";

    private static readonly Lock InitLock = new();

    private static readonly bool BugReportsDisabled =
        string.Equals(Environment.GetEnvironmentVariable(DisableBugReportsEnvironmentVariable), "1",
            StringComparison.Ordinal);

    private readonly Channel<LogEvent> _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _disposeLock = new();
    private IConfiguration _configuration = null!;
    private IDeleteFilesService _deleteFilesService = null!;
    private bool _disposed;
    private IHttpClientFactory _httpClientFactory = null!;
    private bool _initialized;
    private string _logFolder = null!;
    private Task _processTask = null!;

    /// <summary>
    ///     The application (entry assembly) name — e.g. "SimpleLauncher.New" for the new app,
    ///     "SimpleLauncher" for the original. Falls back to the executing assembly (Core)
    ///     and finally a hardcoded default.
    /// </summary>
    private static string ApplicationName =>
        Assembly.GetEntryAssembly()?.GetName().Name
        ?? Assembly.GetExecutingAssembly().GetName().Name
        ?? "SimpleLauncher";

    /// <summary>
    ///     The application (entry assembly) version.
    /// </summary>
    private static string ApplicationVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "Unknown";

    /// <summary>
    ///     Releases all resources used by the sink, stopping the report processing queue.
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed) return;

            _disposed = true;
        }

        // Stop accepting new events and let the consumer drain what is already
        // queued, then exit its read loop cleanly (CORE-14).
        _channel.Writer.TryComplete();

        try
        {
            // Wait for the consumer — including in-flight uploads, each bounded by
            // their own 30s timeout — so reports are not orphaned mid-upload.
            // The wait is bounded so shutdown can never hang forever.
            // (_processTask is null when Initialize was never called.)
            _processTask?.Wait(TimeSpan.FromSeconds(35));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException))
        {
            // Cancellation during shutdown is expected.
        }
#pragma warning disable RCS1075
        catch (Exception)
#pragma warning restore RCS1075
        {
            // Shutdown must never throw.
        }

        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>
    ///     Emits a log event to the sink, queuing warning and error events for reporting.
    /// </summary>
    /// <param name="logEvent">The log event to emit.</param>
    public void Emit(LogEvent logEvent)
    {
        if (_disposed) return;

        if (logEvent.Level < LogEventLevel.Warning) return;

        _channel.Writer.TryWrite(logEvent);
    }

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
        lock (InitLock)
        {
            if (_initialized) return;

            _initialized = true;

            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _deleteFilesService = deleteFilesService;
            _logFolder = logFolder;

            _processTask = ProcessQueueAsync(_cts.Token);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_channel.Reader.TryRead(out var logEvent))
                    try
                    {
                        await SendReportAsync(logEvent);
                    }
                    catch
                    {
                        WriteCriticalError(logEvent);
                    }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown via Dispose; the channel completion path above is the normal exit.
        }
    }

    private async Task SendReportAsync(LogEvent logEvent)
    {
        if (BugReportsDisabled) return;

        if (_httpClientFactory == null || _configuration == null) return;

        var report = BuildReport(logEvent);

        var apiKey = AppConstants.GetApiKey();
        if (string.IsNullOrEmpty(apiKey)) return;

        // Config-controlled file names are reduced to bare file names so an absolute
        // path or ".." escape in configuration cannot redirect writes outside the
        // log folder (CORE-18).
        var errorLogPath = Path.Combine(_logFolder,
            GetSafeLogFileName(_configuration.GetValue<string>("LogPathForAdmin"), "error.log"));

        var userLogPath = Path.Combine(_logFolder,
            GetSafeLogFileName(_configuration.GetValue<string>("LogPath"), "error_user.log"));

        if (errorLogPath != null)
        {
            await File.AppendAllTextAsync(errorLogPath, report);
            if (userLogPath != null)
            {
                await File.AppendAllTextAsync(userLogPath,
                    report +
                    "--------------------------------------------------------------------------------------------------------------\n\n\n");
            }
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("LogErrorsClient");
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);

            var payload = new
            {
                message = report,
                applicationName = ApplicationName,
                version = ApplicationVersion,
                userInfo = GetUserInfo(),
                environment = GetEnvironmentName(),
                stackTrace = BuildStackTrace(logEvent)
            };

            using var jsonContent =
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var apiUrl = _configuration.GetValue<string>("BugReportApiUrl") ??
                         "https://www.purelogiccode.com/bugreport/api/send-bug-report";

            using var response = await httpClient.PostAsync(apiUrl, jsonContent, cts.Token);

            if (response.IsSuccessStatusCode && File.Exists(errorLogPath) && _deleteFilesService != null)
            {
                try
                {
                    await _deleteFilesService.TryDeleteFileAsync(errorLogPath);
                }
                catch
                {
                    // Ignore deletion failures
                }
            }
        }
        catch
        {
            WriteCriticalError(logEvent);
        }
    }

    /// <summary>
    ///     Reduces a config-controlled log file value to a bare file name confined to the
    ///     log folder. Absolute paths and directory components (including ".." escapes)
    ///     are stripped; blank results fall back to <paramref name="fallback" /> (CORE-18).
    /// </summary>
    private static string GetSafeLogFileName(string? configuredValue, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(configuredValue)
            ? fallback
            : Path.GetFileName(configuredValue.Trim());

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private void WriteCriticalError(LogEvent logEvent)
    {
        try
        {
            var criticalLogPath = Path.Combine(_logFolder,
                GetSafeLogFileName(_configuration?.GetValue<string>("LogPathCritical"), "critical_error.log"));
            var report = BuildReport(logEvent) +
                         "\n--------------------------------------------------------------------------------------------------------------\n\n\n";

            if (criticalLogPath != null) File.AppendAllText(criticalLogPath, report);
        }
        catch
        {
            // Can't do anything more
        }
    }

    private static string BuildReport(LogEvent logEvent)
    {
        var message = new StringBuilder();

        message.AppendLine("=== Environment Details ===");
        message.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTime.Now}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Application Name: {ApplicationName}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Application Version: {ApplicationVersion}");
        message.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {RuntimeInformation.OSDescription}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Architecture: {RuntimeInformation.OSArchitecture}");
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Bitness: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Windows Version: {GetMicrosoftWindowsVersion.GetVersion()}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Processor Count: {Environment.ProcessorCount}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Base Directory: {AppContext.BaseDirectory}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Temp Path: {Path.GetTempPath()}");
        message.AppendLine();

        message.AppendLine("=== Error Details ===");
        message.AppendLine(CultureInfo.InvariantCulture, $"Log Level: {logEvent.Level}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Error message: {logEvent.RenderMessage()}");
        message.AppendLine();

        message.AppendLine("=== Exception Details ===");
        if (logEvent.Exception == null)
        {
            message.AppendLine("Type: None");
            message.AppendLine("Message: None");
            message.AppendLine("Source: None");
            message.AppendLine("StackTrace: None");
        }
        else
        {
            AppendException(message, logEvent.Exception);
            if (logEvent.Exception.InnerException != null)
            {
                message.AppendLine();
                message.AppendLine("--- Inner Exception ---");
                AppendException(message, logEvent.Exception.InnerException);
            }
        }

        return message.ToString();
    }

    private static void AppendException(StringBuilder message, Exception exception)
    {
        message.AppendLine(CultureInfo.InvariantCulture, $"Type: {exception.GetType().FullName}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Message: {exception.Message}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Source: {exception.Source}");
        message.AppendLine(CultureInfo.InvariantCulture, $"StackTrace: {exception.StackTrace}");
    }

    private static string? BuildStackTrace(LogEvent logEvent)
    {
        if (logEvent.Exception == null) return null;

        var sb = new StringBuilder();
        var currentEx = logEvent.Exception;
        var depth = 0;
        const int maxDepth = 10;

        while (currentEx != null && depth < maxDepth)
        {
            if (depth > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- INNER EXCEPTION ---");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"Exception Type: {currentEx.GetType().FullName}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {currentEx.Message}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Source: {currentEx.Source}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"StackTrace: {currentEx.StackTrace}");

            currentEx = currentEx.InnerException;
            depth++;
        }

        if (currentEx != null)
        {
            sb.AppendLine();
            sb.AppendLine("--- ADDITIONAL INNER EXCEPTIONS TRUNCATED ---");
        }

        return sb.ToString();
    }

    private static string? GetUserInfo()
    {
        try
        {
            return Environment.MachineName;
        }
        catch
        {
            return null;
        }
    }

    private static string GetEnvironmentName()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}