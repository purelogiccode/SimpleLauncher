using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Services.RetroAchievements;
using Application = System.Windows.Application;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the RetroAchievements settings window.
/// </summary>
public partial class RetroAchievementsSettingsViewModel : ObservableObject
{
    private readonly IRetroAchievementsEmulatorConfiguratorService _configurator;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly RetroAchievementsService _raService;
    private readonly IResourceProvider _resourceProvider;
    private readonly SettingsManagerService _settings;
    [ObservableProperty] public partial string ApiKey { get; set; }

    [ObservableProperty] public partial string Password { get; set; }

    [ObservableProperty] public partial string Username { get; set; }

    /// <summary>Initializes a new instance of the <see cref="RetroAchievementsSettingsViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="raService">The RetroAchievements service.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    /// <param name="configurator">The emulator configurator service for RetroAchievements integration.</param>
    public RetroAchievementsSettingsViewModel(SettingsManagerService settings, ILogger logErrors,
        IMessageBoxLibraryService messageBox, RetroAchievementsService raService, IResourceProvider resourceProvider,
        IRetroAchievementsEmulatorConfiguratorService configurator)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logErrors;
        _messageBox = messageBox;
        _raService = raService;
        _resourceProvider = resourceProvider;
        _configurator = configurator;
        Username = _settings.RaUsername;
        ApiKey = _settings.RaApiKey;
        Password = _settings.RaPassword;
    }

    /// <summary>Event raised to request the emulator executable path from the view.</summary>
    public Func<string?>? RequestExePath { get; set; }

    /// <summary>Delegate that opens a URL in the browser; wired by the view so unit tests never launch a browser.</summary>
    public Action<string>? OpenInBrowser { get; set; }

    /// <summary>Event raised when settings have been saved successfully.</summary>
    public event EventHandler SaveCompleted = null!;

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
                _resourceProvider.GetString("SavingRetroAchievementsSettings", "Saving RetroAchievements settings..."));

            _settings.RaUsername = Username.Trim();
            _settings.RaApiKey = ApiKey;
            _settings.RaPassword = Password;
            await _settings.SaveAsync();

            OpenInBrowser?.Invoke("https://retroachievements.org/controlpanel.php");

            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving RetroAchievements settings");
            await _messageBox.FailedToSaveSettingsMessageBoxAsync();
        }
    }

    [RelayCommand]
    private async Task ConfigureEmulatorAsync(string emulatorName)
    {
        try
        {
            var username = Username.Trim();
            var password = Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                await _messageBox.EnterUsernamePasswordMessageBoxAsync();
                return;
            }

            SaveCurrentSettings();

            var token = _settings.RaToken;
            if (!string.Equals(emulatorName, "RetroArch", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(token) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
                {
                    (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
                        _resourceProvider.GetString("RaStatusLoggingIn", "Logging in to RetroAchievements..."));

                    token = await _raService.GetSessionTokenAsync(username, password);

                    if (!string.IsNullOrEmpty(token))
                    {
                        _settings.RaToken = token;
                        await _settings.SaveAsync();
                    }
                    else
                    {
                        await _messageBox.FailedToLoginToRetroAchievementsMessageBoxAsync();
                        return;
                    }
                }
            }

            var exePath = RequestExePath?.Invoke();
            if (string.IsNullOrEmpty(exePath)) return;

            var success = false;
            try
            {
                switch (emulatorName)
                {
                    case "RetroArch": success = _configurator.ConfigureRetroArch(exePath, username, password); break;
                    case "PCSX2": success = _configurator.ConfigurePcsx2(exePath, username, token); break;
                    case "DuckStation": success = _configurator.ConfigureDuckStation(exePath, username, token); break;
                    case "PPSSPP": success = _configurator.ConfigurePpspp(exePath, username, token); break;
                    case "Dolphin": success = _configurator.ConfigureDolphin(exePath, username, token); break;
                    case "Flycast": success = _configurator.ConfigureFlycast(exePath, username, token); break;
                    case "BizHawk": success = _configurator.ConfigureBizHawk(exePath, username, token); break;
                }

                if (success)
                    await _messageBox.EmulatorConfiguredSuccessfullyMessageBoxAsync();
                else
                    await _messageBox.FailedToConfigureTheEmulatorMessageBoxAsync();
            }
            catch (Exception ex)
            {
                await _messageBox.AnErrorOccurredWhileConfiguringTheEmulatorMessageBoxAsync();
                _logger.Error(ex, $"Failed to configure {emulatorName}.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in ConfigureEmulator method");
        }
    }

    private void SaveCurrentSettings()
    {
        _settings.RaUsername = Username.Trim();
        _settings.RaApiKey = ApiKey;
        _settings.RaPassword = Password;
        _ = _settings.SaveAsync();
    }
}