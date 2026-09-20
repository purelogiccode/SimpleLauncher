#if WINDOWS
using SimpleLauncher.Avalonia.Services.TakeScreenshot;
using AvaloniaWindowScreenshot = SimpleLauncher.Avalonia.Services.TakeScreenshot.WindowScreenshot;
#endif
// ReSharper disable once RedundantUsingDirective
using CoreWindowManager = SimpleLauncher.Core.Services.TakeScreenshot.WindowManager;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.RetroAchievements;
using ILogger = Serilog.ILogger;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.ContextMenus;

/// <summary>
///     Implements context menu actions for game items such as favorites, media viewing,
///     screenshots, and deletion (port of the WPF ContextMenuFunctions).
/// </summary>
public class AvaloniaContextMenuFunctions(
    ILogger logErrors,
    IMessageBoxLibraryService messageBox,
    PlaySoundEffects playSoundEffects,
    IMameDataService mameData,
    IConfiguration configuration,
    IFindCoverImageService findCoverImage,
    LocalizationService localization)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IFindCoverImageService _findCoverImage = findCoverImage;
    private readonly LocalizationService _localization = localization;
    private readonly ILogger _logErrors = logErrors;
    private readonly IMameDataService _mameData = mameData;
    private readonly IMessageBoxLibraryService _messageBox = messageBox;
    private readonly PlaySoundEffects _playSoundEffects = playSoundEffects;
    private IRetroAchievementsHasherTool? _raHasherTool;
    private RetroAchievementsManager? _raManager;
    private IRetroAchievementsSystemMatcher? _raSystemMatcher;

    private RetroAchievementsManager RaManager =>
        _raManager ??= App.ServiceProvider.GetRequiredService<RetroAchievementsManager>();

    private IRetroAchievementsHasherTool RaHasherTool =>
        _raHasherTool ??= App.ServiceProvider.GetRequiredService<IRetroAchievementsHasherTool>();

    private IRetroAchievementsSystemMatcher RaSystemMatcher => _raSystemMatcher ??=
        App.ServiceProvider.GetRequiredService<IRetroAchievementsSystemMatcher>();

    private string GetStatus(string key, string fallback)
    {
        return _localization.GetString(key) is { } s && !string.Equals(s, key, StringComparison.OrdinalIgnoreCase)
            ? s
            : fallback;
    }

    /// <summary>
    ///     Resolves the emulator to use: the emulator selected in the toolbar combo box
    ///     when present, otherwise the system's first configured emulator.
    /// </summary>
    public static string? ResolveEmulatorName(AvaloniaRightClickContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.MainViewModel.SelectedEmulatorName))
            return context.MainViewModel.SelectedEmulatorName;

        return context.SelectedSystemManager.GetSystem(context.SelectedSystemName)?.Emulators.FirstOrDefault()
            ?.EmulatorName;
    }

    /// <summary>
    ///     Adds a game to the favorites list, updates the UI, and notifies the user.
    /// </summary>
    public async Task AddToFavoritesAsync(AvaloniaRightClickContext context)
    {
        try
        {
            // Add the new favorite if it doesn't already exist
            var alreadyFavorite = context.FavoritesManager.FavoriteList.Any(f =>
                f.FileName.Equals(context.FileNameWithExtension, StringComparison.OrdinalIgnoreCase)
                && f.SystemName.Equals(context.SelectedSystemName, StringComparison.OrdinalIgnoreCase));

            if (!alreadyFavorite)
            {
                context.FavoritesManager.FavoriteList.Add(new Favorite
                {
                    FileName = context.FileNameWithExtension,
                    SystemName = context.SelectedSystemName
                });

                _playSoundEffects.PlayNotificationSound();

                await context.FavoritesManager.SaveFavoritesAsync();

                if (context.SourceCard is { } card) card.IsFavorite = true;

                context.MainViewModel.StatusText = GetStatus("FileAddedToFavorites", "File added to favorites.");
                await _messageBox.FileAddedToFavoritesMessageBoxAsync(context.FileNameWithExtension);

                // Keep dependent UI (favorites table hearts, etc.) in sync.
                context.MainViewModel.RefreshFavoritesAndHistory();

                _logErrors.Information("[ContextMenu] Added '{File}' of system '{System}' to favorites",
                    context.FileNameWithExtension, context.SelectedSystemName);
            }
            else
            {
                _logErrors.Debug("[ContextMenu] '{File}' of system '{System}' is already in favorites",
                    context.FileNameWithExtension, context.SelectedSystemName);

                await _messageBox.GameIsAlreadyInFavoritesMessageBoxAsync(context.FileNameWithExtension);
            }
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "An error occurred while adding a game to the favorites");
            await _messageBox.ErrorWhileAddingFavoritesMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Removes a game from the favorites list, updates the UI, and notifies the user.
    /// </summary>
    public async Task RemoveFromFavoritesAsync(AvaloniaRightClickContext context)
    {
        try
        {
            var favoriteToRemove = context.FavoritesManager.FavoriteList.FirstOrDefault(f =>
                f.FileName.Equals(context.FileNameWithExtension, StringComparison.OrdinalIgnoreCase)
                && f.SystemName.Equals(context.SelectedSystemName, StringComparison.OrdinalIgnoreCase));

            if (favoriteToRemove == null)
            {
                _logErrors.Debug("[ContextMenu] No favorite entry found for '{File}' of system '{System}'",
                    context.FileNameWithExtension, context.SelectedSystemName);
                return;
            }

            context.FavoritesManager.FavoriteList.Remove(favoriteToRemove);

            _playSoundEffects.PlayTrashSound();

            await context.FavoritesManager.SaveFavoritesAsync();

            if (context.SourceCard is { } card) card.IsFavorite = false;

            context.MainViewModel.StatusText = GetStatus("FileRemovedFromFavorites", "File removed from favorites.");
            await _messageBox.FileRemovedFromFavoritesMessageBoxAsync(context.FileNameWithExtension);

            context.OnFavoriteRemoved?.Invoke();
            context.MainViewModel.RefreshFavoritesAndHistory();

            _logErrors.Information("[ContextMenu] Removed '{File}' of system '{System}' from favorites",
                context.FileNameWithExtension, context.SelectedSystemName);
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "An error occurred while removing a game from favorites");
            await _messageBox.ErrorWhileRemovingGameFromFavoriteMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Launches the game using the currently selected (or first configured) emulator.
    /// </summary>
    public async Task LaunchGameAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("LaunchingGame", "Launching game...");

        var system = context.SelectedSystemManager.GetSystem(context.SelectedSystemName);
        if (system is null || string.IsNullOrEmpty(system.Emulators.FirstOrDefault()?.EmulatorName))
        {
            // Expected condition (no system/emulator configured); user is notified via the message box.
            _logErrors.Information(
                "[ContextMenu] Launch requested but no system or emulator is configured for '{System}'",
                context.SelectedSystemName);
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(GetLogFilePath());
            return;
        }

        if (string.IsNullOrEmpty(context.FilePath))
        {
            _logErrors.Information("[ContextMenu] Launch requested but FilePath is null or empty");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(GetLogFilePath());
            return;
        }

        var selectedEmulatorName = ResolveEmulatorName(context);
        if (string.IsNullOrEmpty(selectedEmulatorName))
        {
            _logErrors.Information("[ContextMenu] Launch requested but no emulator name was resolved");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(GetLogFilePath());
            return;
        }

        _playSoundEffects.PlayNotificationSound();

        _logErrors.Information("[ContextMenu] Launching '{File}' with emulator '{Emulator}' (system '{System}')",
            context.FilePath, selectedEmulatorName, context.SelectedSystemName);

        await context.MainViewModel.LaunchGameAtPathAsync(context.FilePath, context.SelectedSystemName);
    }

    /// <summary>
    ///     Opens a video link for the specified game in the default browser.
    /// </summary>
    public async Task OpenVideoLinkAsync(AvaloniaRightClickContext context)
    {
        var searchTerm = ResolveSearchTerm(context);
        var searchUrl =
            $"{context.Settings.VideoUrl}{Uri.EscapeDataString($"{searchTerm} {context.SelectedSystemName}")}";

        try
        {
            _logErrors.Debug("[ContextMenu] Opening video link: {Url}", searchUrl);
            OpenUrl(searchUrl);
        }
        catch (Win32Exception ex) when (ex.Message.Contains("No application is associated",
                                            StringComparison.OrdinalIgnoreCase)
                                        || ex.Message.Contains("No hay ninguna aplicación asociada",
                                            StringComparison.OrdinalIgnoreCase))
        {
            _logErrors.Error(ex,
                "Win32Exception: No default application configured for opening web links (Video Link)");
            await _messageBox.NoDefaultBrowserConfiguredMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "There was a problem opening the Video Link");
            context.MainViewModel.StatusText = GetStatus("ErrorOpeningVideoLink", "Error opening video link.");
            await _messageBox.ErrorOpeningVideoLinkMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Opens an information link for the specified game in the default browser.
    /// </summary>
    public async Task OpenInfoLinkAsync(AvaloniaRightClickContext context)
    {
        var searchTerm = ResolveSearchTerm(context);
        var searchUrl =
            $"{context.Settings.InfoUrl}{Uri.EscapeDataString($"{searchTerm} {context.SelectedSystemName}")}";

        try
        {
            _logErrors.Debug("[ContextMenu] Opening info link: {Url}", searchUrl);
            OpenUrl(searchUrl);
        }
        catch (Win32Exception ex) when (ex.Message.Contains("No application is associated",
                                            StringComparison.OrdinalIgnoreCase)
                                        || ex.Message.Contains("No hay ninguna aplicación asociada",
                                            StringComparison.OrdinalIgnoreCase))
        {
            _logErrors.Error(ex,
                "Win32Exception: No default application configured for opening web links (Info Link)");
            await _messageBox.NoDefaultBrowserConfiguredMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "There was a problem opening the Info Link");
            context.MainViewModel.StatusText = GetStatus("ErrorOpeningInfoLink", "Error opening info link.");
            await _messageBox.ProblemOpeningInfoLinkMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Opens the ROM history window for the specified game.
    /// </summary>
    public async Task OpenRomHistoryWindowAsync(AvaloniaRightClickContext context)
    {
        var romName = context.FileNameWithoutExtension.ToLowerInvariant();
        var searchTerm = ResolveSearchTerm(context);

        try
        {
            var historyWindow = App.ServiceProvider.GetRequiredService<RomHistoryWindow>();
            historyWindow.Initialize(romName, context.SelectedSystemName, searchTerm);
            historyWindow.Show(context.OwnerWindow);

            _logErrors.Debug("[ContextMenu] Opened ROM history window for '{File}' (system '{System}')",
                context.FileNameWithoutExtension, context.SelectedSystemName);
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "There was a problem opening the History window");
            context.MainViewModel.StatusText = GetStatus("ErrorOpeningROMHistory", "Error opening ROM history.");
            await _messageBox.CouldNotOpenHistoryWindowMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Opens the RetroAchievements window for the specified game, performing hash
    ///     calculation and game lookup (full WPF flow).
    /// </summary>
    public async Task OpenRetroAchievementsWindowAsync(AvaloniaRightClickContext context)
    {
        string? tempExtractionPath = null;
        var loadingStateShown = false;
        try
        {
            var settings = context.Settings;

            // WPF parity: guard against empty fileNameWithoutExtension
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(context.FilePath);
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
            {
                _logErrors.Debug("[RA Service] File name without extension is empty");
                await _messageBox.ErrorMessageBoxAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.RaApiKey) || string.IsNullOrWhiteSpace(settings.RaUsername))
            {
                await _messageBox.AddRaLoginMessageBoxAsync();
                _playSoundEffects.PlayNotificationSound();

                var raSettingsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsSettingsWindow>();
                await raSettingsWindow.ShowDialog(context.OwnerWindow);

                // If user didn't save credentials, or saved empty ones, return
                if (string.IsNullOrWhiteSpace(settings.RaApiKey) ||
                    string.IsNullOrWhiteSpace(settings.RaUsername))
                {
                    return;
                }
            }

            _logErrors.Debug($"[RA Service] Original system name: {context.SelectedSystemName}");
            var raSystemName = RaSystemMatcher.GetBestMatchSystemName(context.SelectedSystemName);
            _logErrors.Debug($"[RA Service] Resolved system name: {raSystemName}");

            var system = context.SelectedSystemManager.GetSystem(context.SelectedSystemName);

            // Check if system is supported for RetroAchievements
            if (!RaHasherTool.IsSystemSupportedForHashing(context.SelectedSystemName))
            {
                _logErrors.Debug(
                    $"[RA Service] System '{context.SelectedSystemName}' is not supported for RetroAchievements.");

                var messageBoxResult = await _messageBox.GameNotSupportedByRetroAchievementsMessageBoxAsync();
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    _playSoundEffects.PlayNotificationSound();
                    context.MainViewModel.StatusText =
                        GetStatus("OpeningRetroAchievements", "Opening RetroAchievements...");
                    var retroAchievementsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsWindow>();
                    retroAchievementsWindow.Show(context.OwnerWindow);
                }

                return;
            }

            // Disable Hash calculation for systems that Group Files by Folder
            if (system is { GroupByFolder: true })
            {
                await _messageBox.SimpleLauncherDoesNotSupportRaHashOfSystemGroupedByFolderMessageBoxAsync();
                _logErrors.Debug(
                    "[RA Service] 'Simple Launcher' does not support RetroAchievements hash of systems Grouped by Folder");
                _logErrors.Debug(
                    "[RA Service] Please edit the system settings and disable the 'Group Files by Folder' option");
                return;
            }

            if (!File.Exists(context.FilePath))
            {
                _logErrors.Debug($"[RA Service] File not found at {context.FilePath}");
                _logErrors.Warning($"[RA Service] File not found at {context.FilePath}");
                await _messageBox.CouldNotFindAFileMessageBoxAsync();
                return;
            }

            if (string.IsNullOrEmpty(raSystemName))
            {
                _logErrors.Debug("[RA Service] SystemName is null or empty after matching");
                _logErrors.Warning("[RA Service] SystemName is null or empty after matching");

                var messageBoxResult = await _messageBox.GameNotSupportedByRetroAchievementsMessageBoxAsync();
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    _playSoundEffects.PlayNotificationSound();
                    context.MainViewModel.StatusText =
                        GetStatus("OpeningRetroAchievements", "Opening RetroAchievements...");
                    var retroAchievementsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsWindow>();
                    retroAchievementsWindow.Show(context.OwnerWindow);
                }

                return;
            }

            var preparingRaMsg = GetStatus("CalculatingGameHash", "Calculating Game Hash... Please wait.");

            // Show loading overlay before starting the hash calculation
            context.MainViewModel.SetLoadingState(true, preparingRaMsg);
            loadingStateShown = true;
            context.MainViewModel.StatusText = preparingRaMsg;

            // Allow the UI to render the overlay before starting CPU-intensive hash calculation
            await Task.Delay(100);

            var raHashResult = await RaHasherTool.GetGameHashForRetroAchievementsAsync(
                context.FilePath, raSystemName, system?.FileFormatsToLaunch ?? [], context.MainViewModel, _logErrors);

            if (string.Equals(raHashResult.ExtractionErrorMessage, "System selection cancelled by user.",
                    StringComparison.Ordinal))
            {
                _logErrors.Debug("[RA Service] User cancelled RetroAchievements hashing");
                return;
            }

            var hash = raHashResult.Hash;
            tempExtractionPath = raHashResult.TempExtractionPath;

            // Prioritize checking if a hash was successfully obtained.
            if (string.IsNullOrEmpty(hash))
            {
                _logErrors.Debug(
                    $"[RA Service] Failed to get hash for '{context.FileNameWithoutExtension}' (System: {raSystemName}). Reason: {raHashResult.ExtractionErrorMessage}");

                if (raHashResult.ExtractionErrorMessage?.Contains("not supported for RetroAchievements hashing",
                        StringComparison.OrdinalIgnoreCase) == true
                    || raHashResult.IsExtractionSuccessful)
                {
                    var messageBoxResult = await _messageBox.GameNotSupportedByRetroAchievementsMessageBoxAsync();
                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        _playSoundEffects.PlayNotificationSound();
                        context.MainViewModel.StatusText =
                            GetStatus("OpeningRetroAchievements", "Opening RetroAchievements...");
                        var retroAchievementsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsWindow>();
                        retroAchievementsWindow.Show(context.OwnerWindow);
                    }
                }
                else
                {
                    await _messageBox.ExtractionFailedMessageBoxAsync();
                }

                return;
            }

            _logErrors.Debug($"[RA Service] Successfully obtained hash: {hash}");

            // Use the lookup method from RetroAchievementsManager
            var matchedGame = RaManager.GetGameInfoByHash(hash);

            if (matchedGame != null)
            {
                _logErrors.Debug(
                    $"[RA Service] Found match for hash: {hash} -> {matchedGame.Title} (ID: {matchedGame.Id})");

                var achievementsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsForAGameWindow>();
                achievementsWindow.Initialize(matchedGame.Id, context.FileNameWithoutExtension);
                achievementsWindow.Show(context.OwnerWindow);
            }
            else
            {
                _logErrors.Debug($"[RA Service] No match found for hash: {hash}");

                var messageBoxResult = await _messageBox.GameNotSupportedByRetroAchievementsMessageBoxAsync();
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    _playSoundEffects.PlayNotificationSound();
                    context.MainViewModel.StatusText =
                        GetStatus("OpeningRetroAchievements", "Opening RetroAchievements...");
                    var retroAchievementsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsWindow>();
                    retroAchievementsWindow.Show(context.OwnerWindow);
                }
            }
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex,
                $"[RA Service] An unexpected error occurred while processing achievements for {context.FileNameWithoutExtension}.");
            await _messageBox.CouldNotOpenAchievementsWindowMessageBoxAsync();
        }
        finally
        {
            // Only decrement when this call incremented: every early return before the
            // hash calculation must not clear another operation's loading overlay.
            if (loadingStateShown) context.MainViewModel.SetLoadingState(false);

            // --- Remove temporary extraction folder ---
            if (!string.IsNullOrEmpty(tempExtractionPath))
            {
                await CleanTempFolder.CleanupTempDirectoryAsync(tempExtractionPath);
                _logErrors.Debug($"[RA Service] Cleaned up temporary extraction folder: {tempExtractionPath}");
            }
        }
    }

    /// <summary>Opens the cover image for the specified game in an image viewer window.</summary>
    public async Task OpenCoverAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningCoverImage", "Opening cover image...");

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(
            context.SelectedSystemManager.GetSystem(context.SelectedSystemName)?.SystemImageFolder);
        var globalImageDirectory = Path.Combine(baseDirectory, "images", context.SelectedSystemName);
        var imageExtensions = _configuration.GetValue<string[]>("ImageExtensions") ?? [".png", ".jpg", ".jpeg"];

        if (TryFindImage(resolvedSystemImageFolder, context.FileNameWithoutExtension, imageExtensions,
                out var foundImagePath)
            || TryFindImage(globalImageDirectory, context.FileNameWithoutExtension, imageExtensions,
                out foundImagePath))
        {
            _logErrors.Debug("[ContextMenu] Opening cover image '{ImagePath}' for '{File}'", foundImagePath,
                context.FileNameWithoutExtension);

            var imageViewerWindow = App.ServiceProvider.GetRequiredService<ImageViewerWindow>();
            imageViewerWindow.LoadImagePath(foundImagePath);
            imageViewerWindow.Show(context.OwnerWindow);
        }
        else
        {
            _logErrors.Debug("[ContextMenu] No cover image found for '{File}' (system '{System}')",
                context.FileNameWithoutExtension, context.SelectedSystemName);

            await _messageBox.ThereIsNoCoverMessageBoxAsync();
        }
    }

    /// <summary>Opens the title snapshot image for the specified game in an image viewer window.</summary>
    public async Task OpenTitleSnapshotAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningTitleSnapshot", "Opening title snapshot...");
        await OpenImageInSubfolderAsync(context, Path.Combine("title_snapshots", context.SelectedSystemName),
            () => _messageBox.ThereIsNoTitleSnapshotMessageBoxAsync());
    }

    /// <summary>Opens the gameplay snapshot image for the specified game in an image viewer window.</summary>
    public async Task OpenGameplaySnapshotAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningGameplaySnapshot", "Opening gameplay snapshot...");
        await OpenImageInSubfolderAsync(context, Path.Combine("gameplay_snapshots", context.SelectedSystemName),
            () => _messageBox.ThereIsNoGameplaySnapshotMessageBoxAsync());
    }

    /// <summary>Opens the cart image for the specified game in an image viewer window.</summary>
    public async Task OpenCartAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningCartImage", "Opening cart image...");
        await OpenImageInSubfolderAsync(context, Path.Combine("carts", context.SelectedSystemName),
            () => _messageBox.ThereIsNoCartMessageBoxAsync());
    }

    /// <summary>Plays the video file for the specified game using the default media player.</summary>
    public async Task PlayVideoAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("PlayingVideo", "Playing video...");
        var videoDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "videos", context.SelectedSystemName);
        string[] videoExtensions = [".mp4", ".avi", ".mkv"];

        foreach (var extension in videoExtensions)
        {
            var videoPath = Path.Combine(videoDirectory, context.FileNameWithoutExtension + extension);
            if (!File.Exists(videoPath)) continue;

            _logErrors.Debug("[ContextMenu] Playing video '{VideoPath}' for '{File}'", videoPath,
                context.FileNameWithoutExtension);

            OpenUrlOrFile(videoPath);
            return;
        }

        _logErrors.Debug("[ContextMenu] No video file found for '{File}' (system '{System}')",
            context.FileNameWithoutExtension, context.SelectedSystemName);

        await _messageBox.ThereIsNoVideoFileMessageBoxAsync();
    }

    /// <summary>Opens the PDF manual for the specified game using the default PDF viewer.</summary>
    public async Task OpenManualAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningManual", "Opening manual...");
        await OpenPdfInSubfolderAsync(context, Path.Combine("manuals", context.SelectedSystemName),
            () => _messageBox.ThereIsNoManualMessageBoxAsync(),
            () => _messageBox.CouldNotOpenManualMessageBoxAsync());
    }

    /// <summary>Opens the PDF walkthrough for the specified game using the default PDF viewer.</summary>
    public async Task OpenWalkthroughAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningWalkthrough", "Opening walkthrough...");
        await OpenPdfInSubfolderAsync(context, Path.Combine("walkthrough", context.SelectedSystemName),
            () => _messageBox.ThereIsNoWalkthroughMessageBoxAsync(),
            () => _messageBox.CouldNotOpenWalkthroughMessageBoxAsync());
    }

    /// <summary>Opens the cabinet image for the specified game in an image viewer window.</summary>
    public async Task OpenCabinetAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningCabinetImage", "Opening cabinet image...");
        await OpenImageInSubfolderAsync(context, Path.Combine("cabinets", context.SelectedSystemName),
            () => _messageBox.ThereIsNoCabinetMessageBoxAsync());
    }

    /// <summary>Opens the flyer image for the specified game in an image viewer window.</summary>
    public async Task OpenFlyerAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningFlyerImage", "Opening flyer image...");
        await OpenImageInSubfolderAsync(context, Path.Combine("flyers", context.SelectedSystemName),
            () => _messageBox.ThereIsNoFlyerMessageBoxAsync());
    }

    /// <summary>Opens the PCB (printed circuit board) image for the specified game in an image viewer window.</summary>
    public async Task OpenPcbAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("OpeningPCBImage", "Opening PCB image...");
        await OpenImageInSubfolderAsync(context, Path.Combine("pcbs", context.SelectedSystemName),
            () => _messageBox.ThereIsNoPcbMessageBoxAsync());
    }

    /// <summary>
    ///     Launches the specified game, waits for its window to appear, lets the user pick
    ///     the window, captures a screenshot, and saves it as the game's cover image.
    ///     Windows-only: the Win32 capture code is compiled into the net10.0-windows target,
    ///     so other platforms log an Information message and return (LB-04).
    /// </summary>
    public async Task TakeScreenshotOfSelectedWindowAsync(AvaloniaRightClickContext context)
    {
#if !WINDOWS
        // The context-menu entry is hidden outside Windows; this guard keeps any stray caller
        // from creating an empty images folder and showing a misleading "error reported" dialog.
        // Expected platform limitation, so Information (never Warning/Error).
        _logErrors.Information(
            "[TakeScreenshotOfSelectedWindow] Taking a screenshot is only supported on Windows");
        await Task.CompletedTask;
#else
        context.MainViewModel.StatusText = GetStatus("TakingScreenshot", "Taking screenshot...");
        try
        {
            var system = context.SelectedSystemManager.GetSystem(context.SelectedSystemName);
            var systemImageFolder = PathHelper.ResolveRelativeToAppDirectory(system?.SystemImageFolder);
            if (string.IsNullOrEmpty(systemImageFolder))
            {
                // Fallback to default if resolution fails or path is empty
                systemImageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images",
                    context.SelectedSystemName);
            }

            try
            {
                Directory.CreateDirectory(systemImageFolder);
            }
            catch (Exception ex)
            {
                _logErrors.Error(ex,
                    $"[TakeScreenshotOfSelectedWindow] Could not create the system image folder: {systemImageFolder}");
            }

            await TakeScreenshotWindowsAsync(context, systemImageFolder);
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "[TakeScreenshotOfSelectedWindow] There was a problem saving the screenshot");
            await _messageBox.CouldNotSaveScreenshotMessageBoxAsync();
        }
