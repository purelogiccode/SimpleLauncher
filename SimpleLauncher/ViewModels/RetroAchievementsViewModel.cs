using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Services.RetroAchievements;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the RetroAchievements window, managing user profile, unlocks, and completion progress display.
/// </summary>
public partial class RetroAchievementsViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly RetroAchievementsService _raService;
    private readonly IResourceProvider _resourceProvider;
    private readonly SettingsManagerService _settings;

    [ObservableProperty] public partial bool FetchUnlocksEnabled { get; set; } = true;

    [ObservableProperty] public partial DateTime? FromDate { get; set; }

    // Loading state
    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial string NoProfileMainMessage { get; set; } = "";

    [ObservableProperty] public partial string NoProfileSubMessage { get; set; } = "";

    [ObservableProperty] public partial bool NoProfileVisible { get; set; }

    [ObservableProperty] public partial string NoUnlocksMessage { get; set; } = "";

    [ObservableProperty] public partial bool NoUnlocksVisible { get; set; }

    [ObservableProperty] public partial string NoUserProgressMainMessage { get; set; } = "";

    [ObservableProperty] public partial string NoUserProgressSubMessage { get; set; } = "";

    [ObservableProperty] public partial bool NoUserProgressVisible { get; set; }

    [ObservableProperty] public partial string ProfileContributions { get; set; } = "";

    [ObservableProperty] public partial string ProfileId { get; set; } = "";

    // Profile tab
    [ObservableProperty] public partial string? ProfileImageUrl { get; set; }

    [ObservableProperty] public partial string ProfileMemberSince { get; set; } = "";

    [ObservableProperty] public partial string ProfileMotto { get; set; } = "";

    [ObservableProperty] public partial string ProfilePermissions { get; set; } = "";

    [ObservableProperty] public partial string ProfilePoints { get; set; } = "";

    [ObservableProperty] public partial string ProfileProfileId { get; set; } = "";

    [ObservableProperty] public partial string ProfileRank { get; set; } = "";

    [ObservableProperty] public partial string ProfileRichPresence { get; set; } = "";

    [ObservableProperty] public partial string ProfileSoftcorePoints { get; set; } = "";

    [ObservableProperty] public partial string ProfileStatus { get; set; } = "";

    [ObservableProperty] public partial string ProfileTruePoints { get; set; } = "";

    [ObservableProperty] public partial string ProfileUser { get; set; } = "";

    [ObservableProperty] public partial string ProfileWallActive { get; set; } = "";

    [ObservableProperty] public partial ObservableCollection<RaRecentlyPlayedGame>? RecentlyPlayedGames { get; set; }

    [ObservableProperty] public partial DateTime? ToDate { get; set; }

    [ObservableProperty] public partial string TotalPointsEarnedInRange { get; set; } = "0";

    [ObservableProperty] public partial string TotalUnlocksInRange { get; set; } = "0";

    // Unlocks tab
    [ObservableProperty] public partial ObservableCollection<RaEarnedAchievement>? Unlocks { get; set; }

    // User Progress tab
    [ObservableProperty] public partial ObservableCollection<RaUserCompletionGame>? UserProgress { get; set; }

    /// <summary>Initializes a new instance of the <see cref="RetroAchievementsViewModel" />.</summary>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="raService">The RetroAchievements service.</param>
    /// <param name="logger">The logger instance.</param>
    public RetroAchievementsViewModel(
        IMessageBoxLibraryService messageBox,
        IResourceProvider resourceProvider,
        SettingsManagerService settings,
        RetroAchievementsService raService,
        ILogger logger)
    {
        _messageBox = messageBox;
        _resourceProvider = resourceProvider;
        _settings = settings;
        _raService = raService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Set default dates
        FromDate = DateTime.Today.AddMonths(-1);
        ToDate = DateTime.Today;
    }

    /// <summary>Loads the user profile and recently played games from RetroAchievements.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadUserProfileAsync()
    {
        IsLoading = true;
        NoProfileVisible = false;
        RecentlyPlayedGames = null;

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            NoProfileVisible = true;
            NoProfileMainMessage = _resourceProvider.GetString("RaErrorCredentialsNotSetShort",
                "RetroAchievements username or API key is not set.");
            NoProfileSubMessage = _resourceProvider.GetString("RaInfoConfigureCredentials",
                "Please configure your credentials in the RetroAchievements settings.");
            IsLoading = false;
            return;
        }

        try
        {
            _logger.Debug($"[RA VM] Fetching user profile for {_settings.RaUsername}...");
            var userProfile = await _raService.GetUserProfileAsync(_settings.RaUsername, _settings.RaApiKey);

            _logger.Debug($"[RA VM] Fetching recently played games for {_settings.RaUsername}...");
            var recentlyPlayedGames =
                await _raService.GetUserRecentlyPlayedGamesAsync(_settings.RaUsername, _settings.RaApiKey, 50);

            if (userProfile != null)
            {
                ProfileImageUrl = !string.IsNullOrEmpty(userProfile.UserPic)
                    ? $"https://retroachievements.org{userProfile.UserPic}"
                    : null;

                ProfileUser = userProfile.User;
                ProfileMotto = string.IsNullOrWhiteSpace(userProfile.Motto)
                    ? _resourceProvider.GetString("RaInfoNoMotto", "No motto set")
                    : userProfile.Motto;

                ProfileRichPresence = string.IsNullOrWhiteSpace(userProfile.RichPresenceMsg)
                    ? _resourceProvider.GetString("RaInfoNotCurrentlyPlaying", "Not currently playing")
                    : userProfile.RichPresenceMsg;

                var rankFormat = _resourceProvider.GetString("RaInfoRankFormat", "#{0}");
                ProfileRank = string.IsNullOrWhiteSpace(userProfile.Rank)
                    ? _resourceProvider.GetString("RaStatusNotApplicable", "N/A")
                    : string.Format(CultureInfo.InvariantCulture, rankFormat, userProfile.Rank);

                ProfilePoints = userProfile.TotalPoints.ToString("N0", CultureInfo.InvariantCulture);
                ProfileTruePoints = userProfile.TotalTruePoints.ToString("N0", CultureInfo.InvariantCulture);

                if (DateTime.TryParse(userProfile.MemberSince, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal, out var memberSinceDate))
                {
                    ProfileMemberSince = memberSinceDate.ToLocalTime()
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                else
                {
                    ProfileMemberSince = string.IsNullOrWhiteSpace(userProfile.MemberSince)
                        ? _resourceProvider.GetString("RaStatusUnknown", "Unknown")
                        : userProfile.MemberSince;
                }

                ProfileId = userProfile.Id.ToString(CultureInfo.InvariantCulture);
                var contributionsFormat =
                    _resourceProvider.GetString("RaInfoContributionsFormat", "{0} contributions ({1:N0} points)");
                ProfileContributions = string.Format(CultureInfo.InvariantCulture, contributionsFormat,
                    userProfile.ContribCount, userProfile.ContribYield);
                ProfileSoftcorePoints = userProfile.TotalSoftcorePoints.ToString("N0", CultureInfo.InvariantCulture);
                ProfilePermissions = GetPermissionDescription(userProfile.Permissions);
                ProfileStatus = userProfile.Untracked == 1
                    ? _resourceProvider.GetString("RaStatusUntracked", "Untracked")
                    : _resourceProvider.GetString("RaStatusTracked", "Tracked");
                ProfileProfileId = string.IsNullOrWhiteSpace(userProfile.Ulid)
                    ? _resourceProvider.GetString("RaStatusNotApplicable", "N/A")
                    : userProfile.Ulid;
                ProfileWallActive = userProfile.UserWallActive
                    ? _resourceProvider.GetString("RaGenericYes", "Yes")
                    : _resourceProvider.GetString("RaGenericNo", "No");

                RecentlyPlayedGames = recentlyPlayedGames is { Count: > 0 }
                    ? new ObservableCollection<RaRecentlyPlayedGame>(recentlyPlayedGames)
                    : null;

                NoProfileVisible = false;
            }
            else
            {
                NoProfileVisible = true;
                NoProfileMainMessage =
                    _resourceProvider.GetString("RaErrorFailedToLoadUserProfile", "Failed to load user profile.");
                NoProfileSubMessage = _resourceProvider.GetString("RaInfoCheckCredentials",
                    "Please check your RetroAchievements credentials or try again later.");
            }
        }
        catch (RaUnauthorizedException)
        {
            NoProfileVisible = true;
            NoProfileMainMessage = _resourceProvider.GetString("RaErrorUnauthorized",
                "RetroAchievements credentials invalid. Please check your username and API key in settings.");
            NoProfileSubMessage = _resourceProvider.GetString("RaInfoConfigureCredentials",
                "Please configure your credentials in the RetroAchievements settings.");
        }
        catch (Exception ex)
        {
            NoProfileVisible = true;
            NoProfileMainMessage = _resourceProvider.GetString("RaErrorLoadingUserProfile",
                "An error occurred while loading user profile.");
            NoProfileSubMessage = _resourceProvider.GetString("RaInfoCheckConnection",
                "Please try again or check your internet connection.");
            _logger.Error(ex, $"Failed to load user profile for {_settings.RaUsername}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Loads the achievements earned between the selected dates.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadUnlocksByDateAsync()
    {
        IsLoading = true;
        FetchUnlocksEnabled = false;
        NoUnlocksVisible = false;
        Unlocks = null;
        TotalUnlocksInRange = "0";
        TotalPointsEarnedInRange = "0";

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            NoUnlocksVisible = true;
            NoUnlocksMessage = _resourceProvider.GetString("RaErrorCredentialsNotSet",
                "RetroAchievements username or API key is not set. Configure in settings.");
            IsLoading = false;
            FetchUnlocksEnabled = true;
            return;
        }

        var fromDate = FromDate ?? DateTime.Today.AddMonths(-1);
        var toDate = ToDate ?? DateTime.Today;

        try
        {
            _logger.Debug(
                $"[RA VM] Fetching unlocks for {_settings.RaUsername} from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}...");
            var unlocks =
                await _raService.GetAchievementsEarnedBetweenAsync(_settings.RaUsername, _settings.RaApiKey, fromDate,
                    toDate);

            if (unlocks is { Count: > 0 })
            {
                Unlocks = new ObservableCollection<RaEarnedAchievement>(unlocks);
                TotalUnlocksInRange = unlocks.Count.ToString("N0", CultureInfo.InvariantCulture);
                TotalPointsEarnedInRange =
                    unlocks.Sum(static a => a.Points).ToString("N0", CultureInfo.InvariantCulture);
                NoUnlocksVisible = false;
            }
            else
            {
                Unlocks = null;
                TotalUnlocksInRange = "0";
                TotalPointsEarnedInRange = "0";
                NoUnlocksVisible = true;
                NoUnlocksMessage = unlocks == null
                    ? _resourceProvider.GetString("RaErrorFailedToLoadUnlocks",
                        "Failed to load unlocks. Please check your RetroAchievements credentials or try again later.")
                    : _resourceProvider.GetString("RaInfoNoUnlocksFound",
                        "No unlocks found for the selected date range.");
            }
        }
        catch (RaUnauthorizedException)
        {
            NoUnlocksVisible = true;
            NoUnlocksMessage = _resourceProvider.GetString("RaErrorUnauthorized",
                "RetroAchievements credentials invalid. Please check your username and API key in settings.");
        }
        catch (Exception ex)
        {
            Unlocks = null;
            TotalUnlocksInRange = "0";
            TotalPointsEarnedInRange = "0";
            NoUnlocksVisible = true;
            NoUnlocksMessage = _resourceProvider.GetString("RaErrorLoadingUnlocks",
                "An error occurred while loading unlocks. Please try again.");
            _logger.Error(ex, $"Failed to load unlocks by date for user {_settings.RaUsername}");
        }
        finally
        {
            IsLoading = false;
            FetchUnlocksEnabled = true;
        }
    }

    [RelayCommand]
    private async Task FetchUnlocksAsync()
    {
        var fromDate = FromDate ?? DateTime.Today.AddMonths(-1);
        var toDate = ToDate ?? DateTime.Today;

        if (fromDate > toDate)
        {
            await _messageBox.ErrorMessageBoxAsync();
            return;
        }

        await LoadUnlocksByDateAsync();
    }

    [RelayCommand]
    private Task ResetDatesAsync()
    {
        FromDate = DateTime.Today.AddMonths(-1);
        ToDate = DateTime.Today;
        Unlocks = null;
        TotalUnlocksInRange = "0";
        TotalPointsEarnedInRange = "0";
        NoUnlocksVisible = true;
        NoUnlocksMessage =
            _resourceProvider.GetString("RaInfoNoUnlocksFound", "No unlocks found for the selected date range.");

        return LoadUnlocksByDateAsync();
    }

    /// <summary>Loads the user completion progress from RetroAchievements.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadUserProgressAsync()
    {
        IsLoading = true;
        NoUserProgressVisible = false;
        UserProgress = null;

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            NoUserProgressVisible = true;
            NoUserProgressMainMessage = _resourceProvider.GetString("RaErrorCredentialsNotSetShort",
                "RetroAchievements username or API key is not set.");
            NoUserProgressSubMessage = _resourceProvider.GetString("RaInfoConfigureCredentials",
                "Please configure your credentials in the RetroAchievements settings.");
            IsLoading = false;
            return;
        }

        try
        {
            var userProgressList =
                await _raService.GetUserCompletionProgressAsync(_settings.RaUsername, _settings.RaApiKey);

            if (userProgressList is { Count: > 0 })
            {
                UserProgress = new ObservableCollection<RaUserCompletionGame>(userProgressList);
                NoUserProgressVisible = false;
            }
            else
            {
                UserProgress = null;
                NoUserProgressVisible = true;
                if (userProgressList == null)
                {
                    NoUserProgressMainMessage = _resourceProvider.GetString("RaErrorFailedToLoadUserProgress",
                        "Failed to load user completion progress.");
                    NoUserProgressSubMessage = _resourceProvider.GetString("RaInfoCheckCredentials",
                        "Please check your RetroAchievements credentials or try again later.");
                }
                else
                {
                    NoUserProgressMainMessage = _resourceProvider.GetString("RaInfoNoUserProgressFound",
                        "No user completion progress found.");
                    NoUserProgressSubMessage = _resourceProvider.GetString("RaInfoNoUserProgressSubMessage",
                        "This could be because you haven't played any games yet.");
                }
            }
        }
        catch (RaUnauthorizedException)
        {
            NoUserProgressVisible = true;
            NoUserProgressMainMessage = _resourceProvider.GetString("RaErrorUnauthorized",
                "RetroAchievements credentials invalid. Please check your username and API key in settings.");
            NoUserProgressSubMessage = _resourceProvider.GetString("RaInfoConfigureCredentials",
                "Please configure your credentials in the RetroAchievements settings.");
        }
        catch (Exception ex)
        {
            NoUserProgressVisible = true;
            NoUserProgressMainMessage = _resourceProvider.GetString("RaErrorLoadingUserProgress",
                "An error occurred while loading user completion progress.");
            NoUserProgressSubMessage = _resourceProvider.GetString("RaInfoCheckConnection",
                "Please try again or check your internet connection.");
            _logger.Error(ex, $"Failed to load user completion progress for user {_settings.RaUsername}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Builds the public RetroAchievements profile URL for the configured username.</summary>
    /// <returns>The full profile URL string.</returns>
    public string GetProfileUrl()
    {
        return $"https://retroachievements.org/user/{Uri.EscapeDataString(_settings.RaUsername ?? "")}";
    }

    private string GetPermissionDescription(int permissions)
    {
        return permissions switch
        {
            0 => _resourceProvider.GetString("RaPermissionUnregistered", "Unregistered"),
            1 => _resourceProvider.GetString("RaPermissionRegistered", "Registered"),
            2 => _resourceProvider.GetString("RaPermissionJuniorDeveloper", "Junior Developer"),
            3 => _resourceProvider.GetString("RaPermissionDeveloper", "Developer"),
            4 => _resourceProvider.GetString("RaPermissionAdmin", "Admin"),
            _ => $"{_resourceProvider.GetString("RaStatusUnknown", "Unknown")} ({permissions})"
        };
    }
}