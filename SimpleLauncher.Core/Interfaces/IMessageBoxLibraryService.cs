using System.Text;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Provides localized message box dialogs for user notifications, errors, warnings, and confirmations throughout the
///     application.
/// </summary>
public interface IMessageBoxLibraryService
{
    /// <summary>
    ///     Displays instructions for taking a screenshot of a game window.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TakeScreenShotMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when a screenshot fails to save.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotSaveScreenshotMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that the specified game is already in favorites.
    /// </summary>
    /// <param name="fileNameWithExtension">The file name with extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GameIsAlreadyInFavoritesMessageBoxAsync(string fileNameWithExtension);

    /// <summary>
    ///     Displays an error message when adding a game to favorites fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorWhileAddingFavoritesMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when removing a game from favorites fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorWhileRemovingGameFromFavoriteMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Update History window fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorOpeningTheUpdateHistoryWindowMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when a video link fails to open in the browser.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorOpeningVideoLinkMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when an info link fails to open in the browser.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProblemOpeningInfoLinkMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when a URL fails to open in the browser.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorOpeningUrlMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no cover image is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoCoverMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no title snapshot is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoTitleSnapshotMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no gameplay snapshot is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoGameplaySnapshotMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no cart file is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoCartMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no video file is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoVideoFileMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the game manual fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotOpenManualMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when no PDF viewer is installed to open a manual.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoPdfViewerInstalledMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no manual is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoManualMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no walkthrough file is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoWalkthroughMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no cabinet file is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoCabinetMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no flyer file is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoFlyerMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no PCB file is associated with the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoPcbMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming a file was deleted.
    /// </summary>
    /// <param name="fileNameWithExtension">The file name with extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileSuccessfullyDeletedMessageBoxAsync(string fileNameWithExtension);

    /// <summary>
    ///     Displays an error message when a file could not be deleted.
    /// </summary>
    /// <param name="fileNameWithExtension">The file name with extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileCouldNotBeDeletedMessageBoxAsync(string fileNameWithExtension);

    /// <summary>
    ///     Displays an info message when a file no longer exists on disk and the game list will be refreshed.
    /// </summary>
    /// <param name="fileNameWithExtension">The file name with extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileNoLongerExistsMessageBoxAsync(string fileNameWithExtension);

    /// <summary>
    ///     Displays an error when the default image file is missing, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DefaultImageNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the global search encounters an error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GlobalSearchErrorMessageBoxAsync();

    /// <summary>
    ///     Displays a warning prompting the user to enter a search term.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PleaseEnterSearchTermMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when a game fails to launch, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorLaunchingGameMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an informational message prompting the user to select a game to launch.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SelectAGameToLaunchMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming a file was added to favorites.
    /// </summary>
    /// <param name="fileNameWithoutExtension">The file name without extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileAddedToFavoritesMessageBoxAsync(string fileNameWithoutExtension);

    /// <summary>
    ///     Displays a success message confirming a file was removed from favorites.
    /// </summary>
    /// <param name="fileNameWithoutExtension">The file name without extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileRemovedFromFavoritesMessageBoxAsync(string fileNameWithoutExtension);

    /// <summary>
    ///     Displays an error when a specific game could not be launched, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotLaunchThisGameMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays a warning that a protocol handler for the specified protocol is not registered.
    /// </summary>
    /// <param name="protocol">The protocol name that is not registered.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProtocolHandlerNotRegisteredMessageBoxAsync(string protocol);

    /// <summary>
    ///     Displays a warning that the emulator executable path is not configured for the system.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmulatorPathNotConfiguredMessageBoxAsync();

    /// <summary>
    ///     Displays an error stating that the configured emulator executable was not found on disk.
    /// </summary>
    /// <param name="configuredPath">The emulator path as configured in the system settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmulatorExecutableNotFoundMessageBoxAsync(string configuredPath);

    /// <summary>
    ///     Displays an error stating that the file is not a valid executable for the current OS platform.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidExecutableFileMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when calculating global statistics fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorCalculatingStatsMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving a statistics report fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedSaveReportMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming a statistics report was saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReportSavedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning that no statistics are available to save.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoStatsToSaveMessageBoxAsync();

    /// <summary>
    ///     Displays an error when launching a tool fails, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorLaunchingToolMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when the selected tool executable is not found, with an option to reinstall.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SelectedToolNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays a generic error message indicating an error was reported to the developer.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorMessageBoxAsync();

