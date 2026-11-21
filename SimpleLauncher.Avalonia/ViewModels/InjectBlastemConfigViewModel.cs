using System.Globalization;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;
using BlastemConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.BlastemConfigurationService;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the BlastEm emulator configuration injection window.
/// </summary>
internal partial class InjectBlastemConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial string Aspect { get; set; } = "";

    [ObservableProperty] public partial string AudioRate { get; set; } = "";

    private string _emulatorPath = "";

    [ObservableProperty] public partial bool Fullscreen { get; set; }

    [ObservableProperty] public partial string Scaling { get; set; } = "";

    [ObservableProperty] public partial bool Scanlines { get; set; }

    [ObservableProperty] public partial bool ShowBeforeLaunch { get; set; }

    [ObservableProperty] public partial string SyncSource { get; set; } = "";

    [ObservableProperty] public partial bool Vsync { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectBlastemConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectBlastemConfigViewModel(
        SettingsManagerService settings,
        IMessageBoxLibraryService messageBox,
        EmulatorPathResolver emulatorPathResolver,
        ILogger logger)
    {
        _settings = settings;
        _logger = logger;
        _messageBox = messageBox;
        _emulatorPathResolver = emulatorPathResolver;
    }

    /// <summary>
    ///     Available aspect ratio options for BlastEm.
    /// </summary>
    public IList<string> AspectOptions { get; } = ["4:3", "16:9", "stretch"];

    /// <summary>
    ///     Available scaling options for BlastEm.
    /// </summary>
    public IList<string> ScalingOptions { get; } = ["linear", "nearest"];

    /// <summary>
    ///     Available audio sample rate options for BlastEm.
    /// </summary>
    public IList<string> AudioRateOptions { get; } = ["48000", "44100", "22050"];

    /// <summary>
    ///     Available sync source options for BlastEm.
    /// </summary>
    public IList<string> SyncSourceOptions { get; } = ["audio", "video"];

    /// <summary>
    ///     Gets whether the configuration is being injected from launcher mode.
    /// </summary>
    public bool IsLauncherMode { get; private set; }

    /// <summary>
    ///     Gets whether the emulator should be launched after configuration injection.
    /// </summary>
    public bool ShouldRun { get; private set; }

    /// <summary>
    ///     Requests the user to provide the emulator executable path.
    /// </summary>
    public Func<Task<string?>>? RequestEmulatorPath { get; set; }

    /// <summary>
    ///     Gets the owner window for dialog display.
    /// </summary>
    public Func<Window>? GetOwnerWindow { get; set; }

    /// <summary>
    ///     Initializes the ViewModel with the emulator path and launcher mode.
    /// </summary>
    /// <param name="emulatorPath">The file path to the BlastEm emulator executable.</param>
    /// <param name="isLauncherMode">Whether the configuration is being injected from launcher mode.</param>
    public void Initialize(string? emulatorPath, bool isLauncherMode)
    {
        _emulatorPath = emulatorPath ?? "";
        IsLauncherMode = isLauncherMode;
        LoadSettings();
    }

    /// <summary>
    ///     Raised when the window should be closed.
    /// </summary>
    public event EventHandler CloseRequested = null!;

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LoadSettings()
    {
        Fullscreen = _settings.Blastem.Fullscreen;
        Vsync = _settings.Blastem.Vsync;
        Scanlines = _settings.Blastem.Scanlines;
        Aspect = _settings.Blastem.Aspect;
        Scaling = _settings.Blastem.Scaling;
        AudioRate = _settings.Blastem.AudioRate.ToString(CultureInfo.InvariantCulture);
        SyncSource = _settings.Blastem.SyncSource;
        ShowBeforeLaunch = _settings.Blastem.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Blastem.Fullscreen = Fullscreen;
        _settings.Blastem.Vsync = Vsync;
        _settings.Blastem.Scanlines = Scanlines;
        _settings.Blastem.Aspect = Aspect;
        _settings.Blastem.Scaling = Scaling;
        if (int.TryParse(AudioRate, CultureInfo.InvariantCulture, out var audioRate))
            _settings.Blastem.AudioRate = audioRate;

        _settings.Blastem.SyncSource = SyncSource;
        _settings.Blastem.ShowSettingsBeforeLaunch = ShowBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Blastem", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.BlastemEmulatorNotFoundMessageBoxAsync();

        if (RequestEmulatorPath is not { } request) return null;

        var result = await request();
        if (string.IsNullOrEmpty(result)) return null;

        _emulatorPath = result;
        return _emulatorPath;
    }

    private async Task<bool> InjectConfigAsync()
    {
        var path = await EnsureEmulatorPathAsync();
        if (string.IsNullOrEmpty(path))
            throw new OperationCanceledException("User cancelled emulator path selection.");

        try
        {
            BlastemConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (FileNotFoundException ex)
        {
            var errorMsg = $"Configuration file not found for Blastem at: {path}. Details: {ex.Message}";
            _logger.Error(ex, errorMsg);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            var errorMsg = $"Permission denied accessing Blastem configuration at: {path}. Details: {ex.Message}";
            _logger.Error(ex, errorMsg);
            return false;
        }
        catch (IOException ex)
        {
            var errorMsg = $"I/O error while accessing Blastem configuration at: {path}. Details: {ex.Message}";
            _logger.Error(ex, errorMsg);
            return false;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Blastem configuration injection failed for path: {path}. Details: {ex.Message}";
            _logger.Error(ex, errorMsg);
            return false;
        }
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        SaveSettings();
        try
        {
            if (await InjectConfigAsync())
            {
                ShouldRun = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await _messageBox.InjectionFailedGenericMessageBoxAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectBlastemConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleRunButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        SaveSettings();
        try
        {
            if (await InjectConfigAsync())
            {
                await _messageBox.BlastemConfigurationSavedSuccessfullyMessageBoxAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await _messageBox.InjectionFailedGenericMessageBoxAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectBlastemConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}