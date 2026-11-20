using System.Text.RegularExpressions;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Services.GameFilter;

/// <summary>
///     Encapsulates game-list filtering and sorting logic (letter filter, MAME sort,
///     search query, show-games visibility). Extracted from MainViewModel for testability
///     and reuse, mirroring the WPF GameFilterService.
/// </summary>
public partial class AvaloniaGameFilterService
{
    private readonly IMameDataService _mameData;
    private readonly SettingsManagerService _settings;

    public AvaloniaGameFilterService(
        SettingsManagerService settings,
        IMameDataService mameData)
    {
        _settings = settings;
        _mameData = mameData;
    }

    /// <summary>
    ///     Filters the game list by the Show Games setting (ShowAll / ShowWithCover / ShowWithoutCover).
    ///     A game "has a cover" only when its resolved cover is a real image: the resolver falls back
    ///     to images\default.png (which exists), so checking the file alone would mark every game as
    ///     covered and make "Show Only Games Without Cover" always empty (WPF GameFilterService parity).
    /// </summary>
    public List<GameCardViewModel> FilterByShowGamesSetting(List<GameCardViewModel> games)
    {
        var showGamesMode = _settings.ShowGames;
        if (showGamesMode is "ShowAll" || games.Count == 0)
            return games;

        return games.Where(g => string.Equals(showGamesMode, "ShowWithCover", StringComparison.Ordinal)
            ? HasRealCover(g)
            : !HasRealCover(g)).ToList();
    }

    private static bool HasRealCover(GameCardViewModel game)
    {
        return !string.IsNullOrEmpty(game.CoverPath)
               && File.Exists(game.CoverPath)
               && !game.CoverPath.EndsWith("default.png", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Filters the game list by a starting letter. "#" matches files starting with a digit.
    ///     Empty or null letter returns the list unfiltered.
    /// </summary>
    public static List<GameCardViewModel> FilterByLetter(List<GameCardViewModel> games, string letter)
    {
        if (string.IsNullOrEmpty(letter))
            return games;

        if (string.Equals(letter, "#", StringComparison.Ordinal))
        {
            return games.Where(game =>
            {
                var fileName = Path.GetFileName(game.FilePath);
                return !string.IsNullOrEmpty(fileName) && char.IsDigit(fileName[0]);
            }).ToList();
        }

        return games.Where(game =>
        {
            var fileName = Path.GetFileName(game.FilePath);
            return !string.IsNullOrEmpty(fileName) &&
                   fileName.StartsWith(letter, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    /// <summary>
    ///     Sorts the game list by MAME machine description or file name.
    /// </summary>
    public List<GameCardViewModel> SortByMameOrder(List<GameCardViewModel> games, string mameSortOrder)
    {
        if (string.Equals(mameSortOrder, "MachineDescription", StringComparison.Ordinal))
        {
            return games.OrderBy(game =>
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(game.FilePath);
                return _mameData.Lookup.TryGetValue(fileNameWithoutExtension, out var description) &&
                       !string.IsNullOrWhiteSpace(description)
                    ? description
                    : fileNameWithoutExtension;
            }, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return games.OrderBy(game => Path.GetFileName(game.FilePath), StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    ///     Filters the game list by a search query applied to the file name, the visible
    ///     display title and the MAME machine description. Mirrors the WPF
    ///     GameFilterService: multiple terms are ANDed by default, "OR" matches any term,
    ///     and quoted phrases stay together. Searching the raw file name keeps the filter
    ///     working when the Filename Display Mode is "NoFilename" (DisplayTitle is empty).
    /// </summary>
    public static List<GameCardViewModel> FilterBySearchQuery(List<GameCardViewModel> games, string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return games;

        var searchTerms = ParseSearchTerms(searchQuery);
        if (searchTerms.Count == 0)
            return games;

        return games.Where(game =>
        {
            var searchText = $"{game.FileName} {game.DisplayTitle} {game.MachineDescription}";
            return MatchesSearchQuery(searchText, searchTerms);
        }).ToList();
    }

    private static List<string> ParseSearchTerms(string searchTerm)
    {
        var terms = new List<string>();
        foreach (Match match in SearchTermsRegex().Matches(searchTerm))
            terms.Add(match.Value.Trim('"').ToLowerInvariant());

        return terms.Where(static term => !string.IsNullOrWhiteSpace(term)).ToList();
    }

    private static bool MatchesSearchQuery(string text, IReadOnlyCollection<string> searchTerms)
    {
        var keywords = searchTerms
            .Where(static term => !term.Equals("and", StringComparison.OrdinalIgnoreCase) &&
                                  !term.Equals("or", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (keywords.Count == 0) return true;

        var hasAndOperator = searchTerms.Any(static term => term.Equals("and", StringComparison.OrdinalIgnoreCase));
        var hasOrOperator = searchTerms.Any(static term => term.Equals("or", StringComparison.OrdinalIgnoreCase));

        if (hasAndOperator) return keywords.All(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (hasOrOperator) return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return keywords.All(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("""[\"](.+?)[\"]|([^ ]+)""", RegexOptions.None | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex SearchTermsRegex();
}
