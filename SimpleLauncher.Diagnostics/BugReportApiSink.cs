using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Serilog.Core;
using Serilog.Events;

namespace SimpleLauncher.Diagnostics;

/// <summary>
///     A Serilog sink that collects warning and error log events, writes them to log files, and
///     submits them to the bug-report API. Shared by the Avalonia app, the updater and the bundled
///     command-line tools.
/// </summary>
public class BugReportApiSink : ILogEventSink, IDisposable
{
    /// <summary>
    ///     Environment variable that disables bug report submission when set to "1". Automated tests set it so test
    ///     runs - including application processes they launch - never contact the live bug report API.
    /// </summary>
    public const string DisableBugReportsEnvironmentVariable = "SIMPLELAUNCHER_BUGREPORT_DISABLE";

    /// <summary>
    ///     Maximum length accepted by the bug-report API for the <c>message</c> field.
    /// </summary>
    internal const int MaxMessageLength = 4000;

    /// <summary>
    ///     Maximum length accepted by the bug-report API for the <c>stackTrace</c> field.
    /// </summary>
    internal const int MaxStackTraceLength = 8000;

    private const string DefaultApiUrl = "https://www.purelogiccode.com/bugreport/api/send-bug-report";
    private const string TruncationSuffix = "\n... [report truncated by the client]";

    private const string ApiKeyEncoded =
        "YUdwb04zbDFOblExTm5SNWNqVTBNRzg1ZFRnM05qYzJOelp5TlRZM05EVXpORFExTXpJek5USTJOR00zTldJMmREZG5aMmRvWjJjM05uUnlaalUyTkdVPQ==";

    private static readonly Lock InitLock = new();

    private static readonly bool BugReportsDisabled =
        string.Equals(Environment.GetEnvironmentVariable(DisableBugReportsEnvironmentVariable), "1",
            StringComparison.Ordinal);

    private static readonly string DefaultApiKey = DecodeApiKey();

    private readonly Channel<LogEvent> _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _disposeLock = new();
    private bool _disposed;
    private BugReportSinkOptions? _options;
    private HttpClient? _ownedHttpClient;
    private Task? _processTask;

    /// <summary>
    ///     Initializes a sink that stays idle until <see cref="Initialize(BugReportSinkOptions)" /> is called.
    ///     Used by applications whose HTTP client and delete service are created after the logger.
    /// </summary>
    public BugReportApiSink()
    {
    }

    /// <summary>
    ///     Initializes a sink that starts submitting reports immediately.
    /// </summary>
    /// <param name="options">The sink configuration.</param>
    public BugReportApiSink(BugReportSinkOptions options)
    {
        Initialize(options);
    }

    /// <summary>
    ///     The application name - e.g. "SimpleLauncher" for the Windows release and
    ///     "SimpleLauncher.Avalonia" for the Linux/macOS builds. Prefers the running
    ///     executable so a renamed single-file publish reports the name users see.
    /// </summary>
    internal static string ApplicationName
    {
        get
        {
            var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(processName)) return processName;

            return Assembly.GetEntryAssembly()?.GetName().Name
                   ?? Assembly.GetExecutingAssembly().GetName().Name
                   ?? "SimpleLauncher";
        }
    }

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
            // Wait for the consumer - including in-flight uploads, each bounded by
            // their own 30s timeout - so reports are not orphaned mid-upload.
            // The wait is bounded so shutdown can never hang forever.
            // (_processTask is null when Initialize was never called.)
            _processTask?.Wait(TimeSpan.FromSeconds(35));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException))
        {
            // Cancellation during shutdown is expected.
        }
#pragma warning disable RCS1075
        catch (Exception ex)
