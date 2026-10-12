using System.Windows;
using Application = System.Windows.Application;

namespace SimpleLauncher;

/// <summary>
///     Partial MainWindow containing launch tool click handlers for batch file creation and emulator utilities.
/// </summary>
public partial class MainWindow
{
    private async void CreateBatchFilesForXbox360XBLAGames_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.CreateBatchFilesForXbox360XblaGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method CreateBatchFilesForXbox360XBLAGames_ClickAsync");
        }
    }

    private async void XisoStudio_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.LaunchXisoStudioAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method XisoStudio_ClickAsync");
        }
    }

    private async void ChdStudio_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.LaunchChdStudioAsync(_selectedRomFolders?.FirstOrDefault());
            _logger.Debug($"Called CHDStudio with args: {_selectedRomFolders?.FirstOrDefault()}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method ChdStudio_ClickAsync");
        }
    }

    private async void BatchConvertToCompressedFile_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.BatchConvertToCompressedFileAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method BatchConvertToCompressedFile_ClickAsync");
        }
    }

    private async void RvzStudio_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.LaunchRvzStudioAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method RvzStudio_ClickAsync");
        }
    }

    private async void CreateBatchFilesForPS3Games_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.CreateBatchFilesForPs3GamesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method CreateBatchFilesForPS3Games_ClickAsync");
        }
    }

    private async void CreateBatchFilesForScummVMGames_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.CreateBatchFilesForScummVmGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method CreateBatchFilesForScummVMGames_ClickAsync");
        }
    }

    private async void CreateBatchFilesForWindowsGames_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.CreateBatchFilesForWindowsGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method CreateBatchFilesForWindowsGames_ClickAsync");
        }
    }

    private async void FindRomCover_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await ResetUiAsync();
            await _externalToolLauncher.FindRomCoverLaunchAsync(_selectedImageFolder,
                _selectedRomFolders?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method FindRomCover_ClickAsync");
        }
    }

    private async void RomValidator_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await _externalToolLauncher.RomValidatorAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method RomValidator_ClickAsync");
        }
    }

    private async void RetroGameCoverDownloader_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusBarService.UpdateContent((string)Application.Current.TryFindResource("LaunchingTool") ??
                                                 "Launching tool...");
            _audioInput.PlayNotificationSound();
            await ResetUiAsync();
            await _externalToolLauncher.RetroGameCoverDownloaderAsync(_selectedImageFolder,
                _selectedRomFolders?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method RetroGameCoverDownloader_ClickAsync");
        }
    }
}