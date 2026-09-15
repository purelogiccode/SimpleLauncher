using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.MameManager;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Models;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.GameLauncher;
using SimpleLauncher.Services.LoadImages;
using SimpleLauncher.Services.WpfServices;
using SimpleLauncher.ViewModels;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.Pages;

/// <summary>
///     Page displaying the user's favorite games with launching, sorting, and context menu support.
/// </summary>
internal partial class FavoritesPage : ILoadingState, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IContextMenuFunctions _contextMenuFunctions;
    private readonly IContextMenuService _contextMenuService;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly GameLauncherService _gameLauncher;
    private readonly GamePadController _gamePadController;
    private readonly ILogger _logger;
    private readonly List<MameManagerService> _machines;
    private readonly MainWindow _mainWindow;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly SettingsManagerService _settings;
    private readonly FavoritesViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FavoritesPage" /> class.
    /// </summary>
    /// <param name="settings">The application settings manager.</param>
    /// <param name="systemManagers">The list of system manager configurations.</param>
    /// <param name="machines">The list of MAME machine definitions.</param>
    /// <param name="favoritesManager">The manager for favorite game entries.</param>
    /// <param name="mainWindow">The main application window.</param>
    /// <param name="gamePadController">The gamepad input controller.</param>
    /// <param name="gameLauncher">The service used to launch games.</param>
    /// <param name="playSoundEffects">The service for playing sound effects.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="findCoverImage">The service for finding game cover images.</param>
    /// <param name="imageLoader">The service for loading images.</param>
    /// <param name="contextMenuFunctions">The service providing context menu operations.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="contextMenuService">The service for building context menus.</param>
    internal FavoritesPage(
        SettingsManagerService settings,
        List<SystemManager> systemManagers,
        List<MameManagerService> machines,
        FavoritesManager favoritesManager,
        MainWindow mainWindow,
        GamePadController gamePadController,
        GameLauncherService gameLauncher,
        PlaySoundEffects playSoundEffects,
        IConfiguration configuration,
        IFindCoverImageService findCoverImage,
        IImageLoader imageLoader,
        IContextMenuFunctions contextMenuFunctions,
        ILogger logger,
        IContextMenuService contextMenuService)
    {
        InitializeComponent();

        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _gamePadController = gamePadController ?? throw new ArgumentNullException(nameof(gamePadController));
        _gameLauncher = gameLauncher ?? throw new ArgumentNullException(nameof(gameLauncher));
        _playSoundEffects = playSoundEffects ?? throw new ArgumentNullException(nameof(playSoundEffects));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _findCoverImage = findCoverImage ?? throw new ArgumentNullException(nameof(findCoverImage));
        _machines = machines ?? throw new ArgumentNullException(nameof(machines));
        _favoritesManager = favoritesManager ?? throw new ArgumentNullException(nameof(favoritesManager));
        _contextMenuFunctions = contextMenuFunctions ?? throw new ArgumentNullException(nameof(contextMenuFunctions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextMenuService = contextMenuService ?? throw new ArgumentNullException(nameof(contextMenuService));
        _messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();

        _viewModel = new FavoritesViewModel(
            configuration,
            logger,
            favoritesManager,
            settings,
            systemManagers,
            machines,
            playSoundEffects,
            findCoverImage,
            imageLoader,
            _messageBox,
            App.ServiceProvider.GetRequiredService<IResourceProvider>());

        DataContext = _viewModel;

        Loaded += FavoritesPageLoadedAsync;
    }

    /// <summary>
    ///     Releases all resources used by the FavoritesPage.
    /// </summary>
    public void Dispose()
    {
        _viewModel.Dispose();
    }

    /// <summary>
    ///     Sets the loading state of the page, showing or hiding the loading overlay.
    /// </summary>
    /// <param name="isLoading">Whether the page is in a loading state.</param>
    /// <param name="message">The optional message to display while loading.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading)
            LoadingOverlay.Content = message ?? (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
    }

    private async void FavoritesPageLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            // Wire Emergency Return Button from Template
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
                emergencyBtn.Click += EmergencyOverlayRelease_Click;

            await _viewModel.LoadFavoritesAsync();

            // Bind the loaded favorites to the DataGrid
            FavoritesDataGrid.ItemsSource = _viewModel.Favorites;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the FavoritesPageLoadedAsync method");
        }
    }

    private async void RemoveFavoriteButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedItems = FavoritesDataGrid.SelectedItems.Cast<Favorite>().ToList();
            if (selectedItems.Count == 0)
            {
                await _messageBox.SelectAFavoriteToRemoveMessageBoxAsync();
                return;
            }

            _playSoundEffects.PlayTrashSound();

            foreach (var favorite in selectedItems) _viewModel.RemoveFavoriteFromCollection(favorite);

            PreviewImage.Source = null;
            FavoritesDataGrid.ContextMenu = null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method RemoveFavoriteButton_ClickAsync");
        }
    }

    private async void FavoritesPrepareForRightClickContextMenuAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var hitTestResult = VisualTreeHelper.HitTest(FavoritesDataGrid, e.GetPosition(FavoritesDataGrid));
            if (hitTestResult?.VisualHit == null) return;

            var visual = hitTestResult.VisualHit;
            DataGridRow? dataGridRow = null;
            while (visual != null && !Equals(visual, FavoritesDataGrid))
            {
                if (visual is DataGridRow row)
                {
                    dataGridRow = row;
                    break;
                }

                visual = VisualTreeHelper.GetParent(visual);
            }

            if (dataGridRow == null) return;

            dataGridRow.IsSelected = true;

            if (FavoritesDataGrid.SelectedItem is not Favorite selectedFavorite) return;

            var systemManager = _viewModel.GetSystemManager(selectedFavorite.SystemName);
            if (systemManager == null)
            {
                // Expected condition (favorite references a removed system; user is notified below):
                // not a bug, keep it out of the bug report service.
                _logger.Information("systemManager is null for the selected favorite");
                await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
                return;
            }

            var filePath = PathHelper.FindFileInSystemFolders(systemManager.SystemFolders, selectedFavorite.FileName);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                var result =
                    await _messageBox.FavoriteFileDoesNotExistAskToDeleteMessageBoxAsync(filePath ??
                        selectedFavorite.FileName);
                if (result == CoreMessageBoxResult.Yes) _viewModel.RemoveFavoriteFromCollection(selectedFavorite);

                return;
            }

            var emulatorManager = systemManager.Emulators.FirstOrDefault();
            if (emulatorManager == null)
            {
                // Expected condition (system has no emulators configured; user is notified below).
                _logger.Information("emulatorManager is null");
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            void OnRemovedCallback()
            {
                _viewModel.RemoveFavoriteFromCollection(selectedFavorite);
            }

            var context = new RightClickContext(
                filePath,
                selectedFavorite.FileName,
                Path.GetFileNameWithoutExtension(selectedFavorite.FileName),
                selectedFavorite.SystemName,
                systemManager,
                _machines,
                _favoritesManager,
                _settings,
                null,
                selectedFavorite,
                emulatorManager,
                null,
                null,
                _mainWindow,
                _gamePadController,
                OnRemovedCallback,
                _gameLauncher,
                _playSoundEffects,
                this
            );

            var contextMenu =
                _contextMenuService.AddRightClickReturnContextMenu(context, _findCoverImage, _contextMenuFunctions);
            if (contextMenu != null)
            {
                // Close the previous context menu before assigning a new one to prevent leaks.
                if (FavoritesDataGrid.ContextMenu is { IsOpen: true } oldMenu) oldMenu.IsOpen = false;

                FavoritesDataGrid.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "There was an error in the right-click context menu");
            await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
        }
    }

    private async void LaunchGameClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (FavoritesDataGrid.SelectedItem is not Favorite selectedFavorite)
            {
                await _messageBox.SelectAGameToLaunchMessageBoxAsync();
                return;
            }

            _playSoundEffects.PlayNotificationSound();
            await LaunchGameFromFavoriteAsync(selectedFavorite.FileName, selectedFavorite.SystemName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method LaunchGameClickAsync");
        }
    }

    private async Task LaunchGameFromFavoriteAsync(string fileName, string selectedSystemName)
    {
        try
        {
            var selectedSystemManager = _viewModel.GetSystemManager(selectedSystemName);
            if (selectedSystemManager == null)
            {
                // Expected condition (favorite references a removed system; user is notified below).
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
                    var favoriteToRemove = _viewModel.Favorites.FirstOrDefault(fav =>
                        fav.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                        && fav.SystemName.Equals(selectedSystemName, StringComparison.OrdinalIgnoreCase));
                    if (favoriteToRemove != null) _viewModel.RemoveFavoriteFromCollection(favoriteToRemove);
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
                return;
            }

            var selectedEmulatorName = emulatorManager.EmulatorName;
            await _gameLauncher.HandleButtonClickAsync(filePath, selectedEmulatorName, selectedSystemName,
                selectedSystemManager, _settings, WpfWindowContext.FromMainWindow(_mainWindow), _gamePadController,
                this);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[LaunchGameFromFavoritesAsync] Error launching: {fileName}, {selectedSystemName}");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    private async void LaunchGameWithDoubleClickAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (FavoritesDataGrid.SelectedItem is not Favorite selectedFavorite) return;

            _playSoundEffects.PlayNotificationSound();
            await LaunchGameFromFavoriteAsync(selectedFavorite.FileName, selectedFavorite.SystemName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method MouseDoubleClick");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    private async void SetPreviewImageOnSelectionChangedAsync(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (FavoritesDataGrid.SelectedItem is not Favorite selectedFavorite)
            {
                PreviewImage.Source = null;
                FavoritesDataGrid.ContextMenu = null;
                return;
            }

            await _viewModel.UpdatePreviewImageAsync(selectedFavorite.CoverImage);

            // Convert Stream to BitmapImage for WPF display
            if (FavoritesDataGrid.SelectedItem == selectedFavorite)
                PreviewImage.Source = _viewModel.PreviewImageSource?.ToBitmapImage();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the SetPreviewImageOnSelectionChangedAsync method");
        }
    }

    private async void FavoritesDataGrid_PreviewKeyDownAsync(object sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Delete:
                {
                    e.Handled = true;

                    var selectedItems = FavoritesDataGrid.SelectedItems.Cast<Favorite>().ToList();
                    if (selectedItems.Count > 0)
                    {
                        _playSoundEffects.PlayTrashSound();
                        foreach (var favorite in selectedItems) _viewModel.RemoveFavoriteFromCollection(favorite);

                        PreviewImage.Source = null;
                        FavoritesDataGrid.ContextMenu = null;
                    }
                    else
                    {
                        await _messageBox.SelectAFavoriteToRemoveMessageBoxAsync();
                    }

                    break;
                }
                case Key.Enter:
                {
                    e.Handled = true;
                    if (FavoritesDataGrid.SelectedItem is Favorite selectedFavorite)
                    {
                        _playSoundEffects.PlayNotificationSound();
                        await LaunchGameFromFavoriteAsync(selectedFavorite.FileName, selectedFavorite.SystemName);
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling key press in FavoritesDataGrid");
        }
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        LoadingOverlay.Visibility = Visibility.Collapsed;

        _logger.Debug("[Emergency] User forced overlay dismissal in FavoritesPage");
        _mainWindow.UpdateStatusBarService.UpdateContent("Emergency reset performed.");
    }
}