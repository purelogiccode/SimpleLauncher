using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.DownloadService;
using SimpleLauncher.Core.Services.EasyMode;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Services.SystemManager;
using Application = System.Windows.Application;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher;

/// <summary>
///     Window that guides users through downloading, configuring, and launching emulators in easy mode.
/// </summary>
internal partial class EasyModeWindow : IDisposable, INotifyPropertyChanged, ILoadingState
{
    private readonly IConfiguration _configuration;

    private readonly DownloadManager _downloadManager;

    // Track download states for all components
    private readonly Dictionary<string, DownloadButtonState> _downloadStates = new(StringComparer.Ordinal);
    private readonly EasyModeManager _easyModeManager;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;

    private string? _currentDownloadType;
    private bool _disposed;

    // Backing field for DownloadStatus property
    private string _downloadStatus = "";
    private Button? _emergencyReturnButton;
    private EasyModeManager? _manager;

    // Thread-safe operation tracking
    private int _operationInProgressFlag;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EasyModeWindow" /> class.
    /// </summary>
    /// <param name="playSoundEffects">The sound effects service for notification sounds.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="downloadManager">The download manager for handling file downloads.</param>
    /// <param name="easyModeManager">The easy mode manager for system configuration.</param>
    /// <param name="logger">The logger for error logging.</param>
    public EasyModeWindow(PlaySoundEffects playSoundEffects, IConfiguration configuration,
        DownloadManager downloadManager, EasyModeManager easyModeManager, ILogger logger)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _configuration = configuration;
        _playSoundEffects = playSoundEffects;
        _easyModeManager = easyModeManager;
        _logger = logger;
        _messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();

        // Set DataContext for XAML bindings to work
        DataContext = this;

        _downloadManager = downloadManager;

        _downloadManager.DownloadProgressChanged += DownloadManager_ProgressChanged;

        Closed += CloseWindowRoutineAsync;
        Loaded += EasyModeWindowLoadedAsync;

