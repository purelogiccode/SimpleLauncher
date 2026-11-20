using System.Text;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Tests.TestHelpers;

/// <summary>
///     No-op implementation of <see cref="IMessageBoxLibraryService" /> for unit tests.
///     All methods return immediately without displaying any message boxes.
/// </summary>
public class NoOpMessageBoxLibraryService : IMessageBoxLibraryService
{
    /// <summary>
    ///     Does nothing. Does not display the take-screenshot message box.
    /// </summary>
    public Task TakeScreenShotMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-save-screenshot message box.
    /// </summary>
    public Task CouldNotSaveScreenshotMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the game-already-in-favorites message box.
    /// </summary>
    /// <param name="fileNameWithExtension">The game file name.</param>
    public Task GameIsAlreadyInFavoritesMessageBoxAsync(string fileNameWithExtension)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-adding-favorites message box.
    /// </summary>
    public Task ErrorWhileAddingFavoritesMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-removing-favorite message box.
    /// </summary>
    public Task ErrorWhileRemovingGameFromFavoriteMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-opening-update-history message box.
    /// </summary>
    public Task ErrorOpeningTheUpdateHistoryWindowMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-opening-video-link message box.
    /// </summary>
    public Task ErrorOpeningVideoLinkMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the problem-opening-info-link message box.
    /// </summary>
    public Task ProblemOpeningInfoLinkMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-opening-URL message box.
    /// </summary>
    public Task ErrorOpeningUrlMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-cover message box.
    /// </summary>
    public Task ThereIsNoCoverMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-title-snapshot message box.
    /// </summary>
    public Task ThereIsNoTitleSnapshotMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-gameplay-snapshot message box.
    /// </summary>
    public Task ThereIsNoGameplaySnapshotMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-cart message box.
    /// </summary>
    public Task ThereIsNoCartMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-video-file message box.
    /// </summary>
    public Task ThereIsNoVideoFileMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-open-manual message box.
    /// </summary>
    public Task CouldNotOpenManualMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-PDF-viewer-installed message box.
    /// </summary>
    public Task NoPdfViewerInstalledMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-manual message box.
    /// </summary>
    public Task ThereIsNoManualMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-walkthrough message box.
    /// </summary>
    public Task ThereIsNoWalkthroughMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-cabinet message box.
    /// </summary>
    public Task ThereIsNoCabinetMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-flyer message box.
    /// </summary>
    public Task ThereIsNoFlyerMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-PCB message box.
    /// </summary>
    public Task ThereIsNoPcbMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-successfully-deleted message box.
    /// </summary>
    /// <param name="fileNameWithExtension">The deleted file name.</param>
    public Task FileSuccessfullyDeletedMessageBoxAsync(string fileNameWithExtension)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-could-not-be-deleted message box.
    /// </summary>
    /// <param name="fileNameWithExtension">The file name that could not be deleted.</param>
    public Task FileCouldNotBeDeletedMessageBoxAsync(string fileNameWithExtension)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-no-longer-exists message box.
    /// </summary>
    /// <param name="fileNameWithExtension">The file name that no longer exists.</param>
    public Task FileNoLongerExistsMessageBoxAsync(string fileNameWithExtension)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the default-image-not-found message box.
    /// </summary>
    public Task DefaultImageNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the global-search-error message box.
    /// </summary>
    public Task GlobalSearchErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the please-enter-search-term message box.
    /// </summary>
    public Task PleaseEnterSearchTermMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-launching-game message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task ErrorLaunchingGameMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the select-a-game-to-launch message box.
    /// </summary>
    public Task SelectAGameToLaunchMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-added-to-favorites message box.
    /// </summary>
    /// <param name="fileNameWithoutExtension">The file name without extension.</param>
    public Task FileAddedToFavoritesMessageBoxAsync(string fileNameWithoutExtension)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-removed-from-favorites message box.
    /// </summary>
    /// <param name="fileNameWithoutExtension">The file name without extension.</param>
    public Task FileRemovedFromFavoritesMessageBoxAsync(string fileNameWithoutExtension)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-launch-game message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task CouldNotLaunchThisGameMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the protocol-handler-not-registered message box.
    /// </summary>
    /// <param name="protocol">The protocol name.</param>
    public Task ProtocolHandlerNotRegisteredMessageBoxAsync(string protocol)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-path-not-configured message box.
    /// </summary>
    public Task EmulatorPathNotConfiguredMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-executable-not-found message box.
    /// </summary>
    /// <param name="configuredPath">The emulator path as configured in the system settings.</param>
    public Task EmulatorExecutableNotFoundMessageBoxAsync(string configuredPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the invalid-executable-file message box.
    /// </summary>
    public Task InvalidExecutableFileMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-calculating-stats message box.
    /// </summary>
    public Task ErrorCalculatingStatsMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-save-report message box.
    /// </summary>
    public Task FailedSaveReportMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the report-saved message box.
    /// </summary>
    public Task ReportSavedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-stats-to-save message box.
    /// </summary>
    public Task NoStatsToSaveMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-launching-tool message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task ErrorLaunchingToolMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the selected-tool-not-found message box.
    /// </summary>
    public Task SelectedToolNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the generic error message box.
    /// </summary>
    public Task ErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-favorite-found message box.
    /// </summary>
    public Task NoFavoriteFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the move-to-writable-folder message box.
    /// </summary>
    public Task MoveToWritableFolderMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the invalid-system-config message box.
    /// </summary>
    public Task InvalidSystemConfigMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-loading-game-files message box.
    /// </summary>
    public Task ErrorMethodLoadGameFilesAsyncMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-opening-donation-link message box.
    /// </summary>
    public Task ErrorOpeningDonationLinkMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the toggle-gamepad-failure message box.
    /// </summary>
    public Task ToggleGamepadFailureMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the tool-launch-canceled-by-user message box.
    /// </summary>
    public Task ToolLaunchWasCanceledByUserMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-changing-view-mode message box.
    /// </summary>
    public Task ErrorChangingViewModeMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the navigation-button-error message box.
    /// </summary>
    public Task NavigationButtonErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the select-system-before-search message box.
    /// </summary>
    public Task SelectSystemBeforeSearchMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the enter-search-query message box.
    /// </summary>
    public Task EnterSearchQueryMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-loading-help-user-xml message box.
    /// </summary>
    public Task ErrorWhileLoadingHelpUserXmlMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-system-in-help-user-xml message box.
    /// </summary>
    public Task NoSystemInHelpUserXmlMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> CouldNotLoadHelpUserXmlMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-load-help-user-xml message box.
    /// </summary>
    public Task FailedToLoadHelpUserXmlMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the help-user-xml-missing message box.
    /// </summary>
    public Task FileHelpUserXmlIsMissingMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-loading-parameters-md message box.
    /// </summary>
    public Task ErrorWhileLoadingParametersMdMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-system-in-parameters-md message box.
    /// </summary>
    public Task NoSystemInParametersMdMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-load-parameters-md message box.
    /// </summary>
    public Task FailedToLoadParametersMdMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the parameters-md-missing message box.
    /// </summary>
    public Task FileParametersMdIsMissingMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the parameters-md-empty message box.
    /// </summary>
    public Task FileParametersMdIsEmptyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the image-viewer-error message box.
    /// </summary>
    public Task ImageViewerErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the reinstall-file-corrupted message box.
    /// </summary>
    public Task ReinstallSimpleLauncherFileCorruptedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the reinstall-file-missing message box.
    /// </summary>
    public Task ReinstallSimpleLauncherFileMissingMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-checking-for-updates message box.
    /// </summary>
    public Task ErrorCheckingForUpdatesMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-loading-rom-history message box.
    /// </summary>
    public Task ErrorLoadingRomHistoryMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-history-xml-or-dat-found message box.
    /// </summary>
    public Task NoHistoryXmlOrDatFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-opening-browser message box.
    /// </summary>
    public Task ErrorOpeningBrowserMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-xml-corrupted message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task SystemXmlIsCorruptedMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the would-you-like-to-open-log message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task WouldYouLikeToOpenTheLogMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-system-xml-corrupted message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task FileSystemXmlIsCorruptedMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the install-update-manually message box.
    /// </summary>
    public Task InstallUpdateManuallyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the updater-launch-failed message box.
    /// </summary>
    public Task UpdaterLaunchFailedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the required-file-missing message box.
    /// </summary>
    public Task RequiredFileMissingMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the enter-support-request message box.
    /// </summary>
    public Task EnterSupportRequestMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the enter-name message box.
    /// </summary>
    public Task EnterNameMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the enter-email message box.
    /// </summary>
    public Task EnterEmailMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the API-key-error message box.
    /// </summary>
    public Task ApiKeyErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the support-request-success message box.
    /// </summary>
    public Task SupportRequestSuccessMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the support-request-send-error message box.
    /// </summary>
    public Task SupportRequestSendErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the extraction-failed message box.
    /// </summary>
    public Task ExtractionFailedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-need-to-be-compressed message box.
    /// </summary>
    public Task FileNeedToBeCompressedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the downloaded-file-is-missing message box.
    /// </summary>
    public Task DownloadedFileIsMissingMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-is-locked message box.
    /// </summary>
    /// <param name="tempFolderPath">The temporary folder path.</param>
    public Task FileIsLockedMessageBoxAsync(string? tempFolderPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the links-saved message box.
    /// </summary>
    public Task LinksSavedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the dead-zones-saved message box.
    /// </summary>
    public Task DeadZonesSavedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the dead-zones-reverted message box.
    /// </summary>
    public Task DeadZonesRevertedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the links-reverted message box.
    /// </summary>
    public Task LinksRevertedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the main-window-search-engine-error message box.
    /// </summary>
    public Task MainWindowSearchEngineErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the download-extraction-failed message box.
    /// </summary>
    public Task DownloadExtractionFailedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the download-and-extraction-were-successful message box.
    /// </summary>
    public Task DownloadAndExtractionWereSuccessfulMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-download-error message box.
    /// </summary>
    /// <param name="selectedSystem">The selected system configuration.</param>
    public Task ShowEmulatorDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the core-download-error message box.
    /// </summary>
    /// <param name="selectedSystem">The selected system configuration.</param>
    public Task ShowCoreDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the image-pack-download-error message box.
    /// </summary>
    /// <param name="selectedSystem">The selected system configuration.</param>
    public Task ShowImagePackDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the select-a-history-item-to-remove message box.
    /// </summary>
    public Task SelectAHistoryItemToRemoveMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> ReallyWantToRemoveAllPlayHistoryMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the system-added message box.
    /// </summary>
    /// <param name="systemName">The system name.</param>
    /// <param name="resolvedSystemFolder">The resolved system folder path.</param>
    /// <param name="resolvedSystemImageFolder">The resolved system image folder path.</param>
    public Task SystemAddedMessageBoxAsync(string systemName, string resolvedSystemFolder,
        string resolvedSystemImageFolder)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the add-system-failed message box.
    /// </summary>
    /// <param name="details">Optional error details.</param>
    public Task AddSystemFailedMessageBoxAsync(string? details = null)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the right-click-context-menu-error message box.
    /// </summary>
    public Task RightClickContextMenuErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the game-file-does-not-exist message box.
    /// </summary>
    public Task GameFileDoesNotExistMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <param name="filePath">The file path that does not exist.</param>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> GameFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath)
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <param name="filePath">The favorite file path that does not exist.</param>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> FavoriteFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath)
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-open-history-window message box.
    /// </summary>
    public Task CouldNotOpenHistoryWindowMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-open-walkthrough message box.
    /// </summary>
    public Task CouldNotOpenWalkthroughMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the select-a-favorite-to-remove message box.
    /// </summary>
    public Task SelectAFavoriteToRemoveMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-xml-not-found message box.
    /// </summary>
    public Task SystemXmlNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the you-can-add-a-new-system message box.
    /// </summary>
    public Task YouCanAddANewSystemMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-name-required message box.
    /// </summary>
    /// <param name="i">The emulator index.</param>
    public Task EmulatorNameRequiredMessageBoxAsync(int i)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-name-is-required message box.
    /// </summary>
    public Task EmulatorNameIsRequiredMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-name-must-be-unique message box.
    /// </summary>
    /// <param name="emulatorName">The duplicate emulator name.</param>
    public Task EmulatorNameMustBeUniqueMessageBoxAsync(string emulatorName)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-saved-successfully message box.
    /// </summary>
    public Task SystemSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the path-or-parameter-invalid message box.
    /// </summary>
    public Task PathOrParameterInvalidMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-1-required message box.
    /// </summary>
    public Task Emulator1RequiredMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the extension-to-launch-is-required message box.
    /// </summary>
    public Task ExtensionToLaunchIsRequiredMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the extension-to-search-is-required message box.
    /// </summary>
    public Task ExtensionToSearchIsRequiredMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-must-be-compressed message box.
    /// </summary>
    public Task FileMustBeCompressedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-image-folder-can-not-be-empty message box.
    /// </summary>
    public Task SystemImageFolderCanNotBeEmptyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-folder-can-not-be-empty message box.
    /// </summary>
    public Task SystemFolderCanNotBeEmptyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-name-can-not-be-empty message box.
    /// </summary>
    public Task SystemNameCanNotBeEmptyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the invalid-system-name-characters message box.
    /// </summary>
    /// <param name="invalidChars">The invalid characters found.</param>
    public Task InvalidSystemNameCharactersMessageBoxAsync(string invalidChars)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the invalid-folder-characters message box.
    /// </summary>
    /// <param name="invalidChars">The invalid characters found.</param>
    public Task InvalidFolderCharactersMessageBoxAsync(string invalidChars)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the folder-creation-failed message box.
    /// </summary>
    public Task FolderCreationFailedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the select-a-system-to-delete message box.
    /// </summary>
    public Task SelectASystemToDeleteMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-not-found-in-xml message box.
    /// </summary>
    public Task SystemNotFoundInTheXmlMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-finding-game-files message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task ErrorFindingGameFilesMessageBoxAsync(string logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the gamepad-error message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task GamePadErrorMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-launch-game message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task CouldNotLaunchGameMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the invalid-operation message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task InvalidOperationExceptionMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-launching-this-game message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task ThereWasAnErrorLaunchingThisGameMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the batch-file-failed message box.
    /// </summary>
    /// <param name="batchFilePath">The batch file path.</param>
    /// <param name="errorDetail">The error detail.</param>
    /// <param name="logPath">The path to the log file.</param>
    /// <param name="exitCode">The optional exit code.</param>
    public Task BatchFileFailedMessageBoxAsync(string batchFilePath, string errorDetail, string? logPath,
        int? exitCode = null)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see langword="false" /> without displaying a message box.
    /// </summary>
    /// <param name="missingPaths">The list of missing paths.</param>
    /// <returns><see langword="false" />.</returns>
    public Task<bool> BatchFilePathsMissingMessageBoxAsync(IList<string> missingPaths)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    ///     Does nothing. Does not display the elevation-required message box.
    /// </summary>
    public Task ElevationRequiredMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the null-file-extension message box.
    /// </summary>
    public Task NullFileExtensionMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-find-a-file message box.
    /// </summary>
    public Task CouldNotFindAFileMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> SearchOnlineForRomHistoryMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the system-has-been-deleted message box.
    /// </summary>
    /// <param name="selectedSystemName">The deleted system name.</param>
    public Task SystemHasBeenDeletedMessageBoxAsync(string selectedSystemName)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the error-deleting-game message box.
    /// </summary>
    public Task ThereWasAnErrorDeletingTheGameMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-deleting-cover-image message box.
    /// </summary>
    public Task ThereWasAnErrorDeletingTheCoverImageMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <param name="fileNameWithExtension">The game file name.</param>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> AreYouSureYouWantToDeleteTheGameMessageBoxAsync(string fileNameWithExtension)
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <param name="fileNameWithoutExtension">The cover image file name.</param>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> AreYouSureYouWantToDeleteTheCoverImageMessageBoxAsync(string fileNameWithoutExtension)
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> WouldYouLikeToSaveAReportMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the unable-to-restore-backup message box.
    /// </summary>
    public Task SimpleLauncherWasUnableToRestoreBackupMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> WouldYouLikeToRestoreTheLastBackupMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-load-language-resource message box.
    /// </summary>
    public Task FailedToLoadLanguageResourceMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the invalid-system-configuration message box.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public Task InvalidSystemConfigurationMessageBoxAsync(string errorMessage)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the unable-to-open-link message box.
    /// </summary>
    public Task UnableToOpenLinkMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-game-found-in-random-selection message box.
    /// </summary>
    public Task NoGameFoundInTheRandomSelectionMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the please-select-a-system-before message box.
    /// </summary>
    public Task PleaseSelectASystemBeforeMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the toggle-fuzzy-matching-failure message box.
    /// </summary>
    public Task ToggleFuzzyMatchingFailureMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the fuzzy-matching-error-set-threshold message box.
    /// </summary>
    public Task FuzzyMatchingErrorFailToSetThresholdMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the list-of-errors message box.
    /// </summary>
    /// <param name="errorMessages">The error messages.</param>
    public Task ListOfErrorsMessageBoxAsync(StringBuilder errorMessages)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-update-available message box.
    /// </summary>
    /// <param name="currentVersion">The current version string.</param>
    public Task ThereIsNoUpdateAvailableMessageBoxAsync(string currentVersion)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the another-instance-is-running message box.
    /// </summary>
    public Task AnotherInstanceIsRunningMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-start message box.
    /// </summary>
    public Task FailedToStartSimpleLauncherMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-restart message box.
    /// </summary>
    public Task FailedToRestartMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <param name="currentVersion">The current version string.</param>
    /// <param name="latestVersion">The latest version string.</param>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> DoYouWantToUpdateMessageBoxAsync(string currentVersion, string latestVersion)
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the handle-missing-required-files message box.
    /// </summary>
    /// <param name="fileList">The list of missing files.</param>
    public Task HandleMissingRequiredFilesMessageBoxAsync(string fileList)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the API-config-error message box.
    /// </summary>
    /// <param name="reason">The error reason.</param>
    public Task HandleApiConfigErrorMessageBoxAsync(string reason)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the disk-space-error message box.
    /// </summary>
    public Task DiskSpaceErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-check-disk-space message box.
    /// </summary>
    public Task CouldNotCheckForDiskSpaceMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the save-system-failed message box.
    /// </summary>
    /// <param name="details">Optional error details.</param>
    public Task SaveSystemFailedMessageBoxAsync(string? details = null)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-open-download-link message box.
    /// </summary>
    public Task CouldNotOpenTheDownloadLinkMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-loading-app-settings message box.
    /// </summary>
    public Task ErrorLoadingAppSettingsMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the path-manipulation-detected message box.
    /// </summary>
    /// <param name="archivePath">The archive path that triggered the detection.</param>
    public Task PotentialPathManipulationDetectedMessageBoxAsync(string archivePath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-open-sound-configuration-window message box.
    /// </summary>
    public Task CouldNotOpenSoundConfigurationWindowMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-setting-sound-file message box.
    /// </summary>
    public Task ErrorSettingSoundFileMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the notification-sound-disabled message box.
    /// </summary>
    public Task NotificationSoundIsDisableMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-sound-file-selected message box.
    /// </summary>
    public Task NoSoundFileIsSelectedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the settings-saved-successfully message box.
    /// </summary>
    public Task SettingsSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-settings message box.
    /// </summary>
    public Task FailedToSaveSettingsMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-path-is-invalid message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task FilePathIsInvalidMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-mounting-file message box.
    /// </summary>
    /// <param name="exitCode">The optional exit code.</param>
    public Task ThereWasAnErrorMountingTheFileMessageBoxAsync(int? exitCode = null)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Dokan-driver-not-installed message box.
    /// </summary>
    public Task DokanDriverNotInstalledMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the launch-tool-information message box.
    /// </summary>
    /// <param name="info">The information text.</param>
    public Task LaunchToolInformationMessageBoxAsync(string info)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the cannot-screenshot-minimized-window message box.
    /// </summary>
    public Task CannotScreenshotMinimizedWindowMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-copy-log-content message box.
    /// </summary>
    public Task FailedToCopyLogContentMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-find-updater-on-GitHub message box.
    /// </summary>
    public Task CouldNotFindUpdaterOnGitHubMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-open-achievements-window message box.
    /// </summary>
    public Task CouldNotOpenAchievementsWindowMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> GameNotSupportedByRetroAchievementsMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the game-launch-timeout message box.
    /// </summary>
    public Task GameLaunchTimeoutMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the add-RA-login message box.
    /// </summary>
    public Task AddRaLoginMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the no-default-browser-configured message box.
    /// </summary>
    public Task NoDefaultBrowserConfiguredMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> WarnUserAboutMemoryConsumptionMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the group-by-folder-only-for-MAME-and-DOSBox message box.
    /// </summary>
    public Task GroupByFolderOnlyForMameAndDosBoxMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> GroupByFolderWarningMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-location-required message box.
    /// </summary>
    public Task EmulatorLocationRequiredMessageBoxAsync(int emulatorNumber)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the image-pack-downloader-unavailable message box.
    /// </summary>
    public Task ImagePackDownloaderUnavailableMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the easy-mode-unavailable message box.
    /// </summary>
    public Task EasyModeUnavailableMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RA-hash-not-supported-for-grouped-system message box.
    /// </summary>
    public Task SimpleLauncherDoesNotSupportRaHashOfSystemGroupedByFolderMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Returns Yes without displaying the scan-game-path prompt.
    /// </summary>
    public Task<MessageBoxResult> ScanGamePathForRetroAchievementsMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.Yes);
    }

