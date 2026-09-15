using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameItemFactory;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.MameManager;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Models;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.GameLauncher;
using SimpleLauncher.Services.LoadImages;
using SimpleLauncher.Services.SystemManager;
using SimpleLauncher.Services.WpfServices;
using Image = System.Windows.Controls.Image;

// ReSharper disable UnusedMember.Local

namespace SimpleLauncher.Services.GameItemFactory;

/// <summary>
///     Factory that creates WPF game buttons with cover images, favorite star overlays,
///     achievement/video/info shortcut buttons, and context menus for game entities.
/// </summary>
internal partial class GameButtonFactory(
    ComboBox emulatorComboBox,
    ComboBox systemComboBox,
    List<SystemManagerService> systemManagers,
    List<MameManagerService> machines,
    SettingsManagerService settings,
    FavoritesManager favoritesManager,
    WrapPanel gameFileGrid,
    MainWindow mainWindow,
    GamePadController gamePadController,
    GameLauncherService gameLauncher,
    PlaySoundEffects playSoundEffects,
    IGetListOfFilesService getListOfFiles,
    IFindCoverImageService findCoverImage,
    IImageLoader imageLoader,
    IMessageBoxLibraryService messageBox,
    IRetroAchievementsHasherTool raHasherTool,
    IContextMenuFunctions contextMenuFunctions,
    ILogger logger,
    IContextMenuService contextMenuService)
{
    private readonly IContextMenuFunctions _contextMenuFunctions =
        contextMenuFunctions ?? throw new ArgumentNullException(nameof(contextMenuFunctions));

    private readonly IContextMenuService _contextMenuService =
        contextMenuService ?? throw new ArgumentNullException(nameof(contextMenuService));

    private readonly ComboBox _emulatorComboBox =
        emulatorComboBox ?? throw new ArgumentNullException(nameof(emulatorComboBox));

    private readonly FavoritesManager _favoritesManager =
        favoritesManager ?? throw new ArgumentNullException(nameof(favoritesManager));

    private readonly IFindCoverImageService _findCoverImage =
        findCoverImage ?? throw new ArgumentNullException(nameof(findCoverImage));

    private readonly WrapPanel _gameFileGrid = gameFileGrid ?? throw new ArgumentNullException(nameof(gameFileGrid));

    private readonly GameLauncherService _gameLauncher =
        gameLauncher ?? throw new ArgumentNullException(nameof(gameLauncher));

    private readonly GamePadController _gamePadController =
        gamePadController ?? throw new ArgumentNullException(nameof(gamePadController));

    private readonly IGetListOfFilesService _getListOfFiles =
        getListOfFiles ?? throw new ArgumentNullException(nameof(getListOfFiles));

    private readonly IImageLoader _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));

    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly List<MameManagerService> _machines = machines ?? throw new ArgumentNullException(nameof(machines));
    private readonly MainWindow _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

    private readonly IMessageBoxLibraryService _messageBox =
        messageBox ?? throw new ArgumentNullException(nameof(messageBox));

    private readonly PlaySoundEffects _playSoundEffects =
        playSoundEffects ?? throw new ArgumentNullException(nameof(playSoundEffects));

    private readonly IRetroAchievementsHasherTool _raHasherTool =
        raHasherTool ?? throw new ArgumentNullException(nameof(raHasherTool));

    private readonly SettingsManagerService _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    private readonly ComboBox _systemComboBox =
        systemComboBox ?? throw new ArgumentNullException(nameof(systemComboBox));

    private readonly List<SystemManagerService> _systemManagers =
        systemManagers ?? throw new ArgumentNullException(nameof(systemManagers));

    private Button _button = null!;

    /// <summary>
    ///     Gets or sets the height of the game cover image displayed on the button.
    /// </summary>
    public int ImageHeight { get; set; } = settings.ThumbnailSize;

    /// <summary>
    ///     Creates a game button with cover image, favorite star overlay, and context menu for the specified entity.
    /// </summary>
    /// <param name="entityPath">The file or directory path of the game entity.</param>
    /// <param name="systemName">The name of the system this game belongs to.</param>
    /// <param name="systemManager">The system manager service providing configuration for the system.</param>
    /// <returns>A configured WPF Button control representing the game.</returns>
    public async Task<Button> CreateGameButtonAsync(string entityPath, string systemName,
        SystemManagerService systemManager)
    {
        var isDirectory = Directory.Exists(entityPath);

        string fileNameWithoutExtension;

        var fileNameWithExtension = Path.GetFileName(entityPath); // Folder name
        if (isDirectory)
        {
            // Folder name
            fileNameWithoutExtension = fileNameWithExtension;
        }
        else
        {
            fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entityPath);
        }

        var selectedSystemName = systemName;
        var selectedSystemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));

        var imagePath = _findCoverImage.FindCoverImagePath(fileNameWithoutExtension, selectedSystemName,
            selectedSystemManager.SystemImageFolder);
        if (isDirectory) // GroupByFolder is true
        {
            // First, try to find an image with the same name as the folder name.
            // If the found path is a default image, try the fallback logic.
            if (imagePath.EndsWith("default.png", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to current logic: look inside the folder for a file to use as a name.
                var filesInFolder = await _getListOfFiles.GetFilesAsync(entityPath,
                    selectedSystemManager.FileFormatsToSearch, selectedSystemManager.DisableRecursiveSearch,
                    selectedSystemManager.GroupByFolder);
                if (filesInFolder.Count != 0)
                {
                    var representativeFileName = Path.GetFileNameWithoutExtension(filesInFolder[0]);
                    // Now search again with the new name. This will become the final imagePath.
                    imagePath = _findCoverImage.FindCoverImagePath(representativeFileName, selectedSystemName,
                        selectedSystemManager.SystemImageFolder);
                }
            }
        }

        // This is the logic for non-grouped files, which remains the same.
        var (imageStream, isDefaultImage) = await _imageLoader.LoadImageAsync(imagePath);
        var loadedImage = imageStream.ToBitmapImage();

        // Create the view model and determine the initial favorite state:
        var viewModel = new GameButtonViewModel
        {
            IsFavorite = _favoritesManager.FavoriteList.Any(f =>
                f.FileName.Equals(fileNameWithExtension, StringComparison.OrdinalIgnoreCase) &&
                f.SystemName.Equals(selectedSystemName, StringComparison.OrdinalIgnoreCase)),
            // Placeholder logic for achievements
            HasAchievements = true
        };

        // Create a container for text that will hold two rows
        var textPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Determine the display name based on the FilenameDisplayMode setting
        var displayName = GetDisplayName(fileNameWithoutExtension);

        // Show filename unless mode is "NoFilename"
        if (!string.Equals(_settings.FilenameDisplayMode, "NoFilename", StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(displayName))
        {
            var filenameFontSize = GetFilenameFontSize();
            var filenameTextBlock = new TextBlock
            {
                Text = displayName,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = filenameFontSize,
                ToolTip = fileNameWithoutExtension,
                TextWrapping = TextWrapping.Wrap
            };
            textPanel.Children.Add(filenameTextBlock);
        }

        // Determine accessible name for the main game button
        var accessibleGameName = fileNameWithoutExtension;

        // Show machine name if the user enabled DisplayMachineName and this is a MAME system
        if (_settings.DisplayMachineName)
        {
            var machine = _machines.FirstOrDefault(m =>
                m.MachineName.Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
            if (machine != null && !string.IsNullOrWhiteSpace(machine.Description))
            {
                var machineNameFontSize = GetMachineNameFontSize();
                var descriptionTextBlock = new TextBlock
                {
                    Text = machine.Description,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontSize = machineNameFontSize,
                    ToolTip = machine.Description,
                    TextWrapping = TextWrapping.Wrap
                };
                textPanel.Children.Add(descriptionTextBlock);
                accessibleGameName = machine.Description;
            }
        }

        // Calculate dimensions based on the user-selected aspect ratio
        // Base size is determined from ImageHeight (plus some padding)
        double baseSize = ImageHeight + 50;
        double aspectWidth;
        double aspectHeight;

        // Use the ButtonAspectRatio value from settings:
        switch (_settings.ButtonAspectRatio)
        {
            case "Wider":
                aspectWidth = 1.5;
                aspectHeight = 1.0;
                break;
            case "SuperWider":
                aspectWidth = 2.0;
                aspectHeight = 1.0;
                break;
            case "SuperWider2":
                aspectWidth = 2.5;
                aspectHeight = 1.0;
                break;
            case "Taller":
                aspectWidth = 1.0;
                aspectHeight = 1.3;
                break;
            case "SuperTaller":
                aspectWidth = 1.0;
                aspectHeight = 1.6;
                break;
            case "SuperTaller2":
                aspectWidth = 1.0;
                aspectHeight = 1.9;
                break;
            default: // "Square" or any unrecognized value
                aspectWidth = 1.1;
                aspectHeight = 1.0;
                break;
        }

        // Calculate the height for the image area only based on the aspect ratio.
        var imageAreaHeight = baseSize * (aspectHeight / aspectWidth);

        // Create a grid with two rows:
        // Row 0: fixed height for the image container.
        // Row 1: auto-sized for the text.
        var grid = new Grid
        {
            Width = baseSize
            // Notice: NOT setting a fixed Height for the grid
            // so that the text row (Row 1) can expand.
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(imageAreaHeight) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = loadedImage // Assign the loaded image here
        };

        // Wrap the image in a Border that fixes the image area size.
        var imageContainer = new Border
        {
            Width = baseSize,
            Height = imageAreaHeight,
            Child = image
        };
        Grid.SetRow(imageContainer, 0);
        grid.Children.Add(imageContainer);

        // If the game is a favorite, add a star overlay.
        // Create the star overlay image.
        var starImage = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/star.png")),
            Width = 22,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(5)
        };
        // Bind its Visibility to IsFavorite
        var binding = new Binding("IsFavorite")
        {
            Converter = new BooleanToVisibilityConverter()
        };
        starImage.SetBinding(UIElement.VisibilityProperty, binding);
        grid.Children.Add(starImage); // Add the star overlay to the grid.

        var context = new RightClickContext(
            entityPath,
            fileNameWithExtension,
            fileNameWithoutExtension,
            selectedSystemName,
            selectedSystemManager,
            _machines,
            _favoritesManager,
            _settings,
            _emulatorComboBox,
            null,
            null,
            _gameFileGrid,
            null,
            _mainWindow,
            _gamePadController,
            null,
            _gameLauncher,
            _playSoundEffects,
            _mainWindow
        );

        // Capture fields as locals so overlay-button lambdas close over these
        // locals instead of capturing 'this', allowing the factory to be GC'd
        // while buttons are still alive.
        var playSound = _playSoundEffects;
        var mainWindowLocal = _mainWindow;
        var loggerLocal = _logger;
        var messageBoxLocal = _messageBox;
        var machinesLocal = _machines;
        var settingsLocal = _settings;

        const double overlayButtonWidth = 22;
        const double overlayButtonHeight = 22;
        const double overlayButtonSpacing = 5; // Vertical spacing between buttons
        double currentVerticalOffset = 5; // Initial top margin for the first button

        // Only show RetroAchievements icon for supported systems
        var isSystemSupportedForRa = _raHasherTool.IsSystemSupportedForHashing(selectedSystemManager.SystemName);

        if (_settings.OverlayRetroAchievementButton && isSystemSupportedForRa)
        {
            // Add the RetroAchievements trophy icon overlay
            var trophyButton = new Button
            {
                Width = overlayButtonWidth,
                Height = overlayButtonHeight,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, currentVerticalOffset, 5, 0), // Use dynamic offset
                Cursor = Cursors.Hand,
                ToolTip = (string)Application.Current.TryFindResource("ViewAchievements") ??
                          "View Achievements", // Localized ToolTip
                // ReSharper disable once AssignNullToNotNullAttribute
                Style = (Style)Application.Current.FindResource("MahApps.Styles.Button.Chromeless")
            };
            // Set AutomationProperties.Name for screen readers
            AutomationProperties.SetName(trophyButton, (string)trophyButton.ToolTip);

            var trophyImage = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/trophy.png")),
                Stretch = Stretch.Uniform
            };
            trophyButton.Content = trophyImage;

            trophyButton.Click += async (_, e) =>
            {
                try
                {
                    // Prevent the main button's click event from firing
                    e.Handled = true;

                    playSound.PlayNotificationSound();

                    // Null check for mainWindowLocal before using it
                    if (mainWindowLocal == null)
                    {
                        loggerLocal.Warning("_mainWindow is null in trophy button click handler");
                        return;
                    }

                    mainWindowLocal.SetLoadingState(true,
                        (string)Application.Current.TryFindResource("PreparingRetroAchievements") ??
                        "Preparing RetroAchievements...");

                    try
                    {
                        await _contextMenuFunctions.OpenRetroAchievementsWindowAsync(entityPath,
                            fileNameWithoutExtension, selectedSystemManager, mainWindowLocal, playSound,
                            context.LoadingStateProvider, loggerLocal, messageBoxLocal);
                    }
                    catch (Exception ex)
                    {
                        // Notify developer
                        loggerLocal.Error(ex, $"Error opening achievements for {fileNameWithoutExtension}");

                        // Notify user
                        await messageBoxLocal.CouldNotOpenAchievementsWindowMessageBoxAsync();
                    }
                    finally
                    {
                        mainWindowLocal.SetLoadingState(false);
                    }
                }
                catch (Exception ex)
                {
                    loggerLocal.Error(ex, "Error opening Retro Achievements Window");
                    _logger.Debug($"Error opening Retro Achievements Window: {ex.Message}");
                }
            };

            grid.Children.Add(trophyButton);
            currentVerticalOffset += overlayButtonHeight + overlayButtonSpacing; // Update offset for next button
        }

        if (_settings.OverlayOpenVideoButton)
        {
            // Add the Video Link icon overlay
            var videoLinkButton = new Button
            {
                Width = overlayButtonWidth,
                Height = overlayButtonHeight,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, currentVerticalOffset, 5, 0), // Use dynamic offset
                Cursor = Cursors.Hand,
                ToolTip = (string)Application.Current.TryFindResource("ViewVideo") ?? "View Video", // Localized ToolTip
                // ReSharper disable once AssignNullToNotNullAttribute
                Style = (Style)Application.Current.FindResource("MahApps.Styles.Button.Chromeless")
            };
            // Set AutomationProperties.Name for screen readers
            AutomationProperties.SetName(videoLinkButton, (string)videoLinkButton.ToolTip);

            var videoLinkImage = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/video.png")),
                Stretch = Stretch.Uniform
            };
            videoLinkButton.Content = videoLinkImage;

            videoLinkButton.Click += async (_, e) =>
            {
                try
                {
                    // Prevent the main button's click event from firing
                    e.Handled = true;

                    playSound.PlayNotificationSound();

                    context.MainWindow?.SetLoadingState(true,
                        (string)Application.Current.TryFindResource("OpeningLink") ?? "Opening Link...");
                    try
                    {
                        await _contextMenuFunctions.OpenVideoLinkAsync(selectedSystemName, fileNameWithoutExtension,
                            machinesLocal, settingsLocal, mainWindowLocal, loggerLocal, messageBoxLocal);
                    }
                    catch (Exception ex)
                    {
                        // Notify developer
                        loggerLocal.Error(ex, $"Error opening video link for {fileNameWithoutExtension}");

                        // Notify user
                        await messageBoxLocal.ErrorOpeningVideoLinkMessageBoxAsync();
                    }
                    finally
                    {
                        context.MainWindow?.SetLoadingState(false);
                    }
                }
                catch (Exception ex)
                {
                    loggerLocal.Error(ex, "Error opening the video Link");
                    _logger.Debug($"Error opening the video link: {ex.Message}");
                }
            };

            grid.Children.Add(videoLinkButton);
            currentVerticalOffset += overlayButtonHeight + overlayButtonSpacing; // Update offset for next button
        }

        if (_settings.OverlayOpenInfoButton)
        {
            // Add the Info Link icon overlay
            var infoLinkButton = new Button
            {
                Width = overlayButtonWidth,
                Height = overlayButtonHeight,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, currentVerticalOffset, 5, 0), // Use dynamic offset
                Cursor = Cursors.Hand,
                ToolTip = (string)Application.Current.TryFindResource("ViewInfo") ?? "View Info", // Localized ToolTip
                // ReSharper disable once AssignNullToNotNullAttribute
                Style = (Style)Application.Current.FindResource("MahApps.Styles.Button.Chromeless")
            };
            // Set AutomationProperties.Name for screen readers
            AutomationProperties.SetName(infoLinkButton, (string)infoLinkButton.ToolTip);

            var infoLinkImage = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/info.png")),
                Stretch = Stretch.Uniform
            };
            infoLinkButton.Content = infoLinkImage;

            infoLinkButton.Click += async (_, e) =>
            {
                try
                {
                    // Prevent the main button's click event from firing
                    e.Handled = true;

                    playSound.PlayNotificationSound();

                    context.MainWindow?.SetLoadingState(true,
                        (string)Application.Current.TryFindResource("OpeningLink") ?? "Opening Link...");
                    try
                    {
                        await _contextMenuFunctions.OpenInfoLinkAsync(selectedSystemName, fileNameWithoutExtension,
                            machinesLocal, settingsLocal, mainWindowLocal, loggerLocal, messageBoxLocal);
                    }
                    catch (Exception ex)
                    {
                        // Notify developer
                        loggerLocal.Error(ex, $"Error opening info link for {fileNameWithoutExtension}");

                        // Notify user
                        await messageBoxLocal.ProblemOpeningInfoLinkMessageBoxAsync();
                    }
                    finally
                    {
                        context.MainWindow?.SetLoadingState(false);
                    }
                }
                catch (Exception ex)
                {
                    loggerLocal.Error(ex, "Error opening the info Link");
                    _logger.Debug($"Error opening the info link: {ex.Message}");
                }
            };

            grid.Children.Add(infoLinkButton);
            // No need to update currentVerticalOffset here as it's the last button.
        }

        var contextMenu =
            _contextMenuService.AddRightClickReturnContextMenu(context, _findCoverImage, _contextMenuFunctions);

        // Create the kebab menu button
        var kebabButton = new Button
        {
            Content = "...",
            FontWeight = FontWeights.Bold,
            Width = 25,
            Height = 25,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(5),
            Cursor = Cursors.Hand,
            ToolTip = (string)Application.Current.TryFindResource("MoreOptions") ?? "More Options",
            // ReSharper disable once AssignNullToNotNullAttribute
            Style = (Style)Application.Current.FindResource("MahApps.Styles.Button.Chromeless")
        };
        // Set AutomationProperties.Name for screen readers
        AutomationProperties.SetName(kebabButton, (string)kebabButton.ToolTip);

        kebabButton.Click += (_, e) =>
        {
            e.Handled = true; // Stop the main button's click event
            playSound.PlayNotificationSound();
            if (contextMenu != null)
            {
                contextMenu.PlacementTarget = kebabButton;
                contextMenu.IsOpen = true;
            }
        };

        grid.Children.Add(kebabButton);

        // Set the DataContext of the grid to the view model.
        grid.DataContext = viewModel;

        // Create a container for the text.
        var textContainer = new Border
        {
            Child = textPanel,
            Padding = new Thickness(5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(textContainer, 1);
        grid.Children.Add(textContainer);

        _button = new Button
        {
            Content = grid,
            Width = baseSize,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5),
            Padding = new Thickness(0, 5, 0, 0)
        };

        // Set AutomationProperties.Name for the main game button for screen readers
        AutomationProperties.SetName(_button, accessibleGameName);
        AutomationProperties.SetHelpText(_button,
            (string)Application.Current.TryFindResource("LaunchGame") ?? "Launch Game");

        // Apply the 3D style from MainWindow's resources
        _button.SetResourceReference(FrameworkElement.StyleProperty, "GameButtonStyle");

        // Create a unique key for the favorite status
        var key = $"{selectedSystemName}|{fileNameWithExtension}";

        // Create the composite tag object
        var tag = new GameButtonTag
        {
            IsDefaultImage = isDefaultImage, // Use the flag returned by ImageLoader
            Key = key
        };

        // Assign it to the button's Tag property
        _button.Tag = tag;

        // Assign click handler AFTER context is created ***
        // Lambda can safely capture 'context'.
        // Capture remaining fields as locals to avoid capturing 'this'.
        var emulatorCombo = _emulatorComboBox;
        var gameLauncherLocal = _gameLauncher;
        var gamePadCtrl = _gamePadController;

        _button.Click += async (sender, _) =>
        {
            try
            {
                if (sender is not Button clickedButton) return;

                // Prevent multiple clicks while launching
                if (!clickedButton.IsEnabled) return;

                mainWindowLocal?.SetGameButtonsEnabled(false); // Disable all game buttons

                if (emulatorCombo == null)
                {
                    loggerLocal.Warning("[CreateGameButtonAsync] _emulatorComboBox is null");
                    await messageBoxLocal.EmulatorNameIsRequiredMessageBoxAsync();
                    mainWindowLocal?.SetGameButtonsEnabled(true);
                    return;
                }

                var selectedEmulatorName =
                    emulatorCombo.SelectedItem as string; // Update value to get current selected emulator
                if (string.IsNullOrEmpty(selectedEmulatorName))
                {
                    // Notify developer
                    loggerLocal.Warning("[CreateGameButtonAsync] selectedEmulatorName is null or empty");

                    // Notify user
                    await messageBoxLocal.EmulatorNameIsRequiredMessageBoxAsync();

                    mainWindowLocal?.SetGameButtonsEnabled(true); // Re-enable buttons on error
                    return;
                }

                try
                {
                    playSound?.PlayNotificationSound();

                    if (gameLauncherLocal == null)
                    {
                        loggerLocal.Warning("[CreateGameButtonAsync] _gameLauncher is null");
                        return;
                    }

                    await gameLauncherLocal.HandleButtonClickAsync(entityPath, selectedEmulatorName, selectedSystemName,
                        selectedSystemManager, settingsLocal, WpfWindowContext.FromMainWindow(mainWindowLocal!),
                        gamePadCtrl, mainWindowLocal);
                }
                finally
                {
                    mainWindowLocal?.SetGameButtonsEnabled(true); // Re-enable all game buttons
                }
            }
            catch (Exception ex)
            {
                loggerLocal.Error(ex,
                    $"[CreateGameButtonAsync] Error launching the game. entityPath: {entityPath}, systemName: {systemName}");
                _logger.Debug($"Error launching the game: {ex.Message}");
            }
        };

        context.Button = _button;

        _button.ContextMenu = contextMenu;

        return _button;
    }

    private string GetDisplayName(string fileNameWithoutExtension)
    {
        return _settings.FilenameDisplayMode switch
        {
            "CleanUp" => CleanUpFileName(fileNameWithoutExtension),
            "NoFilename" => "",
            _ => fileNameWithoutExtension
        };
    }

    private static string CleanUpFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return fileName;

        // Remove content within parentheses: (...)
        var result = MyRegex().Replace(fileName, "");

        // Remove content within square brackets: [...]
        result = MyRegex1().Replace(result, "");

        // Remove content within curly braces: {...}
        result = MyRegex2().Replace(result, "");

        // Trim trailing whitespace, dots, and underscores
        result = result.Trim().TrimEnd('.', '_', ' ');

        return string.IsNullOrWhiteSpace(result) ? fileName : result;
    }

    [GeneratedRegex(@"\s*\([^)]*\)", RegexOptions.None, 1000)]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"\s*\[[^\]]*\]", RegexOptions.None, 1000)]
    private static partial Regex MyRegex1();

    [GeneratedRegex(@"\s*\{[^}]*\}", RegexOptions.None, 1000)]
    private static partial Regex MyRegex2();

    private double GetFilenameFontSize()
    {
        return _settings.FilenameFontSize switch
        {
            "Small" => 13,
            "Big" => 17,
            _ => 15
        };
    }

    private double GetMachineNameFontSize()
    {
        return _settings.MachineNameFontSize switch
        {
            "Small" => 13,
            "Big" => 17,
            _ => 15
        };
    }
}