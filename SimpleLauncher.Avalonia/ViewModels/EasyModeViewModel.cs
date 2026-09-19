using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.DownloadService;
using SimpleLauncher.Core.Services.EasyMode;
using SimpleLauncher.Core.Services.PlaySound;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     MVVM ViewModel for the EasyMode "Add System" workflow.
///     Ported from SimpleLauncher's EasyModeWindow.xaml.cs — same state machine:
///     selecting a system computes per-component Idle/Downloaded state (Idle enables
///     the download button), downloads go through DownloadManager, and Add System
///     persists the config to system.xml.
/// </summary>
public partial class EasyModeViewModel : ObservableObject, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly DownloadManager _downloadManager;

    // ── Download state tracking ───────────────────────────────────────

    private readonly Dictionary<string, DownloadButtonState> _downloadStates = new(StringComparer.Ordinal);
    private readonly EasyModeManager _easyModeManager;
    private readonly ILogger _logger;
    private readonly Services.LocalizationService _localization;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly SystemManagerService? _systemManager;

    [ObservableProperty] public partial bool CanStopDownload { get; set; }

    private string? _currentDownloadType;
    private bool _disposed;

    [ObservableProperty] public partial double DownloadProgress { get; set; }

    [ObservableProperty] public partial string DownloadStatus { get; set; } = "";

    [ObservableProperty] public partial bool IsAddSystemEnabled { get; set; }

    // WPF parity: controls are disabled when no systems are configured
    [ObservableProperty] public partial bool IsContentEnabled { get; set; } = true;

    [ObservableProperty] public partial bool IsCoreDownloaded { get; set; } = true;

    // Download state bools (bound to button IsEnabled via InverseBool:
    // true = downloaded/downloading → button disabled)
    [ObservableProperty] public partial bool IsEmulatorDownloaded { get; set; } = true;

    // Availability (whether a download link + extract path exists for each image pack)
    [ObservableProperty] public partial bool IsImagePack1Available { get; set; }

    [ObservableProperty] public partial bool IsImagePack1Downloaded { get; set; } = true;

    [ObservableProperty] public partial bool IsImagePack2Available { get; set; }

    [ObservableProperty] public partial bool IsImagePack2Downloaded { get; set; } = true;

    [ObservableProperty] public partial bool IsImagePack3Available { get; set; }

    [ObservableProperty] public partial bool IsImagePack3Downloaded { get; set; } = true;

    [ObservableProperty] public partial bool IsImagePack4Available { get; set; }

    [ObservableProperty] public partial bool IsImagePack4Downloaded { get; set; } = true;

    [ObservableProperty] public partial bool IsImagePack5Available { get; set; }

    [ObservableProperty] public partial bool IsImagePack5Downloaded { get; set; } = true;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool IsOperationInProgress { get; set; }

    [ObservableProperty] public partial string LoadingMessage { get; set; } = "Loading configuration...";

    private EasyModeManager? _manager;
    private int _operationInProgressFlag;

    [ObservableProperty] public partial EasyModeSystemConfig? SelectedSystem { get; set; }

    [ObservableProperty] public partial string SystemFolderPath { get; set; } = "";

    // ── Observable properties ─────────────────────────────────────────

    [ObservableProperty] public partial ObservableCollection<EasyModeSystemConfig> Systems { get; set; } = [];

    public EasyModeViewModel(
        EasyModeManager easyModeManager,
        DownloadManager downloadManager,
        IMessageBoxLibraryService messageBox,
        ILogger logger,
        IConfiguration configuration,
        PlaySoundEffects playSoundEffects,
        SystemManagerService? systemManager = null,
        Services.LocalizationService? localization = null)
    {
        _easyModeManager = easyModeManager;
        _downloadManager = downloadManager;
        _messageBox = messageBox;
        _logger = logger;
        _configuration = configuration;
        _playSoundEffects = playSoundEffects;
        _systemManager = systemManager;
        _localization = localization ?? new Services.LocalizationService();

        _downloadManager.DownloadProgressChanged += OnDownloadProgressChanged;
    }

    /// <summary>
    ///     Set to true when the system was successfully added, so the window can close.
    /// </summary>
    public bool SystemAdded { get; private set; }

    /// <summary>
    ///     Callback invoked when the window should close (after successful add).
    ///     Set by the window code-behind.
    /// </summary>
    public Action? RequestClose { get; set; }

    // ── Cleanup ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;

        _downloadManager.DownloadProgressChanged -= OnDownloadProgressChanged;
        _downloadManager?.CancelDownload();
        _manager?.Dispose();
        _easyModeManager?.Dispose();

        _logger.Debug("EasyModeViewModel disposed: download cancelled and managers released");

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    // ── Initialization ────────────────────────────────────────────────

    /// <summary>
    ///     Loads EasyMode systems from the manager. Call after the window is loaded.
    ///     Only systems with an emulator download link are offered (matches the old PopulateSystemDropdown).
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadingMessage = "Loading configuration...";
        try
        {
            await Task.Yield();

            _manager = await _easyModeManager.LoadAsync();

            if (_manager is not { Systems.Count: > 0 })
            {
                _logger.Information("EasyMode configuration is unavailable or empty; disabling the Add System window");
                await _messageBox.EasyModeUnavailableMessageBoxAsync();
                // WPF parity: disable all controls when no systems are configured
                IsContentEnabled = false;
                return;
            }

            var sorted = _manager.Systems
                .Where(static s => s.IsValid()
                                   && !string.IsNullOrEmpty(s.Emulators?.Emulator?.EmulatorDownloadLink))
                .OrderBy(static s => s.SystemName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.Information("EasyMode loaded {SystemCount} system(s) available for installation", sorted.Count);

            Systems = new ObservableCollection<EasyModeSystemConfig>(sorted);
        }
        finally
        {
            // Closed on every path (including exceptions) so the overlay cannot stick.
            IsLoading = false;
        }
    }

    // ── System selection (ports SystemNameDropdown_SelectionChanged) ──

    partial void OnSelectedSystemChanged(EasyModeSystemConfig? value)
    {
        if (_disposed) return;

        if (value == null)
        {
            // No selection → all components "downloaded" (buttons disabled)
            IsImagePack1Available = false;
            IsImagePack2Available = false;
            IsImagePack3Available = false;
            IsImagePack4Available = false;
            IsImagePack5Available = false;

            SetDownloadState(EasyModeManager.DownloadType.Emulator, DownloadButtonState.Downloaded);
            SetDownloadState(EasyModeManager.DownloadType.Core, DownloadButtonState.Downloaded);
            SetDownloadState(EasyModeManager.DownloadType.ImagePack1, DownloadButtonState.Downloaded);
            SetDownloadState(EasyModeManager.DownloadType.ImagePack2, DownloadButtonState.Downloaded);
            SetDownloadState(EasyModeManager.DownloadType.ImagePack3, DownloadButtonState.Downloaded);
            SetDownloadState(EasyModeManager.DownloadType.ImagePack4, DownloadButtonState.Downloaded);
            SetDownloadState(EasyModeManager.DownloadType.ImagePack5, DownloadButtonState.Downloaded);

            SystemFolderPath = "";
            _logger.Debug("EasyMode system selection cleared; resetting all component states");
            return;
        }

        _logger.Debug("EasyMode system selected: {SystemName}", value.SystemName);

        var emulator = value.Emulators?.Emulator;

        // Image packs are shown only when both a link and an extract path exist
        IsImagePack1Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink)
                                && !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack2Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink2)
                                && !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack3Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink3)
                                && !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack4Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink4)
                                && !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack5Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink5)
                                && !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);

        // Emulator: downloaded if the file already exists on disk, otherwise Idle (button enabled)
        var emulatorLocation = emulator?.EmulatorLocation;
        if (!string.IsNullOrEmpty(emulatorLocation))
        {
            var resolvedEmulatorPath = PathHelper.ResolveRelativeToAppDirectory(emulatorLocation);
            SetDownloadState(EasyModeManager.DownloadType.Emulator,
                File.Exists(resolvedEmulatorPath) ? DownloadButtonState.Downloaded : DownloadButtonState.Idle);
        }
        else
        {
            // No location defined → it can't exist → needs to be downloaded
            SetDownloadState(EasyModeManager.DownloadType.Emulator, DownloadButtonState.Idle);
        }

        // Core: downloaded if the file exists, or if no core download is offered
        var coreLocation = emulator?.CoreLocation;
        var coreDownloadLink = emulator?.CoreDownloadLink;
        if (!string.IsNullOrEmpty(coreLocation))
        {
            var resolvedCorePath = PathHelper.ResolveRelativeToAppDirectory(coreLocation);
            SetDownloadState(EasyModeManager.DownloadType.Core,
                File.Exists(resolvedCorePath) ? DownloadButtonState.Downloaded : DownloadButtonState.Idle);
        }
        else
        {
            SetDownloadState(EasyModeManager.DownloadType.Core,
                string.IsNullOrEmpty(coreDownloadLink) ? DownloadButtonState.Downloaded : DownloadButtonState.Idle);
        }

        _logger.Debug("EasyMode component state for {SystemName}: Emulator={EmulatorState}, Core={CoreState}",
            value.SystemName, GetDownloadState(EasyModeManager.DownloadType.Emulator),
            GetDownloadState(EasyModeManager.DownloadType.Core));

        // Image packs: downloaded only when no download is offered
        SetDownloadState(EasyModeManager.DownloadType.ImagePack1,
            string.IsNullOrEmpty(emulator?.ImagePackDownloadLink)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack2,
            string.IsNullOrEmpty(emulator?.ImagePackDownloadLink2)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack3,
            string.IsNullOrEmpty(emulator?.ImagePackDownloadLink3)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack4,
            string.IsNullOrEmpty(emulator?.ImagePackDownloadLink4)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack5,
            string.IsNullOrEmpty(emulator?.ImagePackDownloadLink5)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);

        // Default folder for the textbox (resolved for display)
        SystemFolderPath = PathHelper.ResolveRelativeToAppDirectory(value.SystemFolder) ?? "";
        _logger.Debug("EasyMode default system folder for {SystemName}: {Folder}", value.SystemName, SystemFolderPath);
    }

    // ── Download commands ─────────────────────────────────────────────

    [RelayCommand]
    private Task DownloadEmulatorAsync()
    {
        return DownloadComponentAsync(EasyModeManager.DownloadType.Emulator);
    }

    [RelayCommand]
    private Task DownloadCoreAsync()
    {
        return DownloadComponentAsync(EasyModeManager.DownloadType.Core);
    }

    [RelayCommand]
    private Task DownloadImagePack1Async()
    {
        return DownloadComponentAsync(EasyModeManager.DownloadType.ImagePack1);
    }

    [RelayCommand]
    private Task DownloadImagePack2Async()
    {
        return DownloadComponentAsync(EasyModeManager.DownloadType.ImagePack2);
    }

    [RelayCommand]
    private Task DownloadImagePack3Async()
    {
        return DownloadComponentAsync(EasyModeManager.DownloadType.ImagePack3);
    }

    [RelayCommand]
    private Task DownloadImagePack4Async()
    {
        return DownloadComponentAsync(EasyModeManager.DownloadType.ImagePack4);
    }

    [RelayCommand]
    private Task DownloadImagePack5Async()
    {
        return DownloadComponentAsync(EasyModeManager.DownloadType.ImagePack5);
    }

    [RelayCommand]
    private void StopDownload()
    {
        _playSoundEffects.PlayNotificationSound();
        if (_disposed) return;

        _downloadManager.CancelDownload();
        _logger.Information("EasyMode download cancellation requested (component: {Component})",
            _currentDownloadType ?? "none");
        CanStopDownload = false;
        DownloadProgress = 0;
        DownloadStatus = _localization.GetString("Cancelingdownload", "Canceling download...");

        if (_currentDownloadType != null)
        {
            if (GetDownloadState(_currentDownloadType) == DownloadButtonState.Downloading)
                SetDownloadState(_currentDownloadType, DownloadButtonState.Failed);

            _currentDownloadType = null;
        }

        EndOperation();
    }

    // ── Add System ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddSystemAsync()
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (!TryStartOperation()) return;

            var selectedSystem = SelectedSystem;
            if (selectedSystem == null)
            {
                _logger.Debug("Add System requested but no system is selected");
                EndOperation();
                return;
            }

            var systemFolder = !string.IsNullOrWhiteSpace(SystemFolderPath)
                ? SystemFolderPath
                : Path.Combine("%BASEFOLDER%", "roms", selectedSystem.SystemName);

            _logger.Information("Adding system to configuration: {SystemName} (folder: {Folder})",
                selectedSystem.SystemName, systemFolder);

            var systemImageFolder = selectedSystem.SystemImageFolder;

            try
            {
                IsAddSystemEnabled = false;
                // WPF parity: the add + folder creation runs behind the loading overlay.
                IsLoading = true;
                DownloadStatus = _localization.GetString("Addingsystemtoconfiguration",
                    "Adding system to configuration...");
                LoadingMessage = DownloadStatus;
                DownloadProgress = 0;

                await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(
                    selectedSystem, systemFolder, _configuration, _logger, _systemManager);

                DownloadStatus = _localization.GetString("Creatingsystemfolders", "Creating system folders...");
                LoadingMessage = DownloadStatus;
                await Task.Yield();

                var resolvedSystemFolder = PathHelper.ResolveRelativeToAppDirectory(systemFolder);
                var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(systemImageFolder);

                if (resolvedSystemFolder != null && resolvedSystemImageFolder != null)
                {
                    await CreateDefaultSystemFoldersService.CreateFoldersAsync(
                        selectedSystem.SystemName,
                        resolvedSystemFolder,
                        resolvedSystemImageFolder,
                        _configuration,
                        _logger,
                        _messageBox);

                    DownloadStatus = _localization.GetString("Systemhasbeensuccessfullyadded",
                        "System has been successfully added!");
                    await _messageBox.SystemAddedMessageBoxAsync(
                        selectedSystem.SystemName,
                        resolvedSystemFolder,
                        resolvedSystemImageFolder);
                }

                // Signal success so the window can close
                _logger.Information("System added to configuration: {SystemName}", selectedSystem.SystemName);
                SystemAdded = true;
                RequestClose?.Invoke();
            }
            catch (InvalidOperationException ex)
            {
                _logger.Information(ex, "Add system validation failed for {SystemName}", selectedSystem.SystemName);
                DownloadStatus =
                    $"{_localization.GetString("ErrorFailedtoaddsystem", "Error: Failed to add system.")} {ex.Message}";
                await _messageBox.AddSystemFailedMessageBoxAsync(ex.Message);
            }
            catch (Exception ex)
            {
                DownloadStatus = _localization.GetString("ErrorFailedtoaddsystem", "Error: Failed to add system.");
                _logger.Error(ex, "Unexpected error adding system");
                await _messageBox.AddSystemFailedMessageBoxAsync();
            }
            finally
            {
                IsAddSystemEnabled = true;
                IsLoading = false;
                EndOperation();
            }
        }
        catch (Exception ex)
        {
            EndOperation();
            _logger.Error(ex, "Error in AddSystemAsync");
        }
    }

    // ── Download flow (ports HandleDownloadAndExtractComponentAsync) ──

    private async Task DownloadComponentAsync(string type)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed || SelectedSystem == null) return;
            if (!TryStartOperation()) return;

            var selectedSystem = SelectedSystem;
            var emulatorConfig = selectedSystem.Emulators?.Emulator;

            _currentDownloadType = type;
            SetDownloadState(type, DownloadButtonState.Downloading);

            var (downloadUrl, easyModeExtractPath, componentName) = type switch
            {
                // WPF parity: component names resolve from resources (Emulator/Core/ImagePack1-5).
                EasyModeManager.DownloadType.Emulator => (emulatorConfig?.EmulatorDownloadLink,
                    emulatorConfig?.EmulatorDownloadExtractPath,
                    _localization.GetString("Emulator", "Emulator")),
                EasyModeManager.DownloadType.Core => (emulatorConfig?.CoreDownloadLink,
                    emulatorConfig?.CoreDownloadExtractPath, _localization.GetString("Core", "Core")),
                EasyModeManager.DownloadType.ImagePack1 => (emulatorConfig?.ImagePackDownloadLink,
                    emulatorConfig?.ImagePackDownloadExtractPath,
                    _localization.GetString("ImagePack1", "Image Pack 1")),
                EasyModeManager.DownloadType.ImagePack2 => (emulatorConfig?.ImagePackDownloadLink2,
                    emulatorConfig?.ImagePackDownloadExtractPath,
                    _localization.GetString("ImagePack2", "Image Pack 2")),
                EasyModeManager.DownloadType.ImagePack3 => (emulatorConfig?.ImagePackDownloadLink3,
                    emulatorConfig?.ImagePackDownloadExtractPath,
                    _localization.GetString("ImagePack3", "Image Pack 3")),
                EasyModeManager.DownloadType.ImagePack4 => (emulatorConfig?.ImagePackDownloadLink4,
                    emulatorConfig?.ImagePackDownloadExtractPath,
                    _localization.GetString("ImagePack4", "Image Pack 4")),
                EasyModeManager.DownloadType.ImagePack5 => (emulatorConfig?.ImagePackDownloadLink5,
                    emulatorConfig?.ImagePackDownloadExtractPath,
                    _localization.GetString("ImagePack5", "Image Pack 5")),
                _ => (null, null, type)
            };

            _logger.Information("Download requested for {Component} ({DownloadType})", componentName, type);

            try
            {
                if (easyModeExtractPath == null)
                {
                    _logger.Debug("No extract path configured for {Component}; nothing to download", componentName);
                    EndOperation();
                    SetDownloadState(type, DownloadButtonState.Idle);
                    return;
                }

                var destinationPath = PathHelper.ResolveRelativeToAppDirectory(easyModeExtractPath);

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _logger.Information("No download URL configured for {Component} ({SystemName})", componentName,
                        selectedSystem.SystemName);
                    DownloadStatus =
                        $"{_localization.GetString("ErrorNodownloadURLfor", "Error: No download URL for")} {componentName}";
                    EndOperation();
                    SetDownloadState(type, DownloadButtonState.Idle);
                    return;
                }

                if (string.IsNullOrEmpty(destinationPath))
                {
                    DownloadStatus =
                        $"{_localization.GetString("ErrorInvalidDestinationPath", "Error: Invalid destination path for")} {componentName}";
                    _logger.Warning("[EasyMode] Invalid destination path for {Component}: {Path}", componentName,
                        easyModeExtractPath);
                    EndOperation();
                    SetDownloadState(type, DownloadButtonState.Idle);
                    return;
                }

                DownloadStatus =
                    $"{_localization.GetString("Preparingtodownload", "Preparing to download")} {componentName}...";
                DownloadProgress = 0;
                CanStopDownload = true;

                DownloadStatus =
                    $"{_localization.GetString("Downloading", "Downloading")} {componentName}...";
                var downloadedFile = await _downloadManager.DownloadFileAsync(downloadUrl);

                if (_disposed)
                {
                    EndOperation();
                    return;
                }

                var success = false;

                if (downloadedFile != null && _downloadManager.IsDownloadCompleted)
                {
                    // WPF parity: the extracting text resolves from resources.
                    var extracting = _localization.GetString("Extracting", "Extracting");

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DownloadStatus = $"{extracting} {componentName}...";
                        DownloadProgress = 0;
                        LoadingMessage = $"{extracting} {componentName}...";
                        IsLoading = true;
                    });

                    try
                    {
                        success = await _downloadManager.ExtractFileAsync(downloadedFile, destinationPath);
                    }
                    finally
                    {
                        // Closed on every path (including exceptions) so the overlay cannot stick.
                        await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
                    }
                }

                if (success)
                {
                    EndOperation();
                    _logger.Information("{Component} downloaded and installed successfully", componentName);
                    DownloadStatus =
                        $"{componentName} {_localization.GetString("hasbeensuccessfullydownloadedandinstalled", "has been successfully downloaded and installed.")}";
                    CanStopDownload = false;

                    await _messageBox.DownloadAndExtractionWereSuccessfulMessageBoxAsync();

                    SetDownloadState(type, DownloadButtonState.Downloaded);
                }
                else
                {
                    if (_disposed)
                    {
                        EndOperation();
                        return;
                    }

                    if (_downloadManager.IsUserCancellation)
                    {
                        _logger.Information("Download of {Component} was canceled by the user", componentName);
                        DownloadStatus =
                            $"{_localization.GetString("Downloadof", "Download of")} {componentName} {_localization.GetString("wascanceled", "was canceled.")}";
                        CanStopDownload = false;
                        EndOperation();
                        SetDownloadState(type, DownloadButtonState.Failed);
                    }
                    else if (_downloadManager.IsFileLockedDuringDownload)
                    {
                        _logger.Information(
                            "Download of {Component} failed because a file is locked in the temp folder", componentName);
                        await _messageBox.ShowDownloadFileLockedMessageBoxAsync(_downloadManager.TempFolder);
                        EndOperation();
                    }
                    else if (_downloadManager.IsDownloadCompleted)
                    {
                        DownloadStatus =
                            $"{_localization.GetString("ErrorFailedtoextract", "Error: Failed to extract")} {componentName}.";
                        EndOperation();
                        SetDownloadState(type, DownloadButtonState.Failed);
                        await _messageBox.ShowExtractionFailedMessageBoxAsync(_downloadManager.TempFolder);
                    }
                    else
                    {
                        DownloadStatus =
                            $"{_localization.GetString("Errorduringdownload", "Error during download")}: {componentName}.";
                        EndOperation();
                        await ShowDownloadErrorDialogAsync(type, selectedSystem);
                        SetDownloadState(type, DownloadButtonState.Failed);
                    }

                    CanStopDownload = false;
                }
            }
            catch (Exception ex)
            {
                if (_disposed)
                {
                    EndOperation();
                    return;
                }

                // WPF parity: "{Error during} {component} {download process.}" from resources.
                DownloadStatus =
                    $"{_localization.GetString("Errorduring", "Error during")} {componentName} {_localization.GetString("downloadprocess", "download process.")}";

                // Notify developer only if it's not an expected condition.
                // Disk space errors are user-environment issues; timeouts/connection failures are
                // user/network conditions — log both at Information so the bug report API does
                // not pick them up (see bug 65965). Anything else is a genuine defect.
                if (!(ex is IOException ioEx &&
                      (ioEx.Message.Contains("Insufficient disk space", StringComparison.Ordinal) ||
                       ioEx.Message.Contains("Cannot check disk space", StringComparison.Ordinal))))
                {
                    if (DownloadErrorClassifier.IsExpectedDownloadException(ex))
                    {
                        _logger.Information(ex, "Error downloading {Component}. URL: {Url}", componentName,
                            downloadUrl);
                    }
                    else
                    {
                        _logger.Error(ex, "Error downloading {Component}. URL: {Url}", componentName, downloadUrl);
                    }
                }

                if (_downloadManager.IsFileLockedDuringDownload)
                {
                    EndOperation();
                    await _messageBox.ShowDownloadFileLockedMessageBoxAsync(_downloadManager.TempFolder);
                }
                else if (_downloadManager.IsDownloadCompleted)
                {
                    EndOperation();
                    await _messageBox.ShowExtractionFailedMessageBoxAsync(_downloadManager.TempFolder);
                }
                else
                {
                    EndOperation();
                    await ShowDownloadErrorDialogAsync(type, selectedSystem);
                }

                if (_disposed) return;

                CanStopDownload = false;
                SetDownloadState(type, DownloadButtonState.Failed);
                EndOperation();
            }
            finally
            {
                _currentDownloadType = null;
                CanStopDownload = false;
                EndOperation();
            }
        }
        catch (Exception ex)
        {
            EndOperation();
            _logger.Error(ex, "Error in DownloadComponentAsync");
        }
    }

    private Task ShowDownloadErrorDialogAsync(string type, EasyModeSystemConfig selectedSystem)
    {
        return type switch
        {
            EasyModeManager.DownloadType.Emulator => _messageBox.ShowEmulatorDownloadErrorMessageBoxAsync(
                selectedSystem),
            EasyModeManager.DownloadType.Core => _messageBox.ShowCoreDownloadErrorMessageBoxAsync(selectedSystem),
            EasyModeManager.DownloadType.ImagePack1 or EasyModeManager.DownloadType.ImagePack2
                or EasyModeManager.DownloadType.ImagePack3 or EasyModeManager.DownloadType.ImagePack4
                or EasyModeManager.DownloadType.ImagePack5 => _messageBox.ShowImagePackDownloadErrorMessageBoxAsync(
                    selectedSystem),
            _ => _messageBox.DownloadExtractionFailedMessageBoxAsync()
        };
    }

    // ── State management ──────────────────────────────────────────────

    private bool TryStartOperation()
    {
        if (Interlocked.CompareExchange(ref _operationInProgressFlag, 1, 0) != 0)
            return false;

        IsOperationInProgress = true;
        IsContentEnabled = false; // Disable controls during operation
        return true;
    }

    private void EndOperation()
    {
        IsOperationInProgress = false;
        IsContentEnabled = true; // Re-enable controls after operation
        Interlocked.Exchange(ref _operationInProgressFlag, 0);
    }

    private DownloadButtonState GetDownloadState(string type)
    {
        return _downloadStates.GetValueOrDefault(type, DownloadButtonState.Idle);
    }

    private void SetDownloadState(string type, DownloadButtonState state)
    {
        _downloadStates[type] = state;

        switch (type)
        {
            case EasyModeManager.DownloadType.Emulator:
                IsEmulatorDownloaded = state is DownloadButtonState.Downloaded or DownloadButtonState.Downloading;
                break;
            case EasyModeManager.DownloadType.Core:
                IsCoreDownloaded = state is DownloadButtonState.Downloaded or DownloadButtonState.Downloading;
                break;
            case EasyModeManager.DownloadType.ImagePack1:
                IsImagePack1Downloaded = state is DownloadButtonState.Downloaded or DownloadButtonState.Downloading;
                break;
            case EasyModeManager.DownloadType.ImagePack2:
                IsImagePack2Downloaded = state is DownloadButtonState.Downloaded or DownloadButtonState.Downloading;
                break;
            case EasyModeManager.DownloadType.ImagePack3:
                IsImagePack3Downloaded = state is DownloadButtonState.Downloaded or DownloadButtonState.Downloading;
                break;
            case EasyModeManager.DownloadType.ImagePack4:
                IsImagePack4Downloaded = state is DownloadButtonState.Downloaded or DownloadButtonState.Downloading;
                break;
            case EasyModeManager.DownloadType.ImagePack5:
                IsImagePack5Downloaded = state is DownloadButtonState.Downloaded or DownloadButtonState.Downloading;
                break;
        }

        UpdateAddSystemButtonState();
    }

    private void UpdateAddSystemButtonState()
    {
        if (_disposed)
        {
            IsAddSystemEnabled = false;
            return;
        }

        var emulatorConfig = SelectedSystem?.Emulators?.Emulator;
        if (emulatorConfig == null)
        {
            IsAddSystemEnabled = false;
            return;
        }

        var isEmulatorRequired = !string.IsNullOrEmpty(emulatorConfig.EmulatorDownloadLink);
        var isEmulatorReady = !isEmulatorRequired || IsEmulatorDownloaded;

        var isCoreRequired = !string.IsNullOrEmpty(emulatorConfig.CoreDownloadLink);
        var isCoreReady = !isCoreRequired || IsCoreDownloaded;

        IsAddSystemEnabled = isEmulatorReady && isCoreReady && !IsOperationInProgress;
    }

    private void OnDownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        if (_disposed) return;

        // DownloadManager raises progress events on the download worker thread (same as the WPF
        // app, which marshals via Dispatcher.InvokeAsync). Marshal to the UI thread before
        // touching bound properties so the progress bar and status text update reliably.
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyProgressUpdate(e);
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed) return;

                ApplyProgressUpdate(e);
            });
        }
    }

    private void ApplyProgressUpdate(DownloadProgressEventArgs e)
    {
        DownloadProgress = e.ProgressPercentage;
        DownloadStatus = e.StatusMessage;
    }
}