using System.Text;
using System.Text.Json;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.ParameterResolver;

/// <summary>
///     Resolves emulator launch parameters by calling a remote API endpoint with the system and emulator context.
/// </summary>
public class ParameterResolverService : IParameterResolverService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ParameterResolverService" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for making API requests.</param>
    /// <param name="logErrors">The logger instance for error logging.</param>
    public ParameterResolverService(IHttpClientFactory httpClientFactory, ILogger logErrors)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logErrors;
    }

    /// <summary>
    ///     Sends a parameter resolution request to the remote API and returns the resolved parameters.
    /// </summary>
    /// <param name="request">The request containing system name, emulator name, and ROM file information.</param>
    /// <returns>The resolved parameters, or null if the API call fails.</returns>
    public async Task<ParameterResolverResult?> ResolveParametersAsync(ParameterResolverRequest request)
    {
        var apiKey = AppConstants.GetApiKey();
        var client = _httpClientFactory.CreateClient("ParameterResolverClient");

        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/ParameterResolver/resolve");
        httpRequest.Headers.Add("X-Api-Key", apiKey);
        httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        string responseBody;
        bool success;
        int statusCode;
        try
        {
            // Bound the call so a black-holed API cannot hang the launch flow forever (CORE-20).
            // The response is disposed before returning (CORE-19); only the body and
            // status are captured for use below.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await client.SendAsync(httpRequest, cts.Token);
            responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            success = response.IsSuccessStatusCode;
            statusCode = (int)response.StatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Network/server failure (unreachable host, timeout): an expected external-service
            // condition, not a bug. Log at Information so it is never reported as a bug.
            _logger.Information(ex, "ParameterResolver API unreachable or timed out");
            return null;
        }

        if (success)
        {
            try
            {
                return JsonSerializer.Deserialize<ParameterResolverResult>(responseBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                // The API returned a 200 with an unparseable body; an external-service condition,
                // not a bug. Log at Information so it is never reported as a bug.
                _logger.Information(ex, "ParameterResolver API returned malformed JSON");
                return null;
            }
        }

        var apiException =
            new InvalidOperationException($"ParameterResolver API returned {statusCode}: {responseBody}");
        // A non-success response (e.g. 400 validation errors from empty user input) is an expected
        // user-error condition; log at Information so it is never reported as a bug.
        _logger.Information(apiException, "ParameterResolver API error");
        return null;
    }
}