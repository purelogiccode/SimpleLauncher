using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the Xenia emulator configuration injection window.
/// </summary>
internal partial class InjectXeniaConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = null!;
    [ObservableProperty] public partial string XeniaAa { get; set; } = null!;

    [ObservableProperty] public partial bool XeniaApplyPatches { get; set; }

    [ObservableProperty] public partial string XeniaApu { get; set; } = null!;

    [ObservableProperty] public partial bool XeniaFullscreen { get; set; }

    [ObservableProperty] public partial bool XeniaGammaSrgb { get; set; }

    [ObservableProperty] public partial string XeniaGpu { get; set; } = null!;

    [ObservableProperty] public partial string XeniaHid { get; set; } = null!;

    [ObservableProperty] public partial bool XeniaMountCache { get; set; }

    [ObservableProperty] public partial bool XeniaMute { get; set; }

    [ObservableProperty] public partial string XeniaReadbackResolve { get; set; } = null!;

    [ObservableProperty] public partial int XeniaResScaleX { get; set; }

    [ObservableProperty] public partial int XeniaResScaleY { get; set; }

    [ObservableProperty] public partial string XeniaScaling { get; set; } = null!;

    [ObservableProperty] public partial bool XeniaShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial int XeniaUserLanguage { get; set; }

    [ObservableProperty] public partial bool XeniaVibration { get; set; }

    [ObservableProperty] public partial bool XeniaVsync { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectXeniaConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectXeniaConfigViewModel(
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
    ///     Available GPU backend options for Xenia.
    /// </summary>
    public IList<string> GpuOptions { get; } = ["d3d12", "vulkan", "null"];

    /// <summary>
    ///     Available resolution scale options for Xenia.
    /// </summary>
    public IList<string> ResScaleOptions { get; } = ["1", "2", "3"];

    /// <summary>
    ///     Available anti-aliasing options for Xenia.
    /// </summary>
    public IList<TagOption> AaOptions { get; } =
    [
        new("", "None"),
        new("fxaa", "FXAA"),
        new("fxaa_extreme", "FXAA Extreme")
    ];

    /// <summary>
    ///     Available scaling options for Xenia.
    /// </summary>
    public IList<string> ScalingOptions { get; } = ["fsr", "cas", "bilinear"];

    /// <summary>
    ///     Available readback resolve options for Xenia.
    /// </summary>
    public IList<string> ReadbackOptions { get; } = ["none", "fast", "full"];

    /// <summary>
    ///     Available APU (audio processing unit) options for Xenia.
    /// </summary>
    public IList<string> ApuOptions { get; } = ["xaudio2", "sdl", "nop", "any"];

    /// <summary>
    ///     Available HID (human interface device) input options for Xenia.
    /// </summary>
    public IList<string> HidOptions { get; } = ["xinput", "sdl", "winkey", "any"];

    /// <summary>
    ///     Available language options for Xenia.
    /// </summary>
    public IList<TagOption> LangOptions { get; } =
    [
        new("1", "English"),
        new("2", "Japanese"),
        new("3", "German"),
        new("4", "French"),
        new("5", "Spanish"),
        new("6", "Italian"),
        new("7", "Korean"),
        new("8", "Chinese"),
        new("9", "Portuguese")
    ];

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
    /// <param name="emulatorPath">The file path to the Xenia emulator executable.</param>
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
        XeniaGpu = _settings.Xenia.Gpu;
        XeniaVsync = _settings.Xenia.Vsync;
        XeniaFullscreen = _settings.Xenia.Fullscreen;
        XeniaResScaleX = _settings.Xenia.ResScaleX;
        XeniaResScaleY = _settings.Xenia.ResScaleY;
        XeniaAa = _settings.Xenia.Aa;
        XeniaScaling = _settings.Xenia.Scaling;
        XeniaReadbackResolve = _settings.Xenia.ReadbackResolve;
        XeniaGammaSrgb = _settings.Xenia.GammaSrgb;
        XeniaApu = _settings.Xenia.Apu;
        XeniaMute = _settings.Xenia.Mute;
        XeniaMountCache = _settings.Xenia.MountCache;
        XeniaVibration = _settings.Xenia.Vibration;
        XeniaApplyPatches = _settings.Xenia.ApplyPatches;
        XeniaHid = _settings.Xenia.Hid;
        XeniaUserLanguage = _settings.Xenia.UserLanguage;
        XeniaShowSettingsBeforeLaunch = _settings.Xenia.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Xenia.Gpu = XeniaGpu;
        _settings.Xenia.Vsync = XeniaVsync;
        _settings.Xenia.Fullscreen = XeniaFullscreen;
        _settings.Xenia.ResScaleX = XeniaResScaleX;
        _settings.Xenia.ResScaleY = XeniaResScaleY;
        _settings.Xenia.Aa = XeniaAa;
        _settings.Xenia.Scaling = XeniaScaling;
        _settings.Xenia.ReadbackResolve = XeniaReadbackResolve;
        _settings.Xenia.GammaSrgb = XeniaGammaSrgb;
        _settings.Xenia.Apu = XeniaApu;
        _settings.Xenia.Mute = XeniaMute;
        _settings.Xenia.MountCache = XeniaMountCache;
        _settings.Xenia.Vibration = XeniaVibration;
        _settings.Xenia.ApplyPatches = XeniaApplyPatches;
        _settings.Xenia.Hid = XeniaHid;
        _settings.Xenia.UserLanguage = XeniaUserLanguage;
        _settings.Xenia.ShowSettingsBeforeLaunch = XeniaShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Xenia", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.XeniaemulatorpathnotfoundMessageBoxAsync();

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
            XeniaConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"Xenia configuration injection failed for path: {path}");
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectXeniaConfigWindow));
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
                await _messageBox.XeniaconfigurationinjectedsuccessfullyMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectXeniaConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}