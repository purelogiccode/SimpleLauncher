using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;
using MameConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.MameConfigurationService;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the MAME emulator configuration injection window.
/// </summary>
internal partial class InjectMameConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = null!;
    private string[] _listOfSecondarySystemFolders = null!;
    [ObservableProperty] public partial bool MameAutoframeskip { get; set; }

    [ObservableProperty] public partial bool MameAutosave { get; set; }

    [ObservableProperty] public partial string MameBgfxBackend { get; set; } = null!;

    [ObservableProperty] public partial string MameBgfxScreenChains { get; set; } = null!;

    [ObservableProperty] public partial bool MameCheat { get; set; }

    [ObservableProperty] public partial bool MameConfirmQuit { get; set; }

    [ObservableProperty] public partial bool MameFilter { get; set; }

    [ObservableProperty] public partial bool MameJoystick { get; set; }

    [ObservableProperty] public partial bool MameKeepAspect { get; set; }

    [ObservableProperty] public partial bool MameMaximize { get; set; }

    [ObservableProperty] public partial bool MameNvramSave { get; set; }

    [ObservableProperty] public partial bool MameRewind { get; set; }

    [ObservableProperty] public partial bool MameShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial bool MameSkipGameInfo { get; set; }

    [ObservableProperty] public partial string MameVideo { get; set; } = null!;

    [ObservableProperty] public partial bool MameWindow { get; set; }

    private string _systemRomPath = null!;

    /// <summary>Initializes a new instance of the <see cref="InjectMameConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectMameConfigViewModel(SettingsManagerService settings, IMessageBoxLibraryService messageBox,
        EmulatorPathResolver emulatorPathResolver, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
        _messageBox = messageBox;
        _emulatorPathResolver = emulatorPathResolver;
    }

    /// <summary>
    ///     Available video output options for MAME.
    /// </summary>
    public IList<string> VideoOptions { get; } = ["auto", "d3d", "opengl", "bgfx", "gdi"];

    /// <summary>
    ///     Available BGFX backend options for MAME.
    /// </summary>
    public IList<string> BgfxBackendOptions { get; } = ["auto", "d3d11", "vulkan", "opengl"];

    /// <summary>
    ///     Available BGFX screen chain options for MAME.
    /// </summary>
    public IList<string> BgfxChainsOptions { get; } = ["default", "crt-geom"];

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
    ///     Initializes the ViewModel with the emulator path, launcher mode, and optional ROM paths.
    /// </summary>
    /// <param name="emulatorPath">The file path to the MAME emulator executable.</param>
    /// <param name="isLauncherMode">Whether the configuration is being injected from launcher mode.</param>
    /// <param name="systemRomPath">Optional path to the system ROM directory.</param>
    /// <param name="listOfSecondaryRomPaths">Optional list of secondary ROM folder paths.</param>
    public void Initialize(string? emulatorPath, bool isLauncherMode, string? systemRomPath = null,
        string[]? listOfSecondaryRomPaths = null)
    {
        _emulatorPath = emulatorPath!;
        IsLauncherMode = isLauncherMode;
        _systemRomPath = systemRomPath!;
        _listOfSecondarySystemFolders = listOfSecondaryRomPaths!;
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
        MameVideo = _settings.Mame.Video;
        MameBgfxBackend = _settings.Mame.BgfxBackend;
        MameBgfxScreenChains = _settings.Mame.BgfxScreenChains;
        MameFilter = _settings.Mame.Filter;
        MameAutoframeskip = _settings.Mame.Autoframeskip;
        MameCheat = _settings.Mame.Cheat;
        MameRewind = _settings.Mame.Rewind;
        MameNvramSave = _settings.Mame.NvramSave;
        MameWindow = _settings.Mame.Window;
        MameMaximize = _settings.Mame.Maximize;
        MameKeepAspect = _settings.Mame.KeepAspect;
        MameSkipGameInfo = _settings.Mame.SkipGameInfo;
        MameAutosave = _settings.Mame.Autosave;
        MameConfirmQuit = _settings.Mame.ConfirmQuit;
        MameJoystick = _settings.Mame.Joystick;
        MameShowSettingsBeforeLaunch = _settings.Mame.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Mame.Video = MameVideo;
        _settings.Mame.BgfxBackend = MameBgfxBackend;
        _settings.Mame.BgfxScreenChains = MameBgfxScreenChains;
        _settings.Mame.Filter = MameFilter;
        _settings.Mame.Autoframeskip = MameAutoframeskip;
        _settings.Mame.Cheat = MameCheat;
        _settings.Mame.Rewind = MameRewind;
        _settings.Mame.NvramSave = MameNvramSave;
        _settings.Mame.Window = MameWindow;
        _settings.Mame.Maximize = MameMaximize;
        _settings.Mame.KeepAspect = MameKeepAspect;
        _settings.Mame.SkipGameInfo = MameSkipGameInfo;
        _settings.Mame.Autosave = MameAutosave;
        _settings.Mame.ConfirmQuit = MameConfirmQuit;
        _settings.Mame.Joystick = MameJoystick;
        _settings.Mame.ShowSettingsBeforeLaunch = MameShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("MAME", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.MameEmulatorPathNotFoundMessageBoxAsync();

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
            MameConfigurationService.InjectSettings(path, _settings, _logger, _systemRomPath,
                _listOfSecondarySystemFolders);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"MAME configuration injection failed for path: {path}");
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectMameConfigWindow));
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
                await _messageBox.MameConfigurationInjectedSuccessfullyMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectMameConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}