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
///     ViewModel for the Azahar emulator configuration injection window.
/// </summary>
internal partial class InjectAzaharConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial bool AsyncShader { get; set; }

    [ObservableProperty] public partial bool AudioStretching { get; set; }

    private string _emulatorPath = "";
    [ObservableProperty] public partial bool Fullscreen { get; set; }

    [ObservableProperty] public partial string GraphicsApi { get; set; } = "";

    [ObservableProperty] public partial bool IsNew3Ds { get; set; }

    [ObservableProperty] public partial string Layout { get; set; } = "";

    [ObservableProperty] public partial string Resolution { get; set; } = "";

    [ObservableProperty] public partial bool ShowBeforeLaunch { get; set; }

    [ObservableProperty] public partial int Volume { get; set; }

    [ObservableProperty] public partial bool Vsync { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectAzaharConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectAzaharConfigViewModel(
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
    /// <param name="emulatorPath">The file path to the Azahar emulator executable.</param>
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
        GraphicsApi = _settings.Azahar.GraphicsApi.ToString(CultureInfo.InvariantCulture);
        Resolution = _settings.Azahar.ResolutionFactor.ToString(CultureInfo.InvariantCulture);
        Layout = _settings.Azahar.LayoutOption.ToString(CultureInfo.InvariantCulture);
        Fullscreen = _settings.Azahar.Fullscreen;
        Vsync = _settings.Azahar.UseVsync;
        AsyncShader = _settings.Azahar.AsyncShaderCompilation;
        IsNew3Ds = _settings.Azahar.IsNew3Ds;
        Volume = _settings.Azahar.Volume;
        ShowBeforeLaunch = _settings.Azahar.ShowSettingsBeforeLaunch;
        AudioStretching = _settings.Azahar.EnableAudioStretching;
    }

    private void SaveSettings()
    {
        if (int.TryParse(GraphicsApi, CultureInfo.InvariantCulture, out var graphicsApi))
            _settings.Azahar.GraphicsApi = graphicsApi;

        if (int.TryParse(Resolution, CultureInfo.InvariantCulture, out var resolution))
            _settings.Azahar.ResolutionFactor = resolution;

        if (int.TryParse(Layout, CultureInfo.InvariantCulture, out var layout)) _settings.Azahar.LayoutOption = layout;

        _settings.Azahar.Fullscreen = Fullscreen;
        _settings.Azahar.UseVsync = Vsync;
        _settings.Azahar.AsyncShaderCompilation = AsyncShader;
        _settings.Azahar.IsNew3Ds = IsNew3Ds;
        _settings.Azahar.Volume = Volume;
        _settings.Azahar.ShowSettingsBeforeLaunch = ShowBeforeLaunch;
        _settings.Azahar.EnableAudioStretching = AudioStretching;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Azahar", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

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
            AzaharConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (AzaharPermissionException)
        {
            await _messageBox.AzaharConfigurationInjectionPermissionErrorMessageBoxAsync();
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, "Azahar injection failed");
            await _messageBox.FailedToSaveAzaharConfigurationMessageBoxAsync();
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
        catch (AzaharPermissionException)
        {
            ShouldRun = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectAzaharConfigWindow));
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
                await _messageBox.AzaharConfigurationSavedSuccessfullyMessageBoxAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await _messageBox.InjectionFailedGenericMessageBoxAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (AzaharPermissionException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectAzaharConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}