using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed EA (Electronic Arts) games via the registry, classifies them via the
///     game-classification API (same as Microsoft Store), and creates shortcuts for confirmed games.
/// </summary>
public class ScanEaGames : IGamePlatformScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScanEaGames" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="httpClientFactory">The HTTP client factory for classification API requests.</param>
    public ScanEaGames(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task ScanAsync(GameScannerService gameScannerService, ILogger logErrors, string windowsRomsPath,
        string windowsImagesPath, ISet<string> ignoredGameNames)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            const string eaRegKey = @"SOFTWARE\WOW6432Node\Electronic Arts\EA Core\Installed Games";
            using var baseKey = Registry.LocalMachine.OpenSubKey(eaRegKey);
            if (baseKey == null) return;

            var candidates = new List<EaGameCandidate>();

            foreach (var contentId in baseKey.GetSubKeyNames())
            {
                try
                {
                    using var gameKey = baseKey.OpenSubKey(contentId);
                    if (gameKey == null) continue;

                    var installDir = gameKey.GetValue("Install Dir") as string;
                    if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) continue;

                    var gameName = new DirectoryInfo(installDir).Name;
                    if (ignoredGameNames.Contains(gameName)) continue;

                    candidates.Add(new EaGameCandidate
                    {
                        Name = gameName,
                        AppId = contentId,
                        InstallLocation = installDir
                    });
                }
                catch (Exception ex)
                {
                    logErrors.Error(ex, $"Error processing EA game: {contentId}");
                }
            }

            if (candidates.Count == 0) return;

            _logger.Debug($"[ScanEaGames] Found {candidates.Count} EA apps. Sending to classification API...");
            foreach (var c in candidates)
                _logger.Debug($"[ScanEaGames]   -> Sending: Name=\"{c.Name}\" AppId=\"{c.AppId}\"");

            var confirmedGames = await ClassifyGamesViaApiAsync(candidates, logErrors);

            if (confirmedGames is { Count: > 0 })
            {
                _logger.Debug($"[ScanEaGames] API returned {confirmedGames.Count} confirmed games.");

                Directory.CreateDirectory(windowsRomsPath);

                foreach (var game in confirmedGames)
                {
                    try
                    {
                        var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(game.Name);
                        var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");

                        var shortcutContent = $"[InternetShortcut]\nURL=origin2://game/launch?offerIds={game.AppId}";
                        await File.WriteAllTextAsync(shortcutPath, shortcutContent);

                        await gameScannerService.FindAndSaveGameImageAsync(logErrors, game.Name, game.InstallLocation,
                            sanitizedGameName, windowsImagesPath);
                    }
                    catch (Exception ex)
                    {
                        logErrors.Error(ex, $"Error creating shortcut for EA game: {game.Name}");
                    }
                }
            }
            else
            {
                _logger.Debug("[ScanEaGames] API returned no confirmed games. No shortcuts created.");
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "An error occurred while scanning for EA games.");
        }
    }

    private async Task<List<EaGameCandidate>> ClassifyGamesViaApiAsync(List<EaGameCandidate> installedApps,
        ILogger logErrors)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("GameClassificationClient");

            var requestBody = new
            {
                SoftwareNames = installedApps.ConvertAll(static app => new
                {
                    app.Name,
                    app.AppId,
                    app.InstallLocation,
                    PackageFamilyName = "",
                    LogoRelativePath = ""
                })
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await client.PostAsync("api/GameIdentification/IsAGame", content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Debug($"[ScanEaGames] Game classification API returned status: {response.StatusCode}");

                if (ExpectedHttpStatus.IsExpectedExternalCondition(response.StatusCode))
                {
                    // Expected condition (rate limit or hosting/WAF block): not a bug, keep it
                    // out of the bug report service.
                    logErrors.Information(
                        $"Game classification API returned {response.StatusCode} (expected external condition). Returning empty game list.");
                }
                else
                {
                    logErrors.Warning(
                        $"Game classification API failed with status {response.StatusCode}. Returning empty game list.");
                }

                return [];
            }

            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
            var apiResponse = JsonSerializer.Deserialize<GameClassificationResponse>(responseJson, JsonOptions);

            if (apiResponse?.Games == null)
            {
                _logger.Debug("[ScanEaGames] Game classification API returned null games list.");
                return [];
            }

            _logger.Debug($"[ScanEaGames] API deserialized games count: {apiResponse.Games.Count}");
            foreach (var g in apiResponse.Games)
                _logger.Debug($"[ScanEaGames]   <- Received game: Name=\"{g.Name}\" AppId=\"{g.AppId}\"");

            var confirmedGames = apiResponse.Games.Select(static g => new EaGameCandidate
            {
                Name = g.Name,
                AppId = g.AppId,
                InstallLocation = g.InstallLocation
            }).ToList();

            return confirmedGames;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("[ScanEaGames] Game classification API request timed out. Returning empty game list.");
            return [];
        }
        catch (HttpRequestException ex)
        {
            _logger.Debug(
                $"[ScanEaGames] Game classification API network error: {ex.Message}. Returning empty game list.");
            return [];
        }
        catch (Exception ex)
        {
            _logger.Debug($"[ScanEaGames] Game classification API error: {ex.Message}. Returning empty game list.");
            logErrors.Error(ex, "Failed to classify EA games via API.");
            return [];
        }
    }

    private sealed class EaGameCandidate
    {
        public string Name { get; set; } = "";
        public string AppId { get; set; } = null!;
        public string InstallLocation { get; set; } = null!;
    }
}