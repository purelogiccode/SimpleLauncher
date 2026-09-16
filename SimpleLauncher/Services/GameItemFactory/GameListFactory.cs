using System.Globalization;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.MameManager;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.GameLauncher;
using SimpleLauncher.Services.LoadImages;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.SystemManager;
using SimpleLauncher.Services.WpfServices;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameItemFactory;

/// <summary>
///     Factory that creates <see cref="GameListViewItem" /> instances for the list view,
///     including play history, favorites status, and preview image loading.
/// </summary>
public class GameListFactory(
    ComboBox emulatorComboBox,
    ComboBox systemComboBox,
    IList<SystemManagerService> systemManagers,
    IList<MameManagerService> machines,
    SettingsManagerService settings,
    FavoritesManager favoritesManager,
    PlayHistoryManager playHistoryManager,
    MainWindow mainWindow,
    GamePadController gamePadController,
    GameLauncherService gameLauncher,
    IConfiguration configuration,
    ILogger logErrors,
    IGetListOfFilesService getListOfFiles,
    IFindCoverImageService findCoverImage,
    IImageLoader imageLoader,
    IMessageBoxLibraryService messageBox)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ComboBox _emulatorComboBox = emulatorComboBox;
    private readonly FavoritesManager _favoritesManager = favoritesManager;
    private readonly IFindCoverImageService _findCoverImage = findCoverImage;
    private readonly GameLauncherService _gameLauncher = gameLauncher;
    private readonly GamePadController _gamePadController = gamePadController;
    private readonly IGetListOfFilesService _getListOfFiles = getListOfFiles;
    private readonly IImageLoader _imageLoader = imageLoader;
    private readonly ILogger _logger = logErrors;
    private readonly IList<MameManagerService> _machines = machines;
    private readonly MainWindow _mainWindow = mainWindow;
    private readonly IMessageBoxLibraryService _messageBox = messageBox;
    private readonly PlayHistoryManager _playHistoryManager = playHistoryManager;

    // ReSharper disable once UnusedMember.Local
    private readonly SettingsManagerService _settings = settings;
    private readonly ComboBox _systemComboBox = systemComboBox;
    private readonly IList<SystemManagerService> _systemManagers = systemManagers;

    /// <summary>
    ///     Creates a <see cref="GameListViewItem" /> for the given game entity path,
    ///     populating it with favorites status, play history, and MAME description data.
    /// </summary>
    public Task<GameListViewItem> CreateGameListViewItemAsync(string entityPath, string systemName,
        SystemManagerService systemManager)
    {
        var isDirectory = Directory.Exists(entityPath);
        string fileNameWithoutExtension;
        string folderPath;

        var fileNameWithExtension = Path.GetFileName(entityPath);
        if (isDirectory)
        {
            fileNameWithoutExtension = fileNameWithExtension;
            folderPath = entityPath;
        }
        else
        {
            fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entityPath);
            folderPath = Path.GetDirectoryName(entityPath) ?? "";
        }

        var machineDescription = GetMachineDescription(fileNameWithoutExtension);

        var isFavorite = _favoritesManager.FavoriteList
            .Any(f => f.FileName.Equals(fileNameWithExtension, StringComparison.OrdinalIgnoreCase) &&
                      f.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));

        // Play history stores the full path in FileName (see AddOrUpdatePlayHistoryItem),
        // so it must be matched against entityPath, not the bare file name.
        var playHistoryItem = _playHistoryManager.PlayHistoryList
            .FirstOrDefault(h => h.FileName.Equals(entityPath, StringComparison.OrdinalIgnoreCase) &&
                                 h.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));

        var timesPlayed = "0";
        var playTime = "0m 0s";

        if (playHistoryItem != null)
        {
            var timeSpan = TimeSpan.FromSeconds(playHistoryItem.TotalPlayTime);
            playTime = timeSpan.TotalHours >= 1
                ? $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s"
                : $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            timesPlayed = playHistoryItem.TimesPlayed.ToString(CultureInfo.InvariantCulture);
        }

        // Create the GameListViewItem with initial data. FileSize will show "Calculating..."
        var gameListViewItem = new GameListViewItem
        {
            FileName = fileNameWithoutExtension,
            MachineDescription = machineDescription,
            FilePath = entityPath,
            FolderPath = folderPath,
            SystemName = systemName,
            IsFavorite = isFavorite,
            TimesPlayed = timesPlayed,
            PlayTime = playTime
        };

        // Return the item immediately. The file size will update in the UI when the task completes.
        return Task.FromResult(gameListViewItem);
    }

    /// <summary>
    ///     Updates the preview image in the main window when the selected game list item changes.
    /// </summary>
    public async Task HandleSelectionChangedAsync(GameListViewItem selectedItem)
    {
        try
        {
            // Ensure the MainWindow and its PreviewImage control are available.
            if (_mainWindow == null)
            {
                // Notify developer
                _logger.Error(
                    new InvalidOperationException(
                        "_mainWindow is null in GameListFactory.HandleSelectionChangedAsync."),
                    "MainWindow instance is null. Cannot update preview");

                return;
            }

            if (_mainWindow.PreviewImage == null)
            {
                // Notify developer
                _logger.Error(
                    new InvalidOperationException(
                        "_mainWindow.PreviewImage is null in GameListFactory.HandleSelectionChangedAsync."),
                    "PreviewImage control in MainWindow is null. Cannot update preview");

                return;
            }

            try
            {
                if (selectedItem == null)
                {
                    // No item selected, clear the preview image.
                    _mainWindow.PreviewImage.Source = null;
                    return;
                }

                var filePath = selectedItem.FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    // Notify developer
                    _logger.Error(
                        new ArgumentException(@"selectedItem.FilePath is null or empty.", nameof(selectedItem)),
                        "Selected item has an invalid file path. Cannot load preview");

                    _mainWindow.PreviewImage.Source = null; // Clear preview
                    var (defaultStream, _) = await _imageLoader.LoadImageAsync(null); // Load global default
                    _mainWindow.Dispatcher.Invoke(() =>
                        _mainWindow.PreviewImage.Source = defaultStream.ToBitmapImage());

                    return;
                }

                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                var selectedSystem = _systemComboBox.SelectedItem as string;
                if (string.IsNullOrEmpty(selectedSystem))
                {
                    // Notify developer
                    _logger.Error(new InvalidOperationException("Selected system name is null or empty from ComboBox."),
                        "No system selected or system name is invalid. Cannot load preview");

                    _mainWindow.PreviewImage.Source = null; // Clear preview
                    var (defaultStream, _) = await _imageLoader.LoadImageAsync(null); // Load global default
                    _mainWindow.Dispatcher.Invoke(() =>
                        _mainWindow.PreviewImage.Source = defaultStream.ToBitmapImage());

                    return;
                }

                var systemManager = _systemManagers?.FirstOrDefault(c =>
                    c.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
                if (systemManager == null)
                {
                    // Notify developer
                    _logger.Error(
                        new InvalidOperationException($"System configuration not found for '{selectedSystem}'."),
                        $"No system configuration for {selectedSystem}. Cannot load preview.");

                    _mainWindow.PreviewImage.Source = null; // Clear preview
                    var (defaultStream, _) = await _imageLoader.LoadImageAsync(null); // Load global default
                    _mainWindow.Dispatcher.Invoke(() =>
                        _mainWindow.PreviewImage.Source = defaultStream.ToBitmapImage());

                    return;
                }

                var isDirectory = Directory.Exists(filePath);

                var previewImagePath = _findCoverImage.FindCoverImagePath(fileNameWithoutExtension, selectedSystem,
                    systemManager.SystemImageFolder);
                if (isDirectory) // GroupByFolder is true
                {
                    // First, try to find an image with the same name as the folder name.
                    // If the found path is a default image, try the fallback logic.
                    if (previewImagePath.EndsWith("default.png", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fallback to current logic: look inside the folder for a file to use as a name.
                        var filesInFolder = await _getListOfFiles.GetFilesAsync(filePath,
                            systemManager.FileFormatsToSearch, systemManager.DisableRecursiveSearch,
                            systemManager.GroupByFolder);
                        if (filesInFolder.Count != 0)
                        {
                            var representativeFileName = Path.GetFileNameWithoutExtension(filesInFolder[0]);
                            // Now search again with the new name.
                            previewImagePath = _findCoverImage.FindCoverImagePath(representativeFileName,
                                selectedSystem, systemManager.SystemImageFolder);
                        }
                    }
                }

                // This is the logic for non-grouped files, which remains the same.
                _mainWindow.PreviewImage.Source = null; // Clear existing image before loading new one

                if (!string.IsNullOrEmpty(previewImagePath))
                    previewImagePath = PathHelper.ResolveRelativeToAppDirectory(previewImagePath);

                var (imageStream, _) =
                    await _imageLoader.LoadImageAsync(
                        previewImagePath); // LoadImageAsync handles null/empty path by returning default

                _mainWindow.Dispatcher.Invoke(() =>
                {
                    // Race condition check: Only assign if the selected item hasn't changed
                    if (_mainWindow.GameDataGrid.SelectedItem == selectedItem)
                        _mainWindow.PreviewImage.Source = imageStream.ToBitmapImage();
                });
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex, "Error loading preview image");

                // Attempt to set a default image in case of any error during the process
                try
                {
                    var (defaultImageStream, _) = await _imageLoader.LoadImageAsync(null); // Load global default
                    _mainWindow.Dispatcher.Invoke(() =>
                    {
                        // Race condition check: Only assign if the selected item hasn't changed (or is now null)
                        if (_mainWindow.GameDataGrid.SelectedItem == selectedItem ||
                            _mainWindow.GameDataGrid.SelectedItem == null)
                        {
                            _mainWindow?.PreviewImage?.Source = defaultImageStream.ToBitmapImage();
                        }
                    });
                }
                catch (Exception fallbackEx)
                {
                    // Notify developer
                    _logger.Error(fallbackEx, "Error loading fallback preview image after an initial error");
                }
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error in method GameListFactory.HandleSelectionChangedAsync");
        }
    }

    /// <summary>
    ///     get machine description.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    private string GetMachineDescription(string fileName)
    {
        var machine = _machines.FirstOrDefault(m => m.MachineName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        return machine?.Description ?? "";
    }

    /// <summary>
    ///     Launches the selected game using the currently chosen emulator and system configuration.
    /// </summary>
    public async Task HandleDoubleClickAsync(GameListViewItem selectedItem)
    {
        if (selectedItem == null)
        {
            // Notify developer
            _logger.Warning("selectedItem is null");

            // Notify user
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            return;
        }

        var filePath = selectedItem.FilePath;
        var selectedEmulatorName = _emulatorComboBox.SelectedItem as string;
        var selectedSystemName = _systemComboBox.SelectedItem as string;
        var selectedSystemManager = _systemManagers.FirstOrDefault(c =>
            c.SystemName.Equals(selectedSystemName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(filePath))
        {
            // Notify developer
            _logger.Warning("[HandleDoubleClickAsync] filepath is null or empty");

            // Notify user
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            return;
        }

        if (string.IsNullOrEmpty(selectedEmulatorName))
        {
            // Notify developer
            _logger.Warning("[HandleDoubleClickAsync] selectedEmulatorName is null or empty");

            // Notify user
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            return;
        }

        if (string.IsNullOrEmpty(selectedSystemName))
        {
            // Notify developer
            _logger.Warning("[HandleDoubleClickAsync] selectedSystemName is null or empty");

            // Notify user
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            return;
        }

        if (selectedSystemManager == null)
        {
            // Notify developer
            _logger.Warning("[HandleDoubleClickAsync] selectedSystemManager is null");

            // Notify user
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            return;
        }

        await _gameLauncher.HandleButtonClickAsync(filePath, selectedEmulatorName, selectedSystemName,
            selectedSystemManager, _settings, WpfWindowContext.FromMainWindow(_mainWindow), _gamePadController, null);
    }
}