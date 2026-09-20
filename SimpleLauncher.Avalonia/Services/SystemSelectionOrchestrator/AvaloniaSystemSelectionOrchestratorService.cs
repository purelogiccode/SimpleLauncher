using System.Text;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Services.DisplaySystemInfo;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Services.SystemSelectionOrchestrator;

/// <summary>
///     Orchestrates system selection UI: loading system.xml into the top System ComboBox,
///     coordinating system selection with the Emulator ComboBox, validating the selected
///     system's configuration, and refreshing the whole shell after system.xml changes
///     (port of the WPF SystemSelectionOrchestratorService onto the Avalonia sidebar shell).
/// </summary>
public class AvaloniaSystemSelectionOrchestratorService
{
    private readonly AvaloniaDisplaySystemInformation? _displaySystemInformation;
    private readonly AvaloniaGameFileLoadingOrchestrator _loadingOrchestrator;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService? _messageBox;
    private readonly SettingsManagerService? _settings;
    private readonly SystemManagerService _systemManager;
    private ISystemSelectionHost _host = null!;

    /// <summary>
    ///     Initializes a new instance of the SystemSelectionOrchestratorService with the specified dependencies.
    /// </summary>
    public AvaloniaSystemSelectionOrchestratorService(
        SystemManagerService systemManager,
        AvaloniaGameFileLoadingOrchestrator loadingOrchestrator,
        ILogger logger,
        AvaloniaDisplaySystemInformation? displaySystemInformation = null,
        IMessageBoxLibraryService? messageBox = null,
        SettingsManagerService? settings = null)
    {
        _systemManager = systemManager;
        _loadingOrchestrator = loadingOrchestrator;
        _logger = logger;
        _displaySystemInformation = displaySystemInformation;
        _messageBox = messageBox;
        _settings = settings;
    }

    /// <summary>Initializes the orchestrator with the specified UI host.</summary>
    public void Initialize(ISystemSelectionHost host)
    {
        _host = host;
    }

