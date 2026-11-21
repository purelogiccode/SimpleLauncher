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
///     ViewModel for the Stella emulator configuration injection window.
/// </summary>
internal partial class InjectStellaConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial bool AudioEnabled { get; set; }

    [ObservableProperty] public partial int AudioVolume { get; set; }

    [ObservableProperty] public partial bool ConfirmExit { get; set; }

    [ObservableProperty] public partial bool CorrectAspect { get; set; }

    private string _emulatorPath = "";
    [ObservableProperty] public partial bool Fullscreen { get; set; }

    [ObservableProperty] public partial int Scanlines { get; set; }

    [ObservableProperty] public partial bool ShowBeforeLaunch { get; set; }

    [ObservableProperty] public partial bool TimeMachine { get; set; }

    [ObservableProperty] public partial string TvFilter { get; set; } = "";

    [ObservableProperty] public partial string VideoDriver { get; set; } = "";

    [ObservableProperty] public partial bool Vsync { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectStellaConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectStellaConfigViewModel(
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
    ///     Available video driver options for Stella.
    /// </summary>
    public IList<string> VideoDriverOptions { get; } = ["direct3d", "opengl", "software"];

    /// <summary>
    ///     Available TV filter options for Stella.
    /// </summary>
    public IList<string> TvFilterOptions { get; } = ["0", "1", "2", "3"];

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
    /// <param name="emulatorPath">The file path to the Stella emulator executable.</param>
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
        Fullscreen = _settings.Stella.Fullscreen;
        Vsync = _settings.Stella.Vsync;
        CorrectAspect = _settings.Stella.CorrectAspect;
        VideoDriver = _settings.Stella.VideoDriver;
        TvFilter = _settings.Stella.TvFilter.ToString(CultureInfo.InvariantCulture);
        Scanlines = _settings.Stella.Scanlines;
        AudioEnabled = _settings.Stella.AudioEnabled;
        AudioVolume = _settings.Stella.AudioVolume;
        TimeMachine = _settings.Stella.TimeMachine;
        ConfirmExit = _settings.Stella.ConfirmExit;
        ShowBeforeLaunch = _settings.Stella.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Stella.Fullscreen = Fullscreen;
        _settings.Stella.Vsync = Vsync;
        _settings.Stella.CorrectAspect = CorrectAspect;
        _settings.Stella.VideoDriver = VideoDriver;
        if (int.TryParse(TvFilter, CultureInfo.InvariantCulture, out var tvFilter))
            _settings.Stella.TvFilter = tvFilter;

        _settings.Stella.Scanlines = Scanlines;
        _settings.Stella.AudioEnabled = AudioEnabled;
        _settings.Stella.AudioVolume = AudioVolume;
        _settings.Stella.TimeMachine = TimeMachine;
        _settings.Stella.ConfirmExit = ConfirmExit;
        _settings.Stella.ShowSettingsBeforeLaunch = ShowBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("Stella", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.StellaEmulatorNotFoundMessageBoxAsync();

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
            StellaConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"Stella configuration injection failed for path: {path}");
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectStellaConfigWindow));
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
                await _messageBox.StellaConfigurationSavedSuccessfullyMessageBoxAsync();
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
            var emulatorName = InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectStellaConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}