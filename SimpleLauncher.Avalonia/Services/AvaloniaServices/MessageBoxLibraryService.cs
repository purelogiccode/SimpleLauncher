using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services.QuitOrReinstall;
using SimpleLauncher.Avalonia.Views;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CheckPaths;

namespace SimpleLauncher.Avalonia.Services.AvaloniaServices;

public class MessageBoxLibraryService : IMessageBoxLibraryService
{
    private readonly IConfiguration _configuration;
    private readonly LocalizationService _localization;
    private readonly IWindowContext _ctx;

    public MessageBoxLibraryService(IWindowContext c, IConfiguration configuration,
        LocalizationService localization)
    {
        _ctx = c;
        _configuration = configuration;
        _localization = localization;
    }

    private Window? O => _ctx.PlatformWindow as Window;


    public async Task ListOfErrorsMessageBoxAsync(StringBuilder errorMessages)
    {
        if (O != null)
        {
            await ShowAsync(O, errorMessages.ToString(), _localization.GetString("Errors", "Errors"), MessageButtons.Ok,
                MessageIcon.Error);
        }
    }


    public Task FailedToInjectYumirConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveYumirConfiguration",
                "Failed to save Yumir configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task Pcsx2SettingssavedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("PCSX2settingssaved", "PCSX2 settings saved."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task ShowEmulatorDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        if (O == null) return;
        var downloaderror = _localization.GetString("Downloaderror", "Download error.");
        var wouldyouliketoberedirected = _localization.GetString("Wouldyouliketoberedirected",
            "Would you like to be redirected to the download page?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{downloaderror}\n\n{wouldyouliketoberedirected}", error,
            MessageButtons.YesNo, MessageIcon.Error);
        if (result != MessageBoxResult.Yes) return;

