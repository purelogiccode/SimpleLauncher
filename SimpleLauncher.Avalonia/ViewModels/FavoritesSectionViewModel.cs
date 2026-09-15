using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.PlaySound;
using ILogger = Serilog.ILogger;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the Favorites section of the main window, showing the user's
///     favorite games in a table with preview, removal, and launching.
///     Mirrors the WPF FavoritesPage / FavoritesViewModel flow: favorites are stored
///     as a file NAME and resolved against the system folders at use time.
/// </summary>
public partial class FavoritesSectionViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly ILogger _logErrors;
    private readonly Services.LocalizationService _localization;
    private readonly MainViewModel _mainViewModel;
    private readonly IMameDataService _mameData;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly SystemManagerService _systemManager;

    [ObservableProperty] public partial ObservableCollection<FavoriteRowViewModel> Favorites { get; set; } = [];

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "";

    [ObservableProperty] public partial string PreviewImagePath { get; set; } = "";

    [ObservableProperty] public partial FavoriteRowViewModel? SelectedFavorite { get; set; }

    public FavoritesSectionViewModel(
        FavoritesManager favoritesManager,
        SystemManagerService systemManager,
        IFindCoverImageService findCoverImage,
        IMameDataService mameData,
        PlaySoundEffects playSoundEffects,
        IMessageBoxLibraryService messageBox,
        IConfiguration configuration,
        MainViewModel mainViewModel,
        ILogger logErrors,
        Services.LocalizationService localization)
    {
        _favoritesManager = favoritesManager;
        _systemManager = systemManager;
        _findCoverImage = findCoverImage;
        _mameData = mameData;
        _playSoundEffects = playSoundEffects;
        _messageBox = messageBox;
        _configuration = configuration;
        _mainViewModel = mainViewModel;
        _logErrors = logErrors;
        _localization = localization;
    }

    /// <summary>
    ///     Keeps the preview pane in sync with the selected row (WPF
    ///     SetPreviewImageOnSelectionChangedAsync parity). When the selection is cleared,
    ///     the preview resets to the placeholder.
    /// </summary>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedFavoriteChanged(FavoriteRowViewModel? oldValue, FavoriteRowViewModel? newValue)
    {
        PreviewImagePath = newValue?.CoverImage ?? "";
    }

    /// <summary>
    ///     Loads the favorites from the manager and enriches each row with machine
    ///     description, default emulator, and cover image.
    /// </summary>
    public async Task LoadFavoritesAsync()
    {
        try
        {
            IsLoading = true;
            LoadingMessage = "Loading favorites...";

            await Task.Yield();

            // WPF parity: reconcile favorites against the current system configuration so
            // entries whose system no longer exists are dropped instead of lingering and
            // failing to launch. Never let reconciliation (or any single bad entry) blank
            // the entire list — see the per-row guard below.
            try
            {
                // Only reconcile when systems are actually configured: an empty list (e.g. a
                // transient read failure) must never wipe every favorite as "missing systems".
                var validSystemNames = _systemManager.LoadSystems().ConvertAll(static s => s.SystemName);
                if (validSystemNames.Count != 0)
                {
                    var removedCount = await _favoritesManager.RemoveFavoritesForMissingSystemsAsync(validSystemNames);
                    if (removedCount > 0)
                    {
                        _logErrors.Information(
                            $"Removed {removedCount} favorite(s) referencing systems that no longer exist.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logErrors.Error(ex, "Error reconciling favorites against configured systems");
            }

            var rows = await Task.Run(() =>
            {
                var favorites = _favoritesManager.FavoriteList.ToList();
                var rows = new List<FavoriteRowViewModel>(favorites.Count);

                foreach (var favorite in favorites)
                {
                    try
                    {
                        // WPF parity: FileName is a bare file name resolved against the
                        // system folders (older entries may hold full paths — kept as-is).
                        var storedName = favorite.FileName ?? "";
                        var systemName = favorite.SystemName ?? "";
                        var system = _systemManager.GetSystem(systemName);
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(storedName) ?? storedName;

                        // Resolve the stored file name to a full path inside the system
                        // folders (the WPF favorites page shows FileName from the resolved
                        // path). Legacy full-path entries are kept as-is.
                        var resolvedPath = system is null
                            ? null
                            : PathHelper.FindFileInSystemFolders(system.SystemFolders, storedName);
                        var filePath = resolvedPath ?? storedName;

                        rows.Add(new FavoriteRowViewModel
                        {
                            StoredFileName = storedName,
                            FilePath = filePath,
                            SystemName = systemName,
                            MachineDescription =
                                _mameData.Lookup.TryGetValue(fileNameWithoutExtension, out var description)
                                    ? description
                                    : "",
                            DefaultEmulator = system?.Emulators.FirstOrDefault()?.EmulatorName ?? "No Default Emulator",
                            CoverImage = system is null
                                ? ""
                                : _findCoverImage.FindCoverImagePath(
                                    fileNameWithoutExtension, systemName, system.SystemImageFolder)
                        });
                    }
                    catch (Exception ex)
                    {
                        // A single corrupt favorite must not blank the whole list (the
                        // previous behavior). Log it and keep the healthy entries.
                        _logErrors.Error(ex,
                            "Error processing a favorite entry; skipping it. FileName={FileName}, System={System}",
                            favorite.FileName, favorite.SystemName);
                    }
                }

                return rows;
            });

            Favorites = new ObservableCollection<FavoriteRowViewModel>(rows);
            _logErrors.Information("Loaded {Count} favorite(s) in the Favorites section", rows.Count);
            SelectedFavorite = null;
            PreviewImagePath = "";
        }
        catch (Exception ex)
        {
            // Only a failure of the load machinery itself (not a single bad entry) reaches
            // here. Preserve whatever was already loaded rather than wiping the list.
            _logErrors.Error(ex, "Error loading favorites in the Favorites section");
            if (Favorites is null || Favorites.Count == 0) Favorites = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     Resolves a favorite row to an existing file path (WPF FindFileInSystemFolders parity).
    ///     The stored name is resolved against the system folders; falls back to the stored
    ///     value so legacy full-path entries keep working.
    /// </summary>
    public string? ResolveFavoritePath(FavoriteRowViewModel row)
    {
        var system = _systemManager.GetSystem(row.SystemName);
        if (system is null) return null;

        return PathHelper.FindFileInSystemFolders(system.SystemFolders, row.StoredFileName) ?? row.FilePath;
    }

    private string GetLogFilePath()
    {
        return PathHelper.ResolveLogFilePath(_configuration);
    }

    /// <summary>
    ///     Removes the given favorite rows (WPF RemoveFavoriteButton_ClickAsync parity:
    ///     trash sound, per-row removal, preview reset, main-view refresh).
    /// </summary>
    public async Task RemoveFavoritesAsync(IReadOnlyList<FavoriteRowViewModel> rows)
    {
        try
        {
            if (rows.Count == 0)
            {
                _mainViewModel.StatusText =
                    _localization.GetString("Selectafavoritetoremovefirst", "Select a favorite to remove first.");
                return;
            }

            _playSoundEffects.PlayTrashSound();

            foreach (var row in rows)
            {
                // WPF parity: favorites are matched by their stored file name (the
                // exact value persisted in favorites.dat — bare name or legacy full path).
                await _favoritesManager.RemoveFavoriteAsync(row.StoredFileName);
                Favorites.Remove(row);
            }

            _logErrors.Information("Removed {Count} favorite(s) in the Favorites section", rows.Count);

            SelectedFavorite = null;
            PreviewImagePath = "";

            var lastName = rows[^1].DisplayName;
            _mainViewModel.StatusText = rows.Count == 1
                ? $"Removed from favorites: {lastName}"
                : $"Removed {rows.Count} favorites";

            _mainViewModel.RefreshFavoritesAndHistory();
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "Error removing favorite from the Favorites section");
        }
    }

    [RelayCommand]
    private Task RemoveSelectedAsync()
    {
        return RemoveFavoritesAsync(SelectedFavorite is null ? [] : [SelectedFavorite]);
    }

    /// <summary>
    ///     Launches the selected favorite (WPF LaunchGameFromFavoriteAsync parity):
    ///     resolve against system folders, prompt to delete when the file is gone,
    ///     and report missing systems/emulators through message boxes.
    /// </summary>
    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        try
        {
            if (SelectedFavorite is not { } favorite)
            {
                _mainViewModel.StatusText =
                    _localization.GetString("Selectagametolaunchfirst", "Select a game to launch first.");
                return;
            }

            _playSoundEffects.PlayNotificationSound();

            var system = _systemManager.GetSystem(favorite.SystemName);
            if (system is null)
            {
                // Expected condition (favorite references a removed system).
                _logErrors.Information("[Favorites] systemManager is null for '{System}'", favorite.SystemName);
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(GetLogFilePath());
                return;
            }

            var filePath = ResolveFavoritePath(favorite);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                var result =
                    await _messageBox.FavoriteFileDoesNotExistAskToDeleteMessageBoxAsync(filePath ??
                        favorite.DisplayName);
                if (result == MessageBoxResult.Yes) await RemoveFavoritesAsync([favorite]);

                _logErrors.Information(
                    "[Favorites] File does not exist: {FileName} (system: {System}, resolved path: {Path})",
                    favorite.StoredFileName, favorite.SystemName, filePath ?? "not found");

                return;
            }

            var emulator = system.Emulators.FirstOrDefault();
            if (emulator is null)
            {
                // Expected condition (system has no emulators configured).
                _logErrors.Information("[Favorites] emulatorManager is null for '{System}'", favorite.SystemName);
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(GetLogFilePath());
                return;
            }

            await _mainViewModel.LaunchGameAtPathAsync(filePath, favorite.SystemName);
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "Error launching favorite from the Favorites section");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(GetLogFilePath());
        }
    }
}