using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Diagnostics;

namespace SimpleLauncher.Core.Services.CoverCatalog;

/// <summary>
///     Downloads missing per-game covers from the R2 cover catalog. The service is
///     local-first: it builds one local name index, matches the current games against it,
///     and only then asks the catalog for a canonical image. Catalog and system indexes are
///     cached on disk and stale caches are used when the network is unavailable.
/// </summary>
public sealed class CoverCatalogDownloadService
{
    private const string HttpClientName = "CoverCatalogClient";
    private const string DefaultCatalogBaseUrl =
        "https://assets.purelogiccode.com/Simple%20Launcher/covers/v1";
    private const int MaxDownloadAttempts = 3;
    private const int MaxParallelDownloads = 4;
    // Bump when the catalog derivative format changes so a new app build ignores the
    // previous index cache (JPEG v1 -> PNG with alpha v2).
    private const string CacheFormatVersion = "v2";

    private static readonly TimeSpan CatalogCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan IndexCacheTtl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _cacheDirectory;
    private readonly string _catalogBaseUrl;
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _imageExtensions;
    private readonly ILogger _logger;
    private readonly SettingsManagerService _settings;

    /// <summary>Initializes a new instance of <see cref="CoverCatalogDownloadService" />.</summary>
    /// <param name="configuration">Application configuration (catalog URL and image extensions).</param>
    /// <param name="httpClientFactory">Factory for the named cover-catalog HTTP client.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="settings">The settings manager (fuzzy threshold and annotation stripping).</param>
    public CoverCatalogDownloadService(IConfiguration configuration, IHttpClientFactory httpClientFactory,
        ILogger logger, SettingsManagerService settings)
        : this(configuration, httpClientFactory, logger, settings, null)
    {
    }

