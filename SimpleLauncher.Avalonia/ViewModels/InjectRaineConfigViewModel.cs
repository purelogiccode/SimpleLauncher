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
///     ViewModel for the Raine emulator configuration injection window.
/// </summary>
internal partial class InjectRaineConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = null!;
    private string _gameFilePath = null!;
    [ObservableProperty] public partial bool RaineFixAspectRatio { get; set; }

    [ObservableProperty] public partial int RaineFrameSkip { get; set; }

    [ObservableProperty] public partial bool RaineFullscreen { get; set; }

    [ObservableProperty] public partial int RaineMusicVolume { get; set; }

    [ObservableProperty] public partial bool RaineMuteMusic { get; set; }

    [ObservableProperty] public partial bool RaineMuteSfx { get; set; }

    [ObservableProperty] public partial string RaineNeoCdBios { get; set; } = null!;

    [ObservableProperty] public partial int RaineResX { get; set; }

    [ObservableProperty] public partial int RaineResY { get; set; }

    [ObservableProperty] public partial string RaineRomDirectory { get; set; } = null!;

    [ObservableProperty] public partial int RaineSampleRate { get; set; }

    [ObservableProperty] public partial int RaineSfxVolume { get; set; }

    [ObservableProperty] public partial bool RaineShowFps { get; set; }

    [ObservableProperty] public partial bool RaineShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial string RaineSoundDriver { get; set; } = null!;

    [ObservableProperty] public partial bool RaineVsync { get; set; }

    private string _systemRomPath = null!;

    /// <summary>Initializes a new instance of the <see cref="InjectRaineConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectRaineConfigViewModel(SettingsManagerService settings, IMessageBoxLibraryService messageBox,
        EmulatorPathResolver emulatorPathResolver, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
        _messageBox = messageBox;
        _emulatorPathResolver = emulatorPathResolver;
    }

    /// <summary>
    ///     Available sound driver options for Raine.
    /// </summary>
    public IList<string> SoundDriverOptions { get; } = ["directsound", "sdl"];

    /// <summary>
    ///     Available audio sample rate options for Raine.
    /// </summary>
    public IList<string> SampleRateOptions { get; } = ["22050", "44100", "48000"];

    /// <summary>
    ///     Available frame skip options for Raine.
    /// </summary>
    public IList<string> FrameSkipOptions { get; } = ["0", "1", "2", "3", "4", "5"];

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
    ///     Requests the user to select a file path.
    /// </summary>
    public Func<Task<string?>>? RequestFilePath { get; set; }

    /// <summary>
    ///     Requests the user to select a folder path.
    /// </summary>
    public Func<Task<string?>>? RequestFolderPath { get; set; }

    /// <summary>
    ///     Initializes the ViewModel with the emulator path, launcher mode, and optional file paths.
    /// </summary>
    /// <param name="emulatorPath">The file path to the Raine emulator executable.</param>
    /// <param name="isLauncherMode">Whether the configuration is being injected from launcher mode.</param>
    /// <param name="gameFilePath">Optional path to the game file.</param>
    /// <param name="systemRomPath">Optional path to the system ROM.</param>
    public void Initialize(string? emulatorPath, bool isLauncherMode, string? gameFilePath = null,
        string? systemRomPath = null)
    {
        _emulatorPath = emulatorPath ?? "";
        _gameFilePath = gameFilePath ?? "";
        _systemRomPath = systemRomPath ?? "";
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
        RaineFullscreen = _settings.Raine.Fullscreen;
        RaineFixAspectRatio = _settings.Raine.FixAspectRatio;
        RaineVsync = _settings.Raine.Vsync;
        RaineResX = _settings.Raine.ResX;
        RaineResY = _settings.Raine.ResY;
        RaineSoundDriver = _settings.Raine.SoundDriver;
        RaineSampleRate = _settings.Raine.SampleRate;
        RaineShowSettingsBeforeLaunch = _settings.Raine.ShowSettingsBeforeLaunch;
        RaineShowFps = _settings.Raine.ShowFps;
        RaineFrameSkip = _settings.Raine.FrameSkip;
        RaineNeoCdBios = _settings.Raine.NeoCdBios ?? "";
        RaineMusicVolume = _settings.Raine.MusicVolume;
        RaineSfxVolume = _settings.Raine.SfxVolume;
        RaineMuteSfx = _settings.Raine.MuteSfx;
        RaineMuteMusic = _settings.Raine.MuteMusic;
        RaineRomDirectory = _settings.Raine.RomDirectory ?? "";
    }

    private void SaveSettings()
    {
        _settings.Raine.Fullscreen = RaineFullscreen;
        _settings.Raine.FixAspectRatio = RaineFixAspectRatio;
        _settings.Raine.Vsync = RaineVsync;
        _settings.Raine.ResX = RaineResX;
        _settings.Raine.ResY = RaineResY;
        _settings.Raine.SoundDriver = RaineSoundDriver;
        _settings.Raine.SampleRate = RaineSampleRate;
        _settings.Raine.ShowSettingsBeforeLaunch = RaineShowSettingsBeforeLaunch;
        _settings.Raine.ShowFps = RaineShowFps;
        _settings.Raine.FrameSkip = RaineFrameSkip;
        _settings.Raine.NeoCdBios = RaineNeoCdBios;
        _settings.Raine.MusicVolume = RaineMusicVolume;
        _settings.Raine.SfxVolume = RaineSfxVolume;
        _settings.Raine.MuteSfx = RaineMuteSfx;
        _settings.Raine.MuteMusic = RaineMuteMusic;
        _settings.Raine.RomDirectory = RaineRomDirectory;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Raine", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.RaineExecutableNotFoundMessageBoxAsync();

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
            RaineConfigurationService.InjectSettings(path, _settings, _logger, _gameFilePath, _systemRomPath,
                _settings.Raine.RomDirectory);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"Raine configuration injection failed for path: {path}");
            return false;
        }
    }

    [RelayCommand]
    private async Task SelectNeoCdBiosAsync()
    {
        if (RequestFilePath is not { } request) return;

        var result = await request();
        if (!string.IsNullOrEmpty(result)) RaineNeoCdBios = result;
    }

    [RelayCommand]
    private async Task SelectRaineRomDirectoryAsync()
    {
        if (RequestFolderPath is not { } request) return;

        var result = await request();
        if (!string.IsNullOrEmpty(result)) RaineRomDirectory = result;
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRaineConfigWindow));
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
                await _messageBox.RaineSettingsSavedAndInjectedMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRaineConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}