using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SimpleLauncher.Core.Services.CoverCatalog;
using SkiaSharp;

namespace CoverCatalogBuilder;

/// <summary>Builds the canonical cover catalog and its JPEG derivatives.</summary>
internal sealed class CatalogBuilder
{
    private const string CatalogFileName = "catalog.json";
    private const string IndexFileName = "index.json";
    private const string ReportFileName = "report.md";

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    private static readonly JsonSerializerOptions JsonCompact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions JsonIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly BuilderOptions _options;

    public CatalogBuilder(BuilderOptions options)
    {
        _options = options;
    }

    /// <summary>Runs the build.</summary>
    /// <returns>0 on success, 1 when a system failed.</returns>
    public int Run()
    {
        try
        {
            Directory.CreateDirectory(_options.OutputDirectory);

            var systems = Directory.EnumerateDirectories(_options.SourceDirectory)
                .Select(static path => Path.GetFileName(path))
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Where(static name => !string.Equals(name, "systems", StringComparison.OrdinalIgnoreCase))
                .Where(name => !_options.ExcludedSystems.Contains(name, StringComparer.OrdinalIgnoreCase))
                .Where(name => _options.Systems.Count == 0 ||
                               _options.Systems.Contains(name, StringComparer.OrdinalIgnoreCase))
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (systems.Count == 0)
            {
                Console.Error.WriteLine("No matching system folders were found.");
                return 1;
            }

            var plans = CreateSystemPlans(systems);
            var results = new List<SystemBuildResult>(plans.Count);
            foreach (var plan in plans)
            {
                var result = ProcessSystem(plan);
                results.Add(result);
                PrintSystemResult(result);
            }

            WriteCatalog(results);
            WriteReport(results);
            PrintSummary(results);
            return results.Any(static result => result.FatalError != null) ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private List<SystemPlan> CreateSystemPlans(IEnumerable<string> systems)
    {
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plans = new List<SystemPlan>();

        foreach (var system in systems)
        {
            var baseSlug = CoverCatalogPlanner.CreateSystemSlug(system);
            var slug = baseSlug;
            var suffix = 2;
            while (!usedSlugs.Add(slug)) slug = $"{baseSlug}-{suffix++}";

            plans.Add(new SystemPlan(system, slug));
        }

        return plans;
    }

    private SystemBuildResult ProcessSystem(SystemPlan plan)
    {
        var stopwatch = Stopwatch.StartNew();
        var systemDirectory = Path.Combine(_options.SourceDirectory, plan.Name);
        var outputDirectory = Path.Combine(_options.OutputDirectory, plan.Slug);
        Directory.CreateDirectory(outputDirectory);

        var candidates = new List<CoverCatalogCandidate>();
        var unsupported = 0;
        var unreadable = 0;

        foreach (var file in Directory.EnumerateFiles(systemDirectory, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (!SupportedExtensions.Contains(extension))
            {
                unsupported++;
                continue;
            }

            var baseName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(baseName) ||
                string.Equals(baseName, "default", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryReadDimensions(file, out var width, out var height))
            {
                unreadable++;
                continue;
            }

            var length = new FileInfo(file).Length;
            candidates.Add(new CoverCatalogCandidate(baseName, extension, length, width, height, file));
        }

        if (candidates.Count == 0)
        {
            return new SystemBuildResult(plan.Name, plan.Slug, 0, 0, 0, 0, unsupported, unreadable,
                stopwatch.Elapsed, null);
        }

        var groups = CoverCatalogPlanner.GroupByName(candidates);
        if (_options.Limit > 0 && groups.Count > _options.Limit)
            groups = groups.Take(_options.Limit).ToList();

        var entries = new ConcurrentBag<CoverCatalogEntry>();
        var errors = new ConcurrentBag<string>();
        var usedFileNames = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var completed = 0;
        var written = 0;
        var skipped = 0;
        long bytesWritten = 0;

        try
        {
            Parallel.ForEach(groups,
                new ParallelOptions { MaxDegreeOfParallelism = _options.Parallelism },
                group =>
                {
                    try
                    {
                        var fileName = CreateOutputFileName(group.Canonical.BaseName, usedFileNames);
                        var destination = Path.Combine(outputDirectory, fileName);
                        var entry = new CoverCatalogEntry
                        {
                            File = fileName,
                            Names = [.. group.Aliases]
                        };

                        if (_options.SkipExisting && File.Exists(destination) && new FileInfo(destination).Length > 0)
                        {
                            if (TryReadDimensions(destination, out var existingWidth, out var existingHeight))
                            {
                                entry.Width = existingWidth;
                                entry.Height = existingHeight;
                            }

                            entry.Size = new FileInfo(destination).Length;
                            Interlocked.Increment(ref skipped);
                        }
                        else
                        {
                            var (width, height) = EncodeCover(group.Canonical.SourcePath, destination,
                                _options.MaxSize, _options.Quality);
                            entry.Width = width;
                            entry.Height = height;
                            entry.Size = new FileInfo(destination).Length;
                            Interlocked.Increment(ref written);
                            Interlocked.Add(ref bytesWritten, entry.Size);
                        }

                        entries.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{group.Canonical.BaseName}: {ex.Message}");
                    }

                    var done = Interlocked.Increment(ref completed);
                    if (done % 1000 == 0)
                        Console.WriteLine($"  [{plan.Name}] {done}/{groups.Count}");
                });
        }
        catch (Exception ex)
        {
            return new SystemBuildResult(plan.Name, plan.Slug, candidates.Count, groups.Count,
                candidates.Count - groups.Count, written, unsupported, unreadable, stopwatch.Elapsed, ex.Message)
            {
                Skipped = skipped,
                BytesWritten = bytesWritten
            };
        }

        var index = new CoverCatalogIndex
        {
            System = plan.Name,
            Slug = plan.Slug,
            Images = entries
                .OrderBy(static entry => entry.File, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        WriteJson(Path.Combine(outputDirectory, IndexFileName), index, JsonCompact);

        var staleRemoved = 0;
        if (_options.Clean)
        {
            var expected = new HashSet<string>(
                index.Images.Select(static entry => entry.File), StringComparer.OrdinalIgnoreCase);
            foreach (var existing in Directory.EnumerateFiles(outputDirectory, "*.jpg",
                         SearchOption.TopDirectoryOnly))
            {
                if (expected.Contains(Path.GetFileName(existing))) continue;
                File.Delete(existing);
                staleRemoved++;
            }
        }

        return new SystemBuildResult(
            plan.Name,
            plan.Slug,
            candidates.Count,
            groups.Count,
            candidates.Count - groups.Count,
            written,
            unsupported,
            unreadable,
            stopwatch.Elapsed,
            errors.IsEmpty ? null : string.Join("; ", errors.Take(5)))
        {
            Skipped = skipped,
            BytesWritten = bytesWritten,
            RemovedStale = staleRemoved
        };
    }

    private void WriteCatalog(IReadOnlyList<SystemBuildResult> results)
    {
        var catalog = new CoverCatalogManifest
        {
            BaseUrl = _options.BaseUrl.TrimEnd('/'),
            GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Systems = results
                .Where(static result => result.FatalError == null && result.Groups > 0)
                .OrderBy(static result => result.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static result => new CoverCatalogSystem
                {
                    Name = result.Name,
                    Slug = result.Slug,
                    Count = result.Groups,
                    IndexFile = $"{result.Slug}/{IndexFileName}"
                })
                .ToList()
        };

        WriteJson(Path.Combine(_options.OutputDirectory, CatalogFileName), catalog, JsonIndented);
    }

    private void WriteReport(IReadOnlyList<SystemBuildResult> results)
    {
        var reportPath = Path.Combine(_options.OutputDirectory, ReportFileName);
        var builder = new StringBuilder();
        builder.AppendLine("# Cover catalog build report");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Generated: {DateTime.UtcNow:O}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Source: `{_options.SourceDirectory}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Output: `{_options.OutputDirectory}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Public base: `{_options.BaseUrl.TrimEnd('/')}`");
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"- Derivative: max {_options.MaxSize}px, JPEG q{_options.Quality}");
        builder.AppendLine();

        var sourceImages = results.Sum(static result => result.SourceFiles);
        var groups = results.Sum(static result => result.Groups);
        var merged = results.Sum(static result => result.Merged);
        var written = results.Sum(static result => result.Written);
        var skipped = results.Sum(static result => result.Skipped);
        var stale = results.Sum(static result => result.RemovedStale);
        var errors = results.Count(static result => result.FatalError != null);
        var bytes = results.Sum(static result => result.BytesWritten);

        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Systems: {results.Count}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Source cover images: {sourceImages:N0}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Canonical covers: {groups:N0}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Variant images merged: {merged:N0}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Derivatives written: {written:N0}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Existing derivatives skipped: {skipped:N0}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Stale derivatives removed: {stale:N0}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Systems with errors: {errors:N0}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Derivative size: {bytes / (1024d * 1024d):N1} MiB");
        if (merged > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"- Estimated originals avoided: {merged:N0} files");
        }

        builder.AppendLine();
        builder.AppendLine("## Systems");
        builder.AppendLine();
        builder.AppendLine("| System | Slug | Source | Covers | Merged | Written | Skipped | Removed | Unsupported | Unreadable | MiB |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var result in results.OrderByDescending(static result => result.SourceFiles))
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"| {result.Name} | {result.Slug} | {result.SourceFiles:N0} | {result.Groups:N0} | {result.Merged:N0} | {result.Written:N0} | {result.Skipped:N0} | {result.RemovedStale:N0} | {result.Unsupported} | {result.Unreadable} | {result.BytesWritten / (1024d * 1024d):N1} |");
            if (!string.IsNullOrEmpty(result.FatalError))
                builder.AppendLine(CultureInfo.InvariantCulture, $"| ↳ error | {result.FatalError} | | | | | | | | | |");
        }

        builder.AppendLine();
        builder.AppendLine("Archives and other non-image files are counted as unsupported and are not uploaded.");
        builder.AppendLine("`Microsoft Windows` is excluded by default because store covers are already served per-game from the bucket root.");

        WriteText(reportPath, builder.ToString());
    }

    private void PrintSummary(IReadOnlyList<SystemBuildResult> results)
    {
        var groups = results.Sum(static result => result.Groups);
        var written = results.Sum(static result => result.Written);
        var bytes = results.Sum(static result => result.BytesWritten);
        Console.WriteLine();
        Console.WriteLine($"Catalog: {Path.Combine(_options.OutputDirectory, CatalogFileName)}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Systems: {results.Count}, covers: {groups:N0}, written: {written:N0}, size: {bytes / (1024d * 1024d * 1024d):N2} GiB"));
        Console.WriteLine($"Public base: {_options.BaseUrl.TrimEnd('/')}");
        if (results.Any(static result => result.FatalError != null))
            Console.WriteLine("Some systems had errors; see report.md.");
    }

    private static void PrintSystemResult(SystemBuildResult result)
    {
        var status = result.FatalError == null ? "ok" : "error";
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[{status}] {result.Name}: {result.SourceFiles:N0} images -> {result.Groups:N0} covers ({result.Merged:N0} merged), {result.Written:N0} written, {result.Skipped:N0} skipped, {result.Duration.TotalSeconds:N1}s"));
        if (result.FatalError != null)
            Console.WriteLine($"  {result.FatalError}");
    }

    private static string CreateOutputFileName(string baseName, ConcurrentDictionary<string, byte> usedFileNames)
    {
        var safeName = SanitizeFileName(baseName);
        if (safeName.Length == 0) safeName = "cover";
        if (safeName.Length > 180)
            safeName = string.Concat(safeName.AsSpan(0, 160), "-", ShortHash(baseName));

        var candidate = $"{safeName}.jpg";
        var suffix = 2;
        while (!usedFileNames.TryAdd(candidate, 0)) candidate = $"{safeName}-{suffix++}.jpg";
        return candidate;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
            builder.Append(invalid.Contains(character) || char.IsControl(character) ? '_' : character);

        return builder.ToString().Trim().TrimEnd('.', ' ');
    }

    private static string ShortHash(string value)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    private static (int Width, int Height) EncodeCover(string sourcePath, string destination, int maxSize, int quality)
    {
        using var source = SKBitmap.Decode(sourcePath) ??
                           throw new InvalidOperationException("The image could not be decoded.");
        if (source.Width <= 0 || source.Height <= 0)
            throw new InvalidOperationException("The image has invalid dimensions.");

        var scale = Math.Min(1d, maxSize / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale, MidpointRounding.AwayFromZero));

        using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        }

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality) ??
                         throw new InvalidOperationException("The image could not be encoded.");

        var tempPath = $"{destination}.tmp-{Guid.NewGuid():N}";
        try
        {
            using (var stream = File.Create(tempPath))
            using (var dataStream = data.AsStream())
            {
                dataStream.CopyTo(stream);
            }

            File.Move(tempPath, destination, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        return (width, height);
    }

    private static bool TryReadDimensions(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream);
            if (codec is null) return false;
            width = codec.Info.Width;
            height = codec.Info.Height;
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteJson<T>(string path, T value, JsonSerializerOptions options)
    {
        WriteText(path, JsonSerializer.Serialize(value, options));
    }

    private static void WriteText(string path, string content)
    {
        var tempPath = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(tempPath, content, new UTF8Encoding(false));
            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private sealed record SystemPlan(string Name, string Slug);
}
