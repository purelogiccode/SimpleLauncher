using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.InjectConfigWindows;
using SimpleLauncher.Services.InjectEmulatorConfig;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the DuckStation emulator configuration injection window.
/// </summary>
public partial class InjectDuckStationConfigViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial string DuckStationAspectRatio { get; set; } = "";

    [ObservableProperty] public partial bool DuckStationOutputMuted { get; set; }

    [ObservableProperty] public partial int DuckStationOutputVolume { get; set; }

    [ObservableProperty] public partial bool DuckStationPauseOnFocusLoss { get; set; }

    [ObservableProperty] public partial bool DuckStationPgxpEnable { get; set; }

    [ObservableProperty] public partial string DuckStationRenderer { get; set; } = "";

    [ObservableProperty] public partial int DuckStationResolutionScale { get; set; }

    [ObservableProperty] public partial bool DuckStationRewindEnable { get; set; }

    [ObservableProperty] public partial int DuckStationRunaheadFrameCount { get; set; }

    [ObservableProperty] public partial bool DuckStationSaveStateOnExit { get; set; }

    [ObservableProperty] public partial bool DuckStationShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial bool DuckStationStartFullscreen { get; set; }

    [ObservableProperty] public partial string DuckStationTextureFilter { get; set; } = "";

    [ObservableProperty] public partial bool DuckStationVsync { get; set; }

    [ObservableProperty] public partial bool DuckStationWidescreenHack { get; set; }

    private string _emulatorPath = "";

    /// <summary>Initializes a new instance of the <see cref="InjectDuckStationConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectDuckStationConfigViewModel(SettingsManagerService settings, IMessageBoxLibraryService messageBox,
        ILogger logger)
    {
        _settings = settings;
        _logger = logger;
        _messageBox = messageBox;
    }

    /// <summary>
    ///     Available renderer options for DuckStation.
    /// </summary>
    public IList<string> RendererOptions { get; } =
        ["Automatic", "Vulkan", "Direct3D 11", "Direct3D 12", "OpenGL", "Software"];

    /// <summary>
    ///     Available resolution scale options for DuckStation.
    /// </summary>
    public IList<TagOption> ResolutionScaleOptions { get; } =
    [
        new("1", "1x (Native)"),
        new("2", "2x (720p)"),
        new("3", "3x (1080p)"),
        new("4", "4x (1440p)"),
        new("5", "5x (1620p)"),
        new("6", "6x (4K)"),
        new("7", "7x (5K)"),
        new("8", "8x (8K)")
    ];

    /// <summary>
    ///     Available texture filter options for DuckStation.
    /// </summary>
    public IList<string> TextureFilterOptions { get; } = ["Nearest", "Bilinear", "Bilinear(Bilinear)"];

    /// <summary>
    ///     Available aspect ratio options for DuckStation.
    /// </summary>
    public IList<string> AspectRatioOptions { get; } = ["Auto", "4:3", "16:9", "16:10", "Crop"];

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
    public Func<string?>? RequestEmulatorPath { get; set; }

    /// <summary>
    ///     Gets the owner window for dialog display.
    /// </summary>
    public Func<Window>? GetOwnerWindow { get; set; }

    /// <summary>
    ///     Initializes the ViewModel with the emulator path and launcher mode.
    /// </summary>
    /// <param name="emulatorPath">The file path to the DuckStation emulator executable.</param>
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
        DuckStationStartFullscreen = _settings.DuckStation.StartFullscreen;
        DuckStationPauseOnFocusLoss = _settings.DuckStation.PauseOnFocusLoss;
        DuckStationSaveStateOnExit = _settings.DuckStation.SaveStateOnExit;
        DuckStationRewindEnable = _settings.DuckStation.RewindEnable;
        DuckStationRunaheadFrameCount = _settings.DuckStation.RunaheadFrameCount;
        DuckStationRenderer = _settings.DuckStation.Renderer;
        DuckStationResolutionScale = _settings.DuckStation.ResolutionScale;
        DuckStationTextureFilter = _settings.DuckStation.TextureFilter;
        DuckStationAspectRatio = _settings.DuckStation.AspectRatio;
        DuckStationWidescreenHack = _settings.DuckStation.WidescreenHack;
        DuckStationPgxpEnable = _settings.DuckStation.PgxpEnable;
        DuckStationVsync = _settings.DuckStation.Vsync;
        DuckStationOutputMuted = _settings.DuckStation.OutputMuted;
        DuckStationOutputVolume = _settings.DuckStation.OutputVolume;
        DuckStationShowSettingsBeforeLaunch = _settings.DuckStation.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.DuckStation.StartFullscreen = DuckStationStartFullscreen;
        _settings.DuckStation.PauseOnFocusLoss = DuckStationPauseOnFocusLoss;
        _settings.DuckStation.SaveStateOnExit = DuckStationSaveStateOnExit;
        _settings.DuckStation.RewindEnable = DuckStationRewindEnable;
        _settings.DuckStation.RunaheadFrameCount = DuckStationRunaheadFrameCount;
        _settings.DuckStation.Renderer = DuckStationRenderer;
        _settings.DuckStation.ResolutionScale = DuckStationResolutionScale;
        _settings.DuckStation.TextureFilter = DuckStationTextureFilter;
        _settings.DuckStation.AspectRatio = DuckStationAspectRatio;
        _settings.DuckStation.WidescreenHack = DuckStationWidescreenHack;
        _settings.DuckStation.PgxpEnable = DuckStationPgxpEnable;
        _settings.DuckStation.Vsync = DuckStationVsync;
        _settings.DuckStation.OutputMuted = DuckStationOutputMuted;
        _settings.DuckStation.OutputVolume = DuckStationOutputVolume;
        _settings.DuckStation.ShowSettingsBeforeLaunch = DuckStationShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = EmulatorPathResolver.TryFindEmulatorPath("DuckStation", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.DuckStationEmulatorNotFoundMessageBoxAsync();

        var result = RequestEmulatorPath?.Invoke();
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
            DuckStationConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (DuckStationPermissionException)
        {
            await _messageBox.FailedToInjectDuckStationConfigurationMessageBoxAsync();
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"DuckStation configuration injection failed for path: {path}");
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
        catch (DuckStationPermissionException)
        {
            // Run: the user asked to configure and launch; allow the launch with defaults.
            ShouldRun = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var emulatorName =
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectDuckStationConfigWindow));
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
                await _messageBox.DuckStationConfigurationSavedSuccessfullyMessageBoxAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await _messageBox.InjectionFailedGenericMessageBoxAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (DuckStationPermissionException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var emulatorName =
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectDuckStationConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}