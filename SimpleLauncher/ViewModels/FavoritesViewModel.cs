using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.MameManager;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Services.Favorites;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the favorites window, managing favorite games list, preview images, and launching.
/// </summary>
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
public partial class FavoritesViewModel : ObservableObject, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly IImageLoader _imageLoader;
    private readonly ILogger _logger;
    private readonly IList<MameManagerService> _machines;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly IResourceProvider _resourceProvider;
    private readonly SettingsManagerService _settings;
    private readonly IList<SystemManager> _systemManagers;

    [ObservableProperty] public partial ObservableCollection<Favorite> Favorites { get; set; } = [];

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "";

    [ObservableProperty] public partial Stream? PreviewImageSource { get; set; }

    [ObservableProperty] public partial Favorite? SelectedFavorite { get; set; }

    /// <summary>Initializes a new instance of the <see cref="FavoritesViewModel" />.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="favoritesManager">The favorites manager for persistence.</param>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="systemManagers">The list of configured system managers.</param>
    /// <param name="machines">The list of MAME machine definitions.</param>
    /// <param name="playSoundEffects">The sound effects service.</param>
    /// <param name="findCoverImage">The cover image lookup service.</param>
    /// <param name="imageLoader">The image loader service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public FavoritesViewModel(
        IConfiguration configuration,
        ILogger logErrors,
        FavoritesManager favoritesManager,
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
        _favoritesManager = favoritesManager;
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

    /// <summary>Loads all favorites with cover images and machine descriptions.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadFavoritesAsync()
    {
        try
        {
            IsLoading = true;
            LoadingMessage = _resourceProvider.GetString("LoadingFavorites", "Loading favorites...");

            await Task.Yield();

            // Reconcile favorites against the current system configuration: entries whose
            // system no longer exists (renamed without migration, or deleted) would otherwise
            // fail to launch with a missing system manager.
            // Only reconcile when systems are actually loaded: an empty list must never be
            // interpreted as "every favorite belongs to a missing system" (which would wipe them).
            var validSystemNames = _systemManagers.Select(static manager => manager.SystemName).ToList();
            if (validSystemNames.Count != 0)
            {
                var removedCount = await _favoritesManager.RemoveFavoritesForMissingSystemsAsync(validSystemNames);
                if (removedCount > 0)
                    _logger.Information(
                        $"Removed {removedCount} favorite(s) referencing systems that no longer exist.");
            }

            var favoritesSnapshot = _favoritesManager.FavoriteList.ToList();
            var systemManagersSnapshot = _systemManagers.ToList();
            var machinesSnapshot = _machines.ToList();

            var processedFavorites = await Task.Run(() =>
            {
                var processedList = new List<Favorite>();
                foreach (var favoriteConfigItem in favoritesSnapshot)
                {
                    var machine = machinesSnapshot.FirstOrDefault(m =>
                        m.MachineName.Equals(Path.GetFileNameWithoutExtension(favoriteConfigItem.FileName),
                            StringComparison.OrdinalIgnoreCase));

                    var machineDescription = machine?.Description ?? "";

                    var systemManager = systemManagersSnapshot.FirstOrDefault(manager =>
                        manager.SystemName.Equals(favoriteConfigItem.SystemName, StringComparison.OrdinalIgnoreCase));

                    var defaultEmulator = systemManager?.Emulators.FirstOrDefault()?.EmulatorName
                                          ?? _resourceProvider.GetString("UnknownString", "Unknown");

                    var coverImagePath = GetCoverImagePath(favoriteConfigItem.SystemName, favoriteConfigItem.FileName);

                    processedList.Add(new Favorite
                    {
                        FileName = favoriteConfigItem.FileName,
                        SystemName = favoriteConfigItem.SystemName,
                        MachineDescription = machineDescription,
                        DefaultEmulator = defaultEmulator,
                        CoverImage = coverImagePath
                    });
                }

                return processedList;
            });

            Favorites = new ObservableCollection<Favorite>(processedFavorites);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading favorites data in FavoritesViewModel");
            await _messageBox.ErrorWhileAddingFavoritesMessageBoxAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveFavoriteAsync()
    {
        try
        {
            if (SelectedFavorite == null)
            {
                await _messageBox.SelectAFavoriteToRemoveMessageBoxAsync();
                return;
            }

            _playSoundEffects.PlayTrashSound();

            Favorites.Remove(SelectedFavorite);

            UpdateFavoritesManagerList();

            PreviewImageSource = null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in RemoveFavoriteAsync");
        }
    }

    [RelayCommand]
    private async Task LaunchGameAsync()
    {
        try
        {
            if (SelectedFavorite == null)
            {
                await _messageBox.SelectAGameToLaunchMessageBoxAsync();
                return;
            }

            _playSoundEffects.PlayNotificationSound();
            await LaunchGameFromFavoriteAsync(SelectedFavorite.FileName, SelectedFavorite.SystemName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in LaunchGameAsync");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    /// <summary>Launches a game from the favorites list by file name and system name.</summary>
    /// <param name="fileName">The ROM file name.</param>
    /// <param name="selectedSystemName">The system name associated with the game.</param>
    /// <param name="loadingStateProvider">Optional loading state provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LaunchGameFromFavoriteAsync(string fileName, string selectedSystemName,
        ILoadingState? loadingStateProvider = null)
    {
        try
        {
            var selectedSystemManager = _systemManagers.FirstOrDefault(manager =>
                manager.SystemName.Equals(selectedSystemName, StringComparison.OrdinalIgnoreCase));
            if (selectedSystemManager == null)
            {
                // Expected condition (favorite references a removed system; user is notified below):
                // not a bug, keep it out of the bug report service.
                _logger.Information("[LaunchGameFromFavoritesAsync] selectedSystemManager is null");
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            var filePath = PathHelper.FindFileInSystemFolders(selectedSystemManager.SystemFolders, fileName);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                var result = await _messageBox.FavoriteFileDoesNotExistAskToDeleteMessageBoxAsync(filePath ?? fileName);
                if (result == CoreMessageBoxResult.Yes)
                {
                    var favoriteToRemove = Favorites.FirstOrDefault(fav =>
                        fav.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                        && fav.SystemName.Equals(selectedSystemName, StringComparison.OrdinalIgnoreCase));
                    if (favoriteToRemove != null) RemoveFavoriteFromCollection(favoriteToRemove);
                }

                _logger.Information(
                    $"[LaunchGameFromFavoritesAsync] File does not exist: {fileName} (system: {selectedSystemName}, resolved path: {filePath ?? "not found"})");
                return;
            }

            var emulatorManager = selectedSystemManager.Emulators.FirstOrDefault();
            if (emulatorManager == null)
            {
                _logger.Information("[LaunchGameFromFavoritesAsync] emulatorManager is null");
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
            }

            // Game launching is handled by the caller (code-behind) since it needs WPF Window context
            // This method provides the data needed for launching
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[LaunchGameFromFavoritesAsync] Error launching: {fileName}, {selectedSystemName}");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
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
        }
    }

    /// <summary>Removes a favorite from the observable collection and persists the change.</summary>
    /// <param name="favorite">The favorite to remove.</param>
    public void RemoveFavoriteFromCollection(Favorite favorite)
    {
        Favorites.Remove(favorite);
        UpdateFavoritesManagerList();
        PreviewImageSource = null;
    }

    private void UpdateFavoritesManagerList()
    {
        _favoritesManager.FavoriteList.Clear();
        foreach (var favorite in Favorites) _favoritesManager.FavoriteList.Add(favorite);

        _favoritesManager.SaveFavoritesAsync();
    }

    private string GetCoverImagePath(string systemName, string fileName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var systemManager = _systemManagers.FirstOrDefault(manager =>
            manager.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));
        var defaultImagePath = Path.Combine(baseDirectory, "images", "default.png");

        if (systemManager == null) return defaultImagePath;

        return _findCoverImage.FindCoverImagePath(fileNameWithoutExtension, systemName,
            systemManager.SystemImageFolder);
    }

    /// <summary>Gets the system manager for the specified system name.</summary>
    /// <param name="systemName">The system name to look up.</param>
    /// <returns>The matching <see cref="SystemManager" />, or <c>null</c> if not found.</returns>
    public SystemManager? GetSystemManager(string systemName)
    {
        return _systemManagers.FirstOrDefault(manager =>
            manager.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));
    }
}