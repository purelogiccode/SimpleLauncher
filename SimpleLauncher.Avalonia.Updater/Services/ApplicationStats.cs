using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SimpleLauncher.Avalonia.Updater.Services;

/// <summary>
///     Service for reporting application usage statistics to the ApplicationStats API
/// </summary>
internal static class ApplicationStats
{
    private const string StatsApiUrl = "https://www.purelogiccode.com/ApplicationStats/stats";

    private const string ApiKeyEncoded =
        "YUdwb04zbDFOblExTm5SNWNqVTBNRzg1ZFRnM05qYzJOelp5TlRZM05EVXpORFExTXpJek5USTJOR00zTldJMmREZG5aMmRvWjJjM05uUnlaalUyTkdVPQ==";

    // HTTP 418 ("I'm a teapot"), returned by some hosting/WAF layers when a request is blocked.
    // The value has no HttpStatusCode member.
    private const HttpStatusCode BlockedByHosting = (HttpStatusCode)418;

    private static readonly string ApiKey = DecodeApiKey();

    private static string DecodeApiKey()
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(ApiKeyEncoded));
        return Encoding.UTF8.GetString(Convert.FromBase64String(decoded));
    }

    /// <summary>
    ///     Sends application launch statistics to the ApplicationStats API.
    ///     This is a fire-and-forget operation that will not block or throw.
    /// </summary>
    public static void SendLaunchStats()
    {
        _ = Task.Run(static async () => await SendLaunchStatsAsync());
    }

    /// <summary>
    ///     Sends application launch statistics to the ApplicationStats API asynchronously.
    /// </summary>
    private static async Task SendLaunchStatsAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "0.0.0";

            var httpClient = MainWindow.HttpClient;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            var payload = new { applicationId = "simplelauncher-updater", version };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var response = await httpClient.PostAsync(StatsApiUrl, content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.TooManyRequests or BlockedByHosting)
                {
                    // Expected condition (rate limit or hosting/WAF block): not a bug, keep it
                    // out of the bug report service.
                    Log.Information("ApplicationStats API returned non-success status: {StatusCode}",
                        response.StatusCode);
                }
                else
                {
                    Log.Warning("ApplicationStats API returned non-success status: {StatusCode}", response.StatusCode);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("ApplicationStats API call timed out.");
        }
        catch (Exception ex)
        {
            // DNS/connection failures are expected network conditions (offline, DNS down,
            // TLS errors) — log at Information, not as a bug.
            if (ex is HttpRequestException or SocketException)
                Log.Information(ex, "ApplicationStats API call failed");
            else
                Log.Warning(ex, "ApplicationStats API call failed");
        }
    }
}