    /// <summary>Loads or reloads system manager configurations and updates the System ComboBox source.</summary>
    public void LoadOrReloadSystemManager()
    {
        try
        {
            var sortedSystemNames = _systemManager.LoadSystems()
                .Select(static manager => manager.SystemName)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToList();
            _host.SetSystemComboBoxItems(sortedSystemNames);
            _logger.Debug("[AvaloniaSystemSelectionOrchestrator] Loaded {Count} systems into the System ComboBox",
                sortedSystemNames.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load or reload the system manager list");
        }
    }

    /// <summary>
    ///     Backward-compatible synchronous entry point (kept for tests/callers that were
    ///     written against the pre-validation orchestrator). Blocks until done.
    /// </summary>
    public void HandleSystemSelectionChanged()
    {
        HandleSystemSelectionChangedAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Handles a top System ComboBox selection change: validates the selected system,
    ///     navigates the game browser to it, refreshes the Emulator ComboBox, and updates
    ///     the play-time display (WPF SystemComboBoxSelectionChangedAsync parity).
    /// </summary>
    public async Task HandleSystemSelectionChangedAsync()
    {
        try
        {
            var systemName = _host.GetSelectedSystem();
            if (string.IsNullOrEmpty(systemName))
            {
                _host.IsPlayTimeVisible = false;
                _host.PlayTime = "00:00:00";
                _logger.Debug("[AvaloniaSystemSelectionOrchestrator] No system selected; play time display was reset");
                return;
            }

            var selectedManager = _systemManager.GetSystem(systemName);
            if (selectedManager == null)
            {
                _logger.Warning("Selected system or its configuration is null.");

                if (_messageBox is { } invalidConfigMessageBox)
                    await invalidConfigMessageBox.InvalidSystemConfigMessageBoxAsync();

                _host.IsPlayTimeVisible = false;
                _host.PlayTime = "00:00:00";
                return;
            }

            // Validate the selected system's folders/image folder/emulator paths
            // (WPF DisplaySystemInfoAsync → ListOfErrorsMessageBoxAsync parity).
            if (_displaySystemInformation is { } displaySystemInformation)
            {
                var validationResult = displaySystemInformation.ValidateSystemConfiguration(selectedManager);
                if (!validationResult.IsValid)
                {
                    var errorMessages = new StringBuilder();
                    foreach (var msg in validationResult.ErrorMessages) errorMessages.Append(msg);

                    _logger.Information(
                        "[AvaloniaSystemSelectionOrchestrator] System configuration validation found {Count} issue(s) for '{System}'",
                        validationResult.ErrorMessages.Count, systemName);

                    if (_messageBox is { } errorListMessageBox)
                        await errorListMessageBox.ListOfErrorsMessageBoxAsync(errorMessages);
                }
            }

            // Hide the play-time display for url/lnk systems (WPF IsPlayTimeVisible parity).
            _host.IsPlayTimeVisible = selectedManager.FileFormatsToSearch?.Any(static f =>
                f.Equals("url", StringComparison.OrdinalIgnoreCase) ||
                f.Equals("lnk", StringComparison.OrdinalIgnoreCase)) != true;

            if (_settings is { } settings)
            {
                var systemPlayTime = settings.SystemPlayTimes.FirstOrDefault(s =>
                    s.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));
                _host.PlayTime = systemPlayTime != null ? systemPlayTime.FormattedPlayTime : "00:00:00";
            }

            // WPF parity: reset the MAME sort order to FileName on every system
            // selection so a toggled sort never persists across system switches.
            _host.MameSortOrder = AppConstants.MameSortOrderFileName;

            _logger.Debug(
                "[AvaloniaSystemSelectionOrchestrator] System '{System}' selected; navigating the game browser",
                systemName);

            // Awaited: the navigation shows the loading overlay and scans the system's
            // folders; the info panel below must only appear once the scan is done.
            await _host.NavigateToSystemAsync(systemName);

            // WPF parity: display the selected system's configuration summary. It stays
            // visible until the user loads games with the letter buttons (ShowGames
            // hides it again — in WPF the info panel is replaced by the game buttons).
            if (_displaySystemInformation is { } systemInfoDisplay)
                _host.ShowSystemInformation(systemInfoDisplay.BuildSystemInfoLines(selectedManager));

            // WPF parity: restart the file watcher for the selected system
            // so only the current system's folders are monitored.
            _host.RestartFileWatcher();

            var emulatorNames = selectedManager.Emulators
                .Select(static e => e.EmulatorName).ToList();
            _host.SetEmulatorComboBoxItems(emulatorNames);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method HandleSystemSelectionChanged");
        }
    }

    /// <summary>
    ///     Refreshes everything that depends on system.xml after a configuration change
    ///     (system added/edited/deleted, image pack downloaded, store scan): invalidates
    ///     cached data, reloads the System ComboBox, rebuilds the sidebar, restarts the
    ///     ROM folder watcher, and re-syncs the Emulator ComboBox with the selection.
    /// </summary>
    public Task ReloadAfterConfigurationChangeAsync()
    {
        try
        {
            _logger.Debug("[AvaloniaSystemSelectionOrchestrator] Reloading the UI after a system configuration change");

            // Per-system file lists are stale once system.xml changed.
            _loadingOrchestrator.InvalidateAll();

            LoadOrReloadSystemManager();
            _host.RefreshSidebar();
            _host.RestartFileWatcher();

            // Keep the Emulator ComboBox in sync with the current selection; clear it
            // when the selected system no longer exists (renamed or deleted).
            var selectedSystem = _host.GetSelectedSystem();
            var emulatorNames = string.IsNullOrEmpty(selectedSystem)
                ? []
                : _systemManager.GetSystem(selectedSystem)?.Emulators
                      .Select(static e => e.EmulatorName).ToList()
                  ?? [];
            _host.SetEmulatorComboBoxItems(emulatorNames);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reloading the UI after a system configuration change");
        }

        return Task.CompletedTask;
    }
}