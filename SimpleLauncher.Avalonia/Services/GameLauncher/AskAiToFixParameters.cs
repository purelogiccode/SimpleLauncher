using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Services.GameLauncher;

/// <summary>
///     Asks an AI service to suggest corrected emulator parameters when a game fails to launch,
///     then persists the accepted suggestion to system.xml.
///     Ported from the original SimpleLauncher (AskAiToFixParameters.cs) and adapted to the
///     new project's services: ISystemConfigurationWriterService for saving and the new
///     SystemManagerService cache for reload.
/// </summary>
public class AskAiToFixParameters
{
    private readonly ILogger _logger;
    private readonly LocalizationService _localization;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IParameterResolverService _parameterResolver;
    private readonly SystemManagerService _systemManager;
    private readonly ISystemConfigurationWriterService _writer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AskAiToFixParameters" /> class.
    /// </summary>
    public AskAiToFixParameters(
        IMessageBoxLibraryService messageBox,
        IParameterResolverService parameterResolver,
        ISystemConfigurationWriterService writer,
        SystemManagerService systemManager,
        ILogger logger,
        LocalizationService localization)
    {
        _messageBox = messageBox;
        _parameterResolver = parameterResolver;
        _writer = writer;
        _systemManager = systemManager;
        _logger = logger;
        _localization = localization;
    }

    /// <summary>
    ///     Prompts the user with an AI-generated parameter suggestion and optionally
    ///     applies the fix to the emulator configuration.
    /// </summary>
    /// <param name="systemManager">The system manager for the current system.</param>
    /// <param name="emulatorManager">The emulator whose parameters may be updated.</param>
    /// <param name="loadingStateProvider">Optional loading overlay provider.</param>
    public async Task ExecuteAsync(
        ISystemManager systemManager,
        Emulator emulatorManager,
        ILoadingState? loadingStateProvider = null)
    {
        try
        {
            if (systemManager is null || emulatorManager is null)
                return;

            var wantAiHelp = await _messageBox.AskAiToFixParametersMessageBoxAsync();
            if (!wantAiHelp)
                return;

            _logger.Debug("[AskAiToFixParameters] User accepted AI parameter suggestion");

            loadingStateProvider?.SetLoadingState(true,
                _localization.GetString("ParameterResolverLoading", "Resolving parameters, please wait..."));

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

                var result = await _parameterResolver.ResolveParametersAsync(request);
                if (result is null)
                {
                    _logger.Debug("[AskAiToFixParameters] ParameterResolver API returned null");
                    return;
                }

                var suggestedParam = result.SuggestedParameter ?? "";
                var explanation = result.Explanation ?? "";

                // The API may return the explanation inside SuggestedParameter
                // (e.g. "Explanation: ..."). Split it out for a cleaner dialog.
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

                var aiSuggestionTitle = _localization.GetString("AiParameterSuggestionTitle", "Parameter Suggestion");
                var confirmMessage =
                    _localization.GetString("ParameterResolverConfirmApply", "Do you want to apply this parameter?");
                var dialogMessage = $"{confirmMessage}\n\n{suggestedParam}";
                if (!string.IsNullOrEmpty(explanation)) dialogMessage += $"\n\nExplanation: {explanation}";

                var applyResult = await _messageBox.CustomQuestionMessageBoxAsync(aiSuggestionTitle, dialogMessage);
                if (!applyResult)
                {
                    _logger.Debug("[AskAiToFixParameters] User declined to apply AI suggestion");
                    return;
                }

                // Build the updated emulator list — preserve all emulators,
                // replace only the matching one's parameters.
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

                var systemToSave = new SystemManagerConfig
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

                // Persist the updated system config (preserves all emulators)
                await _writer.SaveSystemAsync(systemToSave);

                // Invalidate the app's system cache so the next launch uses the new parameters
                _systemManager.InvalidateCache();

                _logger.Debug(
                    "[AskAiToFixParameters] Parameter updated for emulator '{Emulator}' in system '{System}'",
                    emulatorManager.EmulatorName, systemManager.SystemName);

                await _messageBox.CustomInfoMessageBoxAsync(
                    aiSuggestionTitle,
                    _localization.GetString("AiSuggestedParameterApplied",
                        "The parameter has been updated. Please try launching the game again."));
            }
            finally
            {
                loadingStateProvider?.SetLoadingState(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in AskAiToFixParameters");
            _logger.Debug("[AskAiToFixParameters] Error: {Message}", ex.Message);
        }
    }
}