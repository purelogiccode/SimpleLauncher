using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SimpleLauncher.Core;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests connectivity to the bug report and statistics APIs used by SimpleLauncher.
/// </summary>
public class ApiConnectivityTests
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "SimpleLauncherTests/1.0" } }
    };

    private static readonly MockBugReportHandler MockHandler = new();

    private static readonly HttpClient MockHttpClient = new(MockHandler)
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static string GetProjectFilePath(string relativePath)
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation == null)
            throw new InvalidOperationException("Could not determine executing assembly location.");

        var path = Path.Combine(assemblyLocation, "..", "..", "..", "..", relativePath);
        return Path.GetFullPath(path);
    }

    private static async Task<JsonDocument> LoadAppSettingsAsync()
    {
        var settingsPath = GetProjectFilePath(Path.Combine("SimpleLauncher", "appsettings.json"));
        Assert.True(File.Exists(settingsPath), $"appsettings.json not found at {settingsPath}");
        var json = await File.ReadAllTextAsync(settingsPath);
        var settings = JsonDocument.Parse(json);

        // Mirror the app launch: decrypt the API key from appsettings.json into AppConstants.
        AppConstants.InitializeApiKey(settings.RootElement.TryGetProperty("ApiKey", out var apiKey)
            ? apiKey.GetString()
            : null);

        return settings;
    }

    /// <summary>
    ///     Verifies that the bug report request is built correctly without contacting the real API.
    ///     The request targets a reserved, non-resolvable host so this test can never reach the
    ///     production bug report endpoint even if the mock handler is bypassed.
    /// </summary>
    [Fact]
    public async Task BugReportApiCanSendReport()
    {
        using var settings = await LoadAppSettingsAsync();
        var apiKey = AppConstants.GetApiKey();
        var apiUrl = "https://bugreport.invalid/api/send-bug-report";

        var payload = new
        {
            message = "Test bug report from SimpleLauncher.Tests",
            applicationName = "SimpleLauncher.Tests",
            version = "1.0.0",
            userInfo = "TestRunner",
            environment = "Test",
            stackTrace = "Test stack trace"
        };

        // Use a mock HTTP handler so the test validates the request shape without
        // sending a real bug report to the production API.
        MockHandler.Reset();

        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("X-API-KEY", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await MockHttpClient.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Mock bug report API returned {(int)response.StatusCode} ({response.StatusCode}). Expected a success status code.");

        Assert.NotNull(MockHandler.LastRequest);
        Assert.Equal(HttpMethod.Post, MockHandler.LastRequest.Method);
        Assert.Equal(apiUrl, MockHandler.LastRequest.RequestUri?.AbsoluteUri);
        Assert.Contains(MockHandler.LastRequest.Headers.GetValues("X-API-KEY"),
            value => string.Equals(value, apiKey, StringComparison.Ordinal));

        var body = await MockHandler.LastRequest.Content!.ReadAsStringAsync();
        using var bodyJson = JsonDocument.Parse(body);
        Assert.Equal("Test bug report from SimpleLauncher.Tests",
            bodyJson.RootElement.GetProperty("message").GetString());
        Assert.Equal("SimpleLauncher.Tests", bodyJson.RootElement.GetProperty("applicationName").GetString());
        Assert.Equal("Test stack trace", bodyJson.RootElement.GetProperty("stackTrace").GetString());
    }

    /// <summary>
    ///     Verifies that each statistics API endpoint is reachable with valid authentication.
    /// </summary>
    [Theory]
    [InlineData("https://www.purelogiccode.com/simplelauncher/stats/stats/")]
    [InlineData("https://www.purelogiccode.com/ApplicationStats/stats")]
    public async Task StatsApiIsReachable(string url)
    {
        using var settings = await LoadAppSettingsAsync();
        var apiKey = AppConstants.GetApiKey();

        HttpContent content;
        if (url.Contains("simplelauncher/stats", StringComparison.Ordinal))
        {
            // Usage stats call (empty body, matching Stats.CallApiAsync behavior)
            content = new StringContent("", Encoding.UTF8, "application/json");
        }
        else
        {
            // ApplicationStats call
            var payload = new { applicationId = "simplelauncher", version = "1.0.0" };
            content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = content;

        using var response = await HttpClient.SendAsync(request);

        var isReachable = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.TooManyRequests;
        Assert.True(
            isReachable,
            $"Stats API {url} returned {(int)response.StatusCode} ({response.StatusCode}). Could not connect to the stats API.");
    }

    /// <summary>
    ///     Captures the outgoing request and returns a canned success response without
    ///     contacting the real bug report API.
    /// </summary>
    private sealed class MockBugReportHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public void Reset()
        {
            LastRequest = null;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"message":"Bug report received."}""", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}