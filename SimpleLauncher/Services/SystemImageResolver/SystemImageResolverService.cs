using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.FindCoverImage;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.SystemManager;

namespace SimpleLauncher.Services.SystemImageResolver;

/// <summary>
///     Resolves display images for system configurations using exact name matching with optional fuzzy matching fallback.
/// </summary>
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
public class SystemImageResolverService : ISystemImageResolverService
{
    private readonly IConfiguration _configuration;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly SettingsManagerService _settings;

    /// <summary>
    ///     Initializes a new instance of the SystemImageResolverService with the specified dependencies.
    /// </summary>
    public SystemImageResolverService(IConfiguration configuration, IFindCoverImageService findCoverImage,
        SettingsManagerService settings)
    {
        _configuration = configuration;
        _findCoverImage = findCoverImage;
        _settings = settings;
    }

    /// <summary>
    ///     Asynchronously resolves the display image path for a system, using fuzzy matching if enabled.
    /// </summary>
    public Task<string> ResolveDisplayImageAsync(SystemManagerService config)
    {
        var appBaseDir = AppDomain.CurrentDomain.BaseDirectory;
        var systemImageFolder = Path.Combine(appBaseDir, "images", "systems");
        var systemName = config.SystemName;
        var imageExtensions = _configuration.GetValue<string[]>("ImageExtensions") ?? [".png", ".jpg", ".jpeg"];

        foreach (var ext in imageExtensions)
        {
            var systemImagePath = Path.Combine(systemImageFolder, $"{systemName}{ext}");
            if (File.Exists(systemImagePath)) return Task.FromResult(systemImagePath);
        }

        var enableAnnotationStripping = _settings.EnableAnnotationStripping;

        // Normalized exact match (strip annotations)
        if (enableAnnotationStripping)
        {
            var strippedSystemName = FindCoverImageService.StripAnnotations(systemName);
            if (!string.Equals(strippedSystemName, systemName, StringComparison.Ordinal))
            {
                // Try exact match with stripped name
                foreach (var ext in imageExtensions)
                {
                    var systemImagePath = Path.Combine(systemImageFolder, $"{strippedSystemName}{ext}");
                    if (File.Exists(systemImagePath)) return Task.FromResult(systemImagePath);
                }

                // Try stripping annotations from image filenames too
                if (Directory.Exists(systemImageFolder))
                {
                    foreach (var fileInFolder in Directory.GetFiles(systemImageFolder)
                                 .Where(f => imageExtensions.Any(ext =>
                                     f.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
                    {
                        var fileWithoutExt = Path.GetFileNameWithoutExtension(fileInFolder);
                        if (string.IsNullOrEmpty(fileWithoutExt)) continue;

                        if (string.Equals(strippedSystemName, FindCoverImageService.StripAnnotations(fileWithoutExt),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return Task.FromResult(fileInFolder);
                        }
                    }
                }
            }
        }

        var enableFuzzyMatching = _settings.EnableFuzzyMatching;
        var similarityThreshold = _settings.FuzzyMatchingThreshold;

        if (enableFuzzyMatching && Directory.Exists(systemImageFolder))
        {
            var filesInImageFolder = Directory.GetFiles(systemImageFolder)
                .Where(f => imageExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();

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

            if (bestMatchPath != null && highestSimilarity >= similarityThreshold)
                return Task.FromResult(bestMatchPath);
        }

        var defaultImagePath = Path.Combine(systemImageFolder, "default.png");
        return Task.FromResult(File.Exists(defaultImagePath)
            ? defaultImagePath
            : Path.Combine(appBaseDir, "images", "default.png"));
    }
}