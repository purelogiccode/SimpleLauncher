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