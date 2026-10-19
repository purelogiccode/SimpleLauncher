using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.FindCoverImage;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Services.SystemImageResolver;

/// <summary>
///     Resolves display images for system configurations using exact name matching with
///     optional annotation-stripped and fuzzy matching fallbacks (port of the WPF
///     SystemImageResolverService).
/// </summary>
public class SystemImageResolverService : ISystemImageResolverService
{
    private static readonly string[] DefaultImageExtensions = [".png", ".jpg", ".jpeg"];

    private const int ImageFileCacheMilliseconds = 2000;

    private readonly IConfiguration _configuration;
    private readonly Lock _imageFilesLock = new();
    private readonly SettingsManagerService _settings;
    private List<string>? _imageFilesCache;
    private long _imageFilesCacheTimestamp;

    /// <summary>
    ///     Initializes a new instance of the SystemImageResolverService with the specified dependencies.
    /// </summary>
    public SystemImageResolverService(IConfiguration configuration, SettingsManagerService settings)
    {
        _configuration = configuration;
        _settings = settings;
    }

    /// <inheritdoc />
    public Task<string> ResolveDisplayImageAsync(SystemManagerConfig config)
    {
        var match = FindBestMatch(config.SystemName);
        if (match is not null) return Task.FromResult(match);

        var appBaseDir = AppContext.BaseDirectory;
        var defaultImagePath = Path.Combine(appBaseDir, "images", "systems", "default.png");
        return Task.FromResult(File.Exists(defaultImagePath)
            ? defaultImagePath
            : Path.Combine(appBaseDir, "images", "default.png"));
    }

    /// <inheritdoc />
    public Task<string?> ResolveSystemIconAsync(string systemName)
    {
        return Task.FromResult(ResolveSystemIcon(systemName));
    }

    /// <inheritdoc />
    public string? ResolveSystemIcon(string systemName)
    {
        return FindBestMatch(systemName);
    }

    /// <summary>
    ///     Finds the best image for a system name: exact file name, annotation-stripped
    ///     exact match (both directions), then Jaro-Winkler fuzzy match above the
    ///     configured threshold. Null when nothing matches.
    /// </summary>
    private string? FindBestMatch(string? systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName)) return null;

        var appBaseDir = AppContext.BaseDirectory;
        var systemImageFolder = Path.Combine(appBaseDir, "images", "systems");
        var imageExtensions = _configuration.GetValue<string[]>("ImageExtensions") ?? DefaultImageExtensions;

        // Exact match
        foreach (var ext in imageExtensions)
        {
            var systemImagePath = Path.Combine(systemImageFolder, $"{systemName}{ext}");
            if (File.Exists(systemImagePath)) return systemImagePath;
        }

        if (!Directory.Exists(systemImageFolder)) return null;

        var enableAnnotationStripping = _settings.EnableAnnotationStripping;

        // Normalized exact match (strip annotations from the system name)
        if (enableAnnotationStripping)
        {
            var strippedSystemName = FindCoverImageService.StripAnnotations(systemName);
            if (!string.Equals(strippedSystemName, systemName, StringComparison.Ordinal))
            {
                foreach (var ext in imageExtensions)
                {
                    var systemImagePath = Path.Combine(systemImageFolder, $"{strippedSystemName}{ext}");
                    if (File.Exists(systemImagePath)) return systemImagePath;
                }

                // Try stripping annotations from image filenames too
                foreach (var fileInFolder in GetImageFiles(systemImageFolder, imageExtensions))
                {
                    var fileWithoutExt = Path.GetFileNameWithoutExtension(fileInFolder);
                    if (string.IsNullOrEmpty(fileWithoutExt)) continue;

                    if (string.Equals(strippedSystemName, FindCoverImageService.StripAnnotations(fileWithoutExt),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return fileInFolder;
                    }
                }
            }
        }

        var enableFuzzyMatching = _settings.EnableFuzzyMatching;
        var similarityThreshold = _settings.FuzzyMatchingThreshold;

        if (!enableFuzzyMatching) return null;

        var filesInImageFolder = GetImageFiles(systemImageFolder, imageExtensions);

        string? bestMatchPath = null;
        double highestSimilarity = 0;
        var lowerSystemName = systemName.ToLowerInvariant();
        var normalizedSystemName = enableAnnotationStripping
            ? FindCoverImageService.StripAnnotations(lowerSystemName)
            : lowerSystemName;

        foreach (var filePath in filesInImageFolder)
        {
            var fileWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrEmpty(fileWithoutExt)) continue;

            var lowerFileName = fileWithoutExt.ToLowerInvariant();
            var normalizedFileName = enableAnnotationStripping
                ? FindCoverImageService.StripAnnotations(lowerFileName)
                : lowerFileName;

            var similarity =
                FindCoverImageService.CalculateJaroWinklerSimilarity(normalizedSystemName, normalizedFileName);

            if (!(similarity > highestSimilarity)) continue;

            highestSimilarity = similarity;
            bestMatchPath = filePath;
        }

        return bestMatchPath != null && highestSimilarity >= similarityThreshold ? bestMatchPath : null;
    }

    /// <summary>
    ///     Returns the image files in the system-image folder. The raw directory listing is
    ///     cached briefly (2 s): one grid build resolves every configured system, and each
    ///     resolution would otherwise re-enumerate the folder twice (annotation stripping
    ///     and fuzzy matching). A short lifetime still picks up icons dropped in at runtime.
    /// </summary>
    private List<string> GetImageFiles(string systemImageFolder, string[] imageExtensions)
    {
        List<string> files;
        lock (_imageFilesLock)
        {
            var now = Environment.TickCount64;
            if (_imageFilesCache is null || now - _imageFilesCacheTimestamp > ImageFileCacheMilliseconds)
            {
                _imageFilesCache = Directory.GetFiles(systemImageFolder).ToList();
                _imageFilesCacheTimestamp = now;
            }

            files = _imageFilesCache;
        }

        return files
            .Where(f => imageExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}