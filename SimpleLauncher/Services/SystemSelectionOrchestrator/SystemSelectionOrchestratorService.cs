using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.GameFileWatcher;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.LoadImages;
using SimpleLauncher.Services.QuitOrReinstall;
using SimpleLauncher.Services.SystemManager;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.SystemSelectionOrchestrator;

/// <summary>
///     Orchestrates system selection UI, including displaying the system grid, handling system clicks, combo box changes,
///     and system CRUD operations.
/// </summary>
public class SystemSelectionOrchestratorService : ISystemSelectionOrchestrator
{
    private readonly IConfiguration _configuration;
    private readonly IDisplaySystemInformation _displaySystemInformation;
    private readonly IGameCacheService _gameCacheService;
    private readonly GameFileWatcherService _gameFileWatcherService;
    private readonly IGameItemRenderService _gameItemRenderService;
    private readonly IGetListOfFilesService _getListOfFiles;
    private readonly IHelpUserService _helpUserService;
    private readonly IImageLoader _imageLoader;
    private readonly ILogger _logger;
    private readonly IMameDataService _mameDataService;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IParameterResolverService _parameterResolverService;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly QuitSimpleLauncher _quitSimpleLauncher;
    private readonly SettingsManagerService _settings;
    private readonly ISystemConfigurationService _systemConfigurationService;
    private readonly ISystemImageResolverService _systemImageResolverService;
    private readonly IUpdateStatusBar _updateStatusBarService;
    private ISystemSelectionHost _host = null!;

    /// <summary>
    ///     Initializes a new instance of the SystemSelectionOrchestratorService with the specified dependencies.
    /// </summary>
    public SystemSelectionOrchestratorService(
        SettingsManagerService settings,
        ISystemImageResolverService systemImageResolverService,
        IImageLoader imageLoader,
        PlaySoundEffects playSoundEffects,
        IGameCacheService gameCacheService,
        GameFileWatcherService gameFileWatcherService,
        IConfiguration configuration,
        IHelpUserService helpUserService,
        IGameItemRenderService gameItemRenderService,
        IGetListOfFilesService getListOfFiles,
        IUpdateStatusBar updateStatusBarService,
        ISystemConfigurationService systemConfigurationService,
        IMameDataService mameDataService,
        IMessageBoxLibraryService messageBox,
        QuitSimpleLauncher quitSimpleLauncher,
        IDisplaySystemInformation displaySystemInformation,
        ILogger logger,
        IParameterResolverService parameterResolverService)
    {
        _settings = settings;
        _systemImageResolverService = systemImageResolverService;
        _imageLoader = imageLoader;
        _playSoundEffects = playSoundEffects;
        _gameCacheService = gameCacheService;
        _gameFileWatcherService = gameFileWatcherService;
        _configuration = configuration;
        _helpUserService = helpUserService;
        _gameItemRenderService = gameItemRenderService;
        _getListOfFiles = getListOfFiles;
        _updateStatusBarService = updateStatusBarService;
        _systemConfigurationService = systemConfigurationService;
        _mameDataService = mameDataService;
        _messageBox = messageBox;
        _quitSimpleLauncher = quitSimpleLauncher;
        _displaySystemInformation = displaySystemInformation;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parameterResolverService = parameterResolverService;
    }

    /// <summary>Initializes the orchestrator with the specified UI host.</summary>
    public void Initialize(ISystemSelectionHost host)
    {
        _host = host;
    }

    /// <summary>Loads or reloads system manager configurations and updates the combo box source.</summary>
    public void LoadOrReloadSystemManager()
    {
        var managers = _systemConfigurationService.LoadSystemManagers();
        ApplyLoadedSystemManagers(managers);
    }

    /// <summary>Asynchronously loads or reloads system manager configurations and updates the combo box source.</summary>
    public async Task LoadOrReloadSystemManagerAsync()
    {
        var managers = await _systemConfigurationService.LoadSystemManagersAsync();
        ApplyLoadedSystemManagers(managers);
    }

