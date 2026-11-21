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
///     ViewModel for the RetroArch emulator configuration injection window.
/// </summary>
internal partial class InjectRetroArchConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial string AspectRatioIndex { get; set; } = "";

    [ObservableProperty] public partial bool AudioEnable { get; set; }

    [ObservableProperty] public partial bool AudioMute { get; set; }

    [ObservableProperty] public partial bool AutoLoadState { get; set; }

    [ObservableProperty] public partial bool AutoSaveState { get; set; }

    [ObservableProperty] public partial bool Bilinear { get; set; }

    [ObservableProperty] public partial bool CheevosEnable { get; set; }

    [ObservableProperty] public partial bool CheevosHardcore { get; set; }

    [ObservableProperty] public partial bool DiscordAllow { get; set; }

    private string _emulatorPath = "";
    [ObservableProperty] public partial bool Fullscreen { get; set; }

    [ObservableProperty] public partial bool HardSync { get; set; }

    [ObservableProperty] public partial string MenuDriver { get; set; } = "";

    [ObservableProperty] public partial bool PauseNonActive { get; set; }

    [ObservableProperty] public partial bool Rewind { get; set; }

    [ObservableProperty] public partial bool RunAhead { get; set; }

    [ObservableProperty] public partial bool SaveOnExit { get; set; }

    [ObservableProperty] public partial bool ScaleInteger { get; set; }

    [ObservableProperty] public partial bool ShaderEnable { get; set; }

    [ObservableProperty] public partial bool ShowAdvancedSettings { get; set; }

    [ObservableProperty] public partial bool ShowBeforeLaunch { get; set; }

    [ObservableProperty] public partial bool ThreadedVideo { get; set; }

    [ObservableProperty] public partial string VideoDriver { get; set; } = "";

    [ObservableProperty] public partial bool Vsync { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectRetroArchConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectRetroArchConfigViewModel(
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
    ///     Available video driver options for RetroArch.
    /// </summary>
    public IList<string> VideoDriverOptions { get; } = ["gl", "vulkan", "d3d11", "d3d12", "d3d10", "sdl2"];

    /// <summary>
    ///     Available aspect ratio display options for RetroArch.
    /// </summary>
    public IList<string> AspectRatioIndexOptions { get; } = ["Core Provided", "4:3", "16:9", "16:10"];

    /// <summary>
    ///     Tags corresponding to the aspect ratio options for RetroArch.
    /// </summary>
    public IList<string> AspectRatioIndexTags { get; } = ["22", "0", "1", "2"];

    /// <summary>
    ///     Available menu driver options for RetroArch.
    /// </summary>
    public IList<string> MenuDriverOptions { get; } = ["ozone", "xmb", "rgui", "glui"];

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
    /// <param name="emulatorPath">The file path to the RetroArch emulator executable.</param>
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
        VideoDriver = _settings.RetroArch.VideoDriver;
        Fullscreen = _settings.RetroArch.Fullscreen;
        Vsync = _settings.RetroArch.Vsync;
        ThreadedVideo = _settings.RetroArch.ThreadedVideo;
        Bilinear = _settings.RetroArch.Bilinear;
        AspectRatioIndex = _settings.RetroArch.AspectRatioIndex;
        ScaleInteger = _settings.RetroArch.ScaleInteger;
        ShaderEnable = _settings.RetroArch.ShaderEnable;
        HardSync = _settings.RetroArch.HardSync;
        AudioEnable = _settings.RetroArch.AudioEnable;
        AudioMute = _settings.RetroArch.AudioMute;
        PauseNonActive = _settings.RetroArch.PauseNonActive;
        SaveOnExit = _settings.RetroArch.SaveOnExit;
        AutoSaveState = _settings.RetroArch.AutoSaveState;
        AutoLoadState = _settings.RetroArch.AutoLoadState;
        Rewind = _settings.RetroArch.Rewind;
        RunAhead = _settings.RetroArch.RunAhead;
        MenuDriver = _settings.RetroArch.MenuDriver;
        ShowAdvancedSettings = _settings.RetroArch.ShowAdvancedSettings;
        CheevosEnable = _settings.RetroArch.CheevosEnable;
        CheevosHardcore = _settings.RetroArch.CheevosHardcore;
        DiscordAllow = _settings.RetroArch.DiscordAllow;
        ShowBeforeLaunch = _settings.RetroArch.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.RetroArch.VideoDriver = VideoDriver;
        _settings.RetroArch.Fullscreen = Fullscreen;
        _settings.RetroArch.Vsync = Vsync;
        _settings.RetroArch.ThreadedVideo = ThreadedVideo;
        _settings.RetroArch.Bilinear = Bilinear;
        _settings.RetroArch.AspectRatioIndex = AspectRatioIndex;
        _settings.RetroArch.ScaleInteger = ScaleInteger;
        _settings.RetroArch.ShaderEnable = ShaderEnable;
        _settings.RetroArch.HardSync = HardSync;
        _settings.RetroArch.AudioEnable = AudioEnable;
        _settings.RetroArch.AudioMute = AudioMute;
        _settings.RetroArch.PauseNonActive = PauseNonActive;
        _settings.RetroArch.SaveOnExit = SaveOnExit;
        _settings.RetroArch.AutoSaveState = AutoSaveState;
        _settings.RetroArch.AutoLoadState = AutoLoadState;
        _settings.RetroArch.Rewind = Rewind;
        _settings.RetroArch.RunAhead = RunAhead;
        _settings.RetroArch.MenuDriver = MenuDriver;
        _settings.RetroArch.ShowAdvancedSettings = ShowAdvancedSettings;
        _settings.RetroArch.CheevosEnable = CheevosEnable;
        _settings.RetroArch.CheevosHardcore = CheevosHardcore;
        _settings.RetroArch.DiscordAllow = DiscordAllow;
        _settings.RetroArch.ShowSettingsBeforeLaunch = ShowBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("RetroArch", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.RetroArchemulatorpathnotfoundMessageBoxAsync();

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
            RetroArchConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"RetroArch configuration injection failed for path: {path}");
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
            var emulatorName =
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRetroArchConfigWindow));
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
                await _messageBox.RetroArchConfigurationInjectedSuccessfullyMessageBoxAsync();
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
            var emulatorName =
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRetroArchConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}