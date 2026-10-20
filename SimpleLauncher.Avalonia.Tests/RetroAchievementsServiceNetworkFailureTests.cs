using System.Net;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Configuration;
using Moq;
using Serilog.Core;
using Serilog.Events;
using SimpleLauncher.Avalonia.Services.RetroAchievements;
using SimpleLauncher.Core.Services.RetroAchievements;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression tests for bugs #68173/#68172 and #68823-68828: network failures,
///     timeouts and non-JSON API payloads (Cloudflare challenge / maintenance pages)
///     used to be logged at Error, so every affected user sent a bug report to the live
///     API. These are expected external conditions and must be logged at Information
///     only — never Warning/Error — so the bug report service stays silent.
/// </summary>
public class RetroAchievementsServiceNetworkFailureTests
{
    [Fact]
    public async Task GetUserProfileAsync_SslFailure_IsLoggedAtInformation_NotAsBug()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var service = CreateService(new SslFailureHandler(), logger);

        var result = await service.GetUserProfileAsync("gosseyn", "api-key");

        Assert.Null(result);
        Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
        Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
    }

    [Fact]
    public async Task GetUserProfileAsync_Timeout_IsLoggedAtInformation_NotAsBug()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var service = CreateService(new TimeoutHandler(), logger);

        var result = await service.GetUserProfileAsync("gosseyn", "api-key");

        Assert.Null(result);
        Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
        Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
    }

    [Fact]
    public async Task GetUserProfileAsync_HtmlResponse_IsLoggedAtInformation_NotAsBug()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var service = CreateService(new HtmlResponseHandler(), logger);

        var result = await service.GetUserProfileAsync("rcroadster", "api-key");

        Assert.Null(result);
        Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
        Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
    }

    [Fact]
    public async Task GetUserRecentlyPlayedGamesAsync_HtmlResponse_IsLoggedAtInformation_NotAsBug()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var service = CreateService(new HtmlResponseHandler(), logger);

        var result = await service.GetUserRecentlyPlayedGamesAsync("rcroadster", "api-key");

        Assert.Null(result);
        Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
        Assert.Contains(sink.Events, static e => e.Level == LogEventLevel.Information);
    }

    private static RetroAchievementsService CreateService(HttpMessageHandler handler, ILogger logger)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Urls:RetroAchievementsApi"] = "https://retroachievements.org/API/"
            })
            .Build();

        return new RetroAchievementsService(httpClientFactory.Object, new RetroAchievementsManager(), configuration,
            logger);
    }

    /// <summary>
    ///     Simulates the SSL failure from bugs #68173/#68172: an HttpRequestException
    ///     wrapping an AuthenticationException ("The SSL connection could not be established").
    /// </summary>
    private sealed class SslFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var inner = new AuthenticationException("The remote certificate is invalid.");
            throw new HttpRequestException("The SSL connection could not be established, see inner exception.",
                inner);
        }
    }

    /// <summary>
    ///     Simulates an RA API timeout, which surfaces as a TaskCanceledException in the caller.
    /// </summary>
    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("The request was canceled due to the configured timeout.");
        }
    }

    /// <summary>
    ///     Simulates the HTML page Cloudflare/RetroAchievements serves with HTTP 200 during a
    ///     challenge, rate limit or maintenance window (bugs #68823-68828).
    /// </summary>
    private sealed class HtmlResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const string html =
                "<!DOCTYPE html><html><head><title>Just a moment...</title></head><body>Checking your browser</body></html>";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
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