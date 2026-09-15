using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the Daphne emulator configuration injection window.
/// </summary>
public partial class InjectDaphneConfigViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial bool DaphneBilinear { get; set; }

    [ObservableProperty] public partial bool DaphneDisableCrosshairs { get; set; }

    [ObservableProperty] public partial bool DaphneEnableSound { get; set; }

    [ObservableProperty] public partial bool DaphneFullscreen { get; set; }

    [ObservableProperty] public partial int DaphneResX { get; set; }

    [ObservableProperty] public partial int DaphneResY { get; set; }

    [ObservableProperty] public partial bool DaphneShowSettingsBeforeLaunch { get; set; }

    [ObservableProperty] public partial bool DaphneUseOverlays { get; set; }

    /// <summary>Initializes a new instance of the <see cref="InjectDaphneConfigViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="logErrors">The logger instance.</param>
    public InjectDaphneConfigViewModel(SettingsManagerService settings, IMessageBoxLibraryService messageBox,
        ILogger logErrors)
    {
        _settings = settings;
        _messageBox = messageBox;
        _logger = logErrors;
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
    ///     Initializes the ViewModel with the launcher mode.
    /// </summary>
    /// <param name="isLauncherMode">Whether the configuration is being injected from launcher mode.</param>
    public void Initialize(bool isLauncherMode)
    {
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
        DaphneFullscreen = _settings.Daphne.Fullscreen;
        DaphneBilinear = _settings.Daphne.Bilinear;
        DaphneResX = _settings.Daphne.ResX;
        DaphneResY = _settings.Daphne.ResY;
        DaphneEnableSound = _settings.Daphne.EnableSound;
        DaphneDisableCrosshairs = _settings.Daphne.DisableCrosshairs;
        DaphneUseOverlays = _settings.Daphne.UseOverlays;
        DaphneShowSettingsBeforeLaunch = _settings.Daphne.ShowSettingsBeforeLaunch;
    }

    private void SaveSettings()
    {
        _settings.Daphne.Fullscreen = DaphneFullscreen;
        _settings.Daphne.Bilinear = DaphneBilinear;
        _settings.Daphne.ResX = DaphneResX;
        _settings.Daphne.ResY = DaphneResY;
        _settings.Daphne.EnableSound = DaphneEnableSound;
        _settings.Daphne.DisableCrosshairs = DaphneDisableCrosshairs;
        _settings.Daphne.UseOverlays = DaphneUseOverlays;
        _settings.Daphne.ShowSettingsBeforeLaunch = DaphneShowSettingsBeforeLaunch;
        _ = _settings.SaveAsync();
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        try
        {
            SaveSettings();
            ShouldRun = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShouldRun = true;
            _logger.Error(ex, "Error saving Daphne configuration");
            await _messageBox.ErrorMessageBoxAsync();
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            SaveSettings();
            await _messageBox.DaphnesettingssavedsuccessfullyMessageBoxAsync();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving Daphne configuration");
            await _messageBox.DaphneConfigurationSaveFailedMessageBoxAsync();
        }
    }
}