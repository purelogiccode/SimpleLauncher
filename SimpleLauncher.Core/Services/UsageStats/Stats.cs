using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SimpleLauncher.Core.Services.UsageStats;

/// <summary>
///     Manages sending usage and emulator launch statistics to the remote stats API with configurable endpoints and
///     authentication.
/// </summary>
public class Stats
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private string _apiKey = "";
    private bool _isApiEnabled;
    private string _statsApiUrl = "";

    /// <summary>
    ///     Initializes a new instance of the Stats class, loading API configuration.
    /// </summary>
    public Stats(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        LoadConfiguration(configuration);
    }

    private void LoadConfiguration(IConfiguration configuration)
    {
        try
        {
            // Read ApiKey
            _apiKey = AppConstants.GetApiKey();

            if (string.IsNullOrEmpty(_apiKey)) // ApiKey is missing or empty, disable API and log error locally
            {
                _isApiEnabled = false;

                // Notify developer
                _logger.Error(new InvalidOperationException("API Key is missing or empty in the configuration file."),
                    "Stats API Key missing");

                return;
            }

            _statsApiUrl = configuration.GetValue<string>("StatsApiUrl") ??
                           "https://www.purelogiccode.com/simplelauncher/stats/stats/";

            if (string.IsNullOrEmpty(_statsApiUrl))
            {
                _isApiEnabled = false;

                // Notify developer
                _logger.Error(
                    new InvalidOperationException("Stats API URL is missing or empty in the configuration file."),
                    "Stats API URL missing");

                return;
            }

            // If we reached here, configuration is valid
            _isApiEnabled = true;
        }
        catch (Exception ex)
        {
            // Notify developer
            // Catch any other errors during loading (e.g., invalid JSON format)
            _isApiEnabled = false;
            _logger.Error(ex, "Error loading Stats API configuration from appsettings.json");
        }
    }

    /// <summary>
    ///     Call the API.
    ///     If an emulator name is provided, then it is assumed this is an emulator launch call.
    ///     If no emulator name is provided, then it is a general usage call.
    /// </summary>
    /// <param name="emulatorName">The name of the emulator (if applicable); otherwise, null.</param>
    internal async Task CallApiAsync(string? emulatorName = null)
    {
        // Check if API is enabled before proceeding
        if (!_isApiEnabled)
        {
            // Expected condition (API key/URL not configured): not a bug, keep it out of the bug report service.
            _logger.Information("Stats API call skipped: API not enabled");

            return;
        }

        // Determine which payload to send based on whether emulator info is provided.
        var callType = string.IsNullOrWhiteSpace(emulatorName) ? "usage" : "emulator";
        var payloadEmulatorName = string.Equals(callType, "emulator", StringComparison.Ordinal)
            ? NormalizeEmulatorName(emulatorName)
            : null;

        // Use the loaded API URL
        if (await TryApiAsync(callType, payloadEmulatorName))
        {
            // ReSharper disable once RedundantJumpStatement
            return; // Success.
        }
    }

    private async Task<bool> TryApiAsync(string callType, string? emulatorName)
    {
        // Check if HttpClient is initialized (should be if _isApiEnabled is true)
        if (_httpClientFactory == null)
        {
            // Notify developer
            // This indicates a logic error if _isApiEnabled is true but _httpClient is null
            _logger.Error(new InvalidOperationException("HttpClient is null when attempting Stats API call."),
                "Stats API call failed: HttpClient not initialized");

            return false;
        }

        try
        {
            var httpClient = _httpClientFactory?.CreateClient("StatsClient");
            if (httpClient != null)
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                HttpContent jsonContent;
                if (string.Equals(callType, "emulator", StringComparison.Ordinal))
                {
                    // For emulator calls, send ONLY the emulatorName
                    var requestData = new { emulatorName };
                    var json = JsonSerializer.Serialize(requestData);
                    jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                {
                    // For general usage calls, send an empty body.
                    jsonContent = new StringContent("", Encoding.UTF8, "application/json");
                }

                // Use a CancellationToken with a 20-second timeout to prevent indefinite hangs
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                // Send the POST request.
                using var response = await httpClient.PostAsync(_statsApiUrl, jsonContent, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Debug("The Stats was successfully sent. API response: OK");

                    return true; // Success.
                }

                // Expected condition (server-side non-success status, e.g. 5xx during
                // maintenance or 429 rate-limit): the stats call is fire-and-forget telemetry,
                // not a bug — keep it out of the bug report service.
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                var contextMessage = $"Stats API responded with an error.\n" +
                                     $"Status Code: '{response.StatusCode}'.\n" +
                                     $"Response Body: '{errorContent}'\n" +
                                     $"CallType: {callType}" +
                                     (string.Equals(callType, "emulator", StringComparison.Ordinal)
                                         ? $", EmulatorName: {emulatorName}"
                                         : "");
                _logger.Information(new HttpRequestException($"Stats API error: {response.StatusCode}"),
                    contextMessage);
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            // Expected condition (request timed out): not a bug, keep it out of the bug report service.
            var contextMessage = $"Stats API request timed out after 20 seconds.\n" +
                                 $"Stats API URL: '{_statsApiUrl}'.\n" +
                                 $"CallType: {callType}" +
                                 (string.Equals(callType, "emulator", StringComparison.Ordinal)
                                     ? $", EmulatorName: {emulatorName}"
                                     : "");
            _logger.Information(contextMessage);

            return false;
        }
        catch (HttpRequestException ex)
        {
            // Expected condition (DNS/connection/network failure): not a bug, keep it out of the bug report service.
            var contextMessage = $"Error communicating with the Stats API at '{_statsApiUrl}'.\n" +
                                 $"CallType: {callType}" +
                                 (string.Equals(callType, "emulator", StringComparison.Ordinal)
                                     ? $", EmulatorName: {emulatorName}"
                                     : "");
            _logger.Information(ex, contextMessage);

            return false;
        }
        catch (Exception ex)
        {
            // Notify developer
            // Log any other unexpected errors
            var contextMessage = $"Unexpected error while using Stats API at '{_statsApiUrl}'.\n" +
                                 $"CallType: {callType}" +
                                 (string.Equals(callType, "emulator", StringComparison.Ordinal)
                                     ? $", EmulatorName: {emulatorName}"
                                     : "");
            _logger.Error(ex, contextMessage);

            return false;
        }
    }

    /// <summary>
    ///     Converts the input string to a title case (first letter of each word capitalized).
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The normalized string.</returns>
    private static string NormalizeEmulatorName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        // Use CultureInfo.InvariantCulture to ensure consistent title casing regardless of user's locale
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }
}