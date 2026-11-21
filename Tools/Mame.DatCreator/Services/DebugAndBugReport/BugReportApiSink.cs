using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Serilog.Core;
using Serilog.Events;

namespace Mame.DatCreator.Services.DebugAndBugReport;

/// <summary>
///     A Serilog log event sink that sends bug reports to a remote API.
/// </summary>
/// <summary>
///     A Serilog log event sink that sends bug reports to a remote API.
/// </summary>
public class BugReportApiSink : ILogEventSink, IDisposable
{
    private const string ApiUrl = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    private const string ApiKeyEncoded =
        "YUdwb04zbDFOblExTm5SNWNqVTBNRzg1ZFRnM05qYzJOelp5TlRZM05EVXpORFExTXpJek5USTJOR00zTldJMmREZG5aMmRvWjJjM05uUnlaalUyTkdVPQ==";

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
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _logFolder;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BugReportApiSink" /> class.
    /// </summary>
    /// <param name="logFolder">The folder path where log files are stored.</param>
    public BugReportApiSink(string logFolder)
    {
        _logFolder = logFolder;
        _ = ProcessQueueAsync(_cts.Token);
    }

    /// <summary>
    ///     Disposes of resources used by this sink.
    /// </summary>
    /// <summary>
    ///     Disposes of resources used by this sink.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _httpClient.Dispose();
    }

    /// <summary>
    ///     Emits a log event by queuing it for bug report submission.
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

    private async Task SendReportAsync(LogEvent logEvent)
    {
        if (BugReportsDisabled) return;

        var report = BuildReport(logEvent);

        var errorLogPath = Path.Combine(_logFolder, "error.log");
        await File.AppendAllTextAsync(errorLogPath, report);

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var payload = new
            {
                message = report,
                applicationName = assembly.GetName().Name ?? "Mame.DatCreator",
                version = assembly.GetName().Version?.ToString() ?? "Unknown",
                userInfo = GetUserInfo(),
                environment = GetEnvironmentName(),
                stackTrace = logEvent.Exception?.StackTrace
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Add("X-API-KEY", ApiKey);
            request.Content = jsonContent;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode && File.Exists(errorLogPath))
            {
                try
                {
                    File.Delete(errorLogPath);
                }
                catch
                {
                    /* Ignore */
                }
            }
        }
        catch
        {
            WriteCriticalError(logEvent);
        }
    }

    private void WriteCriticalError(LogEvent logEvent)
    {
        try
        {
            var criticalLogPath = Path.Combine(_logFolder, "critical_error.log");
            var report = BuildReport(logEvent) + "\n---\n\n\n";
            File.AppendAllText(criticalLogPath, report);
        }
        catch
        {
            /* Can't do anything more */
        }
    }

    private static string BuildReport(LogEvent logEvent)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var sb = new StringBuilder();

        sb.AppendLine("=== Environment Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Application Name: {assembly.GetName().Name ?? "SimpleLauncher"}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Application Version: {assembly.GetName().Version?.ToString() ?? "Unknown"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {RuntimeInformation.OSDescription}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Bitness: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Windows, Linux or MacOsX Version: {GetOperatingSystemVersion()}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Processor Count: {Environment.ProcessorCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Base Directory: {AppContext.BaseDirectory}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Temp Path: {Path.GetTempPath()}");
        sb.AppendLine();

        sb.AppendLine("=== Error Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Log Level: {logEvent.Level}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Error message: {logEvent.RenderMessage()}");
        sb.AppendLine();

        sb.AppendLine("=== Exception Details ===");
        if (logEvent.Exception == null)
        {
            sb.AppendLine("Type: None");
            sb.AppendLine("Message: None");
            sb.AppendLine("Source: None");
            sb.AppendLine("StackTrace: None");
        }
        else
        {
            AppendException(sb, logEvent.Exception);
            var inner = logEvent.Exception.InnerException;
            while (inner != null)
            {
                sb.AppendLine();
                sb.AppendLine("--- Inner Exception ---");
                AppendException(sb, inner);
                inner = inner.InnerException;
            }
        }

        return sb.ToString();
    }

    private static void AppendException(StringBuilder sb, Exception exception)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"Type: {exception.GetType().FullName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {exception.Message}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Source: {exception.Source}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"StackTrace: {exception.StackTrace}");
    }

    private static string GetOperatingSystemVersion()
    {
        if (OperatingSystem.IsWindows()) return $"Windows {Environment.OSVersion.Version}";
        if (OperatingSystem.IsLinux()) return $"Linux {Environment.OSVersion.Version}";
        if (OperatingSystem.IsMacOS()) return $"macOS {Environment.OSVersion.Version}";
        return RuntimeInformation.OSDescription;
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