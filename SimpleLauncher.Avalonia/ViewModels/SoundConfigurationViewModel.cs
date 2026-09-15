using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the sound configuration window.
/// </summary>
public partial class SoundConfigurationViewModel : ObservableObject
{
    private const string DefaultNotificationSound = "click.mp3";
    private static readonly string AudioFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio");
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly SettingsManagerService _settings;

    [ObservableProperty] public partial bool EnableNotificationSound { get; set; }

    [ObservableProperty] public partial bool IsSoundControlsEnabled { get; set; }

    [ObservableProperty] public partial string NotificationSoundFile { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SoundConfigurationViewModel" /> class.</summary>
    /// <param name="settings">The settings manager for reading and saving sound configuration.</param>
    /// <param name="playSoundEffects">The sound effects service for playing notification sounds.</param>
    /// <param name="logErrors">The logger for recording errors.</param>
    /// <param name="messageBox">The message box service for displaying dialogs.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public SoundConfigurationViewModel(SettingsManagerService settings, PlaySoundEffects playSoundEffects,
        ILogger logErrors, IMessageBoxLibraryService messageBox, IResourceProvider resourceProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _playSoundEffects = playSoundEffects ?? throw new ArgumentNullException(nameof(playSoundEffects));
        _logger = logErrors ?? throw new ArgumentNullException(nameof(logErrors));
        _messageBox = messageBox;
        _ = resourceProvider;
        EnableNotificationSound = _settings.EnableNotificationSound;
        NotificationSoundFile = _settings.CustomNotificationSoundFile;
        IsSoundControlsEnabled = EnableNotificationSound;
    }

    /// <summary>Event raised to request a sound file path from the view.</summary>
    public Func<Task<string?>>? RequestSoundFilePath { get; set; }

    /// <summary>Event raised when settings have been saved.</summary>
    public event EventHandler SaveCompleted = null!;

    /// <summary>Event raised when the window should be closed.</summary>
    public event EventHandler CloseRequested = null!;

    partial void OnEnableNotificationSoundChanged(bool value)
    {
        IsSoundControlsEnabled = value;
    }

    [RelayCommand]
    private async Task ChooseSoundFileAsync()
    {
        var sourceFilePath = RequestSoundFilePath is { } request ? await request() : null;
        if (string.IsNullOrEmpty(sourceFilePath)) return;

        try
        {
            var chosenFileName = Path.GetFileName(sourceFilePath);

            Directory.CreateDirectory(AudioFolderPath);

            var destinationFilePath = Path.Combine(AudioFolderPath, chosenFileName);

            if (!string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destinationFilePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceFilePath, destinationFilePath, true);
            }

            NotificationSoundFile = chosenFileName;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error choosing or copying sound file");
            await _messageBox.ErrorSettingSoundFileMessageBoxAsync();
        }
    }

    [RelayCommand]
    private Task PlayCurrentSoundAsync()
    {
        switch (EnableNotificationSound)
        {
            case true when !string.IsNullOrWhiteSpace(NotificationSoundFile):
                _playSoundEffects.PlayConfiguredSound(NotificationSoundFile);
                break;
            case false:
                return _messageBox.NotificationSoundIsDisableMessageBoxAsync();
            default:
                return _messageBox.NoSoundFileIsSelectedMessageBoxAsync();
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        EnableNotificationSound = true;
        NotificationSoundFile = DefaultNotificationSound;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _settings.EnableNotificationSound = EnableNotificationSound;
        _settings.CustomNotificationSoundFile = NotificationSoundFile;
        await _settings.SaveAsync();

        await _messageBox.SettingsSavedSuccessfullyMessageBoxAsync();

        SaveCompleted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}