    /// <summary>Initializes a new instance of <see cref="CoverCatalogDownloadService" /> with a cache override.</summary>
    /// <param name="configuration">Application configuration (catalog URL and image extensions).</param>
    /// <param name="httpClientFactory">Factory for the named cover-catalog HTTP client.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="settings">The settings manager (fuzzy threshold and annotation stripping).</param>
    /// <param name="cacheDirectory">Cache folder override (tests); null uses the AppData folder.</param>
    public CoverCatalogDownloadService(IConfiguration configuration, IHttpClientFactory httpClientFactory,
        ILogger logger, SettingsManagerService settings, string? cacheDirectory)
    {
        _logger = logger;
        _settings = settings;
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _catalogBaseUrl = (configuration.GetValue<string>("ApiSettings:CoverCatalogUrl") ??
                           DefaultCatalogBaseUrl).TrimEnd('/');
        _cacheDirectory = cacheDirectory ??
                          Path.Combine(AppDataPaths.SimpleLauncherDataFolder, "CoverCatalog");
        _imageExtensions = new HashSet<string>(
            configuration.GetValue<string[]>("ImageExtensions") ?? [".png", ".jpg", ".jpeg"],
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Downloads the missing covers of one system.</summary>
    /// <param name="request">The system, image folder and game paths to check.</param>
    /// <param name="progress">Optional progress receiver (usually marshalled to the UI).</param>
    /// <param name="cancellationToken">Token to cancel the batch.</param>
    /// <returns>The batch result.</returns>
    public async Task<CoverDownloadResult> DownloadMissingCoversAsync(CoverDownloadRequest request,
        IProgress<CoverDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var games = GetGameNames(request.GameFilePaths);
        if (games.Count == 0) return new CoverDownloadResult { Status = CoverDownloadStatus.NoGames };

        var imageFolder = ResolveImageFolder(request);
        Directory.CreateDirectory(imageFolder);

        CoverCatalogManifest catalog;
        try
        {
            catalog = await LoadCatalogAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "The cover catalog could not be loaded");
            return new CoverDownloadResult
            {
                Status = CoverDownloadStatus.CatalogUnavailable,
                TotalGames = games.Count,
                ErrorMessage = ex.Message
            };
        }

        var system = (catalog.Systems ?? []).FirstOrDefault(system =>
            string.Equals(system.Name, request.SystemName, StringComparison.OrdinalIgnoreCase));
        if (system is null)
        {
            _logger.Information("The cover catalog has no entry for system {System}", request.SystemName);
            return new CoverDownloadResult
            {
                Status = CoverDownloadStatus.SystemNotAvailable,
                TotalGames = games.Count
            };
        }

        CoverCatalogIndex index;
        try
        {
            index = await LoadIndexAsync(system, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "The cover index for {System} could not be loaded", request.SystemName);
            return new CoverDownloadResult
            {
                Status = CoverDownloadStatus.CatalogUnavailable,
                TotalGames = games.Count,
                ErrorMessage = ex.Message
            };
        }

        var catalogLookup = new CoverCatalogLookup(index.Images ?? [], stripAnnotations: true);
        var localLookup = new CoverCatalogLookup(CreateLocalEntries(imageFolder),
            stripAnnotations: _settings.EnableAnnotationStripping);
        var fuzzyThreshold = _settings.EnableFuzzyMatching ? _settings.FuzzyMatchingThreshold : 0;

        var missing = new List<MissingCover>();
        var alreadyPresent = 0;
        var notFound = 0;

        foreach (var game in games)
        {
            var local = localLookup.FindBestMatch(game, fuzzyThreshold);
            var entry = catalogLookup.FindBestMatch(game, fuzzyThreshold);

            // A JPEG written by the old catalog cannot carry alpha. If the current catalog
            // entry is a transparent PNG, keep the legacy JPEG and add the corrected PNG
            // beside it (the image extension order prefers .png).
            if (local is not null && !NeedsTransparencyRepair(local, entry))
            {
                alreadyPresent++;
                continue;
            }

            if (entry is null)
            {
                notFound++;
                continue;
            }

            missing.Add(new MissingCover(game, entry));
        }

        var completed = alreadyPresent + notFound;
        var matched = missing.Count;
        progress?.Report(new CoverDownloadProgress(games.Count, completed, ""));

        if (missing.Count == 0)
        {
            return new CoverDownloadResult
            {
                Status = alreadyPresent == games.Count
                    ? CoverDownloadStatus.NoMissingCovers
                    : CoverDownloadStatus.Completed,
                TotalGames = games.Count,
                AlreadyPresent = alreadyPresent,
                NotFound = notFound
            };
        }

        var downloaded = 0;
        var failed = 0;
        var notFoundDownload = 0;
        var raceSkipped = 0;
        var completedCounter = completed;
        var catalogBaseUrl = string.IsNullOrWhiteSpace(catalog.BaseUrl)
            ? _catalogBaseUrl
            : catalog.BaseUrl.TrimEnd('/');

        await Parallel.ForEachAsync(missing,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken },
            async (item, token) =>
            {
                try
                {
                    var extension = Path.GetExtension(item.Entry.File);
                    if (string.IsNullOrEmpty(extension) || !_imageExtensions.Contains(extension))
                        extension = ".png";
                    var target = Path.Combine(imageFolder, $"{item.Game}{extension}");
                    if (File.Exists(target) && new FileInfo(target).Length > 0)
                    {
                        Interlocked.Increment(ref raceSkipped);
                    }
                    else
                    {
                        var url = BuildImageUrl(catalogBaseUrl, system.Slug, item.Entry.File);
                        var outcome = await DownloadImageAsync(url, target, token);
                        if (outcome == DownloadOutcome.Downloaded)
                        {
                            Interlocked.Increment(ref downloaded);
                        }
                        else
                        {
                            Interlocked.Increment(ref notFoundDownload);
                            _logger.Debug("Cover not found in the catalog for {Game}", item.Game);
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    _logger.Information(ex, "Failed to download the cover for {Game}", item.Game);
                }

                var done = Interlocked.Increment(ref completedCounter);
                progress?.Report(new CoverDownloadProgress(games.Count, done, item.Game));
            });

        var result = new CoverDownloadResult
        {
            Status = CoverDownloadStatus.Completed,
            TotalGames = games.Count,
            AlreadyPresent = alreadyPresent + raceSkipped,
            Matched = matched,
            Downloaded = downloaded,
            NotFound = notFound + notFoundDownload,
            Failed = failed
        };

        _logger.Information(
            "Cover download for {System}: {Downloaded} downloaded, {Already} already present, {NotFound} not found, {Failed} failed",
            request.SystemName, result.Downloaded, result.AlreadyPresent, result.NotFound, result.Failed);
        return result;
    }

    private async Task<CoverCatalogManifest> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        return await LoadCachedOrFetchAsync<CoverCatalogManifest>(
            $"{_catalogBaseUrl}/catalog.json",
            Path.Combine(_cacheDirectory, $"catalog.{CacheFormatVersion}.json"),
            CatalogCacheTtl, cancellationToken);
    }

    private async Task<CoverCatalogIndex> LoadIndexAsync(CoverCatalogSystem system, CancellationToken cancellationToken)
    {
        var relative = string.IsNullOrWhiteSpace(system.IndexFile)
            ? $"{system.Slug}/index.json"
            : system.IndexFile;
        return await LoadCachedOrFetchAsync<CoverCatalogIndex>(
            $"{_catalogBaseUrl}/{relative}",
            Path.Combine(_cacheDirectory, $"{system.Slug}.{CacheFormatVersion}.index.json"),
            IndexCacheTtl, cancellationToken);
    }

    private async Task<T> LoadCachedOrFetchAsync<T>(string url, string cachePath, TimeSpan ttl,
        CancellationToken cancellationToken) where T : class
    {
        if (File.Exists(cachePath) &&
            DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < ttl)
        {
            try
            {
                var cached = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(cachePath, cancellationToken),
                    JsonOptions);
                if (cached is not null) return cached;
            }
            catch (JsonException ex)
            {
                _logger.Information(ex, "Ignoring a corrupt cover cache file {Path}", cachePath);
            }
        }

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions) ??
                        throw new InvalidOperationException($"The cover response was empty: {url}");
            await WriteCacheAsync(cachePath, json);
            return value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && File.Exists(cachePath))
        {
            _logger.Information(ex, "Using the stale cover cache for {Path}", cachePath);
            return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(cachePath, cancellationToken),
                       JsonOptions) ??
                   throw new InvalidOperationException($"The stale cover cache could not be read: {cachePath}", ex);
        }
    }

    private async Task WriteCacheAsync(string cachePath, string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath) ?? _cacheDirectory);
            var temp = $"{cachePath}.tmp-{Guid.NewGuid():N}";
            try
            {
                await File.WriteAllTextAsync(temp, json);
                File.Move(temp, cachePath, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Information(ex, "The cover cache could not be written to {Path}", cachePath);
        }
    }

    private async Task<DownloadOutcome> DownloadImageAsync(string url, string destination,
        CancellationToken cancellationToken)
    {
        var temp = $"{destination}.tmp-{Guid.NewGuid():N}";
        try
        {
            for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    if (response.StatusCode == HttpStatusCode.NotFound) return DownloadOutcome.NotFound;
                    if (!response.IsSuccessStatusCode)
                    {
                        if (IsTransientStatus(response.StatusCode) && attempt < MaxDownloadAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                    }

                    await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using (var target = new FileStream(temp, FileMode.Create, FileAccess.Write,
                                     FileShare.None, 81920, true))
                    {
                        await source.CopyToAsync(target, cancellationToken);
                    }

                    File.Move(temp, destination, true);
                    return DownloadOutcome.Downloaded;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < MaxDownloadAttempts && IsTransient(ex))
                {
                    _logger.Debug(ex, "Retrying the cover download ({Attempt}/{Max})", attempt, MaxDownloadAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }

        throw new InvalidOperationException($"The cover download failed: {url}");
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    private static bool IsTransient(Exception exception)
    {
        return exception switch
        {
            HttpRequestException { StatusCode: null } => true,
            HttpRequestException { StatusCode: { } code } => IsTransientStatus(code),
            TaskCanceledException => true,
            IOException => true,
            _ => false
        };
    }

    private List<CoverCatalogEntry> CreateLocalEntries(string imageFolder)
    {
        if (!Directory.Exists(imageFolder)) return [];

        return Directory.EnumerateFiles(imageFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => _imageExtensions.Contains(Path.GetExtension(path)))
            .Select(path => new
            {
                Path = path,
                Name = Path.GetFileNameWithoutExtension(path)
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Name) &&
                                  !string.Equals(item.Name, "default", StringComparison.OrdinalIgnoreCase))
            .Select(static item => new CoverCatalogEntry { File = item.Path, Names = [item.Name] })
            .ToList();
    }

    /// <summary>
    ///     Detects a cover downloaded by the old JPEG catalog that must be repaired by the
    ///     new transparent PNG. Only transparent catalog entries trigger the repair; opaque
    ///     user JPEG covers are left untouched.
    /// </summary>
    private static bool NeedsTransparencyRepair(CoverCatalogEntry local, CoverCatalogEntry? catalog)
    {
        if (catalog?.HasAlpha != true) return false;
        if (!string.Equals(Path.GetExtension(catalog.File), ".png", StringComparison.OrdinalIgnoreCase))
            return false;

        var localExtension = Path.GetExtension(local.File);
        return string.Equals(localExtension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(localExtension, ".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveImageFolder(CoverDownloadRequest request)
    {
        var resolved = PathHelper.ResolveRelativeToAppDirectory(request.SystemImageFolder);
        if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
        return Path.Combine(PathHelper.BaseFolder, "images", request.SystemName);
    }

    private static List<string> GetGameNames(IEnumerable<string> gameFilePaths)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in gameFilePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            var name = Directory.Exists(path)
                ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }

        return names.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildImageUrl(string baseUrl, string slug, string file)
    {
        return $"{baseUrl.TrimEnd('/')}/{slug}/{Uri.EscapeDataString(file)}";
    }

    private enum DownloadOutcome
    {
        Downloaded,
        NotFound
    }

    private sealed record MissingCover(string Game, CoverCatalogEntry Entry);
}