#endif
    }

#if WINDOWS
    private async Task TakeScreenshotWindowsAsync(AvaloniaRightClickContext context, string systemImageFolder)
    {
        // Capture initial window count before launch
        var initialCount = CoreWindowManager.GetOpenWindows().Count;
        _logErrors.Debug($"[Screenshot] Initial window count: {initialCount}");

        // Launch game (fire and forget, as in WPF)
        _ = context.MainViewModel.LaunchGameAtPathAsync(context.FilePath, context.SelectedSystemName);

        // Minimum wait time to process startup
        await Task.Delay(2000);

        // Poll for new windows
        var maxWaitTime = TimeSpan.FromSeconds(30); // Max 30 seconds
        var pollInterval = TimeSpan.FromMilliseconds(500); // Poll every 500ms
        var stopwatch = Stopwatch.StartNew();
        var newWindowDetected = false;

        while (stopwatch.Elapsed < maxWaitTime && !newWindowDetected)
        {
            await Task.Delay(pollInterval);

            var currentCount = CoreWindowManager.GetOpenWindows().Count;
            if (currentCount > initialCount)
            {
                // New window(s) appeared - assume game/emulator launched
                _logErrors.Debug(
                    $"[Screenshot] New window detected. Current count: {currentCount} (initial: {initialCount})");
                newWindowDetected = true;
                break;
            }

            if (stopwatch.Elapsed.TotalSeconds % 5 < pollInterval.TotalMilliseconds / 1000.0)
            {
                _logErrors.Debug(
                    $"[Screenshot] Polling... Elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s / {maxWaitTime.TotalSeconds}s");
            }
        }

        stopwatch.Stop();

        if (!newWindowDetected)
        {
            // Timeout - no new windows appeared
            _logErrors.Debug(
                $"[Screenshot] Timeout after {stopwatch.Elapsed.TotalSeconds:F1}s. No new windows detected.");
            await _messageBox.GameLaunchTimeoutMessageBoxAsync();
            return;
        }

        // Proceed with window selection
        var openWindows = CoreWindowManager.GetOpenWindows();
        var dialog = App.ServiceProvider.GetRequiredService<WindowSelectionDialogWindow>();
        dialog.Initialize(openWindows);
        if (!await dialog.ShowDialog<bool>(context.OwnerWindow) || dialog.SelectedWindowHandle == IntPtr.Zero) return;

        var hWnd = dialog.SelectedWindowHandle;

        // Try to get the client area dimensions; fall back to the full window dimensions
        if (!AvaloniaWindowScreenshot.GetClientAreaRect(hWnd, out var rectangle)
            && !AvaloniaWindowScreenshot.GetWindowRect(hWnd, out rectangle))
            throw new InvalidOperationException("Failed to retrieve window dimensions.");

        var width = rectangle.Right - rectangle.Left;
        var height = rectangle.Bottom - rectangle.Top;

        // Add a check for invalid dimensions (i.e., a minimized window)
        if (width <= 0 || height <= 0)
        {
            await _messageBox.CannotScreenshotMinimizedWindowMessageBoxAsync();
            _logErrors.Debug("Cannot take a screenshot of a minimized window");
            return;
        }

        var fileNameWithoutExtension = context.FileNameWithoutExtension;
        var screenshotPath = Path.Combine(systemImageFolder, $"{fileNameWithoutExtension}.png");

        // Capture the window into a bitmap and save it
        AvaloniaWindowCapture.CaptureRectangleToPng(rectangle.Left, rectangle.Top, width, height, screenshotPath,
            _logErrors);

        _playSoundEffects.PlayShutterSound();

        // Wait, then show the flash effect
        await Task.Delay(1000);
        var flashWindow = App.ServiceProvider.GetRequiredService<FlashOverlayWindow>();
        await flashWindow.ShowFlashAsync();

        // Reload the current Game List
        await RefreshCurrentGameListAsync(context);
    }
