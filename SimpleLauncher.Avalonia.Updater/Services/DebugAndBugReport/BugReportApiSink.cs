using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Serilog.Core;
using Serilog.Events;

namespace SimpleLauncher.Avalonia.Updater.Services.DebugAndBugReport;

/// <summary>
///     A Serilog sink that sends warning-level and above log events to the bug report API,
///     with local file fallback when the API is unavailable.
/// </summary>
internal class BugReportApiSink : ILogEventSink, IDisposable
{
    private const string ApiUrl = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    private const string ApiKeyEncoded =
        "YUdwb04zbDFOblExTm5SNWNqVTBNRzg1ZFRnM05qYzJOelp5TlRZM05EVXpORFExTXpJek5USTJOR00zTldJMmREZG5aMmRvWjJjM05uUnlaalUyTkdVPQ==";

    private const int MaxMessageLength = 4000;
    private const int MaxStackTraceLength = 8000;
    private const string TruncationSuffix = "\n... [report truncated by the client]";

    /// <summary>
    ///     Environment variable that disables bug report submission when set to "1". Automated tests set it so test
    ///     runs — including application processes they launch — never contact the live bug report API.
    /// </summary>
    public const string DisableBugReportsEnvironmentVariable = "SIMPLELAUNCHER_BUGREPORT_DISABLE";

    private static readonly bool BugReportsDisabled =
        string.Equals(Environment.GetEnvironmentVariable(DisableBugReportsEnvironmentVariable), "1",
            StringComparison.Ordinal);

    private static readonly string ApiKey = DecodeApiKey();

    private readonly Channel<LogEvent> _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private readonly CancellationTokenSource _cts = new();
    private readonly string _logFolder;
    private bool _disposed;
    private Task? _processTask;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BugReportApiSink" /> class and starts the background queue processor.
    /// </summary>
    /// <param name="logFolder">The folder where fallback log files will be written.</param>
    public BugReportApiSink(string logFolder)
    {
        _logFolder = logFolder;
        _processTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
    }

    /// <summary>
    ///     Disposes the sink, cancelling the background queue processor.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _channel.Writer.TryComplete();

        try
        {
            // Let the consumer drain the queue (each upload is bounded by its own 30 s
            // timeout) without letting shutdown hang forever.
            _processTask?.Wait(TimeSpan.FromSeconds(35));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException))
        {
            // Cancellation during shutdown is expected.
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Bug report sink shutdown did not complete cleanly");
        }

        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>
    ///     Emits a log event to the bug report queue if its level is Warning or above.
    /// </summary>
    /// <param name="logEvent">The log event to process.</param>
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Warning) return;

        _channel.Writer.TryWrite(logEvent);
    }

    private static string DecodeApiKey()
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(ApiKeyEncoded));
        return Encoding.UTF8.GetString(Convert.FromBase64String(decoded));
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
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Updater bug report submission failed");
                        WriteCriticalError(logEvent);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown via Dispose; the completed channel is the normal exit.
        }
    }

    private async Task SendReportAsync(LogEvent logEvent)
    {
        if (BugReportsDisabled) return;

        var report = BuildReport(logEvent);

        var errorLogPath = Path.Combine(_logFolder, "error.log");
        var userLogPath = Path.Combine(_logFolder, "error_user.log");

        try
        {
            // Persist first; if the folder is unavailable the catch below records the
            // failure instead of letting the processing loop die.
            await File.AppendAllTextAsync(errorLogPath, report);
            await File.AppendAllTextAsync(userLogPath,
                report +
                "--------------------------------------------------------------------------------------------------------------\n\n\n");

            var assembly = Assembly.GetExecutingAssembly();
            var payload = new
            {
                message = TruncateForApi(report, MaxMessageLength),
                applicationName = assembly.GetName().Name ?? "Updater",
                version = assembly.GetName().Version?.ToString() ?? "Unknown",
                userInfo = GetUserInfo(),
                environment = GetEnvironmentName(),
                stackTrace = TruncateForApi(BuildStackTrace(logEvent), MaxStackTraceLength)
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Add("X-API-KEY", ApiKey);
            request.Content = jsonContent;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await MainWindow.HttpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode && File.Exists(errorLogPath))
            {
                try
                {
                    File.Delete(errorLogPath);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Could not delete the submitted updater error log");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Updater bug report submission failed; writing to the critical log");
            WriteCriticalError(logEvent);
        }
    }

    private static string? TruncateForApi(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;

        var keep = Math.Max(0, maxLength - TruncationSuffix.Length);
        return value[..keep] + TruncationSuffix;
    }

    private void WriteCriticalError(LogEvent logEvent)
    {
        try
        {
            var criticalLogPath = Path.Combine(_logFolder, "critical_error.log");
            var report = BuildReport(logEvent) +
                         "\n--------------------------------------------------------------------------------------------------------------\n\n\n";
            File.AppendAllText(criticalLogPath, report);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not write the updater critical bug report fallback log");
        }
    }

    private static string GetOperatingSystemVersion()
    {
        if (OperatingSystem.IsWindows()) return $"Windows {Environment.OSVersion.Version}";
        if (OperatingSystem.IsLinux()) return $"Linux {Environment.OSVersion.Version}";
        if (OperatingSystem.IsMacOS()) return $"macOS {Environment.OSVersion.Version}";
        return RuntimeInformation.OSDescription;
    }

    private static string BuildReport(LogEvent logEvent)
    {
        var message = new StringBuilder();

        message.AppendLine("=== Environment Details ===");
        message.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTime.Now}");
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Application Name: {Assembly.GetExecutingAssembly().GetName().Name ?? "Updater"}");
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Application Version: {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"}");
        message.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {RuntimeInformation.OSDescription}");
        message.AppendLine(CultureInfo.InvariantCulture, $"Architecture: {RuntimeInformation.OSArchitecture}");
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Bitness: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Windows, Linux or MacOsX Version: {GetOperatingSystemVersion()}");
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