    /// <summary>Displays the system selection screen with clickable system buttons.</summary>
    public async Task DisplaySystemSelectionScreenAsync(CancellationToken cancellationToken = default)
    {
        _host.TopSystemSelection.Visibility = Visibility.Collapsed;
        _host.StatusBarArea.Visibility = Visibility.Collapsed;

        _host.ClearGameButtonImages(_host.GameFileGrid);
        _host.GameFileGrid.Children.Clear();
        _host.GameListItems.Clear();

        _host.PreviewImage.Source = null;

        _host.TotalFilesLabel.Content = null;
        _host.PrevPageButton2.IsEnabled = false;
        _host.NextPageButton2.IsEnabled = false;
        ((IUiResetHost)_host).CurrentFilter = null;
        ((IUiResetHost)_host).ActiveSearchQueryOrMode = null;
        _host.SearchTextBox.Text = "";

        _host.GameFileGrid.Visibility = Visibility.Visible;
        _host.ListViewPreviewArea.Visibility = Visibility.Collapsed;

        var systemManagers = _host.GetSystemManagers();
        if (systemManagers == null || systemManagers.Count == 0)
        {
            var noSystemsConfiguredMsg = (string)Application.Current.TryFindResource("NoSystemsConfiguredMessage") ??
                                         "No systems configured. Please use the 'Edit System' menu to add systems.";
            _host.GameFileGrid.Children.Add(new TextBlock
            {
                Text = $"\n{noSystemsConfiguredMsg}",
                Padding = new Thickness(10),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        else
        {
            await PopulateSystemSelectionGridAsync(cancellationToken);
        }

        _host.ResetPaginationButtons();
    }

    /// <summary>Handles a system button click, loading the selected system's games.</summary>
    public async Task SystemButtonClickAsync(string systemName, CancellationToken cancellationToken)
    {
        try
        {
            _host.CancelAndRecreateToken();
            var token = _host.CurrentCancellationToken;

            try
            {
                if (((IUiResetHost)_host).IsUiUpdating) return;

                _host.SetLoadingState(true);
                await Task.Delay(100, token);

                _host.TopSystemSelection.Visibility = Visibility.Visible;
                _host.StatusBarArea.Visibility = Visibility.Visible;
                _host.SystemComboBox.SelectedItem = systemName;

                _playSoundEffects.PlayNotificationSound();
                _updateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LoadingSystem") ??
                                                      "Loading system...");
            }
            catch (OperationCanceledException)
            {
                _host.SetLoadingState(false);
                _updateStatusBarService.UpdateContent(
                    (string)Application.Current.TryFindResource("SystemLoadCancelled") ?? "System load cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in SystemButtonClickAsync");
                await _messageBox.InvalidSystemConfigMessageBoxAsync();

                _host.SystemComboBox.SelectedItem = null;
                await DisplaySystemSelectionScreenAsync(token);

                await _gameCacheService.InvalidateAsync(token);
            }
            finally
            {
                _host.SetLoadingState(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Do nothing - cancellation is expected when the UI is reset
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in SystemButtonClickAsync");
        }
    }

    /// <summary>Deletes a system configuration after user confirmation.</summary>
    public async void DeleteSystemFromContextMenuAsync(string systemName)
    {
        try
        {
            var result = await _messageBox.AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync();
            if (result != CoreMessageBoxResult.Yes) return;

            _playSoundEffects.PlayNotificationSound();

            await SystemManagerService.DeleteSystemAsync(systemName);

            await Task.Delay(100, _host.CurrentCancellationToken);

            await LoadOrReloadSystemManagerAsync();
            await _host.ResetUiAsync();

            await _messageBox.SystemHasBeenDeletedMessageBoxAsync(systemName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in DeleteSystemFromContextMenuAsync");
        }
    }

    /// <summary>Opens the edit system dialog for the specified system.</summary>
    public void EditSystemFromContextMenu(string systemName)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            _updateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("OpeningExpertMode") ??
                                                  "Opening Expert Mode...");

            EditSystemWindow editSystemWindow = new(_settings, _playSoundEffects, _configuration, _helpUserService,
                _imageLoader, _messageBox, _quitSimpleLauncher, _logger, _parameterResolverService, systemName)
            {
                Owner = Application.Current.MainWindow
            };
            editSystemWindow.ShowDialog();

            LoadOrReloadSystemManager();
            _ = _host.ResetUiAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in EditSystemFromContextMenu");
        }
    }

    /// <summary>Handles system combo box selection changes, loading the selected system's games and metadata.</summary>
    public async Task SystemComboBoxSelectionChangedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            try
            {
                if (((IUiResetHost)_host).IsUiUpdating) return;

                if (_host.SystemComboBox.SelectedItem == null)
                {
                    await _gameCacheService.InvalidateAsync(CancellationToken.None);
                    _host.IsPlayTimeVisible = false;

                    _gameFileWatcherService.StopWatching();

                    return;
                }

                ((IUiResetHost)_host).IsUiUpdating = true;
                _host.SetLoadingState(true,
                    (string)Application.Current.TryFindResource("LoadingSystem") ?? "Loading system...");
                await Task.Delay(100, cancellationToken);
                try
                {
                    _host.SearchTextBox.Text = "";
                    _host.EmulatorComboBox.ItemsSource = null;
                    _host.EmulatorComboBox.SelectedIndex = -1;
                    _host.PreviewImage.Source = null;

                    await _gameCacheService.SetSearchResultsAsync([], cancellationToken);
                    ((IUiResetHost)_host).CurrentFilter = null;
                    ((IUiResetHost)_host).ActiveSearchQueryOrMode = null;

                    _host.GameFileGrid.Visibility = Visibility.Visible;
                    _host.ListViewPreviewArea.Visibility = Visibility.Collapsed;

                    var selectedSystem = _host.SystemComboBox.SelectedItem?.ToString();
                    var systemManagers = _host.GetSystemManagers();
                    var selectedManager = systemManagers.FirstOrDefault(c =>
                        c.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
                    if (selectedSystem == null || selectedManager == null)
                    {
                        const string errorMessage = "Selected system or its configuration is null.";
                        _logger.Warning(errorMessage);

                        await _messageBox.InvalidSystemConfigMessageBoxAsync();

                        _host.SystemComboBox.SelectedItem = null;
                        await DisplaySystemSelectionScreenAsync(cancellationToken);

                        await _gameCacheService.InvalidateAsync(cancellationToken);

                        return;
                    }

                    var formats = selectedManager.FileFormatsToSearch;
                    _host.IsPlayTimeVisible = formats?.Any(static f =>
                        f.Equals("url", StringComparison.OrdinalIgnoreCase) ||
                        f.Equals("lnk", StringComparison.OrdinalIgnoreCase)) != true;

                    ((IUiResetHost)_host).MameSortOrder = AppConstants.MameSortOrderFileName;
                    _host.UpdateSortOrderButtonUi();

                    _host.EmulatorComboBox.ItemsSource = selectedManager.Emulators
                        .Select(static emulator => emulator.EmulatorName).ToList();
                    if (_host.EmulatorComboBox.Items.Count > 0) _host.EmulatorComboBox.SelectedIndex = 0;

                    _host.SelectedSystem = selectedSystem;

                    var systemPlayTime = _settings.SystemPlayTimes.FirstOrDefault(s =>
                        s.SystemName.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));
                    _host.PlayTime = systemPlayTime != null ? systemPlayTime.FormattedPlayTime : "00:00:00";

                    var validationResult =
                        await _displaySystemInformation.DisplaySystemInfoAsync(selectedManager, _host.GameFileGrid,
                            cancellationToken);

                    if (!validationResult.IsValid)
                    {
                        var errorMessages = new StringBuilder();
                        foreach (var msg in validationResult.ErrorMessages) errorMessages.Append(msg);

                        await _messageBox.ListOfErrorsMessageBoxAsync(errorMessages);
                    }

                    var resolvedSystemImageFolderPath =
                        PathHelper.ResolveRelativeToAppDirectory(selectedManager.SystemImageFolder);

                    var selectedRomFolders = selectedManager.SystemFolders
                        .Select(static path => PathHelper.ResolveRelativeToAppDirectory(path) ?? path).ToList();
                    var selectedImageFolder = string.IsNullOrWhiteSpace(resolvedSystemImageFolderPath)
                        ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", selectedManager.SystemName)
                        : resolvedSystemImageFolderPath;

                    _host.SetSelectedRomFolders(selectedRomFolders);
                    _host.SetSelectedImageFolder(selectedImageFolder);

                    await PopulateAllGamesForCurrentSystemAsync(selectedManager, selectedSystem, selectedRomFolders,
                        cancellationToken);

                    _gameFileWatcherService.StartWatching(
                        selectedManager.SystemFolders,
                        selectedSystem,
                        selectedManager.FileFormatsToSearch);

                    _host.ResetPaginationButtons();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    const string errorMessage = "Error in the method SystemComboBoxSelectionChangedAsync.";
                    _logger.Error(ex, errorMessage);

                    await _messageBox.InvalidSystemConfigMessageBoxAsync();

                    await _gameCacheService.InvalidateAsync(CancellationToken.None);
                }
                finally
                {
                    _host.SetLoadingState(false);
                    ((IUiResetHost)_host).IsUiUpdating = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when user rapidly switches systems
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in SystemComboBoxSelectionChangedAsync");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when user rapidly switches systems
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in SystemComboBoxSelectionChangedAsync");
        }
    }

    private void ApplyLoadedSystemManagers(IList<SystemManagerService> managers)
    {
        _host.SetSystemManagers(managers);
        var sortedSystemNames = managers.Select(static manager => manager.SystemName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
        _host.SystemComboBox.ItemsSource = sortedSystemNames;

        _gameItemRenderService.ReloadFactories(managers, _mameDataService.Machines.ToList());
    }

    private async Task PopulateSystemSelectionGridAsync(CancellationToken cancellationToken)
    {
        foreach (var config in _host.GetSystemManagers().OrderBy(static s => s.SystemName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var imagePath = await _systemImageResolverService.ResolveDisplayImageAsync(config);
            var (imageStream, _) = await _imageLoader.LoadImageAsync(imagePath);

            var buttonContentPanel = new StackPanel { Orientation = Orientation.Vertical };

            var systemImageSize = _settings.ThumbnailSizeForSystem;

            var image = new Image
            {
                Source = imageStream.ToBitmapImage(),
                Height = systemImageSize * 1.3,
                Width = systemImageSize * 1.3 * 1.6,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(5)
            };
            buttonContentPanel.Children.Add(image);

            var textBlock = new TextBlock
            {
                Text = config.SystemName,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12,
                ToolTip = config.SystemName,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            };
            buttonContentPanel.Children.Add(textBlock);

            var systemButton = new Button
            {
                Content = buttonContentPanel,
                Tag = config.SystemName,
                Width = (systemImageSize * 1.3 * 1.6) + 20,
                Height = (systemImageSize * 1.3) + 40 + 20,
                Margin = new Thickness(5),
                Padding = new Thickness(5)
            };

            AutomationProperties.SetName(systemButton, config.SystemName);
            AutomationProperties.SetHelpText(systemButton,
                (string)Application.Current.TryFindResource("SelectSystemButtonHelpText") ??
                $"Select {config.SystemName} system");

            systemButton.SetResourceReference(FrameworkElement.StyleProperty, "SystemButtonStyle");

            systemButton.Click += async (_, _) =>
                await SystemButtonClickAsync(config.SystemName, _host.CurrentCancellationToken);

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var selectIcon = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/play.png")),
                Width = 16,
                Height = 16
            };
            var selectMenuItem = new MenuItem
            {
                Header = (string)Application.Current.TryFindResource("SelectSystem") ?? "Select System",
                Icon = selectIcon
            };
            selectMenuItem.Click += (_, _) =>
                systemButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, systemButton));

            var editIcon = new Image
            {
                Source =
                    new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/settings.png")),
                Width = 16,
                Height = 16
            };
            var editMenuItem = new MenuItem
            {
                Header = (string)Application.Current.TryFindResource("EditSystem") ?? "Edit System",
                Icon = editIcon
            };
            editMenuItem.Click += (_, _) => EditSystemFromContextMenu(config.SystemName);

            var deleteIcon = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/delete.png")),
                Width = 16,
                Height = 16
            };
            var deleteMenuItem = new MenuItem
            {
                Header = (string)Application.Current.TryFindResource("DeleteSystem") ?? "Delete System",
                Icon = deleteIcon
            };
            deleteMenuItem.Click += (_, _) => DeleteSystemFromContextMenuAsync(config.SystemName);

            contextMenu.Items.Add(selectMenuItem);
            contextMenu.Items.Add(editMenuItem);
            contextMenu.Items.Add(deleteMenuItem);
            systemButton.ContextMenu = contextMenu;

            _host.GameFileGrid.Children.Add(systemButton);
        }
    }

    private async Task PopulateAllGamesForCurrentSystemAsync(SystemManagerService selectedManager,
        string currentSelectedSystem, List<string> selectedRomFolders, CancellationToken cancellationToken)
    {
        var uniqueFilesForSystem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in selectedRomFolders)
        {
            var resolvedSystemFolderPath = PathHelper.ResolveRelativeToAppDirectory(folder);
            if (string.IsNullOrEmpty(resolvedSystemFolderPath) || !Directory.Exists(resolvedSystemFolderPath) ||
                selectedManager.FileFormatsToSearch == null)
            {
                continue;
            }

            var filesInFolder = await _getListOfFiles.GetFilesAsync(resolvedSystemFolderPath,
                selectedManager.FileFormatsToSearch, selectedManager.DisableRecursiveSearch,
                selectedManager.GroupByFolder, cancellationToken);
            // Key by full path: systems whose subfolders contain same-named ROMs must not
            // hide all but one entry in the cache (WPF-07 follow-up).
            foreach (var file in filesInFolder) uniqueFilesForSystem.TryAdd(Path.GetFullPath(file), file);
        }

        await _gameCacheService.SetAllGamesAsync(uniqueFilesForSystem.Values.ToList(), currentSelectedSystem,
            cancellationToken);
    }
}