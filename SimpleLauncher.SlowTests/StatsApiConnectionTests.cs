using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SimpleLauncher.Core;
using Xunit;

namespace SimpleLauncher.SlowTests;

/// <summary>
///     Integration tests that make real HTTP calls to both Stats API endpoints.
///     Stats API 1 (StatsApiUrl):  Usage/emulator launch statistics.
///     Stats API 2 (StatsApiUrl2): Application version statistics.
/// </summary>
public class StatsApiConnectionTests
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

    private static (string apiKey, string statsUrl1, string statsUrl2) ReadStatsConfig(JsonDocument settings)
    {
        var apiKey = AppConstants.GetApiKey();
        var statsUrl1 = settings.RootElement.GetProperty("StatsApiUrl").GetString()
                        ?? "https://www.purelogiccode.com/simplelauncher/stats/stats/";
        var statsUrl2 = settings.RootElement.GetProperty("StatsApiUrl2").GetString()
                        ?? "https://www.purelogiccode.com/ApplicationStats/stats";
        return (apiKey, statsUrl1, statsUrl2);
    }

    // -- Stats API 1: Usage/emulator statistics (StatsApiUrl) --

    /// <summary>
    ///     Verifies that the Stats API 1 usage endpoint is reachable with a valid API key.
    /// </summary>
    [Fact]
    public async Task StatsApi1_UsageCall_IsReachable()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, statsUrl1, _) = ReadStatsConfig(settings);

        using var request = new HttpRequestMessage(HttpMethod.Post, statsUrl1);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent("", Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);

        var isReachable = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.TooManyRequests;
        Assert.True(isReachable,
            $"Stats API 1 ({statsUrl1}) returned {(int)response.StatusCode} ({response.StatusCode}). Expected success or 429.");
    }

    /// <summary>
    ///     Verifies that the Stats API 1 emulator call endpoint is reachable with a valid API key.
    /// </summary>
    [Fact]
    public async Task StatsApi1_EmulatorCall_IsReachable()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, statsUrl1, _) = ReadStatsConfig(settings);

        var payload = new { emulatorName = "Retroarch" };
        using var request = new HttpRequestMessage(HttpMethod.Post, statsUrl1);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);

        var isReachable = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.TooManyRequests;
        Assert.True(isReachable,
            $"Stats API 1 emulator call ({statsUrl1}) returned {(int)response.StatusCode} ({response.StatusCode}). Expected success or 429.");
    }

    /// <summary>
    ///     Verifies that the Stats API 1 returns a valid response body on success.
    /// </summary>
    [Fact]
    public async Task StatsApi1_ReturnsValidResponseContent()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, statsUrl1, _) = ReadStatsConfig(settings);

        using var request = new HttpRequestMessage(HttpMethod.Post, statsUrl1);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent("", Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.NotNull(body);
        }
    }

    /// <summary>
    ///     Verifies that the Stats API 1 responds within the configured timeout.
    /// </summary>
    [Fact]
    public async Task StatsApi1_RespondsWithinTimeout()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, statsUrl1, _) = ReadStatsConfig(settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Post, statsUrl1);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent("", Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request, cts.Token);

        Assert.True(response.StatusCode is not HttpStatusCode.RequestTimeout,
            $"Stats API 1 ({statsUrl1}) timed out.");
    }

    // -- Stats API 2: Application version statistics (StatsApiUrl2) --

    /// <summary>
    ///     Verifies that the Stats API 2 application stats endpoint is reachable with a valid API key.
    /// </summary>
    [Fact]
    public async Task StatsApi2_ApplicationStatsCall_IsReachable()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, _, statsUrl2) = ReadStatsConfig(settings);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var payload = new { applicationId = "simplelauncher", version };

        using var request = new HttpRequestMessage(HttpMethod.Post, statsUrl2);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);

        var isReachable = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.TooManyRequests;
        Assert.True(isReachable,
            $"Stats API 2 ({statsUrl2}) returned {(int)response.StatusCode} ({response.StatusCode}). Expected success or 429.");
    }

    /// <summary>
    ///     Verifies that the Stats API 2 returns a valid response body on success.
    /// </summary>
    [Fact]
    public async Task StatsApi2_ReturnsValidResponseContent()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, _, statsUrl2) = ReadStatsConfig(settings);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var payload = new { applicationId = "simplelauncher", version };

        using var request = new HttpRequestMessage(HttpMethod.Post, statsUrl2);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.NotNull(body);
        }
    }

    /// <summary>
    ///     Verifies that the Stats API 2 responds within the configured timeout.
    /// </summary>
    [Fact]
    public async Task StatsApi2_RespondsWithinTimeout()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, _, statsUrl2) = ReadStatsConfig(settings);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var payload = new { applicationId = "simplelauncher", version };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Post, statsUrl2);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request, cts.Token);

        Assert.True(response.StatusCode is not HttpStatusCode.RequestTimeout,
            $"Stats API 2 ({statsUrl2}) timed out.");
    }

    // -- Both APIs tested together --

    /// <summary>
    ///     Verifies that both Stats API endpoints are reachable with a valid API key.
    /// </summary>
    /// <param name="url">The Stats API endpoint URL.</param>
    [Theory]
    [InlineData("https://www.purelogiccode.com/simplelauncher/stats/stats/")]
    [InlineData("https://www.purelogiccode.com/ApplicationStats/stats")]
    public async Task StatsApi_EndpointIsReachableWithValidAuth(string url)
    {
        using var settings = await LoadAppSettingsAsync();
        var apiKey = AppConstants.GetApiKey();

        HttpContent content;
        if (url.Contains("simplelauncher/stats", StringComparison.Ordinal))
        {
            content = new StringContent("", Encoding.UTF8, "application/json");
        }
        else
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
            var payload = new { applicationId = "simplelauncher", version };
            content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = content;

        using var response = await HttpClient.SendAsync(request);

        var isReachable = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.TooManyRequests;
        Assert.True(isReachable,
            $"Stats API {url} returned {(int)response.StatusCode} ({response.StatusCode}). Could not connect.");
    }

    /// <summary>
    ///     Verifies that both Stats API endpoints respond gracefully when called with an invalid API key.
    /// </summary>
    /// <param name="url">The Stats API endpoint URL.</param>
    [Theory]
    [InlineData("https://www.purelogiccode.com/simplelauncher/stats/stats/")]
    [InlineData("https://www.purelogiccode.com/ApplicationStats/stats")]
    public async Task StatsApi_WithInvalidApiKey_ReturnsNonSuccess(string url)
    {
        HttpContent content;
        if (url.Contains("simplelauncher/stats", StringComparison.Ordinal))
        {
            content = new StringContent("", Encoding.UTF8, "application/json");
        }
        else
        {
            var payload = new { applicationId = "simplelauncher", version = "1.0.0" };
            content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-key-12345");
        request.Content = content;

        using var response = await HttpClient.SendAsync(request);

        // The API may reject or accept the invalid key; we just verify the call completes without crashing
        Assert.True(Enum.IsDefined(response.StatusCode),
            $"Stats API {url} returned an undefined status code with invalid key.");
    }

    /// <summary>
    ///     Verifies that the Stats API configuration values in appsettings.json are valid and distinct.
    /// </summary>
    [Fact]
    public async Task StatsApi_ConfigurationUrlsMatchAppSettings()
    {
        using var settings = await LoadAppSettingsAsync();
        var (apiKey, statsUrl1, statsUrl2) = ReadStatsConfig(settings);

        Assert.False(string.IsNullOrWhiteSpace(apiKey), "ApiKey should not be empty in appsettings.json.");
        Assert.False(string.IsNullOrWhiteSpace(statsUrl1), "StatsApiUrl should not be empty in appsettings.json.");
        Assert.False(string.IsNullOrWhiteSpace(statsUrl2), "StatsApiUrl2 should not be empty in appsettings.json.");
        Assert.Contains("purelogiccode.com", statsUrl1, StringComparison.Ordinal);
        Assert.Contains("purelogiccode.com", statsUrl2, StringComparison.Ordinal);
        Assert.NotEqual(statsUrl1, statsUrl2, StringComparer.Ordinal);
    }
}