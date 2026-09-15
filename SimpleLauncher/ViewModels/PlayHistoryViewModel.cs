using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.MameManager;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Services.PlayHistory;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     Provides the play history list, selection, and preview logic for the play history UI.
/// </summary>
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
public partial class PlayHistoryViewModel : ObservableObject, IDisposable
{
    private const string TimeFormat = "HH:mm:ss";
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
    private readonly IConfiguration _configuration;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly IImageLoader _imageLoader;
    private readonly ILogger _logger;
    private readonly IList<MameManagerService> _machines;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly IResourceProvider _resourceProvider;
    private readonly SettingsManagerService _settings;
    private readonly IList<SystemManager> _systemManagers;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "";

    [ObservableProperty] public partial ObservableCollection<PlayHistoryItem> PlayHistoryList { get; set; } = [];

    [ObservableProperty] public partial Stream? PreviewImageSource { get; set; }

    [ObservableProperty] public partial PlayHistoryItem? SelectedItem { get; set; }

    /// <summary>Initializes a new instance of the <see cref="PlayHistoryViewModel" />.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="playHistoryManager">The play history manager for persistence.</param>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="systemManagers">The list of configured system managers.</param>
    /// <param name="machines">The list of MAME machine definitions.</param>
    /// <param name="playSoundEffects">The sound effects service.</param>
    /// <param name="findCoverImage">The cover image lookup service.</param>
    /// <param name="imageLoader">The image loader service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public PlayHistoryViewModel(
        IConfiguration configuration,
        ILogger logErrors,
        PlayHistoryManager playHistoryManager,
        SettingsManagerService settings,
        IList<SystemManager> systemManagers,
        IList<MameManagerService> machines,
        PlaySoundEffects playSoundEffects,
        IFindCoverImageService findCoverImage,
        IImageLoader imageLoader,
        IMessageBoxLibraryService messageBox,
        IResourceProvider resourceProvider)
    {
        _configuration = configuration;
        _logger = logErrors;
        _playHistoryManager = playHistoryManager;
        _settings = settings;
        _systemManagers = systemManagers;
        _machines = machines;
        _playSoundEffects = playSoundEffects;
        _findCoverImage = findCoverImage;
        _imageLoader = imageLoader;
        _messageBox = messageBox;
        _resourceProvider = resourceProvider;
    }

    /// <summary>Releases resources used by this ViewModel.</summary>
    public void Dispose()
    {
        PreviewImageSource?.Dispose();
        PreviewImageSource = null;
        GC.SuppressFinalize(this);
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnPreviewImageSourceChanged(Stream? oldValue, Stream? newValue)
    {
        oldValue?.Dispose();
    }

    /// <summary>Loads the play history with cover images and machine descriptions.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadHistoryAsync()
    {
        try
        {
            IsLoading = true;
            LoadingMessage = _resourceProvider.GetString("LoadingHistory", "Loading history...");

            await Task.Yield();

            var processedHistory = await Task.Run(() =>
            {
                var processedList = new List<PlayHistoryItem>();
                foreach (var historyItemConfig in _playHistoryManager.PlayHistoryList)
                {
                    var machine = _machines.FirstOrDefault(m =>
                        m.MachineName.Equals(Path.GetFileNameWithoutExtension(historyItemConfig.FileName),
                            StringComparison.OrdinalIgnoreCase));
                    var machineDescription = machine?.Description ?? "";
                    var systemManager = _systemManagers.FirstOrDefault(config =>
                        config.SystemName.Equals(historyItemConfig.SystemName, StringComparison.OrdinalIgnoreCase));
                    var defaultEmulator = systemManager?.Emulators.FirstOrDefault()?.EmulatorName
                                          ?? _resourceProvider.GetString("UnknownString", "Unknown");
                    var coverImagePath = GetCoverImagePath(historyItemConfig.SystemName, historyItemConfig.FileName);

                    processedList.Add(new PlayHistoryItem
                    {
                        FileName = historyItemConfig.FileName,
                        SystemName = historyItemConfig.SystemName,
                        TotalPlayTime = historyItemConfig.TotalPlayTime,
                        TimesPlayed = historyItemConfig.TimesPlayed,
                        LastPlayDate = historyItemConfig.LastPlayDate,
                        LastPlayTime = historyItemConfig.LastPlayTime,
                        MachineDescription = machineDescription,
                        DefaultEmulator = defaultEmulator,
                        CoverImage = coverImagePath
                    });
                }

                return processedList;
            });

            PlayHistoryList = new ObservableCollection<PlayHistoryItem>(processedHistory);
            SortByDate();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading play history data");
            await _messageBox.ErrorLoadingRomHistoryMessageBoxAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Sorts the play history list by last played date in descending order.</summary>
    public void SortByDate()
    {
        var sorted = new ObservableCollection<PlayHistoryItem>(
            PlayHistoryList.OrderByDescending(item => TryParseDateTime(item.LastPlayDate, item.LastPlayTime))
        );
        PlayHistoryList = sorted;
    }

    /// <summary>Sorts the play history list by total play time in descending order.</summary>
    public void SortByTotalPlayTime()
    {
        var sorted = new ObservableCollection<PlayHistoryItem>(
            PlayHistoryList.OrderByDescending(static item => item.TotalPlayTime)
        );
        PlayHistoryList = sorted;
    }

    /// <summary>Sorts the play history list by times played in descending order.</summary>
    public void SortByTimesPlayed()
    {
        var sorted = new ObservableCollection<PlayHistoryItem>(
            PlayHistoryList.OrderByDescending(static item => item.TimesPlayed)
        );
        PlayHistoryList = sorted;
    }

    /// <summary>Removes a single item from the play history and persists the change.</summary>
    /// <param name="item">The play history item to remove.</param>
    public void RemoveItem(PlayHistoryItem item)
    {
        PlayHistoryList.Remove(item);
        _playHistoryManager.PlayHistoryList = PlayHistoryList;
        _ = _playHistoryManager.SavePlayHistoryAsync();
        PreviewImageSource = null;
    }

    /// <summary>Removes multiple items from the play history and persists the change.</summary>
    /// <param name="items">The collection of play history items to remove.</param>
    public void RemoveItems(IEnumerable<PlayHistoryItem> items)
    {
        foreach (var item in items) PlayHistoryList.Remove(item);

        _playHistoryManager.PlayHistoryList = PlayHistoryList;
        _ = _playHistoryManager.SavePlayHistoryAsync();
        PreviewImageSource = null;
    }

    [RelayCommand]
    private async Task RemoveAllAsync()
    {
        try
        {
            var result = await _messageBox.ReallyWantToRemoveAllPlayHistoryMessageBoxAsync();
            if (result == CoreMessageBoxResult.Yes)
            {
                PlayHistoryList.Clear();
                _playHistoryManager.PlayHistoryList = PlayHistoryList;
                await _playHistoryManager.SavePlayHistoryAsync();

                _playSoundEffects.PlayTrashSound();
                PreviewImageSource = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in RemoveAllAsync");
        }
    }

    [RelayCommand]
    private async Task LaunchGameAsync()
    {
        if (SelectedItem == null)
        {
            await _messageBox.SelectAGameToLaunchMessageBoxAsync();
            return;
        }

        _playSoundEffects.PlayNotificationSound();
        // Game launching is handled by the caller (code-behind) since it needs WPF Window context
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

    /// <summary>Reloads the play history from the manager after a game launch.</summary>
    public void RefreshAfterGameLaunch()
    {
        var newList = new ObservableCollection<PlayHistoryItem>();
        foreach (var historyItemConfig in _playHistoryManager.PlayHistoryList)
        {
            var machine = _machines.FirstOrDefault(m =>
                m.MachineName.Equals(Path.GetFileNameWithoutExtension(historyItemConfig.FileName),
                    StringComparison.OrdinalIgnoreCase));
            var machineDescription = machine?.Description ?? "";
            var systemManager = _systemManagers.FirstOrDefault(manager =>
                manager.SystemName.Equals(historyItemConfig.SystemName, StringComparison.OrdinalIgnoreCase));
            var defaultEmulator = systemManager?.Emulators.FirstOrDefault()?.EmulatorName
                                  ?? _resourceProvider.GetString("UnknownString", "Unknown");
            var coverImagePath = GetCoverImagePath(historyItemConfig.SystemName, historyItemConfig.FileName);

            newList.Add(new PlayHistoryItem
            {
                FileName = historyItemConfig.FileName,
                SystemName = historyItemConfig.SystemName,
                TotalPlayTime = historyItemConfig.TotalPlayTime,
                TimesPlayed = historyItemConfig.TimesPlayed,
                LastPlayDate = historyItemConfig.LastPlayDate,
                LastPlayTime = historyItemConfig.LastPlayTime,
                MachineDescription = machineDescription,
                DefaultEmulator = defaultEmulator,
                CoverImage = coverImagePath
            });
        }

        PlayHistoryList = newList;
        SortByDate();
    }

    private DateTime TryParseDateTime(string dateStr, string timeStr)
    {
        try
        {
            if (DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyy-MM-dd HH:mm:ss",
                    InvariantCulture, DateTimeStyles.None, out var result))
            {
                return result;
            }

            string[] dateFormats =
            [
                "yyyy/MM/dd", "yyyy.MM.dd", "dd.MM.yyyy",
                "MM/dd/yyyy", "dd/MM/yyyy", "dd-MM-yyyy",
                "d", "D"
            ];
            foreach (var df in dateFormats)
            {
                if (DateTime.TryParseExact($"{dateStr} {timeStr}",
                        $"{df} {TimeFormat}", InvariantCulture, DateTimeStyles.None, out result))
                {
                    return result;
                }
            }

            if (DateTime.TryParse($"{dateStr} {timeStr}", InvariantCulture, DateTimeStyles.None, out result))
                return result;

            return DateTime.MinValue;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error parsing date: {dateStr} {timeStr}");
            return DateTime.MinValue;
        }
    }

    private string GetCoverImagePath(string systemName, string fileName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var systemManager = _systemManagers.FirstOrDefault(manager =>
            manager.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var defaultCoverImagePath = Path.Combine(baseDirectory, "images", "default.png");

        if (systemManager == null) return defaultCoverImagePath;

        return _findCoverImage.FindCoverImagePath(fileNameWithoutExtension, systemName,
            systemManager.SystemImageFolder);
    }
}