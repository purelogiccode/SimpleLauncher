using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.RetroAchievements;
using SimpleLauncher.Avalonia.Views;
using SimpleLauncher.Core.Services.GamePad;
using System.Globalization;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Preferences window with OpenEmu-style nav-strip layout.
///     RetroAchievements test connection wired to real API.
/// </summary>
public partial class PreferencesWindow : Window
{
    private readonly GamePadController _gamePadController;
    private readonly LocalizationService _localization;
    private readonly Dictionary<string, Panel> _panels = new(StringComparer.OrdinalIgnoreCase);
    private readonly RetroAchievementsService? _raService;
    private readonly SettingsManagerService _settings;
    private readonly AvaloniaCheckForUpdatesService _updateService;

    public PreferencesWindow(SettingsManagerService settings, LocalizationService localization,
        AvaloniaCheckForUpdatesService updateService, GamePadController gamePadController)
    {
        InitializeComponent();
        _settings = settings;
        _localization = localization;
        _updateService = updateService;
        _gamePadController = gamePadController;

        try
        {
            _raService = App.ServiceProvider.GetService<RetroAchievementsService>();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "RetroAchievementsService unavailable in PreferencesWindow");
            _raService = null;
        }

        _panels["general"] = GeneralPanel;
        _panels["systems"] = SystemsPanel;
        _panels["emulators"] = EmulatorsPanel;
        _panels["images"] = ImagesPanel;
        _panels["view"] = ViewPanel;
        _panels["sound"] = SoundPanel;
        _panels["retroachievements"] = RetroAchievementsPanel;
        _panels["updates"] = UpdatesPanel;

        PopulateLanguageCombo();
        LoadSettings();

