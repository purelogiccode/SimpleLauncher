using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.MameManager;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Models;
using SimpleLauncher.Services.Favorites;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the global search window, providing cross-system ROM search with scoring.
/// </summary>
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
public partial class GlobalSearchViewModel : ObservableObject, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly IGetListOfFilesService _getListOfFiles;
    private readonly IImageLoader _imageLoader;
    private readonly ILogger _logger;
    private readonly IList<MameManagerService> _machines;
    private readonly IDictionary<string, string> _mameLookup;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly IResourceProvider _resourceProvider;
    private readonly SettingsManagerService _settings;
    private readonly IList<SystemManager> _systemManagers;
    private CancellationTokenSource _cancellationTokenSource;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool LaunchButtonEnabled { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "";

    [ObservableProperty] public partial bool NoResultsVisible { get; set; }

    [ObservableProperty] public partial Stream? PreviewImageSource { get; set; }

    [ObservableProperty] public partial string ResultsCountText { get; set; } = "";

    [ObservableProperty] public partial bool ResultsCountVisible { get; set; }

    [ObservableProperty] public partial ObservableCollection<SearchResult> SearchResults { get; set; } = [];

    [ObservableProperty] public partial SearchResult? SelectedResult { get; set; }

    [ObservableProperty] public partial int SelectedSystemIndex { get; set; }

    [ObservableProperty] public partial List<string> SystemNames { get; set; } = [];

    /// <summary>Initializes a new instance of the <see cref="GlobalSearchViewModel" />.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="systemManagers">The list of configured system managers.</param>
    /// <param name="machines">The list of MAME machine definitions.</param>
    /// <param name="mameLookup">The MAME description lookup dictionary.</param>
    /// <param name="favoritesManager">The favorites manager.</param>
    /// <param name="playSoundEffects">The sound effects service.</param>
    /// <param name="getListOfFiles">The file listing service.</param>
    /// <param name="findCoverImage">The cover image lookup service.</param>
    /// <param name="imageLoader">The image loader service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public GlobalSearchViewModel(
        IConfiguration configuration,
        ILogger logErrors,
        SettingsManagerService settings,
        IList<SystemManager> systemManagers,
        IList<MameManagerService> machines,
        IDictionary<string, string> mameLookup,
        FavoritesManager favoritesManager,
        PlaySoundEffects playSoundEffects,
        IGetListOfFilesService getListOfFiles,
        IFindCoverImageService findCoverImage,
        IImageLoader imageLoader,
        IMessageBoxLibraryService messageBox,
        IResourceProvider resourceProvider)
    {
        _configuration = configuration;
        _logger = logErrors;
        _settings = settings;
        _systemManagers = systemManagers;
        _machines = machines;
        _mameLookup = mameLookup;
        _favoritesManager = favoritesManager;
        _playSoundEffects = playSoundEffects;
        _getListOfFiles = getListOfFiles;
        _findCoverImage = findCoverImage;
        _imageLoader = imageLoader;
        _messageBox = messageBox;
        _resourceProvider = resourceProvider;
        _cancellationTokenSource = new CancellationTokenSource();

        InitializeSystemNames();
    }

    /// <summary>Releases resources used by this ViewModel.</summary>
    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        PreviewImageSource?.Dispose();
        PreviewImageSource = null;
        GC.SuppressFinalize(this);
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnPreviewImageSourceChanged(Stream? oldValue, Stream? newValue)
    {
        oldValue?.Dispose();
    }

    private void InitializeSystemNames()
    {
        var allSystemsString = _resourceProvider.GetString("AllSystems", "All Systems");
        var names = new List<string> { allSystemsString };
        names.AddRange(_systemManagers.Select(static sm => sm.SystemName)
            .OrderBy(static name => name, StringComparer.Ordinal));
        SystemNames = names;
        SelectedSystemIndex = 0;
    }

    /// <summary>Performs a global search across all configured systems.</summary>
    /// <param name="searchTerm">The search query string.</param>
    /// <param name="selectedSystem">The system name to filter by, or null for all systems.</param>
    /// <param name="searchFilename">Whether to search ROM file names.</param>
    /// <param name="searchMameDescription">Whether to search MAME machine descriptions.</param>
    /// <param name="searchFolderName">Whether to search folder names.</param>
    /// <param name="searchRecursively">Whether to search recursively in subdirectories.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SearchAsync(string searchTerm, string? selectedSystem,
        bool searchFilename, bool searchMameDescription, bool searchFolderName, bool searchRecursively)
    {
        try
        {
            // Cancel previous search
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();

            _cancellationTokenSource = new CancellationTokenSource();

            // Validate search terms
            var parsedTerms = ParseSearchTerms(searchTerm);
            var hasMeaningfulKeywords = parsedTerms
                .Any(static t => !t.Equals("and", StringComparison.OrdinalIgnoreCase) &&
                                 !t.Equals("or", StringComparison.OrdinalIgnoreCase));

            if (!hasMeaningfulKeywords)
            {
                await _messageBox.EnterValidSearchTermsMessageBoxAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                await _messageBox.PleaseEnterSearchTermMessageBoxAsync();
                return;
            }

            LaunchButtonEnabled = false;
            PreviewImageSource = null;
            IsLoading = true;
            LoadingMessage = _resourceProvider.GetString("Searchingpleasewait", "Searching... Please wait.");
            NoResultsVisible = false;
            ResultsCountText = "";
            ResultsCountVisible = false;

            try
            {
                var results = await PerformSearchAsync(
                    searchTerm, selectedSystem, searchFilename, searchMameDescription,
                    searchFolderName, searchRecursively, _cancellationTokenSource.Token);

                if (results.Count > 0)
                {
                    SearchResults = new ObservableCollection<SearchResult>(results);
                    NoResultsVisible = false;
                    ResultsCountText = string.Format(CultureInfo.InvariantCulture,
                        _resourceProvider.GetString("FoundResults", "Found {0} results"), results.Count);
                    ResultsCountVisible = true;
                }
                else
                {
                    SearchResults = [];
                    NoResultsVisible = true;
                    PreviewImageSource = null;
                    ResultsCountText = "";
                    ResultsCountVisible = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Search was canceled - ignore
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during search operation");
                await _messageBox.GlobalSearchErrorMessageBoxAsync();
                NoResultsVisible = true;
                ResultsCountText = "";
                ResultsCountVisible = false;
            }
            finally
            {
                if (!_cancellationTokenSource.IsCancellationRequested) IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in SearchAsync");
        }
    }

    private async Task<List<SearchResult>> PerformSearchAsync(string searchTerm, string? selectedSystem,
        bool searchFilename, bool searchMameDescription, bool searchFolderName, bool searchRecursively,
        CancellationToken token)
    {
        var results = new List<SearchResult>();
        var searchTerms = ParseSearchTerms(searchTerm);

        var allSystemsString = _resourceProvider.GetString("AllSystems", "All Systems");
        IEnumerable<SystemManager> systemsToSearch = _systemManagers;
        if (!string.Equals(selectedSystem, allSystemsString, StringComparison.Ordinal))
        {
            systemsToSearch = _systemManagers.Where(sm =>
                sm.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var systemManager in systemsToSearch)
        {
            token.ThrowIfCancellationRequested();

            var effectiveSystemManager = searchRecursively switch
            {
                true when systemManager.DisableRecursiveSearch => new SystemManager
                {
                    SystemName = systemManager.SystemName ?? "",
                    SystemFolders = systemManager.SystemFolders ?? [],
                    SystemImageFolder = systemManager.SystemImageFolder ?? "",
                    FileFormatsToSearch = systemManager.FileFormatsToSearch ?? [],
                    ExtractFileBeforeLaunch = systemManager.ExtractFileBeforeLaunch,
                    FileFormatsToLaunch = systemManager.FileFormatsToLaunch ?? [],
                    Emulators = systemManager.Emulators,
                    GroupByFolder = systemManager.GroupByFolder,
                    DisableRecursiveSearch = false
                },
                false when !systemManager.DisableRecursiveSearch => new SystemManager
                {
                    SystemName = systemManager.SystemName ?? "",
                    SystemFolders = systemManager.SystemFolders ?? [],
                    SystemImageFolder = systemManager.SystemImageFolder ?? "",
                    FileFormatsToSearch = systemManager.FileFormatsToSearch ?? [],
                    ExtractFileBeforeLaunch = systemManager.ExtractFileBeforeLaunch,
                    FileFormatsToLaunch = systemManager.FileFormatsToLaunch ?? [],
                    Emulators = systemManager.Emulators,
                    GroupByFolder = systemManager.GroupByFolder,
                    DisableRecursiveSearch = true
                },
                _ => systemManager
            };

            foreach (var systemFolderPathRaw in systemManager.SystemFolders!)
            {
                token.ThrowIfCancellationRequested();

                var systemFolderPath = PathHelper.ResolveRelativeToAppDirectory(systemFolderPathRaw);
                if (string.IsNullOrEmpty(systemFolderPath) || !Directory.Exists(systemFolderPath) ||
                    systemManager.FileFormatsToSearch == null)
                {
                    continue;
                }

                var matchedFilesList = await _getListOfFiles.GetFilesAsync(
                    systemFolderPath, systemManager.FileFormatsToSearch, effectiveSystemManager.DisableRecursiveSearch,
                    effectiveSystemManager.GroupByFolder, token);

                var filesInSystemFolder = matchedFilesList.Where(file =>
                {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);

                    var filenameMatch = searchFilename &&
                                        MatchesSearchQuery(fileNameWithoutExtension.ToLowerInvariant(), searchTerms);

                    var mameDescriptionMatch = searchMameDescription &&
                                               _mameLookup.TryGetValue(fileNameWithoutExtension, out var description) &&
                                               MatchesSearchQuery(description.ToLowerInvariant(), searchTerms);

                    var folderNameMatch = searchFolderName;
                    if (folderNameMatch)
                    {
                        var dir = Path.GetDirectoryName(file);
                        var directoryName = dir is null ? null : new DirectoryInfo(dir).Name;
                        folderNameMatch = MatchesSearchQuery(directoryName?.ToLowerInvariant(), searchTerms);
                    }

                    return filenameMatch || mameDescriptionMatch || folderNameMatch;
                });

                var matchedFilePaths = filesInSystemFolder.ToList();
                foreach (var filePath in matchedFilePaths)
                {
                    token.ThrowIfCancellationRequested();

                    var machine = _machines.FirstOrDefault(m =>
                        m.MachineName.Equals(Path.GetFileNameWithoutExtension(filePath),
                            StringComparison.OrdinalIgnoreCase));

                    results.Add(new SearchResult
                    {
                        FileName = Path.GetFileNameWithoutExtension(filePath),
                        FileNameWithExtension = Path.GetFileName(filePath),
                        FolderName =
                            Path.GetDirectoryName(filePath)?.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? "",
                        FilePath = filePath,
                        MachineName = machine?.Description ?? "",
                        SystemName = systemManager.SystemName ?? "",
                        EmulatorManager = systemManager.Emulators.FirstOrDefault() ?? null!,
                        CoverImage = _findCoverImage.FindCoverImagePath(
                            Path.GetFileNameWithoutExtension(filePath), systemManager.SystemName ?? "",
                            systemManager.SystemImageFolder ?? "")
                    });
                }
            }
        }

        return ScoreResults(results, searchTerms);
    }

    /// <summary>Updates the preview image from the specified image path.</summary>
    /// <param name="imagePath">The path to the image file, or null to clear.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdatePreviewImageAsync(string? imagePath)
    {
        try
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                PreviewImageSource = null;
                return;
            }

            var (imageStream, _) = await _imageLoader.LoadImageAsync(imagePath);
            PreviewImageSource = imageStream;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading preview image");
            PreviewImageSource = null;
        }
    }

    /// <summary>Gets the system manager for the specified system name.</summary>
    /// <param name="systemName">The system name to look up.</param>
    /// <returns>The matching <see cref="SystemManager" />, or <c>null</c> if not found.</returns>
    public SystemManager? GetSystemManager(string systemName)
    {
        return _systemManagers.FirstOrDefault(manager =>
            manager.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Cancels any in-progress search operation.</summary>
    public void CancelSearch()
    {
        _cancellationTokenSource.Cancel();
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
        var matches = MyRegex().Matches(searchTerm);
        foreach (Match match in matches) terms.Add(match.Value.Trim('"').ToLowerInvariant());

        return terms.Where(static t => !string.IsNullOrWhiteSpace(t)).ToList();
    }

    [GeneratedRegex("""[\"](.+?)[\"]|([^ ]+)""", RegexOptions.Compiled | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex MyRegex();
}