        Loaded += (_, _) =>
        {
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
            {
                _emergencyReturnButton = emergencyBtn;
                emergencyBtn.Click += EmergencyOverlayRelease_Click;
            }
        };
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the emulator has been downloaded.
    /// </summary>
    public bool IsEmulatorDownloaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            UpdateAddSystemButtonState();
        }
    } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the core has been downloaded.
    /// </summary>
    public bool IsCoreDownloaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            UpdateAddSystemButtonState();
        }
    } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 1 has been downloaded.
    /// </summary>
    public bool IsImagePack1Downloaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 2 has been downloaded.
    /// </summary>
    public bool IsImagePack2Downloaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 3 has been downloaded.
    /// </summary>
    public bool IsImagePack3Downloaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 4 has been downloaded.
    /// </summary>
    public bool IsImagePack4Downloaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 5 has been downloaded.
    /// </summary>
    public bool IsImagePack5Downloaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 1 is available for download.
    /// </summary>
    public bool IsImagePack1Available
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 2 is available for download.
    /// </summary>
    public bool IsImagePack2Available
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 3 is available for download.
    /// </summary>
    public bool IsImagePack3Available
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 4 is available for download.
    /// </summary>
    public bool IsImagePack4Available
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether image pack 5 is available for download.
    /// </summary>
    public bool IsImagePack5Available
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets a value indicating whether an operation is currently in progress.
    /// </summary>
    public bool IsOperationInProgress
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
            UpdateAddSystemButtonState();
        }
    }

    public string DownloadStatus
    {
        get => _downloadStatus;
        set
        {
            if (string.Equals(_downloadStatus, value, StringComparison.Ordinal)) return;

            _downloadStatus = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Disposes of resources used by the window.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // Unsubscribe only: _downloadManager and _easyModeManager are injected DI
        // dependencies, not owned by this window. Disposing them here breaks the next
        // open when the container hands out a shared instance (WPF-08).
        _downloadManager.DownloadProgressChanged -= DownloadManager_ProgressChanged;
    }

    /// <summary>
    ///     Sets the loading state of the window, showing or hiding the loading overlay.
    /// </summary>
    /// <param name="isLoading">Whether the window is in a loading state.</param>
    /// <param name="message">Optional message to display on the loading overlay.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        // BeginInvoke (not Invoke): callers include background continuations and
        // close-time finally blocks; blocking here deadlocks on reentrancy and throws
        // once the window is disposed or the dispatcher shuts down (WPF-17).
        if (_disposed) return;

        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_disposed) return;

                LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

                // Ensure the main content area is disabled to prevent Tab-key navigation
                MainContentGrid?.IsEnabled = !isLoading;

                if (isLoading)
                {
                    LoadingOverlay.Content =
                        message ?? (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
                }
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            Log.Debug($"EasyModeWindow SetLoadingState dropped during shutdown: {ex.Message}");
        }
    }

    /// <summary>
    ///     Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged; // INotifyPropertyChanged implementation

    // Helper to get/set download state with notification
    private DownloadButtonState GetDownloadState(string type)
    {
        return _downloadStates.GetValueOrDefault(type, DownloadButtonState.Idle);
    }

    private void SetDownloadState(string type, DownloadButtonState state)
    {
        _downloadStates[type] = state;
        // Update legacy boolean properties for XAML binding
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
    }

    private bool TryStartOperation()
    {
        // Returns true if we successfully started (was 0, now 1)
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // Properties are set from download continuations on pool threads: marshal to the
        // UI thread like MainWindow does, or bindings throw cross-thread (WPF-12).
        if (_disposed) return;

        if (Dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (!_disposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
            catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
            {
                Log.Debug($"EasyModeWindow OnPropertyChanged dropped during shutdown: {ex.Message}");
            }
        }
    }

    private async void EasyModeWindowLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeManagerAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[EasyModeWindowLoadedAsync] Error initializing EasyModeManager");
        }
    }

    private async Task InitializeManagerAsync()
    {
        SetLoadingState(true,
            (string)Application.Current.TryFindResource("Loadingconfiguration") ?? "Loading configuration...");
        try
        {
            await Task.Yield(); // Allow UI to render the loading overlay

            _manager = await _easyModeManager.LoadAsync();

            if (_manager is not { Systems.Count: > 0 })
            {
                await _messageBox.EasyModeUnavailableMessageBoxAsync();
                SystemNameDropdown.IsEnabled = false;
                SystemFolderTextBox.IsEnabled = false;
                DownloadEmulatorButton.IsEnabled = false;
                DownloadCoreButton.IsEnabled = false;
                DownloadImagePackButton1.IsEnabled = false;
                DownloadImagePackButton2.IsEnabled = false;
                DownloadImagePackButton3.IsEnabled = false;
                DownloadImagePackButton4.IsEnabled = false;
                DownloadImagePackButton5.IsEnabled = false;
                AddSystemButton.IsEnabled = false;
                return;
            }

            PopulateSystemDropdown();
        }
        finally
        {
            // Closed on every path (including exceptions) so the overlay cannot stick.
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// Populates the system dropdown with a sorted list of system names based on the configuration data.
    /// The method retrieves the list of system configurations from the EasyModeManager. It filters systems
    /// that have a non-empty and valid `EmulatorDownloadLink` in their corresponding emulator configuration.
    /// System names are then sorted alphabetically before being assigned to the `ItemsSource` property of the
    /// dropdown UI element.
    /// Preconditions:
    /// - `_manager` should be initialized and its `Systems` property should not be null.
    /// - `SystemNameDropdown` should refer to a valid ComboBox.
    /// Postconditions:
    /// - The dropdown is populated with a sorted list of valid system names. If no valid systems are found,
    /// the dropdown is left empty.
    /// Applies to:
    /// - The method is specifically designed for use in the `EasyModeWindow` class and interacts
    /// with its UI components
    /// </summary>
    private void PopulateSystemDropdown()
    {
        try
        {
            if (_manager?.Systems == null)
            {
                SystemNameDropdown.ItemsSource = new List<string>(); // return an empty list
                return;
            }

            var sortedSystemNames = _manager.Systems
                .Where(static system => !string.IsNullOrEmpty(system.Emulators?.Emulator?.EmulatorDownloadLink))
                .Select(static system => system.SystemName)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToList();

            SystemNameDropdown.ItemsSource = sortedSystemNames;
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error populating system dropdown.";
            _logger.Error(ex, contextMessage);

            SystemNameDropdown.ItemsSource = new List<string>(); // Assign an empty list if there's any error
        }
    }

    private void SystemNameDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SystemNameDropdown.SelectedItem == null)
        {
            // Reset all states if no system is selected
            DownloadEmulatorButton.IsEnabled = false;
            DownloadCoreButton.IsEnabled = false;

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

            UpdateAddSystemButtonState();
            SystemFolderTextBox.Text = "";
            return;
        }

        var selectedSystem = _manager?.Systems.FirstOrDefault(system =>
            system.SystemName.Equals(SystemNameDropdown.SelectedItem.ToString(), StringComparison.OrdinalIgnoreCase));
        if (selectedSystem == null)
        {
            // This should ideally not happen if PopulateSystemDropdown is correct, but handle defensively
            return;
        }

        var emulator = selectedSystem.Emulators?.Emulator;
        // Determine if download links exist for image packs (for visibility)
        IsImagePack1Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink) &&
                                !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack2Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink2) &&
                                !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack3Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink3) &&
                                !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack4Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink4) &&
                                !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);
        IsImagePack5Available = !string.IsNullOrEmpty(emulator?.ImagePackDownloadLink5) &&
                                !string.IsNullOrEmpty(emulator.ImagePackDownloadExtractPath);

        // Check if Emulator file already exists on disk. If so, mark it as "downloaded".
        var emulatorLocation = selectedSystem.Emulators?.Emulator?.EmulatorLocation;
        if (!string.IsNullOrEmpty(emulatorLocation))
        {
            var resolvedEmulatorPath = PathHelper.ResolveRelativeToAppDirectory(emulatorLocation);
            SetDownloadState(EasyModeManager.DownloadType.Emulator,
                File.Exists(resolvedEmulatorPath) ? DownloadButtonState.Downloaded : DownloadButtonState.Idle);
        }
        else
        {
            // If no location is defined, it can't exist, so it needs to be downloaded.
            SetDownloadState(EasyModeManager.DownloadType.Emulator, DownloadButtonState.Idle);
        }

        // Check if Core file already exists or is not needed.
        var coreLocation = selectedSystem.Emulators?.Emulator?.CoreLocation;
        var coreDownloadLink = selectedSystem.Emulators?.Emulator?.CoreDownloadLink;
        if (!string.IsNullOrEmpty(coreLocation))
        {
            var resolvedCorePath = PathHelper.ResolveRelativeToAppDirectory(coreLocation);
            SetDownloadState(EasyModeManager.DownloadType.Core,
                File.Exists(resolvedCorePath) ? DownloadButtonState.Downloaded : DownloadButtonState.Idle);
        }
        else
        {
            // If no location is defined, it's considered "ready" only if no download is offered.
            SetDownloadState(EasyModeManager.DownloadType.Core,
                string.IsNullOrEmpty(coreDownloadLink) ? DownloadButtonState.Downloaded : DownloadButtonState.Idle);
        }

        // Reset download status for image packs.
        SetDownloadState(EasyModeManager.DownloadType.ImagePack1,
            string.IsNullOrEmpty(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack2,
            string.IsNullOrEmpty(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink2)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack3,
            string.IsNullOrEmpty(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink3)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack4,
            string.IsNullOrEmpty(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink4)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);
        SetDownloadState(EasyModeManager.DownloadType.ImagePack5,
            string.IsNullOrEmpty(selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink5)
                ? DownloadButtonState.Downloaded
                : DownloadButtonState.Idle);

        // Resolve path for display in the textbox
        SystemFolderTextBox.Text = PathHelper.ResolveRelativeToAppDirectory(selectedSystem.SystemFolder) ?? "";

        UpdateAddSystemButtonState();
    }

    private async void DownloadEmulatorButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                SetDownloadState(EasyModeManager.DownloadType.Emulator, DownloadButtonState.Downloading);
                OnPropertyChanged(nameof(IsEmulatorDownloaded)); // Force UI update for IsEnabled binding
                await HandleDownloadAndExtractComponentAsync(EasyModeManager.DownloadType.Emulator);
            }
            catch (Exception ex)
            {
                SetDownloadState(EasyModeManager.DownloadType.Emulator, DownloadButtonState.Failed);
                OnPropertyChanged(nameof(IsEmulatorDownloaded));
                if (!_disposed) _logger.Error(ex, "Error in DownloadEmulatorButtonClickAsync");
            }
            finally
            {
                if (!_disposed)
                {
                    // EndOperation is called by HandleDownloadAndExtractComponentAsync
                    // Only reset to Failed if still in Downloading state (not if successfully Downloaded)
                    if (GetDownloadState(EasyModeManager.DownloadType.Emulator) == DownloadButtonState.Downloading)
                    {
                        SetDownloadState(EasyModeManager.DownloadType.Emulator, DownloadButtonState.Failed);
                        OnPropertyChanged(nameof(IsEmulatorDownloaded));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error in DownloadEmulatorButtonClickAsync: {ex}");
            _logger.Error(ex, "Error in DownloadEmulatorButtonClickAsync");
        }
    }

    private async void DownloadCoreButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                SetDownloadState(EasyModeManager.DownloadType.Core, DownloadButtonState.Downloading);
                OnPropertyChanged(nameof(IsCoreDownloaded));
                await HandleDownloadAndExtractComponentAsync(EasyModeManager.DownloadType.Core);
            }
            catch (Exception ex)
            {
                SetDownloadState(EasyModeManager.DownloadType.Core, DownloadButtonState.Failed);
                OnPropertyChanged(nameof(IsCoreDownloaded));
                if (!_disposed) _logger.Error(ex, "Error in DownloadCoreButtonClickAsync");
            }
            finally
            {
                if (!_disposed)
                {
                    // EndOperation is called by HandleDownloadAndExtractComponentAsync
                    if (GetDownloadState(EasyModeManager.DownloadType.Core) == DownloadButtonState.Downloading)
                    {
                        SetDownloadState(EasyModeManager.DownloadType.Core, DownloadButtonState.Failed);
                        OnPropertyChanged(nameof(IsCoreDownloaded));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error in DownloadCoreButtonClickAsync: {ex}");
            _logger.Error(ex, "Error in DownloadCoreButtonClickAsync");
        }
    }

    private async void DownloadImagePackButton1ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack1, DownloadButtonState.Downloading);
                OnPropertyChanged(nameof(IsImagePack1Downloaded));
                await HandleDownloadAndExtractComponentAsync(EasyModeManager.DownloadType.ImagePack1);
            }
            catch (Exception ex)
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack1, DownloadButtonState.Failed);
                OnPropertyChanged(nameof(IsImagePack1Downloaded));
                if (!_disposed) _logger.Error(ex, "Error in DownloadImagePackButton1ClickAsync");
            }
            finally
            {
                if (!_disposed)
                {
                    // EndOperation is called by HandleDownloadAndExtractComponentAsync
                    if (GetDownloadState(EasyModeManager.DownloadType.ImagePack1) == DownloadButtonState.Downloading)
                    {
                        SetDownloadState(EasyModeManager.DownloadType.ImagePack1, DownloadButtonState.Failed);
                        OnPropertyChanged(nameof(IsImagePack1Downloaded));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error in DownloadImagePackButton1ClickAsync: {ex}");
            _logger.Error(ex, "Error in DownloadImagePackButton1ClickAsync");
        }
    }

    private async void DownloadImagePackButton2ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack2, DownloadButtonState.Downloading);
                OnPropertyChanged(nameof(IsImagePack2Downloaded));
                await HandleDownloadAndExtractComponentAsync(EasyModeManager.DownloadType.ImagePack2);
            }
            catch (Exception ex)
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack2, DownloadButtonState.Failed);
                OnPropertyChanged(nameof(IsImagePack2Downloaded));
                if (!_disposed) _logger.Error(ex, "Error in DownloadImagePackButton2ClickAsync");
            }
            finally
            {
                if (!_disposed)
                {
                    // EndOperation is called by HandleDownloadAndExtractComponentAsync
                    if (GetDownloadState(EasyModeManager.DownloadType.ImagePack2) == DownloadButtonState.Downloading)
                    {
                        SetDownloadState(EasyModeManager.DownloadType.ImagePack2, DownloadButtonState.Failed);
                        OnPropertyChanged(nameof(IsImagePack2Downloaded));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error in DownloadImagePackButton2ClickAsync: {ex}");
            _logger.Error(ex, "Error in DownloadImagePackButton2ClickAsync");
        }
    }

    private async void DownloadImagePackButton3ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack3, DownloadButtonState.Downloading);
                OnPropertyChanged(nameof(IsImagePack3Downloaded));
                await HandleDownloadAndExtractComponentAsync(EasyModeManager.DownloadType.ImagePack3);
            }
            catch (Exception ex)
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack3, DownloadButtonState.Failed);
                OnPropertyChanged(nameof(IsImagePack3Downloaded));
                if (!_disposed) _logger.Error(ex, "Error in DownloadImagePackButton3ClickAsync");
            }
            finally
            {
                if (!_disposed)
                {
                    // EndOperation is called by HandleDownloadAndExtractComponentAsync
                    if (GetDownloadState(EasyModeManager.DownloadType.ImagePack3) == DownloadButtonState.Downloading)
                    {
                        SetDownloadState(EasyModeManager.DownloadType.ImagePack3, DownloadButtonState.Failed);
                        OnPropertyChanged(nameof(IsImagePack3Downloaded));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error in DownloadImagePackButton3ClickAsync: {ex}");
            _logger.Error(ex, "Error in DownloadImagePackButton3ClickAsync");
        }
    }

    private async void DownloadImagePackButton4ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack4, DownloadButtonState.Downloading);
                OnPropertyChanged(nameof(IsImagePack4Downloaded));
                await HandleDownloadAndExtractComponentAsync(EasyModeManager.DownloadType.ImagePack4);
            }
            catch (Exception ex)
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack4, DownloadButtonState.Failed);
                OnPropertyChanged(nameof(IsImagePack4Downloaded));
                if (!_disposed) _logger.Error(ex, "Error in DownloadImagePackButton4ClickAsync");
            }
            finally
            {
                if (!_disposed)
                {
                    // EndOperation is called by HandleDownloadAndExtractComponentAsync
                    if (GetDownloadState(EasyModeManager.DownloadType.ImagePack4) == DownloadButtonState.Downloading)
                    {
                        SetDownloadState(EasyModeManager.DownloadType.ImagePack4, DownloadButtonState.Failed);
                        OnPropertyChanged(nameof(IsImagePack4Downloaded));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error in DownloadImagePackButton4ClickAsync: {ex}");
            _logger.Error(ex, "Error in DownloadImagePackButton4ClickAsync");
        }
    }

    private async void DownloadImagePackButton5ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (_disposed) return;
            if (!TryStartOperation()) return;

            try
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack5, DownloadButtonState.Downloading);
                OnPropertyChanged(nameof(IsImagePack5Downloaded));
                await HandleDownloadAndExtractComponentAsync(EasyModeManager.DownloadType.ImagePack5);
            }
            catch (Exception ex)
            {
                SetDownloadState(EasyModeManager.DownloadType.ImagePack5, DownloadButtonState.Failed);
                OnPropertyChanged(nameof(IsImagePack5Downloaded));
                if (!_disposed) _logger.Error(ex, "Error in DownloadImagePackButton5ClickAsync");
            }
            finally
            {
                if (!_disposed)
                {
                    // EndOperation is called by HandleDownloadAndExtractComponentAsync
                    if (GetDownloadState(EasyModeManager.DownloadType.ImagePack5) == DownloadButtonState.Downloading)
                    {
                        SetDownloadState(EasyModeManager.DownloadType.ImagePack5, DownloadButtonState.Failed);
                        OnPropertyChanged(nameof(IsImagePack5Downloaded));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error in DownloadImagePackButton5ClickAsync: {ex}");
            _logger.Error(ex, "Error in DownloadImagePackButton5ClickAsync");
        }
    }

    // Helper method to reduce code duplication for downloads and extractions
    private async Task HandleDownloadAndExtractComponentAsync(string type)
    {
        if (_disposed)
        {
            EndOperation();
            return;
        }

        _currentDownloadType = type;

        // State already set by caller to Downloading

        var selectedSystem = GetSelectedSystem();
        if (selectedSystem == null)
        {
            EndOperation();
            return;
        }

        string? downloadUrl;
        string componentName;
        string? easyModeExtractPath;

        switch (type)
        {
            case EasyModeManager.DownloadType.Emulator:
                downloadUrl = selectedSystem.Emulators?.Emulator?.EmulatorDownloadLink;
                easyModeExtractPath = selectedSystem.Emulators?.Emulator?.EmulatorDownloadExtractPath;
                componentName = (string)Application.Current.TryFindResource("Emulator") ?? "Emulator";
                break;
            case EasyModeManager.DownloadType.Core:
                downloadUrl = selectedSystem.Emulators?.Emulator?.CoreDownloadLink;
                easyModeExtractPath = selectedSystem.Emulators?.Emulator?.CoreDownloadExtractPath;
                componentName = (string)Application.Current.TryFindResource("Core") ?? "Core";
                break;
            case EasyModeManager.DownloadType.ImagePack1:
                downloadUrl = selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink;
                easyModeExtractPath = selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath;
                componentName = (string)Application.Current.TryFindResource("ImagePack1") ?? "Image Pack 1";
                break;
            case EasyModeManager.DownloadType.ImagePack2:
                downloadUrl = selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink2;
                easyModeExtractPath = selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath;
                componentName = (string)Application.Current.TryFindResource("ImagePack2") ?? "Image Pack 2";
                break;
            case EasyModeManager.DownloadType.ImagePack3:
                downloadUrl = selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink3;
                easyModeExtractPath = selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath;
                componentName = (string)Application.Current.TryFindResource("ImagePack3") ?? "Image Pack 3";
                break;
            case EasyModeManager.DownloadType.ImagePack4:
                downloadUrl = selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink4;
                easyModeExtractPath = selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath;
                componentName = (string)Application.Current.TryFindResource("ImagePack4") ?? "Image Pack 4";
                break;
            case EasyModeManager.DownloadType.ImagePack5:
                downloadUrl = selectedSystem.Emulators?.Emulator?.ImagePackDownloadLink5;
                easyModeExtractPath = selectedSystem.Emulators?.Emulator?.ImagePackDownloadExtractPath;
                componentName = (string)Application.Current.TryFindResource("ImagePack5") ?? "Image Pack 5";
                break;
            default:
                EndOperation();
                return;
        }

        if (easyModeExtractPath != null)
        {
            var destinationPath = PathHelper.ResolveRelativeToAppDirectory(easyModeExtractPath);

            // Ensure valid URL and destination path
            if (string.IsNullOrEmpty(downloadUrl))
            {
                var errorNodownloadUrLfor = (string)Application.Current.TryFindResource("ErrorNodownloadURLfor") ??
                                            "Error: No download URL for";
                EndOperation();
                DownloadStatus = $"{errorNodownloadUrLfor} {componentName}";
                SetDownloadState(type, DownloadButtonState.Idle); // Reset state on error
                return;
            }

            if (string.IsNullOrEmpty(destinationPath))
            {
                var errorInvalidDestinationPath =
                    (string)Application.Current.TryFindResource("ErrorInvalidDestinationPath") ??
                    "Error: Invalid destination path for";
                DownloadStatus = $"{errorInvalidDestinationPath} {componentName}";

                EndOperation();
                // Notify developer
                _logger.Warning(
                    $"[HandleDownloadAndExtractComponentAsync] Invalid destination path for {componentName}: {easyModeExtractPath}");
                SetDownloadState(type, DownloadButtonState.Idle); // Reset state on error
                return;
            }

            try
            {
                var preparingtodownload = (string)Application.Current.TryFindResource("Preparingtodownload") ??
                                          "Preparing to download";
                DownloadStatus = $"{preparingtodownload} {componentName}...";

                DownloadProgressBar.Visibility = Visibility.Visible;
                DownloadProgressBar.Value = 0;
                StopDownloadButton.IsEnabled = true;

                var success = false;

                var downloading = (string)Application.Current.TryFindResource("Downloading") ?? "Downloading";
                DownloadStatus = $"{downloading} {componentName}...";

                var downloadedFile = await _downloadManager.DownloadFileAsync(downloadUrl);

                if (_disposed)
                {
                    EndOperation();
                    return;
                }

                if (downloadedFile != null && _downloadManager.IsDownloadCompleted)
                {
                    var extracting = (string)Application.Current.TryFindResource("Extracting") ?? "Extracting";
                    DownloadStatus = $"{extracting} {componentName}...";

                    LoadingOverlay.Content = $"{extracting} {componentName}...";
                    LoadingOverlay.Visibility = Visibility.Visible;
                    await Task.Yield();

                    try
                    {
                        success = await _downloadManager.ExtractFileAsync(downloadedFile, destinationPath);
                    }
                    finally
                    {
                        // Closed on every path (including exceptions) so the overlay cannot stick.
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }

                    await Task.Yield();
                }

                if (success)
                {
                    EndOperation();
                    var hasbeensuccessfullydownloadedandinstalled =
                        (string)Application.Current.TryFindResource("hasbeensuccessfullydownloadedandinstalled") ??
                        "has been successfully downloaded and installed.";
                    DownloadStatus = $"{componentName} {hasbeensuccessfullydownloadedandinstalled}";

                    // Notify user
                    await _messageBox.DownloadAndExtractionWereSuccessfulMessageBoxAsync();

                    StopDownloadButton.IsEnabled = false;
                    // Mark as successfully downloaded
                    SetDownloadState(type, DownloadButtonState.Downloaded);
                }
                else // Download was not completed successfully (either cancelled, locked, or other failure)
                {
                    if (_disposed)
                    {
                        EndOperation();
                        return;
                    }

                    if (_downloadManager.IsUserCancellation) // User cancelled the download
                    {
                        var downloadof = (string)Application.Current.TryFindResource("Downloadof") ?? "Download of";
                        var wascanceled = (string)Application.Current.TryFindResource("wascanceled") ?? "was canceled.";
                        DownloadStatus = $"{downloadof} {componentName} {wascanceled}";
                        StopDownloadButton.IsEnabled = false;
                        EndOperation();
                        SetDownloadState(type, DownloadButtonState.Failed); // Re-enable on cancel
                    }
                    else if (_downloadManager
                             .IsFileLockedDuringDownload) // Specific check for file lock during download
                    {
                        await _messageBox.ShowDownloadFileLockedMessageBoxAsync(_downloadManager.TempFolder);
                        EndOperation();
                    }
                    else if
                        (_downloadManager
                         .IsDownloadCompleted) // This means download was completed, but something went wrong *after* (e.g., during cleanup or a very late error)
                    {
                        var errorFailedtoextract =
                            (string)Application.Current.TryFindResource("ErrorFailedtoextract") ??
                            "Error: Failed to extract";
                        DownloadStatus = $"{errorFailedtoextract} {componentName}.";
                        EndOperation();
                        SetDownloadState(type, DownloadButtonState.Failed); // Re-enable on extraction failure
                        await _messageBox.ShowExtractionFailedMessageBoxAsync(_downloadManager.TempFolder);
                    }
                    else // Generic download failure (not user cancelled, not file locked, not extraction failure)
                    {
                        var errorDuringDownload = (string)Application.Current.TryFindResource("Errorduringdownload") ??
                                                  "Error during download";
                        DownloadStatus = $"{errorDuringDownload}: {componentName}.";

                        EndOperation();
                        // Fallback to original behavior for download failures
                        await ShowDownloadErrorDialogAsync(type, selectedSystem);
                        SetDownloadState(type, DownloadButtonState.Failed); // Re-enable on download failure
                    }

                    StopDownloadButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                if (_disposed)
                {
                    EndOperation();
                    return;
                }

                var errorduring2 = (string)Application.Current.TryFindResource("Errorduring") ?? "Error during";
                var downloadprocess2 = (string)Application.Current.TryFindResource("downloadprocess") ??
                                       "download process.";
                DownloadStatus = $"{errorduring2} {componentName} {downloadprocess2}";

                // Notify developer only if it's not an expected condition.
                // Disk space errors are user-environment issues; timeouts/connection failures are
                // user/network conditions — log both at Information so the bug report API does
                // not pick them up (see bug 65965). Anything else is a genuine defect.
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

                // Check if the download failed due to a file lock
                if (_downloadManager.IsFileLockedDuringDownload)
                {
                    EndOperation();
                    await _messageBox.ShowDownloadFileLockedMessageBoxAsync(_downloadManager.TempFolder);
                }

                // If download was completed, the exception was likely during extraction.
                else if (_downloadManager.IsDownloadCompleted)
                {
                    EndOperation();
                    await _messageBox.ShowExtractionFailedMessageBoxAsync(_downloadManager.TempFolder);
                }
                else // Exception was during download
                {
                    EndOperation();
                    await ShowDownloadErrorDialogAsync(type, selectedSystem);
                }

                if (_disposed)
                {
                    EndOperation();
                    return;
                }

                StopDownloadButton.IsEnabled = false;
                SetDownloadState(type, DownloadButtonState.Failed); // Re-enable on exception
                EndOperation();
            }
            finally
            {
                _currentDownloadType = null;
            }
        }
    }

    private Task ShowDownloadErrorDialogAsync(string type, EasyModeSystemConfig selectedSystem)
    {
        switch (type)
        {
            case EasyModeManager.DownloadType.Emulator:
                return _messageBox.ShowEmulatorDownloadErrorMessageBoxAsync(selectedSystem);
            case EasyModeManager.DownloadType.Core:
                return _messageBox.ShowCoreDownloadErrorMessageBoxAsync(selectedSystem);
            case EasyModeManager.DownloadType.ImagePack1:
            case EasyModeManager.DownloadType.ImagePack2:
            case EasyModeManager.DownloadType.ImagePack3:
            case EasyModeManager.DownloadType.ImagePack4:
            case EasyModeManager.DownloadType.ImagePack5:
                return _messageBox.ShowImagePackDownloadErrorMessageBoxAsync(selectedSystem);
            default:
                return _messageBox.DownloadExtractionFailedMessageBoxAsync();
        }
    }

    private void DownloadManager_ProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        if (_disposed) return;

        Dispatcher.InvokeAsync(() =>
        {
            if (_disposed) return;

            DownloadProgressBar.Value = e.ProgressPercentage;
            DownloadStatus = e.StatusMessage;
        });
    }

    private EasyModeSystemConfig? GetSelectedSystem()
    {
        if (_disposed || _manager == null) return null;

        return SystemNameDropdown.SelectedItem != null
            ? _manager.Systems.FirstOrDefault(system =>
                system.SystemName.Equals(SystemNameDropdown.SelectedItem.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private void StopDownloadButton_Click(object? sender, RoutedEventArgs? e)
    {
        _playSoundEffects.PlayNotificationSound();
        if (_disposed) return; // Early exit if window is already disposed

        _downloadManager.CancelDownload();
        StopDownloadButton.IsEnabled = false;
        DownloadProgressBar.Value = 0;

        var cancelingdownload2 =
            (string)Application.Current.TryFindResource("Cancelingdownload") ?? "Canceling download...";
        DownloadStatus = cancelingdownload2;

        if (_currentDownloadType != null)
        {
            // Only re-enable if it was actually downloading
            var currentState = GetDownloadState(_currentDownloadType);
            if (currentState == DownloadButtonState.Downloading)
            {
                SetDownloadState(_currentDownloadType, DownloadButtonState.Failed);
                // Force UI update for the specific button
                var propertyName = GetBooleanPropertyNameForType(_currentDownloadType);
                if (!string.IsNullOrEmpty(propertyName)) OnPropertyChanged(propertyName);
            }

            _currentDownloadType = null;
        }

        // Always reset operation flag to re-enable UI
        EndOperation();
    }

    private async void AddSystemButtonClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            if (!TryStartOperation()) return;

            try
            {
                var selectedSystem = GetSelectedSystem();
                if (selectedSystem == null)
                {
                    EndOperation();
                    return;
                }

                string systemFolderRaw;
                if (!string.IsNullOrWhiteSpace(SystemFolderTextBox.Text))
                    systemFolderRaw = SystemFolderTextBox.Text;
                else
                    systemFolderRaw = Path.Combine("%BASEFOLDER%", "roms", selectedSystem.SystemName);

                var systemImageFolderRaw = selectedSystem.SystemImageFolder;

                try
                {
                    AddSystemButton.IsEnabled = false;

                    LoadingOverlay.Content =
                        (string)Application.Current.TryFindResource("Addingsystemtoconfiguration") ??
                        "Adding system to configuration...";
                    LoadingOverlay.Visibility = Visibility.Visible;
                    await Task.Yield();

                    await SystemManagerService.AddOrUpdateSystemFromEasyModeAsync(selectedSystem,
                        systemFolderRaw);
                    LoadingOverlay.Content = (string)Application.Current.TryFindResource("Creatingsystemfolders") ??
                                             "Creating system folders...";
                    await Task.Yield();

                    var resolvedSystemFolder = PathHelper.ResolveRelativeToAppDirectory(systemFolderRaw);
                    var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(systemImageFolderRaw);

                    if (resolvedSystemFolder != null && resolvedSystemImageFolder != null)
                    {
                        await CreateDefaultSystemFoldersService.CreateFoldersAsync(selectedSystem.SystemName,
                            resolvedSystemFolder, resolvedSystemImageFolder, _configuration, _logger, _messageBox);

                        var systemhasbeensuccessfullyadded =
                            (string)Application.Current.TryFindResource("Systemhasbeensuccessfullyadded") ??
                            "System has been successfully added!";
                        DownloadStatus = systemhasbeensuccessfullyadded;

                        await _messageBox.SystemAddedMessageBoxAsync(selectedSystem.SystemName, resolvedSystemFolder,
                            resolvedSystemImageFolder);
                    }

                    Close();
                }
                catch (InvalidOperationException ex)
                {
                    var errorFailedtoaddsystem =
                        (string)Application.Current.TryFindResource("ErrorFailedtoaddsystem") ??
                        "Error: Failed to add system.";
                    DownloadStatus = $"{errorFailedtoaddsystem} {ex.Message}";

                    await _messageBox.AddSystemFailedMessageBoxAsync(ex.Message);
                }
                catch (Exception ex)
                {
                    var errorFailedtoaddsystem =
                        (string)Application.Current.TryFindResource("ErrorFailedtoaddsystem") ??
                        "Error: Failed to add system.";
                    DownloadStatus = errorFailedtoaddsystem;

                    const string contextMessage = "Unexpected error adding system.";
                    _logger.Error(ex, contextMessage);

                    await _messageBox.AddSystemFailedMessageBoxAsync();
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    await Task.Yield();

                    if (IsLoaded) AddSystemButton.IsEnabled = true;

                    EndOperation();
                }
            }
            catch (Exception ex)
            {
                EndOperation();
                _logger.Error(ex, "Error in AddSystemButtonClickAsync");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in AddSystemButtonClickAsync");
        }
    }

    private void UpdateAddSystemButtonState()
    {
        if (_disposed) return;

        var selectedSystem = GetSelectedSystem();
        if (selectedSystem?.Emulators?.Emulator == null)
        {
            AddSystemButton.IsEnabled = false;
            return;
        }

        var emulatorConfig = selectedSystem.Emulators.Emulator;

        // The emulator is always required if a download link exists.
        var isEmulatorDownloadRequired = !string.IsNullOrEmpty(emulatorConfig.EmulatorDownloadLink);
        var isEmulatorReady = !isEmulatorDownloadRequired || IsEmulatorDownloaded;

        // The core is only required if a download link for it exists.
        var isCoreDownloadRequired = !string.IsNullOrEmpty(emulatorConfig.CoreDownloadLink);
        var isCoreReady = !isCoreDownloadRequired || IsCoreDownloaded;

        // The "Add System" button is enabled if all *required* components (emulator and core) are ready.
        // Image packs are optional and do not affect this logic.
        AddSystemButton.IsEnabled = isEmulatorReady && isCoreReady && !IsOperationInProgress;
    }

    private static string? GetBooleanPropertyNameForType(string type)
    {
        return type switch
        {
            EasyModeManager.DownloadType.Emulator => nameof(IsEmulatorDownloaded),
            EasyModeManager.DownloadType.Core => nameof(IsCoreDownloaded),
            EasyModeManager.DownloadType.ImagePack1 => nameof(IsImagePack1Downloaded),
            EasyModeManager.DownloadType.ImagePack2 => nameof(IsImagePack2Downloaded),
            EasyModeManager.DownloadType.ImagePack3 => nameof(IsImagePack3Downloaded),
            EasyModeManager.DownloadType.ImagePack4 => nameof(IsImagePack4Downloaded),
            EasyModeManager.DownloadType.ImagePack5 => nameof(IsImagePack5Downloaded),
            _ => null
        };
    }

    private async void CloseWindowRoutineAsync(object? sender, EventArgs e)
    {
        try // Top-level catch for async Task method
        {
            if (_emergencyReturnButton != null)
            {
                _emergencyReturnButton.Click -= EmergencyOverlayRelease_Click;
                _emergencyReturnButton = null;
            }

            _downloadManager.DownloadProgressChanged -= DownloadManager_ProgressChanged;

            var wasDownloading = StopDownloadButton.IsEnabled;
            if (wasDownloading) StopDownloadButton_Click(null, null);

            _manager = null;
            Dispose();

            if (wasDownloading)
            {
                // Give the cancelled download time to observe the token and release its
                // resources; cleanup itself has already run, so this delay is non-critical.
                await Task.Delay(200);
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error closing the Add System window");
        }
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var chooseaFolderwithRoMsorIsOs2 =
            (string)Application.Current.TryFindResource("ChooseafolderwithROMsorISOsforthissystem") ??
            "Choose a folder with 'ROMs' or 'ISOs' for this system";

        // Create a new OpenFolderDialog
        var openFolderDialog = new OpenFolderDialog
        {
            Title = chooseaFolderwithRoMsorIsOs2
        };

        // Show the dialog and handle the result
        if (openFolderDialog.ShowDialog() == true) SystemFolderTextBox.Text = openFolderDialog.FolderName;
    }

    private async void Hyperlink_RequestNavigateAsync(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();

            // Allowlist http(s) only: hyperlink URIs must never reach a shell handler (WPF-04).
            if (!UrlHelper.TryOpenHttpUrlInBrowser(e.Uri.AbsoluteUri))
            {
                _logger.Information($"Blocked non-web or unloadable hyperlink: {e.Uri.AbsoluteUri}");
                await _messageBox.CouldNotOpenTheDownloadLinkMessageBoxAsync();
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error opening the download link");

            // Notify user
            await _messageBox.CouldNotOpenTheDownloadLinkMessageBoxAsync();
        }
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        _downloadManager?.CancelDownload();
        EndOperation();

        LoadingOverlay.Visibility = Visibility.Collapsed;
        MainContentGrid?.IsEnabled = true;

        _logger.Debug("[Emergency] User forced overlay dismissal in EasyModeWindow");
        (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
            "Emergency reset performed.");
    }
}