    /// <summary>
    ///     Displays a warning that no favorite games were found for the selected system.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoFavoriteFoundMessageBoxAsync();

    /// <summary>
    ///     Displays a warning that the application is in a restricted folder and needs to be moved to a writable location.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MoveToWritableFolderMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the system configuration could not be loaded.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidSystemConfigMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when loading the game file list fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorMethodLoadGameFilesAsyncMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the donation link fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorOpeningDonationLinkMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when toggling gamepad support fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ToggleGamepadFailureMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that the tool launch was canceled by the user.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ToolLaunchWasCanceledByUserMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when changing the view mode fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorChangingViewModeMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when a navigation button encounters an error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NavigationButtonErrorMessageBoxAsync();

    /// <summary>
    ///     Displays a warning prompting the user to select a system before searching.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SelectSystemBeforeSearchMessageBoxAsync();

    /// <summary>
    ///     Displays a warning prompting the user to enter a search query.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnterSearchQueryMessageBoxAsync();

    /// <summary>
    ///     Displays an error when loading helpuser.xml fails, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorWhileLoadingHelpUserXmlMessageBoxAsync();

    /// <summary>
    ///     Displays an error when no valid systems are found in helpuser.xml, with an option to reinstall.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoSystemInHelpUserXmlMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt asking whether to reinstall the application after helpuser.xml fails to load.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> CouldNotLoadHelpUserXmlMessageBoxAsync();

    /// <summary>
    ///     Displays an error when helpuser.xml is corrupted, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToLoadHelpUserXmlMessageBoxAsync();

    /// <summary>
    ///     Displays an error when helpuser.xml is missing, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileHelpUserXmlIsMissingMessageBoxAsync();

    /// <summary>
    ///     Displays an error when loading parameters.md fails, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorWhileLoadingParametersMdMessageBoxAsync();

    /// <summary>
    ///     Displays an error when no valid systems are found in parameters.md, with an option to reinstall.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoSystemInParametersMdMessageBoxAsync();

    /// <summary>
    ///     Displays an error when parameters.md fails to load, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToLoadParametersMdMessageBoxAsync();

    /// <summary>
    ///     Displays an error when parameters.md is missing, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileParametersMdIsMissingMessageBoxAsync();

    /// <summary>
    ///     Displays an error when parameters.md is empty, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileParametersMdIsEmptyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the image viewer fails to load an image.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ImageViewerErrorMessageBoxAsync();

    /// <summary>
    ///     Displays an error when mame.dat is corrupted, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReinstallSimpleLauncherFileCorruptedMessageBoxAsync();

    /// <summary>
    ///     Displays an error when mame.dat is missing, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReinstallSimpleLauncherFileMissingMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when checking for application updates fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorCheckingForUpdatesMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when loading ROM history fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorLoadingRomHistoryMessageBoxAsync();

    /// <summary>
    ///     Displays an error when no history.dat or history.xml file is found, with an option to reinstall.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoHistoryXmlOrDatFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the browser fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorOpeningBrowserMessageBoxAsync();

    /// <summary>
    ///     Displays an error when system.xml is corrupted, with an option to open the error log, then shuts down.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemXmlIsCorruptedMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when a game could not be launched, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WouldYouLikeToOpenTheLogMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when the file system.xml is badly corrupted, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileSystemXmlIsCorruptedMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error during update installation, with an option to open the GitHub releases page.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InstallUpdateManuallyMessageBoxAsync();

    /// <summary>
    ///     Displays an error when the updater fails to launch, with an option to open the GitHub releases page.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdaterLaunchFailedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when appsettings.json is missing, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RequiredFileMissingMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message prompting the user to enter support request details.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnterSupportRequestMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message prompting the user to enter a name.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnterNameMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message prompting the user to enter an email address.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnterEmailMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when an API key error occurs in the support form.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApiKeyErrorMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming the support request was sent.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SupportRequestSuccessMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when sending the support request fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SupportRequestSendErrorMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when file extraction fails, with troubleshooting suggestions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExtractionFailedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning that the selected file must be a compressed archive (7z, zip, or rar) for extraction.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileNeedToBeCompressedMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when a downloaded file is missing, with a OneDrive sync suggestion.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DownloadedFileIsMissingMessageBoxAsync();

