using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.FindCoverImage;

namespace SimpleLauncher.Core.Services.CoverCatalog;

/// <summary>
///     Pure planning helpers shared by the cover-catalog build tool and the app. The
///     normalization intentionally reuses the same annotation stripping and fuzzy matching
///     primitives as the local cover lookup, so a single uploaded image can satisfy every
///     region/revision/crack variant of a game.
/// </summary>
public static class CoverCatalogPlanner
{
    /// <summary>
    ///     Normalizes a game name for grouping: the same annotation stripping used by the
    ///     local cover matcher, trimmed. The returned value is not lower-cased so callers can
    ///     display it; use an <see cref="StringComparer.OrdinalIgnoreCase" /> comparer when
    ///     grouping.
    /// </summary>
    /// <param name="name">The ROM or image base name.</param>
    /// <returns>The annotation-stripped name, or the trimmed original when stripping would empty it.</returns>
    public static string NormalizeGameName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        var stripped = FindCoverImageService.StripAnnotations(name).Trim();
        return stripped.Length == 0 ? name.Trim() : stripped;
    }

    /// <summary>
    ///     Creates the URL-safe folder name used for a system. Accents are decomposed and
    ///     removed, letters/digits are lower-cased, and every other run of characters becomes
    ///     a single hyphen.
    /// </summary>
    /// <param name="systemName">The Simple Launcher system name.</param>
    /// <returns>A non-empty slug such as <c>nintendo-nes</c>.</returns>
    public static string CreateSystemSlug(string? systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName)) return "system";

        var decomposed = systemName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "system" : slug;
    }

    /// <summary>
    ///     Groups candidate images by their annotation-stripped game name and selects one
    ///     canonical image per group. All original base names are kept as aliases, so the
    ///     client can satisfy variants without extra uploaded files.
    /// </summary>
    /// <param name="candidates">The source images of one system.</param>
    /// <returns>The canonical groups, sorted by canonical base name.</returns>
    public static IReadOnlyList<CoverCatalogGroup> GroupByName(IEnumerable<CoverCatalogCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var groups = new Dictionary<string, List<CoverCatalogCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var key = NormalizeGameName(candidate.BaseName);
            if (key.Length == 0) key = candidate.BaseName.Trim();

            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups.Add(key, list);
            }

            list.Add(candidate);
        }

        var result = new List<CoverCatalogGroup>(groups.Count);
        foreach (var group in groups)
        {
            var aliases = group.Value
                .Select(static candidate => candidate.BaseName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Add(new CoverCatalogGroup(SelectCanonical(group.Value), aliases));
        }

        result.Sort(static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.Canonical.BaseName, right.Canonical.BaseName));
        return result;
    }

    /// <summary>
    ///     Selects the best representative of a group: box art (portrait) first, then the
    ///     largest image, then PNG, then the unannotated name and the largest/shortest file.
    /// </summary>
    /// <param name="candidates">The candidates in one normalized group (at least one).</param>
    /// <returns>The canonical candidate.</returns>
    public static CoverCatalogCandidate SelectCanonical(IReadOnlyList<CoverCatalogCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
            throw new ArgumentException("At least one cover candidate is required.", nameof(candidates));

        return candidates
            // The collection mixes box art (portrait) with in-game snapshots (landscape).
            // A portrait image is the better cover even when an annotated snapshot has a
            // slightly larger pixel area (for example 439x600 box art vs 799x600 title art).
            .OrderByDescending(static candidate => candidate.Height > candidate.Width ? 1 : 0)
            .ThenByDescending(static candidate => (long)candidate.Width * candidate.Height)
            .ThenBy(static candidate =>
                string.Equals(candidate.Extension, ".png", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(static candidate => IsUnannotated(candidate) ? 0 : 1)
            .ThenByDescending(static candidate => candidate.Size)
            .ThenBy(static candidate => candidate.BaseName.Length)
            .ThenBy(static candidate => candidate.BaseName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static bool IsUnannotated(CoverCatalogCandidate candidate)
    {
        return string.Equals(candidate.BaseName.Trim(), NormalizeGameName(candidate.BaseName),
            StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>One source image considered for the cover catalog.</summary>
/// <param name="BaseName">The file name without its extension (the game name).</param>
/// <param name="Extension">The source extension, including the dot.</param>
/// <param name="Size">The source file size in bytes.</param>
/// <param name="Width">The source image width in pixels.</param>
/// <param name="Height">The source image height in pixels.</param>
/// <param name="SourcePath">The full source path (used only by the build tool).</param>
public sealed record CoverCatalogCandidate(string BaseName, string Extension, long Size, int Width, int Height,
    string SourcePath = "");

/// <summary>One canonical cover and the filename variants it replaces.</summary>
/// <param name="Canonical">The selected source image.</param>
/// <param name="Aliases">Every distinct original base name in the group.</param>
public sealed record CoverCatalogGroup(CoverCatalogCandidate Canonical, IReadOnlyList<string> Aliases);
