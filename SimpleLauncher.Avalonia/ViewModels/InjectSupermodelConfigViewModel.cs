using System.Globalization;
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
///     ViewModel for the Supermodel emulator configuration injection window.
/// </summary>
internal partial class InjectSupermodelConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    private string _emulatorPath = null!;
    [ObservableProperty] public partial bool Fullscreen { get; set; }

    [ObservableProperty] public partial string InputSystem { get; set; } = null!;

    [ObservableProperty] public partial bool MultiThreaded { get; set; }

    [ObservableProperty] public partial int MusicVolume { get; set; }

    [ObservableProperty] public partial bool New3DEngine { get; set; }

    [ObservableProperty] public partial string PowerPcFrequency { get; set; } = null!;

    [ObservableProperty] public partial bool QuadRendering { get; set; }

    [ObservableProperty] public partial int ResX { get; set; }

    [ObservableProperty] public partial int ResY { get; set; }

    [ObservableProperty] public partial bool ShowBeforeLaunch { get; set; }

    [ObservableProperty] public partial int SoundVolume { get; set; }

    [ObservableProperty] public partial bool Stretch { get; set; }

    [ObservableProperty] public partial bool Throttle { get; set; }

    [ObservableProperty] public partial bool Vsync { get; set; }

    [ObservableProperty] public partial bool WideScreen { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectSupermodelConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectSupermodelConfigViewModel(
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
    ///     Available input system options for Supermodel.
    /// </summary>
    public IList<string> InputSystemOptions { get; } = ["xinput", "dinput", "rawinput"];

    /// <summary>
    ///     Available PowerPC frequency options for Supermodel.
    /// </summary>
    public IList<string> PpcFrequencyOptions { get; } = ["50", "60", "75", "100"];

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
    /// <param name="emulatorPath">The file path to the Supermodel emulator executable.</param>
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
        New3DEngine = _settings.Supermodel.New3DEngine;
        QuadRendering = _settings.Supermodel.QuadRendering;
        Fullscreen = _settings.Supermodel.Fullscreen;
        Vsync = _settings.Supermodel.Vsync;
        WideScreen = _settings.Supermodel.WideScreen;
        Stretch = _settings.Supermodel.Stretch;
        ResX = _settings.Supermodel.ResX;
        ResY = _settings.Supermodel.ResY;
        MusicVolume = _settings.Supermodel.MusicVolume;
        SoundVolume = _settings.Supermodel.SoundVolume;
        Throttle = _settings.Supermodel.Throttle;
        MultiThreaded = _settings.Supermodel.MultiThreaded;
        InputSystem = _settings.Supermodel.InputSystem;
        PowerPcFrequency = _settings.Supermodel.PowerPcFrequency.ToString(CultureInfo.InvariantCulture);
        ShowBeforeLaunch = _settings.Supermodel.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Supermodel.New3DEngine = New3DEngine;
        _settings.Supermodel.QuadRendering = QuadRendering;
        _settings.Supermodel.Fullscreen = Fullscreen;
        _settings.Supermodel.Vsync = Vsync;
        _settings.Supermodel.WideScreen = WideScreen;
        _settings.Supermodel.Stretch = Stretch;
        _settings.Supermodel.ResX = ResX;
        _settings.Supermodel.ResY = ResY;
        _settings.Supermodel.MusicVolume = MusicVolume;
        _settings.Supermodel.SoundVolume = SoundVolume;
        _settings.Supermodel.Throttle = Throttle;
        _settings.Supermodel.MultiThreaded = MultiThreaded;
        _settings.Supermodel.InputSystem = InputSystem;
        if (int.TryParse(PowerPcFrequency, CultureInfo.InvariantCulture, out var powerPcFrequency))
            _settings.Supermodel.PowerPcFrequency = powerPcFrequency;

        _settings.Supermodel.ShowSettingsBeforeLaunch = ShowBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Supermodel", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.SupermodelEmulatorNotFoundMessageBoxAsync();

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
            SupermodelConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"Supermodel configuration injection failed for path: {path}");
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
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectSupermodelConfigWindow));
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
                await _messageBox.SupermodelConfigurationSavedSuccessfullyMessageBoxAsync();
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
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectSupermodelConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}