#pragma warning restore RCS1075
        {
            // Shutdown must never throw. Log at Debug so the sink cannot recursively
            // enqueue another warning while it is being disposed.
            Log.Debug(ex, "Bug report sink shutdown did not complete cleanly");
        }

        _cts.Cancel();
        _cts.Dispose();
        _ownedHttpClient?.Dispose();
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
    ///     Initializes the sink with the configuration needed to submit bug reports.
    /// </summary>
    /// <param name="options">The sink configuration. Only the first call is honored.</param>
    public void Initialize(BugReportSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (InitLock)
        {
            if (_options != null) return;

            _options = options;

            // Run the consumer on the thread pool. Initialize is called from the UI thread
            // (App.OnStartup), and awaiting the channel there captures the UI synchronization
            // context: Dispose — which blocks that same UI thread while waiting for the
            // consumer — would then deadlock until the 35 s wait times out, keeping the
            // process alive with a closed main window and a clickable tray icon.
            _processTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_channel.Reader.TryRead(out var logEvent))
                {
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
        }
        catch (OperationCanceledException)
        {
            // Shutdown via Dispose; the channel completion path above is the normal exit.
        }
    }

    private async Task SendReportAsync(LogEvent logEvent)
    {
        if (BugReportsDisabled) return;

        var options = _options;
        if (options is null) return;

        var apiKey = ResolveApiKey(options);
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var report = BuildReport(logEvent);

        // Config-controlled file names are reduced to bare file names so an absolute
        // path or ".." escape in configuration cannot redirect writes outside the
        // log folder (CORE-18).
        var errorLogPath = Path.Combine(options.LogFolder,
            GetSafeLogFileName(options.ErrorLogFileName, "error.log"));

        var userLogPath = Path.Combine(options.LogFolder,
            GetSafeLogFileName(options.UserLogFileName, "error_user.log"));

        try
        {
            // Persist the report first. The appends stay inside this try: if the log
            // folder is missing/read-only, the failure is handled by the catch below
            // (WriteCriticalError never throws) instead of escaping SendReportAsync and
            // silently killing the processing loop.
            await File.AppendAllTextAsync(errorLogPath, report);
            await File.AppendAllTextAsync(userLogPath,
                report +
                "--------------------------------------------------------------------------------------------------------------\n\n\n");

            var payload = new
            {
                message = TruncateForApi(report, MaxMessageLength),
                applicationName = ApplicationName,
                version = ApplicationVersion,
                userInfo = GetUserInfo(),
                environment = GetEnvironmentName(),
                stackTrace = TruncateForApi(BuildStackTrace(logEvent), MaxStackTraceLength)
            };

            var apiUrl = string.IsNullOrWhiteSpace(options.ApiUrl)
                ? DefaultApiUrl
                : options.ApiUrl.TrimEnd('/');

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("X-API-KEY", apiKey);
            using var jsonContent =
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            request.Content = jsonContent;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var httpClient = ResolveHttpClient(options);
            using var response = await httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode && File.Exists(errorLogPath))
            {
                try
                {
                    if (options.DeleteFileAsync != null)
                        await options.DeleteFileAsync(errorLogPath);
                    else
                        File.Delete(errorLogPath);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Could not delete the submitted error log {ErrorLogPath}", errorLogPath);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Bug report submission failed; writing to the critical log");
            WriteCriticalError(logEvent);
        }
    }

    private static string? ResolveApiKey(BugReportSinkOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey)) return options.ApiKey;
        return options.UseDefaultApiKey ? DefaultApiKey : null;
    }

    private HttpClient ResolveHttpClient(BugReportSinkOptions options)
    {
        if (options.HttpClientFactory != null) return options.HttpClientFactory();
        return _ownedHttpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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
        var options = _options;
        if (options is null) return;

        try
        {
            var criticalLogPath = Path.Combine(options.LogFolder,
                GetSafeLogFileName(options.CriticalLogFileName, "critical_error.log"));
            var report = BuildReport(logEvent) +
                         "\n--------------------------------------------------------------------------------------------------------------\n\n\n";

            File.AppendAllText(criticalLogPath, report);
        }
        catch (Exception ex)
        {
            // The bug-report sink must never throw; there is nowhere left to persist the report.
            Log.Debug(ex, "Could not write the critical bug report fallback log");
        }
    }

    /// <summary>
    ///     Builds the human-readable bug-report body. Exposed to the test assemblies through
    ///     <c>InternalsVisibleTo</c> so the required environment/error/exception sections are
    ///     covered without contacting the live API.
    /// </summary>
    /// <param name="logEvent">The warning-or-higher event being reported.</param>
    /// <returns>The complete report text.</returns>
    internal static string BuildReport(LogEvent logEvent)
    {
        var message = new StringBuilder();

        message.Append(BuildEnvironmentDetails());
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
            AppendExceptionChain(message, logEvent.Exception);
        }

        return message.ToString();
    }

    /// <summary>
    ///     Builds the environment block that every report must contain.
    /// </summary>
    /// <returns>The environment details, including the cross-platform OS version.</returns>
    internal static string BuildEnvironmentDetails()
    {
        var message = new StringBuilder();
        message.AppendLine("=== Environment Details ===");
        message.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Application Name: {ApplicationName}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Application Version: {ApplicationVersion}");
        message.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {RuntimeInformation.OSDescription}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Architecture: {RuntimeInformation.OSArchitecture}");
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Bitness: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Windows, Linux or MacOsX Version: {OperatingSystemDetails.GetDescription()}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Processor Count: {Environment.ProcessorCount}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Base Directory: {AppContext.BaseDirectory}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Temp Path: {Path.GetTempPath()}");
        return message.ToString();
    }

    private static void AppendExceptionChain(StringBuilder message, Exception exception)
    {
        var current = exception;
        var depth = 0;
        const int maxDepth = 10;

        while (current is not null && depth < maxDepth)
        {
            if (depth > 0)
            {
                message.AppendLine();
                message.AppendLine("--- Inner Exception ---");
            }

            AppendException(message, current);
            current = current.InnerException;
            depth++;
        }

        if (current is not null)
        {
            message.AppendLine();
            message.AppendLine("--- Additional inner exceptions truncated ---");
        }
    }

    private static void AppendException(StringBuilder message, Exception exception)
    {
        message.AppendLine(CultureInfo.InvariantCulture, $"Type: {exception.GetType().FullName}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Message: {exception.Message}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Source: {exception.Source}");
        message.AppendLine(CultureInfo.InvariantCulture, $"StackTrace: {exception.StackTrace}");
    }

    /// <summary>
    ///     Truncates a value to the API field limit while keeping a visible marker so the
    ///     receiver knows the payload was shortened. Null values stay null.
    /// </summary>
    /// <param name="value">Value to truncate.</param>
    /// <param name="maxLength">Maximum accepted length.</param>
    /// <returns>The original value or a truncated copy.</returns>
    internal static string? TruncateForApi(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;

        var keep = Math.Max(0, maxLength - TruncationSuffix.Length);
        return value[..keep] + TruncationSuffix;
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
        catch (InvalidOperationException ex)
        {
            Log.Debug(ex, "Could not read the machine name for the bug report");
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

    private static string DecodeApiKey()
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(ApiKeyEncoded));
            return Encoding.UTF8.GetString(Convert.FromBase64String(decoded));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not decode the embedded bug report API key");
            return string.Empty;
        }
    }
}
