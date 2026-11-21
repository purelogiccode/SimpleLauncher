using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Moq;
using Serilog.Events;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.DebugAndBugReport;
using Xunit;

namespace SimpleLauncher.Tests;

public class BugReportApiSinkTests
{
    /// <summary>
    ///     Verifies that the test-environment disable switch prevents the sink from creating an
    ///     HTTP client or writing report files, even when a warning/error is emitted.
    /// </summary>
    [Fact]
    public async Task SinkDoesNotSubmitReportsWhenDisabled()
    {
        AppConstants.InitializeApiKey(EncodeApiKey("test-key"));

        var handler = new CountingHandler();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, false));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LogPath"] = "error_user.log",
                ["LogPathForAdmin"] = "error.log",
                ["LogPathCritical"] = "critical_error.log"
            })
            .Build();

        var logFolder = Path.Combine(Path.GetTempPath(), $"sl-bugreport-sink-{Guid.NewGuid():N}");
        Directory.CreateDirectory(logFolder);
        try
        {
            using var sink = new BugReportApiSink();
            sink.Initialize(httpClientFactory.Object, configuration, Mock.Of<IDeleteFilesService>(), logFolder);

            var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Error,
                new InvalidOperationException("Test exception"),
                new MessageTemplate("Test message", []), []);
            sink.Emit(logEvent);

            await Task.Delay(500);

            Assert.Equal(0, handler.RequestCount);
            Assert.Empty(Directory.GetFiles(logFolder));
            httpClientFactory.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            Directory.Delete(logFolder, true);
        }
    }

    /// <summary>
    ///     Verifies that disposing the sink does not block when the sink was initialized on a
    ///     thread whose synchronization context never pumps (the WPF/Avalonia UI thread during
    ///     shutdown). The consumer must run on the thread pool; otherwise Dispose waits for a
    ///     continuation that can only run on the blocked UI thread and stalls shutdown for 35 s.
    /// </summary>
    [Fact]
    public void DisposeDoesNotBlockWhenInitializedOnANonPumpingSynchronizationContext()
    {
        var previousContext = SynchronizationContext.Current;
        var watch = Stopwatch.StartNew();

        var logFolder = Path.Combine(Path.GetTempPath(), $"sl-bugreport-sink-{Guid.NewGuid():N}");
        Directory.CreateDirectory(logFolder);
        try
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LogPath"] = "error_user.log",
                    ["LogPathForAdmin"] = "error.log",
                    ["LogPathCritical"] = "critical_error.log"
                })
                .Build();

            var sink = new BugReportApiSink();
            sink.Initialize(Mock.Of<IHttpClientFactory>(), configuration, Mock.Of<IDeleteFilesService>(),
                logFolder);

            sink.Dispose();
            watch.Stop();

            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5),
                $"Dispose blocked for {watch.Elapsed}; the consumer must not continue on the captured synchronization context.");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            Directory.Delete(logFolder, true);
        }
    }

    /// <summary>
    ///     The bug-report API contract requires a fixed environment block. This test pins
    ///     every required label so a future refactor cannot silently drop one.
    /// </summary>
    [Fact]
    public void BuildReportContainsEveryRequiredEnvironmentField()
    {
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Error,
            new InvalidOperationException("Test exception"),
            new Serilog.Parsing.MessageTemplateParser().Parse("Test message"), []);

        var report = BugReportApiSink.BuildReport(logEvent);

        Assert.Contains("=== Environment Details ===", report, StringComparison.Ordinal);
        Assert.Contains("Date: ", report, StringComparison.Ordinal);
        Assert.Contains("Application Name: ", report, StringComparison.Ordinal);
        Assert.Contains("Application Version: ", report, StringComparison.Ordinal);
        Assert.Contains("OS Version: ", report, StringComparison.Ordinal);
        Assert.Contains("Architecture: ", report, StringComparison.Ordinal);
        Assert.Contains("Bitness: ", report, StringComparison.Ordinal);
        Assert.Contains("Windows, Linux or MacOsX Version: ", report, StringComparison.Ordinal);
        Assert.Contains("Processor Count: ", report, StringComparison.Ordinal);
        Assert.Contains("Base Directory: ", report, StringComparison.Ordinal);
        Assert.Contains("Temp Path: ", report, StringComparison.Ordinal);
        Assert.Contains("=== Error Details ===", report, StringComparison.Ordinal);
        Assert.Contains("Error message: Test message", report, StringComparison.Ordinal);
        Assert.Contains("=== Exception Details ===", report, StringComparison.Ordinal);
        Assert.Contains("Type: System.InvalidOperationException", report, StringComparison.Ordinal);
        Assert.Contains("Message: Test exception", report, StringComparison.Ordinal);
        Assert.Contains("Source: ", report, StringComparison.Ordinal);
        Assert.Contains("StackTrace: ", report, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Inner exceptions are part of the exception details and must be reported, not
    ///     silently dropped after the first level.
    /// </summary>
    [Fact]
    public void BuildReportIncludesInnerExceptionChain()
    {
        var exception = new InvalidOperationException("Outer",
            new InvalidOperationException("Middle", new FormatException("Inner")));
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Error, exception,
            new MessageTemplate("Nested failure", []), []);

        var report = BugReportApiSink.BuildReport(logEvent);

        Assert.Contains("Message: Outer", report, StringComparison.Ordinal);
        Assert.Contains("Message: Middle", report, StringComparison.Ordinal);
        Assert.Contains("Message: Inner", report, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(report, "--- Inner Exception ---"));
    }

    /// <summary>
    ///     The API silently truncates long fields; the client must truncate first and leave a
    ///     marker so reports remain diagnosable.
    /// </summary>
    [Fact]
    public void TruncateForApiKeepsPayloadsWithinTheApiLimits()
    {
        var value = new string('x', BugReportApiSink.MaxMessageLength + 500);

        var truncated = BugReportApiSink.TruncateForApi(value, BugReportApiSink.MaxMessageLength);

        Assert.NotNull(truncated);
        Assert.True(truncated.Length <= BugReportApiSink.MaxMessageLength);
        Assert.Contains("report truncated", truncated, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Short values and null values must pass through unchanged.
    /// </summary>
    [Fact]
    public void TruncateForApiLeavesShortAndNullValuesUntouched()
    {
        Assert.Equal("short", BugReportApiSink.TruncateForApi("short", 10));
        Assert.Null(BugReportApiSink.TruncateForApi(null, 10));
        Assert.Equal(string.Empty, BugReportApiSink.TruncateForApi(string.Empty, 10));
    }

    /// <summary>
    ///     The OS description must be populated on every supported platform; it is the field
    ///     that distinguishes Windows, Linux and macOS reports.
    /// </summary>
    [Fact]
    public void OperatingSystemDetailsAreNeverBlank()
    {
        var description = OperatingSystemDetails.GetDescription();

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.DoesNotContain('\n', description);
        Assert.DoesNotContain('\r', description);
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string EncodeApiKey(string apiKey)
    {
        var encodedOnce = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(encodedOnce));
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Deliberately never invokes the callback: models a UI thread that is blocked
            // in Dispose and cannot process queued continuations.
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}