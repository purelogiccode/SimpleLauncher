using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.PlaySound;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the Play History section of the main window, showing the play
///     history table with sorting, removal, and launching (WPF PlayHistoryPage equivalent).
/// </summary>
public partial class PlayHistorySectionViewModel : ObservableObject
{
    private readonly IFindCoverImageService _findCoverImage;
    private readonly Services.LocalizationService _localization;
    private readonly ILogger _logErrors;
    private readonly MainViewModel _mainViewModel;
    private readonly IMameDataService _mameData;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly SystemManagerService _systemManager;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "";

    [ObservableProperty] public partial ObservableCollection<PlayHistoryItem> PlayHistoryList { get; set; } = [];

    [ObservableProperty] public partial PlayHistoryItem? SelectedItem { get; set; }

    public PlayHistorySectionViewModel(
        PlayHistoryManager playHistoryManager,
        SystemManagerService systemManager,
        IFindCoverImageService findCoverImage,
        IMameDataService mameData,
        PlaySoundEffects playSoundEffects,
        MainViewModel mainViewModel,
        IMessageBoxLibraryService messageBox,
        ILogger logErrors,
        Services.LocalizationService localization)
    {
        _playHistoryManager = playHistoryManager;
        _systemManager = systemManager;
        _findCoverImage = findCoverImage;
        _mameData = mameData;
        _playSoundEffects = playSoundEffects;
        _mainViewModel = mainViewModel;
        _messageBox = messageBox;
        _logErrors = logErrors;
        _localization = localization;
    }

    /// <summary>
    ///     Loads the play history from the manager and enriches each item with machine
    ///     description, default emulator, and cover image.
    /// </summary>
    public async Task LoadHistoryAsync()
    {
        try
        {
            IsLoading = true;
            LoadingMessage = "Loading history...";

            await Task.Yield();

            PlayHistoryList = await Task.Run(() =>
            {
                var processedList = new List<PlayHistoryItem>(_playHistoryManager.PlayHistoryList.Count);
                foreach (var item in _playHistoryManager.PlayHistoryList) processedList.Add(CreateProcessedItem(item));

                return new ObservableCollection<PlayHistoryItem>(processedList);
            });

            ApplySortByDate();
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "Error loading play history data");
            PlayHistoryList = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplySortByDate()
    {
        PlayHistoryList = new ObservableCollection<PlayHistoryItem>(
            PlayHistoryList.OrderByDescending(item => TryParseDateTime(item.LastPlayDate, item.LastPlayTime)));
    }

    private void ApplySortByTotalPlayTime()
    {
        PlayHistoryList = new ObservableCollection<PlayHistoryItem>(
            PlayHistoryList.OrderByDescending(static item => item.TotalPlayTime));
    }

    private void ApplySortByTimesPlayed()
    {
        PlayHistoryList = new ObservableCollection<PlayHistoryItem>(
            PlayHistoryList.OrderByDescending(static item => item.TimesPlayed));
    }

    [RelayCommand]
    private void SortByDate()
    {
        _playSoundEffects.PlayNotificationSound();
        ApplySortByDate();
    }

    [RelayCommand]
    private void SortByTotalPlayTime()
    {
        _playSoundEffects.PlayNotificationSound();
        ApplySortByTotalPlayTime();
    }

    [RelayCommand]
    private void SortByTimesPlayed()
    {
        _playSoundEffects.PlayNotificationSound();
        ApplySortByTimesPlayed();
    }

    /// <summary>Removes the selected history items and persists the change.</summary>
    [RelayCommand]
    private Task RemoveSelectedAsync()
    {
        try
        {
            try
            {
                if (SelectedItem is not { } item)
                {
                    _mainViewModel.StatusText = _localization.GetString("Selectahistoryitemtoremovefirst",
                        "Select a history item to remove first.");
                    return Task.CompletedTask;
                }

                _playSoundEffects.PlayTrashSound();
                PlayHistoryList.Remove(item);
                SelectedItem = null;
                SyncToManager();

                _mainViewModel.RefreshFavoritesAndHistory();
            }
            catch (Exception ex)
            {
                _logErrors.Error(ex, "Error removing history item");
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    /// <summary>Removes all history items after user confirmation.</summary>
    [RelayCommand]
    private async Task RemoveAllAsync()
    {
        try
        {
            // WPF parity: prompt the user before wiping the whole history.
            var result = await _messageBox.ReallyWantToRemoveAllPlayHistoryMessageBoxAsync();
            if (result != MessageBoxResult.Yes) return;

            _playSoundEffects.PlayTrashSound();
            PlayHistoryList.Clear();
            SelectedItem = null;
            SyncToManager();

            _mainViewModel.RefreshFavoritesAndHistory();
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "Error removing all history items");
        }
    }

    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        if (SelectedItem is not { } item)
        {
            _mainViewModel.StatusText =
                _localization.GetString("Selectagametolaunchfirst", "Select a game to launch first.");
            return;
        }

        _playSoundEffects.PlayNotificationSound();

        if (!File.Exists(item.FileName))
        {
            // Expected condition (history points to a missing file) — keep out of the bug report service.
            _logErrors.Information("History file does not exist: {Path}", item.FileName);
            _mainViewModel.StatusText =
                $"{_localization.GetString("Filedoesnotexist", "File does not exist:")} {item.DisplayName}";
            return;
        }

        await _mainViewModel.LaunchGameAtPathAsync(item.FileName, item.SystemName);

        // Reload so the new play session (times played / play time) appears immediately
        await LoadHistoryAsync();
    }

    /// <summary>Persists the current list back to the manager.</summary>
    private void SyncToManager()
    {
        _playHistoryManager.PlayHistoryList = PlayHistoryList;
        _ = _playHistoryManager.SavePlayHistoryAsync().ContinueWith(t =>
        {
            if (t.IsFaulted) Log.Warning(t.Exception, "Failed to save play history");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private PlayHistoryItem CreateProcessedItem(PlayHistoryItem source)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(source.FileName) ?? source.FileName;
        var systemManager = _systemManager.GetSystem(source.SystemName);

        return new PlayHistoryItem
        {
            FileName = source.FileName,
            SystemName = source.SystemName,
            TotalPlayTime = source.TotalPlayTime,
            TimesPlayed = source.TimesPlayed,
            LastPlayDate = source.LastPlayDate,
            LastPlayTime = source.LastPlayTime,
            MachineDescription = _mameData.Lookup.TryGetValue(fileNameWithoutExtension, out var description)
                ? description
                : "",
            DefaultEmulator = systemManager?.Emulators.FirstOrDefault()?.EmulatorName ?? "Unknown",
            CoverImage = systemManager is null
                ? ""
                : _findCoverImage.FindCoverImagePath(fileNameWithoutExtension, source.SystemName,
                    systemManager.SystemImageFolder)
        };
    }

    private static DateTime TryParseDateTime(string dateStr, string timeStr)
    {
        try
        {
            if (DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            {
                return result;
            }

            string[] dateFormats =
            [
                "yyyy/MM/dd", "yyyy.MM.dd", "dd.MM.yyyy",
                "MM/dd/yyyy", "dd/MM/yyyy", "dd-MM-yyyy",
                "d", "D"
            ];
            foreach (var format in dateFormats)
            {
                if (DateTime.TryParseExact($"{dateStr} {timeStr}",
                        $"{format} HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                {
                    return result;
                }
            }

            return DateTime.TryParse($"{dateStr} {timeStr}", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out result)
                ? result
                : DateTime.MinValue;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to parse history date: {Date} {Time}", dateStr, timeStr);
            return DateTime.MinValue;
        }
    }
}