using System.Text.RegularExpressions;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.SystemManager;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameFilter;

/// <summary>
///     Provides game file filtering and sorting operations including letter filtering,
///     search query matching, cover image presence filtering, and MAME description sorting.
/// </summary>
public partial class GameFilterService : IGameFilterService
{
    private readonly IFindCoverImageService _findCoverImage;
    private readonly SettingsManagerService _settings;

    /// <summary>
    ///     Initializes a new instance of <see cref="GameFilterService" />.
    /// </summary>
    public GameFilterService(IFindCoverImageService findCoverImage, SettingsManagerService settings)
    {
        _findCoverImage = findCoverImage;
        _settings = settings;
    }

    /// <summary>
    ///     Filters the game file list based on the "ShowGames" setting, keeping only games
    ///     with or without cover images depending on the configured mode.
    /// </summary>
    public Task<IList<string>> FilterByShowGamesSettingAsync(
        IList<string> files, string selectedSystem, SystemManagerService config)
    {
        if (files.Count == 0 || string.Equals(_settings.ShowGames, "ShowAll", StringComparison.Ordinal))
            return Task.FromResult(files);

        IList<string> filteredFiles = new List<string>();

        foreach (var filePath in files)
        {
            var fileNameWithoutExtension = PathHelper.GetFileNameWithoutExtension(filePath);
            var imagePath =
                _findCoverImage.FindCoverImagePath(fileNameWithoutExtension, selectedSystem, config.SystemImageFolder);

            bool isDefaultImage;
            if (string.IsNullOrEmpty(imagePath) ||
                imagePath.EndsWith("default.png", StringComparison.OrdinalIgnoreCase))
            {
                isDefaultImage = true;
            }
            else
            {
                var resolvedImagePath = PathHelper.ResolveRelativeToAppDirectory(imagePath);
                isDefaultImage = string.IsNullOrEmpty(resolvedImagePath) ||
                                 !File.Exists(resolvedImagePath) ||
                                 resolvedImagePath.EndsWith("default.png", StringComparison.OrdinalIgnoreCase);
            }

            switch (_settings.ShowGames)
            {
                case "ShowWithCover" when !isDefaultImage:
                case "ShowWithoutCover" when isDefaultImage:
                    filteredFiles.Add(filePath);
                    break;
            }
        }

        return Task.FromResult(filteredFiles);
    }

    /// <summary>
    ///     Filters game files whose names start with the specified letter, or digits if "#" is provided.
    /// </summary>
    public Task<IList<string>> FilterByLetterAsync(IList<string> files, string startLetter)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(startLetter))
                return files;

            if (string.Equals(startLetter, "#", StringComparison.Ordinal))
            {
                // Guard the file NAME length, not the path length: "C:\roms\" has a
                // non-empty path but an empty file name, and [0] would throw (WPF-14).
                return files.Where(static file =>
                {
                    if (string.IsNullOrEmpty(file)) return false;

                    var name = Path.GetFileName(file);
                    return name.Length > 0 && char.IsDigit(name[0]);
                }).ToList();
            }

            return files.Where(file => !string.IsNullOrEmpty(file) &&
                                       Path.GetFileName(file)
                                           .StartsWith(startLetter, StringComparison.OrdinalIgnoreCase)).ToList();
        });
    }

    /// <summary>
    ///     Sorts the game file list by MAME machine description or by filename, depending on the sort order setting.
    /// </summary>
    public IList<string> SortByMameDescription(
        IList<string> files, string mameSortOrder, IDictionary<string, string> mameLookup)
    {
        if (string.Equals(mameSortOrder, AppConstants.MameSortOrderMachineDescription, StringComparison.Ordinal))
        {
            return files.OrderBy(f =>
            {
                var fileName = Path.GetFileNameWithoutExtension(f);
                return mameLookup.TryGetValue(fileName, out var description) && !string.IsNullOrWhiteSpace(description)
                    ? description
                    : fileName;
            }, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return files.OrderBy(static f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    ///     Filters game files whose filename or MAME description matches the search query (case-insensitive).
    ///     Supports logical operators AND, OR, and quoted phrases.
    ///     Examples: "mario kart" (AND by default), "mario AND kart", "mario OR kart", "\"super mario\""
    /// </summary>
    public Task<IList<string>> FilterBySearchQueryAsync(
        IList<string> files, string searchQuery, IDictionary<string, string> mameLookup)
    {
        var searchTerms = ParseSearchTerms(searchQuery);
        if (searchTerms.Count == 0)
            return Task.FromResult(files);

        return Task.Run<IList<string>>(() =>
            files.Where(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                var searchText = fileName;
                if (mameLookup != null && mameLookup.TryGetValue(fileName, out var description)
                                       && !string.IsNullOrWhiteSpace(description))
                {
                    searchText = $"{fileName} {description}";
                }

                return MatchesSearchQuery(searchText, searchTerms);
            }).ToList());
    }

    private static List<string> ParseSearchTerms(string searchTerm)
    {
        var terms = new List<string>();
        var matches = SearchTermsRegex().Matches(searchTerm);
        foreach (Match match in matches) terms.Add(match.Value.Trim('"').ToLowerInvariant());

        return terms.Where(static t => !string.IsNullOrWhiteSpace(t)).ToList();
    }

    private static bool MatchesSearchQuery(string text, IReadOnlyCollection<string> searchTerms)
    {
        if (text == null) return false;

        var keywords = searchTerms
            .Where(static t => !t.Equals("and", StringComparison.OrdinalIgnoreCase) &&
                               !t.Equals("or", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (keywords.Count == 0) return true;

        var hasAndOperator = searchTerms.Any(static t => t.Equals("and", StringComparison.OrdinalIgnoreCase));
        var hasOrOperator = searchTerms.Any(static t => t.Equals("or", StringComparison.OrdinalIgnoreCase));

        if (hasAndOperator) return keywords.All(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        if (hasOrOperator) return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return keywords.All(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("""[\"](.+?)[\"]|([^ ]+)""", RegexOptions.None | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex SearchTermsRegex();
}