    /// <summary>
    ///     Displays an error when a downloaded file is locked, with an option to open the temp folder.
    /// </summary>
    /// <param name="tempFolderPath">The path of the temporary folder involved in the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileIsLockedMessageBoxAsync(string? tempFolderPath);

    /// <summary>
    ///     Displays a success message confirming that links were saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LinksSavedMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming that dead zone values were saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeadZonesSavedMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming that dead zone values were reverted to defaults.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeadZonesRevertedMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message confirming that links were reverted to default values.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LinksRevertedMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the main window search engine encounters an error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MainWindowSearchEngineErrorMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when download or extraction of a file fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DownloadExtractionFailedMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming that download and extraction completed.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DownloadAndExtractionWereSuccessfulMessageBoxAsync();

    /// <summary>
    ///     Displays an emulator download error with an option to open the emulator download page.
    /// </summary>
    /// <param name="selectedSystem">The system configuration for which the download failed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowEmulatorDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem);

    /// <summary>
    ///     Displays a core download error with an option to open the core download page.
    /// </summary>
    /// <param name="selectedSystem">The system configuration for which the download failed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowCoreDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem);

    /// <summary>
    ///     Displays an image pack download error with an option to open the image pack download page.
    /// </summary>
    /// <param name="selectedSystem">The system configuration for which the download failed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowImagePackDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem);

    /// <summary>
    ///     Displays a prompt to select a history item before attempting removal.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SelectAHistoryItemToRemoveMessageBoxAsync();

    /// <summary>
    ///     Displays a confirmation prompt asking whether to remove all play history.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> ReallyWantToRemoveAllPlayHistoryMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming a system was added, with folder paths for ROMs and cover images.
    /// </summary>
    /// <param name="systemName">The name of the system that was added.</param>
    /// <param name="resolvedSystemFolder">The resolved path of the system folder.</param>
    /// <param name="resolvedSystemImageFolder">The resolved path of the system image folder.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemAddedMessageBoxAsync(string systemName, string resolvedSystemFolder, string resolvedSystemImageFolder);

    /// <summary>
    ///     Displays an error message when adding a system fails, with optional error details.
    /// </summary>
    /// <param name="details">Optional additional details about the failure.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddSystemFailedMessageBoxAsync(string? details = null);

    /// <summary>
    ///     Displays an error message when the right-click context menu encounters an error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RightClickContextMenuErrorMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that a game file no longer exists and has been removed from the list.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GameFileDoesNotExistMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt asking whether to delete a play history entry when the game file no longer exists.
    /// </summary>
    /// <param name="filePath">The full path to the game file.</param>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> GameFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath);

    /// <summary>
    ///     Displays a prompt asking whether to delete a favorite entry when the game file no longer exists.
    /// </summary>
    /// <param name="filePath">The full path to the game file.</param>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> FavoriteFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath);

    /// <summary>
    ///     Displays an error message when the History window fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotOpenHistoryWindowMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the walkthrough file fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotOpenWalkthroughMessageBoxAsync();

    /// <summary>
    ///     Displays a warning prompting the user to select a favorite before attempting removal.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SelectAFavoriteToRemoveMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when system.xml is not found in the application folder.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemXmlNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that a new system can now be added.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task YouCanAddANewSystemMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that a specific emulator name is required because related data was provided.
    /// </summary>
    /// <param name="i">The 1-based index of the emulator.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmulatorNameRequiredMessageBoxAsync(int i);

    /// <summary>
    ///     Displays an informational message that an emulator name is required.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmulatorNameIsRequiredMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that the emulator name must be unique.
    /// </summary>
    /// <param name="emulatorName">The name of the emulator.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmulatorNameMustBeUniqueMessageBoxAsync(string emulatorName);

    /// <summary>
    ///     Displays a success message confirming the system configuration was saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when one or more paths or parameters are invalid.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PathOrParameterInvalidMessageBoxAsync();

    /// <summary>
    ///     Displays an error message that Emulator 1 name is required.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Emulator1RequiredMessageBoxAsync();

    /// <summary>
    ///     Displays an error message that the extension to launch after extraction is required.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExtensionToLaunchIsRequiredMessageBoxAsync();

    /// <summary>
    ///     Displays an error message that the extension to search in the system folder is required.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExtensionToSearchIsRequiredMessageBoxAsync();