#endif

    /// <summary>
    ///     Deletes the specified game file from disk and reloads the game list.
    /// </summary>
    public async Task DeleteGameAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("DeletingGame", "Deleting game...");
        if (File.Exists(context.FilePath))
        {
            try
            {
                await DeleteFiles.TryDeleteFileAsync(context.FilePath);

                _playSoundEffects.PlayTrashSound();

                await _messageBox.FileSuccessfullyDeletedMessageBoxAsync(context.FileNameWithExtension);

                // Remove the deleted game from favorites so the entry does not linger.
                await RemoveFromFavoritesSilentlyAsync(context);

                // Reload the current Game List to reflect the deletion
                await RefreshCurrentGameListAsync(context);

                _logErrors.Information("[ContextMenu] Deleted game file '{FilePath}'", context.FilePath);
            }
            catch (Exception ex)
            {
                var errorMessage =
                    $"An error occurred while trying to delete the file '{context.FileNameWithExtension}'.";
                _logErrors.Error(ex, errorMessage);

                await _messageBox.FileCouldNotBeDeletedMessageBoxAsync(context.FileNameWithExtension);
            }
        }
        else
        {
            _logErrors.Debug("[ContextMenu] Cannot delete game file because it no longer exists: '{FilePath}'",
                context.FilePath);

            // Notify user the file no longer exists
            await _messageBox.FileNoLongerExistsMessageBoxAsync(context.FileNameWithExtension);

            // Refresh the game list to remove the stale entry
            await RefreshCurrentGameListAsync(context);
        }
    }

    /// <summary>
    ///     Deletes the cover image for the specified game and reloads the game list.
    /// </summary>
    public async Task DeleteCoverImageAsync(AvaloniaRightClickContext context)
    {
        context.MainViewModel.StatusText = GetStatus("DeletingCoverImage", "Deleting cover image...");
        var systemImageFolder =
            context.SelectedSystemManager.GetSystem(context.SelectedSystemName)?.SystemImageFolder ?? "";
        var coverPath = _findCoverImage.FindCoverImagePath(context.FileNameWithoutExtension, context.SelectedSystemName,
            systemImageFolder);

        try
        {
            _playSoundEffects.PlayTrashSound();

            if (string.Equals(Path.GetFileNameWithoutExtension(coverPath), context.FileNameWithoutExtension,
                    StringComparison.Ordinal)
                && !string.Equals(Path.GetFileNameWithoutExtension(coverPath), "default", StringComparison.Ordinal))
            {
                await DeleteFiles.TryDeleteFileAsync(coverPath);
            }

            await Task.Delay(400);

            if (!File.Exists(coverPath))
            {
                await _messageBox.FileSuccessfullyDeletedMessageBoxAsync(coverPath);

                _logErrors.Information("[ContextMenu] Deleted cover image '{CoverPath}'", coverPath);

                // Invalidate the cards' cached covers and reload the current Game List
                await RefreshCurrentGameListAsync(context);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"An error occurred while trying to delete the game cover '{coverPath}'.";
            _logErrors.Error(ex, errorMessage);

            await _messageBox.FileCouldNotBeDeletedMessageBoxAsync(coverPath);
        }
    }

    // ──── helpers ────────────────────────────────────────────────────────────

    private string GetLogFilePath()
    {
        return PathHelper.ResolveLogFilePath(_configuration);
    }

    /// <summary>
    ///     Resolves the MAME machine description for the file name (falls back to the raw name).
    /// </summary>
    private string ResolveSearchTerm(AvaloniaRightClickContext context)
    {
        return _mameData.Lookup.TryGetValue(context.FileNameWithoutExtension, out var description)
               && !string.IsNullOrWhiteSpace(description)
            ? description
            : context.FileNameWithoutExtension;
    }

    private static void OpenUrl(string url)
    {
        // WPF parity: opening a URL/file with the OS shell is cross-platform
        // (UseShellExecute resolves the default browser/app on every OS). The
        // Windows-only guard previously made these context actions silent no-ops
        // on Linux/macOS.
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static void OpenUrlOrFile(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static bool TryFindImage(string? directory, string fileNameWithoutExtension, string[] extensions,
        out string? foundPath)
    {
        foundPath = null;
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return false;

        foreach (var extension in extensions)
        {
            var imagePath = Path.Combine(directory, fileNameWithoutExtension + extension);
            if (!File.Exists(imagePath)) continue;

            foundPath = imagePath;
            return true;
        }

        return false;
    }

    private async Task OpenImageInSubfolderAsync(AvaloniaRightClickContext context, string directoryRelative,
        Func<Task> notFoundMessage)
    {
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directoryRelative);
        var extensions = _configuration.GetValue<string[]>("ImageExtensions") ?? [".png", ".jpg", ".jpeg"];

        foreach (var extension in extensions)
        {
            var imagePath = Path.Combine(directory, context.FileNameWithoutExtension + extension);
            if (!File.Exists(imagePath)) continue;

            _logErrors.Debug("[ContextMenu] Opening image '{ImagePath}' for '{File}'", imagePath,
                context.FileNameWithoutExtension);

            var imageViewerWindow = App.ServiceProvider.GetRequiredService<ImageViewerWindow>();
            imageViewerWindow.LoadImagePath(imagePath);
            imageViewerWindow.Show(context.OwnerWindow);
            return;
        }

        _logErrors.Debug("[ContextMenu] No image found for '{File}' in '{Directory}'",
            context.FileNameWithoutExtension, directory);

        await notFoundMessage();
    }

    private async Task OpenPdfInSubfolderAsync(AvaloniaRightClickContext context, string directoryRelative,
        Func<Task> notFoundMessage, Func<Task> couldNotOpenMessage)
    {
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directoryRelative);
        var pdfPath = Path.Combine(directory, context.FileNameWithoutExtension + ".pdf");

        if (!File.Exists(pdfPath))
        {
            _logErrors.Debug("[ContextMenu] No PDF found for '{File}' in '{Directory}'",
                context.FileNameWithoutExtension, directory);

            await notFoundMessage();
            return;
        }

        try
        {
            _logErrors.Debug("[ContextMenu] Opening PDF '{PdfPath}' for '{File}'", pdfPath,
                context.FileNameWithoutExtension);

            OpenUrlOrFile(pdfPath);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1155) // ERROR_NO_ASSOCIATION
        {
            // No application is associated with the file format (no PDF viewer installed)
            _logErrors.Error(ex, "There was a problem opening the PDF. No PDF viewer is installed");
            await _messageBox.NoPdfViewerInstalledMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "There was a problem opening the PDF");
            await couldNotOpenMessage();
        }
    }

    /// <summary>
    ///     Removes the favorite entry for this game without notifications (used on game deletion).
    /// </summary>
    private async Task RemoveFromFavoritesSilentlyAsync(AvaloniaRightClickContext context)
    {
        try
        {
            var favoriteToRemove = context.FavoritesManager.FavoriteList.FirstOrDefault(f =>
                f.FileName.Equals(context.FileNameWithExtension, StringComparison.OrdinalIgnoreCase)
                && f.SystemName.Equals(context.SelectedSystemName, StringComparison.OrdinalIgnoreCase));

            if (favoriteToRemove == null)
            {
                _logErrors.Debug("[ContextMenu] No favorite entry found to remove for deleted game '{File}'",
                    context.FileNameWithExtension);
                return;
            }

            context.FavoritesManager.FavoriteList.Remove(favoriteToRemove);
            await context.FavoritesManager.SaveFavoritesAsync();
            context.OnFavoriteRemoved?.Invoke();
            context.MainViewModel.RefreshFavoritesAndHistory();

            _logErrors.Information("[ContextMenu] Removed favorite entry for deleted game '{File}'",
                context.FileNameWithExtension);
        }
        catch (Exception ex)
        {
            _logErrors.Error(ex, "Error removing the favorite entry of a deleted game");
        }
    }

    /// <summary>
    ///     Reloads the currently displayed game list (WPF LoadGameFilesAsync parity).
    /// </summary>
    private Task RefreshCurrentGameListAsync(AvaloniaRightClickContext context)
    {
        try
        {
            try
            {
                var mainViewModel = context.MainViewModel;
                if (!string.IsNullOrEmpty(mainViewModel.SelectedSystem))
                {
                    mainViewModel.NavigateToSystemCommand.Execute(mainViewModel.SelectedSystem);
                    _logErrors.Debug("[ContextMenu] Reloaded the current game list");
                }
            }
            catch (Exception ex)
            {
                _logErrors.Error(ex, "[ContextMenu] There was a problem reloading the Game List");
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}