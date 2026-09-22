using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Moq;
using Serilog.Core;
using Serilog.Events;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Services.UsageStats;
using SimpleLauncher.Services.UsageStats;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     This collection must never run in parallel with other test classes: its tests
///     mutate the static <see cref="App.ServiceProvider" /> (installed via reflection),
///     which leaks across test classes while the static lives.
/// </summary>
[CollectionDefinition(nameof(NetworkFailureLogging), DisableParallelization = true)]
public sealed class NetworkFailureLogging;

/// <summary>
///     Regression tests for bug #67150: a DNS failure ("Hôte inconnu") during a stats API
///     call used to be logged at Error, so every offline launch sent a bug report to the
///     live API. Network failures are expected external conditions and must be logged at
///     Information only — never Warning/Error — so the bug report service stays silent.
/// </summary>
[Collection(nameof(NetworkFailureLogging))]
public class ApplicationStatsNetworkFailureTests
{
    [Fact]
    public async Task ApplicationStats_DnsFailure_IsLoggedAtInformation_NotAsBug()
    {
        AppConstants.InitializeApiKey(EncodeApiKey("test-key"));

        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new DnsFailureHandler()));

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(provider => provider.GetService(typeof(IHttpClientFactory)))
            .Returns(httpClientFactory.Object);
        serviceProvider.Setup(provider => provider.GetService(typeof(ILogger))).Returns(logger);
        SetAppServiceProvider(serviceProvider.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatsApiUrl2"] = "https://www.purelogiccode.com/ApplicationStats/stats"
            })
            .Build();

        try
        {
            await ApplicationStats.CallApplicationStatsAsync(configuration, logger);

            Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
            Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
        }
        finally
        {
            SetAppServiceProvider(null!);
        }
    }

    [Fact]
    public async Task CoreStats_DnsFailure_IsLoggedAtInformation_NotAsBug()
    {
        AppConstants.InitializeApiKey(EncodeApiKey("test-key"));

        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new DnsFailureHandler()));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatsApiUrl"] = "https://www.purelogiccode.com/simplelauncher/stats/stats/"
            })
            .Build();

        var stats = new Stats(httpClientFactory.Object, configuration, logger);
        await stats.CallApiAsync();

        Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
        Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
    }

    /// <summary>
    ///     Regression test for bug #67164: a 20-second stats API timeout used to be logged at
    ///     Warning in 5.6.1.0, so a slow connection reported a bug on every emulator launch.
    ///     Timeouts are expected conditions and must be logged at Information only.
    /// </summary>
    [Fact]
    public async Task CoreStats_Timeout_IsLoggedAtInformation_NotAsBug()
    {
        AppConstants.InitializeApiKey(EncodeApiKey("test-key"));

        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new TimeoutHandler()));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatsApiUrl"] = "https://www.purelogiccode.com/simplelauncher/stats/stats/"
            })
            .Build();

        var stats = new Stats(httpClientFactory.Object, configuration, logger);
        await stats.CallApiAsync();

        Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
        Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
    }

    private static void SetAppServiceProvider(IServiceProvider? provider)
    {
        var property = typeof(App).GetProperty(nameof(App.ServiceProvider), BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(property);
        var setter = property.GetSetMethod(true);
        Assert.NotNull(setter);
        setter.Invoke(null, [provider]);
    }

    private static string EncodeApiKey(string apiKey)
    {
        var encodedOnce = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(encodedOnce));
    }

    /// <summary>
    ///     Simulates the "Hôte inconnu" DNS failure from bug #67150: an HttpRequestException
    ///     wrapping a SocketException (WSAHOST_NOT_FOUND).
    /// </summary>
    private sealed class DnsFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var inner = new SocketException(11001, "Hôte inconnu");
            throw new HttpRequestException("Hôte inconnu. (www.purelogiccode.com:443)", inner);
        }
    }

    /// <summary>
    ///     Simulates a stats API timeout: the request is cancelled after 20 seconds, which
    ///     surfaces as an OperationCanceledException (TaskCanceledException) in the caller.
    /// </summary>
    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>Serilog sink that records every emitted event for level assertions.</summary>
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            lock (Events)
            {
                Events.Add(logEvent);
            }
        }
    }
}