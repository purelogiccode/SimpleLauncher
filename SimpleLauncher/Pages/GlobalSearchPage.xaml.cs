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
using SimpleLauncher.Services.WpfServices;
using SimpleLauncher.ViewModels;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;

namespace SimpleLauncher.Pages;

/// <summary>
///     Page for searching games across all systems with filtering, launching, and context menu support.
/// </summary>
internal partial class GlobalSearchPage : IDisposable, ILoadingState
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
    private readonly GlobalSearchViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GlobalSearchPage" /> class.
    /// </summary>
    /// <param name="systemManagers">The list of system manager configurations.</param>
    /// <param name="machines">The list of MAME machine definitions.</param>
    /// <param name="mameLookup">The dictionary mapping MAME ROM names to descriptions.</param>
    /// <param name="favoritesManager">The manager for favorite game entries.</param>
    /// <param name="settings">The application settings manager.</param>
    /// <param name="mainWindow">The main application window.</param>
    /// <param name="gamePadController">The gamepad input controller.</param>
    /// <param name="gameLauncher">The service used to launch games.</param>
    /// <param name="playSoundEffects">The service for playing sound effects.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="getListOfFiles">The service for retrieving lists of files.</param>
    /// <param name="findCoverImage">The service for finding game cover images.</param>
    /// <param name="imageLoader">The service for loading images.</param>
    /// <param name="contextMenuFunctions">The service providing context menu operations.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="contextMenuService">The service for building context menus.</param>
    public GlobalSearchPage(
        List<SystemManager> systemManagers,
        List<MameManagerService> machines,
        Dictionary<string, string> mameLookup,
        FavoritesManager favoritesManager,
        SettingsManagerService settings,
        MainWindow mainWindow,
        GamePadController gamePadController,
        GameLauncherService gameLauncher,
        PlaySoundEffects playSoundEffects,
        IConfiguration configuration,
        IGetListOfFilesService getListOfFiles,
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
        _contextMenuFunctions = contextMenuFunctions ?? throw new ArgumentNullException(nameof(contextMenuFunctions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextMenuService = contextMenuService ?? throw new ArgumentNullException(nameof(contextMenuService));
        _messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();

        _viewModel = new GlobalSearchViewModel(
            configuration,
            logger,
            settings,
            systemManagers,
            machines,
            mameLookup,
            favoritesManager,
            playSoundEffects,
            getListOfFiles,
            findCoverImage,
            imageLoader,
            _messageBox,
            App.ServiceProvider.GetRequiredService<IResourceProvider>());

        DataContext = _viewModel;

        // Populate System ComboBox
        SystemComboBox.ItemsSource = _viewModel.SystemNames;
        SystemComboBox.SelectedIndex = 0;

        Loaded += (_, _) =>
        {
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
                emergencyBtn.Click += EmergencyOverlayRelease_Click;
        };

        Unloaded += GlobalSearchPage_Unloaded;
    }

    /// <summary>
    ///     Releases all resources used by the GlobalSearchPage.
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
        Dispatcher.Invoke(() =>
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            if (isLoading) LoadingOverlay.Content = message;
        });
    }

    private void GlobalSearchPage_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.CancelSearch();
            ResultsDataGrid.ItemsSource = null;
            _viewModel.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error cleaning up resources on page unload");
        }
    }

    private async void SearchButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();

            var searchTerm = SearchTextBox.Text;
            var selectedSystem = SystemComboBox.SelectedItem as string;
            var searchFilename = SearchFilenameCheckBox.IsChecked == true;
            var searchMameDescription = SearchMameDescriptionCheckBox.IsChecked == true;
            var searchFolderName = SearchFolderNameCheckBox.IsChecked == true;
            var searchRecursively = SearchRecursivelyCheckBox.IsChecked == true;

            SetLoadingState(true, "Searching... Please wait.");
            await Task.Yield();

            try
            {
                await _viewModel.SearchAsync(searchTerm, selectedSystem, searchFilename, searchMameDescription,
                    searchFolderName, searchRecursively);

                // Update UI after search
                ResultsDataGrid.ItemsSource = _viewModel.SearchResults;
                NoResultsMessageOverlay.Visibility =
                    _viewModel.NoResultsVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                SetLoadingState(false);
            }
        }
        catch (Exception ex)
        {
            SetLoadingState(false);
            _logger.Error(ex, "Error in SearchButtonClickAsync");
        }
    }

    private void SearchWhenPressEnterKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SearchButtonClickAsync(sender, e);
    }

    private async void LaunchButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ResultsDataGrid.SelectedItem is SearchResult selectedResult &&
                !string.IsNullOrEmpty(selectedResult.FilePath))
            {
                _playSoundEffects.PlayNotificationSound();
                await LaunchGameFromSearchResultAsync(selectedResult.FilePath, selectedResult.SystemName,
                    selectedResult.EmulatorManager);
            }
            else
            {
                await _messageBox.SelectAGameToLaunchMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in LaunchButton_ClickAsync (GlobalSearch)");
            await _messageBox.ErrorLaunchingGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    private async Task LaunchGameFromSearchResultAsync(string filePath, string selectedSystemName,
        Emulator? selectedEmulatorManager)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(selectedSystemName) ||
                selectedEmulatorManager == null)
            {
                _logger.Warning(
                    "[LaunchGameFromSearchResultAsync] filePath or selectedSystemName or selectedEmulatorManager is null");
                await _messageBox.ErrorLaunchingGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            var selectedSystemManager = _viewModel.GetSystemManager(selectedSystemName);
            if (selectedSystemManager == null)
            {
                _logger.Warning("[LaunchGameFromSearchResultAsync] System manager not found");
                await _messageBox.ErrorLaunchingGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            await _gameLauncher.HandleButtonClickAsync(filePath, selectedEmulatorManager.EmulatorName,
                selectedSystemName,
                selectedSystemManager, _settings, WpfWindowContext.FromMainWindow(_mainWindow), _gamePadController,
                this);
        }
        catch (Exception ex)
        {
            _logger.Error(ex,
                $"[LaunchGameFromSearchResultAsync] Error launching: {filePath}, System: {selectedSystemName}");
            await _messageBox.ErrorLaunchingGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    private async void GlobalSearchPrepareForRightClickContextMenuAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (ResultsDataGrid.SelectedItem is not SearchResult selectedResult ||
                string.IsNullOrEmpty(selectedResult.FilePath))
            {
                return;
            }

            var systemManager = _viewModel.GetSystemManager(selectedResult.SystemName);
            if (systemManager == null)
            {
                _logger.Warning("SystemManager is null");
                await _messageBox.ErrorLaunchingGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            if (string.IsNullOrEmpty(selectedResult.FilePath) || string.IsNullOrEmpty(selectedResult.SystemName) ||
                selectedResult.EmulatorManager == null)
            {
                _logger.Warning("FilePath, SystemName, or EmulatorManager is null");
                await _messageBox.ErrorLaunchingGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            var context = new RightClickContext(
                selectedResult.FilePath,
                selectedResult.FileNameWithExtension,
                selectedResult.FileName,
                selectedResult.SystemName,
                systemManager,
                _machines,
                _favoritesManager,
                _settings,
                null,
                null,
                selectedResult.EmulatorManager,
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
                ResultsDataGrid.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in GlobalSearch right-click context menu");
            await _messageBox.RightClickContextMenuErrorMessageBoxAsync();
        }
    }

    private async void ResultsDataGrid_MouseDoubleClickAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (ResultsDataGrid.SelectedItem is not SearchResult selectedResult ||
                string.IsNullOrEmpty(selectedResult.FilePath))
            {
                return;
            }

            _playSoundEffects.PlayNotificationSound();
            await LaunchGameFromSearchResultAsync(selectedResult.FilePath, selectedResult.SystemName,
                selectedResult.EmulatorManager);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in ResultsDataGrid_MouseDoubleClickAsync (GlobalSearch)");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    private async void ActionsWhenUserSelectAResultItemAsync(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (ResultsDataGrid.SelectedItem is SearchResult selectedResult &&
                !string.IsNullOrEmpty(selectedResult.FilePath))
            {
                LaunchButton.IsEnabled = true;
                await _viewModel.UpdatePreviewImageAsync(selectedResult.CoverImage);

                if (ResultsDataGrid.SelectedItem == selectedResult)
                    PreviewImage.Source = _viewModel.PreviewImageSource?.ToBitmapImage();
            }
            else
            {
                LaunchButton.IsEnabled = false;
                PreviewImage.Source = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading image in ActionsWhenUserSelectAResultItemAsync (GlobalSearch)");
            PreviewImage.Source = null;
        }
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        _viewModel.CancelSearch();
        LoadingOverlay.Visibility = Visibility.Collapsed;

        _logger.Debug("[Emergency] User forced overlay dismissal in GlobalSearchPage");
        _mainWindow.UpdateStatusBarService.UpdateContent("Emergency reset performed.");
    }
}