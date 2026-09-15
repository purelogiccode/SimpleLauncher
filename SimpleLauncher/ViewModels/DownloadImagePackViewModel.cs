using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.DownloadService;
using SimpleLauncher.Core.Services.EasyMode;
using SimpleLauncher.Core.Services.PlaySound;
using Application = System.Windows.Application;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the image pack download window.
/// </summary>
public partial class DownloadImagePackViewModel : ObservableObject, IDisposable
{
    private readonly DownloadManager _downloadManager;
    private readonly EasyModeManager _easyModeManager;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly IResourceProvider _resourceProvider;
    private readonly IServiceScope _scope;
    private volatile bool _disposed;
    private bool _isLoading;

    private bool _isOperationInProgress;
    private bool _isStopEnabled;
    private bool _isSystemDropdownEnabled = true;
    private string _loadingMessage = "";
    private EasyModeManager? _manager;
    private int _operationInProgressFlag;
    private double _progressPercentage;

    private string _selectedSystemName = "";
    private string _statusMessage = "";

    /// <summary>Initializes a new instance of the <see cref="DownloadImagePackViewModel" />.</summary>
    /// <param name="playSoundEffects">The sound effects service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="easyModeManager">The Easy Mode configuration manager.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    /// <param name="scopeFactory">The service scope factory for dependency resolution.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public DownloadImagePackViewModel(PlaySoundEffects playSoundEffects, ILogger logger,
        EasyModeManager easyModeManager, IMessageBoxLibraryService messageBox, IServiceScopeFactory scopeFactory,
        IResourceProvider resourceProvider)
    {
        _playSoundEffects = playSoundEffects;
        _logger = logger;
        _easyModeManager = easyModeManager;
        _messageBox = messageBox;
        _resourceProvider = resourceProvider;
        _scope = scopeFactory.CreateScope();
        _downloadManager = _scope.ServiceProvider.GetRequiredService<DownloadManager>();
        _downloadManager.DownloadProgressChanged += DownloadManager_ProgressChanged;

        ImagePacksToDisplay = [];
        SystemNames = [];

        DownloadImagePackCommand = new AsyncRelayCommand<object?>(ExecuteDownloadAsync, _ => !IsOperationInProgress);
    }

    /// <summary>Gets the collection of system names available for image pack download.</summary>
    public ObservableCollection<string> SystemNames { get; }

    /// <summary>Gets the collection of image pack items to display for the selected system.</summary>
    public ObservableCollection<ImagePackDownloadItem> ImagePacksToDisplay { get; }

    /// <summary>Gets whether a download or extraction operation is currently in progress.</summary>
    public bool IsOperationInProgress
    {
        get => _isOperationInProgress;
        private set
        {
            if (SetProperty(ref _isOperationInProgress, value))
            {
                OnPropertyChanged(nameof(IsMainContentEnabled));
                DownloadImagePackCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets whether the stop button is enabled.</summary>
    public bool IsStopEnabled
    {
        get => _isStopEnabled;
        private set => SetProperty(ref _isStopEnabled, value);
    }

    /// <summary>Gets the current download progress percentage (0-100).</summary>
    public double ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetProperty(ref _progressPercentage, value);
    }

    /// <summary>Gets the current status message displayed to the user.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets whether a loading indicator should be shown.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(IsMainContentEnabled));
        }
    }

    /// <summary>Gets the message displayed during loading.</summary>
    public string LoadingMessage
    {
        get => _loadingMessage;
        private set => SetProperty(ref _loadingMessage, value);
    }

    /// <summary>Gets whether the system selection dropdown is enabled.</summary>
    public bool IsSystemDropdownEnabled
    {
        get => _isSystemDropdownEnabled;
        private set => SetProperty(ref _isSystemDropdownEnabled, value);
    }

    /// <summary>Gets or sets the currently selected system name.</summary>
    public string SelectedSystemName
    {
        get => _selectedSystemName;
        set
        {
            if (SetProperty(ref _selectedSystemName, value)) OnSystemSelectionChanged();
        }
    }

    /// <summary>Gets whether the main content area is enabled (no operation or loading in progress).</summary>
    public bool IsMainContentEnabled => !IsOperationInProgress && !IsLoading;

    /// <summary>Gets the command to download the selected image pack.</summary>
    public IAsyncRelayCommand<object?> DownloadImagePackCommand { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private bool TryStartOperation()
    {
        if (Interlocked.CompareExchange(ref _operationInProgressFlag, 1, 0) != 0)
            return false;

        IsOperationInProgress = true;
        return true;
    }

    private void EndOperation()
    {
        IsOperationInProgress = false;
        Interlocked.Exchange(ref _operationInProgressFlag, 0);
    }

    /// <summary>
    ///     Initializes the ViewModel by loading the Easy Mode configuration.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            LoadingMessage = _resourceProvider.GetString("Loadingconfiguration", "Loading configuration...");
            IsLoading = true;
            await Task.Yield();

            _manager = (await _easyModeManager.LoadAsync())!;

            IsLoading = false;

            if (_manager is not { Systems.Count: > 0 })
            {
                await _messageBox.ImagePackDownloaderUnavailableMessageBoxAsync();
                IsSystemDropdownEnabled = false;
                return;
            }

            PopulateSystemDropdown();
        }
        catch (Exception ex)
        {
            IsLoading = false;
            _logger.Error(ex, "Error initializing image pack downloader");
            await _messageBox.ImagePackDownloaderUnavailableMessageBoxAsync();
            IsSystemDropdownEnabled = false;
        }
    }

    private void PopulateSystemDropdown()
    {
        try
        {
            SystemNames.Clear();

            if (_manager?.Systems == null) return;

            var systemsWithImagePacks = _manager.Systems
                .Where(static system =>
                    !string.IsNullOrEmpty(system.Emulators?.Emulator?.ImagePackDownloadLink) ||
                    !string.IsNullOrEmpty(system.Emulators?.Emulator?.ImagePackDownloadLink2) ||
                    !string.IsNullOrEmpty(system.Emulators?.Emulator?.ImagePackDownloadLink3) ||
                    !string.IsNullOrEmpty(system.Emulators?.Emulator?.ImagePackDownloadLink4) ||
                    !string.IsNullOrEmpty(system.Emulators?.Emulator?.ImagePackDownloadLink5))
                .Select(static system => system.SystemName)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToList();

            foreach (var name in systemsWithImagePacks) SystemNames.Add(name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error populating system dropdown");
            SystemNames.Clear();
        }
    }

    private void OnSystemSelectionChanged()
    {
        ImagePacksToDisplay.Clear();

        if (string.IsNullOrEmpty(SelectedSystemName)) return;

        var selectedSystem = GetSelectedSystem();
        if (selectedSystem == null) return;

        AddImagePackItemIfValid(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink,
            selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath,
            _resourceProvider.GetString("ImagePack1", "Image Pack 1"));
        AddImagePackItemIfValid(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink2,
            selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath,
            _resourceProvider.GetString("ImagePack2", "Image Pack 2"));
        AddImagePackItemIfValid(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink3,
            selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath,
            _resourceProvider.GetString("ImagePack3", "Image Pack 3"));
        AddImagePackItemIfValid(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink4,
            selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath,
            _resourceProvider.GetString("ImagePack4", "Image Pack 4"));
        AddImagePackItemIfValid(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink5,
            selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath,
            _resourceProvider.GetString("ImagePack5", "Image Pack 5"));
    }

    private void AddImagePackItemIfValid(string? downloadLink, string? extractPath, string displayName)
    {
        if (!string.IsNullOrEmpty(downloadLink) && !string.IsNullOrEmpty(extractPath))
        {
            ImagePacksToDisplay.Add(new ImagePackDownloadItem
            {
                DisplayName = displayName,
                DownloadUrl = downloadLink,
                ExtractPath = extractPath,
                State = DownloadButtonState.Idle
            });
        }
    }

    private async Task ExecuteDownloadAsync(object? parameter)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                if (parameter is not ImagePackDownloadItem clickedItem)
                {
                    EndOperation();
                    return;
                }

                clickedItem.State = DownloadButtonState.Downloading;

                try
                {
                    await HandleDownloadAndExtractComponentAsync(clickedItem);
                }
                catch (Exception ex)
                {
                    if (_disposed)
                    {
                        EndOperation();
                        return;
                    }

                    _logger.Error(ex, $"Error in DownloadImagePackButtonClickAsync for {clickedItem.DisplayName}.");
                    clickedItem.State = DownloadButtonState.Failed;
                    EndOperation();
                }
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    EndOperation();
                    _logger.Error(ex, "Error in DownloadImagePackButtonClickAsync");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Critical error in DownloadImagePackButtonClickAsync");
            EndOperation();
        }
    }

    private async Task HandleDownloadAndExtractComponentAsync(ImagePackDownloadItem item)
    {
        if (_disposed) return;

        var selectedSystem = GetSelectedSystem();
        if (selectedSystem == null)
        {
            EndOperation();
            item.State = DownloadButtonState.Failed;
            return;
        }

        var downloadUrl = item.DownloadUrl;
        var componentName = item.DisplayName;
        var easyModeExtractPath = item.ExtractPath;

        var destinationPath = PathHelper.ResolveRelativeToAppDirectory(easyModeExtractPath);

        if (string.IsNullOrEmpty(downloadUrl))
        {
            var errorNodownloadUrLfor =
                _resourceProvider.GetString("ErrorNodownloadURLfor", "Error: No download URL for");
            if (_disposed) return;

            StatusMessage = $"{errorNodownloadUrLfor} {componentName}";
            EndOperation();
            item.State = DownloadButtonState.Failed;
            return;
        }

        if (string.IsNullOrEmpty(destinationPath))
        {
            var errorInvalidDestinationPath =
                _resourceProvider.GetString("ErrorInvalidDestinationPath", "Error: Invalid destination path for");
            if (_disposed) return;

            StatusMessage = $"{errorInvalidDestinationPath} {componentName}";

            _logger.Warning(
                $"[HandleDownloadAndExtractComponentAsync] Invalid destination path for {componentName}: {easyModeExtractPath}");
            EndOperation();
            item.State = DownloadButtonState.Failed;
            return;
        }

        try
        {
            var preparingtodownload = _resourceProvider.GetString("Preparingtodownload", "Preparing to download");
            if (_disposed) return;

            StatusMessage = $"{preparingtodownload} {componentName}...";

            if (_disposed) return;

            ProgressPercentage = 0;
            IsStopEnabled = true;

            var success = false;

            var downloading = _resourceProvider.GetString("Downloading", "Downloading");
            if (_disposed) return;

            StatusMessage = $"{downloading} {componentName}...";

            var downloadedFile = await _downloadManager.DownloadFileAsync(downloadUrl);

            if (_disposed) return;

            if (downloadedFile != null && _downloadManager.IsDownloadCompleted)
            {
                var extracting = _resourceProvider.GetString("Extracting", "Extracting");
                if (_disposed) return;

                StatusMessage = $"{extracting} {componentName}...";

                LoadingMessage = $"{extracting} {componentName}...";
                IsLoading = true;
                await Task.Yield();

                success = await _downloadManager.ExtractFileAsync(downloadedFile, destinationPath);
                IsLoading = false;
                await Task.Yield();
            }

            if (_disposed) return;

            if (success)
            {
                var hasbeensuccessfullydownloadedandinstalled = _resourceProvider.GetString(
                    "hasbeensuccessfullydownloadedandinstalled", "has been successfully downloaded and installed.");
                StatusMessage = $"{componentName} {hasbeensuccessfullydownloadedandinstalled}";

                await _messageBox.DownloadAndExtractionWereSuccessfulMessageBoxAsync();

                IsStopEnabled = false;
                EndOperation();
                item.State = DownloadButtonState.Downloaded;
            }
            else
            {
                if (_disposed) return;

                if (_downloadManager.IsUserCancellation)
                {
                    var downloadof = _resourceProvider.GetString("Downloadof", "Download of");
                    var wascanceled = _resourceProvider.GetString("wascanceled", "was canceled.");
                    StatusMessage = $"{downloadof} {componentName} {wascanceled}";
                    IsStopEnabled = false;
                }
                else if (_downloadManager.IsDownloadCompleted)
                {
                    var errorFailedtoextract =
                        _resourceProvider.GetString("ErrorFailedtoextract", "Error: Failed to extract");
                    StatusMessage = $"{errorFailedtoextract} {componentName}.";
                    await _messageBox.ShowExtractionFailedMessageBoxAsync(_downloadManager.TempFolder);
                }
                else
                {
                    var errorFailedtoextract =
                        _resourceProvider.GetString("ErrorFailedtoextract", "Error: Failed to extract");
                    StatusMessage = $"{errorFailedtoextract} {componentName}.";

                    await _messageBox.ShowImagePackDownloadErrorMessageBoxAsync(selectedSystem);
                    if (_disposed) return;
                }

                EndOperation();
                item.State = DownloadButtonState.Failed;

                IsStopEnabled = false;
            }
        }
        catch (Exception ex)
        {
            if (_disposed) return;

            var errorduring2 = _resourceProvider.GetString("Errorduring", "Error during");
            var downloadprocess2 = _resourceProvider.GetString("downloadprocess", "download process.");
            StatusMessage = $"{errorduring2} {componentName} {downloadprocess2}";

            // Notify developer only if it's not an expected condition.
            // Disk space errors are user-environment issues; timeouts/connection failures are
            // user/network conditions — log both at Information so the bug report API does not
            // pick them up (see bug 65965). Anything else is a genuine defect.
            if (!(ex is IOException ioEx &&
                  (ioEx.Message.Contains("Insufficient disk space", StringComparison.Ordinal) ||
                   ioEx.Message.Contains("Cannot check disk space", StringComparison.Ordinal))))
            {
                var contextMessage = $"Error downloading {componentName}.\n" +
                                     $"URL: {downloadUrl}";
                if (DownloadErrorClassifier.IsExpectedDownloadException(ex))
                    _logger.Information(ex, contextMessage);
                else
                    _logger.Error(ex, contextMessage);
            }

            if (_downloadManager.IsDownloadCompleted)
                await _messageBox.ShowExtractionFailedMessageBoxAsync(_downloadManager.TempFolder);
            else
                await _messageBox.ShowImagePackDownloadErrorMessageBoxAsync(selectedSystem);

            EndOperation();

            if (_disposed) return;

            IsStopEnabled = false;
            item.State = DownloadButtonState.Failed;
        }
    }

    private EasyModeSystemConfig? GetSelectedSystem()
    {
        return !string.IsNullOrEmpty(SelectedSystemName)
            ? _manager?.Systems?.FirstOrDefault(system =>
                system.SystemName.Equals(SelectedSystemName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private void DownloadManager_ProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        Application.Current?.Dispatcher?.InvokeAsync(() =>
        {
            if (_disposed) return;

            ProgressPercentage = e.ProgressPercentage;
            StatusMessage = e.StatusMessage;
        });
    }

    [RelayCommand]
    private void StopDownload()
    {
        _playSoundEffects.PlayNotificationSound();

        if (_disposed) return;

        _downloadManager.CancelDownload();
        IsStopEnabled = false;
        ProgressPercentage = 0;

        var downloadcanceled2 = _resourceProvider.GetString("Downloadcanceled", "Download canceled");
        StatusMessage = downloadcanceled2;

        foreach (var item in ImagePacksToDisplay)
        {
            if (_disposed) break;

            if (item.State == DownloadButtonState.Downloading) item.State = DownloadButtonState.Failed;
        }

        EndOperation();
    }

    [RelayCommand]
    private async Task HyperlinkNavigateAsync(string url)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();

            // Allowlist http(s) only: download links must never reach a shell handler (WPF-04).
            if (!UrlHelper.TryOpenHttpUrlInBrowser(url))
            {
                _logger.Information($"Blocked non-web or unloadable download link: {url}");
                await _messageBox.CouldNotOpenTheDownloadLinkMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening the download link");

            await _messageBox.CouldNotOpenTheDownloadLinkMessageBoxAsync();
        }
    }

    /// <summary>
    ///     Forces release of the busy overlay and cancels any in-progress download.
    /// </summary>
    public void EmergencyOverlayRelease()
    {
        _playSoundEffects.PlayNotificationSound();

        _downloadManager?.CancelDownload();

        EndOperation();

        IsLoading = false;

        _logger.Debug("[Emergency] User forced overlay dismissal in DownloadImagePackWindow");
        (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
            "Emergency reset performed.");
    }

    /// <summary>
    ///     Performs cleanup when the window is closing.
    /// </summary>
    public async Task CloseWindowRoutineAsync()
    {
        try
        {
            if (IsStopEnabled)
            {
                StopDownload();
                await Task.Delay(200);
            }

            _manager = null;
            Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error closing the Add System window");
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _downloadManager.DownloadProgressChanged -= DownloadManager_ProgressChanged;
            _scope.Dispose();
        }

        _disposed = true;
    }
}