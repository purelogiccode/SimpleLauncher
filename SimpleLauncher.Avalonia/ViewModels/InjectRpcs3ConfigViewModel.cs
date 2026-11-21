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
///     ViewModel for the RPCS3 emulator configuration injection window.
/// </summary>
internal partial class InjectRpcs3ConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = "";
    [ObservableProperty] public partial int Rpcs3AnisotropicFilter { get; set; }

    [ObservableProperty] public partial string Rpcs3AspectRatio { get; set; } = "";

    [ObservableProperty] public partial bool Rpcs3AudioBuffering { get; set; }

    [ObservableProperty] public partial string Rpcs3AudioRenderer { get; set; } = "";

    [ObservableProperty] public partial string Rpcs3PpuDecoder { get; set; } = "";

    [ObservableProperty] public partial string Rpcs3Renderer { get; set; } = "";

    [ObservableProperty] public partial string Rpcs3Resolution { get; set; } = "";

    [ObservableProperty] public partial int Rpcs3ResolutionScale { get; set; }

    [ObservableProperty] public partial bool Rpcs3ShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial string Rpcs3SpuDecoder { get; set; } = "";

    [ObservableProperty] public partial bool Rpcs3StartFullscreen { get; set; }

    [ObservableProperty] public partial bool Rpcs3Vsync { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectRpcs3ConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectRpcs3ConfigViewModel(
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
    ///     Available renderer options for RPCS3.
    /// </summary>
    public IList<string> RendererOptions { get; } = ["Vulkan", "OpenGL", "Null"];

    /// <summary>
    ///     Available resolution options for RPCS3.
    /// </summary>
    public IList<string> ResolutionOptions { get; } = ["1280x720", "1920x1080", "2560x1440", "3840x2160"];

    /// <summary>
    ///     Available aspect ratio options for RPCS3.
    /// </summary>
    public IList<string> AspectRatioOptions { get; } = ["16:9", "4:3", "Auto"];

    /// <summary>
    ///     Available resolution scale percentage options for RPCS3.
    /// </summary>
    public IList<string> ResolutionScaleOptions { get; } = ["100", "150", "200", "300"];

    /// <summary>
    ///     Available anisotropic filtering options for RPCS3.
    /// </summary>
    public IList<string> AnisotropicFilterOptions { get; } = ["0", "2", "4", "8", "16"];

    /// <summary>
    ///     Available PPU decoder options for RPCS3.
    /// </summary>
    public IList<string> PpuDecoderOptions { get; } =
        ["Recompiler (LLVM)", "Interpreter (static)", "Interpreter (dynamic)"];

    /// <summary>
    ///     Available SPU decoder options for RPCS3.
    /// </summary>
    public IList<string> SpuDecoderOptions { get; } =
        ["Recompiler (LLVM)", "Recompiler (ASMJIT)", "Interpreter (static)", "Interpreter (dynamic)"];

    /// <summary>
    ///     Available audio renderer options for RPCS3.
    /// </summary>
    public IList<string> AudioRendererOptions { get; } = ["Cubeb", "XAudio2", "Null"];

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
    /// <param name="emulatorPath">The file path to the RPCS3 emulator executable.</param>
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
        Rpcs3Renderer = _settings.Rpcs3.Renderer;
        Rpcs3Resolution = _settings.Rpcs3.Resolution;
        Rpcs3AspectRatio = _settings.Rpcs3.AspectRatio;
        Rpcs3Vsync = _settings.Rpcs3.Vsync;
        Rpcs3ResolutionScale = _settings.Rpcs3.ResolutionScale;
        Rpcs3AnisotropicFilter = _settings.Rpcs3.AnisotropicFilter;
        Rpcs3PpuDecoder = _settings.Rpcs3.PpuDecoder;
        Rpcs3SpuDecoder = _settings.Rpcs3.SpuDecoder;
        Rpcs3AudioRenderer = _settings.Rpcs3.AudioRenderer;
        Rpcs3AudioBuffering = _settings.Rpcs3.AudioBuffering;
        Rpcs3StartFullscreen = _settings.Rpcs3.StartFullscreen;
        Rpcs3ShowSettingsBeforeLaunch = _settings.Rpcs3.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Rpcs3.Renderer = Rpcs3Renderer;
        _settings.Rpcs3.Resolution = Rpcs3Resolution;
        _settings.Rpcs3.AspectRatio = Rpcs3AspectRatio;
        _settings.Rpcs3.Vsync = Rpcs3Vsync;
        _settings.Rpcs3.ResolutionScale = Rpcs3ResolutionScale;
        _settings.Rpcs3.AnisotropicFilter = Rpcs3AnisotropicFilter;
        _settings.Rpcs3.PpuDecoder = Rpcs3PpuDecoder;
        _settings.Rpcs3.SpuDecoder = Rpcs3SpuDecoder;
        _settings.Rpcs3.AudioRenderer = Rpcs3AudioRenderer;
        _settings.Rpcs3.AudioBuffering = Rpcs3AudioBuffering;
        _settings.Rpcs3.StartFullscreen = Rpcs3StartFullscreen;
        _settings.Rpcs3.ShowSettingsBeforeLaunch = Rpcs3ShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("RPCS3", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.Rpcs3EmulatorNotFoundPleaseLocateMessageBoxAsync();

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
            Rpcs3ConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"RPCS3 configuration injection failed for path: {path}");
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRpcs3ConfigWindow));
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
                await _messageBox.Rpcs3ConfigurationSavedSuccessfullyMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectRpcs3ConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}