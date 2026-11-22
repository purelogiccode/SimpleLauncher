using System.Globalization;
using System.Text;
using SimpleLauncher.Core.Services.FindCoverImage;

namespace SimpleLauncher.Core.Services.CoverCatalog;

/// <summary>
///     Indexed matcher for cover names. It resolves the exact name, the annotation-stripped
///     name, a normalized token order (articles removed) and finally a bounded
///     Jaro-Winkler fuzzy match. The same matcher is used for the downloaded catalog and
///     for the local image folder, so "already present" and "available online" agree.
/// </summary>
public sealed class CoverCatalogLookup
{
    private static readonly HashSet<string> Articles =
        new(StringComparer.OrdinalIgnoreCase) { "the", "a", "an" };

    private readonly List<FuzzyCandidate> _allCandidates = [];
    private readonly Dictionary<string, CoverCatalogEntry> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<char, List<FuzzyCandidate>> _fuzzyByFirst = [];
    private readonly Dictionary<string, CoverCatalogEntry> _matchKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CoverCatalogEntry> _normalized = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _stripAnnotations;

    /// <summary>Initializes a lookup over the given entries.</summary>
    /// <param name="entries">Catalog entries or synthetic local entries.</param>
    /// <param name="stripAnnotations">
    ///     True to apply <see cref="CoverCatalogPlanner.NormalizeGameName" /> (the app's default);
    ///     false to only trim names.
    /// </param>
    public CoverCatalogLookup(IReadOnlyList<CoverCatalogEntry> entries, bool stripAnnotations = true)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _stripAnnotations = stripAnnotations;

        foreach (var entry in entries)
        {
            var names = entry.Names.Count > 0
                ? entry.Names
                : [Path.GetFileNameWithoutExtension(entry.File)];

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                AddName(name, entry);
            }
        }
    }

    /// <summary>
    ///     Finds the best entry for a ROM base name.
    /// </summary>
    /// <param name="romBaseName">The ROM file name without its extension.</param>
    /// <param name="fuzzyThreshold">
    ///     0 to disable fuzzy matching; otherwise the minimum Jaro-Winkler similarity (normally
    ///     <c>SettingsManagerService.FuzzyMatchingThreshold</c>).
    /// </param>
    /// <returns>The matching entry, or null.</returns>
    public CoverCatalogEntry? FindBestMatch(string? romBaseName, double fuzzyThreshold)
    {
        if (string.IsNullOrWhiteSpace(romBaseName)) return null;

        var raw = romBaseName.Trim();
        if (_exact.TryGetValue(raw, out var exact)) return exact;

        var normalized = Normalize(raw);
        if (normalized.Length == 0) return null;

        if (_normalized.TryGetValue(normalized, out var normalizedEntry)) return normalizedEntry;

        var matchKey = BuildMatchKey(normalized);
        if (matchKey.Length > 0 && _matchKeys.TryGetValue(matchKey, out var keyEntry)) return keyEntry;

        if (fuzzyThreshold <= 0) return null;

        var first = GetBlockingCharacter(normalized);
        if (!_fuzzyByFirst.TryGetValue(first, out var candidates))
        {
            // Small systems can safely compare against every alias; large ones stay blocked
            // to keep a batch of missing covers fast.
            if (_allCandidates.Count > 2000) return null;
            candidates = _allCandidates;
        }

        var lowerRom = normalized.ToLowerInvariant();
        var bestScore = 0d;
        CoverCatalogEntry? bestEntry = null;

        foreach (var candidate in candidates)
        {
            // Avoid comparing names whose lengths are wildly different.
            var lengthDelta = Math.Abs(candidate.Normalized.Length - lowerRom.Length);
            if (lengthDelta > Math.Max(8, lowerRom.Length / 2)) continue;

            var score = FindCoverImageService.CalculateJaroWinklerSimilarity(lowerRom, candidate.Normalized);
            if (score <= bestScore) continue;
            bestScore = score;
            bestEntry = candidate.Entry;
        }

        return bestScore >= fuzzyThreshold ? bestEntry : null;
    }

    private void AddName(string name, CoverCatalogEntry entry)
    {
        var raw = name.Trim();
        if (raw.Length == 0) return;

        _exact.TryAdd(raw, entry);

        var normalized = Normalize(raw);
        if (normalized.Length == 0) return;
        _normalized.TryAdd(normalized, entry);

        var matchKey = BuildMatchKey(normalized);
        if (matchKey.Length > 0) _matchKeys.TryAdd(matchKey, entry);

        var candidate = new FuzzyCandidate(normalized.ToLowerInvariant(), entry);
        _allCandidates.Add(candidate);
        var first = GetBlockingCharacter(normalized);
        if (!_fuzzyByFirst.TryGetValue(first, out var list))
        {
            list = [];
            _fuzzyByFirst.Add(first, list);
        }

        list.Add(candidate);
    }

    private string Normalize(string name)
    {
        return _stripAnnotations
            ? CoverCatalogPlanner.NormalizeGameName(name)
            : name.Trim();
    }

    private static string BuildMatchKey(string normalizedName)
    {
        var builder = new StringBuilder(normalizedName.Length);
        foreach (var character in normalizedName.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
            else
                builder.Append(' ');
        }

        var tokens = builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(static token => !Articles.Contains(token))
            .OrderBy(static token => token, StringComparer.Ordinal)
            .ToList();

        return string.Join(' ', tokens);
    }

    private static char GetBlockingCharacter(string normalizedName)
    {
        foreach (var character in normalizedName)
        {
            if (char.IsLetter(character)) return char.ToLowerInvariant(character);
            if (char.IsDigit(character)) return '#';
        }

        return '#';
    }

    private sealed record FuzzyCandidate(string Normalized, CoverCatalogEntry Entry);
}
