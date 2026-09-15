using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.WpfServices;
using SimpleLauncher.ViewModels;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.Pages;

/// <summary>
///     Page displaying the user's game play history with filtering and context menu support.
/// </summary>
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
public partial class PlayHistoryPage : ILoadingState, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IContextMenuFunctions _contextMenuFunctions;
    private readonly IContextMenuService _contextMenuService;
    private readonly FavoritesManager _favoritesManager;
    private readonly IFindCoverImageService _findCoverImage;
    private readonly GameLauncherService _gameLauncher;
    private readonly GamePadController _gamePadController;
    private readonly ILogger _logger;
    private readonly IList<MameManagerService> _machines;
    private readonly MainWindow _mainWindow;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly SettingsManagerService _settings;
    private readonly PlayHistoryViewModel _viewModel;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PlayHistoryPage" /> class.
    /// </summary>
    /// <param name="systemManagers">The list of system manager configurations.</param>
    /// <param name="machines">The list of MAME machine definitions.</param>
    /// <param name="settings">The application settings manager.</param>
    /// <param name="favoritesManager">The manager for favorite game entries.</param>
    /// <param name="playHistoryManager">The manager for play history entries.</param>
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
    public PlayHistoryPage(IList<SystemManager> systemManagers,
        IList<MameManagerService> machines,
        SettingsManagerService settings,
        FavoritesManager favoritesManager,
        PlayHistoryManager playHistoryManager,
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

        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _gamePadController = gamePadController ?? throw new ArgumentNullException(nameof(gamePadController));
        _gameLauncher = gameLauncher ?? throw new ArgumentNullException(nameof(gameLauncher));
        _playSoundEffects = playSoundEffects ?? throw new ArgumentNullException(nameof(playSoundEffects));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _findCoverImage = findCoverImage ?? throw new ArgumentNullException(nameof(findCoverImage));
        _machines = machines ?? throw new ArgumentNullException(nameof(machines));
        _favoritesManager = favoritesManager ?? throw new ArgumentNullException(nameof(favoritesManager));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _playHistoryManager = playHistoryManager ?? throw new ArgumentNullException(nameof(playHistoryManager));
        _contextMenuFunctions = contextMenuFunctions ?? throw new ArgumentNullException(nameof(contextMenuFunctions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextMenuService = contextMenuService ?? throw new ArgumentNullException(nameof(contextMenuService));
        _messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();

        _viewModel = new PlayHistoryViewModel(
            configuration,
            logger,
            playHistoryManager,
            settings,
            systemManagers.ToList(),
            machines.ToList(),
            playSoundEffects,
            findCoverImage,
            imageLoader,
            _messageBox,
            App.ServiceProvider.GetRequiredService<IResourceProvider>());

        DataContext = _viewModel;

        _cancellationTokenSource = new CancellationTokenSource();

        Loaded += PlayHistoryPageLoadedAsync;
        Unloaded += PlayHistoryPage_Unloaded;
    }

    /// <summary>
    ///     Releases all resources used by the PlayHistoryPage.
    /// </summary>
    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Sets the loading state of the page, showing or hiding the loading overlay.
    /// </summary>
    /// <param name="isLoading">Whether the page is in a loading state.</param>
    /// <param name="message">The optional message to display while loading.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading) LoadingOverlay.Content = message;
    }

    private void PlayHistoryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    private async void PlayHistoryPageLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            // Wire Emergency Return Button from Template
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
                emergencyBtn.Click += EmergencyOverlayRelease_Click;

            await _viewModel.LoadHistoryAsync();

            // Bind to DataGrid
            PlayHistoryDataGrid.ItemsSource = _viewModel.PlayHistoryList;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the PlayHistoryPageLoadedAsync method");
        }
    }

    private async void PlayHistoryPrepareForRightClickContextAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var clickedElement = e.OriginalSource as FrameworkElement;
            if (clickedElement?.DataContext is not PlayHistoryItem selectedItem)
            {
                PlayHistoryDataGrid.ContextMenu = null;
                return;
            }

            if (selectedItem.FileName == null)
            {
                _logger.Warning("History item filename is null");
                await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
                return;
            }

            var systemManager = _viewModel.GetSystemManager(selectedItem.SystemName);
            if (systemManager == null)
            {
                _logger.Warning("systemManager is null");
                await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
                return;
            }

            if (!File.Exists(selectedItem.FileName))
            {
                var result = await _messageBox.GameFileDoesNotExistAskToDeleteMessageBoxAsync(selectedItem.FileName);
                if (result == CoreMessageBoxResult.Yes)
                {
                    _viewModel.RemoveItem(selectedItem);
                    _logger.Debug($"The entry {selectedItem} was removed from the history by user request.");
                }

                return;
            }

            var emulatorManager = systemManager.Emulators.FirstOrDefault();
            if (emulatorManager == null)
            {
                _logger.Warning("emulatorManager is null");
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            var context = new RightClickContext(
                selectedItem.FileName,
                selectedItem.FileName,
                Path.GetFileNameWithoutExtension(selectedItem.FileName),
                selectedItem.SystemName,
                systemManager,
                _machines.ToList(),
                _favoritesManager,
                _settings,
                null,
                null,
                emulatorManager,
                null,
                null,
                _mainWindow,
                _gamePadController,
                null,
                _gameLauncher,
                _playSoundEffects,
                this
            );

            var contextMenu =
                _contextMenuService.AddRightClickReturnContextMenu(context, _findCoverImage, _contextMenuFunctions);
            if (contextMenu != null)
            {
                PlayHistoryDataGrid.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "There was an error in the method PlayHistoryPrepareForRightClickContextAsync");
            await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
        }
    }

    private async Task LaunchGameFromHistoryAsync(string fileName, string selectedSystemName)
    {
        var selectedSystemManager = _viewModel.GetSystemManager(selectedSystemName);
        if (selectedSystemManager == null)
        {
            _logger.Warning("[LaunchGameFromHistoryAsync] systemManager is null");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
            return;
        }

        if (!File.Exists(fileName))
        {
            var result = await _messageBox.GameFileDoesNotExistAskToDeleteMessageBoxAsync(fileName);
            if (result == CoreMessageBoxResult.Yes)
            {
                var itemToRemove = _viewModel.PlayHistoryList.FirstOrDefault(item =>
                    item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                    && item.SystemName.Equals(selectedSystemName, StringComparison.OrdinalIgnoreCase));
                if (itemToRemove != null) _viewModel.RemoveItem(itemToRemove);
            }

            return;
        }

        var emulatorManager = selectedSystemManager.Emulators.FirstOrDefault();
        if (emulatorManager == null)
        {
            _logger.Warning("[LaunchGameFromHistoryAsync] emulatorManager is null");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
            return;
        }

        // Store current selection for restore after refresh
        var selectedItemIdentifier = PlayHistoryDataGrid.SelectedItem is PlayHistoryItem selected
            ? (selected.FileName, selected.SystemName)
            : (FileName: null, SystemName: null);

        var selectedEmulatorName = emulatorManager.EmulatorName;
        await _gameLauncher.HandleButtonClickAsync(fileName, selectedEmulatorName, selectedSystemName,
            selectedSystemManager, _settings, WpfWindowContext.FromMainWindow(_mainWindow), _gamePadController, this);

        // Refresh data and restore selection
        _viewModel.RefreshAfterGameLaunch();
        PlayHistoryDataGrid.ItemsSource = _viewModel.PlayHistoryList;

        if (selectedItemIdentifier is (not null, not null))
        {
            var updatedItem = _viewModel.PlayHistoryList.FirstOrDefault(item =>
                item.FileName.Equals(selectedItemIdentifier.FileName, StringComparison.OrdinalIgnoreCase) &&
                item.SystemName.Equals(selectedItemIdentifier.SystemName, StringComparison.OrdinalIgnoreCase));
            if (updatedItem != null)
            {
                PlayHistoryDataGrid.SelectedItem = updatedItem;
                PlayHistoryDataGrid.ScrollIntoView(updatedItem);
            }
        }
    }

    private async void LaunchGameWithDoubleClickAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (PlayHistoryDataGrid.SelectedItem is not PlayHistoryItem selectedItem) return;

            _playSoundEffects.PlayNotificationSound();
            await LaunchGameFromHistoryAsync(selectedItem.FileName, selectedItem.SystemName);
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
            if (PlayHistoryDataGrid.SelectedItem is not PlayHistoryItem selectedItem)
            {
                PreviewImage.Source = null;
                return;
            }

            await _viewModel.UpdatePreviewImageAsync(selectedItem.CoverImage);

            if (PlayHistoryDataGrid.SelectedItem == selectedItem)
                PreviewImage.Source = _viewModel.PreviewImageSource?.ToBitmapImage();
        }
        catch (Exception ex)
        {
            PreviewImage.Source = null;
            _logger.Error(ex, "Error in the SetPreviewImageOnSelectionChangedAsync method");
        }
    }

    private async void DeleteHistoryItemWithDelButtonAsync(object sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Delete:
                {
                    var selectedItems = PlayHistoryDataGrid.SelectedItems.Cast<PlayHistoryItem>().ToList();
                    if (selectedItems.Count > 0)
                    {
                        _playSoundEffects.PlayTrashSound();
                        _viewModel.RemoveItems(selectedItems);
                        e.Handled = true;
                        PreviewImage.Source = null;
                    }
                    else
                    {
                        await _messageBox.SelectAHistoryItemToRemoveMessageBoxAsync();
                    }

                    PreviewImage.Source = null;
                    break;
                }
                case Key.Enter when PlayHistoryDataGrid.SelectedItem is PlayHistoryItem selectedItem:
                    _playSoundEffects.PlayNotificationSound();
                    _ = LaunchGameFromHistoryAsync(selectedItem.FileName, selectedItem.SystemName);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    await _messageBox.SelectAGameToLaunchMessageBoxAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method DeleteHistoryItemWithDelButtonAsync");
        }
    }

    private void RestoreSelectionAfterSort((string? FileName, string? SystemName) selectedItemIdentifier)
    {
        if (selectedItemIdentifier.FileName == null || selectedItemIdentifier.SystemName == null) return;

        var updatedItem = _viewModel.PlayHistoryList.FirstOrDefault(item =>
            item.FileName.Equals(selectedItemIdentifier.FileName, StringComparison.OrdinalIgnoreCase) &&
            item.SystemName.Equals(selectedItemIdentifier.SystemName, StringComparison.OrdinalIgnoreCase));

        if (updatedItem != null)
        {
            PlayHistoryDataGrid.SelectedItem = updatedItem;
            PlayHistoryDataGrid.ScrollIntoView(updatedItem);
        }
    }

    private (string? FileName, string? SystemName) GetSelectedIdentifier()
    {
        return PlayHistoryDataGrid.SelectedItem is PlayHistoryItem selectedItem
            ? (selectedItem.FileName, selectedItem.SystemName)
            : (FileName: null, SystemName: null);
    }

    private void SortByDate_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("SortingPlayHistory") ?? "Sorting play history...");
        var identifier = GetSelectedIdentifier();

        _playSoundEffects.PlayNotificationSound();
        _viewModel.SortByDate();
        PlayHistoryDataGrid.ItemsSource = _viewModel.PlayHistoryList;

        RestoreSelectionAfterSort(identifier);
    }

    private void SortByTotalPlayTime_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("SortingPlayHistory") ?? "Sorting play history...");
        var identifier = GetSelectedIdentifier();

        _playSoundEffects.PlayNotificationSound();
        _viewModel.SortByTotalPlayTime();
        PlayHistoryDataGrid.ItemsSource = _viewModel.PlayHistoryList;

        RestoreSelectionAfterSort(identifier);
    }

    private void SortByTimesPlayed_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("SortingPlayHistory") ?? "Sorting play history...");
        var identifier = GetSelectedIdentifier();

        _playSoundEffects.PlayNotificationSound();
        _viewModel.SortByTimesPlayed();
        PlayHistoryDataGrid.ItemsSource = _viewModel.PlayHistoryList;

        RestoreSelectionAfterSort(identifier);
    }

    private async void RemoveHistoryItemButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedItems = PlayHistoryDataGrid.SelectedItems.Cast<PlayHistoryItem>().ToList();
            if (selectedItems.Count > 0)
            {
                _mainWindow.UpdateStatusBarService.UpdateContent(
                    (string)Application.Current.TryFindResource("RemovingHistoryItem") ?? "Removing history item...");
                _playSoundEffects.PlayTrashSound();
                _viewModel.RemoveItems(selectedItems);
                PreviewImage.Source = null;
            }
            else
            {
                await _messageBox.SelectAHistoryItemToRemoveMessageBoxAsync();
            }

            PreviewImage.Source = null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method RemoveHistoryItemButton_ClickAsync");
        }
    }

    private async void RemoveAllHistoryItemButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _mainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("RemovingAllHistoryItems") ??
                "Removing all history items...");
            await _viewModel.RemoveAllCommand.ExecuteAsync(null);
            PreviewImage.Source = null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method RemoveAllHistoryItemButton_ClickAsync");
        }
    }

    private async void LaunchGameClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (PlayHistoryDataGrid.SelectedItem is PlayHistoryItem selectedItem)
            {
                _playSoundEffects.PlayNotificationSound();
                await LaunchGameFromHistoryAsync(selectedItem.FileName, selectedItem.SystemName);
            }
            else
            {
                _mainWindow.UpdateStatusBarService.UpdateContent(
                    (string)Application.Current.TryFindResource("LaunchingGameFromHistory") ??
                    "Launching game from history...");
                await _messageBox.SelectAGameToLaunchMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the LaunchGameClickAsync method");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        _cancellationTokenSource?.Cancel();
        LoadingOverlay.Visibility = Visibility.Collapsed;

        _logger.Debug("[Emergency] User forced overlay dismissal in PlayHistoryPage");
        _mainWindow.UpdateStatusBarService.UpdateContent("Emergency reset performed.");
    }
}