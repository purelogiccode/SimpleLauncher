using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Avalonia.Services.UsageStats;

/// <summary>
///     Sends anonymous application usage statistics to the remote stats API.
///     Avalonia port of the WPF ApplicationStats static class.
/// </summary>
public static class ApplicationStats
{
    /// <summary>Asynchronously sends application version statistics to the remote API.</summary>
    public static async Task CallApplicationStatsAsync(IConfiguration configuration, ILogger logErrors)
    {
        try
        {
            var apiKey = AppConstants.GetApiKey();
            var statsUrl = configuration.GetValue<string>("StatsApiUrl2") ??
                           "https://www.purelogiccode.com/ApplicationStats/stats";
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

            var httpClientFactory = App.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            using var client = httpClientFactory.CreateClient("StatsClient");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new { applicationId = "simplelauncher", version };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Use a CancellationToken with a 20-second timeout to prevent indefinite hangs
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            using var response = await client.PostAsync(statsUrl, content, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                if (ExpectedHttpStatus.IsExpectedExternalCondition(response.StatusCode))
                {
                    // Expected condition (rate limit or hosting/WAF block): not a bug, keep it
                    // out of the bug report service.
                    Log.Information("ApplicationStats API returned: {StatusCode} (expected external condition)",
                        response.StatusCode);
                }
                else
                {
                    var ex = new HttpRequestException($"ApplicationStats API returned: {response.StatusCode}");
                    logErrors.Error(ex, "ApplicationStats API returned: {StatusCode}", response.StatusCode);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug("ApplicationStats API call timed out.");
        }
        catch (Exception ex)
        {
            Log.Debug("ApplicationStats API call failed: {Message}", ex.Message);

            // DNS/connection failures are expected network conditions (offline, DNS down,
            // TLS errors) — log at Information, not as a bug. Only unexpected exceptions
            // are reported.
            if (ex is HttpRequestException or SocketException)
                Log.Information(ex, "ApplicationStats API call failed: {Message}", ex.Message);
            else
                logErrors.Error(ex, "ApplicationStats API call failed: {Message}", ex.Message);
        }
    }
}