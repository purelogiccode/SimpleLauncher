using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Services.QuitOrReinstall;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using CoreMessageBoxButton = SimpleLauncher.Core.Models.MessageBoxButton;
using CoreMessageBoxImage = SimpleLauncher.Core.Models.MessageBoxImage;

namespace SimpleLauncher.Services.MessageBox;

/// <summary>
///     Provides localized message box dialogs for user notifications, errors, warnings, and confirmations throughout the
///     application.
/// </summary>
public class MessageBoxLibraryService : IMessageBoxLibraryService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly IMessageDialogService _messageDialog;
    private readonly QuitSimpleLauncher _quitSimpleLauncher;
    private readonly ReinstallSimpleLauncher _reinstallSimpleLauncher;
    private readonly IResourceProvider _resourceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageBoxLibraryService" /> class with the required dependencies.
    /// </summary>
    public MessageBoxLibraryService(IMessageDialogService messageDialog,
        ReinstallSimpleLauncher reinstallSimpleLauncher, QuitSimpleLauncher quitSimpleLauncher, ILogger logErrors,
        IConfiguration configuration, IResourceProvider resourceProvider)
    {
        _messageDialog = messageDialog;
        _reinstallSimpleLauncher = reinstallSimpleLauncher;
        _quitSimpleLauncher = quitSimpleLauncher;
        _logger = logErrors;
        _configuration = configuration;
        _resourceProvider = resourceProvider;
    }

    /// <summary>
    ///     Displays instructions for taking a screenshot of a game window.
    /// </summary>
    public Task TakeScreenShotMessageBoxAsync()
    {
        var thegamewilllaunchnow = _resourceProvider.GetString("Thegamewilllaunchnow", "The game will launch now.");
        var setthegamewindowto = _resourceProvider.GetString("Setthegamewindowto",
            "Set the game window to non-fullscreen. This is important.");
        var youshouldchangetheemulatorparameters = _resourceProvider.GetString("Youshouldchangetheemulatorparameters",
            "You should change the emulator parameters to prevent the emulator from starting in fullscreen.");
        var aselectionwindowwillopeninSimpleLauncherallowingyou = _resourceProvider.GetString(
            "AselectionwindowwillopeninSimpleLauncherallowingyou",
            "A selection window will open in 'Simple Launcher', allowing you to choose the desired window to capture.");
        var assoonasyouselectawindow = _resourceProvider.GetString("assoonasyouselectawindow",
            "As soon as you select a window, a screenshot will be taken and saved in the image folder of the selected system.");
        var takeScreenshot = _resourceProvider.GetString("TakeScreenshot", "Take Screenshot");

        return _messageDialog.ShowInfoAsync($"{thegamewilllaunchnow}\n\n" +
                                            $"{setthegamewindowto}\n\n" +
                                            $"{youshouldchangetheemulatorparameters}\n\n" +
                                            $"{aselectionwindowwillopeninSimpleLauncherallowingyou}\n\n" +
                                            $"{assoonasyouselectawindow}", takeScreenshot);
    }

    /// <summary>
    ///     Displays an error message when a screenshot fails to save.
    /// </summary>
    public Task CouldNotSaveScreenshotMessageBoxAsync()
    {
        var failedtosavescreenshot =
            _resourceProvider.GetString("Failedtosavescreenshot", "Failed to save screenshot.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{failedtosavescreenshot}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an informational message that the specified game is already in favorites.
    /// </summary>
    public Task GameIsAlreadyInFavoritesMessageBoxAsync(string fileNameWithExtension)
    {
        var isalreadyinfavorites = _resourceProvider.GetString("isalreadyinfavorites", "is already in favorites.");
        var info = _resourceProvider.GetString("Info", "Info");

        return _messageDialog.ShowInfoAsync($"{fileNameWithExtension} {isalreadyinfavorites}", info);
    }

    /// <summary>
    ///     Displays an error message when adding a game to favorites fails.
    /// </summary>
    public Task ErrorWhileAddingFavoritesMessageBoxAsync()
    {
        var anerroroccurredwhileaddingthisgame = _resourceProvider.GetString("Anerroroccurredwhileaddingthisgame",
            "An error occurred while adding this game to the favorites.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync(
            $"{anerroroccurredwhileaddingthisgame}\n\n{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when removing a game from favorites fails.
    /// </summary>
    public Task ErrorWhileRemovingGameFromFavoriteMessageBoxAsync()
    {
        var anerroroccurredwhileremoving = _resourceProvider.GetString("Anerroroccurredwhileremoving",
            "An error occurred while removing this game from favorites.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync(
            $"{anerroroccurredwhileremoving}\n\n{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when the Update History window fails to open.
    /// </summary>
    public Task ErrorOpeningTheUpdateHistoryWindowMessageBoxAsync()
    {
        var erroropeningtheUpdateHistorywindow = _resourceProvider.GetString("ErroropeningtheUpdateHistorywindow",
            "Error opening the Update History window.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync(
            $"{erroropeningtheUpdateHistorywindow}\n\n{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when a video link fails to open in the browser.
    /// </summary>
    public Task ErrorOpeningVideoLinkMessageBoxAsync()
    {
        var therewasaproblemopeningtheVideo = _resourceProvider.GetString("TherewasaproblemopeningtheVideo",
            "There was a problem opening the Video Link.");
        var ensureyouhaveadefaultbrowserinstalled = _resourceProvider.GetString("Ensureyouhaveadefaultbrowserinstalled",
            "Ensure you have a default browser installed and configured correctly on your system.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{therewasaproblemopeningtheVideo}\n\n" +
                                             $"{ensureyouhaveadefaultbrowserinstalled}", error);
    }

    /// <summary>
    ///     Displays an error message when an info link fails to open in the browser.
    /// </summary>
    public Task ProblemOpeningInfoLinkMessageBoxAsync()
    {
        var therewasaproblemopeningthe = _resourceProvider.GetString("Therewasaproblemopeningthe",
            "There was a problem opening the Info Link.");
        var ensureyouhaveadefaultbrowserinstalled = _resourceProvider.GetString("Ensureyouhaveadefaultbrowserinstalled",
            "Ensure you have a default browser installed and configured correctly on your system.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{therewasaproblemopeningthe}\n\n" +
                                             $"{ensureyouhaveadefaultbrowserinstalled}", error);
    }

    /// <summary>
    ///     Displays an error message when a URL fails to open in the browser.
    /// </summary>
    public Task ErrorOpeningUrlMessageBoxAsync()
    {
        var therewasaproblemopeningtheUrl =
            _resourceProvider.GetString("TherewasaproblemopeningtheUrl", "There was a problem opening the Url.");
        var ensureyouhaveadefaultbrowserinstalled = _resourceProvider.GetString("Ensureyouhaveadefaultbrowserinstalled",
            "Ensure you have a default browser installed and configured correctly on your system.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{therewasaproblemopeningtheUrl}\n\n" +
                                             $"{ensureyouhaveadefaultbrowserinstalled}", error);
    }

    /// <summary>
    ///     Displays an informational message that no cover image is associated with the game.
    /// </summary>
    public Task ThereIsNoCoverMessageBoxAsync()
    {
        var thereisnocoverfileassociated = _resourceProvider.GetString("Thereisnocoverfileassociated",
            "There is no cover file associated with this game.");
        var covernotfound = _resourceProvider.GetString("Covernotfound", "Cover not found");

        return _messageDialog.ShowInfoAsync(thereisnocoverfileassociated, covernotfound);
    }

    /// <summary>
    ///     Displays an informational message that no title snapshot is associated with the game.
    /// </summary>
    public Task ThereIsNoTitleSnapshotMessageBoxAsync()
    {
        var thereisnotitlesnapshot = _resourceProvider.GetString("Thereisnotitlesnapshot",
            "There is no title snapshot file associated with this game.");
        var titleSnapshotnotfound = _resourceProvider.GetString("TitleSnapshotnotfound", "Title Snapshot not found");

        return _messageDialog.ShowInfoAsync(thereisnotitlesnapshot, titleSnapshotnotfound);
    }

    /// <summary>
    ///     Displays an informational message that no gameplay snapshot is associated with the game.
    /// </summary>
    public Task ThereIsNoGameplaySnapshotMessageBoxAsync()
    {
        var thereisnogameplaysnapshot = _resourceProvider.GetString("Thereisnogameplaysnapshot",
            "There is no gameplay snapshot file associated with this game.");
        var gameplaySnapshotnotfound =
            _resourceProvider.GetString("GameplaySnapshotnotfound", "Gameplay Snapshot not found");

        return _messageDialog.ShowInfoAsync(thereisnogameplaysnapshot, gameplaySnapshotnotfound);
    }

    /// <summary>
    ///     Displays an informational message that no cart file is associated with the game.
    /// </summary>
    public Task ThereIsNoCartMessageBoxAsync()
    {
        var thereisnocartfile =
            _resourceProvider.GetString("Thereisnocartfile", "There is no cart file associated with this game.");
        var cartnotfound = _resourceProvider.GetString("Cartnotfound", "Cart not found");

        return _messageDialog.ShowInfoAsync(thereisnocartfile, cartnotfound);
    }

    /// <summary>
    ///     Displays an informational message that no video file is associated with the game.
    /// </summary>
    public Task ThereIsNoVideoFileMessageBoxAsync()
    {
        var thereisnovideofile =
            _resourceProvider.GetString("Thereisnovideofile", "There is no video file associated with this game.");
        var videonotfound = _resourceProvider.GetString("Videonotfound", "Video not found");

        return _messageDialog.ShowInfoAsync(thereisnovideofile, videonotfound);
    }

    /// <summary>
    ///     Displays an error message when the game manual fails to open.
    /// </summary>
    public Task CouldNotOpenManualMessageBoxAsync()
    {
        var failedtoopenthemanual = _resourceProvider.GetString("Failedtoopenthemanual", "Failed to open the manual.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{failedtoopenthemanual}\n\n{theerrorwasreportedtothedeveloper}",
            error);
    }

    /// <summary>
    ///     Displays an error message when no PDF viewer is installed to open a manual.
    /// </summary>
    public Task NoPdfViewerInstalledMessageBoxAsync()
    {
        var nopdfviewerinstalled =
            _resourceProvider.GetString("NoPDFViewerInstalled", "No PDF viewer is installed on your system.");
        var pleaseinstallapdfviewer = _resourceProvider.GetString("PleaseInstallAPDFViewer",
            "Please install a PDF viewer (such as Adobe Acrobat Reader, Sumatra PDF, or Microsoft Edge) to open this file.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{nopdfviewerinstalled}\n\n{pleaseinstallapdfviewer}", error);
    }

    /// <summary>
    ///     Displays an informational message that no manual is associated with the game.
    /// </summary>
    public Task ThereIsNoManualMessageBoxAsync()
    {
        var thereisnomanual =
            _resourceProvider.GetString("Thereisnomanual", "There is no manual associated with this file.");
        var manualNotFound = _resourceProvider.GetString("Manualnotfound", "Manual not found");

        return _messageDialog.ShowInfoAsync(thereisnomanual, manualNotFound);
    }

    /// <summary>
    ///     Displays an informational message that no walkthrough file is associated with the game.
    /// </summary>
    public Task ThereIsNoWalkthroughMessageBoxAsync()
    {
        var thereisnowalkthrough = _resourceProvider.GetString("Thereisnowalkthrough",
            "There is no walkthrough file associated with this game.");
        var walkthroughnotfound = _resourceProvider.GetString("Walkthroughnotfound", "Walkthrough not found");

        return _messageDialog.ShowInfoAsync(thereisnowalkthrough, walkthroughnotfound);
    }

    /// <summary>
    ///     Displays an informational message that no cabinet file is associated with the game.
    /// </summary>
    public Task ThereIsNoCabinetMessageBoxAsync()
    {
        var thereisnocabinetfile = _resourceProvider.GetString("Thereisnocabinetfile",
            "There is no cabinet file associated with this game.");
        var cabinetnotfound = _resourceProvider.GetString("Cabinetnotfound", "Cabinet not found");

        return _messageDialog.ShowInfoAsync(thereisnocabinetfile, cabinetnotfound);
    }

    /// <summary>
    ///     Displays an informational message that no flyer file is associated with the game.
    /// </summary>
    public Task ThereIsNoFlyerMessageBoxAsync()
    {
        var thereisnoflyer =
            _resourceProvider.GetString("Thereisnoflyer", "There is no flyer file associated with this game.");
        var flyernotfound = _resourceProvider.GetString("Flyernotfound", "Flyer not found");

        return _messageDialog.ShowInfoAsync(thereisnoflyer, flyernotfound);
    }

    /// <summary>
    ///     Displays an informational message that no PCB file is associated with the game.
    /// </summary>
    public Task ThereIsNoPcbMessageBoxAsync()
    {
        var thereisnoPcBfile =
            _resourceProvider.GetString("ThereisnoPCBfile", "There is no PCB file associated with this game.");
        var pCBnotfound = _resourceProvider.GetString("PCBnotfound", "PCB not found");

        return _messageDialog.ShowInfoAsync(thereisnoPcBfile, pCBnotfound);
    }

    /// <summary>
    ///     Displays a success message confirming a file was deleted.
    /// </summary>
    public Task FileSuccessfullyDeletedMessageBoxAsync(string fileNameWithExtension)
    {
        var thefile = _resourceProvider.GetString("Thefile", "The file");
        var hasbeensuccessfullydeleted =
            _resourceProvider.GetString("hasbeensuccessfullydeleted", "has been successfully deleted.");
        var fileDeleted = _resourceProvider.GetString("Filedeleted", "File deleted");

        return _messageDialog.ShowInfoAsync($"{thefile} '{fileNameWithExtension}' {hasbeensuccessfullydeleted}",
            fileDeleted);
    }

    /// <summary>
    ///     Displays an error message when a file could not be deleted.
    /// </summary>
    public Task FileCouldNotBeDeletedMessageBoxAsync(string fileNameWithExtension)
    {
        var anerroroccurredwhiletryingtodelete = _resourceProvider.GetString("Anerroroccurredwhiletryingtodelete",
            "An error occurred while trying to delete the file");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync(
            $"{anerroroccurredwhiletryingtodelete} '{fileNameWithExtension}'.\n\n" +
            $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an info message when a file no longer exists on disk and the game list will be refreshed.
    /// </summary>
    public Task FileNoLongerExistsMessageBoxAsync(string fileNameWithExtension)
    {
        var thefilenolongerexists =
            _resourceProvider.GetString("Thefilenolongerexists", "The file no longer exists on disk");
        var thegamelistwillberefreshed =
            _resourceProvider.GetString("Thegamelistwillberefreshed", "The game list will be refreshed.");
        var info = _resourceProvider.GetString("Info", "Info");

        return _messageDialog.ShowInfoAsync(
            $"{thefilenolongerexists} '{fileNameWithExtension}'.\n\n{thegamelistwillberefreshed}", info);
    }

    /// <summary>
    ///     Displays an error when the default image file is missing, with an option to reinstall the application.
    /// </summary>
    public async Task DefaultImageNotFoundMessageBoxAsync()
    {
        var defaultpngfileismissing =
            _resourceProvider.GetString("defaultpngfileismissing", "'default.png' file is missing.");
        var doyouwanttoreinstallSimpleLauncher = _resourceProvider.GetString("DoyouwanttoreinstallSimpleLauncher",
            "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var error = _resourceProvider.GetString("Error", "Error");

        var reinstall =
            await _messageDialog.ShowYesNoAsync(
                $"{defaultpngfileismissing}\n\n{doyouwanttoreinstallSimpleLauncher}", error);

        if (reinstall)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var pleasereinstallSimpleLauncher = _resourceProvider.GetString("PleasereinstallSimpleLauncher",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");

            await _messageDialog.ShowErrorAsync(pleasereinstallSimpleLauncher, error);
        }
    }

    /// <summary>
    ///     Displays an error message when the global search encounters an error.
    /// </summary>
    public Task GlobalSearchErrorMessageBoxAsync()
    {
        var therewasanerrorusingtheGlobal = _resourceProvider.GetString("TherewasanerrorusingtheGlobal",
            "There was an error using the Global Search.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync(
            $"{therewasanerrorusingtheGlobal}\n\n{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a warning prompting the user to enter a search term.
    /// </summary>
    public Task PleaseEnterSearchTermMessageBoxAsync()
    {
        var pleaseenterasearchterm =
            _resourceProvider.GetString("Pleaseenterasearchterm", "Please enter a search term.");
        var warning = _resourceProvider.GetString("Warning", "Warning");

        return _messageDialog.ShowWarningAsync(pleaseenterasearchterm, warning);
    }

    /// <summary>
    ///     Displays an error message when a game fails to launch, with an option to open the error log.
    /// </summary>
    public async Task ErrorLaunchingGameMessageBoxAsync(string? logPath)
    {
        var therewasanerrorlaunchingtheselected = _resourceProvider.GetString("Therewasanerrorlaunchingtheselected",
            "There was an error launching the selected game.");
        var dowanttoopenthefileerroruserlog = _resourceProvider.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result =
            await _messageDialog.ShowYesNoAsync(
                $"{therewasanerrorlaunchingtheselected}\n\n{dowanttoopenthefileerroruserlog}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwasnotfound = _resourceProvider.GetString("Thefileerroruserlogwasnotfound",
                    "The file 'error_user.log' was not found!");

                await _messageDialog.ShowErrorAsync(thefileerroruserlogwasnotfound, error);
            }
        }
    }

    /// <summary>
    ///     Displays an informational message prompting the user to select a game to launch.
    /// </summary>
    public Task SelectAGameToLaunchMessageBoxAsync()
    {
        var pleaseselectagametolaunch =
            _resourceProvider.GetString("Pleaseselectagametolaunch", "Please select a game to launch.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(pleaseselectagametolaunch, info);
    }

    /// <summary>
    ///     Displays a success message confirming a file was added to favorites.
    /// </summary>
    public Task FileAddedToFavoritesMessageBoxAsync(string fileNameWithoutExtension)
    {
        var hasbeenaddedtofavorites =
            _resourceProvider.GetString("hasbeenaddedtofavorites", "has been added to favorites.");
        var success = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync($"{fileNameWithoutExtension} {hasbeenaddedtofavorites}", success);
    }

    /// <summary>
    ///     Displays a success message confirming a file was removed from favorites.
    /// </summary>
    public Task FileRemovedFromFavoritesMessageBoxAsync(string fileNameWithoutExtension)
    {
        var wasremovedfromfavorites =
            _resourceProvider.GetString("wasremovedfromfavorites", "was removed from favorites.");
        var success = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync($"{fileNameWithoutExtension} {wasremovedfromfavorites}", success);
    }

    /// <summary>
    ///     Displays an error when a specific game could not be launched, with an option to open the error log.
    /// </summary>
    public async Task CouldNotLaunchThisGameMessageBoxAsync(string? logPath)
    {
        var simpleLaunchercouldnotlaunchthisgame = _resourceProvider.GetString("SimpleLaunchercouldnotlaunchthisgame",
            "'Simple Launcher' could not launch this game.");
        var dowanttoopenthefileerroruserlog = _resourceProvider.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");
        var result = await _messageDialog.ShowYesNoAsync($"{simpleLaunchercouldnotlaunchthisgame}\n\n" +
                                                         $"{dowanttoopenthefileerroruserlog}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwasnotfound = _resourceProvider.GetString("Thefileerroruserlogwasnotfound",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwasnotfound, error);
            }
        }
    }

    /// <summary>
    ///     Displays a warning that a protocol handler for the specified protocol is not registered.
    /// </summary>
    public Task ProtocolHandlerNotRegisteredMessageBoxAsync(string protocol)
    {
        var protocolHandlerNotRegistered = _resourceProvider.GetString("ProtocolHandlerNotRegistered",
            "Protocol handler for '{0}://' is not registered. Please ensure the associated application is installed.");
        var launchErrorTitle = _resourceProvider.GetString("LaunchErrorTitle", "Launch Error");

        return _messageDialog.ShowWarningAsync(
            string.Format(CultureInfo.InvariantCulture, protocolHandlerNotRegistered, protocol), launchErrorTitle);
    }

    /// <summary>
    ///     Displays a warning that the emulator executable path is not configured for the system.
    /// </summary>
    public Task EmulatorPathNotConfiguredMessageBoxAsync()
    {
        var emulatorPathNotConfigured =
            _resourceProvider.GetString("EmulatorPathNotConfigured", "The emulator path is not configured.");
        var emulatorPathNotConfiguredDetails1 = _resourceProvider.GetString("EmulatorPathNotConfiguredDetails1",
            "The emulator you are using does not have a valid executable path configured.");
        var emulatorPathNotConfiguredDetails2 =
            _resourceProvider.GetString("EmulatorPathNotConfiguredDetails2", "This typically happens when:");
        var emulatorPathNotConfiguredDetails3 = _resourceProvider.GetString("EmulatorPathNotConfiguredDetails3",
            "- The system was configured to run executable files directly.");
        var emulatorPathNotConfiguredDetails4 = _resourceProvider.GetString("EmulatorPathNotConfiguredDetails4",
            "- But you are trying to launch a file that requires an emulator");
        var emulatorPathNotConfiguredDetails5 = _resourceProvider.GetString("EmulatorPathNotConfiguredDetails5",
            "Please edit the system configuration and provide a valid emulator path.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowWarningAsync($"{emulatorPathNotConfigured}\n" +
                                               $"{emulatorPathNotConfiguredDetails1}\n\n" +
                                               $"{emulatorPathNotConfiguredDetails2}\n" +
                                               $"{emulatorPathNotConfiguredDetails3}\n" +
                                               $"{emulatorPathNotConfiguredDetails4}\n\n" +
                                               $"{emulatorPathNotConfiguredDetails5}", error);
    }

    /// <summary>
    ///     Displays an error stating that the configured emulator executable was not found on disk.
    /// </summary>
    /// <param name="configuredPath">The emulator path as configured in the system settings.</param>
    public Task EmulatorExecutableNotFoundMessageBoxAsync(string configuredPath)
    {
        var emulatorNotFound = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        var details1 = _resourceProvider.GetString("EmulatorExecutableNotFoundDetails1",
            "The emulator executable was not found at the configured path:");
        var details2 = _resourceProvider.GetString("EmulatorExecutableNotFoundDetails2",
            "The emulator may have been moved, renamed, or deleted.");
        var details3 = _resourceProvider.GetString("EmulatorExecutableNotFoundDetails3",
            "Please edit the system configuration and select the emulator executable again.");

        return _messageDialog.ShowErrorAsync(
            $"{details1}\n{configuredPath}\n\n{details2}\n{details3}", emulatorNotFound);
    }

    /// <summary>
    ///     Displays an error stating that the file is not a valid executable for the current OS platform.
    /// </summary>
    public Task InvalidExecutableFileMessageBoxAsync()
    {
        var invalidExecutableFile = _resourceProvider.GetString("InvalidExecutableFile", "Invalid Executable File");
        var details1 = _resourceProvider.GetString("InvalidExecutableFileDetails1",
            "The file could not be run because it is not a valid application for this operating system.");
        var details2 = _resourceProvider.GetString("InvalidExecutableFileDetails2",
            "The file may be corrupted, incomplete, or designed for a different platform.");
        var details3 = _resourceProvider.GetString("InvalidExecutableFileDetails3",
            "If this is a game for another system, please configure an emulator for that system and launch the game through it.");

        return _messageDialog.ShowErrorAsync($"{details1}\n\n{details2}\n{details3}", invalidExecutableFile);
    }

    /// <summary>
    ///     Displays an error message when calculating global statistics fails.
    /// </summary>
    public Task ErrorCalculatingStatsMessageBoxAsync()
    {
        var anerroroccurredwhilecalculatingtheGlobal = _resourceProvider.GetString(
            "AnerroroccurredwhilecalculatingtheGlobal", "An error occurred while calculating the Global Statistics.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{anerroroccurredwhilecalculatingtheGlobal}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when saving a statistics report fails.
    /// </summary>
    public Task FailedSaveReportMessageBoxAsync()
    {
        var failedtosavethereport = _resourceProvider.GetString("Failedtosavethereport", "Failed to save the report.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{failedtosavethereport}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a success message confirming a statistics report was saved.
    /// </summary>
    public Task ReportSavedMessageBoxAsync()
    {
        var reportsavedsuccessfully =
            _resourceProvider.GetString("Reportsavedsuccessfully", "Report saved successfully.");
        var success = _resourceProvider.GetString("Success", "Success");

        return _messageDialog.ShowInfoAsync(reportsavedsuccessfully, success);
    }

    /// <summary>
    ///     Displays a warning that no statistics are available to save.
    /// </summary>
    public Task NoStatsToSaveMessageBoxAsync()
    {
        var nostatisticsavailabletosave =
            _resourceProvider.GetString("Nostatisticsavailabletosave", "No statistics available to save.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowWarningAsync(nostatisticsavailabletosave, error);
    }

    /// <summary>
    ///     Displays an error when launching a tool fails, with an option to open the error log.
    /// </summary>
    public async Task ErrorLaunchingToolMessageBoxAsync(string? logPath)
    {
        var anerroroccurredwhilelaunchingtheselectedtool = _resourceProvider.GetString(
            "Anerroroccurredwhilelaunchingtheselectedtool", "An error occurred while launching the selected tool.");
        var grantSimpleLauncheradministrative = _resourceProvider.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var temporarilydisableyourantivirussoftware = _resourceProvider.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var dowanttoopenthefileerroruserlog = _resourceProvider.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");
        var result = await _messageDialog.ShowYesNoAsync($"{anerroroccurredwhilelaunchingtheselectedtool}\n\n" +
                                                         $"{grantSimpleLauncheradministrative}\n\n" +
                                                         $"{temporarilydisableyourantivirussoftware}\n\n" +
                                                         $"{dowanttoopenthefileerroruserlog}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwasnotfound = _resourceProvider.GetString("Thefileerroruserlogwasnotfound",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwasnotfound, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when the selected tool executable is not found, with an option to reinstall.
    /// </summary>
    public async Task SelectedToolNotFoundMessageBoxAsync()
    {
        var theselectedtoolwasnotfound = _resourceProvider.GetString("Theselectedtoolwasnotfound",
            "The selected tool was not found in the expected path.");
        var doyouwanttoreinstallSimpleLauncher = _resourceProvider.GetString("DoyouwanttoreinstallSimpleLauncher",
            "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var error = _resourceProvider.GetString("Error", "Error");

        var reinstall = await _messageDialog.ShowYesNoAsync($"{theselectedtoolwasnotfound}\n\n" +
                                                            $"{doyouwanttoreinstallSimpleLauncher}", error);

        if (reinstall)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var pleasereinstallSimpleLaunchermanually = _resourceProvider.GetString(
                "PleasereinstallSimpleLaunchermanually",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");
            await _messageDialog.ShowErrorAsync(pleasereinstallSimpleLaunchermanually, error);
        }
    }

    /// <summary>
    ///     Displays a generic error message indicating an error was reported to the developer.
    /// </summary>
    public Task ErrorMessageBoxAsync()
    {
        var therewasanerror = _resourceProvider.GetString("Therewasanerror", "There was an error.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerror}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a warning that no favorite games were found for the selected system.
    /// </summary>
    public Task NoFavoriteFoundMessageBoxAsync()
    {
        var thereisnoFavoriteforthissystem = _resourceProvider.GetString("ThereisnoFavoriteforthissystem",
            "There is no Favorite for this system, or you have not chosen a system.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowInfoAsync(thereisnoFavoriteforthissystem, warning);
    }

    /// <summary>
    ///     Displays a warning that the application is in a restricted folder and needs to be moved to a writable location.
    /// </summary>
    public Task MoveToWritableFolderMessageBoxAsync()
    {
        var itlookslikeSimpleLauncherisinstalled = _resourceProvider.GetString("ItlookslikeSimpleLauncherisinstalled",
            "It looks like 'Simple Launcher' is installed in a folder where it does not have write access.");
        var itneedswriteaccesstoitsfolder =
            _resourceProvider.GetString("Itneedswriteaccesstoitsfolder", "It needs write access to its folder.");
        var pleasemovetheapplicationfolder = _resourceProvider.GetString("Pleasemovetheapplicationfolder",
            "Please move the application folder to a writable location (e.g., your home folder).");
        var ifpossiblerunitwithadministrative = _resourceProvider.GetString("Ifpossiblerunitwithadministrative",
            "If possible, run it with elevated privileges.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync($"{itlookslikeSimpleLauncherisinstalled}\n\n" +
                                               $"{itneedswriteaccesstoitsfolder}\n\n" +
                                               $"{pleasemovetheapplicationfolder}\n\n" +
                                               $"{ifpossiblerunitwithadministrative}", warning);
    }

    /// <summary>
    ///     Displays an error message when the system configuration could not be loaded.
    /// </summary>
    public Task InvalidSystemConfigMessageBoxAsync()
    {
        var therewasanerrorwhileloading = _resourceProvider.GetString("Therewasanerrorwhileloading",
            "There was an error while loading the system configuration for this system.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorwhileloading}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when loading the game file list fails.
    /// </summary>
    public Task ErrorMethodLoadGameFilesAsyncMessageBoxAsync()
    {
        var therewasanerrorloadingthegame = _resourceProvider.GetString("Therewasanerrorloadingthegame",
            "There was an error loading the game list.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorloadingthegame}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when the donation link fails to open.
    /// </summary>
    public Task ErrorOpeningDonationLinkMessageBoxAsync()
    {
        var therewasanerroropeningthedonation = _resourceProvider.GetString("Therewasanerroropeningthedonation",
            "There was an error opening the Donation Link.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerroropeningthedonation}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when toggling gamepad support fails.
    /// </summary>
    public Task ToggleGamepadFailureMessageBoxAsync()
    {
        var failedtotogglegamepad = _resourceProvider.GetString("Failedtotogglegamepad", "Failed to toggle gamepad.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{failedtotogglegamepad}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an informational message that the tool launch was canceled by the user.
    /// </summary>
    public Task ToolLaunchWasCanceledByUserMessageBoxAsync()
    {
        var thelaunchoftheselectedtoolwascanceledbytheuser = _resourceProvider.GetString(
            "thelaunchoftheselectedtoolwascanceledbytheuser",
            "The launch of the selected tool was canceled by the user.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(thelaunchoftheselectedtoolwascanceledbytheuser, info);
    }

    /// <summary>
    ///     Displays an error message when changing the view mode fails.
    /// </summary>
    public Task ErrorChangingViewModeMessageBoxAsync()
    {
        var therewasanerrorwhilechangingtheviewmode = _resourceProvider.GetString(
            "Therewasanerrorwhilechangingtheviewmode", "There was an error while changing the view mode.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorwhilechangingtheviewmode}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when a navigation button encounters an error.
    /// </summary>
    public Task NavigationButtonErrorMessageBoxAsync()
    {
        var therewasanerrorinthenavigationbutton = _resourceProvider.GetString("Therewasanerrorinthenavigationbutton",
            "There was an error in the navigation button.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorinthenavigationbutton}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a warning prompting the user to select a system before searching.
    /// </summary>
    public Task SelectSystemBeforeSearchMessageBoxAsync()
    {
        var pleaseselectasystembeforesearching = _resourceProvider.GetString("Pleaseselectasystembeforesearching",
            "Please select a system before searching.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(pleaseselectasystembeforesearching, warning);
    }

    /// <summary>
    ///     Displays a warning prompting the user to enter a search query.
    /// </summary>
    public Task EnterSearchQueryMessageBoxAsync()
    {
        var pleaseenterasearchquery =
            _resourceProvider.GetString("Pleaseenterasearchquery", "Please enter a search query.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(pleaseenterasearchquery, warning);
    }

    /// <summary>
    ///     Displays an error when loading helpuser.xml fails, with an option to reinstall the application.
    /// </summary>
    public async Task ErrorWhileLoadingHelpUserXmlMessageBoxAsync()
    {
        var unexpectederrorwhileloadinghelpuserxml =
            _resourceProvider.GetString("Unexpectederrorwhileloadinghelpuserxml",
                "Unexpected error while loading 'helpuser.xml'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{unexpectederrorwhileloadinghelpuserxml}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error when no valid systems are found in helpuser.xml, with an option to reinstall.
    /// </summary>
    public async Task NoSystemInHelpUserXmlMessageBoxAsync()
    {
        var novalidsystemsfoundinthefilehelpuserxml = _resourceProvider.GetString(
            "Novalidsystemsfoundinthefilehelpuserxml", "No valid systems found in the file 'helpuser.xml'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{novalidsystemsfoundinthefilehelpuserxml}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays a prompt asking whether to reinstall the application after helpuser.xml fails to load.
    /// </summary>
    public Task<CoreMessageBoxResult> CouldNotLoadHelpUserXmlMessageBoxAsync()
    {
        var simpleLaunchercouldnotloadhelpuserxml = _resourceProvider.GetString("SimpleLaunchercouldnotloadhelpuserxml",
            "'Simple Launcher' could not load 'helpuser.xml'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowAsync($"{simpleLaunchercouldnotloadhelpuserxml}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            CoreMessageBoxButton.YesNo, CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error when helpuser.xml is corrupted, with an option to reinstall the application.
    /// </summary>
    public async Task FailedToLoadHelpUserXmlMessageBoxAsync()
    {
        var unabletoloadhelpuserxml = _resourceProvider.GetString("Unabletoloadhelpuserxml",
            "Unable to load 'helpuser.xml'. The file may be corrupted.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{unabletoloadhelpuserxml}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error when helpuser.xml is missing, with an option to reinstall the application.
    /// </summary>
    public async Task FileHelpUserXmlIsMissingMessageBoxAsync()
    {
        var thefilehelpuserxmlismissing =
            _resourceProvider.GetString("Thefilehelpuserxmlismissing", "The file 'helpuser.xml' is missing.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{thefilehelpuserxmlismissing}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error when loading parameters.md fails, with an option to reinstall the application.
    /// </summary>
    public async Task ErrorWhileLoadingParametersMdMessageBoxAsync()
    {
        var unexpectederrorwhileloadingparametersmd = _resourceProvider.GetString(
            "Unexpectederrorwhileloadingparametersmd", "Unexpected error while loading 'parameters.md'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{unexpectederrorwhileloadingparametersmd}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error when no valid systems are found in parameters.md, with an option to reinstall.
    /// </summary>
    public async Task NoSystemInParametersMdMessageBoxAsync()
    {
        var novalidsystemsfoundinthefileparametersmd = _resourceProvider.GetString(
            "Novalidsystemsfoundinthefileparametersmd", "No valid systems found in the file 'parameters.md'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{novalidsystemsfoundinthefileparametersmd}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error when parameters.md fails to load, with an option to reinstall the application.
    /// </summary>
    public async Task FailedToLoadParametersMdMessageBoxAsync()
    {
        var unabletoloadparametersmd = _resourceProvider.GetString("Unabletoloadparametersmd",
            "Unable to load 'parameters.md'. The file may be corrupted or in use.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{unabletoloadparametersmd}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error when parameters.md is missing, with an option to reinstall the application.
    /// </summary>
    public async Task FileParametersMdIsMissingMessageBoxAsync()
    {
        var thefileparametersmdismissing =
            _resourceProvider.GetString("Thefileparametersmdismissing", "The file 'parameters.md' is missing.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{thefileparametersmdismissing}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error when parameters.md is empty, with an option to reinstall the application.
    /// </summary>
    public async Task FileParametersMdIsEmptyMessageBoxAsync()
    {
        var thefileparametersmdisempty =
            _resourceProvider.GetString("Thefileparametersmdisempty", "The file 'parameters.md' is empty.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{thefileparametersmdisempty}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error message when the image viewer fails to load an image.
    /// </summary>
    public Task ImageViewerErrorMessageBoxAsync()
    {
        var failedtoloadtheimageintheImage = _resourceProvider.GetString("FailedtoloadtheimageintheImage",
            "Failed to load the image in the Image Viewer window.");
        var theimagemaybecorruptedorinaccessible = _resourceProvider.GetString("Theimagemaybecorruptedorinaccessible",
            "The image may be corrupted or inaccessible.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{failedtoloadtheimageintheImage}\n\n" +
                                             $"{theimagemaybecorruptedorinaccessible}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error when mame.dat is corrupted, with an option to reinstall the application.
    /// </summary>
    public async Task ReinstallSimpleLauncherFileCorruptedMessageBoxAsync()
    {
        var simpleLaunchercouldnotloadthefilemamedat = _resourceProvider.GetString(
            "SimpleLaunchercouldnotloadthefilemamedat",
            "'Simple Launcher' could not load the file 'mame.dat' or it is corrupted.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _resourceProvider.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{simpleLaunchercouldnotloadthefilemamedat}\n\n" +
                                                         $"{doyouwanttoautomaticreinstallSimpleLauncher}", error);

        if (result)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var pleasereinstallSimpleLaunchermanually = _resourceProvider.GetString(
                "PleasereinstallSimpleLaunchermanually",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");
            var theapplicationwillshutdown =
                _resourceProvider.GetString("Theapplicationwillshutdown", "The application will shutdown.");
            await _messageDialog.ShowErrorAsync($"{pleasereinstallSimpleLaunchermanually}\n\n" +
                                                $"{theapplicationwillshutdown}", error);

            _quitSimpleLauncher.SimpleQuitApplication();
        }
    }

    /// <summary>
    ///     Displays an error when mame.dat is missing, with an option to reinstall the application.
    /// </summary>
    public async Task ReinstallSimpleLauncherFileMissingMessageBoxAsync()
    {
        var thefilemamedatcouldnotbefound = _resourceProvider.GetString("Thefilemamedatcouldnotbefound",
            "The file 'mame.dat' could not be found in the application folder.");
        var doyouwanttoautomaticreinstall = _resourceProvider.GetString("Doyouwanttoautomaticreinstall",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{thefilemamedatcouldnotbefound}\n\n"
                                                         + $"{doyouwanttoautomaticreinstall}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error message when checking for application updates fails.
    /// </summary>
    public Task ErrorCheckingForUpdatesMessageBoxAsync()
    {
        var anerroroccurredwhilecheckingforupdates =
            _resourceProvider.GetString("Anerroroccurredwhilecheckingforupdates",
                "An error occurred while checking for updates.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{anerroroccurredwhilecheckingforupdates}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when loading ROM history fails.
    /// </summary>
    public Task ErrorLoadingRomHistoryMessageBoxAsync()
    {
        var anerroroccurredwhileloadingRoMhistory = _resourceProvider.GetString("AnerroroccurredwhileloadingROMhistory",
            "An error occurred while loading ROM history.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{anerroroccurredwhileloadingRoMhistory}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error when no history.dat or history.xml file is found, with an option to reinstall.
    /// </summary>
    public async Task NoHistoryXmlOrDatFoundMessageBoxAsync()
    {
        var nohistoryxmlfilefound = _resourceProvider.GetString("Nohistoryxmlfilefound2",
            "No 'history.dat' or 'history.xml' file found in the application folder.");
        var doyouwanttoreinstallSimpleLauncher = _resourceProvider.GetString("DoyouwanttoreinstallSimpleLauncher",
            "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{nohistoryxmlfilefound}\n\n" +
                                                         $"{doyouwanttoreinstallSimpleLauncher}", error);

        if (result) _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
    }

    /// <summary>
    ///     Displays an error message when the browser fails to open.
    /// </summary>
    public Task ErrorOpeningBrowserMessageBoxAsync()
    {
        var anerroroccurredwhileopeningthebrowser = _resourceProvider.GetString("Anerroroccurredwhileopeningthebrowser",
            "An error occurred while opening the browser.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{anerroroccurredwhileopeningthebrowser}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error when system.xml is corrupted, with an option to open the error log, then shuts down.
    /// </summary>
    public async Task SystemXmlIsCorruptedMessageBoxAsync(string? logPath)
    {
        var systemxmliscorrupted =
            _resourceProvider.GetString("systemxmliscorrupted", "'system.xml' is corrupted or could not be opened.");
        var pleasefixitmanuallyordeleteit =
            _resourceProvider.GetString("Pleasefixitmanuallyordeleteit", "Please fix it manually or delete it.");
        var ifyouchoosetodeleteit = _resourceProvider.GetString("Ifyouchoosetodeleteit",
            "If you choose to delete it, 'Simple Launcher' will create a new one for you.");
        var theapplicationwillshutdown =
            _resourceProvider.GetString("Theapplicationwillshutdown", "The application will shutdown.");
        var wouldyouliketoopentheerroruserlog = _resourceProvider.GetString("Wouldyouliketoopentheerroruserlog",
            "Would you like to open the 'error_user.log' file to investigate the issue?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{systemxmliscorrupted} {pleasefixitmanuallyordeleteit}\n\n" +
                                                         $"{ifyouchoosetodeleteit}\n\n" +
                                                         $"{theapplicationwillshutdown}" +
                                                         $"{wouldyouliketoopentheerroruserlog}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlog =
                    _resourceProvider.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlog, error);
            }
        }

        _quitSimpleLauncher.SimpleQuitApplication();
    }

    /// <summary>
    ///     Displays an error when a game could not be launched, with an option to open the error log.
    /// </summary>
    public async Task WouldYouLikeToOpenTheLogMessageBoxAsync(string? logPath)
    {
        var simpleLauncherWasUnableToLaunchThisGame = _resourceProvider.GetString(
            "SimpleLauncherWasUnableToLaunchThisGame", "'Simple Launcher' was unable to launch this game.");
        var wouldyouliketoopentheerroruserlogfiletodebug = _resourceProvider.GetString(
            "Wouldyouliketoopentheerroruserlogfiletodebug",
            "Would you like to open the 'error_user.log' file to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");
        var result = await _messageDialog.ShowYesNoAsync($"{simpleLauncherWasUnableToLaunchThisGame}\n\n" +
                                                         $"{wouldyouliketoopentheerroruserlogfiletodebug}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex, "Failed to open the 'error_user.log' file. The file may not exist");
                var thefileerroruserlog =
                    _resourceProvider.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlog, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when the file system.xml is badly corrupted, with an option to open the error log.
    /// </summary>
    public async Task FileSystemXmlIsCorruptedMessageBoxAsync(string? logPath)
    {
        var thefilesystemxmlisbadlycorrupted = _resourceProvider.GetString("Thefilesystemxmlisbadlycorrupted",
            "The file 'system.xml' is badly corrupted.");
        var wouldyouliketoopentheerroruserlog = _resourceProvider.GetString("Wouldyouliketoopentheerroruserlog",
            "Would you like to open the 'error_user.log' file to investigate the issue?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{thefilesystemxmlisbadlycorrupted}\n\n" +
                                                         $"{wouldyouliketoopentheerroruserlog}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlog =
                    _resourceProvider.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlog, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error during update installation, with an option to open the GitHub releases page.
    /// </summary>
    public async Task InstallUpdateManuallyMessageBoxAsync()
    {
        var therewasanerrorinstallingorupdating = _resourceProvider.GetString("Therewasanerrorinstallingorupdating",
            "There was an error installing or updating the application.");
        var wouldyouliketoberedirectedtothedownloadpage = _resourceProvider.GetString(
            "Wouldyouliketoberedirectedtothedownloadpage",
            "Would you like to be redirected to the download page to install or update it manually?");
        var error = _resourceProvider.GetString("Error", "Error");

        var messageBoxResult = await _messageDialog.ShowYesNoAsync($"{therewasanerrorinstallingorupdating}\n\n" +
                                                                   $"{wouldyouliketoberedirectedtothedownloadpage}",
            error);

        if (messageBoxResult)
        {
            var downloadPageUrl = _configuration.GetValue<string>("Urls:GitHubReleases") ??
                                  "https://github.com/purelogiccode/SimpleLauncher/releases/latest/";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadPageUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex, "Error in method InstallUpdateManuallyMessageBoxAsync");

                // Notify user
                var anerroroccurredwhileopeningthebrowser = _resourceProvider.GetString(
                    "Anerroroccurredwhileopeningthebrowser", "An error occurred while opening the browser.");
                var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
                    "The error was reported to the developer who will try to fix the issue.");
                await _messageDialog.ShowErrorAsync($"{anerroroccurredwhileopeningthebrowser}\n\n" +
                                                    $"{theerrorwasreportedtothedeveloper}", error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when the updater fails to launch, with an option to open the GitHub releases page.
    /// </summary>
    public async Task UpdaterLaunchFailedMessageBoxAsync()
    {
        var updaterLaunchFailed = _resourceProvider.GetString("UpdaterLaunchFailed", "Failed to launch the Updater.");
        var accessDeniedExplanation = _resourceProvider.GetString("AccessDeniedExplanation",
            "This may be due to insufficient permissions or Windows security settings blocking the file.");
        var wouldyouliketoberedirectedtothedownloadpage = _resourceProvider.GetString(
            "Wouldyouliketoberedirectedtothedownloadpage",
            "Would you like to be redirected to the download page to install or update it manually?");
        var error = _resourceProvider.GetString("Error", "Error");

        var messageBoxResult = await _messageDialog.ShowYesNoAsync($"{updaterLaunchFailed}\n\n" +
                                                                   $"{accessDeniedExplanation}\n\n" +
                                                                   $"{wouldyouliketoberedirectedtothedownloadpage}",
            error);

        if (messageBoxResult)
        {
            var downloadPageUrl = _configuration.GetValue<string>("Urls:GitHubReleases") ??
                                  "https://github.com/purelogiccode/SimpleLauncher/releases/latest/";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadPageUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex, "Error in method UpdaterLaunchFailedMessageBoxAsync");

                // Notify user
                var anerroroccurredwhileopeningthebrowser = _resourceProvider.GetString(
                    "Anerroroccurredwhileopeningthebrowser", "An error occurred while opening the browser.");
                var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
                    "The error was reported to the developer who will try to fix the issue.");
                await _messageDialog.ShowErrorAsync($"{anerroroccurredwhileopeningthebrowser}\n\n" +
                                                    $"{theerrorwasreportedtothedeveloper}", error);
            }
        }
    }

    /// <summary>
    ///     Displays a warning when appsettings.json is missing, with an option to reinstall the application.
    /// </summary>
    public async Task RequiredFileMissingMessageBoxAsync()
    {
        var fileappsettingsjsonismissing =
            _resourceProvider.GetString("Fileappsettingsjsonismissing", "File 'appsettings.json' is missing.");
        var theapplicationwillnotbeabletosendthesupportrequest = _resourceProvider.GetString(
            "Theapplicationwillnotbeabletosendthesupportrequest",
            "The application will not be able to send the support request.");
        var doyouwanttoautomaticallyreinstall = _resourceProvider.GetString("Doyouwanttoautomaticallyreinstall",
            "Do you want to automatically reinstall 'Simple Launcher' to fix the problem?");
        var warning = _resourceProvider.GetString("Warning", "Warning");

        var messageBoxResult = await _messageDialog.ShowYesNoAsync($"{fileappsettingsjsonismissing}\n\n" +
                                                                   $"{theapplicationwillnotbeabletosendthesupportrequest}\n\n" +
                                                                   $"{doyouwanttoautomaticallyreinstall}", warning);

        if (messageBoxResult)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var pleasereinstallSimpleLauncher = _resourceProvider.GetString("PleasereinstallSimpleLauncher",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");
            await _messageDialog.ShowWarningAsync(pleasereinstallSimpleLauncher, warning);
        }
    }

    /// <summary>
    ///     Displays an informational message prompting the user to enter support request details.
    /// </summary>
    public Task EnterSupportRequestMessageBoxAsync()
    {
        var pleaseenterthedetailsofthesupportrequest = _resourceProvider.GetString(
            "Pleaseenterthedetailsofthesupportrequest", "Please enter the details of the support request.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(pleaseenterthedetailsofthesupportrequest, info);
    }

    /// <summary>
    ///     Displays an informational message prompting the user to enter a name.
    /// </summary>
    public Task EnterNameMessageBoxAsync()
    {
        var pleaseenterthename = _resourceProvider.GetString("Pleaseenterthename", "Please enter the name.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(pleaseenterthename, info);
    }

    /// <summary>
    ///     Displays an informational message prompting the user to enter an email address.
    /// </summary>
    public Task EnterEmailMessageBoxAsync()
    {
        var pleaseentertheemail = _resourceProvider.GetString("Pleaseentertheemail", "Please enter the email.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(pleaseentertheemail, info);
    }

    /// <summary>
    ///     Displays an error message when an API key error occurs in the support form.
    /// </summary>
    public Task ApiKeyErrorMessageBoxAsync()
    {
        var therewasanerrorintheApiKey = _resourceProvider.GetString("TherewasanerrorintheAPIKey",
            "There was an error in the API Key of this form.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorintheApiKey}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a success message confirming the support request was sent.
    /// </summary>
    public Task SupportRequestSuccessMessageBoxAsync()
    {
        var supportrequestsentsuccessfully =
            _resourceProvider.GetString("Supportrequestsentsuccessfully", "Support request sent successfully.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(supportrequestsentsuccessfully, info);
    }

    /// <summary>
    ///     Displays an error message when sending the support request fails.
    /// </summary>
    public Task SupportRequestSendErrorMessageBoxAsync()
    {
        var anerroroccurredwhilesendingthesupportrequest = _resourceProvider.GetString(
            "Anerroroccurredwhilesendingthesupportrequest", "An error occurred while sending the support request.");
        var thebugwasreportedtothedeveloper = _resourceProvider.GetString("Thebugwasreportedtothedeveloper",
            "The bug was reported to the developer that will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowInfoAsync($"{anerroroccurredwhilesendingthesupportrequest}\n\n" +
                                            $"{thebugwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when file extraction fails, with troubleshooting suggestions.
    /// </summary>
    public Task ExtractionFailedMessageBoxAsync()
    {
        var extractionfailed = _resourceProvider.GetString("Extractionfailed", "Extraction failed.");
        var ensurethefileisnotcorrupted =
            _resourceProvider.GetString("Ensurethefileisnotcorrupted", "Ensure the file is not corrupted.");
        var ensureyouhaveenoughspaceintheHdd = _resourceProvider.GetString("EnsureyouhaveenoughspaceintheHDD",
            "Ensure you have enough space in the HDD to extract the file.");
        var grantSimpleLauncheradministrative = _resourceProvider.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var ensuretheSimpleLauncherfolder = _resourceProvider.GetString("EnsuretheSimpleLauncherfolder",
            "Ensure the 'Simple Launcher' folder is a writable directory.");
        var temporarilydisableyourantivirus = _resourceProvider.GetString("Temporarilydisableyourantivirus",
            "Temporarily disable your antivirus software and try again.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{extractionfailed}\n\n" +
                                             $"{ensurethefileisnotcorrupted}\n" +
                                             $"{ensureyouhaveenoughspaceintheHdd}\n" +
                                             $"{grantSimpleLauncheradministrative}\n" +
                                             $"{ensuretheSimpleLauncherfolder}\n" +
                                             $"{temporarilydisableyourantivirus}", error);
    }

    /// <summary>
    ///     Displays a warning that the selected file must be a compressed archive (7z, zip, rar, tar.gz, or tgz) for extraction.
    /// </summary>
    public Task FileNeedToBeCompressedMessageBoxAsync()
    {
        var theselectedfilecannotbe =
            _resourceProvider.GetString("Theselectedfilecannotbe", "The selected file cannot be extracted.");
        var toextractafileitneedstobe = _resourceProvider.GetString("Toextractafileitneedstobe",
            "To extract a file, it needs to be a 7z, zip, rar, tar.gz, tgz, tar.xz, or txz file.");
        var pleasefixthatintheEditwindow =
            _resourceProvider.GetString("PleasefixthatintheEditwindow", "Please fix that in the Edit window.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync($"{theselectedfilecannotbe}\n\n" +
                                               $"{toextractafileitneedstobe}\n\n" +
                                               $"{pleasefixthatintheEditwindow}", warning);
    }

    /// <summary>
    ///     Displays an error message when a downloaded file is missing, with a OneDrive sync suggestion.
    /// </summary>
    public Task DownloadedFileIsMissingMessageBoxAsync()
    {
        var downloadedfileismissing =
            _resourceProvider.GetString("Downloadedfileismissing", "Downloaded file is missing.");
        var oneDriveIssue = _resourceProvider.GetString("oneDriveIssue",
            "If the file is in OneDrive, ensure it is synced and downloaded to your device. Right-click the file in File Explorer and select 'Always keep on this device'.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{downloadedfileismissing}\n\n" +
                                             $"{oneDriveIssue}", error);
    }

    /// <summary>
    ///     Displays an error when a downloaded file is locked, with an option to open the temp folder.
    /// </summary>
    public async Task FileIsLockedMessageBoxAsync(string? tempFolderPath)
    {
        var downloadedfileislocked =
            _resourceProvider.GetString("Downloadedfileislocked", "Downloaded file is locked.");
        var grantSimpleLauncheradministrative = _resourceProvider.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var temporarilydisableyourantivirussoftware = _resourceProvider.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var ensuretheSimpleLauncher = _resourceProvider.GetString("EnsuretheSimpleLauncher",
            "Ensure the 'Simple Launcher' folder is a writable directory.");
        var openTempFolderQuestion = _resourceProvider.GetString("OpenTempFolderQuestion",
            "Would you like to open the temporary folder to inspect the file?"); // New line
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{downloadedfileislocked}\n\n" +
                                                         $"{grantSimpleLauncheradministrative}\n\n" +
                                                         $"{temporarilydisableyourantivirussoftware}\n\n" +
                                                         $"{ensuretheSimpleLauncher}\n\n" +
                                                         $"{openTempFolderQuestion}", error); // Changed to YesNo

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempFolderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                var errorOpeningFolderTitle =
                    _resourceProvider.GetString("ErrorOpeningFolderTitle", "Error Opening Folder");
                var errorOpeningFolderMessage = _resourceProvider.GetString("ErrorOpeningFolderMessage",
                    "Could not open the temporary folder.");
                await _messageDialog.ShowErrorAsync(errorOpeningFolderMessage, errorOpeningFolderTitle);
                _logger.Error(ex, $"Failed to open temp folder: {tempFolderPath}");
            }
        }
    }

    /// <summary>
    ///     Displays a success message confirming that links were saved.
    /// </summary>
    public Task LinksSavedMessageBoxAsync()
    {
        var linkssavedsuccessfully = _resourceProvider.GetString("Linkssavedsuccessfully", "Links saved successfully.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(linkssavedsuccessfully, info);
    }

    /// <summary>
    ///     Displays a success message confirming that dead zone values were saved.
    /// </summary>
    public Task DeadZonesSavedMessageBoxAsync()
    {
        var deadZonevaluessavedsuccessfully = _resourceProvider.GetString("DeadZonevaluessavedsuccessfully",
            "DeadZone values saved successfully.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(deadZonevaluessavedsuccessfully, info);
    }

    /// <summary>
    ///     Displays a success message confirming that dead zone values were reverted to defaults.
    /// </summary>
    public Task DeadZonesRevertedMessageBoxAsync()
    {
        var deadZonesRevertedToDefaultValues = _resourceProvider.GetString("DeadZonesRevertedToDefaultValues",
            "DeadZone values reverted to default values.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(deadZonesRevertedToDefaultValues, info);
    }

    /// <summary>
    ///     Displays an informational message confirming that links were reverted to default values.
    /// </summary>
    public Task LinksRevertedMessageBoxAsync()
    {
        var linksreverted =
            _resourceProvider.GetString("Linksrevertedtodefaultvalues", "Links reverted to default values.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(linksreverted, info);
    }

    /// <summary>
    ///     Displays an error message when the main window search engine encounters an error.
    /// </summary>
    public Task MainWindowSearchEngineErrorMessageBoxAsync()
    {
        var therewasanerrorwiththesearchengine = _resourceProvider.GetString("Therewasanerrorwiththesearchengine",
            "There was an error with the search engine.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorwiththesearchengine}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when download or extraction of a file fails.
    /// </summary>
    public Task DownloadExtractionFailedMessageBoxAsync()
    {
        var downloadorextractionfailed =
            _resourceProvider.GetString("DownloadorExtractionFailed", "Download or extraction failed.");
        var grantSimpleLauncheradministrativeaccess = _resourceProvider.GetString(
            "GrantSimpleLauncheradministrativeaccess", "Grant 'Simple Launcher' administrative access and try again.");
        var ensuretheSimpleLauncherfolder = _resourceProvider.GetString("EnsuretheSimpleLauncherfolder",
            "Ensure the 'Simple Launcher' folder is a writable directory.");
        var temporarilydisableyourantivirus = _resourceProvider.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{downloadorextractionfailed}\n\n" +
                                             $"{grantSimpleLauncheradministrativeaccess}\n\n" +
                                             $"{ensuretheSimpleLauncherfolder}\n\n" +
                                             $"{temporarilydisableyourantivirus}", error);
    }

    /// <summary>
    ///     Displays a success message confirming that download and extraction completed.
    /// </summary>
    public Task DownloadAndExtractionWereSuccessfulMessageBoxAsync()
    {
        var downloadandextractioncompletedsuccessfully = _resourceProvider.GetString(
            "Downloadandextractioncompletedsuccessfully", "Download and extraction completed successfully.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(downloadandextractioncompletedsuccessfully, info);
    }

    /// <summary>
    ///     Displays an emulator download error with an option to open the emulator download page.
    /// </summary>
    public async Task ShowEmulatorDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        var downloaderror = _resourceProvider.GetString("Downloaderror", "Download error.");
        var wouldyouliketoberedirected = _resourceProvider.GetString("Wouldyouliketoberedirected",
            "Would you like to be redirected to the download page?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{downloaderror}\n\n" +
                                                         $"{wouldyouliketoberedirected}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                    { FileName = selectedSystem.Emulators.Emulator.EmulatorDownloadLink, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // Notify developer
                const string contextMessage = "Error opening the download link.";
                _logger.Error(ex, contextMessage);

                // Notify user
                var erroropeningthedownloadlink = _resourceProvider.GetString("Erroropeningthedownloadlink",
                    "Error opening the download link.");
                var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
                    "The error was reported to the developer who will try to fix the issue.");
                await _messageDialog.ShowErrorAsync($"{erroropeningthedownloadlink}\n\n" +
                                                    $"{theerrorwasreportedtothedeveloper}", error);
            }
        }
    }

    /// <summary>
    ///     Displays a core download error with an option to open the core download page.
    /// </summary>
    public async Task ShowCoreDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        var downloaderror = _resourceProvider.GetString("Downloaderror", "Download error.");
        var wouldyouliketoberedirected =
            _resourceProvider.GetString("Wouldyouliketoberedirected",
                "Would you like to be redirected to the download page?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{downloaderror}\n\n" +
                                                         $"{wouldyouliketoberedirected}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = selectedSystem.Emulators.Emulator.CoreDownloadLink,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Notify developer
                const string contextMessage = "Error opening the download link.";
                _logger.Error(ex, contextMessage);

                // Notify user
                var erroropeningthedownloadlink = _resourceProvider.GetString("Erroropeningthedownloadlink",
                    "Error opening the download link.");
                var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
                    "The error was reported to the developer who will try to fix the issue.");
                await _messageDialog.ShowErrorAsync($"{erroropeningthedownloadlink}\n\n" +
                                                    $"{theerrorwasreportedtothedeveloper}", error);
            }
        }
    }

    /// <summary>
    ///     Displays an image pack download error with an option to open the image pack download page.
    /// </summary>
    public async Task ShowImagePackDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        if (selectedSystem?.Emulators?.Emulator?.ImagePackDownloadLink == null) return;

        {
            var downloadError = _resourceProvider.GetString("Downloaderror", "Download error.");
            var wouldYouLikeToBeRedirected = _resourceProvider.GetString("Wouldyouliketoberedirected",
                "Would you like to be redirected to the download page?");
            var errorCaption = _resourceProvider.GetString("Error", "Error");

            var result = await _messageDialog.ShowYesNoAsync($"{downloadError}\n\n" +
                                                             $"{wouldYouLikeToBeRedirected}", errorCaption);

            if (result)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink, UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    // Notify developer
                    const string contextMessage = "Error opening the download link.";
                    _logger.Error(ex, contextMessage);

                    // Notify user
                    var errorOpeningDownloadLink = _resourceProvider.GetString("Erroropeningthedownloadlink",
                        "Error opening the download link.");
                    var errorWasReported = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
                        "The error was reported to the developer who will try to fix the issue.");
                    await _messageDialog.ShowErrorAsync($"{errorOpeningDownloadLink}\n\n{errorWasReported}",
                        errorCaption);
                }
            }
        }
    }

    /// <summary>
    ///     Displays a prompt to select a history item before attempting removal.
    /// </summary>
    public Task SelectAHistoryItemToRemoveMessageBoxAsync()
    {
        var message =
            _resourceProvider.GetString("SelectAHistoryItemToRemove", "Please select a history item to remove.");
        var pleaseselectaitem = _resourceProvider.GetString("Pleaseselectaitem", "Please select a item");
        return _messageDialog.ShowInfoAsync(message, pleaseselectaitem);
    }

    /// <summary>
    ///     Displays a confirmation prompt asking whether to remove all play history.
    /// </summary>
    public Task<CoreMessageBoxResult> ReallyWantToRemoveAllPlayHistoryMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("AreYouSureYouWantToRemoveAllHistory",
            "Are you sure you want to remove all play history?");
        var confirmation = _resourceProvider.GetString("Confirmation", "Confirmation");
        return _messageDialog.ShowAsync(message, confirmation, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays a success message confirming a system was added, with folder paths for ROMs and cover images.
    /// </summary>
    public Task SystemAddedMessageBoxAsync(string systemName, string resolvedSystemFolder,
        string resolvedSystemImageFolder)
    {
        var thesystem = _resourceProvider.GetString("Thesystem", "The system");
        var hasbeenaddedsuccessfully =
            _resourceProvider.GetString("hasbeenaddedsuccessfully", "has been added successfully.");
        var putRoMsorIsOsforthissysteminside = _resourceProvider.GetString("PutROMsorISOsforthissysteminside",
            "Put ROMs or ISOs for this system inside");
        var putcoverimagesforthissysteminside = _resourceProvider.GetString("Putcoverimagesforthissysteminside",
            "Put cover images for this system inside");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync($"{thesystem} '{systemName}' {hasbeenaddedsuccessfully}\n\n"
                                            + $"{putRoMsorIsOsforthissysteminside} '{resolvedSystemFolder}'\n\n"
                                            + $"{putcoverimagesforthissysteminside} '{resolvedSystemImageFolder}'.",
            info);
    }

    /// <summary>
    ///     Displays an error message when adding a system fails, with optional error details.
    /// </summary>
    public Task AddSystemFailedMessageBoxAsync(string? details = null)
    {
        var therewasanerroradding =
            _resourceProvider.GetString("Therewasanerroradding", "There was an error adding this system.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        var errorDetails = _resourceProvider.GetString("ErrorDetails", "Details:");

        var message = $"{therewasanerroradding}\n\n" +
                      $"{theerrorwasreportedtothedeveloper}";

        if (!string.IsNullOrEmpty(details)) message += $"\n\n{errorDetails} {details}";

        return _messageDialog.ShowErrorAsync(message, error);
    }

    /// <summary>
    ///     Displays an error message when the right-click context menu encounters an error.
    /// </summary>
    public Task RightClickContextMenuErrorMessageBoxAsync()
    {
        var therewasanerrorintherightclick = _resourceProvider.GetString("Therewasanerrorintherightclick",
            "There was an error in the right-click context menu.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorintherightclick}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an informational message that a game file no longer exists and has been removed from the list.
    /// </summary>
    public Task GameFileDoesNotExistMessageBoxAsync()
    {
        var thegamefiledoesnotexist =
            _resourceProvider.GetString("Thegamefiledoesnotexist", "The game file does not exist!");
        var thefilehasbeenremovedfromthelist = _resourceProvider.GetString("Thefilehasbeenremovedfromthelist",
            "The file has been removed from the list.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync($"{thegamefiledoesnotexist}\n\n" +
                                            $"{thefilehasbeenremovedfromthelist}", info);
    }

    /// <summary>
    ///     Displays a prompt asking whether to delete a play history entry when the game file no longer exists.
    /// </summary>
    public Task<CoreMessageBoxResult> GameFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath)
    {
        var thegamefiledoesnotexist =
            _resourceProvider.GetString("Thegamefiledoesnotexist", "The game file does not exist!");
        var filepathis = _resourceProvider.GetString("FilePathIs", "File path:");
        var doYouWantToDeleteThisEntry = _resourceProvider.GetString("DoYouWantToDeleteThisEntry",
            "Do you want to delete this entry from the play history?");
        var clickNoToKeepTheEntry =
            _resourceProvider.GetString("ClickNoToKeepTheEntry", "Click 'No' to keep the entry in the list.");
        var gameNotAvailable = _resourceProvider.GetString("GameNotAvailable", "Game Not Available");
        var message = $"{thegamefiledoesnotexist}\n\n" +
                      $"{filepathis}\n{filePath}\n\n" +
                      $"{doYouWantToDeleteThisEntry}\n" +
                      $"{clickNoToKeepTheEntry}";
        return _messageDialog.ShowAsync(message, gameNotAvailable, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays a prompt asking whether to delete a favorite entry when the game file no longer exists.
    /// </summary>
    public Task<CoreMessageBoxResult> FavoriteFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath)
    {
        var thegamefiledoesnotexist =
            _resourceProvider.GetString("Thegamefiledoesnotexist", "The game file does not exist!");
        var filepathis = _resourceProvider.GetString("FilePathIs", "File path:");
        var doYouWantToDeleteThisFavorite = _resourceProvider.GetString("DoYouWantToDeleteThisFavorite",
            "Do you want to delete this favorite from the list?");
        var clickNoToKeepTheFavorite =
            _resourceProvider.GetString("ClickNoToKeepTheFavorite", "Click 'No' to keep the favorite in the list.");
        var gameNotAvailable = _resourceProvider.GetString("GameNotAvailable", "Game Not Available");
        var message = $"{thegamefiledoesnotexist}\n\n" +
                      $"{filepathis}\n{filePath}\n\n" +
                      $"{doYouWantToDeleteThisFavorite}\n" +
                      $"{clickNoToKeepTheFavorite}";
        return _messageDialog.ShowAsync(message, gameNotAvailable, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error message when the History window fails to open.
    /// </summary>
    public Task CouldNotOpenHistoryWindowMessageBoxAsync()
    {
        var therewasaproblemopeningtheHistorywindow = _resourceProvider.GetString(
            "TherewasaproblemopeningtheHistorywindow", "There was a problem opening the History window.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasaproblemopeningtheHistorywindow}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when the walkthrough file fails to open.
    /// </summary>
    public Task CouldNotOpenWalkthroughMessageBoxAsync()
    {
        var failedtoopenthewalkthroughfile =
            _resourceProvider.GetString("Failedtoopenthewalkthroughfile", "Failed to open the walkthrough file.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{failedtoopenthewalkthroughfile}\n\n"
                                             + $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a warning prompting the user to select a favorite before attempting removal.
    /// </summary>
    public Task SelectAFavoriteToRemoveMessageBoxAsync()
    {
        var pleaseselectafavoritetoremove =
            _resourceProvider.GetString("Pleaseselectafavoritetoremove", "Please select a favorite to remove.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(pleaseselectafavoritetoremove, warning);
    }

    /// <summary>
    ///     Displays an error message when system.xml is not found in the application folder.
    /// </summary>
    public Task SystemXmlNotFoundMessageBoxAsync()
    {
        var systemxmlnotfound = _resourceProvider.GetString("systemxmlnotfound",
            "'system.xml' not found inside the application folder.");
        var pleaserestartSimpleLauncher =
            _resourceProvider.GetString("PleaserestartSimpleLauncher", "Please restart 'Simple Launcher'.");
        var ifthatdoesnotwork = _resourceProvider.GetString("Ifthatdoesnotwork",
            "If that does not work, please reinstall 'Simple Launcher'.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{systemxmlnotfound}\n\n" +
                                             $"{pleaserestartSimpleLauncher}\n\n" +
                                             $"{ifthatdoesnotwork}", error);
    }

    /// <summary>
    ///     Displays an informational message that a new system can now be added.
    /// </summary>
    public Task YouCanAddANewSystemMessageBoxAsync()
    {
        var youcanaddanewsystem = _resourceProvider.GetString("Youcanaddanewsystem", "You can add a new system now.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(youcanaddanewsystem, info);
    }

    /// <summary>
    ///     Displays an informational message that a specific emulator name is required because related data was provided.
    /// </summary>
    public Task EmulatorNameRequiredMessageBoxAsync(int i)
    {
        var emulator = _resourceProvider.GetString("Emulator", "Emulator");
        var nameisrequiredbecauserelateddata = _resourceProvider.GetString("nameisrequiredbecauserelateddata",
            "name is required because related data has been provided.");
        var pleasefixthisfield = _resourceProvider.GetString("Pleasefixthisfield", "Please fix this field.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync($"{emulator} {i} {nameisrequiredbecauserelateddata}\n\n" +
                                            $"{pleasefixthisfield}", info);
    }

    /// <summary>
    ///     Displays an informational message that an emulator name is required.
    /// </summary>
    public Task EmulatorNameIsRequiredMessageBoxAsync()
    {
        var emulatornameisrequired =
            _resourceProvider.GetString("Emulatornameisrequired", "Emulator name is required.");
        var pleasefixthat = _resourceProvider.GetString("Pleasefixthat", "Please fix that.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowInfoAsync($"{emulatornameisrequired}\n\n" +
                                            $"{pleasefixthat}", error);
    }

    /// <summary>
    ///     Displays an informational message that the emulator name must be unique.
    /// </summary>
    public Task EmulatorNameMustBeUniqueMessageBoxAsync(string emulatorName)
    {
        var thename = _resourceProvider.GetString("Thename", "The name");
        var isusedmultipletimes = _resourceProvider.GetString("isusedmultipletimes",
            "is used multiple times. Each emulator name must be unique.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync($"{thename} '{emulatorName}' {isusedmultipletimes}", info);
    }

    /// <summary>
    ///     Displays a success message confirming the system configuration was saved.
    /// </summary>
    public Task SystemSavedSuccessfullyMessageBoxAsync()
    {
        var systemsavedsuccessfully =
            _resourceProvider.GetString("Systemsavedsuccessfully", "System saved successfully.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(systemsavedsuccessfully, info);
    }

    /// <summary>
    ///     Displays an error message when one or more paths or parameters are invalid.
    /// </summary>
    public Task PathOrParameterInvalidMessageBoxAsync()
    {
        var oneormorepathsorparameters = _resourceProvider.GetString("Oneormorepathsorparameters",
            "One or more paths or parameters are invalid.");
        var pleasefixthemtoproceed =
            _resourceProvider.GetString("Pleasefixthemtoproceed", "Please fix them to proceed.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{oneormorepathsorparameters}\n\n" +
                                             $"{pleasefixthemtoproceed}", error);
    }

    /// <summary>
    ///     Displays an error message that Emulator 1 name is required.
    /// </summary>
    public Task Emulator1RequiredMessageBoxAsync()
    {
        var emulator1Nameisrequired =
            _resourceProvider.GetString("Emulator1Nameisrequired", "'Emulator 1 Name' is required.");
        var pleasefixthisfield = _resourceProvider.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{emulator1Nameisrequired}\n\n" +
                                             $"{pleasefixthisfield}", error);
    }

    /// <summary>
    ///     Displays an error message that the extension to launch after extraction is required.
    /// </summary>
    public Task ExtensionToLaunchIsRequiredMessageBoxAsync()
    {
        var extensiontoLaunchAfterExtraction = _resourceProvider.GetString("ExtensiontoLaunchAfterExtraction",
            "'Extension to Launch After Extraction' is required when 'Extract File Before Launch' is set to true.");
        var pleasefixthisfield = _resourceProvider.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{extensiontoLaunchAfterExtraction}\n\n" +
                                             $"{pleasefixthisfield}", error);
    }

    /// <summary>
    ///     Displays an error message that the extension to search in the system folder is required.
    /// </summary>
    public Task ExtensionToSearchIsRequiredMessageBoxAsync()
    {
        var extensiontoSearchintheSystemFolder = _resourceProvider.GetString("ExtensiontoSearchintheSystemFolder",
            "'Extension to Search in the System Folder' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _resourceProvider.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{extensiontoSearchintheSystemFolder}\n\n" +
                                             $"{pleasefixthisfield}", error);
    }

    /// <summary>
    ///     Displays an error message that the search extension must include zip, 7z, or rar when extraction is enabled.
    /// </summary>
    public Task FileMustBeCompressedMessageBoxAsync()
    {
        var whenExtractFileBeforeLaunch = _resourceProvider.GetString("WhenExtractFileBeforeLaunch",
            "When 'Extract File Before Launch' is set to true, 'Extension to Search in the System Folder' must include 'zip', '7z', or 'rar'.");
        var itwillnotacceptotherextensions =
            _resourceProvider.GetString("Itwillnotacceptotherextensions", "It will not accept other extensions.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{whenExtractFileBeforeLaunch}\n\n" +
                                             $"{itwillnotacceptotherextensions}", error);
    }

    /// <summary>
    ///     Displays an error message that the system image folder field cannot be empty.
    /// </summary>
    public Task SystemImageFolderCanNotBeEmptyMessageBoxAsync()
    {
        var systemImageFoldercannotbeempty = _resourceProvider.GetString("SystemImageFoldercannotbeempty",
            "'System Image Folder' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _resourceProvider.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{systemImageFoldercannotbeempty}\n\n" +
                                             $"{pleasefixthisfield}", error);
    }

    /// <summary>
    ///     Displays an error message that the system folder field cannot be empty.
    /// </summary>
    public Task SystemFolderCanNotBeEmptyMessageBoxAsync()
    {
        var systemFoldercannotbeempty = _resourceProvider.GetString("SystemFoldercannotbeempty",
            "'System Folder' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _resourceProvider.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{systemFoldercannotbeempty}\n\n" +
                                             $"{pleasefixthisfield}", error);
    }

    /// <summary>
    ///     Displays an error message that the system name field cannot be empty.
    /// </summary>
    public Task SystemNameCanNotBeEmptyMessageBoxAsync()
    {
        var systemNamecannotbeemptyor = _resourceProvider.GetString("SystemNamecannotbeemptyor",
            "'System Name' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _resourceProvider.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{systemNamecannotbeemptyor}\n\n" +
                                             $"{pleasefixthisfield}", error);
    }

    /// <summary>
    ///     Displays an error message listing invalid characters found in the system name.
    /// </summary>
    public Task InvalidSystemNameCharactersMessageBoxAsync(string invalidChars)
    {
        var systemNamecontainsinvalid =
            _resourceProvider.GetString("SystemNamecontainsinvalid", "'System Name' contains invalid characters:");
        var pleaseRemoveTheseCharacters = _resourceProvider.GetString("PleaseRemoveTheseCharacters",
            "Please remove these characters and try again.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{systemNamecontainsinvalid}\n\n{invalidChars}\n\n" +
                                             $"{pleaseRemoveTheseCharacters}", error);
    }

    /// <summary>
    ///     Displays an error message listing invalid characters found in the system folder name.
    /// </summary>
    public Task InvalidFolderCharactersMessageBoxAsync(string invalidChars)
    {
        var systemFoldercontainsinvalid = _resourceProvider.GetString("SystemFoldercontainsinvalid",
            "'System Folder' contains invalid characters:");
        var pleaseRemoveTheseCharacters = _resourceProvider.GetString("PleaseRemoveTheseCharacters",
            "Please remove these characters and try again.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{systemFoldercontainsinvalid}\n\n{invalidChars}\n\n" +
                                             $"{pleaseRemoveTheseCharacters}", error);
    }

    /// <summary>
    ///     Displays an error message when creating system folders fails, with troubleshooting suggestions.
    /// </summary>
    public Task FolderCreationFailedMessageBoxAsync()
    {
        var simpleLauncherfailedtocreatethe = _resourceProvider.GetString("SimpleLauncherfailedtocreatethe",
            "'Simple Launcher' failed to create the necessary folders for this system.");
        var grantSimpleLauncheradministrative = _resourceProvider.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var temporarilydisableyourantivirus = _resourceProvider.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var ensurethattheSimpleLauncherfolderislocatedinawritable = _resourceProvider.GetString(
            "EnsurethattheSimpleLauncherfolderislocatedinawritable",
            "Ensure that the 'Simple Launcher' folder is located in a writable directory.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync($"{simpleLauncherfailedtocreatethe}\n\n" +
                                            $"{grantSimpleLauncheradministrative}\n\n" +
                                            $"{temporarilydisableyourantivirus}\n\n" +
                                            $"{ensurethattheSimpleLauncherfolderislocatedinawritable}", info);
    }

    /// <summary>
    ///     Displays a warning prompting the user to select a system before deleting.
    /// </summary>
    public Task SelectASystemToDeleteMessageBoxAsync()
    {
        var pleaseselectasystemtodelete =
            _resourceProvider.GetString("Pleaseselectasystemtodelete", "Please select a system to delete.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(pleaseselectasystemtodelete, warning);
    }

    /// <summary>
    ///     Displays a warning that the selected system was not found in the XML document.
    /// </summary>
    public Task SystemNotFoundInTheXmlMessageBoxAsync()
    {
        var selectedsystemnotfound =
            _resourceProvider.GetString("Selectedsystemnotfound", "Selected system not found in the XML document!");
        var alert = _resourceProvider.GetString("Alert", "Alert");
        return _messageDialog.ShowWarningAsync(selectedsystemnotfound, alert);
    }

    /// <summary>
    ///     Displays an error when finding game files fails, with an option to open the error log.
    /// </summary>
    public async Task ErrorFindingGameFilesMessageBoxAsync(string logPath)
    {
        var therewasanerrorfinding =
            _resourceProvider.GetString("Therewasanerrorfinding", "There was an error finding the game files.");
        var doyouwanttoopenthefileerroruserlog = _resourceProvider.GetString("Doyouwanttoopenthefileerroruserlog",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");
        var result = await _messageDialog.ShowYesNoAsync($"{therewasanerrorfinding}\n\n" +
                                                         $"{doyouwanttoopenthefileerroruserlog}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                // Notify user
                var thefileerroruserlogwas = _resourceProvider.GetString("Thefileerroruserlogwas",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwas, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when the gamepad controller encounters an error, with an option to open the error log.
    /// </summary>
    public async Task GamePadErrorMessageBoxAsync(string? logPath)
    {
        var therewasanerrorwiththeGamePadController = _resourceProvider.GetString(
            "TherewasanerrorwiththeGamePadController", "There was an error with the GamePad Controller.");
        var grantSimpleLauncheradministrative = _resourceProvider.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var temporarilydisableyourantivirus = _resourceProvider.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var doyouwanttoopenthefile = _resourceProvider.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");
        var result = await _messageDialog.ShowYesNoAsync($"{therewasanerrorwiththeGamePadController}\n\n" +
                                                         $"{grantSimpleLauncheradministrative}\n\n" +
                                                         $"{temporarilydisableyourantivirus}\n\n" +
                                                         $"{doyouwanttoopenthefile}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwas = _resourceProvider.GetString("Thefileerroruserlogwas",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwas, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when a game could not be launched, with troubleshooting tips and an option to open the error log.
    /// </summary>
    public async Task CouldNotLaunchGameMessageBoxAsync(string? logPath)
    {
        var simpleLaunchercouldnotlaunch = _resourceProvider.GetString("SimpleLaunchercouldnotlaunch",
            "'Simple Launcher' could not launch the selected game.");
        var makesuretheRoMorIsOyouretrying = _resourceProvider.GetString("MakesuretheROMorISOyouretrying",
            "Make sure the ROM or ISO you're trying to run is not corrupted.");
        var ifyouaretryingtorunRetroarchensurethattheBios = _resourceProvider.GetString(
            "IfyouaretryingtorunRetroarchensurethattheBIOS",
            "If you are trying to run Retroarch, ensure that the BIOS or required files for the core are installed.");
        var alsomakesureyouarecallingtheemulator = _resourceProvider.GetString("Alsomakesureyouarecallingtheemulator",
            "Also, make sure you are calling the emulator with the correct parameter.");
        var youcanturnoffthistypeoferrormessageinExpertmode = _resourceProvider.GetString(
            "YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.");
        var doyouwanttoopenthefile = _resourceProvider.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");
        var result = await _messageDialog.ShowYesNoAsync($"{simpleLaunchercouldnotlaunch}\n\n" +
                                                         $"{makesuretheRoMorIsOyouretrying}\n" +
                                                         $"{ifyouaretryingtorunRetroarchensurethattheBios}\n" +
                                                         $"{alsomakesureyouarecallingtheemulator}\n\n" +
                                                         $"{youcanturnoffthistypeoferrormessageinExpertmode}\n\n" +
                                                         $"{doyouwanttoopenthefile}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwas = _resourceProvider.GetString("Thefileerroruserlogwas",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwas, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when an invalid operation occurs during game launch, with an option to open the error log.
    /// </summary>
    public async Task InvalidOperationExceptionMessageBoxAsync(string? logPath)
    {
        var simpleLaunchercouldnotlaunch = _resourceProvider.GetString("SimpleLaunchercouldnotlaunch",
            "'Simple Launcher' could not launch the selected game.");
        var makesuretheRoMorIsOyouretrying = _resourceProvider.GetString("MakesuretheROMorISOyouretrying",
            "Make sure the ROM or ISO you're trying to run is not corrupted.");
        var ifyouaretryingtorunRetroarchensurethattheBios = _resourceProvider.GetString(
            "IfyouaretryingtorunRetroarchensurethattheBIOS",
            "If you are trying to run Retroarch, ensure that the BIOS or required files for the core are installed.");
        var alsomakesureyouarecallingtheemulator = _resourceProvider.GetString("Alsomakesureyouarecallingtheemulator",
            "Also, make sure you are calling the emulator with the correct parameter.");
        var youcanturnoffthistypeoferrormessageinExpertmode = _resourceProvider.GetString(
            "YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.");
        var doyouwanttoopenthefile = _resourceProvider.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");
        var result = await _messageDialog.ShowYesNoAsync($"{simpleLaunchercouldnotlaunch}\n\n" +
                                                         $"{makesuretheRoMorIsOyouretrying}\n" +
                                                         $"{ifyouaretryingtorunRetroarchensurethattheBios}\n" +
                                                         $"{alsomakesureyouarecallingtheemulator}\n\n" +
                                                         $"{youcanturnoffthistypeoferrormessageinExpertmode}\n\n" +
                                                         $"{doyouwanttoopenthefile}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwas = _resourceProvider.GetString("Thefileerroruserlogwas",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwas, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when a game fails to launch, with an option to open the error log.
    /// </summary>
    public async Task ThereWasAnErrorLaunchingThisGameMessageBoxAsync(string? logPath)
    {
        var therewasanerrorlaunchingthisgame = _resourceProvider.GetString("Therewasanerrorlaunchingthisgame",
            "There was an error launching this game.");
        var youcanturnoffthistypeoferrormessageinExpertmode = _resourceProvider.GetString(
            "YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.");
        var doyouwanttoopenthefileerroruserlog = _resourceProvider.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{therewasanerrorlaunchingthisgame}\n\n" +
                                                         $"{youcanturnoffthistypeoferrormessageinExpertmode}\n\n" +
                                                         $"{doyouwanttoopenthefileerroruserlog}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlog =
                    _resourceProvider.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlog, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when a batch file fails to execute, with error details and an option to open the error log.
    /// </summary>
    public async Task BatchFileFailedMessageBoxAsync(string batchFilePath, string errorDetail, string? logPath,
        int? exitCode = null)
    {
        var batchFileName = Path.GetFileName(batchFilePath);
        var batchfilefailed = _resourceProvider.GetString("Batchfilefailed", "The batch file failed to run.");
        var batchNameMessage = $"{batchfilefailed}\n\n{batchFileName}";
        var errorMessage = !string.IsNullOrEmpty(errorDetail)
            ? $"Error: {errorDetail}\n\n"
            : "";
        var exitCodeMessage = exitCode.HasValue
            ? $"Exit code: {exitCode.Value}\n\n"
            : "";
        var explanation = exitCode is < 0
            ? _resourceProvider.GetString("Theprogramlaunchedbythisbatch",
                "The program launched by this batch file may have crashed or been terminated unexpectedly. Negative exit codes typically indicate system-level failures.")
            : _resourceProvider.GetString("Batchfilefailedexplanation",
                "This usually means a path referenced inside the batch file no longer exists or is incorrect.");
        var youcanturnoff = _resourceProvider.GetString("YoucanturnoffthiserrormessageinExpertmode",
            "You can turn off this error message in Expert mode.");
        var doyouwanttoopen = _resourceProvider.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");

        var message = $"{batchNameMessage}\n\n" +
                      $"{exitCodeMessage}{errorMessage}{explanation}\n\n" +
                      $"{youcanturnoff}\n\n" +
                      $"{doyouwanttoopen}";

        var result = await _messageDialog.ShowYesNoAsync(message, error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a batch file error message box. The file may not exist");
                var notFound =
                    _resourceProvider.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(notFound, error);
            }
        }
    }

    /// <summary>
    ///     Displays a warning listing missing paths referenced by a batch file, with an option to continue.
    /// </summary>
    public Task<bool> BatchFilePathsMissingMessageBoxAsync(IList<string> missingPaths)
    {
        var batchfilepathsmissing = _resourceProvider.GetString("Batchfilepathsmissing",
            "The batch file references paths that do not exist:");
        var batchfilepathsmissingexplanation = _resourceProvider.GetString("Batchfilepathsmissingexplanation",
            "This may cause the batch file to fail. Not all paths may be detected — this is a best-effort check.");
        var doyouwanttocontinueanyway =
            _resourceProvider.GetString("Doyouwanttocontinueanyway", "Do you want to continue anyway?");
        var warning = _resourceProvider.GetString("Warning", "Warning");

        var pathsList = string.Join("\n", missingPaths.Select(static p => $"  - {p}"));
        var message =
            $"{batchfilepathsmissing}\n\n{pathsList}\n\n{batchfilepathsmissingexplanation}\n\n{doyouwanttocontinueanyway}";

        return _messageDialog.ShowYesNoAsync(message, warning);
    }

    /// <summary>
    ///     Displays an error message when administrator privileges are required to launch a game.
    /// </summary>
    public Task ElevationRequiredMessageBoxAsync()
    {
        var therewasanerrorlaunchingthisgame = _resourceProvider.GetString("Therewasanerrorlaunchingthisgame",
            "There was an error launching this game.");
        var elevationrequired = _resourceProvider.GetString("ElevationRequired",
            "The requested operation requires elevation (Administrator privileges).");
        var cannotRunProgramsRequiringElevation = _resourceProvider.GetString("CannotRunProgramsRequiringElevation",
            "'Simple Launcher' cannot run programs that require elevation. Change the game's configuration to not require administrator rights (for example, uncheck 'Run this program as an administrator' in the executable's compatibility settings).");
        var error = _resourceProvider.GetString("Error", "Error");

        return _messageDialog.ShowErrorAsync($"{therewasanerrorlaunchingthisgame}\n\n" +
                                             $"{elevationrequired}\n\n" +
                                             $"{cannotRunProgramsRequiringElevation}", error);
    }

    /// <summary>
    ///     Displays an error when the file extension to launch after extraction is not configured.
    /// </summary>
    public Task NullFileExtensionMessageBoxAsync()
    {
        var thereisnoExtension = _resourceProvider.GetString("ThereisnoExtension",
            "There is no 'Extension to Launch After Extraction' set in the system configuration.");
        var pleaseeditthissystemto =
            _resourceProvider.GetString("Pleaseeditthissystemto", "Please edit this system to fix that.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{thereisnoExtension}\n\n" +
                                             $"{pleaseeditthissystemto}", error);
    }

    /// <summary>
    ///     Displays an error when no file matching the configured extension is found in the extracted folder.
    /// </summary>
    public Task CouldNotFindAFileMessageBoxAsync()
    {
        var couldnotfindafilewiththeextensiondefined = _resourceProvider.GetString(
            "Couldnotfindafilewiththeextensiondefined",
            "Could not find a file with the extension defined in 'Extension to Launch After Extraction' inside the extracted folder.");
        var pleaseeditthissystemtofix =
            _resourceProvider.GetString("Pleaseeditthissystemto", "Please edit this system to fix that.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{couldnotfindafilewiththeextensiondefined}\n\n" +
                                             $"{pleaseeditthissystemtofix}", error);
    }

    /// <summary>
    ///     Displays a prompt asking whether to search online for ROM history when none is found locally.
    /// </summary>
    public Task<CoreMessageBoxResult> SearchOnlineForRomHistoryMessageBoxAsync()
    {
        var thereisnoRoMhistoryinthelocaldatabase = _resourceProvider.GetString("ThereisnoROMhistoryinthelocaldatabase",
            "There is no ROM history in the local database for this file.");
        var doyouwanttosearchonline = _resourceProvider.GetString("Doyouwanttosearchonline",
            "Do you want to search online for the ROM history?");
        var rOmHistoryNotFound = _resourceProvider.GetString("ROMHistorynotfound", "ROM History not found");
        return _messageDialog.ShowAsync($"{thereisnoRoMhistoryinthelocaldatabase}\n\n" +
                                        $"{doyouwanttosearchonline}", rOmHistoryNotFound, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays a success message confirming a system was deleted.
    /// </summary>
    public Task SystemHasBeenDeletedMessageBoxAsync(string selectedSystemName)
    {
        var system = _resourceProvider.GetString("System", "System");
        var hasbeendeleted = _resourceProvider.GetString("hasbeendeleted", "has been deleted.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync($"{system} '{selectedSystemName}' {hasbeendeleted}", info);
    }

    /// <summary>
    ///     Displays a confirmation prompt asking whether to delete the selected system.
    /// </summary>
    public Task<CoreMessageBoxResult> AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync()
    {
        var areyousureyouwanttodeletethis = _resourceProvider.GetString("Areyousureyouwanttodeletethis",
            "Are you sure you want to delete this system?");
        var confirmation = _resourceProvider.GetString("Confirmation", "Confirmation");
        return _messageDialog.ShowAsync(areyousureyouwanttodeletethis, confirmation, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error message when deleting a game file fails.
    /// </summary>
    public Task ThereWasAnErrorDeletingTheGameMessageBoxAsync()
    {
        var therewasanerrordeletingthefile = _resourceProvider.GetString("Therewasanerrordeletingthefile",
            "There was an error deleting the file.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrordeletingthefile}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an error message when deleting a cover image fails.
    /// </summary>
    public Task ThereWasAnErrorDeletingTheCoverImageMessageBoxAsync()
    {
        var therewasanerrordeletingthecoverimage = _resourceProvider.GetString("Therewasanerrordeletingthecoverimage",
            "There was an error deleting the cover image.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrordeletingthecoverimage}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a confirmation prompt asking whether to permanently delete a game file.
    /// </summary>
    public Task<CoreMessageBoxResult> AreYouSureYouWantToDeleteTheGameMessageBoxAsync(string fileNameWithExtension)
    {
        var areyousureyouwanttodeletethefile = _resourceProvider.GetString("Areyousureyouwanttodeletethefile",
            "Are you sure you want to delete the file");
        var thisactionwilldelete = _resourceProvider.GetString("Thisactionwilldelete",
            "This action will delete the file from the HDD and cannot be undone.");
        var confirmDeletion = _resourceProvider.GetString("ConfirmDeletion", "Confirm Deletion");
        return _messageDialog.ShowAsync($"{areyousureyouwanttodeletethefile} '{fileNameWithExtension}'?\n\n" +
                                        $"{thisactionwilldelete}", confirmDeletion, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays a confirmation prompt asking whether to permanently delete a game's cover image.
    /// </summary>
    public Task<CoreMessageBoxResult> AreYouSureYouWantToDeleteTheCoverImageMessageBoxAsync(
        string fileNameWithoutExtension)
    {
        var areyousureyouwanttodeletethecoverimageof = _resourceProvider.GetString(
            "Areyousureyouwanttodeletethecoverimageof", "Are you sure you want to delete the cover image of");
        var thisactionwilldelete = _resourceProvider.GetString("Thisactionwilldelete",
            "This action will delete the file from the HDD and cannot be undone.");
        var confirmDeletion = _resourceProvider.GetString("ConfirmDeletion", "Confirm Deletion");
        return _messageDialog.ShowAsync(
            $"{areyousureyouwanttodeletethecoverimageof} '{fileNameWithoutExtension}'?\n\n" +
            $"{thisactionwilldelete}", confirmDeletion, CoreMessageBoxButton.YesNo, CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays a prompt asking whether to save a report with the current results.
    /// </summary>
    public Task<CoreMessageBoxResult> WouldYouLikeToSaveAReportMessageBoxAsync()
    {
        var wouldyouliketosaveareport = _resourceProvider.GetString("Wouldyouliketosaveareport",
            "Would you like to save a report with the results?");
        var saveReport = _resourceProvider.GetString("SaveReport", "Save Report");
        return _messageDialog.ShowAsync(wouldyouliketosaveareport, saveReport, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error message when the application is unable to restore the last backup.
    /// </summary>
    public Task SimpleLauncherWasUnableToRestoreBackupMessageBoxAsync()
    {
        var simpleLauncherwasunabletorestore = _resourceProvider.GetString("SimpleLauncherwasunabletorestore",
            "'Simple Launcher' was unable to restore the last backup.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(simpleLauncherwasunabletorestore, error);
    }

    /// <summary>
    ///     Displays a prompt asking whether to restore a backup when system.xml is missing.
    /// </summary>
    public Task<CoreMessageBoxResult> WouldYouLikeToRestoreTheLastBackupMessageBoxAsync()
    {
        var icouldnotfindthefilesystemxml = _resourceProvider.GetString("Icouldnotfindthefilesystemxml",
            "I could not find the file 'system.xml', which is required to start the application.");
        var butIfoundabackupfile = _resourceProvider.GetString("ButIfoundabackupfile", "But I found a backup file.");
        var wouldyouliketorestore =
            _resourceProvider.GetString("Wouldyouliketorestore", "Would you like to restore the last backup?");
        var restoreBackup = _resourceProvider.GetString("RestoreBackup", "Restore Backup?");
        return _messageDialog.ShowAsync($"{icouldnotfindthefilesystemxml}\n\n" +
                                        $"{butIfoundabackupfile}\n\n" +
                                        $"{wouldyouliketorestore}", restoreBackup, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error message when loading language resources fails.
    /// </summary>
    public Task FailedToLoadLanguageResourceMessageBoxAsync()
    {
        var failedtoloadlanguageresources =
            _resourceProvider.GetString("Failedtoloadlanguageresources", "Failed to load language resources.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var languageLoadingError = _resourceProvider.GetString("LanguageLoadingError", "Language Loading Error");
        return _messageDialog.ShowErrorAsync($"{failedtoloadlanguageresources}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", languageLoadingError);
    }

    /// <summary>
    ///     Displays a warning message about an invalid system configuration.
    /// </summary>
    public Task InvalidSystemConfigurationMessageBoxAsync(string errorMessage)
    {
        var invalidSystemConfiguration =
            _resourceProvider.GetString("InvalidSystemConfiguration", "Invalid System Configuration");
        return _messageDialog.ShowWarningAsync(errorMessage, invalidSystemConfiguration);
    }

    /// <summary>
    ///     Displays an error message when a link fails to open.
    /// </summary>
    public Task UnableToOpenLinkMessageBoxAsync()
    {
        var unabletoopenthelink = _resourceProvider.GetString("Unabletoopenthelink", "Unable to open the link.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{unabletoopenthelink}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays an informational message that no games were found for the random selection feature.
    /// </summary>
    public Task NoGameFoundInTheRandomSelectionMessageBoxAsync()
    {
        var nogamesfoundtorandomlyselectfrom = _resourceProvider.GetString("Nogamesfoundtorandomlyselectfrom",
            "No games found to randomly select from. Please check your system selection.");
        var feelingLucky = _resourceProvider.GetString("FeelingLucky", "Feeling Lucky");
        return _messageDialog.ShowInfoAsync(nogamesfoundtorandomlyselectfrom, feelingLucky);
    }

    /// <summary>
    ///     Displays a warning prompting the user to select a system before using the Feeling Lucky feature.
    /// </summary>
    public Task PleaseSelectASystemBeforeMessageBoxAsync()
    {
        var pleaseselectasystembeforeusingtheFeeling = _resourceProvider.GetString(
            "PleaseselectasystembeforeusingtheFeeling",
            "Please select a system before using the Feeling Lucky feature.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowInfoAsync(pleaseselectasystembeforeusingtheFeeling, warning);
    }

    /// <summary>
    ///     Displays an error message when toggling fuzzy matching logic fails.
    /// </summary>
    public Task ToggleFuzzyMatchingFailureMessageBoxAsync()
    {
        var therewasanerrortogglingthefuzzymatchinglogic = _resourceProvider.GetString(
            "Therewasanerrortogglingthefuzzymatchinglogic", "There was an error toggling the fuzzy matching logic.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(therewasanerrortogglingthefuzzymatchinglogic, error);
    }

    /// <summary>
    ///     Displays an error message when setting the fuzzy matching threshold fails.
    /// </summary>
    public Task FuzzyMatchingErrorFailToSetThresholdMessageBoxAsync()
    {
        var errorMessage = _resourceProvider.GetString("SetFuzzyMatchingThresholdFailureMessageBoxText",
            "Failed to set fuzzy matching threshold.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(errorMessage, error);
    }

    /// <summary>
    ///     Displays a list of validation errors for a system configuration.
    /// </summary>
    public Task ListOfErrorsMessageBoxAsync(StringBuilder errorMessages)
    {
        var editSystemtofixit = _resourceProvider.GetString("EditSystemtofixit", "Edit System to fix it.");
        var validationerrors = _resourceProvider.GetString("Validationerrors", "Validation errors");
        var fullMessage = errorMessages + editSystemtofixit;
        return _messageDialog.ShowErrorAsync(fullMessage, validationerrors);
    }

    /// <summary>
    ///     Displays an informational message that no update is available, showing the current version.
    /// </summary>
    public Task ThereIsNoUpdateAvailableMessageBoxAsync(string currentVersion)
    {
        var thereisnoupdateavailable =
            _resourceProvider.GetString("thereisnoupdateavailable", "There is no update available.");
        var thecurrentversionis = _resourceProvider.GetString("Thecurrentversionis", "The current version is");
        var noupdateavailable = _resourceProvider.GetString("Noupdateavailable", "No update available");
        return _messageDialog.ShowInfoAsync($"{thereisnoupdateavailable}\n\n" +
                                            $"{thecurrentversionis} {currentVersion}", noupdateavailable);
    }

    /// <summary>
    ///     Displays an informational message that another instance of Simple Launcher is already running.
    /// </summary>
    public Task AnotherInstanceIsRunningMessageBoxAsync()
    {
        var anotherinstanceofSimpleLauncherisalreadyrunning = _resourceProvider.GetString(
            "AnotherinstanceofSimpleLauncherisalreadyrunning",
            "Another instance of 'Simple Launcher' is already running.");
        return _messageDialog.ShowInfoAsync(anotherinstanceofSimpleLauncherisalreadyrunning, "Simple Launcher");
    }

    /// <summary>
    ///     Displays an error message when Simple Launcher fails to start due to an instance check error.
    /// </summary>
    public Task FailedToStartSimpleLauncherMessageBoxAsync()
    {
        var failedtostartSimpleLauncherAnerroroccurred = _resourceProvider.GetString(
            "FailedtostartSimpleLauncherAnerroroccurred",
            "Failed to start 'Simple Launcher'. An error occurred while checking for existing instances.");
        var simpleLauncherError = _resourceProvider.GetString("SimpleLauncherError", "Simple Launcher Error");
        return _messageDialog.ShowErrorAsync(failedtostartSimpleLauncherAnerroroccurred, simpleLauncherError);
    }

    /// <summary>
    ///     Displays an error message when the application fails to restart.
    /// </summary>
    public Task FailedToRestartMessageBoxAsync()
    {
        var failedtorestarttheapplication =
            _resourceProvider.GetString("Failedtorestarttheapplication", "Failed to restart the application.");
        var restartError = _resourceProvider.GetString("RestartError", "Restart Error");
        return _messageDialog.ShowErrorAsync(failedtorestarttheapplication, restartError);
    }

    /// <summary>
    ///     Displays a prompt asking whether to download and install an available update.
    /// </summary>
    public Task<CoreMessageBoxResult> DoYouWantToUpdateMessageBoxAsync(string currentVersion, string latestVersion)
    {
        var thereIsAsoftwareUpdateAvailable = _resourceProvider.GetString("Thereisasoftwareupdateavailable",
            "There is a software update available.");
        var theCurrentVersionIs = _resourceProvider.GetString("Thecurrentversionis", "The current version is");
        var theUpdateVersionIs = _resourceProvider.GetString("Theupdateversionis", "The update version is");
        var doYouWantToDownloadAndInstall = _resourceProvider.GetString("Doyouwanttodownloadandinstall",
            "Do you want to download and install the latest version automatically?");
        var updateAvailable = _resourceProvider.GetString("UpdateAvailable", "Update Available");
        return _messageDialog.ShowAsync($"{thereIsAsoftwareUpdateAvailable}\n" +
                                        $"{theCurrentVersionIs} {currentVersion}\n" +
                                        $"{theUpdateVersionIs} {latestVersion}\n\n" +
                                        $"{doYouWantToDownloadAndInstall}", updateAvailable, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Information);
    }

    /// <summary>
    ///     Displays an error when required files are missing, with an option to reinstall the application.
    /// </summary>
    public async Task HandleMissingRequiredFilesMessageBoxAsync(string fileList)
    {
        var thefollowingrequiredfilesaremissing = _resourceProvider.GetString("Thefollowingrequiredfilesaremissing",
            "The following required file(s) are missing:");
        var missingRequiredFiles = _resourceProvider.GetString("MissingRequiredFiles", "Missing Required Files");
        var doyouwanttoreinstallSimpleLauncher = _resourceProvider.GetString("DoyouwanttoreinstallSimpleLauncher",
            "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var reinstall = await _messageDialog.ShowYesNoAsync($"{thefollowingrequiredfilesaremissing}\n" +
                                                            $"{fileList}\n\n" +
                                                            $"{doyouwanttoreinstallSimpleLauncher}",
            missingRequiredFiles);

        if (reinstall)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var pleasereinstallSimpleLauncher = _resourceProvider.GetString("PleasereinstallSimpleLauncher",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");
            var theapplicationwillshutdown =
                _resourceProvider.GetString("Theapplicationwillshutdown", "The application will shutdown.");
            await _messageDialog.ShowErrorAsync($"{pleasereinstallSimpleLauncher}\n\n{theapplicationwillshutdown}",
                missingRequiredFiles);

            _quitSimpleLauncher.SimpleQuitApplication();
        }
    }

    /// <summary>
    ///     Displays an error when the API configuration fails to load, with an option to reinstall.
    /// </summary>
    public async Task HandleApiConfigErrorMessageBoxAsync(string reason)
    {
        var apiConfigErrorTitle = _resourceProvider.GetString("ApiConfigErrorTitle", "API Configuration Error");
        var apiConfigErrorMessage = _resourceProvider.GetString("ApiConfigErrorMessage",
            "'Simple Launcher' encountered an error loading its API configuration.");
        var reasonLabel = _resourceProvider.GetString("ReasonLabel", "Reason:");
        var reinstallSuggestion = _resourceProvider.GetString("ReinstallSuggestion",
            "This might prevent some features (like automatic bug reporting) from working correctly. Would you like to reinstall 'Simple Launcher' to fix this?");

        var result = await _messageDialog.ShowYesNoAsync($"{apiConfigErrorMessage}\n\n" +
                                                         $"{reasonLabel} {reason}\n\n" +
                                                         $"{reinstallSuggestion}", apiConfigErrorTitle);

        if (result)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var manualReinstallSuggestion = _resourceProvider.GetString("ManualReinstallSuggestion",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");
            var applicationWillShutdown =
                _resourceProvider.GetString("Theapplicationwillshutdown", "The application will shutdown.");

            await _messageDialog.ShowErrorAsync($"{manualReinstallSuggestion}\n\n" +
                                                $"{applicationWillShutdown}", apiConfigErrorTitle);

            _quitSimpleLauncher.SimpleQuitApplication();
        }
    }

    /// <summary>
    ///     Displays an error message when there is not enough disk space for extraction.
    /// </summary>
    public Task DiskSpaceErrorMessageBoxAsync()
    {
        var notenoughdiskspaceforextraction = _resourceProvider.GetString("Notenoughdiskspaceforextraction",
            "Not enough disk space for extraction.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(notenoughdiskspaceforextraction, error);
    }

    /// <summary>
    ///     Displays an error message when disk space cannot be checked for the specified path.
    /// </summary>
    public Task CouldNotCheckForDiskSpaceMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("SimpleLaunchercouldnotcheckdiskspace",
            "'Simple Launcher' could not check disk space for the specified path. Please check the path and try again.");
        var caption = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message, caption);
    }

    /// <summary>
    ///     Displays an error message when saving the system configuration fails.
    /// </summary>
    public Task SaveSystemFailedMessageBoxAsync(string? details = null)
    {
        var failedToSaveSystem =
            _resourceProvider.GetString("FailedToSaveSystem", "Failed to save system configuration.");
        var checkPermissions = _resourceProvider.GetString("CheckFilePermissions",
            "Please check file permissions and ensure the file is not locked.");
        var errorDetails = _resourceProvider.GetString("ErrorDetails", "Details:");
        var error = _resourceProvider.GetString("Error", "Error");

        var message = $"{failedToSaveSystem}\n\n" +
                      $"{checkPermissions}";

        if (!string.IsNullOrEmpty(details)) message += $"\n\n{errorDetails} {details}";

        return _messageDialog.ShowErrorAsync(message, error);
    }

    /// <summary>
    ///     Displays an error message when the download link could not be opened.
    /// </summary>
    public Task CouldNotOpenTheDownloadLinkMessageBoxAsync()
    {
        var simpleLaunchercouldnotopenthedownloadlink = _resourceProvider.GetString(
            "SimpleLaunchercouldnotopenthedownloadlink", "'Simple Launcher' could not open the download link.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(simpleLaunchercouldnotopenthedownloadlink, error);
    }

    /// <summary>
    ///     Displays an error message when loading appsettings.json fails.
    /// </summary>
    public Task ErrorLoadingAppSettingsMessageBoxAsync()
    {
        var therewasanerrorloadingconfiguration = _resourceProvider.GetString("Therewasanerrorloadingconfiguration",
            "There was an error loading 'appsettings.json'.");
        var theerrorwasreportedtothedeveloper = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{therewasanerrorloadingconfiguration}\n\n" +
                                             $"{theerrorwasreportedtothedeveloper}", error);
    }

    /// <summary>
    ///     Displays a security warning when a potential path manipulation (Zip Slip) is detected in an archive.
    /// </summary>
    public Task PotentialPathManipulationDetectedMessageBoxAsync(string archivePath)
    {
        var title = _resourceProvider.GetString("SecurityWarning", "Security Warning");
        var pathManipulationDetected =
            _resourceProvider.GetString("PathManipulationDetected", "Potential Path Manipulation Detected");
        var zipSlipExplanation = _resourceProvider.GetString("ZipSlipExplanation",
            "A security vulnerability called 'Zip Slip' was detected in the archive file. This is a path traversal vulnerability that could allow an attacker to write files outside of the intended extraction directory.");
        var archivePathMessage = _resourceProvider.GetString("ArchivePathMessage", "Archive file:");
        var actionTaken = _resourceProvider.GetString("ActionTaken",
            "For your security, the extraction process has been properly handle and the issue has been logged.");
        var reportedToDeveloper = _resourceProvider.GetString("ReportedToDeveloper",
            "This security issue has been reported to the developer team.");
        return _messageDialog.ShowWarningAsync($"{pathManipulationDetected}\n\n" +
                                               $"{zipSlipExplanation}\n\n" +
                                               $"{archivePathMessage} {archivePath}\n\n" +
                                               $"{actionTaken}\n\n" +
                                               $"{reportedToDeveloper}", title);
    }

    /// <summary>
    ///     Displays a warning when Easy Mode is unavailable due to Web API access issues, with troubleshooting suggestions.
    /// </summary>
    public Task CouldNotOpenSoundConfigurationWindowMessageBoxAsync()
    {
        var couldNotOpenSoundConfigurationWindow = _resourceProvider.GetString("CouldNotOpenSoundConfigurationWindow",
            "Could not open sound configuration window");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(couldNotOpenSoundConfigurationWindow, warning);
    }

    /// <summary>
    ///     Displays a warning when choosing or copying a sound file fails.
    /// </summary>
    public Task ErrorSettingSoundFileMessageBoxAsync()
    {
        var errorSettingSoundFile =
            _resourceProvider.GetString("errorSettingSoundFile", "Error choosing or copying sound file.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(errorSettingSoundFile, warning);
    }

    /// <summary>
    ///     Displays an informational message that notification sounds are disabled.
    /// </summary>
    public Task NotificationSoundIsDisableMessageBoxAsync()
    {
        var notificationSoundIsDisable =
            _resourceProvider.GetString("NotificationSoundIsDisable", "Notification sound is disable");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(notificationSoundIsDisable, info);
    }

    /// <summary>
    ///     Displays a warning that no sound file is currently selected.
    /// </summary>
    public Task NoSoundFileIsSelectedMessageBoxAsync()
    {
        var noSoundFileSelectedWarning =
            _resourceProvider.GetString("NoSoundFileSelectedWarning", "No sound file is selected.");
        var warning = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(noSoundFileSelectedWarning, warning);
    }

    /// <summary>
    ///     Displays a success message confirming settings were saved.
    /// </summary>
    public Task SettingsSavedSuccessfullyMessageBoxAsync()
    {
        var settingsSavedSuccessfully =
            _resourceProvider.GetString("SettingsSavedSuccessfully", "Settings saved successfully.");
        var info = _resourceProvider.GetString("Info", "Info");
        return _messageDialog.ShowInfoAsync(settingsSavedSuccessfully, info);
    }

    /// <summary>
    ///     Displays an error message when saving settings fails.
    /// </summary>
    public Task FailedToSaveSettingsMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("FailedToSaveSettings",
            "Failed to save settings. Please check that the application folder is writable and not locked by another process.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message, error);
    }

    /// <summary>
    ///     Displays an error when a game file path is invalid, with troubleshooting suggestions and an option to open the
    ///     error log.
    /// </summary>
    public async Task FilePathIsInvalidMessageBoxAsync(string? logPath)
    {
        var simpleLaunchercouldnotlaunch = _resourceProvider.GetString("SimpleLaunchercouldnotlaunch",
            "'Simple Launcher' could not launch the selected game.");
        var thefilepathisinvalid = _resourceProvider.GetString("Thefilepathisinvalid",
            "The filepath is invalid or the file does not exist!");
        var networkPathIssue = _resourceProvider.GetString("networkPathIssue",
            "If the file is on a network drive ensure your computer is still connected to that drive.");
        var usbDeviceIssue = _resourceProvider.GetString("usbDeviceIssue",
            "If the file is on a portable USB device ensure it is still connected to your computer.");
        var oneDriveIssue = _resourceProvider.GetString("oneDriveIssue",
            "If the file is in OneDrive, ensure it is synced and downloaded to your device. Right-click the file in File Explorer and select 'Always keep on this device'.");
        var avoidusingspecialcharactersinthefilepath = _resourceProvider.GetString(
            "Avoidusingspecialcharactersinthefilepath",
            "Avoid using special characters in the filepath, such as @, !, ?, ~, or any other special characters.");
        var youcanturnoffthistypeoferrormessageinExpertmode = _resourceProvider.GetString(
            "YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.");
        var doyouwanttoopenthefile = _resourceProvider.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _resourceProvider.GetString("Error", "Error");

        var result = await _messageDialog.ShowYesNoAsync($"{simpleLaunchercouldnotlaunch}\n\n" +
                                                         $"{thefilepathisinvalid}\n" +
                                                         $"{networkPathIssue}\n" +
                                                         $"{usbDeviceIssue}\n" +
                                                         $"{oneDriveIssue}\n" +
                                                         $"{avoidusingspecialcharactersinthefilepath}\n\n" +
                                                         $"{youcanturnoffthistypeoferrormessageinExpertmode}\n\n" +
                                                         $"{doyouwanttoopenthefile}", error);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwas = _resourceProvider.GetString("Thefileerroruserlogwas",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwas, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when mounting a file fails, with an option to download Dokan if needed.
    /// </summary>
    public async Task ThereWasAnErrorMountingTheFileMessageBoxAsync(int? exitCode = null)
    {
        var simpleLaunchercouldnotmount = _resourceProvider.GetString("SimpleLaunchercouldnotmount",
            "'Simple Launcher' could not mount the selected game.");
        var reasonMessage = exitCode switch
        {
            -1073741510 => _resourceProvider.GetString("ThisDokanVersionIncompatible",
                "The installed version of Dokan may be incompatible. Try reinstalling or updating Dokan."),
            -1073741515 => _resourceProvider.GetString("Dokannotinstalled",
                "Dokan library is not installed. Dokan is required for mounting ZIP, CHD and disk image files."),
            _ => _resourceProvider.GetString("ThismaybeduetoDokannotbeinginstalled2",
                "This may be due to Dokan not being installed. Dokan is required for mounting ZIP, CHD and disk image files.")
        };
        var doyouwanttoopenthefile = _resourceProvider.GetString("DoyouwanttoopenyourbrowsertodownloadDokan",
            "Do you want to open your browser to download Dokan?");
        var error = _resourceProvider.GetString("Error", "Error");
        var messageBoxResult = await _messageDialog.ShowYesNoAsync($"{simpleLaunchercouldnotmount}\n\n" +
                                                                   $"{reasonMessage}\n\n" +
                                                                   $"{doyouwanttoopenthefile}", error);

        if (messageBoxResult)
        {
            var downloadPageUrl = _configuration.GetValue<string>("Urls:DokanyWebsite") ??
                                  "https://github.com/dokan-dev/dokany";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadPageUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex, "Could not open the Dokan website");

                // Notify user
                var anerroroccurredwhileopeningthebrowser = _resourceProvider.GetString(
                    "Anerroroccurredwhileopeningyourbrowser", "An error occurred while opening your browser.");
                await _messageDialog.ShowErrorAsync(anerroroccurredwhileopeningthebrowser, error);
            }
        }
    }

    /// <summary>
    ///     Displays an error when the Dokan driver is not installed, with an option to open the Dokan download page.
    /// </summary>
    public async Task DokanDriverNotInstalledMessageBoxAsync()
    {
        var dokanDriverNotFound = _resourceProvider.GetString("DokanDriverNotFound",
            "The Dokan file system driver (dokan2.dll) is required to mount archives as virtual drives. It does not appear to be installed on this system.");
        var doYouWantToOpenBrowser = _resourceProvider.GetString("DoyouwanttoopenyourbrowsertodownloadDokan",
            "Do you want to open your browser to download Dokan?");
        var error = _resourceProvider.GetString("Error", "Error");
        var messageBoxResult =
            await _messageDialog.ShowYesNoAsync($"{dokanDriverNotFound}\n\n{doYouWantToOpenBrowser}", error);

        if (messageBoxResult)
        {
            var downloadPageUrl = _configuration.GetValue<string>("Urls:DokanyWebsite") ??
                                  "https://github.com/dokan-dev/dokany";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadPageUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Could not open the Dokan website");
                var anerroroccurredwhileopeningthebrowser = _resourceProvider.GetString(
                    "Anerroroccurredwhileopeningyourbrowser", "An error occurred while opening your browser.");
                await _messageDialog.ShowErrorAsync(anerroroccurredwhileopeningthebrowser, error);
            }
        }
    }

    /// <summary>
    ///     Displays informational text about launching a tool.
    /// </summary>
    public Task LaunchToolInformationMessageBoxAsync(string info)
    {
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowInfoAsync(info, error);
    }

    /// <summary>
    ///     Displays an error message that a screenshot cannot be taken of a minimized window.
    /// </summary>
    public Task CannotScreenshotMinimizedWindowMessageBoxAsync()
    {
        var cannottakeascreenshotofaminimizedwindow = _resourceProvider.GetString(
            "Cannottakeascreenshotofaminimizedwindow", "Cannot take a screenshot of a minimized window.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(cannottakeascreenshotofaminimizedwindow, error);
    }

    /// <summary>
    ///     Displays an error message when copying log content to the clipboard fails.
    /// </summary>
    public Task FailedToCopyLogContentMessageBoxAsync()
    {
        var failedtocopylogcontent =
            _resourceProvider.GetString("Failedtocopylogcontent", "Failed to copy log content.");
        var copyError = _resourceProvider.GetString("CopyError", "Copy Error");
        return _messageDialog.ShowErrorAsync(failedtocopylogcontent, copyError);
    }

    /// <summary>
    ///     Displays an error message when the updater application cannot be found on GitHub.
    /// </summary>
    public Task CouldNotFindUpdaterOnGitHubMessageBoxAsync()
    {
        var simpleLaunchercouldnotfindtheupdater = _resourceProvider.GetString("SimpleLaunchercouldnotfindtheupdater",
            "'Simple Launcher' could not find the updater application on GitHub.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(simpleLaunchercouldnotfindtheupdater, error);
    }

    /// <summary>
    ///     Displays an error message when the achievements window fails to open.
    /// </summary>
    public Task CouldNotOpenAchievementsWindowMessageBoxAsync()
    {
        var couldNotOpenAchievementsWindow = _resourceProvider.GetString("CouldNotOpenAchievementsWindow",
            "Could not open the achievements window.");
        var theErrorWasReported = _resourceProvider.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{couldNotOpenAchievementsWindow}\n\n{theErrorWasReported}", error);
    }

    /// <summary>
    ///     Displays a prompt when a game is not supported by RetroAchievements, with an option to open the global window.
    /// </summary>
    public Task<CoreMessageBoxResult> GameNotSupportedByRetroAchievementsMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("SimpleLaunchercouldnotcalculate",
            "'Simple Launcher' could not calculate the hash value of this game or this game is not yet supported by RetroAchievements.");
        var message2 = _resourceProvider.GetString("DoyouwanttoopentheglobalRetroAchievements",
            "Do you want to open the global RetroAchievements window?");
        var title = _resourceProvider.GetString("RetroAchievements", "RetroAchievements");
        return _messageDialog.ShowAsync($"{message1}\n\n" +
                                        $"{message2}", title, CoreMessageBoxButton.YesNo, CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error message when the game launch process times out.
    /// </summary>
    public Task GameLaunchTimeoutMessageBoxAsync()
    {
        var gamelaunchtimedoutPleasetryagainorcheckiftheemulatorstarted = _resourceProvider.GetString(
            "GamelaunchtimedoutPleasetryagainorcheckiftheemulatorstarted",
            "Game launch timed out. Please try again or check if the emulator started.");
        var gamelaunchtimedout = _resourceProvider.GetString("Gamelaunchtimedout", "Game launch timed out");
        return _messageDialog.ShowErrorAsync(gamelaunchtimedoutPleasetryagainorcheckiftheemulatorstarted,
            gamelaunchtimedout);
    }

    /// <summary>
    ///     Displays an informational message prompting the user to add RetroAchievements login credentials.
    /// </summary>
    public Task AddRaLoginMessageBoxAsync()
    {
        var youneedtoaddRetroAchievementlogin = _resourceProvider.GetString("YouneedtoaddRetroAchievementlogin",
            "You need to add RetroAchievement login information to use this feature.");
        var attention = _resourceProvider.GetString("Attention", "Attention");
        return _messageDialog.ShowInfoAsync(youneedtoaddRetroAchievementlogin, attention);
    }

    /// <summary>
    ///     Displays an error message when no default web browser is configured in the operating system.
    /// </summary>
    public Task NoDefaultBrowserConfiguredMessageBoxAsync()
    {
        var noDefaultBrowserConfiguredMessage = _resourceProvider.GetString("NoDefaultBrowserConfiguredMessage",
            "Your operating system does not have a default web browser configured. Please set one in Windows Settings (Apps > Default apps) to open web links.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(noDefaultBrowserConfiguredMessage, error);
    }

    /// <summary>
    ///     Displays a warning about high memory usage when setting a very high number of games per page in Grid mode.
    /// </summary>
    public Task<CoreMessageBoxResult> WarnUserAboutMemoryConsumptionMessageBoxAsync()
    {
        var warningMessage = _resourceProvider.GetString("WarningSettingupaveryhighnumberofgamesperpage",
            "Warning! Setting a very high number of games per page will significantly increase system memory usage when in Grid mode. If the number is too high, this may cause the application to crash. Please proceed with caution.");
        var proceedQuestion =
            _resourceProvider.GetString("AreYouSureYouWantToProceed", "Are you sure you want to proceed?");
        var warningTitle = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowAsync($"{warningMessage}\n\n{proceedQuestion}", warningTitle,
            CoreMessageBoxButton.YesNo, CoreMessageBoxImage.Warning);
    }

    /// <summary>
    ///     Displays a compatibility warning that the Group Files by Folder option only works with MAME or DOSBox emulators.
    /// </summary>
    public Task GroupByFolderOnlyForMameAndDosBoxMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("TheGroupFilesbyFolderoptionisonlycompatiblewith",
            "The 'Group Files by Folder' option is only compatible with MAME emulators (Software List CHDs) or DOSBox emulators (uncompressed DOS game folders). To use a different emulator, please edit the system settings and disable this option.");
        var title = _resourceProvider.GetString("CompatibilityWarning", "Compatibility Warning");
        return _messageDialog.ShowWarningAsync(message, title);
    }

    /// <summary>
    ///     Displays a configuration warning when Group Files by Folder is enabled without a compatible emulator.
    /// </summary>
    public Task<CoreMessageBoxResult> GroupByFolderWarningMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("YouhaveenabledGroupFilesbyFolderbuthave",
            "You have enabled 'Group Files by Folder' but have configured neither a MAME nor a DOSBox emulator. This option is only compatible with MAME (Software List CHDs) or DOSBox (uncompressed game folders). Are you sure you want to save these settings?");
        var title = _resourceProvider.GetString("ConfigurationWarning", "Configuration Warning");
        return _messageDialog.ShowAsync(message, title, CoreMessageBoxButton.YesNo, CoreMessageBoxImage.Warning);
    }

    /// <summary>
    ///     Displays a welcome message for first-time users with no systems configured, offering to add a system via Easy Mode.
    /// </summary>
    public Task<CoreMessageBoxResult> FirstRunWelcomeMessageBoxAsync()
    {
        var welcomeToSimpleLauncher =
            _resourceProvider.GetString("WelcomeToSimpleLauncher", "Welcome to 'Simple Launcher'!");
        var noSystemsFound =
            _resourceProvider.GetString("NoSystemsFound", "No systems were found in your configuration.");
        var easyModeGuide = _resourceProvider.GetString("DoyouwanttoaddyourfirstsystemusingtheEasyMode",
            "Do you want to add your first system using the Easy Mode?");
        var welcome = _resourceProvider.GetString("Welcome", "Welcome");
        return _messageDialog.ShowAsync($"{welcomeToSimpleLauncher}\n\n" +
                                        $"{noSystemsFound}\n\n" +
                                        $"{easyModeGuide}", welcome, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays a warning that the specified emulator's path is required.
    /// </summary>
    public Task EmulatorLocationRequiredMessageBoxAsync(int emulatorNumber)
    {
        var message = _resourceProvider.GetString($"Emulator{emulatorNumber}pathisrequired",
            $"Emulator {emulatorNumber} path is required.");
        var title = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(message, title);
    }

    /// <summary>
    ///     Displays an error when the image pack downloader Web API is unavailable.
    /// </summary>
    public Task ImagePackDownloaderUnavailableMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("SimpleLaunchercouldnotaccesstheWebAPI",
            "'Simple Launcher' could not access the Web API to download the updated URLs. Please try again later.");
        var title = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message, title);
    }

    /// <summary>
    ///     Displays a warning when Easy Mode is unavailable due to Web API access issues, with troubleshooting suggestions.
    /// </summary>
    public Task EasyModeUnavailableMessageBoxAsync()
    {
        var message = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration",
            "'Simple Launcher' could not access the Web API to download the updated configuration.");
        var message2 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration2", "This could be due to:");
        var message3 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration3",
            "• A government firewall or internet restriction in your region");
        var message4 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration4", "• Network connectivity issues");
        var message5 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration5",
            "To resolve this issue, you can:");
        var message6 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration6",
            "1. Enable a VPN connection and try again");
        var message7 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration7",
            "2. Check your internet connection");
        var message8 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration8",
            "3. Configure systems manually using the Edit System feature");
        var message9 = _resourceProvider.GetString(
            "SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration9",
            "Note: A VPN may be required if you are located in a country with internet restrictions.");
        var title = _resourceProvider.GetString("EasyModeUnavailable", "Easy Mode Unavailable");
        return _messageDialog.ShowWarningAsync($"{message}\n\n" +
                                               $"{message2}\n" +
                                               $"{message3}\n" +
                                               $"{message4}\n\n" +
                                               $"{message5}\n" +
                                               $"{message6}\n" +
                                               $"{message7}\n" +
                                               $"{message8}\n\n" +
                                               $"{message9}", title);
    }

    /// <summary>
    ///     Displays an error that RetroAchievements hash is not supported for systems grouped by folder.
    /// </summary>
    public Task SimpleLauncherDoesNotSupportRaHashOfSystemGroupedByFolderMessageBoxAsync()
    {
        var simpleLauncherdoesnotsupportRetroAchievementshashofSystems = _resourceProvider.GetString(
            "simpleLauncherdoesnotsupportRetroAchievementshashofSystems",
            "'Simple Launcher' does not support RetroAchievements hash of systems Grouped by Folder.");
        var pleaseedittheSystemsettingsanddisablethe = _resourceProvider.GetString(
            "pleaseedittheSystemsettingsanddisablethe",
            "Please edit the system settings and disable the 'Group Files by Folder' option.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{simpleLauncherdoesnotsupportRetroAchievementshashofSystems}\n\n" +
                                             $"{pleaseedittheSystemsettingsanddisablethe}", error);
    }

    /// <summary>
    ///     Displays a prompt asking whether to scan the game path for RetroAchievements
    ///     compatibility when no hash scan result exists for the selected system.
    /// </summary>
    public Task<CoreMessageBoxResult> ScanGamePathForRetroAchievementsMessageBoxAsync()
    {
        var weNeedToScanYourGamePath = _resourceProvider.GetString("WeNeedToScanYourGamePath",
            "We need to scan your game path to see what game is compatible with RetroAchievements.");
        var retroAchievements = _resourceProvider.GetString("RetroAchievements", "RetroAchievements");
        return _messageDialog.ShowAsync(weNeedToScanYourGamePath, retroAchievements, CoreMessageBoxButton.YesNo,
            CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error that the current processor architecture is unsupported.
    /// </summary>
    public Task UnsupportedArchitectureMessageBoxAsync()
    {
        var simpleLauncherdoesnotsupportthecurrentprocessorarchitecture = _resourceProvider.GetString(
            "SimpleLauncherdoesnotsupportthecurrentprocessorarchitecture",
            "'Simple Launcher' does not support the current processor architecture. We only support 64-bit (x64) or ARM64. The application will now close.");
        var unsupportedArchitecture =
            _resourceProvider.GetString("UnsupportedArchitecture", "Unsupported Architecture");
        return _messageDialog.ShowErrorAsync(simpleLauncherdoesnotsupportthecurrentprocessorarchitecture,
            unsupportedArchitecture);
    }

    /// <summary>
    ///     Displays an error when the 7z DLL is missing, with an option to reinstall the application.
    /// </summary>
    public async Task SevenZipDllNotFoundMessageBoxAsync()
    {
        var the7Zdllismissingfromtheapplicationfolder = _resourceProvider.GetString(
            "The7zdllismissingfromtheapplicationfolder", "The 7z dll is missing from the application folder!");
        var doyouwanttoreinstallSimpleLauncher = _resourceProvider.GetString("DoyouwanttoreinstallSimpleLauncher",
            "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var error = _resourceProvider.GetString("Error", "Error");

        var reinstall = await _messageDialog.ShowYesNoAsync(
            $"{the7Zdllismissingfromtheapplicationfolder}\n\n{doyouwanttoreinstallSimpleLauncher}", error);

        if (reinstall)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var pleasereinstallSimpleLauncher = _resourceProvider.GetString("PleasereinstallSimpleLauncher",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");
            await _messageDialog.ShowErrorAsync(pleasereinstallSimpleLauncher, error);

            Application.Current?.Shutdown();
        }
    }

    /// <summary>
    ///     Displays an error when the 7-Zip library fails to initialize, with an option to reinstall the application.
    /// </summary>
    public async Task FailedToInitializeSevenZipMessageBoxAsync()
    {
        var anunexpectederroroccurredwhileinitializingthe7Ziplibrary = _resourceProvider.GetString(
            "Anunexpectederroroccurredwhileinitializingthe7Ziplibrary",
            "An unexpected error occurred while initializing the 7-Zip library.");
        var doyouwanttoreinstallSimpleLauncher = _resourceProvider.GetString("DoyouwanttoreinstallSimpleLauncher",
            "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var error = _resourceProvider.GetString("Error", "Error");

        var reinstall = await _messageDialog.ShowYesNoAsync(
            $"{anunexpectederroroccurredwhileinitializingthe7Ziplibrary}\n\n{doyouwanttoreinstallSimpleLauncher}",
            error);

        if (reinstall)
        {
            _reinstallSimpleLauncher.StartUpdaterAndShutdownAsync();
        }
        else
        {
            var pleasereinstallSimpleLauncher = _resourceProvider.GetString("PleasereinstallSimpleLauncher",
                "Please reinstall 'Simple Launcher' manually to fix the issue.");
            await _messageDialog.ShowErrorAsync(pleasereinstallSimpleLauncher, error);

            Application.Current?.Shutdown();
        }
    }

    /// <summary>
    ///     Displays an error when file extraction fails after download, with an option to open the temp folder.
    /// </summary>
    public async Task ShowExtractionFailedMessageBoxAsync(string tempFolderPath)
    {
        var extractionFailedTitle = _resourceProvider.GetString("ExtractionFailedTitle", "Extraction Failed");
        var extractionFailedMessage = _resourceProvider.GetString("ExtractionFailedMessage",
            "The file was downloaded successfully, but automatic extraction failed. This can happen if an antivirus program is scanning or locking the file.");
        var openTempFolderQuestion = _resourceProvider.GetString("OpenTempFolderQuestion",
            "Would you like to open the temporary folder to inspect the file?");
        var result = await _messageDialog.ShowYesNoAsync($"{extractionFailedMessage}\n\n{openTempFolderQuestion}",
            extractionFailedTitle);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempFolderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                var errorOpeningFolderTitle =
                    _resourceProvider.GetString("ErrorOpeningFolderTitle", "Error Opening Folder");
                var errorOpeningFolderMessage = _resourceProvider.GetString("ErrorOpeningFolderMessage",
                    "Could not open the temporary folder.");
                await _messageDialog.ShowErrorAsync(errorOpeningFolderMessage, errorOpeningFolderTitle);
                _logger.Error(ex, $"Failed to open temp folder: {tempFolderPath}");
            }
        }
    }

    /// <summary>
    ///     Displays a download failure message when the temporary file is locked, with an option to open the temp folder.
    /// </summary>
    public async Task ShowDownloadFileLockedMessageBoxAsync(string tempFolderPath)
    {
        var downloadFailedTitle = _resourceProvider.GetString("DownloadFailedTitle", "Download Failed");
        var downloadFileLockedMessage = _resourceProvider.GetString("DownloadFileLockedMessage",
            "The download could not be completed because the temporary file is locked by another process (e.g., antivirus software).");
        var openTempFolderQuestion = _resourceProvider.GetString("OpenTempFolderQuestion",
            "Would you like to open the temporary folder to inspect the file?");
        var result = await _messageDialog.ShowYesNoAsync($"{downloadFileLockedMessage}\n\n{openTempFolderQuestion}",
            downloadFailedTitle);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempFolderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                var errorOpeningFolderTitle =
                    _resourceProvider.GetString("ErrorOpeningFolderTitle", "Error Opening Folder");
                var errorOpeningFolderMessage = _resourceProvider.GetString("ErrorOpeningFolderMessage",
                    "Could not open the temporary folder.");
                await _messageDialog.ShowErrorAsync(errorOpeningFolderMessage, errorOpeningFolderTitle);
                _logger.Error(ex, $"Failed to open temp folder: {tempFolderPath}");
            }
        }
    }

    /// <summary>
    ///     Displays a custom game launch error message with an option to open the error log.
    /// </summary>
    public async Task ShowCustomMessageBoxAsync(string message, string launchError, string? logPath)
    {
        var therewasanerrorlaunchingtheselected = _resourceProvider.GetString("Therewasanerrorlaunchingtheselected",
            "There was an error launching the selected game.");
        var dowanttoopenthefileerroruserlog = _resourceProvider.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");

        var result = await _messageDialog.ShowYesNoAsync($"{therewasanerrorlaunchingtheselected}\n\n" +
                                                         $"{message}\n\n" +
                                                         $"{dowanttoopenthefileerroruserlog}", launchError);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                var thefileerroruserlogwasnotfound = _resourceProvider.GetString("Thefileerroruserlogwasnotfound",
                    "The file 'error_user.log' was not found!");

                await _messageDialog.ShowErrorAsync(thefileerroruserlogwasnotfound, launchError);
            }
        }
    }

    /// <summary>
    ///     Displays a warning prompting the user to enter valid search terms.
    /// </summary>
    public Task EnterValidSearchTermsMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("EnterValidSearchTerms", "Please enter valid search terms.");
        var title = _resourceProvider.GetString("InvalidSearch", "Invalid Search");
        return _messageDialog.ShowWarningAsync(message, title);
    }

    /// <summary>
    ///     Displays a notification that the operation was cancelled.
    /// </summary>
    public Task OperationCancelledMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("OperationCancelledMessage", "The operation was cancelled.");
        var title = _resourceProvider.GetString("OperationCancelled", "Operation Cancelled");
        return _messageDialog.ShowInfoAsync(message, title);
    }

    /// <summary>
    ///     Displays a confirmation dialog asking whether to cancel processing and close.
    /// </summary>
    public Task<CoreMessageBoxResult> DoYouWantToCancelAndCloseMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("ProcessingStillRunningMessage",
            "Processing is still running. Do you want to cancel and close?");
        var title = _resourceProvider.GetString("ConfirmClose", "Confirm Close");
        return _messageDialog.ShowAsync(message, title, CoreMessageBoxButton.YesNo, CoreMessageBoxImage.Question);
    }

    /// <summary>
    ///     Displays an error message when the browser cannot be opened for AI support.
    /// </summary>
    public Task CouldNotOpenBrowserForAiSupportMessageBoxAsync()
    {
        var message =
            _resourceProvider.GetString("CouldnotopenbrowserforAIsupport", "Could not open browser for AI support.");
        var error = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message, error);
    }

    /// <summary>
    ///     Displays a warning when PowerShell execution policy restrictions prevent scanning Microsoft Store games.
    /// </summary>
    public Task PowerShellExecutionPolicyRestrictionsMessageBoxAsync()
    {
        var unabletoscanMicrosoftStoregames = _resourceProvider.GetString("UnabletoscanMicrosoftStoregames",
            "Unable to scan Microsoft Store games due to PowerShell execution policy restrictions.");
        var thisistypicallycausedbyGroupPolicy = _resourceProvider.GetString("ThisistypicallycausedbyGroupPolicy",
            "This is typically caused by Group Policy settings on corporate or managed PCs.");
        var simpleLaunchercannotperform = _resourceProvider.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var powerShellRestricted = _resourceProvider.GetString("PowerShellRestricted", "PowerShell Restricted");
        return _messageDialog.ShowWarningAsync($"{unabletoscanMicrosoftStoregames}\n\n" +
                                               $"{thisistypicallycausedbyGroupPolicy}\n\n" +
                                               $"{simpleLaunchercannotperform}", powerShellRestricted);
    }

    /// <summary>
    ///     Displays a warning when an ISO file cannot be mounted due to PowerShell execution policy restrictions.
    /// </summary>
    public Task UnabletomountIsOfileMessageBoxAsync()
    {
        var unabletomountIsOfile = _resourceProvider.GetString("UnabletomountISOfile",
            "Unable to mount ISO file due to PowerShell execution policy restrictions.");
        var thisistypicallycausedbyGroup = _resourceProvider.GetString("ThisistypicallycausedbyGroup",
            "This is typically caused by Group Policy settings on corporate or managed PCs.");
        var simpleLaunchercannotperform = _resourceProvider.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var powerShellRestricted = _resourceProvider.GetString("PowerShellRestricted", "PowerShell Restricted");
        return _messageDialog.ShowWarningAsync($"{unabletomountIsOfile}\n\n" +
                                               $"{thisistypicallycausedbyGroup}\n\n" +
                                               $"{simpleLaunchercannotperform}", powerShellRestricted);
    }

    /// <summary>
    ///     Displays a warning when an ISO file cannot be dismounted due to PowerShell execution policy restrictions.
    /// </summary>
    public Task UnabletoDismountIsOfileMessageBoxAsync()
    {
        var unabletodismountIsOfile = _resourceProvider.GetString("UnabletoDismountISOfile",
            "Unable to dismount ISO file due to PowerShell execution policy restrictions.");
        var thisistypicallycausedbyGroup = _resourceProvider.GetString("ThisistypicallycausedbyGroup",
            "This is typically caused by Group Policy settings on corporate or managed PCs.");
        var simpleLaunchercannotperform = _resourceProvider.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var powerShellRestricted = _resourceProvider.GetString("PowerShellRestricted", "PowerShell Restricted");
        return _messageDialog.ShowWarningAsync($"{unabletodismountIsOfile}\n\n" +
                                               $"{thisistypicallycausedbyGroup}\n\n" +
                                               $"{simpleLaunchercannotperform}", powerShellRestricted);
    }

    /// <summary>
    ///     Displays a warning when an application control policy blocks a file or link.
    /// </summary>
    public Task ApplicationControlPolicyBlockedMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("ApplicationControlPolicyBlockedFile",
            "An application control policy blocked this file or link.");
        var simpleLaunchercannotperform = _resourceProvider.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var securityPolicyBlocked = _resourceProvider.GetString("SecurityPolicyBlocked", "Security Policy Blocked");
        return _messageDialog.ShowWarningAsync($"{message}\n\n" +
                                               $"{simpleLaunchercannotperform}\n\n", securityPolicyBlocked);
    }

    /// <summary>
    ///     Displays a warning when an application control policy blocks a link, and copies the URL to the clipboard.
    /// </summary>
    public async Task ApplicationControlPolicyBlockedManualLinkMessageBoxAsync(string url)
    {
        var message = _resourceProvider.GetString("ApplicationControlPolicyBlockedFileManualLink",
            "An application control policy blocked this link.");
        var simpleLaunchercannotperform = _resourceProvider.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var theUrLwascopiedtotheclipboard = _resourceProvider.GetString("TheURLwascopiedtotheclipboard",
            "The URL was copied to the clipboard for your convenience. You can paste it into your browser.");
        var securityPolicyBlocked = _resourceProvider.GetString("SecurityPolicyBlocked", "Security Policy Blocked");
        await _messageDialog.ShowWarningAsync($"{message}\n\n" +
                                              $"{simpleLaunchercannotperform}\n\n" +
                                              $"{theUrLwascopiedtotheclipboard}", securityPolicyBlocked);
        Clipboard.SetText(url); // Copy URL to clipboard
    }

    /// <summary>
    ///     Displays a warning prompting the user to enter RetroAchievements credentials before configuring an emulator.
    /// </summary>
    public Task EnterYourRetroAchievementsUsernameMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("PleaseenteryourRetroAchievements",
            "Please enter your RetroAchievements username, API key, and password before configuring an emulator.");
        var message2 = _resourceProvider.GetString("CredentialsRequired", "Credentials Required");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after an emulator has been configured for RetroAchievements.
    /// </summary>
    public Task EmulatorConfiguredSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Emulatorconfiguredsuccessfullyfor",
            "Emulator configured successfully for RetroAchievements!");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when emulator configuration fails due to a missing or read-only config file.
    /// </summary>
    public Task FailedToConfigureTheEmulatorMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Failedtoconfiguretheemulator",
            "Failed to configure the emulator. The configuration file might be missing, in an unexpected location, or read-only.");
        var message2 = _resourceProvider.GetString("ConfigurationFailed", "Configuration Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when an exception occurs while configuring an emulator.
    /// </summary>
    public Task AnErrorOccurredWhileConfiguringTheEmulatorMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Anerroroccurredwhileconfiguringtheemulator",
            "An error occurred while configuring the emulator.");
        var message2 = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when logging in to RetroAchievements fails.
    /// </summary>
    public Task FailedToLoginToRetroAchievementsMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtologintoRetroAchievements",
            "Failed to log in to RetroAchievements. Please check your username and password.");
        var message2 = _resourceProvider.GetString("LoginFailed", "Login Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when system.xml is locked or inaccessible by another process.
    /// </summary>
    public Task FileSystemXmlIsLockedMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Thefilesystemxmlislocked",
            "The file 'system.xml' is locked or inaccessible by another process.");
        var message2 = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning when injecting MAME configuration fails.
    /// </summary>
    public Task FailedToInjectMameConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectMAMEconfiguration",
            "Failed to inject MAME configuration. The error has been logged. Please check the emulator path and try again.");
        var message2 = _resourceProvider.GetString("InjectionError", "Injection Error");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after MAME configuration has been injected.
    /// </summary>
    public Task MameConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("MAMEconfigurationinjectedsuccessfully",
            "MAME configuration injected successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning when injecting MAME configuration fails (alternate message).
    /// </summary>
    public Task FailedtoinjectMamEconfiguration2MessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectMAMEconfigurationTheerror",
            "Failed to inject MAME configuration. The error has been logged.");
        var message2 = _resourceProvider.GetString("InjectionError", "Injection Error");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a prompt to locate the MAME emulator executable.
    /// </summary>
    public Task MameEmulatorPathNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("MAMEemulatorpathnotfoundPleaseselect",
            "MAME emulator path not found. Please select 'mame.exe' or 'mame64.exe' to apply these settings.");
        var message2 = _resourceProvider.GetString("EmulatorRequired", "Emulator Required");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a prompt to locate the RetroArch emulator executable.
    /// </summary>
    public Task RetroArchemulatorpathnotfoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("RetroArchemulatorpathnotfoundPlease",
            "RetroArch emulator path not found. Please select 'retroarch.exe' to apply these settings.");
        var message2 = _resourceProvider.GetString("EmulatorRequired", "Emulator Required");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning when injecting RetroArch configuration fails.
    /// </summary>
    public Task FailedtoinjectRetroArchconfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectRetroArchconfigurationTheerror",
            "Failed to inject RetroArch configuration. The error has been logged. Please check the emulator path and try again.");
        var message2 = _resourceProvider.GetString("InjectionError", "Injection Error");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after RetroArch configuration has been injected.
    /// </summary>
    public Task RetroArchConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("RetroArchconfigurationinjectedsuccessfully",
            "RetroArch configuration injected successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning when injecting RetroArch configuration fails (alternate message).
    /// </summary>
    public Task FailedtoinjectRetroArchconfiguration2MessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectRetroArchconfigurationTheerrorhas",
            "Failed to inject RetroArch configuration. The error has been logged.");
        var message2 = _resourceProvider.GetString("InjectionError", "Injection Error");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a prompt to locate the Xenia emulator executable.
    /// </summary>
    public Task XeniaemulatorpathnotfoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Xeniaemulatorpathnotfound",
            "Xenia emulator path not found. Please select 'xenia.exe' or 'xenia_canary.exe' to apply these settings.");
        var message2 = _resourceProvider.GetString("EmulatorRequired", "Emulator Required");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning when injecting Xenia configuration fails.
    /// </summary>
    public Task FailedtoinjectXeniaconfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectXeniaconfigurationTheerrorPleasecheck",
            "Failed to inject Xenia configuration. The error has been logged. Please check the emulator path and try again.");
        var message2 = _resourceProvider.GetString("InjectionError", "Injection Error");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Xenia configuration has been injected.
    /// </summary>
    public Task XeniaconfigurationinjectedsuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Xeniaconfigurationinjectedsuccessfully",
            "Xenia configuration injected successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning when injecting Xenia configuration fails (alternate message).
    /// </summary>
    public Task FailedtoinjectXeniaconfiguration2MessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectXeniaconfigurationTheerror",
            "Failed to inject Xenia configuration. The error has been logged.");
        var message2 = _resourceProvider.GetString("InjectionError", "Injection Error");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning prompting the user to enter RetroAchievements username and password first.
    /// </summary>
    public Task EnterUsernamePasswordMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("EnterUsernamePassword",
            "Please enter your RetroAchievements username and password first.");
        var message2 = _resourceProvider.GetString("MissingInformation", "Missing Information");
        return _messageDialog.ShowWarningAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Ares emulator cannot be found.
    /// </summary>
    public Task AresemulatornotfoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Aresemulatornotfound",
            "Ares emulator not found. Please locate 'ares.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Daphne settings have been saved.
    /// </summary>
    public Task DaphnesettingssavedsuccessfullyMessageBoxAsync()
    {
        var message1 =
            _resourceProvider.GetString("Daphnesettingssavedsuccessfully", "Daphne settings saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after PCSX2 settings have been saved.
    /// </summary>
    public Task Pcsx2SettingssavedMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("PCSX2settingssaved", "PCSX2 settings saved.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a warning when PCSX2 configuration injection fails due to permission issues.
    /// </summary>
    public Task Pcsx2ConfigurationInjectionPermissionErrorMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        var message1 = _resourceProvider.GetString("Pcsx2ConfigPermissionError1",
            "Failed to inject PCSX2 configuration. The emulator is installed in a protected system directory.");
        var message2 = _resourceProvider.GetString("Pcsx2ConfigPermissionError2",
            "The configuration file could not be modified due to insufficient permissions.");
        var message3 = _resourceProvider.GetString("Pcsx2ConfigPermissionError3", "To fix this, either:");
        var message4 = _resourceProvider.GetString("Pcsx2ConfigPermissionError4",
            "1. Run Simple Launcher as administrator, or");
        var message5 = _resourceProvider.GetString("Pcsx2ConfigPermissionError5",
            "2. Install PCSX2 in a user directory (e.g., C:\\Users\\YourName\\PCSX2)");
        var message6 = _resourceProvider.GetString("Pcsx2ConfigPermissionError6",
            "The game will launch with the emulator's default settings.");
        return _messageDialog.ShowWarningAsync($"{message1}\n\n" +
                                               $"{message2}\n\n" +
                                               $"{message3}\n" +
                                               $"{message4}\n" +
                                               $"{message5}\n\n" +
                                               $"{message6}", title);
    }

    /// <summary>
    ///     Displays a success message after emulator settings have been saved.
    /// </summary>
    public Task SettingsSavedMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("SettingsSaved", "Settings saved.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Cemu emulator cannot be found.
    /// </summary>
    public Task CemuEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Cemuemulatornotfound",
            "Cemu emulator not found. Please locate 'Cemu.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Ares configuration fails.
    /// </summary>
    public Task FailedtoinjectAresconfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectAresconfiguration",
            "Failed to inject Ares configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Cemu configuration has been saved.
    /// </summary>
    public Task CemuConfigurationSavedMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("CemuConfigurationSaved", "Cemu configuration saved.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Flycast emulator cannot be found.
    /// </summary>
    public Task FlycastEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Flycastemulatornotfound",
            "Flycast emulator not found. Please locate 'flycast.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Ares configuration has been saved.
    /// </summary>
    public Task AresConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("AresConfigurationSavedSuccessfully",
            "Ares configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Ares configuration fails.
    /// </summary>
    public Task FailedToSaveAresConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveAresConfiguration",
            "Failed to save Ares configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Flycast configuration fails.
    /// </summary>
    public Task FailedToInjectFlycastConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToInjectFlycastConfiguration",
            "Failed to inject Flycast configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Flycast configuration has been saved.
    /// </summary>
    public Task FlycastConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FlycastConfigurationSavedSuccessfully",
            "Flycast configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Dolphin emulator cannot be found.
    /// </summary>
    public Task DolphinEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("DolphinEmulatorNotFound",
            "Dolphin emulator not found. Please locate 'Dolphin.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Flycast configuration fails.
    /// </summary>
    public Task FailedToSaveFlycastConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveFlycastConfiguration",
            "Failed to save Flycast configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Dolphin configuration fails.
    /// </summary>
    public Task FailedToInjectDolphinConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToInjectDolphinConfiguration",
            "Failed to inject Dolphin configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Dolphin configuration has been saved.
    /// </summary>
    public Task DolphinConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("DolphinConfigurationSavedSuccessfully",
            "Dolphin configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Dolphin configuration fails.
    /// </summary>
    public Task FailedToSaveDolphinConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveDolphinConfiguration",
            "Failed to save Dolphin configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the SEGA Model 2 emulator cannot be found.
    /// </summary>
    public Task SegaModel2EmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("SEGAModel2EmulatorNotFound",
            "SEGA Model 2 emulator not found. Please locate 'emulator.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting SEGA Model 2 configuration fails.
    /// </summary>
    public Task FailedToInjectSegaModel2ConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToInjectSEGAModel2Configuration",
            "Failed to inject SEGA Model 2 configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after SEGA Model 2 configuration has been saved.
    /// </summary>
    public Task SegaModel2ConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("SEGAModel2ConfigurationSavedSuccessfully",
            "SEGA Model 2 configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Blastem emulator cannot be found.
    /// </summary>
    public Task BlastemEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("BlastememulatornotfoundPleaselocate",
            "Blastem emulator not found. Please locate 'blastem.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Blastem configuration fails.
    /// </summary>
    public Task FailedToInjectBlastemConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToInjectBlastemConfiguration",
            "Failed to inject Blastem configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Blastem configuration has been saved.
    /// </summary>
    public Task BlastemConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("BlastemConfigurationSavedSuccessfully",
            "Blastem configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving SEGA Model 2 configuration fails.
    /// </summary>
    public Task FailedToSaveSegaModel2ConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveSEGAModel2Configuration",
            "Failed to save SEGA Model 2 configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Blastem configuration fails.
    /// </summary>
    public Task FailedToSaveBlastemConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveBlastemConfiguration",
            "Failed to save Blastem configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the RPCS3 emulator cannot be found.
    /// </summary>
    public Task Rpcs3EmulatorNotFoundPleaseLocateMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("RPCS3emulatornotfoundPleaselocate",
            "RPCS3 emulator not found. Please locate 'rpcs3.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting RPCS3 configuration fails.
    /// </summary>
    public Task FailedToInjectRpcs3ConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToInjectRPCS3Configuration",
            "Failed to inject RPCS3 configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after RPCS3 configuration has been saved.
    /// </summary>
    public Task Rpcs3ConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("RPCS3ConfigurationSavedSuccessfully",
            "RPCS3 configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving RPCS3 configuration fails.
    /// </summary>
    public Task FailedToSaveRpcs3ConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtosaveRPCS3configurationPleasecheck",
            "Failed to save RPCS3 configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Stella emulator cannot be found.
    /// </summary>
    public Task StellaEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("StellaemulatornotfoundPleaselocate",
            "Stella emulator not found. Please locate 'stella.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Stella configuration fails.
    /// </summary>
    public Task FailedToInjectStellaConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectStellaconfiguration",
            "Failed to inject Stella configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Supermodel emulator cannot be found.
    /// </summary>
    public Task SupermodelEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("SupermodelEmulatorNotFound",
            "Supermodel emulator not found. Please locate 'Supermodel.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Stella configuration has been saved.
    /// </summary>
    public Task StellaConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("StellaConfigurationSavedSuccessfully",
            "Stella configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Supermodel configuration fails.
    /// </summary>
    public Task FailedToInjectSupermodelConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectSupermodelconfiguration",
            "Failed to inject Supermodel configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Stella configuration fails.
    /// </summary>
    public Task FailedToSaveStellaConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveStellaConfiguration",
            "Failed to save Stella configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Supermodel configuration has been saved.
    /// </summary>
    public Task SupermodelConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Supermodelconfigurationsavedsuccessfully",
            "Supermodel configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Supermodel configuration fails.
    /// </summary>
    public Task FailedToSaveSupermodelConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveSupermodelConfiguration",
            "Failed to save Supermodel configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Mednafen emulator cannot be found.
    /// </summary>
    public Task MednafenEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Mednafenemulatornotfound",
            "Mednafen emulator not found. Please locate 'mednafen.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Mesen emulator cannot be found.
    /// </summary>
    public Task MesenEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("Mesenemulatornotfound",
            "Mesen emulator not found. Please locate 'Mesen.exe'.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Mednafen configuration fails.
    /// </summary>
    public Task FailedToInjectMednafenConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectMednafenconfiguration",
            "Failed to inject Mednafen configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Mesen configuration fails.
    /// </summary>
    public Task FailedToInjectMesenConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedtoinjectMesenconfiguration",
            "Failed to inject Mesen configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the DuckStation emulator cannot be found.
    /// </summary>
    public Task DuckStationEmulatorNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("DuckStationemulatornotfound",
            "DuckStation emulator not found. Please locate the DuckStation executable.");
        var message2 = _resourceProvider.GetString("EmulatorNotFound", "Emulator Not Found");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Mednafen configuration has been saved.
    /// </summary>
    public Task MednafenConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("MednafenConfigurationSavedSuccessfully",
            "Mednafen configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Mednafen configuration fails.
    /// </summary>
    public Task FailedToSaveMednafenConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveMednafenConfiguration",
            "Failed to save Mednafen configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting DuckStation configuration fails.
    /// </summary>
    public Task FailedToInjectDuckStationConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToInjectDuckStationConfiguration",
            "Failed to inject DuckStation configuration. Please check file permissions and try again.");
        var message2 = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after DuckStation configuration has been saved.
    /// </summary>
    public Task DuckStationConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("DuckStationConfigurationSavedSuccessfully",
            "DuckStation configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Mesen configuration fails.
    /// </summary>
    public Task FailedToSaveMesenConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveMesenConfiguration",
            "Failed to save Mesen configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving DuckStation configuration fails.
    /// </summary>
    public Task FailedToSaveDuckStationConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveDuckStationConfiguration",
            "Failed to save DuckStation configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Mesen configuration has been saved.
    /// </summary>
    public Task MesenConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("MesenConfigurationSavedSuccessfully",
            "Mesen configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting Yumir configuration fails.
    /// </summary>
    public Task FailedToInjectYumirConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveYumirConfiguration",
            "Failed to save Yumir configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Yumir configuration has been saved.
    /// </summary>
    public Task YumirConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("YumirConfigurationSavedSuccessfully",
            "Yumir configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after Raine configuration has been injected.
    /// </summary>
    public Task RaineSettingsSavedAndInjectedMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("RaineSettingsSavedAndInjectedSuccessfully",
            "Raine configuration has been successfully injected.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Raine executable cannot be found.
    /// </summary>
    public Task RaineExecutableNotFoundMessageBoxAsync()
    {
        var message1 =
            _resourceProvider.GetString("RaineConfig_PathNotFound", "Raine executable not found. Please select it.");
        var message2 = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the Yumir executable cannot be found.
    /// </summary>
    public Task YumirEmulatorNotFoundMessageBoxAsync()
    {
        var message1 =
            _resourceProvider.GetString("YumirConfig_PathNotFound", "Yumir executable not found. Please select it.");
        var message2 = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when the ReDream executable cannot be found.
    /// </summary>
    public Task ReDreamEmulatorPathNotFoundMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("ReDreamConfig_PathNotFound",
            "ReDream executable not found. Please select it.");
        var message2 = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when injecting ReDream configuration fails.
    /// </summary>
    public Task FailedToInjectReDreamConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveReDreamConfiguration",
            "Failed to save ReDream configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays a success message after ReDream configuration has been injected.
    /// </summary>
    public Task ReDreamConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("ReDreamConfigurationSavedSuccessfully",
            "ReDream configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when a game fails to launch due to a DEP violation.
    /// </summary>
    public Task CouldNotLaunchGameDueToDepViolationMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message = _resourceProvider.GetString("CouldNotLaunchGameDueToDepViolation",
            "The game failed to launch due to a DEP (Data Execution Prevention) violation.");
        var message2 = _resourceProvider.GetString("CouldNotLaunchGameDueToDepViolation2",
            "This is a Windows security feature that prevents programs from executing code in protected memory regions.");
        var message3 = _resourceProvider.GetString("CouldNotLaunchGameDueToDepViolation3",
            "Ensure you're using the latest emulator version with improved security compatibility.");
        var message4 = _resourceProvider.GetString("CouldNotLaunchGameDueToDepViolation4",
            "You can also try to switch to a different emulator or core.");
        return _messageDialog.ShowErrorAsync($"{message}\n\n" +
                                             $"{message2}\n\n" +
                                             $"{message3}\n\n" +
                                             $"{message4}", title);
    }

    /// <summary>
    ///     Displays a MAME ROM set error with an option to visit the PleasureDome website.
    /// </summary>
    public async Task MameRomSetErrorMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("ROMFilesNotFound", "ROM Files Not Found");
        var message1 = _resourceProvider.GetString("MameRomSetError1",
            "MAME emulator could not find required files to launch this game.");
        var message2 = _resourceProvider.GetString("MameRomSetError2x",
            "MAME is very restrictive about the filename of the game.");
        var message3 =
            _resourceProvider.GetString("MameRomSetError3", "Please ensure you are running a compatible ROM set.");
        var message4 = _resourceProvider.GetString("MameRomSetError4",
            "Would you like to visit the PleasureDome website to download a compatible ROM set?");
        var result = await _messageDialog.ShowYesNoAsync($"{message1}\n\n" +
                                                         $"{message2}\n\n" +
                                                         $"{message3}\n\n" +
                                                         $"{message4}", title);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _configuration.GetValue<string>("Urls:PleasureDomeWebsite") ??
                               "https://pleasuredome.github.io/pleasuredome/index.html",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await _messageDialog.ShowErrorAsync($"Could not open browser: {ex.Message}", "Error");
                _logger.Error(ex, "Could not open browser");
            }
        }
    }

    /// <summary>
    ///     Displays a MAME unknown system error with an option to visit the PleasureDome website.
    /// </summary>
    public async Task MameUnknownSystemErrorMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("UnknownSystemError", "Unknown System Error");
        var message1 = _resourceProvider.GetString("MameUnknownSystemError1",
            "MAME emulator could not find a matching compatible system to launch.");
        var message2 = _resourceProvider.GetString("MameUnknownSystemError2",
            "MAME is very restrictive about the filename of the game.");
        var message3 = _resourceProvider.GetString("MameUnknownSystemError3",
            "The filename of your game must match the expected filename to run on MAME.");
        var message4 = _resourceProvider.GetString("MameUnknownSystemError4",
            "Please ensure you are running a compatible ROM set.");
        var message5 = _resourceProvider.GetString("MameUnknownSystemError5",
            "Would you like to visit the PleasureDome website to download a compatible ROM set?");
        var result = await _messageDialog.ShowYesNoAsync($"{message1}\n\n" +
                                                         $"{message2}\n\n" +
                                                         $"{message3}\n\n" +
                                                         $"{message4}\n\n" +
                                                         $"{message5}", title);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _configuration.GetValue<string>("Urls:PleasureDomeWebsite") ??
                               "https://pleasuredome.github.io/pleasuredome/index.html",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await _messageDialog.ShowErrorAsync($"Could not open browser: {ex.Message}", "Error");
                _logger.Error(ex, "Could not open browser");
            }
        }
    }

    /// <summary>
    ///     Displays an error when MAME cannot load an image file, with an option to visit the PleasureDome website.
    /// </summary>
    public async Task MameUnableToLoadImageMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("UnableToLoadImage", "Unable to load image");
        var message1 = _resourceProvider.GetString("MameUnableToLoadImageError1",
            "MAME emulator could not load the image file.");
        var message2 = _resourceProvider.GetString("MameUnableToLoadImageError2",
            "MAME is very restrictive about the filename of the game.");
        var message3 = _resourceProvider.GetString("MameUnableToLoadImageError3",
            "The filename of your game must match the expected filename to run on MAME.");
        var message4 = _resourceProvider.GetString("MameUnableToLoadImageError4",
            "Please ensure you are running a compatible ROM set.");
        var message5 = _resourceProvider.GetString("MameUnableToLoadImageError5",
            "Would you like to visit the PleasureDome website to download a compatible ROM set?");
        var result = await _messageDialog.ShowYesNoAsync($"{message1}\n\n" +
                                                         $"{message2}\n\n" +
                                                         $"{message3}\n\n" +
                                                         $"{message4}\n\n" +
                                                         $"{message5}", title);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _configuration.GetValue<string>("Urls:PleasureDomeWebsite") ??
                               "https://pleasuredome.github.io/pleasuredome/index.html",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await _messageDialog.ShowErrorAsync($"Could not open browser: {ex.Message}", "Error");
                _logger.Error(ex, "Could not open browser");
            }
        }
    }

    /// <summary>
    ///     Displays an error that the Ootake emulator does not support CHD, ISO, or CUE/BIN files.
    /// </summary>
    public Task OotakeDoesNotSupportImageFilesMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message = _resourceProvider.GetString("OotakeemulatordoesnotsupportCHD",
            "Ootake emulator does not support CHD, ISO, CUE/BIN files.");
        return _messageDialog.ShowErrorAsync(message, title);
    }

    /// <summary>
    ///     Displays an error that the Geolith libretro DLL does not support compressed files, with an option to visit a wiki
    ///     page.
    /// </summary>
    public async Task GeolithDoesNotSupportCompressedFilesMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message1 = _resourceProvider.GetString("GeolithLibretroDllDoesNotSupportZIP1",
            "'geolith_libretro.dll' does not support ZIP, 7Z or RAR files.");
        var message2 =
            _resourceProvider.GetString("GeolithLibretroDllDoesNotSupportZIP2", "It only support NEO files.");
        var message3 = _resourceProvider.GetString("GeolithLibretroDllDoesNotSupportZIP3",
            "Please ensure you are running a compatible ROM set.");
        var message4 = _resourceProvider.GetString("GeolithLibretroDllDoesNotSupportZIP4",
            "Would you like to visit the url 'wiki.terraonion.com' to get more info about that?");
        var result = await _messageDialog.ShowYesNoAsync($"{message1}\n\n" +
                                                         $"{message2}\n\n" +
                                                         $"{message3}\n\n" +
                                                         $"{message4}", title);

        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://wiki.terraonion.com/index.php/Neobuilder_Guide",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await _messageDialog.ShowErrorAsync($"Could not open browser: {ex.Message}", "Error");
                _logger.Error(ex, "Could not open browser");
            }
        }
    }

    /// <summary>
    ///     Displays an error that the RetroArch parameter should contain -L to point to the desired core.
    /// </summary>
    public Task RetroArchParameterShouldContainLMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message = _resourceProvider.GetString("RetroArchParameterShouldContainL",
            "The RetroArch parameter should contain -L to properly point to the desired core.");
        var message2 = _resourceProvider.GetString("EditthissysteminExpertModeandfixtheparameter",
            "Edit this system in 'Expert Mode' and fix the parameter field for this emulator.");
        return _messageDialog.ShowErrorAsync($"{message}\n\n" +
                                             $"{message2}", title);
    }

    /// <summary>
    ///     Displays a RetroArch parameter issue with troubleshooting tips and an option to open the error log.
    /// </summary>
    public async Task RetroArchParameterIssueMessageBoxAsync(string? logPath)
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message = _resourceProvider.GetString("RetroArchParameterIssue", "RetroArch could not launch your game.");
        var message2 = _resourceProvider.GetString("RetroArchParameterIssue2",
            "99% of the launch failures are due to incorrect parameters.");
        var message3 = _resourceProvider.GetString("RetroArchParameterIssue3",
            "Go back to 'Expert Mode' and double-check the parameter field for this emulator. Double-check the path to the desired core. Read the recommendations from the 'Simple Launcher' developer for the specific system.");
        var message4 = _resourceProvider.GetString("RetroArchParameterIssue4",
            "Check the core requirements to run it. Some cores require a BIOS file to work. Read the core documentation to figure out what the requirements are for that specific core.");
        var doyouwanttoopenthefileerroruserlog = _resourceProvider.GetString("Doyouwanttoopenthefileerroruserlog",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var result = await _messageDialog.ShowYesNoAsync($"{message}\n\n" +
                                                         $"{message2}\n\n" +
                                                         $"{message3}\n\n" +
                                                         $"{message4}\n\n" +
                                                         $"{doyouwanttoopenthefileerroruserlog}", title);
        if (result)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PathHelper.ResolveActualLogFile(logPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Information(ex,
                    "Failed to open the error log file from a message box. The file may not exist");
                // Notify user
                var thefileerroruserlogwas = _resourceProvider.GetString("Thefileerroruserlogwas",
                    "The file 'error_user.log' was not found!");
                await _messageDialog.ShowErrorAsync(thefileerroruserlogwas, title);
            }
        }
    }

    /// <summary>
    ///     Displays an error when special characters in the file path prevent RetroArch from launching.
    /// </summary>
    public Task RetroArchSpecialCharactersInPathMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message = _resourceProvider.GetString("RetroArchSpecialCharactersInPath1",
            "The emulator could not launch the game because the file path contains special characters (for example: ´, `, ~, !, ?).");
        var message2 = _resourceProvider.GetString("RetroArchSpecialCharactersInPath2",
            "RetroArch cannot create its required folders in paths with these characters.");
        var message3 = _resourceProvider.GetString("RetroArchSpecialCharactersInPath3",
            "To fix this, please move your emulator and your game files to a folder that uses only standard letters and numbers, such as C:\\Games\\.");
        return _messageDialog.ShowErrorAsync($"{message}\n\n" +
                                             $"{message2}\n\n" +
                                             $"{message3}", title);
    }

    /// <summary>
    ///     Displays a warning when Azahar configuration injection fails due to permission issues.
    /// </summary>
    public Task AzaharConfigurationInjectionPermissionErrorMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("InjectionFailed", "Injection Failed");
        var message1 = _resourceProvider.GetString("AzaharConfigPermissionError1",
            "Failed to inject Azahar configuration. The emulator is installed in a protected system directory.");
        var message2 = _resourceProvider.GetString("AzaharConfigPermissionError2",
            "The configuration file could not be modified due to insufficient permissions.");
        var message3 = _resourceProvider.GetString("AzaharConfigPermissionError3", "To fix this, either:");
        var message4 = _resourceProvider.GetString("AzaharConfigPermissionError4",
            "1. Run Simple Launcher as administrator, or");
        var message5 = _resourceProvider.GetString("AzaharConfigPermissionError5",
            "2. Install Azahar in a user directory (e.g., C:\\Users\\YourName\\Azahar)");
        var message6 = _resourceProvider.GetString("AzaharConfigPermissionError6",
            "The game will launch with the emulator's default settings.");
        return _messageDialog.ShowWarningAsync($"{message1}\n\n" +
                                               $"{message2}\n\n" +
                                               $"{message3}\n" +
                                               $"{message4}\n" +
                                               $"{message5}\n\n" +
                                               $"{message6}", title);
    }

    /// <summary>
    ///     Displays a success message after Azahar configuration has been saved.
    /// </summary>
    public Task AzaharConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("AzaharConfigurationSavedSuccessfully",
            "Azahar configuration saved successfully.");
        var message2 = _resourceProvider.GetString("Success", "Success");
        return _messageDialog.ShowInfoAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error message when saving Azahar configuration fails.
    /// </summary>
    public Task FailedToSaveAzaharConfigurationMessageBoxAsync()
    {
        var message1 = _resourceProvider.GetString("FailedToSaveAzaharConfiguration",
            "Failed to save Azahar configuration. Please check file permissions.");
        var message2 = _resourceProvider.GetString("SaveFailed", "Save Failed");
        return _messageDialog.ShowErrorAsync(message1, message2);
    }

    /// <summary>
    ///     Displays an error that the Xemu parameter should contain '-dvd_path'.
    /// </summary>
    public Task XemuParameterShouldContainDvdPathMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message = _resourceProvider.GetString("XemuParameterShouldContainDvdPath",
            "The Xemu parameter should contain '-dvd_path'.");
        var message2 = _resourceProvider.GetString("EditthissysteminExpertModeandfixtheparameter",
            "Edit this system in 'Expert Mode' and fix the parameter field for this emulator.");
        return _messageDialog.ShowErrorAsync($"{message}\n\n" +
                                             $"{message2}", title);
    }

    /// <summary>
    ///     Displays an error that the application cannot run from a temporary folder.
    /// </summary>
    public Task PleaseExtractApplicationFirstMessageBoxAsync()
    {
        var title = _resourceProvider.GetString("Error", "Error");
        var message = _resourceProvider.GetString("SimpleLaunchercannotrunfromatemporary",
            "'Simple Launcher' cannot run from a temporary folder.");
        var message2 = _resourceProvider.GetString("Pleaseextracttheapplicationtoapermanentfolder",
            "Please extract the application to a permanent folder before running it.");
        return _messageDialog.ShowErrorAsync($"{message}\n\n" +
                                             $"{message2}", title);
    }

    /// <summary>
    ///     Displays a generic error message when configuration injection fails.
    /// </summary>
    public Task InjectionFailedGenericMessageBoxAsync()
    {
        var errorMessage = _resourceProvider.GetString("Failedtoinjectconfiguration",
            "Failed to inject configuration. The error has been logged to the developer.");
        var errorTitle = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(errorMessage, errorTitle);
    }

    /// <summary>
    ///     Displays an error message when saving Daphne configuration fails.
    /// </summary>
    public Task DaphneConfigurationSaveFailedMessageBoxAsync()
    {
        var errorMessage = _resourceProvider.GetString("Failedtosaveconfiguration",
            "Failed to save configuration. The error has been logged to the developer.");
        var errorTitle = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync(errorMessage, errorTitle);
    }

    /// <summary>
    ///     Displays a warning when image download times out due to Cloudflare access issues.
    /// </summary>
    public Task ShowImageDownloadTimeoutMessageBoxAsync()
    {
        var simpleLauncherCouldNotDownloadImages = _resourceProvider.GetString("SimpleLauncherCouldNotDownloadImages",
            "Simple Launcher could not download images due to access issues to Cloudflare servers.");
        var thisMayBeDueToCountryFirewallRestrictions = _resourceProvider.GetString(
            "ThisMayBeDueToCountryFirewallRestrictions", "This may be due to country firewall restrictions.");
        var pleaseTryAgainBehindAVpn =
            _resourceProvider.GetString("PleaseTryAgainBehindAVpn", "Please try again behind a VPN.");
        var imageDownloadError = _resourceProvider.GetString("ImageDownloadError", "Image Download Error");

        return _messageDialog.ShowWarningAsync($"{simpleLauncherCouldNotDownloadImages}\n\n" +
                                               $"{thisMayBeDueToCountryFirewallRestrictions}\n\n" +
                                               $"{pleaseTryAgainBehindAVpn}", imageDownloadError);
    }

    /// <summary>
    ///     Displays a prompt asking the user to enter a system name before choosing an image.
    /// </summary>
    public Task SystemNameRequiredBeforeChoosingImageMessageBoxAsync()
    {
        var message = _resourceProvider.GetString("SystemNameRequiredBeforeChoosingImage",
            "Please enter a system name before choosing an image.");
        var title = _resourceProvider.GetString("SystemNameRequired", "System Name Required");
        return _messageDialog.ShowInfoAsync(message, title);
    }

    /// <summary>
    ///     Displays a warning when the selected image format is not supported.
    /// </summary>
    public Task InvalidImageFormatMessageBoxAsync()
    {
        var message =
            _resourceProvider.GetString("InvalidImageFormat", "Only PNG, JPG, and JPEG images are supported.");
        var title = _resourceProvider.GetString("InvalidImageFormatTitle", "Invalid Image Format");
        return _messageDialog.ShowWarningAsync(message, title);
    }

    /// <summary>
    ///     Displays an error message when copying the system image fails.
    /// </summary>
    public Task FailedToCopySystemImageMessageBoxAsync(string errorMessage)
    {
        var baseMessage = _resourceProvider.GetString("FailedToCopySystemImage", "Failed to copy the image:");
        var title = _resourceProvider.GetString("Error", "Error");
        return _messageDialog.ShowErrorAsync($"{baseMessage} {errorMessage}", title);
    }

    /// <summary>
    ///     Displays a generic warning message with the specified message text.
    /// </summary>
    public Task WarningMessageBoxAsync(string message)
    {
        var title = _resourceProvider.GetString("Warning", "Warning");
        return _messageDialog.ShowWarningAsync(message, title);
    }

    /// <summary>
    ///     Displays a custom error message with the specified message and title.
    /// </summary>
    public Task CustomErrorMessageBoxAsync(string message, string title)
    {
        return _messageDialog.ShowErrorAsync(message, title);
    }

    /// <summary>
    ///     Displays a custom yes/no question dialog and returns the user's response.
    /// </summary>
    public async Task<bool> CustomQuestionMessageBoxAsync(string title, string message)
    {
        var result = await _messageDialog.ShowYesNoAsync(message, title);
        return result;
    }

    /// <summary>
    ///     Displays a custom informational message with the specified title and message.
    /// </summary>
    /// <param name="title">The title of the message box.</param>
    /// <param name="message">The message text to display.</param>
    public Task CustomInfoMessageBoxAsync(string title, string message)
    {
        return _messageDialog.ShowInfoAsync(message, title);
    }

    /// <summary>
    ///     Displays a prompt asking whether Simple Launcher AI should suggest corrected launch parameters after a failed
    ///     launch.
    /// </summary>
    /// <returns>True if the user accepted; otherwise, false.</returns>
    public async Task<bool> AskAiToFixParametersMessageBoxAsync()
    {
        var askAiMessage = _resourceProvider.GetString("AskAiToFixParameters",
            "The launch failed. Would you like 'Simple Launcher AI' to suggest correct parameters for this emulator?");
        var pleaseWaitForAiResponse = _resourceProvider.GetString("pleaseWaitForAiResponse",
            "If you click Yes, please wait some seconds for the AI response.");
        var askAiTitle = _resourceProvider.GetString("AskAiToFixParametersTitle", "Simple Launcher AI");
        var result = await _messageDialog.ShowYesNoAsync($"{askAiMessage}\n\n" +
                                                         $"{pleaseWaitForAiResponse}", askAiTitle);
        return result;
    }
}