    /// <summary>
    ///     Displays an error message that the search extension must include zip, 7z, or rar when extraction is enabled.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileMustBeCompressedMessageBoxAsync();

    /// <summary>
    ///     Displays an error message that the system image folder field cannot be empty.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemImageFolderCanNotBeEmptyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message that the system folder field cannot be empty.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemFolderCanNotBeEmptyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message that the system name field cannot be empty.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemNameCanNotBeEmptyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message listing invalid characters found in the system name.
    /// </summary>
    /// <param name="invalidChars">The characters that are not allowed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidSystemNameCharactersMessageBoxAsync(string invalidChars);

    /// <summary>
    ///     Displays an error message listing invalid characters found in the system folder name.
    /// </summary>
    /// <param name="invalidChars">The characters that are not allowed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidFolderCharactersMessageBoxAsync(string invalidChars);

    /// <summary>
    ///     Displays an error message when creating system folders fails, with troubleshooting suggestions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FolderCreationFailedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning prompting the user to select a system before deleting.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SelectASystemToDeleteMessageBoxAsync();

    /// <summary>
    ///     Displays a warning that the selected system was not found in the XML document.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemNotFoundInTheXmlMessageBoxAsync();

    /// <summary>
    ///     Displays an error when finding game files fails, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorFindingGameFilesMessageBoxAsync(string logPath);

