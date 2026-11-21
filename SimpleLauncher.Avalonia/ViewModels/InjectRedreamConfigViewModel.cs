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
///     ViewModel for the Redream emulator configuration injection window.
/// </summary>
internal partial class InjectRedreamConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = "";
    [ObservableProperty] public partial string RedreamAspect { get; set; } = null!;

    [ObservableProperty] public partial string RedreamBroadcast { get; set; } = null!;

    [ObservableProperty] public partial string RedreamCable { get; set; } = null!;

    [ObservableProperty] public partial bool RedreamFramerate { get; set; }

    [ObservableProperty] public partial bool RedreamFrameskip { get; set; }

    [ObservableProperty] public partial string RedreamFullmode { get; set; } = null!;

    [ObservableProperty] public partial int RedreamHeight { get; set; }

    [ObservableProperty] public partial string RedreamLanguage { get; set; } = null!;

    [ObservableProperty] public partial int RedreamLatency { get; set; }

    [ObservableProperty] public partial string RedreamRegion { get; set; } = null!;

    [ObservableProperty] public partial string RedreamRenderer { get; set; } = null!;

    [ObservableProperty] public partial int RedreamRes { get; set; }

    [ObservableProperty] public partial bool RedreamShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial int RedreamVolume { get; set; }

    [ObservableProperty] public partial bool RedreamVsync { get; set; }

    [ObservableProperty] public partial int RedreamWidth { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectRedreamConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectRedreamConfigViewModel(
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
    ///     Available video cable options for Redream.
    /// </summary>
    public IList<string> CableOptions { get; } = ["vga", "composite", "rgb"];

    /// <summary>
    ///     Available broadcast standard options for Redream.
    /// </summary>
    public IList<string> BroadcastOptions { get; } = ["ntsc", "pal", "pal_m", "pal_n"];

    /// <summary>
    ///     Available aspect ratio options for Redream.
    /// </summary>
    public IList<string> AspectOptions { get; } = ["4:3", "16:9", "stretch"];

    /// <summary>
    ///     Available internal resolution options for Redream.
    /// </summary>
    public IList<string> ResOptions { get; } = ["1", "2", "3", "4", "5", "6", "7", "8"];

    /// <summary>
    ///     Available renderer options for Redream.
    /// </summary>
    public IList<string> RendererOptions { get; } = ["hle_perstrip", "hle_perpixel", "lle"];

    /// <summary>
    ///     Available fullscreen mode options for Redream.
    /// </summary>
    public IList<string> FullmodeOptions { get; } = ["windowed", "exclusive fullscreen", "borderless fullscreen"];

    /// <summary>
    ///     Tags corresponding to the fullscreen mode options for Redream.
    /// </summary>
    public IList<string> FullmodeTags { get; } = ["windowed", "exclusive fullscreen", "borderless fullscreen"];

    /// <summary>
    ///     Available window size options for Redream.
    /// </summary>
    public IList<string> WindowSizeOptions { get; } =
    [
        "640x480", "800x600", "1024x768", "1280x960", "1024x576", "1280x720", "1600x900", "1920x1080", "2560x1440",
        "3840x2160", "2560x1080", "3440x1440"
    ];

    /// <summary>
    ///     Available language options for Redream.
    /// </summary>
    public IList<string> LanguageOptions { get; } = ["english", "japanese", "german", "french", "spanish", "italian"];

    /// <summary>
    ///     Available region options for Redream.
    /// </summary>
    public IList<string> RegionOptions { get; } = ["usa", "japan", "europe"];

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
    /// <param name="emulatorPath">The file path to the Redream emulator executable.</param>
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
        RedreamCable = _settings.Redream.Cable;
        RedreamBroadcast = _settings.Redream.Broadcast;
        RedreamVsync = _settings.Redream.Vsync;
        RedreamFrameskip = _settings.Redream.Frameskip;
        RedreamAspect = _settings.Redream.Aspect;
        RedreamRes = _settings.Redream.Res;
        RedreamRenderer = _settings.Redream.Renderer;
        RedreamFullmode = _settings.Redream.Fullmode;
        RedreamWidth = _settings.Redream.Width;
        RedreamHeight = _settings.Redream.Height;
        RedreamLanguage = _settings.Redream.Language;
        RedreamRegion = _settings.Redream.Region;
        RedreamVolume = _settings.Redream.Volume;
        RedreamLatency = _settings.Redream.Latency;
        RedreamFramerate = _settings.Redream.Framerate;
        RedreamShowSettingsBeforeLaunch = _settings.Redream.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Redream.Cable = RedreamCable;
        _settings.Redream.Broadcast = RedreamBroadcast;
        _settings.Redream.Vsync = RedreamVsync;
        _settings.Redream.Frameskip = RedreamFrameskip;
        _settings.Redream.Aspect = RedreamAspect;
        _settings.Redream.Res = RedreamRes;
        _settings.Redream.Renderer = RedreamRenderer;
        _settings.Redream.Fullmode = RedreamFullmode;
        _settings.Redream.Width = RedreamWidth;
        _settings.Redream.Height = RedreamHeight;
        _settings.Redream.Language = RedreamLanguage;
        _settings.Redream.Region = RedreamRegion;
        _settings.Redream.Volume = RedreamVolume;
        _settings.Redream.Latency = RedreamLatency;
        _settings.Redream.Framerate = RedreamFramerate;
        _settings.Redream.ShowSettingsBeforeLaunch = RedreamShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Redream", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.ReDreamEmulatorPathNotFoundMessageBoxAsync();

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
            RedreamConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"Redream configuration injection failed for path: {path}");
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRedreamConfigWindow));
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
                await _messageBox.ReDreamConfigurationInjectedSuccessfullyMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRedreamConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}