using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Serilog.Core;
using Serilog.Events;

namespace SimpleLauncher.ResourceTranslator.Services.DebugAndBugReport;

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
    private readonly Task _processTask;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BugReportApiSink" /> class.
    /// </summary>
    /// <param name="logFolder">The folder path where log files are stored.</param>
    public BugReportApiSink(string logFolder)
    {
        _logFolder = logFolder;
        _processTask = ProcessQueueAsync(_cts.Token);
    }

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
                applicationName = assembly.GetName().Name ?? "ResourceTranslator",
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
            var report = BuildReport(logEvent) +
                         "\n---\n\n\n";
            File.AppendAllText(criticalLogPath, report);
        }
        catch
        {
            /* Can't do anything more */
        }
    }

    private static string BuildReport(LogEvent logEvent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Environment Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTime.Now}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Application: {Assembly.GetExecutingAssembly().GetName().Name ?? "ResourceTranslator"}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Version: {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine();
        sb.AppendLine("=== Error Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Level: {logEvent.Level}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {logEvent.RenderMessage()}");
        if (logEvent.Exception != null) sb.AppendLine(CultureInfo.InvariantCulture, $"Exception: {logEvent.Exception}");

        sb.AppendLine();
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