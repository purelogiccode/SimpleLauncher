using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher;

/// <summary>
///     Partial MainWindow containing menu item click handlers and system management operations.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    ///     The ordered aspect ratios for cycling through button thumbnail sizes.
    /// </summary>
    private static readonly string[] AspectRatios =
    [
        AppConstants.AspectSquare, AppConstants.AspectWider, AppConstants.AspectSuperWider,
        AppConstants.AspectSuperWider2, AppConstants.AspectTaller, AppConstants.AspectSuperTaller,
        AppConstants.AspectSuperTaller2
    ];

    /// <summary>
    ///     Sets the current view mode (e.g. grid or list) and updates the UI accordingly.
    /// </summary>
    /// <param name="viewMode">The view mode identifier to apply.</param>
    internal void SetViewMode(string viewMode)
    {
        _menuOrchestrator.SetViewMode(viewMode);
    }

    private void EasyMode_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleEasyMode();
    }

    private void ExpertMode_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleExpertMode();
    }

    private void DownloadImagePack_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleDownloadImagePack();
    }

    private async void ScanForMicrosoftWindowsGames_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                await _menuOrchestrator.HandleScanForWindowsGamesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method ScanForMicrosoftWindowsGames_ClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ScanForMicrosoftWindowsGames_ClickAsync");
        }
    }

    /// <summary>
    ///     Resets the UI to its default state asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    internal async Task ResetUiAsync()
    {
        try
        {
            await UiResetService.ResetUiAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ResetUiAsync");
        }
    }

    /// <summary>
    ///     Reloads the system manager and refreshes the system list.
    /// </summary>
    public Task LoadOrReloadSystemManagerAsync()
    {
        return _gameBrowser.LoadOrReloadSystemManagerAsync();
    }

    private async void EditLinksClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                await _menuOrchestrator.HandleEditLinksAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method EditLinksClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method EditLinksClickAsync");
        }
    }

    private async void ToggleGamepad_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (sender is not MenuItem menuItem) return;

                await _menuOrchestrator.HandleToggleGamepadAsync(menuItem.IsChecked);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method ToggleGamepad_Click");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ToggleGamepad_Click");
        }
    }

    private void SetGamepadDeadZone_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleSetGamepadDeadZone();
    }

    private async void ToggleFuzzyMatchingClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (sender is not MenuItem menuItem) return;

                await _menuOrchestrator.HandleToggleFuzzyMatchingAsync(menuItem.IsChecked);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method ToggleFuzzyMatchingClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ToggleFuzzyMatchingClickAsync");
        }
    }

    private async void SetFuzzyMatchingThresholdClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                await _menuOrchestrator.HandleSetFuzzyMatchingThresholdAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method SetFuzzyMatchingThresholdClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method SetFuzzyMatchingThresholdClickAsync");
        }
    }

    private async void ToggleAnnotationStrippingClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                if (sender is not MenuItem menuItem) return;

                await _menuOrchestrator.HandleToggleAnnotationStrippingAsync(menuItem.IsChecked);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method ToggleAnnotationStrippingClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ToggleAnnotationStrippingClickAsync");
        }
    }

    private void Support_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleSupport();
    }

    private async void Donate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                await _menuOrchestrator.HandleDonateAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method Donate_Click");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method Donate_Click");
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleAbout();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleExit();
    }

    private async void ShowAllGamesClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleShowGamesAsync("ShowAll");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ShowAllGamesClickAsync");
        }
    }

    private async void ShowGamesWithCoverClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleShowGamesAsync("ShowWithCover");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ShowGamesWithCoverClickAsync");
        }
    }

    private async void ShowGamesWithoutCoverClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleShowGamesAsync("ShowWithoutCover");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ShowGamesWithoutCoverClickAsync");
        }
    }

    private async void ButtonSizeClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem clickedItem) return;

            var sizeText = clickedItem.Name.Replace("Size", "");
            if (!int.TryParse(new string(sizeText.Where(char.IsDigit).ToArray()),
                    CultureInfo.InvariantCulture, out var newSize))
            {
                return;
            }

            await _menuOrchestrator.HandleButtonSizeAsync(newSize);
            SyncCardSizeSlider();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ButtonSizeClickAsync");
        }
    }

    /// <summary>
    ///     Debounce interval (milliseconds) of the top-bar card size slider: the game list
    ///     is reloaded once after the user stops dragging instead of on every value change.
    /// </summary>
    private const int CardSizeSliderDebounceMilliseconds = 350;

    private DispatcherTimer? _cardSizeSliderDebounceTimer;

    /// <summary>
    ///     Slider twin of the "Set Button Size" menu: resizes the game buttons once the
    ///     user stops dragging. The actual resize runs through the same menu orchestrator
    ///     path so settings, check marks and the status bar stay in sync.
    /// </summary>
    private void ButtonSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_cardSizeSliderDebounceTimer is null)
        {
            _cardSizeSliderDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(CardSizeSliderDebounceMilliseconds)
            };
            _cardSizeSliderDebounceTimer.Tick += CardSizeSliderDebounceTimerTick;
        }

        // Restart the debounce on every change, then skip the reload when the slider
        // already matches the applied size (startup and programmatic syncs).
        _cardSizeSliderDebounceTimer.Stop();

        var size = (int)Math.Round(e.NewValue);
        if (size == _settings.ThumbnailSize) return;

        _cardSizeSliderDebounceTimer.Start();
    }

    private async void CardSizeSliderDebounceTimerTick(object? sender, EventArgs e)
    {
        try
        {
            _cardSizeSliderDebounceTimer?.Stop();

            try
            {
                var newSize = (int)Math.Round(CardSizeSlider.Value);
                if (newSize == _settings.ThumbnailSize) return;

                await _menuOrchestrator.HandleButtonSizeAsync(newSize);
                // The handler snaps to the 50px grid, so re-sync the slider to the applied size.
                SyncCardSizeSlider();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method CardSizeSliderDebounceTimerTick");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method CardSizeSliderDebounceTimerTick");
        }
    }

    /// <summary>
    ///     Keeps the slider handle in sync when the thumbnail size changes elsewhere
    ///     (Set Button Size menu, zoom buttons, Ctrl+Mouse wheel).
    /// </summary>
    private void SyncCardSizeSlider()
    {
        CardSizeSlider.Value = _settings.ThumbnailSize;
    }

    private async void ButtonAspectRatioClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem clickedItem) return;

            await _menuOrchestrator.HandleButtonAspectRatioAsync(clickedItem.Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ButtonAspectRatioClickAsync");
        }
    }

    private async void GamesPerPageClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem clickedItem) return;

            var pageText = clickedItem.Name.Replace("Page", "");
            if (!int.TryParse(new string(pageText.Where(char.IsDigit).ToArray()),
                    CultureInfo.InvariantCulture, out var newPage))
            {
                return;
            }

            await _menuOrchestrator.HandleGamesPerPageAsync(newPage);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method GamesPerPageClickAsync");
        }
    }

    private void ShowGlobalSearchWindow_Click()
    {
        _menuOrchestrator.HandleShowGlobalSearch();
    }

    private void ShowGlobalStatsWindow_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleShowGlobalStats();
    }

    private void ShowFavoritesWindow_Click()
    {
        _menuOrchestrator.HandleShowFavorites();
    }

    private void ShowPlayHistoryWindow_Click()
    {
        _menuOrchestrator.HandleShowPlayHistory();
    }

    /// <summary>
    ///     Opens the RetroAchievements window when the menu item is clicked.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The routed event arguments.</param>
    private void ShowRetroAchievementsWindowClick(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleShowRetroAchievements();
    }

    private void UpdateShowGamesCheckMarks(string selectedValue)
    {
        _menuOrchestrator.UpdateShowGamesCheckMarks(selectedValue);
    }

    private void UpdateButtonAspectRatioCheckMarks(string selectedValue)
    {
        _menuOrchestrator.UpdateButtonAspectRatioCheckMarks(selectedValue);
    }

    private async void NavToggleButtonAspectRatioClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isLoadingGames) return;

            CancelAndRecreateToken();

            _audioInput.PlayNotificationSound();

            // Get the current index of the aspect ratio
            var currentIndex = Array.IndexOf(AspectRatios, _settings.ButtonAspectRatio);

            // Calculate the next index, wrapping around to 0 if at the end
            var nextIndex = (currentIndex + 1) % AspectRatios.Length;

            // Get the new aspect ratio
            var newAspectRatio = AspectRatios[nextIndex];

            // Update the settings
            _settings.ButtonAspectRatio = newAspectRatio;
            await _settings.SaveAsync();

            UpdateButtonAspectRatioCheckMarks(newAspectRatio);
            // Notify user
            UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("TogglingButtonAspectRatio") ??
                "Toggling button aspect ratio...");

            var (sl, sq) = GetLoadGameFilesParams();
            SetLoadingState(true,
                (string)Application.Current.TryFindResource("ReloadingGames") ?? "Reloading games...");
            await Task.Yield(); // Allow UI to render the loading overlay
            await _gameBrowser.LoadGameFilesAsync(sl, sq, _cancellationSource.Token);
        }
        catch (Exception ex)
        {
            // Notify developer
            const string errorMessage = "Error in the method NavToggleButtonAspectRatioClickAsync.";
            _logger.Error(ex, errorMessage);

            // Notify user
            await _messageBox.ErrorMessageBoxAsync();
        }
    }

    private async void FilenameDisplayMode_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem clickedItem) return;

            var mode = clickedItem.Name switch
            {
                "FilenameDisplayOriginal" => "Original",
                "FilenameDisplayCleanUp" => "CleanUp",
                "FilenameDisplayNoFilename" => "NoFilename",
                _ => "Original"
            };

            await _menuOrchestrator.HandleFilenameDisplayModeAsync(mode);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method FilenameDisplayMode_ClickAsync");
        }
    }

    private async void DisplayMachineName_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;

            await _menuOrchestrator.HandleDisplayMachineNameAsync(menuItem.IsChecked);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method DisplayMachineName_ClickAsync");
        }
    }

    private async void FilenameFontSize_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem clickedItem) return;

            var size = clickedItem.Name switch
            {
                "FilenameFontSizeSmall" => "Small",
                "FilenameFontSizeNormal" => "Normal",
                "FilenameFontSizeBig" => "Big",
                _ => "Normal"
            };

            await _menuOrchestrator.HandleFilenameFontSizeAsync(size);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method FilenameFontSize_ClickAsync");
        }
    }

    private async void MachineNameFontSize_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem clickedItem) return;

            var size = clickedItem.Name switch
            {
                "MachineNameFontSizeSmall" => "Small",
                "MachineNameFontSizeNormal" => "Normal",
                "MachineNameFontSizeBig" => "Big",
                _ => "Normal"
            };

            await _menuOrchestrator.HandleMachineNameFontSizeAsync(size);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method MachineNameFontSize_ClickAsync");
        }
    }

    private async void ChangeViewMode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                await _menuOrchestrator.HandleChangeViewModeAsync(sender);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method ChangeViewMode_Click");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ChangeViewMode_Click");
        }
    }

    private void ApplyShowGamesSetting()
    {
        UpdateShowGamesCheckMarks(_settings.ShowGames);
        UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("ApplyingGameVisibilitySettings") ??
            "Applying game visibility settings...");
    }

    private void ChangeLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _menuOrchestrator.HandleChangeLanguage(menuItem);
    }

    private void NavRestartButton_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.HandleRestart();
    }

    private void NavGlobalSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ShowGlobalSearchWindow_Click();
    }

    private void NavFavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        ShowFavoritesWindow_Click();
    }

    private void NavHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPlayHistoryWindow_Click();
    }

    private void NavRetroAchievementsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowRetroAchievementsWindowClick(sender, e);
    }

    private void NavExpertModeButton_Click(object sender, RoutedEventArgs e)
    {
        ExpertMode_Click(sender, e);
    }

    private async void NavSelectedSystemFavoriteButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleShowSystemFavoritesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavSelectedSystemFavoriteButtonClickAsync");
        }
    }

    private async void NavRandomLuckGameButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleFeelingLuckyAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavRandomLuckGameButtonClickAsync");
        }
    }

    private async void NavShowGamesWithRetroAchievementsButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleShowGamesWithRetroAchievementsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavShowGamesWithRetroAchievementsButtonClickAsync");
        }
    }

    private async void RescanRetroAchievements_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleRescanRetroAchievementsForSelectedSystemAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method RescanRetroAchievements_Click");
        }
    }

    private void OpenAppDataPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var appDataPath = AppDataPaths.SimpleLauncherDataFolder;
            if (string.IsNullOrEmpty(appDataPath) || !Directory.Exists(appDataPath))
            {
                _logger.Debug("AppData path does not exist: " + appDataPath);
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = appDataPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method OpenAppDataPath_Click");
        }
    }

    private async void NavZoomInButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleZoomInAsync();
            SyncCardSizeSlider();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavZoomInButtonClickAsync");
        }
    }

    private async void NavZoomOutButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleZoomOutAsync();
            SyncCardSizeSlider();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavZoomOutButtonClickAsync");
        }
    }

    private async void NavToggleViewModeClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleToggleViewModeAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method NavToggleViewModeClickAsync");
        }
    }

    private async void SoundConfiguration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isDisposed) return;

            try
            {
                await _menuOrchestrator.HandleSoundConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in the method SoundConfiguration_Click");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method SoundConfiguration_Click");
        }
    }

    private async void ShowRetroAchievementsSettingsWindow_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _menuOrchestrator.HandleShowRetroAchievementsSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ShowRetroAchievementsSettingsWindow_ClickAsync");
        }
    }

    private async void ToggleRetroAchievementButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;

            await _menuOrchestrator.HandleToggleRetroAchievementButtonAsync(menuItem.IsChecked);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ToggleRetroAchievementButton_ClickAsync");
        }
    }

    private async void ToggleVideoLinkButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;

            await _menuOrchestrator.HandleToggleVideoLinkButtonAsync(menuItem.IsChecked);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ToggleVideoLinkButton_ClickAsync");
        }
    }

    private async void ToggleInfoLinkButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;

            await _menuOrchestrator.HandleToggleInfoLinkButtonAsync(menuItem.IsChecked);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ToggleInfoLinkButton_ClickAsync");
        }
    }

    // Emulator config windows
    private void ShowXeniaSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Xenia");
    }

    private void ShowMameSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Mame");
    }

    private void ShowRetroArchSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("RetroArch");
    }

    private void ShowSupermodelSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Supermodel");
    }

    private void ShowMednafenSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Mednafen");
    }

    private void ShowSegaModel2Settings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("SegaModel2");
    }

    private void ShowAresSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Ares");
    }

    private void ShowDaphneSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Daphne");
    }

    private void ShowBlastemSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Blastem");
    }

    private void ShowMesenSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Mesen");
    }

    private void ShowDuckStationSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("DuckStation");
    }

    private void ShowRPCS3Settings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("RPCS3");
    }

    private void ShowFlycastSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Flycast");
    }

    private void ShowStellaSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Stella");
    }

    private void ShowDolphinSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Dolphin");
    }

    private void ShowCemuSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Cemu");
    }

    private void ShowPcsx2Settings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("PCSX2");
    }

    private void ShowAzaharSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Azahar");
    }

    private void ShowYumirSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Yumir");
    }

    private void ShowRaineSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Raine");
    }

    private void ShowRedreamSettings_Click(object sender, RoutedEventArgs e)
    {
        _menuOrchestrator.ShowEmulatorConfigWindow("Redream");
    }

    private void ChangeBaseTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _menuOrchestrator.ChangeBaseTheme(menuItem);
    }

    private void ChangeAccentColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _menuOrchestrator.ChangeAccentColor(menuItem);
    }
}