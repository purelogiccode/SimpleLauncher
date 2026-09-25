using System.Reflection;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Moq;
using Serilog.Core;
using Serilog.Events;
using SimpleLauncher.Core.Services.EasyMode;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the <see cref="EasyModeManager" /> configuration chain
///     (local XML → API → fallback URL), including the in-memory fallback parse
///     that must survive a read-only application directory.
/// </summary>
public class EasyModeManagerFallbackTests : IDisposable
{
    // All candidate local XML file names; GetLocalXmlFileName() depends on the
    // OS + architecture of the machine running the tests.
    private static readonly string[] LocalXmlFileNames =
    [
        "easymode.xml",
        "easymode_arm64.xml",
        "easymode_linux_x64.xml",
        "easymode_linux_arm64.xml",
        "easymode_macos_x64.xml",
        "easymode_macos_arm64.xml"
    ];

    private const string ValidXml = """
                                    <?xml version="1.0" encoding="utf-8"?>
                                    <EasyMode>
                                      <EasyModeSystemConfig>
                                        <SystemName>Test System</SystemName>
                                        <SystemFolder>roms/test</SystemFolder>
                                        <SystemImageFolder>images/test</SystemImageFolder>
                                        <FileFormatsToSearch>
                                          <FormatToSearch>.zip</FormatToSearch>
                                        </FileFormatsToSearch>
                                        <FileFormatsToLaunch>
                                          <FormatToLaunch>.zip</FormatToLaunch>
                                        </FileFormatsToLaunch>
                                        <Emulators>
                                          <Emulator>
                                            <EmulatorName>TestEmulator</EmulatorName>
                                            <EmulatorDownloadLink>https://example.test/emu.zip</EmulatorDownloadLink>
                                          </Emulator>
                                        </Emulators>
                                      </EasyModeSystemConfig>
                                    </EasyMode>
                                    """;

    private readonly CaptureSink _sink = new();

    public EasyModeManagerFallbackTests()
    {
        ResetSessionCache();
        DeleteLocalXmlFiles();
    }

    public void Dispose()
    {
        DeleteLocalXmlFiles();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------
    // Fallback URL chain
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ApiFails_FallbackXmlIsParsedInMemoryAndPersisted()
    {
        var manager = CreateManager(m => m.RequestUri != null &&
            m.RequestUri.AbsoluteUri.Contains("api/Systems", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

        var result = await manager.LoadAsync();

        Assert.NotNull(result);
        var system = Assert.Single(result.Systems);
        Assert.Equal("Test System", system.SystemName);
        Assert.Equal("TestEmulator", system.Emulators.Emulator.EmulatorName);

        // The downloaded XML is still cached next to the app for the next run.
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, GetLocalXmlFileName())));
    }

    [Fact]
    public async Task LoadAsync_FallbackXmlInvalid_ReturnsNullWithoutThrowing()
    {
        var manager = CreateManager(request => request.RequestUri != null &&
                request.RequestUri.AbsoluteUri.Contains("api/Systems", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<NotEasyMode />", Encoding.UTF8, "application/xml")
            });

        var result = await manager.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_ApiReturnsEmptyList_LogsInformationNotWarning()
    {
        // An empty API response is an expected data condition (architecture not
        // configured server-side yet); per repo policy it must never reach the
        // Warning+ bug-report sink.
        var manager = CreateManager(request => request.RequestUri != null &&
                request.RequestUri.AbsoluteUri.Contains("api/Systems", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await manager.LoadAsync();

        Assert.Null(result);
        Assert.Contains(_sink.Events,
            e => e.Level == LogEventLevel.Information &&
                 e.RenderMessage().Contains("returned no systems", StringComparison.Ordinal));
        Assert.DoesNotContain(_sink.Events, e => e.Level >= LogEventLevel.Warning);
    }

    // ------------------------------------------------------------------
    // Local XML path (now routed through the same hardened parser)
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_LocalXmlPresent_ParsesWithoutNetworkAccess()
    {
        var localXmlPath = Path.Combine(AppContext.BaseDirectory, GetLocalXmlFileName());
        await File.WriteAllTextAsync(localXmlPath, ValidXml);

        // Any HTTP access would return a 500, so a successful local load proves
        // the local path never touched the network.
        var manager = CreateManager(_ => new HttpResponseMessage(
            HttpStatusCode.InternalServerError));

        var result = await manager.LoadAsync();

        Assert.NotNull(result);
        Assert.Equal("Test System", Assert.Single(result.Systems).SystemName);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private EasyModeManager CreateManager(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new RoutingHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                // Set every platform key so the test is independent of the OS/arch it runs on.
                ["Urls:EasyModeFallbackXmlX64"] = "https://fallback.test/easymode.xml",
                ["Urls:EasyModeFallbackXmlArm64"] = "https://fallback.test/easymode_arm64.xml",
                ["Urls:EasyModeFallbackXmlLinuxX64"] = "https://fallback.test/easymode_linux_x64.xml",
                ["Urls:EasyModeFallbackXmlLinuxArm64"] = "https://fallback.test/easymode_linux_arm64.xml",
                ["Urls:EasyModeFallbackXmlMacosX64"] = "https://fallback.test/easymode_macos_x64.xml",
                ["Urls:EasyModeFallbackXmlMacosArm64"] = "https://fallback.test/easymode_macos_arm64.xml",
                ["EasyModeCacheDurationMinutes"] = "60"
            }).Build();

        // A real Serilog pipeline so convenience methods (Debug/Information/...) emit
        // LogEvents through the sink instead of being dropped (a bare custom ILogger
        // implementation has Serilog's default BindMessageTemplate return false and
        // silently discards everything).
        var serilogLogger = new LoggerConfiguration().WriteTo.Sink(_sink).CreateLogger();

        return new EasyModeManager(serilogLogger, config, httpFactoryMock.Object, serilogLogger);
    }

    private static string GetLocalXmlFileName()
    {
        var method = typeof(EasyModeManager).GetMethod("GetLocalXmlFileName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, null));
    }

    private static void ResetSessionCache()
    {
        var field = typeof(EasyModeManager).GetField("_apiCache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        (EasyModeManager?, DateTime) emptyCache = (null, default);
        field.SetValue(null, emptyCache);
    }

    private static void DeleteLocalXmlFiles()
    {
        foreach (var fileName in LocalXmlFileNames)
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) :
        HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    /// <summary>Serilog sink that records every emitted event for level assertions.</summary>
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
