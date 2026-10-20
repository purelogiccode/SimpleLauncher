using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Models;

namespace SimpleLauncher.Services.RetroAchievements;

/// <summary>
///     Provides methods to interact with the RetroAchievements REST API for user authentication, game progress, and
///     leaderboard data.
/// </summary>
public class RetroAchievementsService
{
    private readonly string _apiBaseUrl;

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _requestBaseUrl;
    private readonly string _siteBaseUrl;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsService" /> class.
    /// </summary>
    public RetroAchievementsService(
        IHttpClientFactory httpClientFactory,
        RetroAchievementsManager raManager,
        ILogger logErrors,
        IConfiguration configuration,
        ILogger logger)
    {
        _httpClient = httpClientFactory?.CreateClient("RetroAchievementsClient") ??
                      throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logErrors ?? throw new ArgumentNullException(nameof(logErrors));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RaManager = raManager ?? throw new ArgumentNullException(nameof(raManager));

        // Load URLs from appsettings.json
        _apiBaseUrl = configuration.GetValue<string>("Urls:RetroAchievementsApi") ??
                      "https://retroachievements.org/API/";
        _requestBaseUrl = configuration.GetValue<string>("Urls:RetroAchievementsRequest") ??
                          "https://retroachievements.org/dorequest.php";
        _siteBaseUrl = configuration.GetValue<string>("Urls:RetroAchievementsSite") ?? "https://retroachievements.org/";
    }

    /// <summary>
    ///     Gets the RetroAchievements manager containing the local game database.
    /// </summary>
    public RetroAchievementsManager RaManager { get; }

