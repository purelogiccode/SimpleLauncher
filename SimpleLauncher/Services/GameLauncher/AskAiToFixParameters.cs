using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.LoadingOverlay;
using SimpleLauncher.Services.SystemManager;

namespace SimpleLauncher.Services.GameLauncher;

/// <summary>
///     Provides functionality to ask an AI service to suggest corrected emulator parameters when a game fails to launch.
/// </summary>
public static class AskAiToFixParameters
{
    /// <summary>
    ///     Prompts the user with an AI-generated parameter suggestion and optionally applies the fix to the emulator
    ///     configuration.
    /// </summary>
    /// <param name="systemManager">The system manager for the current system.</param>
    /// <param name="emulatorManager">The emulator whose parameters may be updated.</param>
    /// <param name="messageBoxLibrary">The message box service for user interaction.</param>
    /// <param name="parameterResolverService">The AI parameter resolver service.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="logger">The logger instance.</param>
    public static async Task ExecuteAsync(
        ISystemManager systemManager,
        Emulator emulatorManager,
        IMessageBoxLibraryService messageBoxLibrary,
        IParameterResolverService parameterResolverService,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        try
        {
            if (systemManager == null || emulatorManager == null)
                return;

            var wantAiHelp = await messageBoxLibrary.AskAiToFixParametersMessageBoxAsync();
            if (!wantAiHelp)
                return;

            logger.Debug("[AskAiToFixParameters] User accepted AI parameter suggestion");

            var loadingOverlayService = serviceProvider.GetRequiredService<LoadingOverlayService>();
            var loadingMessage = (string)Application.Current.TryFindResource("ParameterResolverLoading") ??
                                 "Resolving parameters, please wait...";
            loadingOverlayService.SetLoadingState(true, loadingMessage);

            try
            {
                var request = new ParameterResolverRequest
                {
                    SystemName = systemManager.SystemName ?? "",
                    SystemFolder = systemManager.PrimarySystemFolder ?? "",
                    FileFormatsToSearch = systemManager.FileFormatsToSearch?.ToList() ?? [],
                    ExtractFileBeforeLaunch = systemManager.ExtractFileBeforeLaunch,
                    FileFormatsToLaunch = systemManager.FileFormatsToLaunch?.ToList() ?? [],
                    GroupByFolder = systemManager.GroupByFolder,
                    DisableRecursiveSearch = systemManager.DisableRecursiveSearch,
                    EmulatorName = emulatorManager.EmulatorName ?? "",
                    EmulatorPath = emulatorManager.EmulatorLocation ?? "",
                    CurrentParameters = emulatorManager.EmulatorParameters ?? ""
                };

                var result = await parameterResolverService.ResolveParametersAsync(request);
                if (result == null)
                {
                    logger.Debug("[AskAiToFixParameters] ParameterResolver API returned null");
                    return;
                }

                var suggestedParam = result.SuggestedParameter ?? "";
                var explanation = result.Explanation ?? "";

                if (!string.IsNullOrWhiteSpace(suggestedParam) &&
                    suggestedParam.StartsWith("Explanation:", StringComparison.OrdinalIgnoreCase))
                {
                    var explanationFromParam = suggestedParam["Explanation:".Length..].Trim();
                    if (string.IsNullOrEmpty(explanation) ||
                        !explanation.Equals(explanationFromParam, StringComparison.OrdinalIgnoreCase))
                    {
                        explanation = explanationFromParam;
                    }

                    suggestedParam = "";
                }

                var aiSuggestionTitle = (string)Application.Current.TryFindResource("AiParameterSuggestionTitle") ??
                                        "Parameter Suggestion";
                var confirmMessage = (string)Application.Current.TryFindResource("ParameterResolverConfirmApply") ??
                                     "Do you want to apply this parameter?";

                var dialogMessage = $"{confirmMessage}\n\n{suggestedParam}";
                if (!string.IsNullOrEmpty(explanation)) dialogMessage += $"\n\nExplanation: {explanation}";

                var applyResult =
                    await messageBoxLibrary.CustomQuestionMessageBoxAsync(aiSuggestionTitle, dialogMessage);
                if (!applyResult)
                {
                    logger.Debug("[AskAiToFixParameters] User declined to apply AI suggestion");
                    return;
                }

                // Build updated emulator list with the new parameters
                var updatedEmulators = new List<Emulator>();
                foreach (var emu in systemManager.Emulators.Cast<Emulator>())
                {
                    if (emu.EmulatorName.Equals(emulatorManager.EmulatorName, StringComparison.OrdinalIgnoreCase))
                    {
                        updatedEmulators.Add(new Emulator
                        {
                            EmulatorName = emu.EmulatorName,
                            EmulatorLocation = emu.EmulatorLocation,
                            EmulatorParameters = suggestedParam,
                            ReceiveANotificationOnEmulatorError = emu.ReceiveANotificationOnEmulatorError,
                            ImagePackDownloadLink = emu.ImagePackDownloadLink,
                            ImagePackDownloadLink2 = emu.ImagePackDownloadLink2,
                            ImagePackDownloadLink3 = emu.ImagePackDownloadLink3,
                            ImagePackDownloadLink4 = emu.ImagePackDownloadLink4,
                            ImagePackDownloadLink5 = emu.ImagePackDownloadLink5,
                            ImagePackDownloadExtractPath = emu.ImagePackDownloadExtractPath
                        });
                    }
                    else
                    {
                        updatedEmulators.Add(emu);
                    }
                }

                var systemToSave = new SystemManagerService
                {
                    SystemName = systemManager.SystemName ?? "",
                    SystemFolders = systemManager.SystemFolders ?? [],
                    SystemImageFolder = systemManager.SystemImageFolder ?? "",
                    FileFormatsToSearch = systemManager.FileFormatsToSearch ?? [],
                    ExtractFileBeforeLaunch = systemManager.ExtractFileBeforeLaunch,
                    FileFormatsToLaunch = systemManager.FileFormatsToLaunch ?? [],
                    GroupByFolder = systemManager.GroupByFolder,
                    DisableRecursiveSearch = systemManager.DisableRecursiveSearch,
                    Emulators = updatedEmulators
                };

                await SystemManagerService.SaveSystemConfigurationAsync(systemToSave,
                    systemManager.SystemName, logger, configuration);

                // Reload system managers so the main window uses the updated parameters
                var systemSelectionOrchestrator = serviceProvider.GetRequiredService<ISystemSelectionOrchestrator>();
                await systemSelectionOrchestrator.LoadOrReloadSystemManagerAsync();
                await systemSelectionOrchestrator.SystemComboBoxSelectionChangedAsync();

                logger.Debug(
                    $"[AskAiToFixParameters] Parameter updated for emulator '{emulatorManager.EmulatorName}' in system '{systemManager.SystemName}'.");

                var appliedMessage = (string)Application.Current.TryFindResource("AiSuggestedParameterApplied") ??
                                     "The parameter has been updated. Please try launching the game again.";
                await messageBoxLibrary.CustomInfoMessageBoxAsync(aiSuggestionTitle, appliedMessage);
            }
            finally
            {
                loadingOverlayService.SetLoadingState(false);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in AskAiToFixParameters");
            logger.Debug($"[AskAiToFixParameters] Error: {ex.Message}");
        }
    }
}