        var downloadLink = selectedSystem.Emulators?.Emulator?.EmulatorDownloadLink;
        if (string.IsNullOrEmpty(downloadLink)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadLink,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Expected condition (no default browser / broken link): not a bug report.
            Log.Information(ex, "Error opening the emulator download link");
            await CouldNotOpenTheDownloadLinkMessageBoxAsync();
        }
    }


    public Task InvalidSystemConfigurationMessageBoxAsync(string errorMessage)
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, errorMessage,
            _localization.GetString("InvalidSystemConfiguration", "Invalid System Configuration"), MessageButtons.Ok,
            MessageIcon.Warning);
    }

    public async Task<MessageBoxResult> CouldNotLoadHelpUserXmlMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var simpleLaunchercouldnotloadhelpuserxml = _localization.GetString("SimpleLaunchercouldnotloadhelpuserxml",
            "'Simple Launcher' could not load 'helpuser.xml'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        return await ShowAsync(O, $"{simpleLaunchercouldnotloadhelpuserxml}\n\n" +
                                  $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task MednafenConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("MednafenConfigurationSavedSuccessfully",
                "Mednafen configuration saved successfully."), _localization.GetString("Success", "Success"),
            MessageButtons.Ok,
            MessageIcon.Information);
    }


    public async Task CustomErrorMessageBoxAsync(string message, string title)
    {
        if (O != null) await ShowAsync(O, message, title, MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task CustomInfoMessageBoxAsync(string title, string message)
    {
        if (O != null) await ShowAsync(O, message, title, MessageButtons.Ok, MessageIcon.Information);
    }


    public Task ErrorOpeningTheUpdateHistoryWindowMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            $"{_localization.GetString("ErroropeningtheUpdateHistorywindow", "Error opening the Update History window.")}\n\n" +
            $"{_localization.GetString("Theerrorwasreportedtothedeveloper", "The error was reported to the developer who will try to fix the issue.")}",
            _localization.GetString("Error", "Error"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task RaineExecutableNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("RaineConfig_PathNotFound", "Raine executable not found. Please select it."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task PleaseExtractApplicationFirstMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var title = _localization.GetString("Error", "Error");
        var message = _localization.GetString("SimpleLaunchercannotrunfromatemporary",
            "'Simple Launcher' cannot run from a temporary folder.");
        var message2 = _localization.GetString("Pleaseextracttheapplicationtoapermanentfolder",
            "Please extract the application to a permanent folder before running it.");
        return ShowAsync(O, $"{message}\n\n{message2}", title, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ApplicationControlPolicyBlockedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var message = _localization.GetString("ApplicationControlPolicyBlockedFile",
            "An application control policy blocked this file or link.");
        var simpleLaunchercannotperform = _localization.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var securityPolicyBlocked = _localization.GetString("SecurityPolicyBlocked", "Security Policy Blocked");
        return ShowAsync(O, $"{message}\n\n{simpleLaunchercannotperform}\n\n", securityPolicyBlocked,
            MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task ThereIsNoFlyerMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnoflyer", "There is no flyer file associated with this game."),
            _localization.GetString("Flyernotfound", "Flyer not found"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task FailedToSaveMesenConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveMesenConfiguration",
                "Failed to save Mesen configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SystemNotFoundInTheXmlMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Selectedsystemnotfound", "Selected system not found in the XML document!"),
            _localization.GetString("Alert", "Alert"), MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task PotentialPathManipulationDetectedMessageBoxAsync(string archivePath)
    {
        if (O == null) return Task.CompletedTask;
        var title = _localization.GetString("SecurityWarning", "Security Warning");
        var pathManipulationDetected =
            _localization.GetString("PathManipulationDetected", "Potential Path Manipulation Detected");
        var zipSlipExplanation = _localization.GetString("ZipSlipExplanation",
            "A security vulnerability called 'Zip Slip' was detected in the archive file. This is a path traversal vulnerability that could allow an attacker to write files outside of the intended extraction directory.");
        var archivePathMessage = _localization.GetString("ArchivePathMessage", "Archive file:");
        var actionTaken = _localization.GetString("ActionTaken",
            "For your security, the extraction process has been properly handle and the issue has been logged.");
        var reportedToDeveloper = _localization.GetString("ReportedToDeveloper",
            "This security issue has been reported to the developer team.");
        return ShowAsync(O, $"{pathManipulationDetected}\n\n{zipSlipExplanation}\n\n" +
                            $"{archivePathMessage} {archivePath}\n\n{actionTaken}\n\n{reportedToDeveloper}", title,
            MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task ExtractionFailedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var extractionfailed = _localization.GetString("Extractionfailed", "Extraction failed.");
        var ensurethefileisnotcorrupted =
            _localization.GetString("Ensurethefileisnotcorrupted", "Ensure the file is not corrupted.");
        var ensureyouhaveenoughspaceintheHdd = _localization.GetString("EnsureyouhaveenoughspaceintheHDD",
            "Ensure you have enough space in the HDD to extract the file.");
        var grantSimpleLauncheradministrative = _localization.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var ensuretheSimpleLauncherfolder = _localization.GetString("EnsuretheSimpleLauncherfolder",
            "Ensure the 'Simple Launcher' folder is a writable directory.");
        var temporarilydisableyourantivirus = _localization.GetString("Temporarilydisableyourantivirus",
            "Temporarily disable your antivirus software and try again.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{extractionfailed}\n\n{ensurethefileisnotcorrupted}\n" +
                            $"{ensureyouhaveenoughspaceintheHdd}\n{grantSimpleLauncheradministrative}\n" +
                            $"{ensuretheSimpleLauncherfolder}\n{temporarilydisableyourantivirus}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task ErrorWhileRemovingGameFromFavoriteMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O, _localization.GetString("Errorremovingfromfavorites", "Error removing from favorites."),
                _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public async Task FileAddedToFavoritesMessageBoxAsync(string fileNameWithoutExtension)
    {
        if (O != null)
        {
            await ShowAsync(O,
                $"{fileNameWithoutExtension} {_localization.GetString("Addedtofavorites", "added to favorites.")}",
                _localization.GetString("Added", "Added"), MessageButtons.Ok,
                MessageIcon.Information);
        }
    }


    public async Task FileRemovedFromFavoritesMessageBoxAsync(string fileNameWithoutExtension)
    {
        if (O != null)
        {
            await ShowAsync(O,
                $"{fileNameWithoutExtension} {_localization.GetString("Removedfromfavorites", "removed from favorites.")}",
                _localization.GetString("Removed", "Removed"), MessageButtons.Ok,
                MessageIcon.Information);
        }
    }


    public Task GameFileDoesNotExistMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var thegamefiledoesnotexist =
            _localization.GetString("Thegamefiledoesnotexist", "The game file does not exist!");
        var thefilehasbeenremovedfromthelist = _localization.GetString("Thefilehasbeenremovedfromthelist",
            "The file has been removed from the list.");
        var info = _localization.GetString("Info", "Info");
        return ShowAsync(O, $"{thegamefiledoesnotexist}\n\n{thefilehasbeenremovedfromthelist}", info,
            MessageButtons.Ok, MessageIcon.Information);
    }


    public Task CouldNotCheckForDiskSpaceMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SimpleLaunchercouldnotcheckdiskspace",
                "'Simple Launcher' could not check disk space for the specified path. Please check the path and try again."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task ReinstallSimpleLauncherFileMissingMessageBoxAsync()
    {
        if (O == null) return;

        var result = await ShowAsync(O,
            $"{_localization.GetString("Thefilemamedatcouldnotbefound", "The file 'mame.dat' could not be found in the application folder.")}\n\n" +
            $"{_localization.GetString("DoyouwanttoautomaticallyreinstallSimpleLaunchertofixit", "Do you want to automatically reinstall 'Simple Launcher' to fix it?")}",
            _localization.GetString("Error", "Error"), MessageButtons.YesNo, MessageIcon.Question);

        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }

    public Task FailedToStartSimpleLauncherMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtostartSimpleLauncherAnerroroccurred",
                "Failed to start 'Simple Launcher'. An error occurred while checking for existing instances."),
            _localization.GetString("SimpleLauncherError", "Simple Launcher Error"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task RequiredFileMissingMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("PleasereinstallSimpleLauncher",
                "Please reinstall 'Simple Launcher' manually to fix the issue."),
            _localization.GetString("Warning", "Warning"),
            MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task CouldNotOpenBrowserForAiSupportMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("CouldnotopenbrowserforAIsupport", "Could not open browser for AI support."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task StellaEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("StellaemulatornotfoundPleaselocate",
                "Stella emulator not found. Please locate 'stella.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ThereWasAnErrorDeletingTheGameMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrordeletingthefile = _localization.GetString("Therewasanerrordeletingthefile",
            "There was an error deleting the file.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrordeletingthefile}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task PleaseEnterSearchTermMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Pleaseenterasearchterm", "Please enter a search term."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public async Task ShowCoreDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        if (O == null) return;
        var downloaderror = _localization.GetString("Downloaderror", "Download error.");
        var wouldyouliketoberedirected = _localization.GetString("Wouldyouliketoberedirected",
            "Would you like to be redirected to the download page?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{downloaderror}\n\n{wouldyouliketoberedirected}", error,
            MessageButtons.YesNo, MessageIcon.Error);
        if (result != MessageBoxResult.Yes) return;

        var downloadLink = selectedSystem.Emulators?.Emulator?.CoreDownloadLink;
        if (string.IsNullOrEmpty(downloadLink)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadLink,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Expected condition (no default browser / broken link): not a bug report.
            Log.Information(ex, "Error opening the core download link");
            await CouldNotOpenTheDownloadLinkMessageBoxAsync();
        }
    }

    public async Task<MessageBoxResult> WarnUserAboutMemoryConsumptionMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var warningMessage = _localization.GetString("WarningSettingupaveryhighnumberofgamesperpage",
            "Warning! Setting a very high number of games per page will significantly increase system memory usage when in Grid mode. If the number is too high, this may cause the application to crash. Please proceed with caution.");
        var proceedQuestion =
            _localization.GetString("AreYouSureYouWantToProceed", "Are you sure you want to proceed?");
        var warningTitle = _localization.GetString("Warning", "Warning");
        return await ShowAsync(O, $"{warningMessage}\n\n{proceedQuestion}", warningTitle,
            MessageButtons.YesNo, MessageIcon.Warning);
    }


    public Task ErrorLoadingAppSettingsMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorloadingconfiguration = _localization.GetString("Therewasanerrorloadingconfiguration",
            "There was an error loading 'appsettings.json'.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorloadingconfiguration}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task CouldNotSaveScreenshotMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var failedtosavescreenshot =
            _localization.GetString("Failedtosavescreenshot", "Failed to save screenshot.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{failedtosavescreenshot}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task NoHistoryXmlOrDatFoundMessageBoxAsync()
    {
        if (O == null) return;
        var nohistoryxmlfilefound = _localization.GetString("Nohistoryxmlfilefound2",
            "No 'history.dat' or 'history.xml' file found in the application folder.");
        var doyouwanttoreinstallSimpleLauncher = _localization.GetString("DoyouwanttoreinstallSimpleLauncher",
            "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{nohistoryxmlfilefound}\n\n{doyouwanttoreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);

        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task SystemNameCanNotBeEmptyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var systemNamecannotbeemptyor = _localization.GetString("SystemNamecannotbeemptyor",
            "'System Name' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _localization.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{systemNamecannotbeemptyor}\n\n{pleasefixthisfield}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SupermodelEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SupermodelEmulatorNotFound",
                "Supermodel emulator not found. Please locate 'Supermodel.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ToolLaunchWasCanceledByUserMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("thelaunchoftheselectedtoolwascanceledbytheuser",
                "The launch of the selected tool was canceled by the user."), _localization.GetString("Info", "Info"),
            MessageButtons.Ok,
            MessageIcon.Information);
    }


    public async Task CouldNotLaunchGameMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var result = await ShowAsync(O,
            $"{_localization.GetString("SimpleLaunchercouldnotlaunch", "'Simple Launcher' could not launch the selected game.")}\n\n" +
            $"{_localization.GetString("MakesuretheROMorISOyouretrying", "Make sure the ROM or ISO you're trying to run is not corrupted.")}\n" +
            $"{_localization.GetString("IfyouaretryingtorunRetroarchensurethattheBIOS", "If you are trying to run Retroarch, ensure that the BIOS or required files for the core are installed.")}\n" +
            $"{_localization.GetString("Alsomakesureyouarecallingtheemulator", "Also, make sure you are calling the emulator with the correct parameter.")}\n\n" +
            $"{_localization.GetString("YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.")}\n\n" +
            $"{_localization.GetString("Doyouwanttoopenthefile", "Do you want to open the file 'error_user.log' to debug the error?")}",
            _localization.GetString("Error", "Error"), MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(logPath))
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
                Log.Error(ex, "Failed to open the error log file");
            }
        }
    }


    public Task FailedToInjectMednafenConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectMednafenconfiguration",
                "Failed to inject Mednafen configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task ErrorLaunchingGameMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var therewasanerrorlaunchingtheselected =
            _localization.GetString("Therewasanerrorlaunchingtheselected",
                "There was an error launching the selected game.");
        var dowanttoopenthefileerroruserlog = _localization.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O,
            $"{therewasanerrorlaunchingtheselected}\n\n{dowanttoopenthefileerroruserlog}", error,
            MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwasnotfound",
                        "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task ReDreamConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("ReDreamConfigurationSavedSuccessfully",
                "ReDream configuration saved successfully."), _localization.GetString("Success", "Success"),
            MessageButtons.Ok,
            MessageIcon.Information);
    }


    public async Task FailedToLoadParametersMdMessageBoxAsync()
    {
        if (O == null) return;
        var unabletoloadparametersmd = _localization.GetString("Unabletoloadparametersmd",
            "Unable to load 'parameters.md'. The file may be corrupted or in use.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{unabletoloadparametersmd}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public async Task CouldNotLaunchThisGameMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var simpleLaunchercouldnotlaunchthisgame = _localization.GetString("SimpleLaunchercouldnotlaunchthisgame",
            "'Simple Launcher' could not launch this game.");
        var dowanttoopenthefileerroruserlog = _localization.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O,
            $"{simpleLaunchercouldnotlaunchthisgame}\n\n{dowanttoopenthefileerroruserlog}", error,
            MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwasnotfound",
                        "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task EnterYourRetroAchievementsUsernameMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("PleaseenteryourRetroAchievements",
                "Please enter your RetroAchievements username, API key, and password before configuring an emulator."),
            _localization.GetString("CredentialsRequired", "Credentials Required"), MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task FailedtoinjectRetroArchconfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectRetroArchconfigurationTheerror",
                "Failed to inject RetroArch configuration. The error has been logged. Please check the emulator path and try again."),
            _localization.GetString("InjectionError", "Injection Error"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public async Task InvalidOperationExceptionMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var simpleLaunchercouldnotlaunch = _localization.GetString("SimpleLaunchercouldnotlaunch",
            "'Simple Launcher' could not launch the selected game.");
        var makesuretheRoMorIsOyouretrying = _localization.GetString("MakesuretheROMorISOyouretrying",
            "Make sure the ROM or ISO you're trying to run is not corrupted.");
        var ifyouaretryingtorunRetroarchensurethattheBios = _localization.GetString(
            "IfyouaretryingtorunRetroarchensurethattheBIOS",
            "If you are trying to run Retroarch, ensure that the BIOS or required files for the core are installed.");
        var alsomakesureyouarecallingtheemulator = _localization.GetString("Alsomakesureyouarecallingtheemulator",
            "Also, make sure you are calling the emulator with the correct parameter.");
        var youcanturnoffthistypeoferrormessageinExpertmode = _localization.GetString(
            "YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.");
        var doyouwanttoopenthefile = _localization.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{simpleLaunchercouldnotlaunch}\n\n" +
                                        $"{makesuretheRoMorIsOyouretrying}\n" +
                                        $"{ifyouaretryingtorunRetroarchensurethattheBios}\n" +
                                        $"{alsomakesureyouarecallingtheemulator}\n\n" +
                                        $"{youcanturnoffthistypeoferrormessageinExpertmode}\n\n" +
                                        $"{doyouwanttoopenthefile}", error, MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwas", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public async Task RetroArchParameterIssueMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var result = await ShowAsync(O,
            $"{_localization.GetString("RetroArchParameterIssue", "RetroArch could not launch your game.")}\n\n" +
            $"{_localization.GetString("RetroArchParameterIssue2", "99% of the launch failures are due to incorrect parameters.")}\n\n" +
            $"{_localization.GetString("RetroArchParameterIssue3", "Go back to 'Expert Mode' and double-check the parameter field for this emulator. Double-check the path to the desired core. Read the recommendations from the 'Simple Launcher' developer for the specific system.")}\n\n" +
            $"{_localization.GetString("RetroArchParameterIssue4", "Check the core requirements to run it. Some cores require a BIOS file to work. Read the core documentation to figure out what the requirements are for that specific core.")}\n\n" +
            $"{_localization.GetString("Doyouwanttoopenthefileerroruserlog", "Do you want to open the file 'error_user.log' to debug the error?")}",
            _localization.GetString("Error", "Error"), MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(logPath))
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwas", "The file 'error_user.log' was not found!"),
                    _localization.GetString("Error", "Error"), MessageButtons.Ok,
                    MessageIcon.Error);
            }
        }
    }


    public Task DaphneConfigurationSaveFailedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Failedtosaveconfiguration",
                "Failed to save configuration. The error has been logged to the developer."),
            _localization.GetString("Error", "Error"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToInjectMesenConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectMesenconfiguration",
                "Failed to inject Mesen configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task EnterSearchQueryMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Pleaseenterasearchquery", "Please enter a search query."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task SegaModel2ConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SEGAModel2ConfigurationSavedSuccessfully",
                "SEGA Model 2 configuration saved successfully."), _localization.GetString("Success", "Success"),
            MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task SevenZipDllNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            $"{_localization.GetString("The7zdllismissingfromtheapplicationfolder", "The 7z dll is missing from the application folder!")}\n\n" +
            $"{_localization.GetString("DoyouwanttoreinstallSimpleLauncher", "Do you want to reinstall 'Simple Launcher' to fix the issue?")}",
            _localization.GetString("Error", "Error"), MessageButtons.YesNo,
            MessageIcon.Question);
    }


    public Task PathOrParameterInvalidMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var oneormorepathsorparameters = _localization.GetString("Oneormorepathsorparameters",
            "One or more paths or parameters are invalid.");
        var pleasefixthemtoproceed =
            _localization.GetString("Pleasefixthemtoproceed", "Please fix them to proceed.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{oneormorepathsorparameters}\n\n{pleasefixthemtoproceed}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task NoSystemInHelpUserXmlMessageBoxAsync()
    {
        if (O == null) return;
        var novalidsystemsfoundinthefilehelpuserxml = _localization.GetString(
            "Novalidsystemsfoundinthefilehelpuserxml", "No valid systems found in the file 'helpuser.xml'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{novalidsystemsfoundinthefilehelpuserxml}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task AnErrorOccurredWhileConfiguringTheEmulatorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Anerroroccurredwhileconfiguringtheemulator",
                "An error occurred while configuring the emulator."), _localization.GetString("Error", "Error"),
            MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task NavigationButtonErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorinthenavigationbutton = _localization.GetString("Therewasanerrorinthenavigationbutton",
            "There was an error in the navigation button.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorinthenavigationbutton}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task XemuParameterShouldContainDvdPathMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var title = _localization.GetString("Error", "Error");
        var message = _localization.GetString("XemuParameterShouldContainDvdPath",
            "The Xemu parameter should contain '-dvd_path'.");
        var message2 = _localization.GetString("EditthissysteminExpertModeandfixtheparameter",
            "Edit this system in 'Expert Mode' and fix the parameter field for this emulator.");
        return ShowAsync(O, $"{message}\n\n{message2}", title, MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task<MessageBoxResult> GameFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath)
    {
        if (O == null) return MessageBoxResult.Cancel;
        var thegamefiledoesnotexist =
            _localization.GetString("Thegamefiledoesnotexist", "The game file does not exist!");
        var filepathis = _localization.GetString("FilePathIs", "File path:");
        var doYouWantToDeleteThisEntry = _localization.GetString("DoYouWantToDeleteThisEntry",
            "Do you want to delete this entry from the play history?");
        var clickNoToKeepTheEntry =
            _localization.GetString("ClickNoToKeepTheEntry", "Click 'No' to keep the entry in the list.");
        var gameNotAvailable = _localization.GetString("GameNotAvailable", "Game Not Available");
        var message = $"{thegamefiledoesnotexist}\n\n" +
                      $"{filepathis}\n{filePath}\n\n" +
                      $"{doYouWantToDeleteThisEntry}\n" +
                      $"{clickNoToKeepTheEntry}";
        return await ShowAsync(O, message, gameNotAvailable, MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task EmulatorLocationRequiredMessageBoxAsync(int emulatorNumber)
    {
        if (O == null) return Task.CompletedTask;
        var message = _localization.GetString($"Emulator{emulatorNumber}pathisrequired",
            $"Emulator {emulatorNumber} path is required.");
        return ShowAsync(O, message, _localization.GetString("Warning", "Warning"), MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public async Task ThereWasAnErrorLaunchingThisGameMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var therewasanerrorlaunchingthisgame = _localization.GetString("Therewasanerrorlaunchingthisgame",
            "There was an error launching this game.");
        var youcanturnoffthistypeoferrormessageinExpertmode = _localization.GetString(
            "YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.");
        var doyouwanttoopenthefileerroruserlog = _localization.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{therewasanerrorlaunchingthisgame}\n\n" +
                                        $"{youcanturnoffthistypeoferrormessageinExpertmode}\n\n" +
                                        $"{doyouwanttoopenthefileerroruserlog}", error, MessageButtons.YesNo,
            MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task SystemImageFolderCanNotBeEmptyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var systemImageFoldercannotbeempty = _localization.GetString("SystemImageFoldercannotbeempty",
            "'System Image Folder' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _localization.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{systemImageFoldercannotbeempty}\n\n{pleasefixthisfield}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task<MessageBoxResult> SearchOnlineForRomHistoryMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var thereisnoRoMhistoryinthelocaldatabase = _localization.GetString("ThereisnoROMhistoryinthelocaldatabase",
            "There is no ROM history in the local database for this file.");
        var doyouwanttosearchonline = _localization.GetString("Doyouwanttosearchonline",
            "Do you want to search online for the ROM history?");
        var rOmHistoryNotFound = _localization.GetString("ROMHistorynotfound", "ROM History not found");
        return await ShowAsync(O, $"{thereisnoRoMhistoryinthelocaldatabase}\n\n{doyouwanttosearchonline}",
            rOmHistoryNotFound, MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task CouldNotOpenHistoryWindowMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasaproblemopeningtheHistorywindow = _localization.GetString(
            "TherewasaproblemopeningtheHistorywindow", "There was a problem opening the History window.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasaproblemopeningtheHistorywindow}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToSaveAzaharConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveAzaharConfiguration",
                "Failed to save Azahar configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToLoginToRetroAchievementsMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtologintoRetroAchievements",
                "Failed to log in to RetroAchievements. Please check your username and password."),
            _localization.GetString("LoginFailed", "Login Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToInjectReDreamConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveReDreamConfiguration",
                "Failed to save ReDream configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedtoinjectMamEconfiguration2MessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectMAMEconfigurationTheerror",
                "Failed to inject MAME configuration. The error has been logged."),
            _localization.GetString("InjectionError", "Injection Error"),
            MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task LinksSavedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Linkssavedsuccessfully", "Links saved successfully."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task CouldNotOpenManualMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            $"{_localization.GetString("Failedtoopenthemanual", "Failed to open the manual.")}\n\n" +
            $"{_localization.GetString("Theerrorwasreportedtothedeveloper", "The error was reported to the developer who will try to fix the issue.")}",
            _localization.GetString("Error", "Error"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public async Task ShowCustomMessageBoxAsync(string message, string launchError, string? logPath)
    {
        if (O == null) return;

        var therewasanerrorlaunchingtheselected = _localization.GetString("Therewasanerrorlaunchingtheselected",
            "There was an error launching the selected game.");
        var dowanttoopenthefileerroruserlog = _localization.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");

        var result = await ShowAsync(O,
            $"{therewasanerrorlaunchingtheselected}\n\n{message}\n\n{dowanttoopenthefileerroruserlog}", launchError,
            MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwasnotfound",
                        "The file 'error_user.log' was not found!"),
                    launchError, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task NullFileExtensionMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var thereisnoExtension = _localization.GetString("ThereisnoExtension",
            "There is no 'Extension to Launch After Extraction' set in the system configuration.");
        var pleaseeditthissystemto =
            _localization.GetString("Pleaseeditthissystemto", "Please edit this system to fix that.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{thereisnoExtension}\n\n{pleaseeditthissystemto}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task<MessageBoxResult> DoYouWantToCancelAndCloseMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var message = _localization.GetString("ProcessingStillRunningMessage",
            "Processing is still running. Do you want to cancel and close?");
        var title = _localization.GetString("ConfirmClose", "Confirm Close");
        return await ShowAsync(O, message, title, MessageButtons.YesNo, MessageIcon.Question);
    }


    public async Task ThereWasAnErrorMountingTheFileMessageBoxAsync(int? exitCode = null)
    {
        if (O == null) return;
        Log.Debug("File mounting error. Exit code: {ExitCode}", exitCode);
        var simpleLauncherCouldNotMount = _localization.GetString("SimpleLaunchercouldnotmount",
            "'Simple Launcher' could not mount the selected game.");
        var reasonMessage = exitCode switch
        {
            -1073741510 => _localization.GetString("ThisDokanVersionIncompatible",
                "The installed version of Dokan may be incompatible. Try reinstalling or updating Dokan."),
            -1073741515 => _localization.GetString("Dokannotinstalled",
                "Dokan library is not installed. Dokan is required for mounting ZIP, CHD and disk image files."),
            _ => _localization.GetString("ThismaybeduetoDokannotbeinginstalled2",
                "This may be due to Dokan not being installed. Dokan is required for mounting ZIP, CHD and disk image files.")
        };
        var doYouWantToOpenBrowser = _localization.GetString("DoyouwanttoopenyourbrowsertodownloadDokan",
            "Do you want to open your browser to download Dokan?");
        var error = _localization.GetString("Error", "Error");
        var messageBoxResult = await ShowAsync(O,
            $"{simpleLauncherCouldNotMount}\n\n{reasonMessage}\n\n{doYouWantToOpenBrowser}",
            error, MessageButtons.YesNo, MessageIcon.Question);

        if (messageBoxResult != MessageBoxResult.Yes) return;

        var downloadPageUrl = _configuration.GetValue<string>("Urls:DokanyWebsite")
                              ?? "https://github.com/dokan-dev/dokany";
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
            // Expected condition (no default browser / headless session): not a bug report.
            Log.Information(ex, "Could not open the Dokan website");
            await ShowAsync(O,
                _localization.GetString("Anerroroccurredwhileopeningyourbrowser",
                    "An error occurred while opening your browser."),
                error, MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public Task FailedtoinjectXeniaconfiguration2MessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectXeniaconfigurationTheerror",
                "Failed to inject Xenia configuration. The error has been logged."),
            _localization.GetString("InjectionError", "Injection Error"),
            MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task SimpleLauncherDoesNotSupportRaHashOfSystemGroupedByFolderMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var simpleLauncherdoesnotsupportRetroAchievementshashofSystems = _localization.GetString(
            "simpleLauncherdoesnotsupportRetroAchievementshashofSystems",
            "'Simple Launcher' does not support RetroAchievements hash of systems Grouped by Folder.");
        var pleaseedittheSystemsettingsanddisablethe = _localization.GetString(
            "pleaseedittheSystemsettingsanddisablethe",
            "Please edit the system settings and disable the 'Group Files by Folder' option.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{simpleLauncherdoesnotsupportRetroAchievementshashofSystems}\n\n" +
                            $"{pleaseedittheSystemsettingsanddisablethe}", error, MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task FailedToSaveStellaConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveStellaConfiguration",
                "Failed to save Stella configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ExtensionToSearchIsRequiredMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var extensiontoSearchintheSystemFolder = _localization.GetString("ExtensiontoSearchintheSystemFolder",
            "'Extension to Search in the System Folder' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _localization.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{extensiontoSearchintheSystemFolder}\n\n{pleasefixthisfield}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task YouCanAddANewSystemMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Youcanaddanewsystem", "You can add a new system now."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }

    public async Task<MessageBoxResult> ReallyWantToRemoveAllPlayHistoryMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        return await ShowAsync(O,
            _localization.GetString("AreYouSureYouWantToRemoveAllHistory",
                "Are you sure you want to remove all play history?"),
            _localization.GetString("Confirmation", "Confirmation"), MessageButtons.YesNo, MessageIcon.Question);
    }

    public async Task<MessageBoxResult> AreYouSureYouWantToDeleteTheGameMessageBoxAsync(string fileNameWithExtension)
    {
        if (O == null) return MessageBoxResult.Cancel;
        var loc = App.ServiceProvider?.GetService<LocalizationService>();
        var areYouSure =
            loc?.GetString("Areyousureyouwanttodeletethefile") is { } s1 && !string.Equals(s1,
                "Areyousureyouwanttodeletethefile"
                , StringComparison.OrdinalIgnoreCase)
                ? s1
                : "Are you sure you want to delete the file";
        var thisAction = loc?.GetString("Thisactionwilldelete") is { } s2 && !string.Equals(s2, "Thisactionwilldelete"
            , StringComparison.OrdinalIgnoreCase)
            ? s2
            : "This action will delete the file from the HDD and cannot be undone.";
        var confirm =
            loc?.GetString("ConfirmDeletion") is { } s3 &&
            !string.Equals(s3, "ConfirmDeletion", StringComparison.OrdinalIgnoreCase)
                ? s3
                : "Confirm Deletion";
        return await ShowAsync(O, $"{areYouSure} '{fileNameWithExtension}'?\n\n{thisAction}", confirm,
            MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task ReDreamEmulatorPathNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("ReDreamConfig_PathNotFound", "ReDream executable not found. Please select it."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task FailedToCopySystemImageMessageBoxAsync(string errorMessage)
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            $"{_localization.GetString("FailedToCopySystemImage", "Failed to copy the image:")} {errorMessage}",
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SupportRequestSuccessMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Supportrequestsentsuccessfully", "Support request sent successfully."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }

    public async Task<MessageBoxResult> AreYouSureYouWantToDeleteTheCoverImageMessageBoxAsync(
        string fileNameWithoutExtension)
    {
        if (O == null) return MessageBoxResult.Cancel;
        var loc = App.ServiceProvider?.GetService<LocalizationService>();
        var areYouSure =
            loc?.GetString("Areyousureyouwanttodeletethecoverimageof") is { } s1 &&
            !string.Equals(s1, "Areyousureyouwanttodeletethecoverimageof"
                , StringComparison.OrdinalIgnoreCase)
                ? s1
                : "Are you sure you want to delete the cover image of";
        var thisAction = loc?.GetString("Thisactionwilldelete") is { } s2 && !string.Equals(s2, "Thisactionwilldelete"
            , StringComparison.OrdinalIgnoreCase)
            ? s2
            : "This action will delete the file from the HDD and cannot be undone.";
        var confirm =
            loc?.GetString("ConfirmDeletion") is { } s3 &&
            !string.Equals(s3, "ConfirmDeletion", StringComparison.OrdinalIgnoreCase)
                ? s3
                : "Confirm Deletion";
        return await ShowAsync(O, $"{areYouSure} '{fileNameWithoutExtension}'?\n\n{thisAction}", confirm,
            MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task ThereWasAnErrorDeletingTheCoverImageMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrordeletingthecoverimage = _localization.GetString("Therewasanerrordeletingthecoverimage",
            "There was an error deleting the cover image.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrordeletingthecoverimage}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SystemNameRequiredBeforeChoosingImageMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SystemNameRequiredBeforeChoosingImage",
                "Please enter a system name before choosing an image."),
            _localization.GetString("SystemNameRequired", "System Name Required"),
            MessageButtons.Ok, MessageIcon.Information);
    }


    public Task MainWindowSearchEngineErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorwiththesearchengine = _localization.GetString("Therewasanerrorwiththesearchengine",
            "There was an error with the search engine.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorwiththesearchengine}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task BlastemEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("BlastememulatornotfoundPleaselocate",
                "Blastem emulator not found. Please locate 'blastem.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task NoSoundFileIsSelectedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("NoSoundFileSelectedWarning", "No sound file is selected."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task NotificationSoundIsDisableMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("NotificationSoundIsDisable", "Notification sound is disable"),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task SelectASystemToDeleteMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Pleaseselectasystemtodelete", "Please select a system to delete."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task FlycastConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FlycastConfigurationSavedSuccessfully",
                "Flycast configuration saved successfully."), _localization.GetString("Success", "Success"),
            MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task AnotherInstanceIsRunningMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("AnotherinstanceofSimpleLauncherisalreadyrunning",
                "Another instance of 'Simple Launcher' is already running."), "Simple Launcher",
            MessageButtons.Ok, MessageIcon.Information);
    }


    public Task AzaharConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("AzaharConfigurationSavedSuccessfully", "Azahar configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task FailedToLoadLanguageResourceMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var failedtoloadlanguageresources =
            _localization.GetString("Failedtoloadlanguageresources", "Failed to load language resources.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var languageLoadingError = _localization.GetString("LanguageLoadingError", "Language Loading Error");
        return ShowAsync(O, $"{failedtoloadlanguageresources}\n\n{theerrorwasreportedtothedeveloper}",
            languageLoadingError, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SystemHasBeenDeletedMessageBoxAsync(string selectedSystemName)
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            $"{_localization.GetString("System", "System")} '{selectedSystemName}' {_localization.GetString("hasbeendeleted", "has been deleted.")}",
            _localization.GetString("Info", "Info"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task UnabletomountIsOfileMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var unabletomountIsOfile = _localization.GetString("UnabletomountISOfile",
            "Unable to mount ISO file due to PowerShell execution policy restrictions.");
        var thisistypicallycausedbyGroup = _localization.GetString("ThisistypicallycausedbyGroup",
            "This is typically caused by Group Policy settings on corporate or managed PCs.");
        var simpleLaunchercannotperform = _localization.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var powerShellRestricted = _localization.GetString("PowerShellRestricted", "PowerShell Restricted");
        return ShowAsync(O, $"{unabletomountIsOfile}\n\n{thisistypicallycausedbyGroup}\n\n" +
                            $"{simpleLaunchercannotperform}", powerShellRestricted, MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task FailedToInjectSupermodelConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectSupermodelconfiguration",
                "Failed to inject Supermodel configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ShowImageDownloadTimeoutMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var simpleLauncherCouldNotDownloadImages = _localization.GetString("SimpleLauncherCouldNotDownloadImages",
            "Simple Launcher could not download images due to access issues to Cloudflare servers.");
        var thisMayBeDueToCountryFirewallRestrictions = _localization.GetString(
            "ThisMayBeDueToCountryFirewallRestrictions", "This may be due to country firewall restrictions.");
        var pleaseTryAgainBehindAVpn =
            _localization.GetString("PleaseTryAgainBehindAVpn", "Please try again behind a VPN.");
        var imageDownloadError = _localization.GetString("ImageDownloadError", "Image Download Error");
        return ShowAsync(O, $"{simpleLauncherCouldNotDownloadImages}\n\n" +
                            $"{thisMayBeDueToCountryFirewallRestrictions}\n\n{pleaseTryAgainBehindAVpn}",
            imageDownloadError, MessageButtons.Ok, MessageIcon.Warning);
    }


    public async Task ErrorFindingGameFilesMessageBoxAsync(string logPath)
    {
        if (O == null) return;

        var therewasanerrorfinding = _localization.GetString("Therewasanerrorfinding",
            "There was an error finding the game files.");
        var doyouwanttoopenthefileerroruserlog = _localization.GetString("Doyouwanttoopenthefileerroruserlog",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{therewasanerrorfinding}\n\n{doyouwanttoopenthefileerroruserlog}", error,
            MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwas", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task FailedtoinjectXeniaconfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectXeniaconfigurationTheerrorPleasecheck",
                "Failed to inject Xenia configuration. The error has been logged. Please check the emulator path and try again."),
            _localization.GetString("InjectionError", "Injection Error"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task PleaseSelectASystemBeforeMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("PleaseselectasystembeforeusingtheFeeling",
                "Please select a system before using the Feeling Lucky feature."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task ErrorOpeningDonationLinkMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerroropeningthedonation = _localization.GetString("Therewasanerroropeningthedonation",
            "There was an error opening the Donation Link.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerroropeningthedonation}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerror = _localization.GetString("Therewasanerror", "There was an error.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerror}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task NoGameFoundInTheRandomSelectionMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Nogamesfoundtorandomlyselectfrom",
                "No games found to randomly select from. Please check your system selection."),
            _localization.GetString("FeelingLucky", "Feeling Lucky"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task FileCouldNotBeDeletedMessageBoxAsync(string fileNameWithExtension)
    {
        if (O != null)
        {
            await ShowAsync(O,
                _localization.GetString("Couldnotdelete", "Could not delete ") + fileNameWithExtension,
                _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public Task CemuConfigurationSavedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("CemuConfigurationSaved", "Cemu configuration saved."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task InjectionFailedGenericMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Failedtoinjectconfiguration",
                "Failed to inject configuration. The error has been logged to the developer."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FolderCreationFailedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var simpleLauncherfailedtocreatethe = _localization.GetString("SimpleLauncherfailedtocreatethe",
            "'Simple Launcher' failed to create the necessary folders for this system.");
        var grantSimpleLauncheradministrative = _localization.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var temporarilydisableyourantivirus = _localization.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var ensurethattheSimpleLauncherfolderislocatedinawritable = _localization.GetString(
            "EnsurethattheSimpleLauncherfolderislocatedinawritable",
            "Ensure that the 'Simple Launcher' folder is located in a writable directory.");
        var info = _localization.GetString("Info", "Info");
        return ShowAsync(O, $"{simpleLauncherfailedtocreatethe}\n\n{grantSimpleLauncheradministrative}\n\n" +
                            $"{temporarilydisableyourantivirus}\n\n" +
                            $"{ensurethattheSimpleLauncherfolderislocatedinawritable}", info,
            MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedToSaveBlastemConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveBlastemConfiguration",
                "Failed to save Blastem configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToSaveSupermodelConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveSupermodelConfiguration",
                "Failed to save Supermodel configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task<MessageBoxResult> AreYouSureDoYouWantToDeleteThisSystemMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        return await ShowAsync(O,
            _localization.GetString("Areyousureyouwanttodeletethis", "Are you sure you want to delete this system?"),
            _localization.GetString("Confirmation", "Confirmation"), MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task AddRaLoginMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("YouneedtoaddRetroAchievementlogin",
                "You need to add RetroAchievement login information to use this feature."),
            _localization.GetString("Attention", "Attention"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedSaveReportMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var failedtosavethereport = _localization.GetString("Failedtosavethereport", "Failed to save the report.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{failedtosavethereport}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ApiKeyErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorintheApiKey = _localization.GetString("TherewasanerrorintheAPIKey",
            "There was an error in the API Key of this form.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorintheApiKey}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task LinksRevertedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Linksrevertedtodefaultvalues", "Links reverted to default values."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedToRestartMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Failedtorestarttheapplication", "Failed to restart the application."),
            _localization.GetString("RestartError", "Restart Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task HandleMissingRequiredFilesMessageBoxAsync(string fileList)
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("PleasereinstallSimpleLauncher",
                "Please reinstall 'Simple Launcher' manually to fix the issue.") + "\n\n" +
            _localization.GetString("Theapplicationwillshutdown", "The application will shutdown."),
            _localization.GetString("MissingRequiredFiles", "Missing Required Files"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task Rpcs3EmulatorNotFoundPleaseLocateMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("RPCS3emulatornotfoundPleaselocate",
                "RPCS3 emulator not found. Please locate 'rpcs3.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToCopyLogContentMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Failedtocopylogcontent", "Failed to copy log content."),
            _localization.GetString("CopyError", "Copy Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task OperationCancelledMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("OperationCancelledMessage", "The operation was cancelled."),
            _localization.GetString("OperationCancelled", "Operation Cancelled"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task FailedToSaveAresConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveAresConfiguration",
                "Failed to save Ares configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToSaveRpcs3ConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtosaveRPCS3configurationPleasecheck",
                "Failed to save RPCS3 configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToInjectRpcs3ConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToInjectRPCS3Configuration",
                "Failed to inject RPCS3 configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task AzaharConfigurationInjectionPermissionErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var title = _localization.GetString("InjectionFailed", "Injection Failed");
        var message1 = _localization.GetString("AzaharConfigPermissionError1",
            "Failed to inject Azahar configuration. The emulator is installed in a protected system directory.");
        var message2 = _localization.GetString("AzaharConfigPermissionError2",
            "The configuration file could not be modified due to insufficient permissions.");
        var message3 = _localization.GetString("AzaharConfigPermissionError3", "To fix this, either:");
        var message4 = _localization.GetString("AzaharConfigPermissionError4",
            "1. Run Simple Launcher as administrator, or");
        var message5 = _localization.GetString("AzaharConfigPermissionError5",
            @"2. Install Azahar in a user directory (e.g., C:\Users\YourName\Azahar)");
        var message6 = _localization.GetString("AzaharConfigPermissionError6",
            "The game will launch with the emulator's default settings.");
        return ShowAsync(O, $"{message1}\n\n{message2}\n\n{message3}\n{message4}\n{message5}\n\n" +
                            $"{message6}", title, MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task FlycastEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Flycastemulatornotfound",
                "Flycast emulator not found. Please locate 'flycast.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task CemuEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Cemuemulatornotfound", "Cemu emulator not found. Please locate 'Cemu.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToSaveDuckStationConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveDuckStationConfiguration",
                "Failed to save DuckStation configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ThereIsNoUpdateAvailableMessageBoxAsync(string currentVersion)
    {
        if (O == null) return Task.CompletedTask;
        var thereisnoupdateavailable =
            _localization.GetString("thereisnoupdateavailable", "There is no update available.");
        var thecurrentversionis = _localization.GetString("Thecurrentversionis", "The current version is");
        var noupdateavailable = _localization.GetString("Noupdateavailable", "No update available");
        return ShowAsync(O, $"{thereisnoupdateavailable}\n\n{thecurrentversionis} {currentVersion}",
            noupdateavailable, MessageButtons.Ok, MessageIcon.Information);
    }


    public Task GroupByFolderOnlyForMameAndDosBoxMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("TheGroupFilesbyFolderoptionisonlycompatiblewith",
                "The 'Group Files by Folder' option is only compatible with MAME emulators (Software List CHDs) or DOSBox emulators (uncompressed DOS game folders). To use a different emulator, please edit the system settings and disable this option."),
            _localization.GetString("CompatibilityWarning", "Compatibility Warning"), MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task CouldNotFindAFileMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var couldnotfindafilewiththeextensiondefined = _localization.GetString(
            "Couldnotfindafilewiththeextensiondefined",
            "Could not find a file with the extension defined in 'Extension to Launch After Extraction' inside the extracted folder.");
        var pleaseeditthissystemtofix =
            _localization.GetString("Pleaseeditthissystemto", "Please edit this system to fix that.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{couldnotfindafilewiththeextensiondefined}\n\n{pleaseeditthissystemtofix}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task RaineSettingsSavedAndInjectedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("RaineSettingsSavedAndInjectedSuccessfully",
                "Raine configuration has been successfully injected."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task AresemulatornotfoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Aresemulatornotfound", "Ares emulator not found. Please locate 'ares.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task FilePathIsInvalidMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var simpleLaunchercouldnotlaunch = _localization.GetString("SimpleLaunchercouldnotlaunch",
            "'Simple Launcher' could not launch the selected game.");
        var thefilepathisinvalid = _localization.GetString("Thefilepathisinvalid",
            "The filepath is invalid or the file does not exist!");
        var networkPathIssue = _localization.GetString("networkPathIssue",
            "If the file is on a network drive ensure your computer is still connected to that drive.");
        var usbDeviceIssue = _localization.GetString("usbDeviceIssue",
            "If the file is on a portable USB device ensure it is still connected to your computer.");
        var oneDriveIssue = _localization.GetString("oneDriveIssue",
            "If the file is in OneDrive, ensure it is synced and downloaded to your device. Right-click the file in File Explorer and select 'Always keep on this device'.");
        var avoidusingspecialcharactersinthefilepath = _localization.GetString(
            "Avoidusingspecialcharactersinthefilepath",
            "Avoid using special characters in the filepath, such as @, !, ?, ~, or any other special characters.");
        var youcanturnoffthistypeoferrormessageinExpertmode = _localization.GetString(
            "YoucanturnoffthiserrormessageinExpertmode", "You can turn off this error message in Expert mode.");
        var doyouwanttoopenthefile = _localization.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{simpleLaunchercouldnotlaunch}\n\n" +
                                        $"{thefilepathisinvalid}\n" +
                                        $"{networkPathIssue}\n" +
                                        $"{usbDeviceIssue}\n" +
                                        $"{oneDriveIssue}\n" +
                                        $"{avoidusingspecialcharactersinthefilepath}\n\n" +
                                        $"{youcanturnoffthistypeoferrormessageinExpertmode}\n\n" +
                                        $"{doyouwanttoopenthefile}", error, MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwas", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task SettingsSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("SettingsSavedSuccessfully", "Settings saved successfully."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task SystemXmlIsCorruptedMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var systemxmliscorrupted = _localization.GetString("systemxmliscorrupted",
            "'system.xml' is corrupted or could not be opened.");
        var pleasefixitmanuallyordeleteit = _localization.GetString("Pleasefixitmanuallyordeleteit",
            "Please fix it manually or delete it.");
        var ifyouchoosetodeleteit = _localization.GetString("Ifyouchoosetodeleteit",
            "If you choose to delete it, 'Simple Launcher' will create a new one for you.");
        var theapplicationwillshutdown =
            _localization.GetString("Theapplicationwillshutdown", "The application will shutdown.");
        var wouldyouliketoopentheerroruserlog = _localization.GetString("Wouldyouliketoopentheerroruserlog",
            "Would you like to open the 'error_user.log' file to investigate the issue?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{systemxmliscorrupted} {pleasefixitmanuallyordeleteit}\n\n" +
                                        $"{ifyouchoosetodeleteit}\n\n" +
                                        $"{theapplicationwillshutdown}" +
                                        $"{wouldyouliketoopentheerroruserlog}", error, MessageButtons.YesNo,
            MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }

        App.ServiceProvider.GetRequiredService<AvaloniaQuitSimpleLauncher>().SimpleQuitApplication();
    }


    public Task RetroArchParameterShouldContainLMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var title = _localization.GetString("Error", "Error");
        var message = _localization.GetString("RetroArchParameterShouldContainL",
            "The RetroArch parameter should contain -L to properly point to the desired core.");
        var message2 = _localization.GetString("EditthissysteminExpertModeandfixtheparameter",
            "Edit this system in 'Expert Mode' and fix the parameter field for this emulator.");
        return ShowAsync(O, $"{message}\n\n{message2}", title, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ThereIsNoPcbMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("ThereisnoPCBfile", "There is no PCB file associated with this game."),
            _localization.GetString("PCBnotfound", "PCB not found"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task SegaModel2EmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SEGAModel2EmulatorNotFound",
                "SEGA Model 2 emulator not found. Please locate 'emulator.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ErrorCalculatingStatsMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var anerroroccurredwhilecalculatingtheGlobal = _localization.GetString(
            "AnerroroccurredwhilecalculatingtheGlobal", "An error occurred while calculating the Global Statistics.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{anerroroccurredwhilecalculatingtheGlobal}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task WouldYouLikeToOpenTheLogMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var result = await ShowAsync(O,
            _localization.GetString("SimpleLauncherWasUnableToLaunchThisGame",
                "'Simple Launcher' was unable to launch this game.") + "\n\n" +
            _localization.GetString("Wouldyouliketoopentheerroruserlogfiletodebug",
                "Would you like to open the 'error_user.log' file to debug the error?"),
            _localization.GetString("Error", "Error"), MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(logPath))
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
                Log.Information(ex, "Failed to open the 'error_user.log' file. The file may not exist");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!"),
                    _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task CouldNotOpenTheDownloadLinkMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SimpleLaunchercouldnotopenthedownloadlink",
                "'Simple Launcher' could not open the download link."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task FileNoLongerExistsMessageBoxAsync(string fileNameWithExtension)
    {
        if (O != null)
        {
            await ShowAsync(O,
                fileNameWithExtension + _localization.GetString("nolongerexists", " no longer exists."),
                _localization.GetString("Notfound", "Not Found"), MessageButtons.Ok, MessageIcon.Warning);
        }
    }


    public Task SystemSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Systemsavedsuccessfully", "System saved successfully."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task UnsupportedArchitectureMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SimpleLauncherdoesnotsupportthecurrentprocessorarchitecture",
                "'Simple Launcher' does not support the current processor architecture. We only support 64-bit (x64) or ARM64. The application will now close."),
            _localization.GetString("UnsupportedArchitecture", "Unsupported Architecture"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task FailedToInjectBlastemConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToInjectBlastemConfiguration",
                "Failed to inject Blastem configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task DownloadExtractionFailedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var downloadorextractionfailed =
            _localization.GetString("DownloadorExtractionFailed", "Download or extraction failed.");
        var grantSimpleLauncheradministrativeaccess = _localization.GetString(
            "GrantSimpleLauncheradministrativeaccess", "Grant 'Simple Launcher' administrative access and try again.");
        var ensuretheSimpleLauncherfolder = _localization.GetString("EnsuretheSimpleLauncherfolder",
            "Ensure the 'Simple Launcher' folder is a writable directory.");
        var temporarilydisableyourantivirus = _localization.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{downloadorextractionfailed}\n\n" +
                            $"{grantSimpleLauncheradministrativeaccess}\n\n" +
                            $"{ensuretheSimpleLauncherfolder}\n\n" +
                            $"{temporarilydisableyourantivirus}", error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ProblemOpeningInfoLinkMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasaproblemopeningthe = _localization.GetString("Therewasaproblemopeningthe",
            "There was a problem opening the Info Link.");
        var ensureyouhaveadefaultbrowserinstalled = _localization.GetString("Ensureyouhaveadefaultbrowserinstalled",
            "Ensure you have a default browser installed and configured correctly on your system.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasaproblemopeningthe}\n\n{ensureyouhaveadefaultbrowserinstalled}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task FileParametersMdIsEmptyMessageBoxAsync()
    {
        if (O == null) return;
        var thefileparametersmdisempty =
            _localization.GetString("Thefileparametersmdisempty", "The file 'parameters.md' is empty.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{thefileparametersmdisempty}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task DuckStationEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("DuckStationemulatornotfound",
                "DuckStation emulator not found. Please locate the DuckStation executable."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SelectAFavoriteToRemoveMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Pleaseselectafavoritetoremove", "Please select a favorite to remove."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task FileMustBeCompressedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var whenExtractFileBeforeLaunch = _localization.GetString("WhenExtractFileBeforeLaunch",
            "When 'Extract File Before Launch' is set to true, 'Extension to Search in the System Folder' must include 'zip', '7z', or 'rar'.");
        var itwillnotacceptotherextensions =
            _localization.GetString("Itwillnotacceptotherextensions", "It will not accept other extensions.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{whenExtractFileBeforeLaunch}\n\n{itwillnotacceptotherextensions}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task<MessageBoxResult> WouldYouLikeToSaveAReportMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var wouldyouliketosaveareport = _localization.GetString("Wouldyouliketosaveareport",
            "Would you like to save a report with the results?");
        var saveReport = _localization.GetString("SaveReport", "Save Report");
        return await ShowAsync(O, wouldyouliketosaveareport, saveReport, MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task EmulatorConfiguredSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Emulatorconfiguredsuccessfullyfor",
                "Emulator configured successfully for RetroAchievements!"),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task GeolithDoesNotSupportCompressedFilesMessageBoxAsync()
    {
        if (O == null) return;
        var title = _localization.GetString("Error", "Error");
        var message1 = _localization.GetString("GeolithLibretroDllDoesNotSupportZIP1",
            "'geolith_libretro.dll' does not support ZIP, 7Z or RAR files.");
        var message2 = _localization.GetString("GeolithLibretroDllDoesNotSupportZIP2", "It only support NEO files.");
        var message3 = _localization.GetString("GeolithLibretroDllDoesNotSupportZIP3",
            "Please ensure you are running a compatible ROM set.");
        var message4 = _localization.GetString("GeolithLibretroDllDoesNotSupportZIP4",
            "Would you like to visit the url 'wiki.terraonion.com' to get more info about that?");
        var result = await ShowAsync(O, $"{message1}\n\n{message2}\n\n{message3}\n\n{message4}", title,
            MessageButtons.YesNo, MessageIcon.Warning);
        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Could not open browser");
                await ShowAsync(O,
                    $"{_localization.GetString("Couldnotopenbrowser", "Could not open browser: ")}{ex.Message}",
                    _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task SaveSystemFailedMessageBoxAsync(string? details = null)
    {
        if (O == null) return Task.CompletedTask;
        var failedToSaveSystem =
            _localization.GetString("FailedToSaveSystem", "Failed to save system configuration.");
        var checkPermissions = _localization.GetString("CheckFilePermissions",
            "Please check file permissions and ensure the file is not locked.");
        var errorDetails = _localization.GetString("ErrorDetails", "Details:");
        var error = _localization.GetString("Error", "Error");

        var message = $"{failedToSaveSystem}\n\n" +
                      $"{checkPermissions}";
        if (!string.IsNullOrEmpty(details)) message += $"\n\n{errorDetails} {details}";

        return ShowAsync(O, message, error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task Pcsx2ConfigurationInjectionPermissionErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var title = _localization.GetString("InjectionFailed", "Injection Failed");
        var message1 = _localization.GetString("Pcsx2ConfigPermissionError1",
            "Failed to inject PCSX2 configuration. The emulator is installed in a protected system directory.");
        var message2 = _localization.GetString("Pcsx2ConfigPermissionError2",
            "The configuration file could not be modified due to insufficient permissions.");
        var message3 = _localization.GetString("Pcsx2ConfigPermissionError3", "To fix this, either:");
        var message4 = _localization.GetString("Pcsx2ConfigPermissionError4",
            "1. Run Simple Launcher as administrator, or");
        var message5 = _localization.GetString("Pcsx2ConfigPermissionError5",
            @"2. Install PCSX2 in a user directory (e.g., C:\Users\YourName\PCSX2)");
        var message6 = _localization.GetString("Pcsx2ConfigPermissionError6",
            "The game will launch with the emulator's default settings.");
        return ShowAsync(O, $"{message1}\n\n{message2}\n\n{message3}\n{message4}\n{message5}\n\n" +
                            $"{message6}", title, MessageButtons.Ok, MessageIcon.Warning);
    }


    public async Task FailedToLoadHelpUserXmlMessageBoxAsync()
    {
        if (O == null) return;
        var unabletoloadhelpuserxml = _localization.GetString("Unabletoloadhelpuserxml",
            "Unable to load 'helpuser.xml'. The file may be corrupted.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{unabletoloadhelpuserxml}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task ElevationRequiredMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var message =
            $"{_localization.GetString("Therewasanerrorlaunchingthisgame", "There was an error launching this game.")}\n\n" +
            $"{_localization.GetString("ElevationRequired", "The requested operation requires elevation (Administrator privileges).")}\n\n" +
            $"{_localization.GetString("CannotRunProgramsRequiringElevation", "'Simple Launcher' cannot run programs that require elevation. Change the game's configuration to not require administrator rights (for example, uncheck 'Run this program as an administrator' in the executable's compatibility settings).")}";
        return ShowAsync(O, message, _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FileNeedToBeCompressedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var theselectedfilecannotbe =
            _localization.GetString("Theselectedfilecannotbe", "The selected file cannot be extracted.");
        var toextractafileitneedstobe = _localization.GetString("Toextractafileitneedstobe",
            "To extract a file, it needs to be a 7z, zip, or rar file.");
        var pleasefixthatintheEditwindow =
            _localization.GetString("PleasefixthatintheEditwindow", "Please fix that in the Edit window.");
        var warning = _localization.GetString("Warning", "Warning");
        return ShowAsync(O, $"{theselectedfilecannotbe}\n\n{toextractafileitneedstobe}\n\n" +
                            $"{pleasefixthatintheEditwindow}", warning, MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task NoStatsToSaveMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Nostatisticsavailabletosave", "No statistics available to save."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task AresConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("AresConfigurationSavedSuccessfully", "Ares configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedToInitializeSevenZipMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var anunexpectederroroccurredwhileinitializingthe7Ziplibrary = _localization.GetString(
            "Anunexpectederroroccurredwhileinitializingthe7Ziplibrary",
            "An unexpected error occurred while initializing the 7-Zip library.");
        var doyouwanttoreinstallSimpleLauncher =
            _localization.GetString("DoyouwanttoreinstallSimpleLauncher",
                "Do you want to reinstall 'Simple Launcher' to fix the issue?");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O,
            $"{anunexpectederroroccurredwhileinitializingthe7Ziplibrary}\n\n" +
            $"{doyouwanttoreinstallSimpleLauncher}", error, MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task GameLaunchTimeoutMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("GamelaunchtimedoutPleasetryagainorcheckiftheemulatorstarted",
                "Game launch timed out. Please try again or check if the emulator started."),
            _localization.GetString("Gamelaunchtimedout", "Game launch timed out"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task CouldNotFindUpdaterOnGitHubMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SimpleLaunchercouldnotfindtheupdater",
                "'Simple Launcher' could not find the updater application on GitHub."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ImageViewerErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var failedtoloadtheimageintheImage = _localization.GetString("FailedtoloadtheimageintheImage",
            "Failed to load the image in the Image Viewer window.");
        var theimagemaybecorruptedorinaccessible = _localization.GetString("Theimagemaybecorruptedorinaccessible",
            "The image may be corrupted or inaccessible.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{failedtoloadtheimageintheImage}\n\n{theimagemaybecorruptedorinaccessible}\n\n" +
                            $"{theerrorwasreportedtothedeveloper}", error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToInjectStellaConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectStellaconfiguration",
                "Failed to inject Stella configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ErrorMethodLoadGameFilesAsyncMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorloadingthegame = _localization.GetString("Therewasanerrorloadingthegame",
            "There was an error loading the game list.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorloadingthegame}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToSaveSettingsMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveSettings",
                "Failed to save settings. Please check that the application folder is writable and not locked by another process."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task TakeScreenShotMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O,
                _localization.GetString("PressPrintScreentocaptureascreenshot",
                    "Press Print Screen to capture a screenshot."),
                _localization.GetString("Screenshot", "Screenshot"), MessageButtons.Ok, MessageIcon.Information);
        }
    }


    public Task ThereIsNoCoverMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnocoverfileassociated",
                "There is no cover file associated with this game."),
            _localization.GetString("Covernotfound", "Cover not found"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedToSaveDolphinConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveDolphinConfiguration",
                "Failed to save Dolphin configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task ErrorOpeningUrlMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O, _localization.GetString("Couldnotopenthelink", "Could not open the link."),
                _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public async Task ReinstallSimpleLauncherFileCorruptedMessageBoxAsync()
    {
        if (O == null) return;

        var simpleLaunchercouldnotloadthefilemamedat = _localization.GetString(
            "SimpleLaunchercouldnotloadthefilemamedat",
            "'Simple Launcher' could not load the file 'mame.dat' or it is corrupted.");
        var doyouwanttoautomaticallyreinstall =
            _localization.GetString("DoyouwanttoautomaticallyreinstallSimpleLaunchertofixit",
                "Do you want to automatically reinstall 'Simple Launcher' to fix it?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O,
            $"{simpleLaunchercouldnotloadthefilemamedat}\n\n" +
            $"{doyouwanttoautomaticallyreinstall}", error, MessageButtons.YesNo, MessageIcon.Question);

        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
        else
            App.ServiceProvider.GetRequiredService<AvaloniaQuitSimpleLauncher>().SimpleQuitApplication();
    }


    public Task ErrorLoadingRomHistoryMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var anerroroccurredwhileloadingRoMhistory = _localization.GetString("AnerroroccurredwhileloadingROMhistory",
            "An error occurred while loading ROM history.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{anerroroccurredwhileloadingRoMhistory}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task GlobalSearchErrorMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O, _localization.GetString("Searcherror", "Search error."),
                _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public Task SystemAddedMessageBoxAsync(string systemName, string resolvedSystemFolder,
        string resolvedSystemImageFolder)
    {
        if (O == null) return Task.CompletedTask;
        var thesystem = _localization.GetString("Thesystem", "The system");
        var hasbeenaddedsuccessfully = _localization.GetString("hasbeenaddedsuccessfully",
            "has been added successfully.");
        var putRoMsorIsOsforthissysteminside = _localization.GetString("PutROMsorISOsforthissysteminside",
            "Put ROMs or ISOs for this system inside");
        var putcoverimagesforthissysteminside = _localization.GetString("Putcoverimagesforthissysteminside",
            "Put cover images for this system inside");
        var info = _localization.GetString("Info", "Info");
        return ShowAsync(O, $"{thesystem} '{systemName}' {hasbeenaddedsuccessfully}\n\n" +
                            $"{putRoMsorIsOsforthissysteminside} '{resolvedSystemFolder}'\n\n" +
                            $"{putcoverimagesforthissysteminside} '{resolvedSystemImageFolder}'.", info,
            MessageButtons.Ok, MessageIcon.Information);
    }


    public Task StellaConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("StellaConfigurationSavedSuccessfully",
                "Stella configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task GamePadErrorMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var therewasanerrorwiththeGamePadController = _localization.GetString("TherewasanerrorwiththeGamePadController",
            "There was an error with the GamePad Controller.");
        var grantSimpleLauncheradministrative = _localization.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var temporarilydisableyourantivirus = _localization.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var doyouwanttoopenthefile = _localization.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{therewasanerrorwiththeGamePadController}\n\n" +
                                        $"{grantSimpleLauncheradministrative}\n\n" +
                                        $"{temporarilydisableyourantivirus}\n\n" +
                                        $"{doyouwanttoopenthefile}", error, MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwas", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task OotakeDoesNotSupportImageFilesMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("OotakeemulatordoesnotsupportCHD",
                "Ootake emulator does not support CHD, ISO, CUE/BIN files."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task DiskSpaceErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Notenoughdiskspaceforextraction", "Not enough disk space for extraction."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToSaveSegaModel2ConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveSEGAModel2Configuration",
                "Failed to save SEGA Model 2 configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToInjectFlycastConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToInjectFlycastConfiguration",
                "Failed to inject Flycast configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ThereIsNoGameplaySnapshotMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnogameplaysnapshot",
                "There is no gameplay snapshot file associated with this game."),
            _localization.GetString("GameplaySnapshotnotfound", "Gameplay Snapshot not found"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task ErrorSettingSoundFileMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("errorSettingSoundFile", "Error choosing or copying sound file."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task DolphinEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("DolphinEmulatorNotFound",
                "Dolphin emulator not found. Please locate 'Dolphin.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task Rpcs3ConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("RPCS3ConfigurationSavedSuccessfully",
                "RPCS3 configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedToInjectDolphinConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToInjectDolphinConfiguration",
                "Failed to inject Dolphin configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task UnableToOpenLinkMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var unabletoopenthelink = _localization.GetString("Unabletoopenthelink", "Unable to open the link.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{unabletoopenthelink}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ProtocolHandlerNotRegisteredMessageBoxAsync(string protocol)
    {
        if (O == null) return Task.CompletedTask;
        var protocolHandlerNotRegistered = _localization.GetString("ProtocolHandlerNotRegistered",
            "Protocol handler for '{0}://' is not registered. Please ensure the associated application is installed.");
        var launchErrorTitle = _localization.GetString("LaunchErrorTitle", "Launch Error");
        var message = string.Format(CultureInfo.InvariantCulture, protocolHandlerNotRegistered, protocol);
        return ShowAsync(O, message, launchErrorTitle, MessageButtons.Ok, MessageIcon.Warning);
    }

    public async Task<MessageBoxResult> DoYouWantToUpdateMessageBoxAsync(string currentVersion, string latestVersion)
    {
        if (O == null) return MessageBoxResult.Cancel;
        var thereIsAsoftwareUpdateAvailable = _localization.GetString("Thereisasoftwareupdateavailable",
            "There is a software update available.");
        var theCurrentVersionIs = _localization.GetString("Thecurrentversionis", "The current version is");
        var theUpdateVersionIs = _localization.GetString("Theupdateversionis", "The update version is");
        var doYouWantToDownloadAndInstall = _localization.GetString("Doyouwanttodownloadandinstall",
            "Do you want to download and install the latest version automatically?");
        var updateAvailable = _localization.GetString("UpdateAvailable", "Update Available");
        return await ShowAsync(O, $"{thereIsAsoftwareUpdateAvailable}\n" +
                                  $"{theCurrentVersionIs} {currentVersion}\n" +
                                  $"{theUpdateVersionIs} {latestVersion}\n\n" +
                                  $"{doYouWantToDownloadAndInstall}", updateAvailable, MessageButtons.YesNo,
            MessageIcon.Information);
    }


    public Task CannotScreenshotMinimizedWindowMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Cannottakeascreenshotofaminimizedwindow",
                "Cannot take a screenshot of a minimized window."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task MoveToWritableFolderMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var itlookslikeSimpleLauncherisinstalled = _localization.GetString("ItlookslikeSimpleLauncherisinstalled",
            "It looks like 'Simple Launcher' is installed in a restricted folder (e.g., Program Files), where it does not have write access.");
        var itneedswriteaccesstoitsfolder =
            _localization.GetString("Itneedswriteaccesstoitsfolder", "It needs write access to its folder.");
        var pleasemovetheapplicationfolder = _localization.GetString("Pleasemovetheapplicationfolder",
            "Please move the application folder to a writable location like the 'Documents' folder.");
        var ifpossiblerunitwithadministrative = _localization.GetString("Ifpossiblerunitwithadministrative",
            "If possible, run it with administrative privileges.");
        var warning = _localization.GetString("Warning", "Warning");
        return ShowAsync(O, $"{itlookslikeSimpleLauncherisinstalled}\n\n{itneedswriteaccesstoitsfolder}\n\n" +
                            $"{pleasemovetheapplicationfolder}\n\n{ifpossiblerunitwithadministrative}", warning,
            MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task ReportSavedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Reportsavedsuccessfully", "Report saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task NoSystemInParametersMdMessageBoxAsync()
    {
        if (O == null) return;
        var novalidsystemsfoundinthefileparametersmd = _localization.GetString(
            "Novalidsystemsfoundinthefileparametersmd", "No valid systems found in the file 'parameters.md'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{novalidsystemsfoundinthefileparametersmd}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task AddSystemFailedMessageBoxAsync(string? details = null)
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerroradding =
            _localization.GetString("Therewasanerroradding", "There was an error adding this system.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        var errorDetails = _localization.GetString("ErrorDetails", "Details:");

        var message = $"{therewasanerroradding}\n\n" +
                      $"{theerrorwasreportedtothedeveloper}";
        if (!string.IsNullOrEmpty(details)) message += $"\n\n{errorDetails} {details}";

        return ShowAsync(O, message, error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task InvalidSystemConfigMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorwhileloading = _localization.GetString("Therewasanerrorwhileloading",
            "There was an error while loading the system configuration for this system.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorwhileloading}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task BlastemConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("BlastemConfigurationSavedSuccessfully",
                "Blastem configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task MesenEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Mesenemulatornotfound", "Mesen emulator not found. Please locate 'Mesen.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ToggleGamepadFailureMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var failedtotogglegamepad = _localization.GetString("Failedtotogglegamepad", "Failed to toggle gamepad.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{failedtotogglegamepad}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task RetroArchSpecialCharactersInPathMessageBoxAsync()
    {
        if (O == null) return;

        var message1 = _localization.GetString("RetroArchSpecialCharactersInPath1",
            "The emulator could not launch the game because the file path contains special characters (for example: ´, `, ~, !, ?).");
        var message2 = _localization.GetString("RetroArchSpecialCharactersInPath2",
            "RetroArch cannot create its required folders in paths with these characters.");
        var message3 = _localization.GetString("RetroArchSpecialCharactersInPath3",
            @"To fix this, please move your emulator and your game files to a folder that uses only standard letters and numbers, such as C:\Games\.");
        var error = _localization.GetString("Error", "Error");
        await ShowAsync(O, $"{message1}\n\n{message2}\n\n{message3}", error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ExtensionToLaunchIsRequiredMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var extensiontoLaunchAfterExtraction = _localization.GetString("ExtensiontoLaunchAfterExtraction",
            "'Extension to Launch After Extraction' is required when 'Extract File Before Launch' is set to true.");
        var pleasefixthisfield = _localization.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{extensiontoLaunchAfterExtraction}\n\n{pleasefixthisfield}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task RetroArchemulatorpathnotfoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("RetroArchemulatorpathnotfoundPlease",
                "RetroArch emulator path not found. Please select 'retroarch.exe' to apply these settings."),
            _localization.GetString("EmulatorRequired", "Emulator Required"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task EmulatorNameIsRequiredMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var emulatornameisrequired =
            _localization.GetString("Emulatornameisrequired", "Emulator name is required.");
        var pleasefixthat = _localization.GetString("Pleasefixthat", "Please fix that.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{emulatornameisrequired}\n\n{pleasefixthat}", error, MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task EmulatorNameMustBeUniqueMessageBoxAsync(string emulatorName)
    {
        if (O == null) return Task.CompletedTask;
        var thename = _localization.GetString("Thename", "The name");
        var isusedmultipletimes = _localization.GetString("isusedmultipletimes",
            "is used multiple times. Each emulator name must be unique.");
        var info = _localization.GetString("Info", "Info");
        return ShowAsync(O, $"{thename} '{emulatorName}' {isusedmultipletimes}", info, MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task FailedToInjectMameConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectMAMEconfiguration",
                "Failed to inject MAME configuration. The error has been logged. Please check the emulator path and try again."),
            _localization.GetString("InjectionError", "Injection Error"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task ImagePackDownloaderUnavailableMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SimpleLaunchercouldnotaccesstheWebAPI",
                "'Simple Launcher' could not access the Web API to download the updated URLs. Please try again later."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task<bool> BatchFilePathsMissingMessageBoxAsync(IList<string> missingPaths)
    {
        if (O == null) return false;
        var batchfilepathsmissing =
            _localization.GetString("Batchfilepathsmissing", "The batch file references paths that do not exist:");
        var batchfilepathsmissingexplanation = _localization.GetString(
            "ThismaycausethebatchfiletofailNotallpathsmaybedetectedthisisabesteffortcheck",
            "This may cause the batch file to fail. Not all paths may be detected - this is a best-effort check.");
        var doyouwanttocontinueanyway =
            _localization.GetString("Doyouwanttocontinueanyway", "Do you want to continue anyway?");
        var warning = _localization.GetString("Warning", "Warning");

        var pathsList = string.Join("\n", missingPaths.Select(static p => $"  - {p}"));
        var message = $"{batchfilepathsmissing}\n\n{pathsList}\n\n" +
                      $"{batchfilepathsmissingexplanation}\n\n" +
                      $"{doyouwanttocontinueanyway}";
        var result = await ShowAsync(O, message, warning, MessageButtons.YesNo, MessageIcon.Question);
        return result == MessageBoxResult.Yes;
    }


    public Task SelectAGameToLaunchMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Pleaseselectagametolaunch", "Please select a game to launch."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task YumirEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("YumirConfig_PathNotFound", "Yumir executable not found. Please select it."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task DokanDriverNotInstalledMessageBoxAsync()
    {
        if (O == null) return;
        var dokanDriverNotFound = _localization.GetString("DokanDriverNotFound",
            "The Dokan file system driver (dokan2.dll) is required to mount archives as virtual drives. It does not appear to be installed on this system.");
        var doYouWantToOpenBrowser =
            _localization.GetString("DoyouwanttoopenyourbrowsertodownloadDokan",
                "Do you want to open your browser to download Dokan?");
        var error = _localization.GetString("Error", "Error");
        var messageBoxResult = await ShowAsync(O, $"{dokanDriverNotFound}\n\n{doYouWantToOpenBrowser}", error,
            MessageButtons.YesNo, MessageIcon.Question);

        // WPF parity: open the configured Dokan page only when the user agrees.
        if (messageBoxResult != MessageBoxResult.Yes) return;

        var downloadPageUrl = _configuration.GetValue<string>("Urls:DokanyWebsite")
                              ?? "https://github.com/dokan-dev/dokany";
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
            // Expected condition (no default browser / headless session): not a bug report.
            Log.Information(ex, "Could not open the Dokan website");
            await ShowAsync(O,
                _localization.GetString("Anerroroccurredwhileopeningyourbrowser",
                    "An error occurred while opening your browser."),
                error, MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public async Task FileIsLockedMessageBoxAsync(string? tempFolderPath)
    {
        if (O == null) return;
        Log.Debug("File is locked. Temp folder: {TempFolderPath}", tempFolderPath);
        var downloadedFileIsLocked =
            _localization.GetString("Downloadedfileislocked", "Downloaded file is locked.");
        var grantAdministrative = _localization.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var disableAntivirus = _localization.GetString("Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var ensureWritable = _localization.GetString("EnsuretheSimpleLauncher",
            "Ensure the 'Simple Launcher' folder is a writable directory.");
        var openTempFolderQuestion = _localization.GetString("OpenTempFolderQuestion",
            "Would you like to open the temporary folder to inspect the file?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O,
            $"{downloadedFileIsLocked}\n\n{grantAdministrative}\n\n{disableAntivirus}\n\n{ensureWritable}\n\n{openTempFolderQuestion}",
            error, MessageButtons.YesNo, MessageIcon.Question);

        if (result != MessageBoxResult.Yes || string.IsNullOrWhiteSpace(tempFolderPath)) return;

        // WPF parity: open the temp folder and surface an error only when that fails.
        if (await ExternalLinkHelper.TryOpenFolderAsync(tempFolderPath, O)) return;

        await ShowAsync(O,
            _localization.GetString("ErrorOpeningFolderMessage", "Could not open the temporary folder."),
            _localization.GetString("ErrorOpeningFolderTitle", "Error Opening Folder"),
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SupermodelConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Supermodelconfigurationsavedsuccessfully",
                "Supermodel configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task SupportRequestSendErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var anerroroccurredwhilesendingthesupportrequest = _localization.GetString(
            "Anerroroccurredwhilesendingthesupportrequest", "An error occurred while sending the support request.");
        var thebugwasreportedtothedeveloper = _localization.GetString("Thebugwasreportedtothedeveloper",
            "The bug was reported to the developer that will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{anerroroccurredwhilesendingthesupportrequest}\n\n" +
                            $"{thebugwasreportedtothedeveloper}", error, MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task ErrorWhileLoadingParametersMdMessageBoxAsync()
    {
        if (O == null) return;
        var unexpectederrorwhileloadingparametersmd = _localization.GetString(
            "Unexpectederrorwhileloadingparametersmd", "Unexpected error while loading 'parameters.md'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{unexpectederrorwhileloadingparametersmd}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task ThereIsNoWalkthroughMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnowalkthrough",
                "There is no walkthrough file associated with this game."),
            _localization.GetString("Walkthroughnotfound", "Walkthrough not found"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public async Task ErrorWhileAddingFavoritesMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O, _localization.GetString("Erroraddingtofavorites", "Error adding to favorites."),
                _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public Task MameConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("MAMEconfigurationinjectedsuccessfully",
                "MAME configuration injected successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task FileHelpUserXmlIsMissingMessageBoxAsync()
    {
        if (O == null) return;
        var thefilehelpuserxmlismissing =
            _localization.GetString("Thefilehelpuserxmlismissing", "The file 'helpuser.xml' is missing.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{thefilehelpuserxmlismissing}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public async Task GameIsAlreadyInFavoritesMessageBoxAsync(string fileNameWithExtension)
    {
        if (O != null)
        {
            await ShowAsync(O,
                $"{fileNameWithExtension} {_localization.GetString("isalreadyinfavorites", "is already in favorites.")}",
                _localization.GetString("AlreadyFavorited", "Already Favorited"), MessageButtons.Ok,
                MessageIcon.Information);
        }
    }


    public Task DownloadedFileIsMissingMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var downloadedfileismissing =
            _localization.GetString("Downloadedfileismissing", "Downloaded file is missing.");
        var oneDriveIssue = _localization.GetString("oneDriveIssue",
            "If the file is in OneDrive, ensure it is synced and downloaded to your device. Right-click the file in File Explorer and select 'Always keep on this device'.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{downloadedfileismissing}\n\n{oneDriveIssue}", error, MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task DaphnesettingssavedsuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Daphnesettingssavedsuccessfully", "Daphne settings saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task ApplicationControlPolicyBlockedManualLinkMessageBoxAsync(string url)
    {
        if (O == null) return;
        var message = _localization.GetString("ApplicationControlPolicyBlockedFileManualLink",
            "An application control policy blocked this link.");
        var simpleLaunchercannotperform = _localization.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var theUrLwascopiedtotheclipboard = _localization.GetString("TheURLwascopiedtotheclipboard",
            "The URL was copied to the clipboard for your convenience. You can paste it into your browser.");
        var securityPolicyBlocked = _localization.GetString("SecurityPolicyBlocked", "Security Policy Blocked");
        await ShowAsync(O, $"{message}\n\n{simpleLaunchercannotperform}\n\n{theUrLwascopiedtotheclipboard}",
            securityPolicyBlocked, MessageButtons.Ok, MessageIcon.Warning);

        if (O.Clipboard is { } clipboard)
        {
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.CreateText(url));
            await clipboard.SetDataAsync(dataTransfer); // Copy URL to clipboard
        }
    }


    public Task DeadZonesRevertedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("DeadZonesRevertedToDefaultValues", "DeadZone values reverted to default values."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task ShowDownloadFileLockedMessageBoxAsync(string tempFolderPath)
    {
        if (O == null) return Task.CompletedTask;
        Log.Debug("Download file is locked. Temp folder: {TempFolderPath}", tempFolderPath);
        var downloadFileLockedMessage = _localization.GetString("DownloadFileLockedMessage",
            "The download could not be completed because the temporary file is locked by another process (e.g., antivirus software).");
        var openTempFolderQuestion = _localization.GetString("OpenTempFolderQuestion",
            "Would you like to open the temporary folder to inspect the file?");
        var downloadFailedTitle = _localization.GetString("DownloadFailedTitle", "Download Failed");
        return ShowAsync(O, $"{downloadFileLockedMessage}\n\n{openTempFolderQuestion}", downloadFailedTitle,
            MessageButtons.YesNo, MessageIcon.Question);
    }

    public async Task<MessageBoxResult> FirstRunWelcomeMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var welcomeToSimpleLauncher =
            _localization.GetString("WelcomeToSimpleLauncher", "Welcome to 'Simple Launcher'!");
        var noSystemsFound =
            _localization.GetString("NoSystemsFound", "No systems were found in your configuration.");
        var easyModeGuide = _localization.GetString("DoyouwanttoaddyourfirstsystemusingtheEasyMode",
            "Do you want to add your first system using the Easy Mode?");
        var welcome = _localization.GetString("Welcome", "Welcome");
        return await ShowAsync(O, $"{welcomeToSimpleLauncher}\n\n{noSystemsFound}\n\n{easyModeGuide}", welcome,
            MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task SettingsSavedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("SettingsSaved", "Settings saved."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task CouldNotOpenWalkthroughMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var failedtoopenthewalkthroughfile =
            _localization.GetString("Failedtoopenthewalkthroughfile", "Failed to open the walkthrough file.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{failedtoopenthewalkthroughfile}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task XeniaconfigurationinjectedsuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Xeniaconfigurationinjectedsuccessfully",
                "Xenia configuration injected successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FileSystemXmlIsLockedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thefilesystemxmlislocked",
                "The file 'system.xml' is locked or inaccessible by another process."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task MameUnableToLoadImageMessageBoxAsync()
    {
        if (O == null) return;

        var title = _localization.GetString("UnableToLoadImage", "Unable to Load Image");
        var message1 = _localization.GetString("MameUnableToLoadImageError1",
            "MAME emulator could not load the image file.");
        var message2 = _localization.GetString("MameUnableToLoadImageError2",
            "MAME is very restrictive about the filename of the game.");
        var message3 = _localization.GetString("MameUnableToLoadImageError3",
            "The filename of your game must match the expected filename to run on MAME.");
        var message4 = _localization.GetString("MameUnableToLoadImageError4",
            "Please ensure you are running a compatible ROM set.");
        var message5 = _localization.GetString("MameUnableToLoadImageError5",
            "Would you like to visit the PleasureDome website to download a compatible ROM set?");
        var result = await ShowAsync(O,
            $"{message1}\n\n" +
            $"{message2}\n\n" +
            $"{message3}\n\n" +
            $"{message4}\n\n" +
            $"{message5}", title, MessageButtons.YesNo, MessageIcon.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var url = _configuration.GetValue<string>("Urls:PleasureDomeWebsite") ??
                          "https://pleasuredome.github.io/pleasuredome/index.html";
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not open browser");
            }
        }
    }


    public Task NoPdfViewerInstalledMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var nopdfviewerinstalled =
            _localization.GetString("NoPDFViewerInstalled", "No PDF viewer is installed on your system.");
        var pleaseinstallapdfviewer = _localization.GetString("PleaseInstallAPDFViewer",
            "Please install a PDF viewer (such as Adobe Acrobat Reader, Sumatra PDF, or Microsoft Edge) to open this file.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{nopdfviewerinstalled}\n\n{pleaseinstallapdfviewer}", error, MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task SelectAHistoryItemToRemoveMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SelectAHistoryItemToRemove", "Please select a history item to remove."),
            _localization.GetString("Pleaseselectaitem", "Please select a item"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task XeniaemulatorpathnotfoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Xeniaemulatorpathnotfound",
                "Xenia emulator path not found. Please select 'xenia.exe' or 'xenia_canary.exe' to apply these settings."),
            _localization.GetString("EmulatorRequired", "Emulator Required"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task EnterEmailMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Pleaseentertheemail", "Please enter the email."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task FileParametersMdIsMissingMessageBoxAsync()
    {
        if (O == null) return;
        var thefileparametersmdismissing =
            _localization.GetString("Thefileparametersmdismissing", "The file 'parameters.md' is missing.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{thefileparametersmdismissing}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task PowerShellExecutionPolicyRestrictionsMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var unabletoscanMicrosoftStoregames = _localization.GetString("UnabletoscanMicrosoftStoregames",
            "Unable to scan Microsoft Store games due to PowerShell execution policy restrictions.");
        var thisistypicallycausedbyGroupPolicy = _localization.GetString("ThisistypicallycausedbyGroupPolicy",
            "This is typically caused by Group Policy settings on corporate or managed PCs.");
        var simpleLaunchercannotperform = _localization.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var powerShellRestricted = _localization.GetString("PowerShellRestricted", "PowerShell Restricted");
        return ShowAsync(O, $"{unabletoscanMicrosoftStoregames}\n\n{thisistypicallycausedbyGroupPolicy}\n\n" +
                            $"{simpleLaunchercannotperform}", powerShellRestricted, MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task FailedToInjectSegaModel2ConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToInjectSEGAModel2Configuration",
                "Failed to inject SEGA Model 2 configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task Emulator1RequiredMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var emulator1Nameisrequired =
            _localization.GetString("Emulator1Nameisrequired", "'Emulator 1 Name' is required.");
        var pleasefixthisfield = _localization.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{emulator1Nameisrequired}\n\n{pleasefixthisfield}", error, MessageButtons.Ok,
            MessageIcon.Error);
    }

    public async Task<MessageBoxResult> WouldYouLikeToRestoreTheLastBackupMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var icouldnotfindthefilesystemxml = _localization.GetString("Icouldnotfindthefilesystemxml",
            "I could not find the file 'system.xml', which is required to start the application.");
        var butIfoundabackupfile = _localization.GetString("ButIfoundabackupfile", "But I found a backup file.");
        var wouldyouliketorestore =
            _localization.GetString("Wouldyouliketorestore", "Would you like to restore the last backup?");
        var restoreBackup = _localization.GetString("RestoreBackup", "Restore Backup?");
        return await ShowAsync(O, $"{icouldnotfindthefilesystemxml}\n\n{butIfoundabackupfile}\n\n" +
                                  $"{wouldyouliketorestore}", restoreBackup, MessageButtons.YesNo,
            MessageIcon.Question);
    }


    public async Task InstallUpdateManuallyMessageBoxAsync()
    {
        if (O == null) return;
        var therewasanerrorinstallingorupdating = _localization.GetString("Therewasanerrorinstallingorupdating",
            "There was an error installing or updating the application.");
        var wouldyouliketoberedirectedtothedownloadpage = _localization.GetString(
            "Wouldyouliketoberedirectedtothedownloadpage",
            "Would you like to be redirected to the download page to install or update it manually?");
        var error = _localization.GetString("Error", "Error");
        var messageBoxResult = await ShowAsync(O, $"{therewasanerrorinstallingorupdating}\n\n" +
                                                  $"{wouldyouliketoberedirectedtothedownloadpage}", error,
            MessageButtons.YesNo, MessageIcon.Question);

        if (messageBoxResult != MessageBoxResult.Yes) return;

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
            // Expected condition (no default browser): not a bug report.
            Log.Information(ex, "Error in method InstallUpdateManuallyMessageBoxAsync");
            var anerroroccurredwhileopeningthebrowser = _localization.GetString(
                "Anerroroccurredwhileopeningthebrowser", "An error occurred while opening the browser.");
            var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
                "The error was reported to the developer who will try to fix the issue.");
            await ShowAsync(O, $"{anerroroccurredwhileopeningthebrowser}\n\n{theerrorwasreportedtothedeveloper}",
                error, MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public Task ToggleFuzzyMatchingFailureMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Therewasanerrortogglingthefuzzymatchinglogic",
                "There was an error toggling the fuzzy matching logic."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task NoDefaultBrowserConfiguredMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("NoDefaultBrowserConfiguredMessage",
                "Your operating system does not have a default web browser configured. Please set one in Windows Settings (Apps > Default apps) to open web links."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task ErrorWhileLoadingHelpUserXmlMessageBoxAsync()
    {
        if (O == null) return;
        var unexpectederrorwhileloadinghelpuserxml =
            _localization.GetString("Unexpectederrorwhileloadinghelpuserxml",
                "Unexpected error while loading 'helpuser.xml'.");
        var doyouwanttoautomaticreinstallSimpleLauncher = _localization.GetString(
            "DoyouwanttoautomaticreinstallSimpleLauncher",
            "Do you want to automatic reinstall 'Simple Launcher' to fix it.");
        var error = _localization.GetString("Error", "Error");
        var result = await ShowAsync(O, $"{unexpectederrorwhileloadinghelpuserxml}\n\n" +
                                        $"{doyouwanttoautomaticreinstallSimpleLauncher}", error,
            MessageButtons.YesNo, MessageIcon.Question);
        if (result == MessageBoxResult.Yes)
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
    }


    public Task RightClickContextMenuErrorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorintherightclick = _localization.GetString("Therewasanerrorintherightclick",
            "There was an error in the right-click context menu.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorintherightclick}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task YumirConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("YumirConfigurationSavedSuccessfully",
                "Yumir configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedtoinjectAresconfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectAresconfiguration",
                "Failed to inject Ares configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ThereIsNoCartMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnocartfile", "There is no cart file associated with this game."),
            _localization.GetString("Cartnotfound", "Cart not found"), MessageButtons.Ok, MessageIcon.Information);
    }

    public async Task<MessageBoxResult> GroupByFolderWarningMessageBoxAsync()
    {
        if (O == null) return MessageBoxResult.Cancel;
        var message = _localization.GetString("YouhaveenabledGroupFilesbyFolderbuthave",
            "You have enabled 'Group Files by Folder' but have configured neither a MAME nor a DOSBox emulator. This option is only compatible with MAME (Software List CHDs) or DOSBox (uncompressed game folders). Are you sure you want to save these settings?");
        var title = _localization.GetString("ConfigurationWarning", "Configuration Warning");
        return await ShowAsync(O, message, title, MessageButtons.YesNo, MessageIcon.Warning);
    }


    public Task LaunchToolInformationMessageBoxAsync(string info)
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, info, _localization.GetString("Error", "Error"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public async Task SystemXmlNotFoundMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O,
                $"{_localization.GetString("systemxmlnotfound", "'system.xml' not found inside the application folder.")}\n\n" +
                $"{_localization.GetString("PleaserestartSimpleLauncher", "Please restart 'Simple Launcher'.")}\n\n" +
                $"{_localization.GetString("Ifthatdoesnotwork", "If that does not work, please reinstall 'Simple Launcher'.")}",
                _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public Task SystemFolderCanNotBeEmptyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var systemFoldercannotbeempty = _localization.GetString("SystemFoldercannotbeempty",
            "'System Folder' cannot be empty or contain only spaces.");
        var pleasefixthisfield = _localization.GetString("Pleasefixthisfield", "Please fix this field.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{systemFoldercannotbeempty}\n\n{pleasefixthisfield}", error, MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task InvalidImageFormatMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("InvalidImageFormat", "Only PNG, JPG, and JPEG images are supported."),
            _localization.GetString("InvalidImageFormatTitle", "Invalid Image Format"), MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task FailedToSaveMednafenConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveMednafenConfiguration",
                "Failed to save Mednafen configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task FileSystemXmlIsCorruptedMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var thefilesystemxmlisbadlycorrupted = _localization.GetString("Thefilesystemxmlisbadlycorrupted",
            "The file 'system.xml' is badly corrupted.");
        var wouldyouliketoopentheerroruserlog = _localization.GetString("Wouldyouliketoopentheerroruserlog",
            "Would you like to open the 'error_user.log' file to investigate the issue?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{thefilesystemxmlisbadlycorrupted}\n\n{wouldyouliketoopentheerroruserlog}",
            error, MessageButtons.YesNo, MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public async Task MameUnknownSystemErrorMessageBoxAsync()
    {
        if (O == null) return;

        var title = _localization.GetString("UnknownSystemError", "Unknown System Error");
        var message1 = _localization.GetString("MameUnknownSystemError1",
            "MAME emulator could not find a matching compatible system to launch.");
        var message2 = _localization.GetString("MameUnknownSystemError2",
            "MAME is very restrictive about the filename of the game.");
        var message3 = _localization.GetString("MameUnknownSystemError3",
            "The filename of your game must match the expected filename to run on MAME.");
        var message4 = _localization.GetString("MameUnknownSystemError4",
            "Please ensure you are running a compatible ROM set.");
        var message5 = _localization.GetString("MameUnknownSystemError5",
            "Would you like to visit the PleasureDome website to download a compatible ROM set?");
        var result = await ShowAsync(O,
            $"{message1}\n\n" +
            $"{message2}\n\n" +
            $"{message3}\n\n" +
            $"{message4}\n\n" +
            $"{message5}", title, MessageButtons.YesNo, MessageIcon.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var url = _configuration.GetValue<string>("Urls:PleasureDomeWebsite") ??
                          "https://pleasuredome.github.io/pleasuredome/index.html";
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not open browser");
            }
        }
    }


    public Task MameEmulatorPathNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("MAMEemulatorpathnotfoundPleaseselect",
                "MAME emulator path not found. Please select 'mame.exe' or 'mame64.exe' to apply these settings."),
            _localization.GetString("EmulatorRequired", "Emulator Required"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task EnterUsernamePasswordMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("EnterUsernamePassword",
                "Please enter your RetroAchievements username and password first."),
            _localization.GetString("MissingInformation", "Missing Information"), MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task InvalidFolderCharactersMessageBoxAsync(string invalidChars)
    {
        if (O == null) return Task.CompletedTask;
        var systemFoldercontainsinvalid = _localization.GetString("SystemFoldercontainsinvalid",
            "'System Folder' contains invalid characters:");
        var pleaseRemoveTheseCharacters = _localization.GetString("PleaseRemoveTheseCharacters",
            "Please remove these characters and try again.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{systemFoldercontainsinvalid}\n\n{invalidChars}\n\n{pleaseRemoveTheseCharacters}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task CouldNotOpenSoundConfigurationWindowMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("CouldNotOpenSoundConfigurationWindow",
                "Could not open sound configuration window"),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public async Task<bool> CustomQuestionMessageBoxAsync(string title, string message)
    {
        if (O != null)
        {
            var r = await ShowAsync(O, message, title, MessageButtons.YesNo, MessageIcon.Question);
            return r == MessageBoxResult.Yes;
        }

        return false;
    }


    public Task ThereIsNoManualMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnomanual", "There is no manual associated with this file."),
            _localization.GetString("Manualnotfound", "Manual not found"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task MesenConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("MesenConfigurationSavedSuccessfully",
                "Mesen configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FailedToSaveFlycastConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToSaveFlycastConfiguration",
                "Failed to save Flycast configuration. Please check file permissions."),
            _localization.GetString("SaveFailed", "Save Failed"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedtoinjectRetroArchconfiguration2MessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedtoinjectRetroArchconfigurationTheerrorhas",
                "Failed to inject RetroArch configuration. The error has been logged."),
            _localization.GetString("InjectionError", "Injection Error"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public async Task<MessageBoxResult> ScanGamePathForRetroAchievementsMessageBoxAsync()
    {
        if (O != null)
        {
            return await ShowAsync(O,
                _localization.GetString("WeNeedToScanYourGamePath",
                    "We need to scan your game path to see what game is compatible with RetroAchievements."),
                _localization.GetString("RetroAchievements", "RetroAchievements"), MessageButtons.YesNo,
                MessageIcon.Question);
        }

        return MessageBoxResult.Cancel;
    }


    public Task SelectSystemBeforeSearchMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Pleaseselectasystembeforesearching",
                "Please select a system before searching."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public async Task UpdaterLaunchFailedMessageBoxAsync()
    {
        if (O == null) return;
        var updaterLaunchFailed = _localization.GetString("UpdaterLaunchFailed", "Failed to launch the Updater.");
        var accessDeniedExplanation = _localization.GetString("AccessDeniedExplanation",
            "This may be due to insufficient permissions or Windows security settings blocking the file.");
        var wouldyouliketoberedirectedtothedownloadpage = _localization.GetString(
            "Wouldyouliketoberedirectedtothedownloadpage",
            "Would you like to be redirected to the download page to install or update it manually?");
        var error = _localization.GetString("Error", "Error");
        var messageBoxResult = await ShowAsync(O, $"{updaterLaunchFailed}\n\n{accessDeniedExplanation}\n\n" +
                                                  $"{wouldyouliketoberedirectedtothedownloadpage}", error,
            MessageButtons.YesNo, MessageIcon.Question);

        if (messageBoxResult != MessageBoxResult.Yes) return;

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
            // Expected condition (no default browser): not a bug report.
            Log.Information(ex, "Error in method UpdaterLaunchFailedMessageBoxAsync");
            var anerroroccurredwhileopeningthebrowser = _localization.GetString(
                "Anerroroccurredwhileopeningthebrowser", "An error occurred while opening the browser.");
            var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
                "The error was reported to the developer who will try to fix the issue.");
            await ShowAsync(O, $"{anerroroccurredwhileopeningthebrowser}\n\n{theerrorwasreportedtothedeveloper}",
                error, MessageButtons.Ok, MessageIcon.Error);
        }
    }


    public async Task MameRomSetErrorMessageBoxAsync()
    {
        if (O == null) return;

        var title = _localization.GetString("ROMFilesNotFound", "ROM Files Not Found");
        var message1 = _localization.GetString("MameRomSetError1",
            "MAME emulator could not find required files to launch this game.");
        var message2 = _localization.GetString("MameRomSetError2x",
            "MAME is very restrictive about the filename of the game.");
        var message3 = _localization.GetString("MameRomSetError3",
            "Please ensure you are running a compatible ROM set.");
        var message4 = _localization.GetString("MameRomSetError4",
            "Would you like to visit the PleasureDome website to download a compatible ROM set?");
        var result = await ShowAsync(O,
            $"{message1}\n\n" +
            $"{message2}\n\n" +
            $"{message3}\n\n" +
            $"{message4}", title, MessageButtons.YesNo, MessageIcon.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var url = _configuration.GetValue<string>("Urls:PleasureDomeWebsite") ??
                          "https://pleasuredome.github.io/pleasuredome/index.html";
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not open browser");
            }
        }
    }


    public Task EnterSupportRequestMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Pleaseenterthedetailsofthesupportrequest",
                "Please enter the details of the support request."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task FuzzyMatchingErrorFailToSetThresholdMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SetFuzzyMatchingThresholdFailureMessageBoxText",
                "Failed to set fuzzy matching threshold."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ErrorChangingViewModeMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasanerrorwhilechangingtheviewmode = _localization.GetString(
            "Therewasanerrorwhilechangingtheviewmode", "There was an error while changing the view mode.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasanerrorwhilechangingtheviewmode}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task DownloadAndExtractionWereSuccessfulMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Downloadandextractioncompletedsuccessfully",
                "Download and extraction completed successfully."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task EmulatorPathNotConfiguredMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var emulatorPathNotConfigured =
            _localization.GetString("EmulatorPathNotConfigured", "The emulator path is not configured.");
        var emulatorPathNotConfiguredDetails1 = _localization.GetString("EmulatorPathNotConfiguredDetails1",
            "The emulator you are using does not have a valid executable path configured.");
        var emulatorPathNotConfiguredDetails2 =
            _localization.GetString("EmulatorPathNotConfiguredDetails2", "This typically happens when:");
        var emulatorPathNotConfiguredDetails3 = _localization.GetString("EmulatorPathNotConfiguredDetails3",
            "- The system was configured to run directly executable files (.bat, .exe, .lnk)");
        var emulatorPathNotConfiguredDetails4 = _localization.GetString("EmulatorPathNotConfiguredDetails4",
            "- But you are trying to launch a file that requires an emulator");
        var emulatorPathNotConfiguredDetails5 = _localization.GetString("EmulatorPathNotConfiguredDetails5",
            "Please edit the system configuration and provide a valid emulator path.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{emulatorPathNotConfigured}\n{emulatorPathNotConfiguredDetails1}\n\n" +
                            $"{emulatorPathNotConfiguredDetails2}\n{emulatorPathNotConfiguredDetails3}\n" +
                            $"{emulatorPathNotConfiguredDetails4}\n\n{emulatorPathNotConfiguredDetails5}", error,
            MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task EmulatorExecutableNotFoundMessageBoxAsync(string configuredPath)
    {
        if (O == null) return Task.CompletedTask;
        var details1 = _localization.GetString("EmulatorExecutableNotFoundDetails1",
            "The emulator executable was not found at the configured path:");
        var details2 = _localization.GetString("EmulatorExecutableNotFoundDetails2",
            "The emulator may have been moved, renamed, or deleted.");
        var details3 = _localization.GetString("EmulatorExecutableNotFoundDetails3",
            "Please edit the system configuration and select the emulator executable again.");
        return ShowAsync(O,
            $"{details1}\n{configuredPath}\n\n{details2}\n{details3}",
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task InvalidExecutableFileMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var details1 = _localization.GetString("InvalidExecutableFileDetails1",
            "The file could not be run because it is not a valid application for this operating system.");
        var details2 = _localization.GetString("InvalidExecutableFileDetails2",
            "The file may be corrupted, incomplete, or designed for a different platform.");
        var details3 = _localization.GetString("InvalidExecutableFileDetails3",
            "If this is a game for another system, please configure an emulator for that system and launch the game through it.");
        return ShowAsync(O,
            $"{details1}\n\n{details2}\n{details3}",
            _localization.GetString("InvalidExecutableFile", "Invalid Executable File"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task ThereIsNoCabinetMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnocabinetfile", "There is no cabinet file associated with this game."),
            _localization.GetString("Cabinetnotfound", "Cabinet not found"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task CouldNotLaunchGameDueToDepViolationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var title = _localization.GetString("Error", "Error");
        var message = _localization.GetString("CouldNotLaunchGameDueToDepViolation",
            "The game failed to launch due to a DEP (Data Execution Prevention) violation.");
        var message2 = _localization.GetString("CouldNotLaunchGameDueToDepViolation2",
            "This is a Windows security feature that prevents programs from executing code in protected memory regions.");
        var message3 = _localization.GetString("CouldNotLaunchGameDueToDepViolation3",
            "Ensure you're using the latest emulator version with improved security compatibility.");
        var message4 = _localization.GetString("CouldNotLaunchGameDueToDepViolation4",
            "You can also try to switch to a different emulator or core.");
        return ShowAsync(O, $"{message}\n\n{message2}\n\n{message3}\n\n{message4}", title,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public Task MednafenEmulatorNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Mednafenemulatornotfound",
                "Mednafen emulator not found. Please locate 'mednafen.exe'."),
            _localization.GetString("EmulatorNotFound", "Emulator Not Found"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task EnterValidSearchTermsMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("EnterValidSearchTerms", "Please enter valid search terms."),
            _localization.GetString("InvalidSearch", "Invalid Search"), MessageButtons.Ok, MessageIcon.Warning);
    }


    public Task DeadZonesSavedMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("DeadZonevaluessavedsuccessfully", "DeadZone values saved successfully."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task EmulatorNameRequiredMessageBoxAsync(int i)
    {
        if (O == null) return Task.CompletedTask;
        var emulator = _localization.GetString("Emulator", "Emulator");
        var nameisrequiredbecauserelateddata = _localization.GetString("nameisrequiredbecauserelateddata",
            "name is required because related data has been provided.");
        var pleasefixthisfield = _localization.GetString("Pleasefixthisfield", "Please fix this field.");
        var info = _localization.GetString("Info", "Info");
        return ShowAsync(O, $"{emulator} {i} {nameisrequiredbecauserelateddata}\n\n{pleasefixthisfield}", info,
            MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task HandleApiConfigErrorMessageBoxAsync(string reason)
    {
        if (O == null) return;
        var apiConfigErrorTitle = _localization.GetString("ApiConfigErrorTitle", "API Configuration Error");
        var apiConfigErrorMessage = _localization.GetString("ApiConfigErrorMessage",
            "'Simple Launcher' encountered an error loading its API configuration.");
        var reasonLabel = _localization.GetString("ReasonLabel", "Reason:");
        var reinstallSuggestion = _localization.GetString("ReinstallSuggestion",
            "This might prevent some features (like automatic bug reporting) from working correctly. Would you like to reinstall 'Simple Launcher' to fix this?");
        var result = await ShowAsync(O, $"{apiConfigErrorMessage}\n\n{reasonLabel} {reason}\n\n" +
                                        $"{reinstallSuggestion}", apiConfigErrorTitle, MessageButtons.YesNo,
            MessageIcon.Question);

        if (result == MessageBoxResult.Yes)
        {
            _ = App.ServiceProvider.GetRequiredService<AvaloniaCheckForUpdatesService>().ReinstallAndShutdownAsync();
            return;
        }

        var manualReinstallSuggestion = _localization.GetString("ManualReinstallSuggestion",
            "Please reinstall 'Simple Launcher' manually to fix the issue.");
        var applicationWillShutdown =
            _localization.GetString("Theapplicationwillshutdown", "The application will shutdown.");
        await ShowAsync(O, $"{manualReinstallSuggestion}\n\n{applicationWillShutdown}", apiConfigErrorTitle,
            MessageButtons.Ok, MessageIcon.Error);
        App.ServiceProvider.GetRequiredService<AvaloniaQuitSimpleLauncher>().SimpleQuitApplication();
    }


    public Task ShowImagePackDownloadErrorMessageBoxAsync(EasyModeSystemConfig selectedSystem)
    {
        if (O == null) return Task.CompletedTask;
        Log.Debug("Image pack download error for system '{System}'", selectedSystem?.SystemName);
        var erroropeningthedownloadlink =
            _localization.GetString("Erroropeningthedownloadlink", "Error opening the download link.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{erroropeningthedownloadlink}\n\n{theerrorwasreportedtothedeveloper}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }


    public async Task DefaultImageNotFoundMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O,
                _localization.GetString("Defaultcoverimagenotfound", "Default cover image not found."),
                _localization.GetString("MissingImage", "Missing Image"), MessageButtons.Ok, MessageIcon.Warning);
        }
    }


    public async Task<bool> AskAiToFixParametersMessageBoxAsync()
    {
        if (O != null)
        {
            var result = await ShowAsync(O,
                _localization.GetString("DoyouwantSimpleLauncherAItosuggestcorrectparametersforthisemulator",
                    "Do you want Simple Launcher AI to suggest correct parameters for this emulator?"),
                _localization.GetString("AIParameterSuggestion", "AI Parameter Suggestion"),
                MessageButtons.YesNo,
                MessageIcon.Question);
            return result == MessageBoxResult.Yes;
        }

        return false;
    }


    public Task UnabletoDismountIsOfileMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var unabletodismountIsOfile = _localization.GetString("UnabletoDismountISOfile",
            "Unable to dismount ISO file due to PowerShell execution policy restrictions.");
        var thisistypicallycausedbyGroup = _localization.GetString("ThisistypicallycausedbyGroup",
            "This is typically caused by Group Policy settings on corporate or managed PCs.");
        var simpleLaunchercannotperform = _localization.GetString("SimpleLaunchercannotperform",
            "'Simple Launcher' cannot perform the requested task.");
        var powerShellRestricted = _localization.GetString("PowerShellRestricted", "PowerShell Restricted");
        return ShowAsync(O, $"{unabletodismountIsOfile}\n\n{thisistypicallycausedbyGroup}\n\n" +
                            $"{simpleLaunchercannotperform}", powerShellRestricted, MessageButtons.Ok,
            MessageIcon.Warning);
    }


    public Task ErrorCheckingForUpdatesMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var anerroroccurredwhilecheckingforupdates =
            _localization.GetString("Anerroroccurredwhilecheckingforupdates",
                "An error occurred while checking for updates.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{anerroroccurredwhilecheckingforupdates}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task SimpleLauncherWasUnableToRestoreBackupMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("SimpleLauncherwasunabletorestore",
                "'Simple Launcher' was unable to restore the last backup."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task InvalidSystemNameCharactersMessageBoxAsync(string invalidChars)
    {
        if (O == null) return Task.CompletedTask;
        var systemNamecontainsinvalid =
            _localization.GetString("SystemNamecontainsinvalid", "'System Name' contains invalid characters:");
        var pleaseRemoveTheseCharacters = _localization.GetString("PleaseRemoveTheseCharacters",
            "Please remove these characters and try again.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{systemNamecontainsinvalid}\n\n{invalidChars}\n\n{pleaseRemoveTheseCharacters}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task FailedToInjectDuckStationConfigurationMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("FailedToInjectDuckStationConfiguration",
                "Failed to inject DuckStation configuration. Please check file permissions and try again."),
            _localization.GetString("InjectionFailed", "Injection Failed"), MessageButtons.Ok, MessageIcon.Error);
    }

    public async Task<MessageBoxResult> FavoriteFileDoesNotExistAskToDeleteMessageBoxAsync(string filePath)
    {
        if (O == null) return MessageBoxResult.Cancel;
        var thegamefiledoesnotexist =
            _localization.GetString("Thegamefiledoesnotexist", "The game file does not exist!");
        var filepathis = _localization.GetString("FilePathIs", "File path:");
        var doYouWantToDeleteThisFavorite = _localization.GetString("DoYouWantToDeleteThisFavorite",
            "Do you want to delete this favorite from the list?");
        var clickNoToKeepTheFavorite =
            _localization.GetString("ClickNoToKeepTheFavorite", "Click 'No' to keep the favorite in the list.");
        var gameNotAvailable = _localization.GetString("GameNotAvailable", "Game Not Available");
        var message = $"{thegamefiledoesnotexist}\n\n" +
                      $"{filepathis}\n{filePath}\n\n" +
                      $"{doYouWantToDeleteThisFavorite}\n" +
                      $"{clickNoToKeepTheFavorite}";
        return await ShowAsync(O, message, gameNotAvailable, MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task CouldNotOpenAchievementsWindowMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var couldNotOpenAchievementsWindow = _localization.GetString("CouldNotOpenAchievementsWindow",
            "Could not open the achievements window.");
        var theErrorWasReported = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{couldNotOpenAchievementsWindow}\n\n{theErrorWasReported}", error, MessageButtons.Ok,
            MessageIcon.Error);
    }


    public async Task EasyModeUnavailableMessageBoxAsync()
    {
        if (O != null)
        {
            await ShowAsync(O,
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration", "'Simple Launcher' could not access the Web API to download the updated configuration.")}\n\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration2", "This could be due to:")}\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration3", "• A government firewall or internet restriction in your region")}\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration4", "• Network connectivity issues")}\n\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration5", "To resolve this issue, you can:")}\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration6", "1. Enable a VPN connection and try again")}\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration7", "2. Check your internet connection")}\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration8", "3. Configure systems manually using the Edit System feature")}\n\n" +
                $"{_localization.GetString("SimpleLaunchercouldnotaccesstheWebAPIToDownloadTheUpdatedConfiguration9", "Note: A VPN may be required if you are located in a country with internet restrictions.")}",
                _localization.GetString("EasyModeUnavailable", "Easy Mode Unavailable"), MessageButtons.Ok,
                MessageIcon.Warning);
        }
    }


    public Task DolphinConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("DolphinConfigurationSavedSuccessfully",
                "Dolphin configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task ShowExtractionFailedMessageBoxAsync(string tempFolderPath)
    {
        if (O == null) return Task.CompletedTask;
        Log.Debug("Extraction failed. Temp folder: {TempFolderPath}", tempFolderPath);
        var extractionFailedMessage = _localization.GetString("ExtractionFailedMessage",
            "The file was downloaded successfully, but automatic extraction failed. This can happen if an antivirus program is scanning or locking the file.");
        var openTempFolderQuestion = _localization.GetString("OpenTempFolderQuestion",
            "Would you like to open the temporary folder to inspect the file?");
        var extractionFailedTitle = _localization.GetString("ExtractionFailedTitle", "Extraction Failed");
        return ShowAsync(O, $"{extractionFailedMessage}\n\n{openTempFolderQuestion}", extractionFailedTitle,
            MessageButtons.YesNo, MessageIcon.Question);
    }


    public Task NoFavoriteFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("ThereisnoFavoriteforthissystem",
                "There is no Favorite for this system, or you have not chosen a system."),
            _localization.GetString("Warning", "Warning"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task FileSuccessfullyDeletedMessageBoxAsync(string fileNameWithExtension)
    {
        if (O != null)
        {
            await ShowAsync(O,
                $"{fileNameWithExtension} {_localization.GetString("wasdeleted", "deleted.")}",
                _localization.GetString("Deleted", "Deleted"), MessageButtons.Ok, MessageIcon.Information);
        }
    }


    public async Task WarningMessageBoxAsync(string message)
    {
        if (O != null)
        {
            await ShowAsync(O, message, _localization.GetString("Warning", "Warning"), MessageButtons.Ok,
                MessageIcon.Warning);
        }
    }


    public Task FailedToConfigureTheEmulatorMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Failedtoconfiguretheemulator",
                "Failed to configure the emulator. The configuration file might be missing, in an unexpected location, or read-only."),
            _localization.GetString("ConfigurationFailed", "Configuration Failed"), MessageButtons.Ok,
            MessageIcon.Error);
    }


    public Task DuckStationConfigurationSavedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("DuckStationConfigurationSavedSuccessfully",
                "DuckStation configuration saved successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task ErrorOpeningBrowserMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var anerroroccurredwhileopeningthebrowser = _localization.GetString("Anerroroccurredwhileopeningthebrowser",
            "An error occurred while opening the browser.");
        var theerrorwasreportedtothedeveloper = _localization.GetString("Theerrorwasreportedtothedeveloper",
            "The error was reported to the developer who will try to fix the issue.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{anerroroccurredwhileopeningthebrowser}\n\n{theerrorwasreportedtothedeveloper}",
            error, MessageButtons.Ok, MessageIcon.Error);
    }


    public Task EnterNameMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O, _localization.GetString("Pleaseenterthename", "Please enter the name."),
            _localization.GetString("Info", "Info"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task ThereIsNoVideoFileMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnovideofile", "There is no video file associated with this game."),
            _localization.GetString("Videonotfound", "Video not found"), MessageButtons.Ok, MessageIcon.Information);
    }


    public async Task ErrorLaunchingToolMessageBoxAsync(string? logPath)
    {
        if (O == null) return;

        var anerroroccurredwhilelaunchingtheselectedtool = _localization.GetString(
            "Anerroroccurredwhilelaunchingtheselectedtool", "An error occurred while launching the selected tool.");
        var grantSimpleLauncheradministrative = _localization.GetString("GrantSimpleLauncheradministrative",
            "Grant 'Simple Launcher' administrative access and try again.");
        var temporarilydisableyourantivirussoftware = _localization.GetString(
            "Youcanalsotemporarilydisableyourantivirussoftware",
            "You can also temporarily disable your antivirus software or add 'Simple Launcher' folder to the antivirus exclusion list.");
        var dowanttoopenthefileerroruserlog = _localization.GetString("Dowanttoopenthefileerroruserlog",
            "Do want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");

        var result = await ShowAsync(O, $"{anerroroccurredwhilelaunchingtheselectedtool}\n\n" +
                                        $"{grantSimpleLauncheradministrative}\n\n" +
                                        $"{temporarilydisableyourantivirussoftware}\n\n" +
                                        $"{dowanttoopenthefileerroruserlog}", error, MessageButtons.YesNo,
            MessageIcon.Error);

        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlogwasnotfound",
                        "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public async Task<MessageBoxResult> GameNotSupportedByRetroAchievementsMessageBoxAsync()
    {
        if (O != null)
        {
            return await ShowAsync(O,
                $"{_localization.GetString("SimpleLaunchercouldnotcalculate", "'Simple Launcher' could not calculate the hash value of this game or this game is not yet supported by RetroAchievements.")}\n\n" +
                $"{_localization.GetString("DoyouwanttoopentheglobalRetroAchievements", "Do you want to open the global RetroAchievements window?")}",
                _localization.GetString("RetroAchievements", "RetroAchievements"), MessageButtons.YesNo,
                MessageIcon.Question);
        }

        return MessageBoxResult.Cancel;
    }


    public async Task BatchFileFailedMessageBoxAsync(string batchFilePath, string errorDetail, string? logPath,
        int? exitCode = null)
    {
        if (O == null) return;
        var batchFileName = Path.GetFileName(batchFilePath);
        var batchfilefailed = _localization.GetString("Batchfilefailed", "The batch file failed to run.");
        var batchNameMessage = $"{batchfilefailed}\n\n{batchFileName}";
        var errorMessage = !string.IsNullOrEmpty(errorDetail) ? $"Error: {errorDetail}\n\n" : "";
        var exitCodeMessage = exitCode.HasValue ? $"Exit code: {exitCode.Value}\n\n" : "";
        var explanation = exitCode < 0
            ? _localization.GetString("Theprogramlaunchedbythisbatch",
                "The program launched by this batch file may have crashed or been terminated unexpectedly. Negative exit codes typically indicate system-level failures.")
            : _localization.GetString("Batchfilefailedexplanation",
                "This usually means a path referenced inside the batch file no longer exists or is incorrect.");
        var youcanturnoff = _localization.GetString("YoucanturnoffthiserrormessageinExpertmode",
            "You can turn off this error message in Expert mode.");
        var doyouwanttoopen = _localization.GetString("Doyouwanttoopenthefile",
            "Do you want to open the file 'error_user.log' to debug the error?");
        var error = _localization.GetString("Error", "Error");
        var message = $"{batchNameMessage}\n\n{exitCodeMessage}{errorMessage}{explanation}\n\n" +
                      $"{youcanturnoff}\n\n" +
                      $"{doyouwanttoopen}";
        var result = await ShowAsync(O, message, error, MessageButtons.YesNo, MessageIcon.Error);
        if (result == MessageBoxResult.Yes)
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
                Log.Error(ex, "Failed to open the error log file from a batch file error message box");
                await ShowAsync(O,
                    _localization.GetString("Thefileerroruserlog", "The file 'error_user.log' was not found!"),
                    error, MessageButtons.Ok, MessageIcon.Error);
            }
        }
    }


    public Task RetroArchConfigurationInjectedSuccessfullyMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("RetroArchconfigurationinjectedsuccessfully",
                "RetroArch configuration injected successfully."),
            _localization.GetString("Success", "Success"), MessageButtons.Ok, MessageIcon.Information);
    }


    public Task SelectedToolNotFoundMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("PleasereinstallSimpleLaunchermanually",
                "Please reinstall 'Simple Launcher' manually to fix the issue."),
            _localization.GetString("Error", "Error"), MessageButtons.Ok, MessageIcon.Error);
    }


    public Task ThereIsNoTitleSnapshotMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        return ShowAsync(O,
            _localization.GetString("Thereisnotitlesnapshot",
                "There is no title snapshot file associated with this game."),
            _localization.GetString("TitleSnapshotnotfound", "Title Snapshot not found"), MessageButtons.Ok,
            MessageIcon.Information);
    }


    public Task ErrorOpeningVideoLinkMessageBoxAsync()
    {
        if (O == null) return Task.CompletedTask;
        var therewasaproblemopeningtheVideo = _localization.GetString("TherewasaproblemopeningtheVideo",
            "There was a problem opening the Video Link.");
        var ensureyouhaveadefaultbrowserinstalled = _localization.GetString("Ensureyouhaveadefaultbrowserinstalled",
            "Ensure you have a default browser installed and configured correctly on your system.");
        var error = _localization.GetString("Error", "Error");
        return ShowAsync(O, $"{therewasaproblemopeningtheVideo}\n\n{ensureyouhaveadefaultbrowserinstalled}", error,
            MessageButtons.Ok, MessageIcon.Error);
    }

    private static async Task<MessageBoxResult> ShowAsync(
        Window? owner, string message, string caption,
        MessageButtons buttons, MessageIcon icon)
    {
        if (owner is null) return MessageBoxResult.Cancel;

        return await MessageDialogWindow.ShowAsync(owner, message, caption, buttons, icon);
    }
}