    /// <summary>
    ///     Logs in to RetroAchievements to retrieve a session token.
    /// </summary>
    public async Task<string?> GetSessionTokenAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        try
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "r", "login" },
                { "u", username },
                { "p", password }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await _httpClient.PostAsync(_requestBaseUrl, content);

            if (!response.IsSuccessStatusCode) return null;

            var jsonString = await response.Content.ReadAsStringAsync();
            if (!TryGetJsonPayload(jsonString, nameof(GetSessionTokenAsync), out var json)) return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return root.TryGetProperty("Success", out var success) && success.GetBoolean() &&
                   root.TryGetProperty("Token", out var token)
                ? token.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            // Expected external condition (malformed/non-JSON API payload): not a bug.
            _logger.Information(ex,
                "[RA Service] Invalid JSON response in GetSessionTokenAsync; the API may be temporarily unavailable.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[RA Service] Failed to get session token");
            return null;
        }
    }

    /// <summary>
    ///     Fetches the user's progress and achievement list for a specific game ID.
    /// </summary>
    public async Task<(RaUserGameProgress? Progress, List<RaAchievement>? Achievements)>
        GetGameInfoAndUserProgressAsync(int gameId, string username, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.Debug("[RA Service] Username or API Key is missing");
            return (null, null);
        }

        try
        {
            _logger.Debug($"[RA Service] Fetching user progress for GameID {gameId}...");

            var url =
                $"{_apiBaseUrl}API_GetGameInfoAndUserProgress.php?u={Uri.EscapeDataString(username)}&g={gameId}&y={Uri.EscapeDataString(apiKey)}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Warning(
                    $"[RA Service] API_GetGameInfoAndUserProgress failed with status {response.StatusCode} for gameId {gameId}: {error}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new RaUnauthorizedException(
                        "RetroAchievements API returned Unauthorized. Check username and API key.");
                }

                return (null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = DeserializeApiResponse<RaGameProgressResponse>(json, nameof(GetGameInfoAndUserProgressAsync));

            if (apiResponse == null) return (null, null);

            var apiAchievements = apiResponse.Achievements?.Values ?? Enumerable.Empty<RaApiAchievement>();

            var raApiAchievements = apiAchievements.ToList();
            var pointsEarnedHardcore = raApiAchievements
                .Where(static a => a.DateEarnedHardcore != null)
                .Sum(static a => a.Points);

            var progress = new RaUserGameProgress
            {
                GameTitle = apiResponse.Title,
                GameIconUrl = $"{_siteBaseUrl.TrimEnd('/')}{apiResponse.ImageIcon}",
                ConsoleName = apiResponse.ConsoleName,
                AchievementsEarned = apiResponse.NumAwardedToUser,
                TotalAchievements = apiResponse.NumAchievements,
                PointsEarned = raApiAchievements.Where(static a => a.DateEarned != null).Sum(static a => a.Points),
                PointsEarnedHardcore = pointsEarnedHardcore,
                TotalPoints = raApiAchievements.Sum(static a => a.Points),
                UserCompletion = apiResponse.UserCompletion,
                UserCompletionHardcore = apiResponse.UserCompletionHardcore,
                HighestAwardKind = apiResponse.HighestAwardKind,
                HighestAwardDate = apiResponse.HighestAwardDate
            };

            var achievements = raApiAchievements
                .OrderBy(static a => a.DisplayOrder)
                .Select(a => new RaAchievement
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    Points = a.Points,
                    BadgeUri = $"{_siteBaseUrl.TrimEnd('/')}/Badge/{a.BadgeName}.png",
                    IsUnlocked = a.DateEarned != null || a.DateEarnedHardcore != null,
                    DateUnlocked = a.DateEarnedHardcore ?? a.DateEarned,
                    UnlockedInHardcore = a.DateEarnedHardcore != null,
                    DisplayOrder = a.DisplayOrder,
                    NumAwarded = a.NumAwarded,
                    NumAwardedHardcore = a.NumAwardedHardcore,
                    Author = a.Author,
                    AuthorUlid = a.AuthorUlid,
                    DateModified = a.DateModified,
                    DateCreated = a.DateCreated,
                    BadgeName = a.BadgeName,
                    Type = a.Type,
                    DateEarnedHardcore = a.DateEarnedHardcore,
                    DateEarned = a.DateEarned,
                    TrueRatio = a.TrueRatio
                }).ToList();

            return (progress, achievements);
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Unexpected error in GetGameInfoAndUserProgressAsync for gameId {gameId}.");
            return (null, null);
        }
    }

    /// <summary>
    ///     Fetches extended game details including achievements, metadata, and player statistics.
    /// </summary>
    public async Task<RaGameExtendedDetails?> GetGameExtendedAsync(int gameId, string username, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var url =
                $"{_apiBaseUrl}API_GetGameExtended.php?u={Uri.EscapeDataString(username)}&i={gameId}&y={Uri.EscapeDataString(apiKey)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new RaUnauthorizedException("RetroAchievements API returned Unauthorized.");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return DeserializeApiResponse<RaGameExtendedDetails>(json, nameof(GetGameExtendedAsync));
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Error in GetGameExtendedAsync for gameId {gameId}.");
            return null;
        }
    }

    /// <summary>
    ///     Retrieves the user's rank and score for a specific game.
    /// </summary>
    public async Task<IList<RaUserGameRank>?> GetUserGameRankAndScoreAsync(int gameId, string username, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var url =
                $"{_apiBaseUrl}API_GetUserGameRankAndScore.php?u={Uri.EscapeDataString(username)}&g={gameId}&y={Uri.EscapeDataString(apiKey)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new RaUnauthorizedException("RetroAchievements API returned Unauthorized.");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return DeserializeApiResponse<List<RaUserGameRank>>(json, nameof(GetUserGameRankAndScoreAsync));
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Error in GetUserGameRankAndScoreAsync for gameId {gameId}.");
            return null;
        }
    }

    /// <summary>
    ///     Retrieves the global leaderboard rank and score data for a specific game.
    /// </summary>
    public async Task<IList<RaGameRankAndScore>?> GetGameRankAndScoreAsync(int gameId, string username, string apiKey,
        bool latestMasters = false)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey)) return null;

        var typeParam = latestMasters ? "1" : "0";
        try
        {
            var url =
                $"{_apiBaseUrl}API_GetGameRankAndScore.php?u={Uri.EscapeDataString(username)}&g={gameId}&y={Uri.EscapeDataString(apiKey)}&t={typeParam}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new RaUnauthorizedException("RetroAchievements API returned Unauthorized.");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return DeserializeApiResponse<List<RaGameRankAndScore>>(json, nameof(GetGameRankAndScoreAsync));
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Error in GetGameRankAndScoreAsync for gameId {gameId}.");
            return null;
        }
    }

    /// <summary>
    ///     Retrieves the RetroAchievements profile for the specified user.
    /// </summary>
    public async Task<RaProfile?> GetUserProfileAsync(string username, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var url =
                $"{_apiBaseUrl}API_GetUserProfile.php?u={Uri.EscapeDataString(username)}&y={Uri.EscapeDataString(apiKey)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new RaUnauthorizedException("RetroAchievements API returned Unauthorized.");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return DeserializeApiResponse<RaProfile>(json, nameof(GetUserProfileAsync));
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            // Expected condition (DNS/connection/SSL failure, e.g. bugs #68173/#68172):
            // not a bug, keep it out of the bug report service.
            _logger.Information(ex, $"[RA Service] Network error in GetUserProfileAsync for user {username}.");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            // Expected condition (request timed out): not a bug, keep it out of the bug report service.
            _logger.Information(ex, $"[RA Service] Timeout in GetUserProfileAsync for user {username}.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Error in GetUserProfileAsync for user {username}.");
            return null;
        }
    }

    /// <summary>
    ///     Retrieves the user's recently played games with achievement progress.
    /// </summary>
    public async Task<IList<RaRecentlyPlayedGame>?> GetUserRecentlyPlayedGamesAsync(string username, string apiKey,
        int count = 10, int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var url =
                $"{_apiBaseUrl}API_GetUserRecentlyPlayedGames.php?u={Uri.EscapeDataString(username)}&y={Uri.EscapeDataString(apiKey)}&c={count}&o={offset}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new RaUnauthorizedException("RetroAchievements API returned Unauthorized.");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return DeserializeApiResponse<List<RaRecentlyPlayedGame>>(json, nameof(GetUserRecentlyPlayedGamesAsync));
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Error in GetUserRecentlyPlayedGamesAsync for user {username}.");
            return null;
        }
    }

    /// <summary>
    ///     Retrieves achievements earned by the user within a specified date range.
    /// </summary>
    public async Task<IList<RaEarnedAchievement>?> GetAchievementsEarnedBetweenAsync(string username, string apiKey,
        DateTime fromDate, DateTime toDate)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey)) return null;

        var epochFrom = new DateTimeOffset(fromDate).ToUnixTimeSeconds();
        var epochTo = new DateTimeOffset(toDate.AddDays(1).AddSeconds(-1)).ToUnixTimeSeconds();

        try
        {
            var url =
                $"{_apiBaseUrl}API_GetAchievementsEarnedBetween.php?u={Uri.EscapeDataString(username)}&f={epochFrom}&t={epochTo}&y={Uri.EscapeDataString(apiKey)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new RaUnauthorizedException("RetroAchievements API returned Unauthorized.");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return DeserializeApiResponse<List<RaEarnedAchievement>>(json, nameof(GetAchievementsEarnedBetweenAsync));
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Error in GetAchievementsEarnedBetweenAsync for user {username}.");
            return null;
        }
    }

    /// <summary>
    ///     Retrieves the user's game completion progress with award information.
    /// </summary>
    public async Task<IList<RaUserCompletionGame>?> GetUserCompletionProgressAsync(string username, string apiKey,
        int count = 100, int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var url =
                $"{_apiBaseUrl}API_GetUserCompletionProgress.php?u={Uri.EscapeDataString(username)}&y={Uri.EscapeDataString(apiKey)}&c={count}&o={offset}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new RaUnauthorizedException("RetroAchievements API returned Unauthorized.");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = DeserializeApiResponse<RaUserCompletionProgressResponse>(json,
                nameof(GetUserCompletionProgressAsync));

            if (apiResponse?.Results == null) return null;

            foreach (var game in apiResponse.Results)
            {
                if (!string.IsNullOrEmpty(game.ImageIcon) &&
                    !game.ImageIcon.StartsWith(_siteBaseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    game.ImageIcon = $"{_siteBaseUrl}{game.ImageIcon}";
                }
            }

            return apiResponse.Results;
        }
        catch (RaUnauthorizedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA Service] Error in GetUserCompletionProgressAsync for user {username}.");
            return null;
        }
    }

    /// <summary>
    ///     Deserializes a RetroAchievements API payload. An HTML or empty payload is an expected
    ///     external condition (Cloudflare challenge, rate limit, maintenance page) and is logged at
    ///     Information so it never reaches the bug report service (bugs #68823-68828).
    /// </summary>
    private T? DeserializeApiResponse<T>(string json, string operation)
    {
        if (!TryGetJsonPayload(json, operation, out var payload)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(payload);
        }
        catch (JsonException ex)
        {
            // The API returned a payload that is not valid JSON: not an application bug.
            _logger.Information(ex,
                $"[RA Service] Invalid JSON response in {operation}; the API may be temporarily unavailable.");
            return default;
        }
    }

    /// <summary>
    ///     Returns false for empty or non-JSON (HTML) API payloads, logging them at Information
    ///     level so they stay out of the bug report service.
    /// </summary>
    private bool TryGetJsonPayload(string json, string operation, out string payload)
    {
        payload = json;

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.Information(
                $"[RA Service] Empty response in {operation}; the RetroAchievements API may be temporarily unavailable.");
            return false;
        }

        var trimmed = json.AsSpan().TrimStart();
        if (!trimmed.IsEmpty && trimmed[0] == '<')
        {
            _logger.Information(
                $"[RA Service] Non-JSON response in {operation}; the RetroAchievements API returned an HTML page (maintenance, rate limit or challenge). Response starts with: {GetResponseSnippet(json)}");
            return false;
        }

        return true;
    }

    private static string GetResponseSnippet(string response)
    {
        var singleLine = response.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= 120 ? singleLine : $"{singleLine[..120]}...";
    }
}