    /// <summary>
    ///     Displays an error when the gamepad controller encounters an error, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GamePadErrorMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when a game could not be launched, with troubleshooting tips and an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotLaunchGameMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when an invalid operation occurs during game launch, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidOperationExceptionMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when a game fails to launch, with an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereWasAnErrorLaunchingThisGameMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when a batch file fails to execute, with error details and an option to open the error log.
    /// </summary>
    /// <param name="batchFilePath">The full path to the batch file that failed to execute.</param>
    /// <param name="errorDetail">The error details or message produced by the failing batch file.</param>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <param name="exitCode">The exit code returned by the process, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BatchFileFailedMessageBoxAsync(string batchFilePath, string errorDetail, string? logPath,
        int? exitCode = null);

    /// <summary>
    ///     Displays a warning listing missing paths referenced by a batch file, with an option to continue.
    /// </summary>
    /// <param name="missingPaths">The list of file paths that are missing.</param>
    /// <returns>A task representing the asynchronous operation, resulting in a value indicating the user's response.</returns>
    Task<bool> BatchFilePathsMissingMessageBoxAsync(IList<string> missingPaths);

    /// <summary>
    ///     Displays an error message when administrator privileges are required to launch a game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ElevationRequiredMessageBoxAsync();

    /// <summary>
    ///     Displays an error when the file extension to launch after extraction is not configured.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NullFileExtensionMessageBoxAsync();

    /// <summary>
    ///     Displays an error when no file matching the configured extension is found in the extracted folder.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotFindAFileMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt asking whether to search online for ROM history when none is found locally.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> SearchOnlineForRomHistoryMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming a system was deleted.
    /// </summary>
    /// <param name="selectedSystemName">The name of the system that was deleted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemHasBeenDeletedMessageBoxAsync(string selectedSystemName);

    /// <summary>
    ///     Displays a confirmation prompt asking whether to delete the selected system.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when deleting a game file fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereWasAnErrorDeletingTheGameMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when deleting a cover image fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereWasAnErrorDeletingTheCoverImageMessageBoxAsync();

    /// <summary>
    ///     Displays a confirmation prompt asking whether to permanently delete a game file.
    /// </summary>
    /// <param name="fileNameWithExtension">The file name with extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> AreYouSureYouWantToDeleteTheGameMessageBoxAsync(string fileNameWithExtension);

    /// <summary>
    ///     Displays a confirmation prompt asking whether to permanently delete a game's cover image.
    /// </summary>
    /// <param name="fileNameWithoutExtension">The file name without extension of the game file.</param>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> AreYouSureYouWantToDeleteTheCoverImageMessageBoxAsync(string fileNameWithoutExtension);

    /// <summary>
    ///     Displays a prompt asking whether to save a report with the current results.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> WouldYouLikeToSaveAReportMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the application is unable to restore the last backup.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SimpleLauncherWasUnableToRestoreBackupMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt asking whether to restore a backup when system.xml is missing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> WouldYouLikeToRestoreTheLastBackupMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when loading language resources fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToLoadLanguageResourceMessageBoxAsync();

    /// <summary>
    ///     Displays a warning message about an invalid system configuration.
    /// </summary>
    /// <param name="errorMessage">The error message to display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidSystemConfigurationMessageBoxAsync(string errorMessage);

    /// <summary>
    ///     Displays an error message when a link fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnableToOpenLinkMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that no games were found for the random selection feature.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoGameFoundInTheRandomSelectionMessageBoxAsync();

    /// <summary>
    ///     Displays a warning prompting the user to select a system before using the Feeling Lucky feature.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PleaseSelectASystemBeforeMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when toggling fuzzy matching logic fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ToggleFuzzyMatchingFailureMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when setting the fuzzy matching threshold fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FuzzyMatchingErrorFailToSetThresholdMessageBoxAsync();

    /// <summary>
    ///     Displays a list of validation errors for a system configuration.
    /// </summary>
    /// <param name="errorMessages">The accumulated error messages to display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ListOfErrorsMessageBoxAsync(StringBuilder errorMessages);

    /// <summary>
    ///     Displays an informational message that no update is available, showing the current version.
    /// </summary>
    /// <param name="currentVersion">The currently installed version of the application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereIsNoUpdateAvailableMessageBoxAsync(string currentVersion);

    /// <summary>
    ///     Displays an informational message that another instance of Simple Launcher is already running.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AnotherInstanceIsRunningMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when Simple Launcher fails to start due to an instance check error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToStartSimpleLauncherMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the application fails to restart.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToRestartMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt asking whether to download and install an available update.
    /// </summary>
    /// <param name="currentVersion">The currently installed version of the application.</param>
    /// <param name="latestVersion">The latest available version of the application.</param>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> DoYouWantToUpdateMessageBoxAsync(string currentVersion, string latestVersion);

    /// <summary>
    ///     Displays an error when required files are missing, with an option to reinstall the application.
    /// </summary>
    /// <param name="fileList">The list of missing required files.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleMissingRequiredFilesMessageBoxAsync(string fileList);

    /// <summary>
    ///     Displays an error when the API configuration fails to load, with an option to reinstall.
    /// </summary>
    /// <param name="reason">The reason for the configuration error.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleApiConfigErrorMessageBoxAsync(string reason);

    /// <summary>
    ///     Displays an error message when there is not enough disk space for extraction.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DiskSpaceErrorMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when disk space cannot be checked for the specified path.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotCheckForDiskSpaceMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving the system configuration fails.
    /// </summary>
    /// <param name="details">Optional additional details about the failure.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveSystemFailedMessageBoxAsync(string? details = null);

    /// <summary>
    ///     Displays an error message when the download link could not be opened.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotOpenTheDownloadLinkMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when loading appsettings.json fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorLoadingAppSettingsMessageBoxAsync();

    /// <summary>
    ///     Displays a security warning when a potential path manipulation (Zip Slip) is detected in an archive.
    /// </summary>
    /// <param name="archivePath">The path of the archive file involved in the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PotentialPathManipulationDetectedMessageBoxAsync(string archivePath);

    /// <summary>
    ///     Displays a warning when the sound configuration window fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotOpenSoundConfigurationWindowMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when choosing or copying a sound file fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ErrorSettingSoundFileMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message that notification sounds are disabled.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotificationSoundIsDisableMessageBoxAsync();

    /// <summary>
    ///     Displays a warning that no sound file is currently selected.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoSoundFileIsSelectedMessageBoxAsync();

    /// <summary>
    ///     Displays a success message confirming settings were saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SettingsSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving settings fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveSettingsMessageBoxAsync();

    /// <summary>
    ///     Displays an error when a game file path is invalid, with troubleshooting suggestions and an option to open the
    ///     error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FilePathIsInvalidMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when mounting a file fails, with an option to download Dokan if needed.
    /// </summary>
    /// <param name="exitCode">The exit code returned by the process, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ThereWasAnErrorMountingTheFileMessageBoxAsync(int? exitCode = null);

    /// <summary>
    ///     Displays an error when the Dokan driver is not installed, with an option to open the Dokan download page.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DokanDriverNotInstalledMessageBoxAsync();

    /// <summary>
    ///     Displays informational text about launching a tool.
    /// </summary>
    /// <param name="info">The informational text to display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchToolInformationMessageBoxAsync(string info);

    /// <summary>
    ///     Displays an error message that a screenshot cannot be taken of a minimized window.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CannotScreenshotMinimizedWindowMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when copying log content to the clipboard fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToCopyLogContentMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the updater application cannot be found on GitHub.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotFindUpdaterOnGitHubMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the achievements window fails to open.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotOpenAchievementsWindowMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt when a game is not supported by RetroAchievements, with an option to open the global window.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> GameNotSupportedByRetroAchievementsMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the game launch process times out.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GameLaunchTimeoutMessageBoxAsync();

    /// <summary>
    ///     Displays an informational message prompting the user to add RetroAchievements login credentials.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddRaLoginMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when no default web browser is configured in the operating system.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NoDefaultBrowserConfiguredMessageBoxAsync();

    /// <summary>
    ///     Displays a warning about high memory usage when setting a very high number of games per page in Grid mode.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> WarnUserAboutMemoryConsumptionMessageBoxAsync();

    /// <summary>
    ///     Displays a compatibility warning that the Group Files by Folder option only works with MAME or DOSBox emulators.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GroupByFolderOnlyForMameAndDosBoxMessageBoxAsync();

    /// <summary>
    ///     Displays a configuration warning when Group Files by Folder is enabled without a compatible emulator.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> GroupByFolderWarningMessageBoxAsync();

    /// <summary>
    ///     Displays a warning that the specified emulator's path is required.
    /// </summary>
    /// <param name="emulatorNumber">The 1-based emulator number whose path is required.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmulatorLocationRequiredMessageBoxAsync(int emulatorNumber);

    /// <summary>
    ///     Displays an error when the image pack downloader Web API is unavailable.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ImagePackDownloaderUnavailableMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when Easy Mode is unavailable due to Web API access issues, with troubleshooting suggestions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EasyModeUnavailableMessageBoxAsync();

    /// <summary>
    ///     Displays an error that RetroAchievements hash is not supported for systems grouped by folder.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SimpleLauncherDoesNotSupportRaHashOfSystemGroupedByFolderMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt asking whether to scan the game path for RetroAchievements
    ///     compatibility when no hash scan result exists for the selected system.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> ScanGamePathForRetroAchievementsMessageBoxAsync();

    /// <summary>
    ///     Displays an error that the current processor architecture is unsupported.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnsupportedArchitectureMessageBoxAsync();

    /// <summary>
    ///     Displays an error when the 7z DLL is missing, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SevenZipDllNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error when the 7-Zip library fails to initialize, with an option to reinstall the application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInitializeSevenZipMessageBoxAsync();

    /// <summary>
    ///     Displays an error when file extraction fails after download, with an option to open the temp folder.
    /// </summary>
    /// <param name="tempFolderPath">The path of the temporary folder involved in the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowExtractionFailedMessageBoxAsync(string tempFolderPath);

    /// <summary>
    ///     Displays a download failure message when the temporary file is locked, with an option to open the temp folder.
    /// </summary>
    /// <param name="tempFolderPath">The path of the temporary folder involved in the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowDownloadFileLockedMessageBoxAsync(string tempFolderPath);

    /// <summary>
    ///     Displays a custom game launch error message with an option to open the error log.
    /// </summary>
    /// <param name="message">The main message text to display.</param>
    /// <param name="launchError">Additional launch error details to display.</param>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowCustomMessageBoxAsync(string message, string launchError, string? logPath);

    /// <summary>
    ///     Displays a warning prompting the user to enter valid search terms.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnterValidSearchTermsMessageBoxAsync();

    /// <summary>
    ///     Displays a notification that the operation was cancelled.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OperationCancelledMessageBoxAsync();

    /// <summary>
    ///     Displays a confirmation dialog asking whether to cancel processing and close.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in the user's selection.</returns>
    Task<MessageBoxResult> DoYouWantToCancelAndCloseMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the browser cannot be opened for AI support.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotOpenBrowserForAiSupportMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when PowerShell execution policy restrictions prevent scanning Microsoft Store games.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PowerShellExecutionPolicyRestrictionsMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when an ISO file cannot be mounted due to PowerShell execution policy restrictions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnabletomountIsOfileMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when an ISO file cannot be dismounted due to PowerShell execution policy restrictions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnabletoDismountIsOfileMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when an application control policy blocks a file or link.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplicationControlPolicyBlockedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when an application control policy blocks a link, and copies the URL to the clipboard.
    /// </summary>
    /// <param name="url">The URL that was blocked.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplicationControlPolicyBlockedManualLinkMessageBoxAsync(string url);

    /// <summary>
    ///     Displays a warning prompting the user to enter RetroAchievements credentials before configuring an emulator.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnterYourRetroAchievementsUsernameMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after an emulator has been configured for RetroAchievements.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EmulatorConfiguredSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when emulator configuration fails due to a missing or read-only config file.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToConfigureTheEmulatorMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when an exception occurs while configuring an emulator.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AnErrorOccurredWhileConfiguringTheEmulatorMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when logging in to RetroAchievements fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToLoginToRetroAchievementsMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when system.xml is locked or inaccessible by another process.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FileSystemXmlIsLockedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when injecting MAME configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectMameConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after MAME configuration has been injected.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MameConfigurationInjectedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when injecting MAME configuration fails (alternate message).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedtoinjectMamEconfiguration2MessageBoxAsync();

    /// <summary>
    ///     Displays a prompt to locate the MAME emulator executable.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MameEmulatorPathNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt to locate the RetroArch emulator executable.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RetroArchemulatorpathnotfoundMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when injecting RetroArch configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedtoinjectRetroArchconfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after RetroArch configuration has been injected.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RetroArchConfigurationInjectedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when injecting RetroArch configuration fails (alternate message).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedtoinjectRetroArchconfiguration2MessageBoxAsync();

    /// <summary>
    ///     Displays a prompt to locate the Xenia emulator executable.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task XeniaemulatorpathnotfoundMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when injecting Xenia configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedtoinjectXeniaconfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Xenia configuration has been injected.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task XeniaconfigurationinjectedsuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when injecting Xenia configuration fails (alternate message).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedtoinjectXeniaconfiguration2MessageBoxAsync();

    /// <summary>
    ///     Displays a warning prompting the user to enter RetroAchievements username and password first.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnterUsernamePasswordMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Ares emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AresemulatornotfoundMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Daphne settings have been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DaphnesettingssavedsuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after PCSX2 settings have been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Pcsx2SettingssavedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when PCSX2 configuration injection fails due to permission issues.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Pcsx2ConfigurationInjectionPermissionErrorMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after emulator settings have been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SettingsSavedMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Cemu emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CemuEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Ares configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedtoinjectAresconfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Cemu configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CemuConfigurationSavedMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Flycast emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FlycastEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Ares configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AresConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Ares configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveAresConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Flycast configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectFlycastConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Flycast configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FlycastConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Dolphin emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DolphinEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Flycast configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveFlycastConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Dolphin configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectDolphinConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Dolphin configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DolphinConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Dolphin configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveDolphinConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the SEGA Model 2 emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SegaModel2EmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting SEGA Model 2 configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectSegaModel2ConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after SEGA Model 2 configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SegaModel2ConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Blastem emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BlastemEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Blastem configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectBlastemConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Blastem configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BlastemConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving SEGA Model 2 configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveSegaModel2ConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Blastem configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveBlastemConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the RPCS3 emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Rpcs3EmulatorNotFoundPleaseLocateMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting RPCS3 configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectRpcs3ConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after RPCS3 configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Rpcs3ConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving RPCS3 configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveRpcs3ConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Stella emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StellaEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Stella configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectStellaConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Supermodel emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SupermodelEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Stella configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StellaConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Supermodel configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectSupermodelConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Stella configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveStellaConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Supermodel configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SupermodelConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Supermodel configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveSupermodelConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Mednafen emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MednafenEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Mesen emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MesenEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Mednafen configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectMednafenConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Mesen configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectMesenConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the DuckStation emulator cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DuckStationEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Mednafen configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MednafenConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Mednafen configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveMednafenConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting DuckStation configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectDuckStationConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after DuckStation configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DuckStationConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Mesen configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveMesenConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving DuckStation configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveDuckStationConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Mesen configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MesenConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting Yumir configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectYumirConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Yumir configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task YumirConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Raine configuration has been injected.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RaineSettingsSavedAndInjectedMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Raine executable cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RaineExecutableNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the Yumir executable cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task YumirEmulatorNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when the ReDream executable cannot be found.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReDreamEmulatorPathNotFoundMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when injecting ReDream configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToInjectReDreamConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after ReDream configuration has been injected.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReDreamConfigurationInjectedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when a game fails to launch due to a DEP violation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CouldNotLaunchGameDueToDepViolationMessageBoxAsync();

    /// <summary>
    ///     Displays a MAME ROM set error with an option to visit the PleasureDome website.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MameRomSetErrorMessageBoxAsync();

    /// <summary>
    ///     Displays a MAME unknown system error with an option to visit the PleasureDome website.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MameUnknownSystemErrorMessageBoxAsync();

    /// <summary>
    ///     Displays an error when MAME cannot load an image file, with an option to visit the PleasureDome website.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MameUnableToLoadImageMessageBoxAsync();

    /// <summary>
    ///     Displays an error that the Ootake emulator does not support CHD, ISO, or CUE/BIN files.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OotakeDoesNotSupportImageFilesMessageBoxAsync();

    /// <summary>
    ///     Displays an error that the Geolith libretro DLL does not support compressed files, with an option to visit a wiki
    ///     page.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GeolithDoesNotSupportCompressedFilesMessageBoxAsync();

    /// <summary>
    ///     Displays an error that the RetroArch parameter should contain -L to point to the desired core.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RetroArchParameterShouldContainLMessageBoxAsync();

    /// <summary>
    ///     Displays a RetroArch parameter issue with troubleshooting tips and an option to open the error log.
    /// </summary>
    /// <param name="logPath">The full path to the log file, if available.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RetroArchParameterIssueMessageBoxAsync(string? logPath);

    /// <summary>
    ///     Displays an error when special characters in the file path prevent RetroArch from launching.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RetroArchSpecialCharactersInPathMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when Azahar configuration injection fails due to permission issues.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AzaharConfigurationInjectionPermissionErrorMessageBoxAsync();

    /// <summary>
    ///     Displays a success message after Azahar configuration has been saved.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AzaharConfigurationSavedSuccessfullyMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Azahar configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToSaveAzaharConfigurationMessageBoxAsync();

    /// <summary>
    ///     Displays an error that the Xemu parameter should contain '-dvd_path'.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task XemuParameterShouldContainDvdPathMessageBoxAsync();

    /// <summary>
    ///     Displays an error that the application cannot run from a temporary folder.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PleaseExtractApplicationFirstMessageBoxAsync();

    /// <summary>
    ///     Displays a generic error message when configuration injection fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InjectionFailedGenericMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when saving Daphne configuration fails.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DaphneConfigurationSaveFailedMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when image download times out due to Cloudflare access issues.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowImageDownloadTimeoutMessageBoxAsync();

    /// <summary>
    ///     Displays a prompt asking the user to enter a system name before choosing an image.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SystemNameRequiredBeforeChoosingImageMessageBoxAsync();

    /// <summary>
    ///     Displays a warning when the selected image format is not supported.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidImageFormatMessageBoxAsync();

    /// <summary>
    ///     Displays an error message when copying the system image fails.
    /// </summary>
    /// <param name="errorMessage">The error message to display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FailedToCopySystemImageMessageBoxAsync(string errorMessage);

    /// <summary>
    ///     Displays a generic warning message with the specified message text.
    /// </summary>
    /// <param name="message">The message text to display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WarningMessageBoxAsync(string message);

    /// <summary>
    ///     Displays a custom error message with the specified message and title.
    /// </summary>
    /// <param name="message">The message text to display.</param>
    /// <param name="title">The title of the message box.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CustomErrorMessageBoxAsync(string message, string title);

    /// <summary>
    ///     Displays a custom yes/no question dialog and returns the user's response.
    /// </summary>
    /// <param name="title">The title of the message box.</param>
    /// <param name="message">The message text to display.</param>
    /// <returns>A task representing the asynchronous operation, resulting in a value indicating the user's response.</returns>
    Task<bool> CustomQuestionMessageBoxAsync(string title, string message);

    /// <summary>
    ///     Displays a custom informational message box with the specified message and title.
    /// </summary>
    /// <param name="title">The title of the message box.</param>
    /// <param name="message">The message text to display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CustomInfoMessageBoxAsync(string title, string message);

    /// <summary>
    ///     Displays a question dialog asking whether 'Simple Launcher AI' should suggest correct parameters for the emulator.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, resulting in a value indicating the user's response.</returns>
    Task<bool> AskAiToFixParametersMessageBoxAsync();
}