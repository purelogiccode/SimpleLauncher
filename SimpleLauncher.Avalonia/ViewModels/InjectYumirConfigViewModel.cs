using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the Yumir emulator configuration injection window.
/// </summary>
internal partial class InjectYumirConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = null!;
    [ObservableProperty] public partial bool YumirAutoDetectRegion { get; set; }

    [ObservableProperty] public partial bool YumirForceAspectRatio { get; set; }

    [ObservableProperty] public partial double YumirForcedAspect { get; set; }

    [ObservableProperty] public partial bool YumirFullscreen { get; set; }

    [ObservableProperty] public partial bool YumirMute { get; set; }

    [ObservableProperty] public partial bool YumirPauseWhenUnfocused { get; set; }

    [ObservableProperty] public partial bool YumirReduceLatency { get; set; }

    [ObservableProperty] public partial bool YumirShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial string YumirVideoStandard { get; set; } = null!;

    [ObservableProperty] public partial double YumirVolume { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectYumirConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectYumirConfigViewModel(
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
    ///     Available video standard options for Yumir.
    /// </summary>
    public IList<string> VideoStandardOptions { get; } = ["PAL", "NTSC"];

    /// <summary>
    ///     Available forced aspect ratio display options for Yumir.
    /// </summary>
    public IList<string> ForcedAspectOptions { get; } = ["16:9", "4:3"];

    /// <summary>
    ///     Tags corresponding to the forced aspect ratio options for Yumir.
    /// </summary>
    public IList<string> ForcedAspectTags { get; } = ["1.7777777777777777", "1.3333333333333333"];

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
    /// <param name="emulatorPath">The file path to the Yumir emulator executable.</param>
    /// <param name="isLauncherMode">Whether the configuration is being injected from launcher mode.</param>
    public void Initialize(string? emulatorPath, bool isLauncherMode)
    {
        _emulatorPath = emulatorPath!;
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
        YumirFullscreen = _settings.Yumir.Fullscreen;
        YumirForceAspectRatio = _settings.Yumir.ForceAspectRatio;
        YumirReduceLatency = _settings.Yumir.ReduceLatency;
        YumirMute = _settings.Yumir.Mute;
        YumirVolume = _settings.Yumir.Volume;
        YumirAutoDetectRegion = _settings.Yumir.AutoDetectRegion;
        YumirVideoStandard = _settings.Yumir.VideoStandard;
        YumirPauseWhenUnfocused = _settings.Yumir.PauseWhenUnfocused;
        YumirForcedAspect = _settings.Yumir.ForcedAspect;
        YumirShowSettingsBeforeLaunch = _settings.Yumir.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Yumir.Fullscreen = YumirFullscreen;
        _settings.Yumir.ForceAspectRatio = YumirForceAspectRatio;
        _settings.Yumir.ReduceLatency = YumirReduceLatency;
        _settings.Yumir.Mute = YumirMute;
        _settings.Yumir.Volume = YumirVolume;
        _settings.Yumir.AutoDetectRegion = YumirAutoDetectRegion;
        _settings.Yumir.VideoStandard = YumirVideoStandard;
        _settings.Yumir.PauseWhenUnfocused = YumirPauseWhenUnfocused;
        _settings.Yumir.ForcedAspect = YumirForcedAspect;
        _settings.Yumir.ShowSettingsBeforeLaunch = YumirShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Yumir", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.YumirEmulatorNotFoundMessageBoxAsync();

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
            YumirConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"Yumir configuration injection failed for path: {path}");
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectYumirConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleRunButtonFailure(_logger, ex, emulatorName, _emulatorPath, window, _messageBox);
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
                await _messageBox.YumirConfigurationSavedSuccessfullyMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectYumirConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}