    /// <summary>
    ///     Does nothing. Does not display the unsupported-architecture message box.
    /// </summary>
    public Task UnsupportedArchitectureMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the 7zip-dll-not-found message box.
    /// </summary>
    public Task SevenZipDllNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-initialize-7zip message box.
    /// </summary>
    public Task FailedToInitializeSevenZipMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the extraction-failed message box.
    /// </summary>
    /// <param name="tempFolderPath">The temporary folder path.</param>
    public Task ShowExtractionFailedMessageBoxAsync(string tempFolderPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the download-file-locked message box.
    /// </summary>
    /// <param name="tempFolderPath">The temporary folder path.</param>
    public Task ShowDownloadFileLockedMessageBoxAsync(string tempFolderPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display a custom message box.
    /// </summary>
    /// <param name="message">The message text.</param>
    /// <param name="launchError">The launch error text.</param>
    /// <param name="logPath">The path to the log file.</param>
    public Task ShowCustomMessageBoxAsync(string message, string launchError, string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the enter-valid-search-terms message box.
    /// </summary>
    public Task EnterValidSearchTermsMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the operation-cancelled message box.
    /// </summary>
    public Task OperationCancelledMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see cref="MessageBoxResult.No" /> without displaying a message box.
    /// </summary>
    /// <returns><see cref="MessageBoxResult.No" />.</returns>
    public Task<MessageBoxResult> DoYouWantToCancelAndCloseMessageBoxAsync()
    {
        return Task.FromResult(MessageBoxResult.No);
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-open-browser-for-AI-support message box.
    /// </summary>
    public Task CouldNotOpenBrowserForAiSupportMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the PowerShell-execution-policy-restrictions message box.
    /// </summary>
    public Task PowerShellExecutionPolicyRestrictionsMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the unable-to-mount-ISO message box.
    /// </summary>
    public Task UnabletomountIsOfileMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the unable-to-dismount-ISO message box.
    /// </summary>
    public Task UnabletoDismountIsOfileMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the application-control-policy-blocked message box.
    /// </summary>
    public Task ApplicationControlPolicyBlockedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the application-control-policy-blocked-manual-link message box.
    /// </summary>
    /// <param name="url">The blocked URL.</param>
    public Task ApplicationControlPolicyBlockedManualLinkMessageBoxAsync(string url)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the enter-RA-username message box.
    /// </summary>
    public Task EnterYourRetroAchievementsUsernameMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the emulator-configured-successfully message box.
    /// </summary>
    public Task EmulatorConfiguredSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-configure-emulator message box.
    /// </summary>
    public Task FailedToConfigureTheEmulatorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the error-configuring-emulator message box.
    /// </summary>
    public Task AnErrorOccurredWhileConfiguringTheEmulatorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-login-to-RA message box.
    /// </summary>
    public Task FailedToLoginToRetroAchievementsMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the file-system-xml-is-locked message box.
    /// </summary>
    public Task FileSystemXmlIsLockedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-MAME-configuration message box.
    /// </summary>
    public Task FailedToInjectMameConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the MAME-configuration-injected-successfully message box.
    /// </summary>
    public Task MameConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-MAME-configuration-2 message box.
    /// </summary>
    public Task FailedtoinjectMamEconfiguration2MessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the MAME-emulator-path-not-found message box.
    /// </summary>
    public Task MameEmulatorPathNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RetroArch-emulator-path-not-found message box.
    /// </summary>
    public Task RetroArchemulatorpathnotfoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-RetroArch-configuration message box.
    /// </summary>
    public Task FailedtoinjectRetroArchconfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RetroArch-configuration-injected-successfully message box.
    /// </summary>
    public Task RetroArchConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-RetroArch-configuration-2 message box.
    /// </summary>
    public Task FailedtoinjectRetroArchconfiguration2MessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Xenia-emulator-path-not-found message box.
    /// </summary>
    public Task XeniaemulatorpathnotfoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Xenia-configuration message box.
    /// </summary>
    public Task FailedtoinjectXeniaconfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Xenia-configuration-injected-successfully message box.
    /// </summary>
    public Task XeniaconfigurationinjectedsuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Xenia-configuration-2 message box.
    /// </summary>
    public Task FailedtoinjectXeniaconfiguration2MessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the enter-username-password message box.
    /// </summary>
    public Task EnterUsernamePasswordMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Ares-emulator-not-found message box.
    /// </summary>
    public Task AresemulatornotfoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Daphne-settings-saved-successfully message box.
    /// </summary>
    public Task DaphnesettingssavedsuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the PCSX2-settings-saved message box.
    /// </summary>
    public Task Pcsx2SettingssavedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the PCSX2 configuration injection permission error message box.
    /// </summary>
    public Task Pcsx2ConfigurationInjectionPermissionErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the settings-saved message box.
    /// </summary>
    public Task SettingsSavedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Cemu-emulator-not-found message box.
    /// </summary>
    public Task CemuEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Ares-configuration message box.
    /// </summary>
    public Task FailedtoinjectAresconfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Cemu-configuration-saved message box.
    /// </summary>
    public Task CemuConfigurationSavedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Flycast-emulator-not-found message box.
    /// </summary>
    public Task FlycastEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Ares-configuration-saved-successfully message box.
    /// </summary>
    public Task AresConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Ares-configuration message box.
    /// </summary>
    public Task FailedToSaveAresConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Flycast-configuration message box.
    /// </summary>
    public Task FailedToInjectFlycastConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Flycast-configuration-saved-successfully message box.
    /// </summary>
    public Task FlycastConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Dolphin-emulator-not-found message box.
    /// </summary>
    public Task DolphinEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Flycast-configuration message box.
    /// </summary>
    public Task FailedToSaveFlycastConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Dolphin-configuration message box.
    /// </summary>
    public Task FailedToInjectDolphinConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Dolphin-configuration-saved-successfully message box.
    /// </summary>
    public Task DolphinConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Dolphin-configuration message box.
    /// </summary>
    public Task FailedToSaveDolphinConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Sega-Model-2-emulator-not-found message box.
    /// </summary>
    public Task SegaModel2EmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Sega-Model-2-configuration message box.
    /// </summary>
    public Task FailedToInjectSegaModel2ConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Sega-Model-2-configuration-saved-successfully message box.
    /// </summary>
    public Task SegaModel2ConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the BlastEm-emulator-not-found message box.
    /// </summary>
    public Task BlastemEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-BlastEm-configuration message box.
    /// </summary>
    public Task FailedToInjectBlastemConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the BlastEm-configuration-saved-successfully message box.
    /// </summary>
    public Task BlastemConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Sega-Model-2-configuration message box.
    /// </summary>
    public Task FailedToSaveSegaModel2ConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-BlastEm-configuration message box.
    /// </summary>
    public Task FailedToSaveBlastemConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RPCS3-emulator-not-found message box.
    /// </summary>
    public Task Rpcs3EmulatorNotFoundPleaseLocateMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-RPCS3-configuration message box.
    /// </summary>
    public Task FailedToInjectRpcs3ConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RPCS3-configuration-saved-successfully message box.
    /// </summary>
    public Task Rpcs3ConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-RPCS3-configuration message box.
    /// </summary>
    public Task FailedToSaveRpcs3ConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Stella-emulator-not-found message box.
    /// </summary>
    public Task StellaEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Stella-configuration message box.
    /// </summary>
    public Task FailedToInjectStellaConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Supermodel-emulator-not-found message box.
    /// </summary>
    public Task SupermodelEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Stella-configuration-saved-successfully message box.
    /// </summary>
    public Task StellaConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Supermodel-configuration message box.
    /// </summary>
    public Task FailedToInjectSupermodelConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Stella-configuration message box.
    /// </summary>
    public Task FailedToSaveStellaConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Supermodel-configuration-saved-successfully message box.
    /// </summary>
    public Task SupermodelConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Supermodel-configuration message box.
    /// </summary>
    public Task FailedToSaveSupermodelConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Mednafen-emulator-not-found message box.
    /// </summary>
    public Task MednafenEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Mesen-emulator-not-found message box.
    /// </summary>
    public Task MesenEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Mednafen-configuration message box.
    /// </summary>
    public Task FailedToInjectMednafenConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Mesen-configuration message box.
    /// </summary>
    public Task FailedToInjectMesenConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the DuckStation-emulator-not-found message box.
    /// </summary>
    public Task DuckStationEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Mednafen-configuration-saved-successfully message box.
    /// </summary>
    public Task MednafenConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Mednafen-configuration message box.
    /// </summary>
    public Task FailedToSaveMednafenConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-DuckStation-configuration message box.
    /// </summary>
    public Task FailedToInjectDuckStationConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the DuckStation-configuration-saved-successfully message box.
    /// </summary>
    public Task DuckStationConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Mesen-configuration message box.
    /// </summary>
    public Task FailedToSaveMesenConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-DuckStation-configuration message box.
    /// </summary>
    public Task FailedToSaveDuckStationConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Mesen-configuration-saved-successfully message box.
    /// </summary>
    public Task MesenConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-Ymir-configuration message box.
    /// </summary>
    public Task FailedToInjectYumirConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Ymir-configuration-saved-successfully message box.
    /// </summary>
    public Task YumirConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Raine-settings-saved-and-injected message box.
    /// </summary>
    public Task RaineSettingsSavedAndInjectedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Raine-executable-not-found message box.
    /// </summary>
    public Task RaineExecutableNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Ymir-emulator-not-found message box.
    /// </summary>
    public Task YumirEmulatorNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the ReDream-emulator-path-not-found message box.
    /// </summary>
    public Task ReDreamEmulatorPathNotFoundMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-inject-ReDream-configuration message box.
    /// </summary>
    public Task FailedToInjectReDreamConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the ReDream-configuration-injected-successfully message box.
    /// </summary>
    public Task ReDreamConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the could-not-launch-game-due-to-DEP-violation message box.
    /// </summary>
    public Task CouldNotLaunchGameDueToDepViolationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the MAME-ROM-set-error message box.
    /// </summary>
    public Task MameRomSetErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the MAME-unknown-system-error message box.
    /// </summary>
    public Task MameUnknownSystemErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the MAME-unable-to-load-image message box.
    /// </summary>
    public Task MameUnableToLoadImageMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Ootake-does-not-support-image-files message box.
    /// </summary>
    public Task OotakeDoesNotSupportImageFilesMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Geolith-does-not-support-compressed-files message box.
    /// </summary>
    public Task GeolithDoesNotSupportCompressedFilesMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RetroArch-parameter-should-contain-L message box.
    /// </summary>
    public Task RetroArchParameterShouldContainLMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RetroArch-parameter-issue message box.
    /// </summary>
    /// <param name="logPath">The path to the log file.</param>
    public Task RetroArchParameterIssueMessageBoxAsync(string? logPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the RetroArch-special-characters-in-path message box.
    /// </summary>
    public Task RetroArchSpecialCharactersInPathMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Azahar-configuration-injection-permission-error message box.
    /// </summary>
    public Task AzaharConfigurationInjectionPermissionErrorMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Azahar-configuration-saved-successfully message box.
    /// </summary>
    public Task AzaharConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-save-Azahar-configuration message box.
    /// </summary>
    public Task FailedToSaveAzaharConfigurationMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Xemu-parameter-should-contain-DVD-path message box.
    /// </summary>
    public Task XemuParameterShouldContainDvdPathMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the please-extract-application-first message box.
    /// </summary>
    public Task PleaseExtractApplicationFirstMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the injection-failed-generic message box.
    /// </summary>
    public Task InjectionFailedGenericMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the Daphne-configuration-save-failed message box.
    /// </summary>
    public Task DaphneConfigurationSaveFailedMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the image-download-timeout message box.
    /// </summary>
    public Task ShowImageDownloadTimeoutMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the system-name-required-before-choosing-image message box.
    /// </summary>
    public Task SystemNameRequiredBeforeChoosingImageMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the invalid-image-format message box.
    /// </summary>
    public Task InvalidImageFormatMessageBoxAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display the failed-to-copy-system-image message box.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public Task FailedToCopySystemImageMessageBoxAsync(string errorMessage)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display a warning message box.
    /// </summary>
    /// <param name="message">The warning message.</param>
    public Task WarningMessageBoxAsync(string message)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Does nothing. Does not display a custom error message box.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="title">The message box title.</param>
    public Task CustomErrorMessageBoxAsync(string message, string title)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see langword="false" /> without displaying a message box.
    /// </summary>
    /// <param name="title">The message box title.</param>
    /// <param name="message">The question message.</param>
    /// <returns><see langword="false" />.</returns>
    public Task<bool> CustomQuestionMessageBoxAsync(string title, string message)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    ///     Does nothing. Does not display a custom info message box.
    /// </summary>
    /// <param name="title">The message box title.</param>
    /// <param name="message">The info message.</param>
    public Task CustomInfoMessageBoxAsync(string title, string message)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns <see langword="false" /> without displaying a message box asking whether to use AI to fix parameters.
    /// </summary>
    /// <returns><see langword="false" />.</returns>
    public Task<bool> AskAiToFixParametersMessageBoxAsync()
    {
        return Task.FromResult(false);
    }
}