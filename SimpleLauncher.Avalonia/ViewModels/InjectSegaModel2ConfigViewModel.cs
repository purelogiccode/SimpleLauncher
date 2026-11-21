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
///     ViewModel for the Sega Model 2 emulator configuration injection window.
/// </summary>
internal partial class InjectSegaModel2ConfigViewModel : ObservableObject
{
    private readonly EmulatorPathResolver _emulatorPathResolver;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial bool Bilinear { get; set; }

    [ObservableProperty] public partial bool DrawCross { get; set; }

    private string _emulatorPath = null!;
    [ObservableProperty] public partial bool EnableFf { get; set; }

    [ObservableProperty] public partial bool FilterTilemaps { get; set; }

    [ObservableProperty] public partial string Fsaa { get; set; } = null!;

    [ObservableProperty] public partial bool HoldGears { get; set; }

    [ObservableProperty] public partial int ResX { get; set; }

    [ObservableProperty] public partial int ResY { get; set; }

    [ObservableProperty] public partial bool ShowBeforeLaunch { get; set; }

    [ObservableProperty] public partial bool Trilinear { get; set; }

    [ObservableProperty] public partial bool UseRawInput { get; set; }

    [ObservableProperty] public partial string WideScreen { get; set; } = null!;

    [ObservableProperty] public partial bool XInput { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectSegaModel2ConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="emulatorPathResolver">The emulator path resolver service.</param>
    /// <param name="logger">The logger instance.</param>
    public InjectSegaModel2ConfigViewModel(
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
    ///     Available widescreen mode options for Sega Model 2.
    /// </summary>
    public IList<string> WideScreenOptions { get; } = ["0", "1", "2"];

    /// <summary>
    ///     Available full-screen anti-aliasing options for Sega Model 2.
    /// </summary>
    public IList<string> FsaaOptions { get; } = ["0", "2", "4", "8"];

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
    /// <param name="emulatorPath">The file path to the Sega Model 2 emulator executable.</param>
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
        ResX = _settings.SegaModel2.ResX;
        ResY = _settings.SegaModel2.ResY;
        WideScreen = _settings.SegaModel2.WideScreen.ToString(CultureInfo.InvariantCulture);
        Fsaa = _settings.SegaModel2.Fsaa.ToString(CultureInfo.InvariantCulture);
        Bilinear = _settings.SegaModel2.Bilinear;
        Trilinear = _settings.SegaModel2.Trilinear;
        FilterTilemaps = _settings.SegaModel2.FilterTilemaps;
        DrawCross = _settings.SegaModel2.DrawCross;
        XInput = _settings.SegaModel2.XInput;
        EnableFf = _settings.SegaModel2.EnableFf;
        HoldGears = _settings.SegaModel2.HoldGears;
        UseRawInput = _settings.SegaModel2.UseRawInput;
        ShowBeforeLaunch = _settings.SegaModel2.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.SegaModel2.ResX = ResX;
        _settings.SegaModel2.ResY = ResY;
        if (int.TryParse(WideScreen, CultureInfo.InvariantCulture, out var wideScreen))
            _settings.SegaModel2.WideScreen = wideScreen;

        if (int.TryParse(Fsaa, CultureInfo.InvariantCulture, out var fsaa)) _settings.SegaModel2.Fsaa = fsaa;

        _settings.SegaModel2.Bilinear = Bilinear;
        _settings.SegaModel2.Trilinear = Trilinear;
        _settings.SegaModel2.FilterTilemaps = FilterTilemaps;
        _settings.SegaModel2.DrawCross = DrawCross;
        _settings.SegaModel2.XInput = XInput;
        _settings.SegaModel2.EnableFf = EnableFf;
        _settings.SegaModel2.HoldGears = HoldGears;
        _settings.SegaModel2.UseRawInput = UseRawInput;
        _settings.SegaModel2.ShowSettingsBeforeLaunch = ShowBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    private async Task<string?> EnsureEmulatorPathAsync()
    {
        if (!string.IsNullOrEmpty(_emulatorPath) && File.Exists(_emulatorPath)) return _emulatorPath;

        var resolved = _emulatorPathResolver.TryFindEmulatorPath("SEGA Model 2", _logger);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            _emulatorPath = resolved;
            return _emulatorPath;
        }

        await _messageBox.SegaModel2EmulatorNotFoundMessageBoxAsync();

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
            SegaModel2ConfigurationService.InjectSettings(path, _settings, _logger);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, $"SEGA Model 2 configuration injection failed for path: {path}");
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
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectSegaModel2ConfigWindow));
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
                await _messageBox.SegaModel2ConfigurationSavedSuccessfullyMessageBoxAsync();
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
                InjectionErrorHandler.GetEmulatorName(_emulatorPath, typeof(InjectSegaModel2ConfigWindow));
            var window = GetOwnerWindow?.Invoke();
            InjectionErrorHandler.HandleSaveButtonFailure(_logger, ex, emulatorName, _emulatorPath, window,
                _messageBox);
        }
    }
}