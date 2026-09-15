using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Models;
using SimpleLauncher.Services.WpfServices;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.ContextMenu;

/// <summary>
///     Builds and manages right-click context menus for game items in the launcher.
/// </summary>
public class ContextMenuService : IContextMenuService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IRetroAchievementsHasherTool _raHasherTool;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuService" /> class.
    /// </summary>
    /// <param name="configuration">The application configuration used to resolve the log file path.</param>
    /// <param name="logger">The service used to log errors.</param>
    /// <param name="messageBox">The service used to display message boxes to the user.</param>
    /// <param name="raHasherTool">The RetroAchievements hasher tool used to check system support.</param>
    public ContextMenuService(IConfiguration configuration, ILogger logger, IMessageBoxLibraryService messageBox,
        IRetroAchievementsHasherTool raHasherTool)
    {
        _configuration = configuration;
        _logger = logger;
        _messageBox = messageBox;
        _raHasherTool = raHasherTool;
    }

    /// <summary>
    ///     Creates and returns a context menu for the specified game item.
    /// </summary>
    /// <param name="context">The right-click context containing game and UI information.</param>
    /// <param name="findCoverImage">The service used to find cover images.</param>
    /// <param name="contextMenuFunctions">The functions that handle context menu actions.</param>
    /// <returns>A configured <see cref="System.Windows.Controls.ContextMenu" /> for the game item.</returns>
    public System.Windows.Controls.ContextMenu AddRightClickReturnContextMenu(RightClickContext context,
        IFindCoverImageService findCoverImage, IContextMenuFunctions contextMenuFunctions)
    {
        return CreateMenu(context, findCoverImage, contextMenuFunctions);
    }

    /// <summary>
    ///     Attaches a right-click context menu to the specified button and returns it.
    /// </summary>
    /// <param name="context">The right-click context containing game and UI information.</param>
    /// <param name="findCoverImage">The service used to find cover images.</param>
    /// <param name="contextMenuFunctions">The functions that handle context menu actions.</param>
    /// <returns>The <see cref="Button" /> with the context menu attached.</returns>
    public Button AddRightClickReturnButton(RightClickContext context, IFindCoverImageService findCoverImage,
        IContextMenuFunctions contextMenuFunctions)
    {
        context.Button?.ContextMenu = CreateMenu(context, findCoverImage, contextMenuFunctions);

        return context.Button!;
    }

    private System.Windows.Controls.ContextMenu CreateMenu(RightClickContext context,
        IFindCoverImageService findCoverImage, IContextMenuFunctions contextMenuFunctions)
    {
        var contextMenu = new System.Windows.Controls.ContextMenu();

        // Launch Game Context Menu
        var launchMenuItemIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/launch.png")),
            Width = 16,
            Height = 16
        };
        var launchMenuItem2 = (string)Application.Current.TryFindResource("LaunchGame") ?? "Launch Game";
        var launchMenuItem = new MenuItem
        {
            Header = launchMenuItem2,
            Icon = launchMenuItemIcon
        };
        launchMenuItem.Click += async (_, _) =>
        {
            context.MainWindow.SetGameButtonsEnabled(false);
            try
            {
                context.PlaySoundEffects?.PlayNotificationSound();

                string? selectedEmulatorName;

                if (context.EmulatorComboBox is { SelectedItem: not null })
                {
                    selectedEmulatorName = context.EmulatorComboBox.SelectedItem.ToString();
                }
                else if
                    (context.Emulator !=
                     null) // This branch is taken if EmulatorComboBox is null (e.g., from GlobalSearch)
                {
                    selectedEmulatorName = context.Emulator.EmulatorName;
                }
                else if (context.SelectedSystemManager.Emulators.FirstOrDefault() is { } fallbackEmulator)
                {
                    // Fallback when no emulator source was supplied (e.g., list-view context menu):
                    // use the system's first configured emulator.
                    selectedEmulatorName = fallbackEmulator.EmulatorName;
                }
                else
                {
                    selectedEmulatorName = null; // <-- selectedEmulatorName could be null here
                }

                if (await CheckParametersForNullOrEmptyAsync(selectedEmulatorName))
                    return; // The finally block will still execute

                if (context.GameLauncherService != null)
                {
                    await context.GameLauncherService.HandleButtonClickAsync(context.FilePath, selectedEmulatorName!,
                        context.SelectedSystemName, context.SelectedSystemManager, context.Settings,
                        WpfWindowContext.FromMainWindow(context.MainWindow), context.GamePadController,
                        context.LoadingStateProvider);
                }

                // Notify user
                context.MainWindow.UpdateStatusBarService.UpdateContent(
                    (string)Application.Current.TryFindResource("LaunchingGame") ?? "Launching game...");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[CreateMenu] Error launching the game");
                _logger.Debug($"Error launching the game: {ex.Message}");
            }
            finally
            {
                context.MainWindow.SetGameButtonsEnabled(true);
            }
        };

        // Add To Favorites Context Menu
        var addToFavoritesIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/heart.png")),
            Width = 16,
            Height = 16
        };
        var addToFavorites2 = (string)Application.Current.TryFindResource("AddToFavorites") ?? "Add To Favorites";
        var addToFavorites = new MenuItem
        {
            Header = addToFavorites2,
            Icon = addToFavoritesIcon
        };
        addToFavorites.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("AddingToFavorites") ?? "Adding to favorites...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.AddToFavoritesAsync(context.SelectedSystemName, context.FileNameWithExtension,
                context.GameFileGrid!, context.FavoritesManager, context.MainWindow, context.PlaySoundEffects!, _logger,
                _messageBox);
        };

        // Remove From Favorites Context Menu
        var removeFromFavoritesIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/brokenheart.png")),
            Width = 16,
            Height = 16
        };
        var removeFromFavorites2 = (string)Application.Current.TryFindResource("RemoveFromFavorites") ??
                                   "Remove From Favorites";
        var removeFromFavorites = new MenuItem
        {
            Header = removeFromFavorites2,
            Icon = removeFromFavoritesIcon
        };
        removeFromFavorites.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("RemovingFromFavorites") ?? "Removing from favorites...");
            context.PlaySoundEffects?.PlayTrashSound();
            _ = contextMenuFunctions.RemoveFromFavoritesAsync(context.SelectedSystemName, context.FileNameWithExtension,
                context.GameFileGrid!, context.FavoritesManager, context.MainWindow, context.PlaySoundEffects!, _logger,
                _messageBox);

            // Invoke the callback if it exists
            context.OnFavoriteRemoved?.Invoke();
        };

        // Open Video Link Context Menu
        var openVideoLinkIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/video.png")),
            Width = 16,
            Height = 16
        };
        var openVideoLink2 = (string)Application.Current.TryFindResource("OpenVideoLink") ?? "Open Video Link";
        var openVideoLink = new MenuItem
        {
            Header = openVideoLink2,
            Icon = openVideoLinkIcon
        };
        openVideoLink.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningVideoLink") ?? "Opening video link...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenVideoLinkAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                context.Machines, context.Settings, context.MainWindow, _logger, _messageBox);
        };

        // Open Info Link Context Menu
        var openInfoLinkIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/info.png")),
            Width = 16,
            Height = 16
        };
        var openInfoLink2 = (string)Application.Current.TryFindResource("OpenInfoLink") ?? "Open Info Link";
        var openInfoLink = new MenuItem
        {
            Header = openInfoLink2,
            Icon = openInfoLinkIcon
        };
        openInfoLink.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningInfoLink") ?? "Opening info link...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenInfoLinkAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                context.Machines, context.Settings, context.MainWindow, _logger, _messageBox);
        };

        // Open History Context Menu
        var openHistoryIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/romhistory.png")),
            Width = 16,
            Height = 16
        };
        var openHistoryWindow2 = (string)Application.Current.TryFindResource("OpenROMHistory") ?? "Open ROM History";
        var openHistoryWindow = new MenuItem
        {
            Header = openHistoryWindow2,
            Icon = openHistoryIcon
        };
        openHistoryWindow.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningROMHistory") ?? "Opening ROM history...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenRomHistoryWindowAsync(context.SelectedSystemName,
                context.FileNameWithoutExtension, context.Machines, context.MainWindow, _logger, _messageBox);
        };

        // View Achievements Context Menu - Only add for supported systems
        var isSystemSupportedForRa =
            _raHasherTool.IsSystemSupportedForHashing(context.SelectedSystemManager.SystemName);
        MenuItem? viewAchievementsItem = null;
        if (isSystemSupportedForRa)
        {
            var viewAchievementsIcon = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/trophy.png")),
                Width = 16,
                Height = 16
            };
            var viewAchievementsText =
                (string)Application.Current.TryFindResource("ViewAchievements") ?? "View Achievements";
            viewAchievementsItem = new MenuItem
            {
                Header = viewAchievementsText,
                Icon = viewAchievementsIcon
            };
            viewAchievementsItem.Click += async (_, _) =>
            {
                try
                {
                    context.PlaySoundEffects?.PlayNotificationSound();
                    await contextMenuFunctions.OpenRetroAchievementsWindowAsync(context.FilePath,
                        context.FileNameWithoutExtension, context.SelectedSystemManager, context.MainWindow,
                        context.PlaySoundEffects!, context.LoadingStateProvider, _logger, _messageBox);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error opening the RetroAchievements window");
                    _logger.Debug($"Error opening the RetroAchievements window: {ex.Message}");
                }
            };
        }

        // Open Cover Context Menu
        var openCoverIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/cover.png")),
            Width = 16,
            Height = 16
        };
        var openCover2 = (string)Application.Current.TryFindResource("Cover") ?? "Cover";
        var openCover = new MenuItem
        {
            Header = openCover2,
            Icon = openCoverIcon
        };
        openCover.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningCoverImage") ?? "Opening cover image...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenCoverAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                context.SelectedSystemManager, context.MainWindow, _messageBox);
        };

        // Open Title Snapshot Context Menu
        var openTitleSnapshotIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/snapshot.png")),
            Width = 16,
            Height = 16
        };
        var openTitleSnapshot2 = (string)Application.Current.TryFindResource("TitleSnapshot") ?? "Title Snapshot";
        var openTitleSnapshot = new MenuItem
        {
            Header = openTitleSnapshot2,
            Icon = openTitleSnapshotIcon
        };
        openTitleSnapshot.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningTitleSnapshot") ?? "Opening title snapshot...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenTitleSnapshotAsync(context.SelectedSystemName,
                context.FileNameWithoutExtension, _messageBox);
        };

        // Open Gameplay Snapshot Context Menu
        var openGameplaySnapshotIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/snapshot.png")),
            Width = 16,
            Height = 16
        };
        var openGameplaySnapshot2 =
            (string)Application.Current.TryFindResource("GameplaySnapshot") ?? "Gameplay Snapshot";
        var openGameplaySnapshot = new MenuItem
        {
            Header = openGameplaySnapshot2,
            Icon = openGameplaySnapshotIcon
        };
        openGameplaySnapshot.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningGameplaySnapshot") ??
                "Opening gameplay snapshot...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenGameplaySnapshotAsync(context.SelectedSystemName,
                context.FileNameWithoutExtension, _messageBox);
        };

        // Open Cart Context Menu
        var openCartIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/cart.png")),
            Width = 16,
            Height = 16
        };
        var openCart2 = (string)Application.Current.TryFindResource("Cart") ?? "Cart";
        var openCart = new MenuItem
        {
            Header = openCart2,
            Icon = openCartIcon
        };
        openCart.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningCartImage") ?? "Opening cart image...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenCartAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                _messageBox);
        };

        // Open Video Context Menu
        var openVideoIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/video.png")),
            Width = 16,
            Height = 16
        };
        var openVideo2 = (string)Application.Current.TryFindResource("Video") ?? "Video";
        var openVideo = new MenuItem
        {
            Header = openVideo2,
            Icon = openVideoIcon
        };
        openVideo.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("PlayingVideo") ?? "Playing video...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.PlayVideoAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                _messageBox);
        };

        // Open Manual Context Menu
        var openManualIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/manual.png")),
            Width = 16,
            Height = 16
        };
        var openManual2 = (string)Application.Current.TryFindResource("Manual") ?? "Manual";
        var openManual = new MenuItem
        {
            Header = openManual2,
            Icon = openManualIcon
        };
        openManual.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningManual") ?? "Opening manual...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenManualAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                _logger, _messageBox);
        };

        // Open Walkthrough Context Menu
        var openWalkthroughIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/walkthrough.png")),
            Width = 16,
            Height = 16
        };
        var openWalkthrough2 = (string)Application.Current.TryFindResource("Walkthrough") ?? "Walkthrough";
        var openWalkthrough = new MenuItem
        {
            Header = openWalkthrough2,
            Icon = openWalkthroughIcon
        };
        openWalkthrough.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningWalkthrough") ?? "Opening walkthrough...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenWalkthroughAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                _logger, _messageBox);
        };

        // Open Cabinet Context Menu
        var openCabinetIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/cabinet.png")),
            Width = 16,
            Height = 16
        };
        var openCabinet2 = (string)Application.Current.TryFindResource("Cabinet") ?? "Cabinet";
        var openCabinet = new MenuItem
        {
            Header = openCabinet2,
            Icon = openCabinetIcon
        };
        openCabinet.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningCabinetImage") ?? "Opening cabinet image...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenCabinetAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                _messageBox);
        };

        // Open Flyer Context Menu
        var openFlyerIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/flyer.png")),
            Width = 16,
            Height = 16
        };
        var openFlyer2 = (string)Application.Current.TryFindResource("Flyer") ?? "Flyer";
        var openFlyer = new MenuItem
        {
            Header = openFlyer2,
            Icon = openFlyerIcon
        };
        openFlyer.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningFlyerImage") ?? "Opening flyer image...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenFlyerAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                _messageBox);
        };

        // Open PCB Context Menu
        var openPcbIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/pcb.png")),
            Width = 16,
            Height = 16
        };
        var openPcb2 = (string)Application.Current.TryFindResource("PCB") ?? "PCB";
        var openPcb = new MenuItem
        {
            Header = openPcb2,
            Icon = openPcbIcon
        };
        openPcb.Click += (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("OpeningPCBImage") ?? "Opening PCB image...");
            context.PlaySoundEffects?.PlayNotificationSound();
            _ = contextMenuFunctions.OpenPcbAsync(context.SelectedSystemName, context.FileNameWithoutExtension,
                _messageBox);
        };

        // Take Screenshot Context Menu
        var takeScreenshotIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/snapshot.png")),
            Width = 16,
            Height = 16
        };
        var takeScreenshot2 = (string)Application.Current.TryFindResource("TakeScreenshot") ?? "Take Screenshot";
        var takeScreenshot = new MenuItem
        {
            Header = takeScreenshot2,
            Icon = takeScreenshotIcon
        };

        takeScreenshot.Click += async (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("TakingScreenshot") ?? "Taking screenshot...");
            try
            {
                context.PlaySoundEffects?.PlayNotificationSound();

                // Notify user
                await _messageBox.TakeScreenShotMessageBoxAsync();

                string? selectedEmulatorName;

                if (context.EmulatorComboBox is { SelectedItem: not null })
                    selectedEmulatorName = context.EmulatorComboBox.SelectedItem.ToString();
                else if (context.Emulator != null)
                    selectedEmulatorName = context.Emulator.EmulatorName;
                else
                    selectedEmulatorName = null;

                _ = contextMenuFunctions.TakeScreenshotOfSelectedWindowAsync(context.FilePath, selectedEmulatorName!,
                    context.SelectedSystemName, context.SelectedSystemManager, context.Settings, null,
                    context.MainWindow, context.GamePadController, context.GameLauncherService!,
                    context.PlaySoundEffects!, context.LoadingStateProvider, _logger, _messageBox);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error taking the screenshot");
                _logger.Debug($"Error taking the screenshot: {ex.Message}");
            }
        };

        // Delete Game Context Menu
        var deleteGameIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/delete.png")),
            Width = 16,
            Height = 16
        };
        var deleteGame2 = (string)Application.Current.TryFindResource("DeleteGame") ?? "Delete Game";
        var deleteGame = new MenuItem
        {
            Header = deleteGame2,
            Icon = deleteGameIcon
        };

        deleteGame.Click += async (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("DeletingGame") ?? "Deleting game...");
            try
            {
                context.PlaySoundEffects?.PlayNotificationSound();

                var result =
                    await _messageBox.AreYouSureYouWantToDeleteTheGameMessageBoxAsync(context.FileNameWithExtension);
                if (result == CoreMessageBoxResult.Yes)
                {
                    try
                    {
                        await contextMenuFunctions.RemoveFromFavoritesAsync(context.SelectedSystemName,
                            context.FileNameWithExtension, context.GameFileGrid!, context.FavoritesManager,
                            context.MainWindow, context.PlaySoundEffects!, _logger, _messageBox);

                        // Invoke the callback if it exists
                        context.OnFavoriteRemoved?.Invoke();

                        await Task.Delay(500);
                        await contextMenuFunctions.DeleteGameAsync(context.FilePath, context.FileNameWithExtension,
                            context.MainWindow, context.PlaySoundEffects!, _logger, _messageBox);
                    }
                    catch (Exception ex)
                    {
                        // Notify developer
                        const string contextMessage = "Error deleting the game.";
                        _logger.Error(ex, contextMessage);

                        // Notify user
                        await _messageBox.ThereWasAnErrorDeletingTheGameMessageBoxAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting the game");
                _logger.Debug($"Error deleting the game: {ex.Message}");
            }
        };

        // Delete Cover Image Context Menu
        var deleteCoverImageIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/delete.png")),
            Width = 16,
            Height = 16
        };
        var deleteCoverImage2 = (string)Application.Current.TryFindResource("DeleteCoverImage") ?? "Delete Cover Image";
        var deleteCoverImage = new MenuItem
        {
            Header = deleteCoverImage2,
            Icon = deleteCoverImageIcon
        };

        deleteCoverImage.Click += async (_, _) =>
        {
            context.MainWindow.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("DeletingCoverImage") ?? "Deleting cover image...");
            try
            {
                context.PlaySoundEffects?.PlayNotificationSound();

                var result =
                    await _messageBox.AreYouSureYouWantToDeleteTheCoverImageMessageBoxAsync(
                        context.FileNameWithoutExtension);
                if (result == CoreMessageBoxResult.Yes)
                {
                    try
                    {
                        await contextMenuFunctions.DeleteCoverImageAsync(context.FileNameWithoutExtension,
                            context.SelectedSystemName, context.SelectedSystemManager, context.Settings,
                            context.MainWindow, context.PlaySoundEffects!, _logger, findCoverImage, _messageBox);
                    }
                    catch (Exception ex)
                    {
                        // Notify developer
                        var contextMessage = $"Error deleting the cover image of {context.FileNameWithoutExtension}.";
                        _logger.Error(ex, contextMessage);

                        // Notify user
                        await _messageBox.ThereWasAnErrorDeletingTheCoverImageMessageBoxAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting the cover image");
                _logger.Debug($"Error deleting the cover image: {ex.Message}");
            }
        };

        contextMenu.Items.Add(launchMenuItem);
        contextMenu.Items.Add(addToFavorites);
        contextMenu.Items.Add(removeFromFavorites);
        contextMenu.Items.Add(new Separator());
        // Only add View Achievements menu item for supported systems
        if (viewAchievementsItem != null)
        {
            contextMenu.Items.Add(viewAchievementsItem);
            contextMenu.Items.Add(new Separator());
        }

        contextMenu.Items.Add(openVideoLink);
        contextMenu.Items.Add(openInfoLink);
        contextMenu.Items.Add(openHistoryWindow);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(openCover);
        contextMenu.Items.Add(openTitleSnapshot);
        contextMenu.Items.Add(openGameplaySnapshot);
        contextMenu.Items.Add(openCart);
        contextMenu.Items.Add(openVideo);
        contextMenu.Items.Add(openManual);
        contextMenu.Items.Add(openWalkthrough);
        contextMenu.Items.Add(openCabinet);
        contextMenu.Items.Add(openFlyer);
        contextMenu.Items.Add(openPcb);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(takeScreenshot);
        contextMenu.Items.Add(deleteGame);
        contextMenu.Items.Add(deleteCoverImage);

        return contextMenu;

        async Task<bool> CheckParametersForNullOrEmptyAsync(string? selectedEmulatorName)
        {
            if (string.IsNullOrEmpty(context.FilePath))
            {
                // Expected condition (stale entry); the user is already notified via the message box — not a bug report.
                _logger.Information("Right click context menu was invoked, but the FilePath is null or empty");

                // Notify user
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                return true;
            }

            if (string.IsNullOrEmpty(selectedEmulatorName))
            {
                // Log at Information level: expected condition (user right-clicked a game with no emulator
                // selected) and the user is already notified via the message box — not a bug report.
                _logger.Information(
                    "[CheckParametersForNullOrEmptyAsync] Right click context menu was invoked, but the SelectedEmulatorName is null or empty");

                // Notify user
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                return true;
            }

            if (string.IsNullOrEmpty(context.SelectedSystemName))
            {
                // Expected condition; the user is already notified via the message box — not a bug report.
                _logger.Information(
                    "Right click context menu was invoked, but the SelectedSystemName is null or empty");

                // Notify user
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                return true;
            }

            if (context.SelectedSystemManager == null)
            {
                // Expected condition; the user is already notified via the message box — not a bug report.
                _logger.Information("Right click context menu was invoked, but the SelectedSystemManager is null");

                // Notify user
                await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                return true;
            }

            return false;
        }
    }
}