        // Persist all preferences when the window closes (language/RA changes are
        // also saved on the fly; this covers the remaining toggles and text fields).
        Closed += (_, _) => SavePreferences();
    }

    /// <summary>
    ///     Writes every editable preference back to SettingsManagerService and persists
    ///     settings.xml — without this the preference controls were inert.
    /// </summary>
    private void SavePreferences()
    {
        try
        {
            if (DefaultViewCombo.SelectedItem is ComboBoxItem { Tag: string viewMode }) _settings.ViewMode = viewMode;

            if (int.TryParse(s: CardWidthBox.Text, provider: CultureInfo.InvariantCulture, result: out var cardWidth) &&
                cardWidth is >= 148 and <= 280)
            {
                _settings.ThumbnailSize = cardWidth;
            }

            _settings.EnableGamePadNavigation = GamepadNavCheck.IsChecked == true;
            _settings.DisplayMachineName = DisplayMachineNameCheck.IsChecked == true;
            _settings.EnableNotificationSound = NotificationSoundCheck.IsChecked == true;
            _settings.RaUsername = RaUsernameBox.Text?.Trim() ?? "";
            _settings.RaApiKey = RaApiKeyBox.Text?.Trim() ?? "";

            _ = _settings.SaveAsync();

            // Start or stop the gamepad controller to match the new preference
            if (_settings.EnableGamePadNavigation)
                _ = _gamePadController.StartAsync();
            else
                _ = _gamePadController.StopAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save preferences");
        }
    }

    // ── Systems page: EasyMode launcher ───────────────────────────────

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OpenEasyMode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var easyModeWindow = App.ServiceProvider.GetRequiredService<EasyModeWindow>();
            await easyModeWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open EasyMode window");
        }
    }

    private async void OpenEditSystem_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Factory allows a pre-selected system name to be passed (null = no pre-selection)
            var editSystemWindow = App.ServiceProvider.GetRequiredService<Func<string?, EditSystemWindow>>()(null);
            await editSystemWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open EditSystem window");
        }
    }

    private async void DownloadImagePack_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var imagePackWindow = App.ServiceProvider.GetRequiredService<DownloadImagePackWindow>();
            await imagePackWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open DownloadImagePack window");
        }
    }

    private async void CheckUpdates_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            CheckUpdatesButton.IsEnabled = false;
            await _updateService.ManualCheckForUpdatesAsync(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates from PreferencesWindow");
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void PopulateLanguageCombo()
    {
        LanguageCombo.Items.Clear();
        foreach (var (code, name) in LocalizationService.AvailableLanguages)
            LanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item) return;

        var tag = item.Tag as string ?? "general";

        foreach (var panel in _panels.Values) panel.IsVisible = false;

        if (_panels.TryGetValue(tag, out var selected)) selected.IsVisible = true;
    }

    private void LoadSettings()
    {
        // General
        foreach (var lbi in LanguageCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(lbi.Tag as string, _settings.Language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageCombo.SelectedItem = lbi;
                break;
            }
        }

        // View
        foreach (var lbi in DefaultViewCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(lbi.Tag as string, _settings.ViewMode, StringComparison.OrdinalIgnoreCase))
            {
                DefaultViewCombo.SelectedItem = lbi;
                break;
            }
        }

        CardWidthBox.Text = _settings.ThumbnailSize.ToString(CultureInfo.InvariantCulture);
        GamepadNavCheck.IsChecked = _settings.EnableGamePadNavigation;
        DisplayMachineNameCheck.IsChecked = _settings.DisplayMachineName;

        // Sound
        NotificationSoundCheck.IsChecked = _settings.EnableNotificationSound;

        // RA
        RaUsernameBox.Text = _settings.RaUsername ?? "";
        RaApiKeyBox.Text = _settings.RaApiKey ?? "";
    }

    private void LanguageCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem { Tag: string lang })
        {
            _settings.Language = lang;
            _localization.LoadLanguage(lang);

            // Persist the language choice — without this the selection is lost on restart
            _ = _settings.SaveAsync();
        }
    }

    private async void RaTestButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var username = RaUsernameBox.Text?.Trim();
            var apiKey = RaApiKeyBox.Text?.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiKey))
            {
                await MessageDialogWindow.ShowAsync(this,
                    _localization.GetString("PleaseenterbothusernameandAPIkey",
                        "Please enter both username and API key."),
                    _localization.GetString("RetroAchievements", "RetroAchievements"),
                    MessageButtons.Ok,
                    MessageIcon.Warning);
                return;
            }

            _settings.RaUsername = username;
            _settings.RaApiKey = apiKey;

            // Persist the RA credentials so they survive a restart
            _ = _settings.SaveAsync();

            RaTestButton.IsEnabled = false;
            RaTestButton.Content = _localization.GetString("Testing", "Testing...");

            try
            {
                if (_raService is not null)
                {
                    var profile = await _raService.GetUserProfileAsync(username, apiKey);
                    if (profile is not null)
                    {
                        var connectedTemplate = _localization.GetString("ConnectedasPointsRank",
                            "Connected as: {0}\nPoints: {1:N0}\nRank: {2}");
                        await MessageDialogWindow.ShowAsync(this,
                            string.Format(CultureInfo.InvariantCulture, connectedTemplate, profile.User,
                                profile.TotalPoints, profile.Rank),
                            _localization.GetString("RetroAchievementsConnected", "RetroAchievements — Connected"),
                            MessageButtons.Ok,
                            MessageIcon.Information);
                        return;
                    }
                }

                await MessageDialogWindow.ShowAsync(this,
                    _localization.GetString("CouldnotconnectCheckyourusernameandAPIkey",
                        "Could not connect. Check your username and API key."),
                    _localization.GetString("RetroAchievementsFailed", "RetroAchievements — Failed"),
                    MessageButtons.Ok,
                    MessageIcon.Warning);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RetroAchievements test connection failed for user {User}", username);
                await MessageDialogWindow.ShowAsync(this,
                    $"{_localization.GetString("Connectionerror", "Connection error: ")}{ex.Message}",
                    _localization.GetString("RetroAchievementsError", "RetroAchievements — Error"),
                    MessageButtons.Ok,
                    MessageIcon.Error);
            }
            finally
            {
                RaTestButton.IsEnabled = true;
                RaTestButton.Content = _localization.GetString("PreferencesWindow_Test_Connection", "Test Connection");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RetroAchievements test connection failed");
        }
    }
}