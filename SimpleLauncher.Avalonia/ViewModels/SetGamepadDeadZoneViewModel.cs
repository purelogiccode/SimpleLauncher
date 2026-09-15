using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the gamepad dead zone configuration window.
/// </summary>
public partial class SetGamepadDeadZoneViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settingsManager;

    private double _deadZoneX;
    private double _deadZoneY;

    /// <summary>Initializes a new instance of the <see cref="SetGamepadDeadZoneViewModel" /> class.</summary>
    /// <param name="settingsManager">The settings manager for reading and saving dead zone values.</param>
    /// <param name="messageBox">The message box service for displaying dialogs.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    /// <param name="logErrors">The logger for recording errors.</param>
    public SetGamepadDeadZoneViewModel(SettingsManagerService settingsManager, IMessageBoxLibraryService messageBox,
        IResourceProvider resourceProvider, ILogger logErrors)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _messageBox = messageBox;
        _ = resourceProvider;
        _logger = logErrors;

        _deadZoneX = _settingsManager.DeadZoneX;
        _deadZoneY = _settingsManager.DeadZoneY;
    }

    /// <summary>Gets or sets the X-axis dead zone value.</summary>
    public double DeadZoneX
    {
        get => _deadZoneX;
        set
        {
            if (SetProperty(ref _deadZoneX, value)) OnPropertyChanged(nameof(DeadZoneXText));
        }
    }

    /// <summary>Gets or sets the Y-axis dead zone value.</summary>
    public double DeadZoneY
    {
        get => _deadZoneY;
        set
        {
            if (SetProperty(ref _deadZoneY, value)) OnPropertyChanged(nameof(DeadZoneYText));
        }
    }

    /// <summary>Gets the X-axis dead zone formatted for display.</summary>
    public string DeadZoneXText => _deadZoneX.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Gets the Y-axis dead zone formatted for display.</summary>
    public string DeadZoneYText => _deadZoneY.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Event raised when settings have been saved.</summary>
    public event EventHandler SaveCompleted = null!;

    /// <summary>Event raised when the window should be closed.</summary>
    public event EventHandler CloseRequested = null!;

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _settingsManager.DeadZoneX = (float)DeadZoneX;
            _settingsManager.DeadZoneY = (float)DeadZoneY;
            await _settingsManager.SaveAsync();

            await _messageBox.DeadZonesSavedMessageBoxAsync();

            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving gamepad dead zone settings");
            await _messageBox.FailedToSaveSettingsMessageBoxAsync();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RevertAsync()
    {
        _settingsManager.DeadZoneX = SettingsManagerService.DefaultDeadZoneX;
        _settingsManager.DeadZoneY = SettingsManagerService.DefaultDeadZoneY;
        await _settingsManager.SaveAsync();

        DeadZoneX = SettingsManagerService.DefaultDeadZoneX;
        DeadZoneY = SettingsManagerService.DefaultDeadZoneY;

        await _messageBox.DeadZonesRevertedMessageBoxAsync();

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}