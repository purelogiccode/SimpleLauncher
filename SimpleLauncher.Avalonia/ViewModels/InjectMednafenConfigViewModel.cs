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
///     ViewModel for the Mednafen emulator configuration injection window.
/// </summary>
internal partial class InjectMednafenConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = null!;
    [ObservableProperty] public partial bool MednafenBilinear { get; set; }

    [ObservableProperty] public partial bool MednafenCheats { get; set; }

    [ObservableProperty] public partial bool MednafenFullscreen { get; set; }

    [ObservableProperty] public partial bool MednafenRewind { get; set; }

    [ObservableProperty] public partial int MednafenScanlines { get; set; }

    [ObservableProperty] public partial string MednafenShader { get; set; } = null!;

    [ObservableProperty] public partial bool MednafenShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial string MednafenSpecial { get; set; } = null!;

    [ObservableProperty] public partial string MednafenStretch { get; set; } = null!;

    [ObservableProperty] public partial string MednafenVideoDriver { get; set; } = null!;

    [ObservableProperty] public partial int MednafenVolume { get; set; }

    [ObservableProperty] public partial bool MednafenVsync { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectMednafenConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectMednafenConfigViewModel(SettingsManagerService settings, IMessageBoxLibraryService messageBox,
        EmulatorPathResolver emulatorPathResolver, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
        _messageBox = messageBox;
        _emulatorPathResolver = emulatorPathResolver;
    }

    /// <summary>
    ///     Available video driver options for Mednafen.
    /// </summary>
    public IList<string> VideoDriverOptions { get; } = ["opengl", "soft", "default"];

    /// <summary>
    ///     Available stretch mode options for Mednafen.
    /// </summary>
    public IList<string> StretchOptions { get; } = ["0", "full", "aspect", "aspect_int"];

    /// <summary>
    ///     Available shader options for Mednafen.
    /// </summary>
    public IList<string> ShaderOptions { get; } = ["none", "ip", "ipsharper", "scale2x", "snes_ntsc", "goat"];

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
    /// <param name="emulatorPath">The file path to the Mednafen emulator executable.</param>
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
        MednafenVideoDriver = _settings.Mednafen.VideoDriver;
        MednafenStretch = _settings.Mednafen.Stretch;

        if (!string.IsNullOrEmpty(_settings.Mednafen.Special) &&
            !string.Equals(_settings.Mednafen.Special, "none", StringComparison.Ordinal))
        {
            MednafenShader = _settings.Mednafen.Special;
        }
        else
        {
            MednafenShader = _settings.Mednafen.Shader;
        }

        MednafenSpecial = _settings.Mednafen.Special;
        MednafenFullscreen = _settings.Mednafen.Fullscreen;
        MednafenVsync = _settings.Mednafen.Vsync;
        MednafenBilinear = _settings.Mednafen.Bilinear;
        MednafenScanlines = _settings.Mednafen.Scanlines;
        MednafenVolume = _settings.Mednafen.Volume;
        MednafenCheats = _settings.Mednafen.Cheats;
        MednafenRewind = _settings.Mednafen.Rewind;
        MednafenShowSettingsBeforeLaunch = _settings.Mednafen.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Mednafen.VideoDriver = MednafenVideoDriver;
        _settings.Mednafen.Stretch = MednafenStretch;

        if (MednafenShader is "scale2x" or "snes_ntsc")
        {
            _settings.Mednafen.Special = MednafenShader;
            _settings.Mednafen.Shader = "none";
        }
        else
        {
            _settings.Mednafen.Special = "none";
            _settings.Mednafen.Shader = MednafenShader;
        }

        _settings.Mednafen.Fullscreen = MednafenFullscreen;
        _settings.Mednafen.Vsync = MednafenVsync;
        _settings.Mednafen.Bilinear = MednafenBilinear;
        _settings.Mednafen.Scanlines = MednafenScanlines;
        _settings.Mednafen.Volume = MednafenVolume;
        _settings.Mednafen.Cheats = MednafenCheats;
        _settings.Mednafen.Rewind = MednafenRewind;
        _settings.Mednafen.ShowSettingsBeforeLaunch = MednafenShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Mednafen", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.MednafenEmulatorNotFoundMessageBoxAsync();

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
            MednafenConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"Mednafen configuration injection failed for path: {path}");
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectMednafenConfigWindow));
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
                await _messageBox.MednafenConfigurationSavedSuccessfullyMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectMednafenConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}