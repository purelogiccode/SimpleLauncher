using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using MessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the GlobalStatsWindow.
/// </summary>
public class GlobalStatsViewModel : ObservableObject, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IGetListOfFilesService _getListOfFiles;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly Lock _processingLock = new();
    private readonly IResourceProvider _resourceProvider;
    private string _busyOverlayText = "";
    private CancellationTokenSource? _cancellationTokenSource = new();
    private bool _forceClose;
    private GlobalStatsData _globalStats = new();

    // Set once a stats run completes: GlobalStatsData is a class, so a null check cannot
    // tell "never ran" from "ran" — without this an empty default report could be saved.
    private bool _hasStats;
    private string _infoText = "";
    private bool _isBusyOverlayVisible;
    private bool _isCancelOverlayVisible;
    private bool _isProcessing;
    private bool _isSaveButtonVisible;
    private bool _isStartButtonVisible = true;
    private IList<SystemManager> _systemManagers = [];

    private ObservableCollection<SystemStatsData> _systemStats = [];

    /// <summary>Initializes a new instance of the <see cref="GlobalStatsViewModel" />.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="getListOfFiles">The file listing service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public GlobalStatsViewModel(IConfiguration configuration, ILogger logErrors, IGetListOfFilesService getListOfFiles,
        IMessageBoxLibraryService messageBox, IResourceProvider resourceProvider)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logErrors;
        _getListOfFiles = getListOfFiles;
        _messageBox = messageBox;
        _resourceProvider = resourceProvider;

        StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
        SaveReportCommand = new AsyncRelayCommand(SaveReportAsync, () => CanSaveReport);
        ClosingCommand = new AsyncRelayCommand<CancelEventArgs?>(ClosingAsync);
    }

    /// <summary>Gets the command to start the statistics calculation.</summary>
    public IAsyncRelayCommand StartCommand { get; }

    /// <summary>Gets the command to cancel the statistics calculation.</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>Gets the command to save the statistics report to a file.</summary>
    public IAsyncRelayCommand SaveReportCommand { get; }

    /// <summary>Gets the command invoked when the window is closing.</summary>
    public IAsyncRelayCommand<CancelEventArgs?> ClosingCommand { get; }

    private bool _disposed;

    /// <summary>Releases resources used by this ViewModel.</summary>
    public void Dispose()
    {
        CancellationTokenSource? cts;
        lock (_processingLock)
        {
            if (_disposed) return;

            _disposed = true;
            cts = _cancellationTokenSource;
            _cancellationTokenSource = null;
        }

        // Cancel first so in-flight work observes cancellation instead of disposal.
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Initializes the ViewModel with the system managers to analyze.</summary>
    /// <param name="systemManagers">The list of configured system managers.</param>
    public void Initialize(IList<SystemManager> systemManagers)
    {
        _systemManagers = systemManagers ?? throw new ArgumentNullException(nameof(systemManagers));

        // Initialize info text
        InfoText = _resourceProvider.GetString("GlobalStatsExplanation", "");
        BusyOverlayText = _resourceProvider.GetString("Processingpleasewait", "Processing");
    }

    #region Events

    /// <summary>
    ///     Event raised when the window should be closed.
    /// </summary>
    public event EventHandler CloseRequested = null!;

    #endregion

    private async Task StartAsync()
    {
        CancellationTokenSource currentCts;
        lock (_processingLock)
        {
            // Single-flight plus teardown guard: concurrent starts and a racing
            // Dispose can otherwise use a CTS being disposed (WPF-21).
            if (IsProcessing || _disposed) return;

            IsProcessing = true;
            _forceClose = false;

            // Dispose any previous CTS and create a new one atomically
            var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource());
            // ReSharper disable once ConstantConditionalAccessQualifier
            oldCts?.Dispose();

            currentCts = _cancellationTokenSource!;
        }

        try
        {
            try
            {
                if (_systemManagers is null or { Count: 0 })
                {
                    InfoText = _resourceProvider.GetString("GlobalStatsNoSystems",
                        "No systems are configured. Please add systems before calculating global statistics.");
                    return;
                }

                IsStartButtonVisible = false;
                IsSaveButtonVisible = false;
                IsBusyOverlayVisible = true;
                IsCancelOverlayVisible = true;

                await Task.Yield();

                await ProcessGlobalStatsAsync(currentCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!_forceClose) await _messageBox.OperationCancelledMessageBoxAsync();

                ResetUiAfterProcessing();
            }
            catch (Exception ex)
            {
                // A torn-down VM racing in-flight work is shutdown noise, not a bug.
                if (!_disposed) _logger.Error(ex, "An error occurred while calculating Global Statistics");

                if (!_forceClose && !_disposed)
                {
                    try
                    {
                        await _messageBox.ErrorCalculatingStatsMessageBoxAsync();
                    }
                    catch
                    {
                        // Swallow message-box failures to ensure UI is always reset
                    }
                }

                ResetUiAfterProcessing();
            }
            finally
            {
                CancellationTokenSource? ownedCts;
                lock (_processingLock)
                {
                    IsProcessing = false;
                    // Only clear the field if no newer run or Dispose replaced it.
                    ownedCts = ReferenceEquals(_cancellationTokenSource, currentCts)
                        ? _cancellationTokenSource
                        : null;
                    if (ownedCts is not null) _cancellationTokenSource = null;
                }

                // CTS.Dispose is idempotent: safe even if Dispose() already disposed it.
                ownedCts?.Dispose();

                // Close window if user requested it during processing
                if (_forceClose) CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            if (!_disposed) _logger.Error(ex, "An error occurred while calculating Global Statistics");
            ResetUiAfterProcessing();
        }
    }

    private async Task ProcessGlobalStatsAsync(CancellationToken cancellationToken)
    {
        // Sequential calculation
        var systemStatsList = await CalculateSystemStatsSequentialAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        _globalStats = CalculateGlobalStats(systemStatsList);
        _hasStats = true;
        cancellationToken.ThrowIfCancellationRequested();

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        await dispatcher.InvokeAsync(() =>
        {
            SystemStats = new ObservableCollection<SystemStatsData>(systemStatsList);

            var explanation = _resourceProvider.GetString("GlobalStatsExplanation", "Statistics calculated:");
            var totalSystemsText = _resourceProvider.GetString("TotalSystems", "Total Systems:");
            var totalEmulatorsText = _resourceProvider.GetString("TotalEmulators", "Total Emulators:");
            var totalGamesText = _resourceProvider.GetString("TotalGames", "Total Games:");
            var totalImagesText = _resourceProvider.GetString("TotalImages", "Total Matched Images:");
            var totalSystemsWithMissingImagesText =
                _resourceProvider.GetString("TotalSystemsWithMissingImages", "Systems with Missing Images:");
            var appFolderText = _resourceProvider.GetString("ApplicationFolder", "Folder:");
            var totalDiskSizeText = _resourceProvider.GetString("TotalDiskSize", "Disk Size:");

            var statsText = $"{totalSystemsText} {_globalStats.TotalSystems}\n" +
                            $"{totalEmulatorsText} {_globalStats.TotalEmulators}\n" +
                            $"{totalGamesText} {_globalStats.TotalGames:N0}\n" +
                            $"{totalImagesText} {_globalStats.TotalImages:N0}\n" +
                            $"{totalSystemsWithMissingImagesText} {_globalStats.TotalSystemsWithMissingImages}\n" +
                            $"{appFolderText} {AppDomain.CurrentDomain.BaseDirectory}\n" +
                            $"{totalDiskSizeText} {_globalStats.TotalDiskSize / (1024.0 * 1024):N2} MB\n";

            InfoText = explanation + "\n\n" + statsText;

            IsBusyOverlayVisible = false;
            IsCancelOverlayVisible = false;
            IsSaveButtonVisible = true;
        });

        lock (_processingLock)
        {
            // Mark processing as complete BEFORE showing the save dialog.
            // MessageBox.Show pumps UI messages, allowing the user to click the
            // close button while the dialog is open. If _isProcessing were still
            // true, GlobalStatsWindow_Closing would incorrectly prompt to cancel
            // an operation that has already finished.
            IsProcessing = false;

            if (_forceClose)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        DoYouWantToSaveTheReportMessageBoxAsync();
    }

    private Task<List<SystemStatsData>> CalculateSystemStatsSequentialAsync(CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var results = new List<SystemStatsData>();
            var imageExtensions = _configuration.GetValue<string[]>("ImageExtensions") ?? [".png", ".jpg", ".jpeg"];
            var processingText = _resourceProvider.GetString("Processingpleasewait", "Processing");
            var processingSystemText = _resourceProvider.GetString("ProcessingSystem", "Processing system");

            foreach (var systemManager in _systemManagers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Update UI overlay text to show current system
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                        BusyOverlayText = $"{processingText}\n{processingSystemText} {systemManager.SystemName}");
                }

                var allRomFiles = new List<string>();
                foreach (var folderRaw in systemManager.SystemFolders)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = PathHelper.ResolveRelativeToAppDirectory(folderRaw);
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path) &&
                        systemManager.FileFormatsToSearch != null)
                    {
                        var files = await _getListOfFiles.GetFilesAsync(path, systemManager.FileFormatsToSearch,
                            systemManager.DisableRecursiveSearch, systemManager.GroupByFolder, cancellationToken);
                        allRomFiles.AddRange(files);
                    }
                }

                // Sequential Disk Size Calculation (File by File)
                long totalDiskSize = 0;
                foreach (var file in allRomFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var longPath = PathHelper.GetLongPath(file);
                        if (longPath != null) totalDiskSize += new FileInfo(longPath).Length;
                    }
                    catch
                    {
                        // Skip inaccessible files
                    }
                }

                // Image Matching
                var romFileBaseNames =
                    new HashSet<string>(allRomFiles.Select(static f => Path.GetFileNameWithoutExtension(f) ?? ""),
                        StringComparer.OrdinalIgnoreCase);
                var systemImageFolder = systemManager.SystemImageFolder;
                var resolvedImagePath = string.IsNullOrEmpty(systemImageFolder)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", systemManager.SystemName)
                    : PathHelper.ResolveRelativeToAppDirectory(systemImageFolder);

                var numberOfImages = 0;
                if (Directory.Exists(resolvedImagePath))
                {
                    var imageFiles = Directory.EnumerateFiles(resolvedImagePath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => imageExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        .Select(Path.GetFileNameWithoutExtension);

                    numberOfImages = imageFiles.Count(f => romFileBaseNames.Contains(f ?? ""));
                }

                results.Add(new SystemStatsData
                {
                    SystemName = systemManager.SystemName,
                    NumberOfFiles = allRomFiles.Count,
                    NumberOfImages = numberOfImages,
                    TotalDiskSize = totalDiskSize
                });
            }

            return results.OrderBy(static s => s.SystemName, StringComparer.Ordinal).ToList();
        }, cancellationToken);
    }

    private GlobalStatsData CalculateGlobalStats(List<SystemStatsData> systemStats)
    {
        return new GlobalStatsData
        {
            TotalSystems = systemStats.Count,
            // Emulators is null-forgiving but can be null at runtime (deserialization skew).
            TotalEmulators = _systemManagers.Sum(static c => c.Emulators?.Count ?? 0),
            TotalGames = systemStats.Sum(static s => s.NumberOfFiles),
            TotalImages = systemStats.Sum(static s => s.NumberOfImages),
            TotalDiskSize = systemStats.Sum(static s => s.TotalDiskSize),
            TotalSystemsWithMissingImages = systemStats.Count(static s => s.NumberOfFiles > s.NumberOfImages)
        };
    }

    private void ResetUiAfterProcessing()
    {
        IsBusyOverlayVisible = false;
        IsCancelOverlayVisible = false;
        IsStartButtonVisible = true;
        IsSaveButtonVisible = false;
        SystemStats.Clear();
        InfoText = _resourceProvider.GetString("GlobalStatsExplanation", "");
        BusyOverlayText = _resourceProvider.GetString("Processingpleasewait", "Processing");
    }

    private async void DoYouWantToSaveTheReportMessageBoxAsync()
    {
        try
        {
            var result = await _messageBox.WouldYouLikeToSaveAReportMessageBoxAsync();
            if (result == MessageBoxResult.Yes) await SaveReportAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method DoYouWantToSaveTheReportMessageBoxAsync");
        }
    }

    private async Task SaveReportAsync()
    {
        // Gate on _hasStats so an empty default report cannot be saved when stats never ran.
        if (!_hasStats) return;

        var saveFileDialog = new SaveFileDialog
        {
            FileName = "GlobalStatsReport",
            DefaultExt = ".txt",
            Filter = "Text documents (.txt)|*.txt"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var systemStatsList = SystemStats.ToList();
                await File.WriteAllTextAsync(saveFileDialog.FileName,
                    GenerateReportText(_globalStats, systemStatsList));
                await _messageBox.ReportSavedMessageBoxAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save report");
                await _messageBox.FailedSaveReportMessageBoxAsync();
            }
        }
    }

    private string GenerateReportText(GlobalStatsData globalStats, IEnumerable<SystemStatsData> systemStats)
    {
        var titleText = _resourceProvider.GetString("GlobalStatsReportTitle", "Global Stats Report");
        var totalSystemsText = _resourceProvider.GetString("TotalSystems", "Total Systems:");
        var totalGamesText = _resourceProvider.GetString("TotalGames", "Total Games:");
        var totalDiskSizeText = _resourceProvider.GetString("TotalDiskSize", "Disk Size:");
        var systemSpecificsText = _resourceProvider.GetString("SystemSpecifics", "System Specifics:");
        var gamesText = _resourceProvider.GetString("Games", "Games");
        var imagesText = _resourceProvider.GetString("Images", "Images");

        var report = $"{titleText}\n-------------------\n" +
                     $"{totalSystemsText} {globalStats.TotalSystems}\n" +
                     $"{totalGamesText} {globalStats.TotalGames:N0}\n" +
                     $"{totalDiskSizeText} {globalStats.TotalDiskSize / (1024.0 * 1024):N2} MB\n\n" +
                     $"{systemSpecificsText}\n-------------------\n";

        return systemStats.Aggregate(report,
            (current, s) =>
                current + $"{s.SystemName}: {s.NumberOfFiles} {gamesText}, {s.NumberOfImages} {imagesText}\n");
    }

    private void Cancel()
    {
        if (_cancellationTokenSource is { IsCancellationRequested: false })
        {
            _cancellationTokenSource.Cancel();
            BusyOverlayText = _resourceProvider.GetString("CancellingPleasewait", "Cancelling...");
            IsCancelOverlayVisible = false;
        }
    }

    /// <summary>Cancels any in-progress processing and releases the busy overlay.</summary>
    public void EmergencyOverlayRelease()
    {
        Cancel();
    }

    private async Task ClosingAsync(CancelEventArgs? e)
    {
        try
        {
            bool needsConfirmation;
            lock (_processingLock)
            {
                // Window disposal is owned by the window itself; never Dispose here while
                // the prompt below may still be awaiting (WPF-21).
                if (_disposed || !IsProcessing)
                {
                    // Not processing, allow normal close
                    return;
                }

                if (e is null)
                {
                    // No event to defer: cancel processing and let the close proceed.
                    _forceClose = true;
                    needsConfirmation = false;
                }
                else
                {
                    // Processing is active - cancel the close and ask user to confirm
                    e.Cancel = true;
                    needsConfirmation = true;
                }
            }

            if (e is null)
            {
                Cancel();
                return;
            }

            if (needsConfirmation)
            {
                var result = await _messageBox.DoYouWantToCancelAndCloseMessageBoxAsync();
                if (result == MessageBoxResult.Yes)
                {
                    lock (_processingLock)
                    {
                        _forceClose = true;
                    }

                    Cancel();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method Closing");
        }
    }

    #region Properties

    /// <summary>
    ///     Gets or sets the system statistics data.
    /// </summary>
    public ObservableCollection<SystemStatsData> SystemStats
    {
        get => _systemStats;
        private set => SetProperty(ref _systemStats, value);
    }

    /// <summary>
    ///     Gets or sets the info text displayed at the top.
    /// </summary>
    public string InfoText
    {
        get => _infoText;
        private set => SetProperty(ref _infoText, value);
    }

    /// <summary>
    ///     Gets or sets the busy overlay text.
    /// </summary>
    public string BusyOverlayText
    {
        get => _busyOverlayText;
        private set => SetProperty(ref _busyOverlayText, value);
    }

    /// <summary>
    ///     Gets or sets whether processing is in progress.
    /// </summary>
    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (SetProperty(ref _isProcessing, value))
            {
                StartCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether the busy overlay is visible.
    /// </summary>
    public bool IsBusyOverlayVisible
    {
        get => _isBusyOverlayVisible;
        private set => SetProperty(ref _isBusyOverlayVisible, value);
    }

    /// <summary>
    ///     Gets or sets whether the cancel overlay is visible.
    /// </summary>
    public bool IsCancelOverlayVisible
    {
        get => _isCancelOverlayVisible;
        private set => SetProperty(ref _isCancelOverlayVisible, value);
    }

    /// <summary>
    ///     Gets or sets whether the save button is visible.
    /// </summary>
    public bool IsSaveButtonVisible
    {
        get => _isSaveButtonVisible;
        private set
        {
            if (SetProperty(ref _isSaveButtonVisible, value)) SaveReportCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    ///     Gets or sets whether the start button is visible.
    /// </summary>
    public bool IsStartButtonVisible
    {
        get => _isStartButtonVisible;
        private set => SetProperty(ref _isStartButtonVisible, value);
    }

    #endregion

    #region CanExecute Properties

    private bool CanStart => !IsProcessing;
    private bool CanCancel => IsProcessing;
    private bool CanSaveReport => IsSaveButtonVisible;

    #endregion
}