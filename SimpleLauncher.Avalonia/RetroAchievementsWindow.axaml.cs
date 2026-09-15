using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.RetroAchievements;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using IResourceProvider = SimpleLauncher.Core.Interfaces.IResourceProvider;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for browsing RetroAchievements user profile, unlocks, and completion progress.
/// </summary>
public partial class RetroAchievementsWindow : Window
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly IResourceProvider _resourceProvider;
    private readonly RetroAchievementsViewModel _viewModel;

    // AV-21: generation counter invalidates superseded tab loads (rapid tab
    // switches overlap; only the latest load may touch the UI), and _isClosed
    // guards UI updates after the window closed mid-load.
    private int _loadGeneration;
    private bool _isClosed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsWindow" /> class.
    /// </summary>
    /// <param name="playSoundEffects">The sound effects service.</param>
    /// <param name="logger">The error logging service.</param>
    /// <param name="settings">The application settings manager.</param>
    /// <param name="raService">The RetroAchievements API service.</param>
    public RetroAchievementsWindow(PlaySoundEffects playSoundEffects, ILogger logger, SettingsManagerService settings,
        RetroAchievementsService raService)
    {
        InitializeComponent();

        _playSoundEffects = playSoundEffects;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();
        _resourceProvider = App.ServiceProvider.GetRequiredService<IResourceProvider>();

        // Localize the emergency return button (WPF DynamicResource ReturnButton parity)
        var localization = App.ServiceProvider.GetRequiredService<LocalizationService>();
        EmergencyReturnButton.Content = localization.GetString("ReturnButton");
        ToolTip.SetTip(EmergencyReturnButton,
            localization.GetString("ClickHereIfTheLoadingScreenIsStuckToReturnToTheMainMenu"));

        _viewModel = new RetroAchievementsViewModel(
            _messageBox,
            App.ServiceProvider.GetRequiredService<IResourceProvider>(),
            settings,
            raService,
            logger);

        DataContext = _viewModel;

        Opened += RetroAchievementsWindow_Opened;
        Closed += (_, _) =>
        {
            _isClosed = true;
            _loadGeneration++; // Invalidate any load still in flight.
        };
    }

    /// <summary>
    ///     Whether the given load is still the latest one and the window is alive.
    /// </summary>
    private bool IsCurrentLoad(int generation)
    {
        return !_isClosed && IsLoaded && generation == _loadGeneration;
    }

    private void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.Source is not TabControl { SelectedItem: TabItem selectedTab })
                return;

            if (!selectedTab.IsSelected) return;

            switch (selectedTab.Tag?.ToString())
            {
                case "MyProfile":
                    _playSoundEffects.PlayNotificationSound();
                    _ = LoadUserProfileAsync();
                    break;
                case "Unlocks":
                    _playSoundEffects.PlayNotificationSound();
                    _ = LoadUnlocksByDateAsync();
                    break;
                case "UserProgress":
                    _playSoundEffects.PlayNotificationSound();
                    _ = LoadUserProgressAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in TabControl_SelectionChanged of RetroAchievementsWindow");
        }
    }

    private void RetroAchievementsWindow_Opened(object? sender, EventArgs e)
    {
        try
        {
            _ = LoadUserProfileAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in RetroAchievementsWindow_Opened");
        }
    }

    private async Task LoadUserProfileAsync()
    {
        var generation = ++_loadGeneration;
        _logger.Debug("Fetching user profile...");
        SetLoadingState(true);

        await _viewModel.LoadUserProfileAsync();

        // AV-21: superseded by a newer tab load, or the window closed mid-load.
        if (!IsCurrentLoad(generation)) return;

        // Toggle overlays
        UserProfilePanel.IsVisible = !_viewModel.NoProfileVisible;
        NoProfileOverlay.IsVisible = _viewModel.NoProfileVisible;

        if (_viewModel.NoProfileVisible)
        {
            NoProfileMainMessage.Text = _viewModel.NoProfileMainMessage;
            NoProfileSubMessage.Text = _viewModel.NoProfileSubMessage;
            SetLoadingState(false);
            return;
        }

        // Update profile header
        UserProfileUser.Text = _viewModel.ProfileUser;
        UserProfileMotto.Text = _viewModel.ProfileMotto;
        UserProfileRichPresence.Text = _viewModel.ProfileRichPresence;

        // Update stats
        PointsValue.Text = _viewModel.ProfilePoints;
        TruePointsValue.Text = _viewModel.ProfileTruePoints;
        RankValue.Text = _viewModel.ProfileRank;

        // Update detailed info
        UserProfileMemberSince.Text = _viewModel.ProfileMemberSince;
        UserProfileId.Text = _viewModel.ProfileId;
        UserProfileContributions.Text = _viewModel.ProfileContributions;
        UserProfileSoftcorePoints.Text = _viewModel.ProfileSoftcorePoints;
        UserProfilePermissions.Text = _viewModel.ProfilePermissions;
        UserProfileStatus.Text = _viewModel.ProfileStatus;
        UserProfileProfileId.Text = _viewModel.ProfileProfileId;
        UserProfileWallActive.Text = _viewModel.ProfileWallActive;

        // Update profile image
        UserProfilePic.Url = _viewModel.ProfileImageUrl;

        // Bind recently played games
        UserProfileRecentlyPlayed.ItemsSource = _viewModel.RecentlyPlayedGames;

        SetLoadingState(false);
    }

    private async Task LoadUnlocksByDateAsync()
    {
        var generation = ++_loadGeneration;
        _logger.Debug("Fetching earned achievements by date...");
        SetLoadingState(true);

        // Sync DatePickers with ViewModel (Avalonia DatePicker uses DateTimeOffset)
        FromDatePicker.SelectedDate = ToDateTimeOffset(_viewModel.FromDate);
        ToDatePicker.SelectedDate = ToDateTimeOffset(_viewModel.ToDate);

        await _viewModel.LoadUnlocksByDateAsync();

        // AV-21: superseded by a newer tab load, or the window closed mid-load.
        if (!IsCurrentLoad(generation)) return;

        // Bind unlocks data
        UnlocksDataGrid.ItemsSource = _viewModel.Unlocks;

        // Update totals
        TotalUnlocksInRangeText.Text = _viewModel.TotalUnlocksInRange;
        TotalPointsEarnedInRangeText.Text = _viewModel.TotalPointsEarnedInRange;

        // Toggle overlay
        NoUnlocksOverlay.IsVisible = _viewModel.NoUnlocksVisible;
        if (_viewModel.NoUnlocksVisible) NoUnlocksMessage.Text = _viewModel.NoUnlocksMessage;

        FetchUnlocksButton.IsEnabled = _viewModel.FetchUnlocksEnabled;

        SetLoadingState(false);
    }

    private async void FetchUnlocksClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Sync ViewModel with DatePickers before fetching
            _viewModel.FromDate = FromDatePicker.SelectedDate?.DateTime;
            _viewModel.ToDate = ToDatePicker.SelectedDate?.DateTime;

            await _viewModel.FetchUnlocksCommand.ExecuteAsync(null);
            // AV-21: the window may have closed while fetching.
            if (_isClosed || !IsLoaded) return;

            UnlocksDataGrid.ItemsSource = _viewModel.Unlocks;
            TotalUnlocksInRangeText.Text = _viewModel.TotalUnlocksInRange;
            TotalPointsEarnedInRangeText.Text = _viewModel.TotalPointsEarnedInRange;
            NoUnlocksOverlay.IsVisible = _viewModel.NoUnlocksVisible;
            if (_viewModel.NoUnlocksVisible) NoUnlocksMessage.Text = _viewModel.NoUnlocksMessage;

            FetchUnlocksButton.IsEnabled = _viewModel.FetchUnlocksEnabled;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to fetch unlocks by date");
        }
    }

    private async void ResetDatesClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            _logger.Debug("Resetting dates and fetching unlocks...");

            await _viewModel.ResetDatesCommand.ExecuteAsync(null);
            // AV-21: the window may have closed while fetching.
            if (_isClosed || !IsLoaded) return;

            UnlocksDataGrid.ItemsSource = _viewModel.Unlocks;
            TotalUnlocksInRangeText.Text = _viewModel.TotalUnlocksInRange;
            TotalPointsEarnedInRangeText.Text = _viewModel.TotalPointsEarnedInRange;
            NoUnlocksOverlay.IsVisible = _viewModel.NoUnlocksVisible;
            if (_viewModel.NoUnlocksVisible) NoUnlocksMessage.Text = _viewModel.NoUnlocksMessage;

            FromDatePicker.SelectedDate = ToDateTimeOffset(_viewModel.FromDate);
            ToDatePicker.SelectedDate = ToDateTimeOffset(_viewModel.ToDate);
            FetchUnlocksButton.IsEnabled = _viewModel.FetchUnlocksEnabled;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to reset date range");
        }
    }

    private async Task LoadUserProgressAsync()
    {
        var generation = ++_loadGeneration;
        _logger.Debug("Fetching user completion progress...");
        SetLoadingState(true);

        await _viewModel.LoadUserProgressAsync();

        // AV-21: superseded by a newer tab load, or the window closed mid-load.
        if (!IsCurrentLoad(generation)) return;

        // Bind user progress data
        UserProgressDataGrid.ItemsSource = _viewModel.UserProgress;

        // Toggle overlay
        NoUserProgressOverlay.IsVisible = _viewModel.NoUserProgressVisible;
        if (_viewModel.NoUserProgressVisible)
        {
            NoUserProgressMainMessage.Text = _viewModel.NoUserProgressMainMessage;
            NoUserProgressSubMessage.Text = _viewModel.NoUserProgressSubMessage;
        }

        SetLoadingState(false);
    }

    private async void OpenUrlInBrowserAsync(string url)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            // AV-15: validate the http(s) scheme and open via the cross-platform
            // launcher instead of assuming Windows shell-execute.
            if (!await ExternalLinkHelper.TryOpenUrlAsync(url, GetTopLevel(this)))
            {
                _logger.Error("Unable to open URL: invalid or unreachable URL");
                await _messageBox.UnableToOpenLinkMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error opening URL: {url}");
            await _messageBox.UnableToOpenLinkMessageBoxAsync();
        }
    }

    private void ViewProfileOnRaButton_Click(object? sender, RoutedEventArgs e)
    {
        var url = _viewModel.GetProfileUrl();
        if (!string.IsNullOrWhiteSpace(url)) OpenUrlInBrowserAsync(url);
    }

    private async void OpenRaSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsSettingsWindow>();
            _playSoundEffects.PlayNotificationSound();
            await settingsWindow.ShowDialog(this);

            // AV-21: the window may have closed while the settings dialog was open.
            if (_isClosed || !IsLoaded) return;

            // Reload current tab
            if (TabControl.SelectedItem is TabItem selectedTab)
            {
                switch (selectedTab.Tag?.ToString())
                {
                    case "MyProfile":
                        _playSoundEffects.PlayNotificationSound();
                        _ = LoadUserProfileAsync();
                        break;
                    case "Unlocks":
                        _playSoundEffects.PlayNotificationSound();
                        _ = LoadUnlocksByDateAsync();
                        break;
                    case "UserProgress":
                        _playSoundEffects.PlayNotificationSound();
                        _ = LoadUserProgressAsync();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method OpenRaSettings_Click");
        }
    }

    /// <summary>
    ///     Toggles the loading overlay with an optional message.
    /// </summary>
    /// <param name="isLoading">Whether to show or hide the loading overlay.</param>
    /// <param name="message">Optional message to display while loading.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        LoadingOverlay.IsVisible = isLoading;
        if (isLoading) LoadingOverlayMessage.Text = message ?? _resourceProvider.GetString("Loading", "Loading...");
    }

    private void EmergencyOverlayRelease_Click(object? sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        LoadingOverlay.IsVisible = false;

        _logger.Debug("[Emergency] User forced overlay dismissal in RetroAchievements Window");
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? date)
    {
        return date is { } d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Local)) : null;
    }
}