using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.PlaySound;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the Global Search section of the main window, providing
///     cross-system ROM search with logical operators and scoring (WPF GlobalSearchPage equivalent).
/// </summary>
public partial class GlobalSearchSectionViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly IGetListOfFilesService _getListOfFiles;
    private readonly Services.LocalizationService _localization;
    private readonly ILogger _logErrors;
    private readonly MainViewModel _mainViewModel;
    private readonly IMameDataService _mameData;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly SystemManagerService _systemManagerService;
    private CancellationTokenSource _cancellationTokenSource = new();

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool LaunchButtonEnabled { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "";

    [ObservableProperty] public partial bool NoResultsVisible { get; set; }

    [ObservableProperty] public partial string ResultsCountText { get; set; } = "";

    [ObservableProperty] public partial bool SearchFilename { get; set; } = true;

    [ObservableProperty] public partial bool SearchFolderName { get; set; }

    [ObservableProperty] public partial bool SearchMameDescription { get; set; } = true;

    [ObservableProperty] public partial bool SearchRecursively { get; set; }

    [ObservableProperty] public partial ObservableCollection<SearchResult> SearchResults { get; set; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = "";

    [ObservableProperty] public partial SearchResult? SelectedResult { get; set; }

    [ObservableProperty] public partial int SelectedSystemIndex { get; set; }

    [ObservableProperty] public partial List<string> SystemNames { get; set; } = [];

    public GlobalSearchSectionViewModel(
        SystemManagerService systemManagerService,
        IGetListOfFilesService getListOfFiles,
        IFindCoverImageService findCoverImage,
        IMameDataService mameData,
        PlaySoundEffects playSoundEffects,
        IMessageBoxLibraryService messageBox,
        MainViewModel mainViewModel,
        ILogger logErrors,
        Services.LocalizationService localization,
        IConfiguration configuration)
    {
        _systemManagerService = systemManagerService;
        _getListOfFiles = getListOfFiles;
        _findCoverImage = findCoverImage;
        _mameData = mameData;
        _playSoundEffects = playSoundEffects;
        _messageBox = messageBox;
        _mainViewModel = mainViewModel;
        _logErrors = logErrors;
        _localization = localization;
        _configuration = configuration;

        RefreshSystemNames();
    }

    /// <summary>
    ///     Rebuilds the system dropdown from the current system configuration while keeping
    ///     the user's selection when that system still exists. Called when the Global Search
    ///     section is shown and after system configuration changes, because this ViewModel
    ///     is a singleton and would otherwise keep a stale list until restart.
    /// </summary>
    public void RefreshSystemNames()
    {
        var previous = SelectedSystemIndex >= 0 && SelectedSystemIndex < SystemNames.Count
            ? SystemNames[SelectedSystemIndex]
            : null;

        var names = new List<string> { "All Systems" };
        names.AddRange(_systemManagerService.LoadSystems()
            .Select(static s => s.SystemName)
            .OrderBy(static name => name, StringComparer.Ordinal));

        SystemNames = names;
        SelectedSystemIndex = previous is null
            ? 0
            : Math.Max(0, names.FindIndex(name => string.Equals(name, previous, StringComparison.OrdinalIgnoreCase)));
    }

    partial void OnSelectedResultChanged(SearchResult? value)
    {
        LaunchButtonEnabled = value is not null;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            // Capture the token source in a local: the field is replaced by the next
            // search, and the finally block must inspect THIS search's source.
            var cts = new CancellationTokenSource();
            var previous = _cancellationTokenSource;
            _cancellationTokenSource = cts;
            try
            {
                await previous.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
                // A concurrent cancel already disposed the previous source.
            }

            previous.Dispose();
            var token = cts.Token;

            var searchTerm = SearchText;
            var parsedTerms = ParseSearchTerms(searchTerm);
            var hasMeaningfulKeywords = parsedTerms
                .Any(static t => !t.Equals("and", StringComparison.OrdinalIgnoreCase) &&
                                 !t.Equals("or", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _mainViewModel.StatusText =
                    _localization.GetString("Pleaseenterasearchterm", "Please enter a search term.");
                return;
            }

            if (!hasMeaningfulKeywords)
            {
                await _messageBox.EnterValidSearchTermsMessageBoxAsync();
                return;
            }

            _playSoundEffects.PlayNotificationSound();

            var selectedSystem = SelectedSystemIndex >= 0 && SelectedSystemIndex < SystemNames.Count
                ? SystemNames[SelectedSystemIndex]
                : SystemNames.FirstOrDefault();

            LaunchButtonEnabled = false;
            IsLoading = true;
            LoadingMessage = "Searching... Please wait.";
            NoResultsVisible = false;
            ResultsCountText = "";

            try
            {
                var results = await PerformSearch(
                    searchTerm, selectedSystem,
                    SearchFilename, SearchMameDescription, SearchFolderName, SearchRecursively, token);

                if (results.Count > 0)
                {
                    SearchResults = new ObservableCollection<SearchResult>(results);
                    NoResultsVisible = false;
                    ResultsCountText = $"Found {results.Count} results";
                }
                else
                {
                    SearchResults = [];
                    SelectedResult = null;
                    NoResultsVisible = true;
                    ResultsCountText = "";
                }

                _logErrors.Information("Global search for '{SearchTerm}' returned {Count} result(s)", searchTerm,
                    results.Count);
            }
            catch (OperationCanceledException)
            {
                // Search was canceled — ignore
            }
            catch (Exception ex)
            {
                _logErrors.Error(ex, "Error during the global search operation");
                await _messageBox.GlobalSearchErrorMessageBoxAsync();
                SearchResults = [];
                SelectedResult = null;
                NoResultsVisible = true;
                ResultsCountText = "";
            }
            finally
            {
                // Only clear the spinner when THIS search was not superseded/cancelled;
                // reading the shared field could clear a newer search's loading state.
                if (!cts.IsCancellationRequested) IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "Error in the global search command");
        }
    }

    private async Task<List<SearchResult>> PerformSearch(
        string searchTerm, string? selectedSystem,
        bool searchFilename, bool searchMameDescription, bool searchFolderName, bool searchRecursively,
        CancellationToken token)
    {
        var results = new List<SearchResult>();
        var searchTerms = ParseSearchTerms(searchTerm);
        var systems = _systemManagerService.LoadSystems();

        IEnumerable<SystemManagerConfig> systemsToSearch = systems;
        if (!string.IsNullOrEmpty(selectedSystem) &&
            !string.Equals(selectedSystem, "All Systems", StringComparison.Ordinal))
        {
            systemsToSearch = systems.Where(sm =>
                sm.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var systemManager in systemsToSearch)
        {
            token.ThrowIfCancellationRequested();

            var effectiveSystem = searchRecursively switch
            {
                true when systemManager.DisableRecursiveSearch =>
                    CloneWithRecursion(systemManager, false),
                false when !systemManager.DisableRecursiveSearch =>
                    CloneWithRecursion(systemManager, true),
                _ => systemManager
            };

            foreach (var systemFolderPathRaw in systemManager.SystemFolders)
            {
                token.ThrowIfCancellationRequested();

                var systemFolderPath = PathHelper.ResolveRelativeToAppDirectory(systemFolderPathRaw);
                if (string.IsNullOrEmpty(systemFolderPath) || !Directory.Exists(systemFolderPath)) continue;

                var matchedFilesList = await _getListOfFiles.GetFilesAsync(
                    systemFolderPath, systemManager.FileFormatsToSearch,
                    effectiveSystem.DisableRecursiveSearch, effectiveSystem.GroupByFolder, token);

                var matchedFiles = matchedFilesList.Where(file =>
                {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);

                    var filenameMatch = searchFilename &&
                                        MatchesSearchQuery(fileNameWithoutExtension.ToLowerInvariant(), searchTerms);

                    var mameDescriptionMatch = searchMameDescription &&
                                               _mameData.Lookup.TryGetValue(fileNameWithoutExtension,
                                                   out var description) &&
                                               MatchesSearchQuery(description.ToLowerInvariant(), searchTerms);

                    var folderNameMatch = false;
                    if (searchFolderName)
                    {
                        var dir = Path.GetDirectoryName(file);
                        var directoryName = dir is null ? null : new DirectoryInfo(dir).Name;
                        folderNameMatch = MatchesSearchQuery(directoryName?.ToLowerInvariant(), searchTerms);
                    }

                    return filenameMatch || mameDescriptionMatch || folderNameMatch;
                });

                foreach (var filePath in matchedFiles)
                {
                    token.ThrowIfCancellationRequested();

                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    var machine = _mameData.Machines.FirstOrDefault(m =>
                        m.MachineName.Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));

                    results.Add(new SearchResult
                    {
                        FileName = fileNameWithoutExtension,
                        FileNameWithExtension = Path.GetFileName(filePath),
                        FolderName =
                            Path.GetDirectoryName(filePath)?.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? "",
                        FilePath = filePath,
                        MachineName = machine?.Description ?? "",
                        SystemName = systemManager.SystemName,
                        EmulatorManager = systemManager.Emulators.FirstOrDefault(),
                        CoverImage = _findCoverImage.FindCoverImagePath(
                            fileNameWithoutExtension, systemManager.SystemName, systemManager.SystemImageFolder)
                    });
                }
            }
        }

        return ScoreResults(results, searchTerms);
    }

    /// <summary>
    ///     Clones the system config with an overridden recursive-search setting
    ///     (SystemManagerConfig is immutable, mirroring the WPF page behavior).
    /// </summary>
    private static SystemManagerConfig CloneWithRecursion(SystemManagerConfig source, bool disableRecursive)
    {
        return new SystemManagerConfig
        {
            SystemName = source.SystemName,
            SystemFolders = source.SystemFolders,
            SystemImageFolder = source.SystemImageFolder,
            FileFormatsToSearch = source.FileFormatsToSearch,
            ExtractFileBeforeLaunch = source.ExtractFileBeforeLaunch,
            FileFormatsToLaunch = source.FileFormatsToLaunch,
            Emulators = source.Emulators,
            GroupByFolder = source.GroupByFolder,
            DisableRecursiveSearch = disableRecursive
        };
    }

    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        try
        {
            if (SelectedResult is not { } result)
            {
                _mainViewModel.StatusText =
                    _localization.GetString("Selectagametolaunchfirst", "Select a game to launch first.");
                return;
            }

            if (string.IsNullOrEmpty(result.FilePath) || string.IsNullOrEmpty(result.SystemName) ||
                result.EmulatorManager is null)
            {
                _logErrors.Information(
                    "[GlobalSearch] filePath, selectedSystemName or selectedEmulatorManager is null for '{FileName}'",
                    result.FileName);
                await _messageBox.ErrorLaunchingGameMessageBoxAsync(PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            _playSoundEffects.PlayNotificationSound();
            await _mainViewModel.LaunchGameAtPathAsync(result.FilePath, result.SystemName);
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "Error launching game from the global search results");
            await _messageBox.ErrorLaunchingGameMessageBoxAsync(PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    /// <summary>Cancels any in-progress search operation.</summary>
    public void CancelSearch()
    {
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The search completed and disposed its token source — nothing to cancel.
        }
    }

    private static List<SearchResult> ScoreResults(List<SearchResult> results, List<string> searchTerms)
    {
        foreach (var result in results)
        {
            var fileName = result.FileName.ToLowerInvariant();
            var machineName = result.MachineName?.ToLowerInvariant() ?? "";
            var folderName = result.FolderName?.ToLowerInvariant() ?? "";

            result.Score = CalculateScore(fileName, searchTerms)
                           + CalculateScore(machineName, searchTerms)
                           + CalculateScore(folderName, searchTerms);
        }

        return results.OrderByDescending(static r => r.Score)
            .ThenBy(static r => r.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int CalculateScore(string text, List<string> searchTerms)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var score = 0;
        foreach (var term in searchTerms)
        {
            var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;

            score += 10;
            score += text.Length - index;
        }

        return score;
    }

    private static bool MatchesSearchQuery(string? text, IReadOnlyCollection<string> searchTerms)
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

    private static List<string> ParseSearchTerms(string searchTerm)
    {
        var terms = new List<string>();
        foreach (Match match in MyRegex().Matches(searchTerm)) terms.Add(match.Value.Trim('"').ToLowerInvariant());

        return terms.Where(static t => !string.IsNullOrWhiteSpace(t)).ToList();
    }

    [GeneratedRegex("""[\"](.+?)[\"]|([^ ]+)""", RegexOptions.Compiled | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex MyRegex();
}