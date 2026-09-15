using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Models;
using SimpleLauncher.Services.RetroAchievements;

namespace SimpleLauncher;

/// <summary>
///     Window displaying RetroAchievements data for a specific game, including achievements, rankings, and progress.
/// </summary>
public partial class RetroAchievementsForAGameWindow : ILoadingState
{
    private static readonly HttpClient SharedHttpClient = new();
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly PlaySoundEffects _playSoundEffects;
    private readonly RetroAchievementsService _raService;
    private readonly SettingsManagerService _settings;
    private Button? _emergencyReturnButton;

    private int _gameId;
    private string _gameTitleForDisplay = "";

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsForAGameWindow" /> class.
    /// </summary>
    /// <param name="playSoundEffects">The sound effects service.</param>
    /// <param name="settings">The application settings manager.</param>
    /// <param name="raService">The RetroAchievements API service.</param>
    /// <param name="logger">The error logging service.</param>
    public RetroAchievementsForAGameWindow(PlaySoundEffects playSoundEffects, SettingsManagerService settings,
        RetroAchievementsService raService, ILogger logger)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);
        Owner = Application.Current.MainWindow;

        _settings = settings;
        _raService = raService;
        _playSoundEffects = playSoundEffects;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messageBox = App.ServiceProvider.GetRequiredService<IMessageBoxLibraryService>();

        Loaded += AchievementsWindow_Loaded;

        Loaded += (_, _) =>
        {
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
            {
                _emergencyReturnButton = emergencyBtn;
                emergencyBtn.Click += EmergencyOverlayRelease_Click;
            }
        };

        Closing += (_, _) =>
        {
            if (_emergencyReturnButton != null)
            {
                _emergencyReturnButton.Click -= EmergencyOverlayRelease_Click;
                _emergencyReturnButton = null;
            }
        };
    }

    /// <summary>
    ///     Toggles the loading overlay with an optional message.
    /// </summary>
    /// <param name="isLoading">Whether to show or hide the loading overlay.</param>
    /// <param name="message">Optional message to display while loading.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        Dispatcher.Invoke(() =>
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            if (isLoading)
            {
                LoadingOverlay.Content =
                    message ?? (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
            }
        });
    }

    /// <summary>
    ///     Initializes the window with the specified game ID and display title.
    /// </summary>
    /// <param name="gameId">The RetroAchievements game ID.</param>
    /// <param name="gameTitleForDisplay">The game title to display in the window.</param>
    public void Initialize(int gameId, string gameTitleForDisplay)
    {
        _gameId = gameId;
        _gameTitleForDisplay = gameTitleForDisplay;
    }

    private void AchievementsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            GameTitleTextBlock.Text = _gameTitleForDisplay;
            // Force load the first tab's data
            // The SelectionChanged event might not fire if the first tab is already selected
            _ = LoadGameAchievementsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize RetroAchievementsForAGameWindow");
        }
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.Source is not TabControl { SelectedItem: TabItem selectedTab })
                return;

            if (selectedTab is not { IsSelected: true }) return;

            var tag = selectedTab.Tag?.ToString();

            switch (tag)
            {
                case "Achievements":
                    _playSoundEffects.PlayNotificationSound();
                    (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
                        (string)Application.Current.TryFindResource("LoadingAchievements") ??
                        "Loading achievements...");
                    _ = LoadGameAchievementsAsync();
                    break;
                case "GameInfo":
                    _playSoundEffects.PlayNotificationSound();
                    (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
                        (string)Application.Current.TryFindResource("LoadingExtendedGameInfo") ??
                        "Loading extended game info...");
                    _ = LoadGameInfoAsync();
                    break;
                case "GameRanking":
                    _playSoundEffects.PlayNotificationSound();
                    (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
                        (string)Application.Current.TryFindResource("LoadingGameRankings") ??
                        "Loading game rankings...");
                    _ = LoadGameRankingAsync();
                    break;
                case "MyProfile":
                    _playSoundEffects.PlayNotificationSound();
                    (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
                        (string)Application.Current.TryFindResource("LoadingUserProfile") ?? "Loading user profile...");
                    _ = LoadUserProfileAsync();
                    break;
                case "Unlocks":
                    _playSoundEffects.PlayNotificationSound();
                    (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
                        (string)Application.Current.TryFindResource("LoadingUserUnlocks") ?? "Loading user unlocks...");
                    _ = LoadUnlocksByDateAsync();
                    break;
                case "UserProgress":
                    _playSoundEffects.PlayNotificationSound();
                    (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
                        (string)Application.Current.TryFindResource("LoadingUserCompletionProgress") ??
                        "Loading user completion progress...");
                    _ = LoadUserProgressAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in TabControl_SelectionChanged of RetroAchievementsForAGameWindow");
        }
    }

    private void UpdateProgressDisplay(RaUserGameProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                // Parse completion percentages
                double casualCompletion = 0;
                double hardcoreCompletion = 0;

                if (!string.IsNullOrWhiteSpace(progress.UserCompletion))
                {
                    var casualText = progress.UserCompletion.Replace("%", "").Trim();
                    if (!double.TryParse(casualText, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out casualCompletion))
                    {
                        _logger.Warning(
                            $"Failed to parse casual completion percentage: '{casualText}' (original: '{progress.UserCompletion}')");
                    }
                }

                if (!string.IsNullOrWhiteSpace(progress.UserCompletionHardcore))
                {
                    var hardcoreText = progress.UserCompletionHardcore.Replace("%", "").Trim();
                    if (!double.TryParse(hardcoreText, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out hardcoreCompletion))
                    {
                        _logger.Warning(
                            $"Failed to parse hardcore completion percentage: '{hardcoreText}' (original: '{progress.UserCompletionHardcore}')");
                    }
                }

                // Update progress bars
                CasualProgressbar.Value = casualCompletion;
                HardcoreProgressbar.Value = hardcoreCompletion;

                // Update progress text
                CasualProgressText.Text = $"{casualCompletion:F1}%";
                HardcoreProgressText.Text = $"{hardcoreCompletion:F1}%";

                // Update achievement stats
                EarnedAchievementsValue.Text = $"{progress.AchievementsEarned}";
                TotalAchievementsValue.Text = $"{progress.TotalAchievements}";
                TotalPointsEarnedValue.Text = $"{progress.PointsEarned:N0}";
                TruePointsEarnedValue.Text = $"{progress.PointsEarnedHardcore:N0}";

                // Update highest award info
                string awardKindDisplay;
                switch (progress.HighestAwardKind?.ToLowerInvariant())
                {
                    case "mastered":
                        awardKindDisplay = (string)Application.Current.TryFindResource("RaAwardMastered") ?? "Mastered";
                        break;
                    default:
                        awardKindDisplay = string.IsNullOrWhiteSpace(progress.HighestAwardKind)
                            ? (string)Application.Current.TryFindResource("RaStatusNone") ?? "None"
                            : CapitalizeFirstLetter(progress.HighestAwardKind);
                        break;
                }

                HighestAwardKindText.Text = awardKindDisplay;

                // Set Highest Award Icon (using existing trophy.png from ContextMenu.cs)
                if (progress.HighestAwardKind?.Equals("mastered", StringComparison.OrdinalIgnoreCase) == true)
                {
                    HighestAwardIcon.Source =
                        new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/trophy.png"));
                    HighestAwardIcon.Visibility = Visibility.Visible;
                }
                else
                {
                    HighestAwardIcon.Visibility = Visibility.Collapsed;
                }

                if (DateTime.TryParse(progress.HighestAwardDate, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal, out var awardDate))
                {
                    HighestAwardDateText.Text =
                        awardDate.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                else
                {
                    HighestAwardDateText.Text =
                        (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";
                }
            }
            catch (Exception ex)
            {
                // Fallback values if parsing fails
                CasualProgressbar.Value = 0;
                HardcoreProgressbar.Value = 0;
                CasualProgressText.Text = "0%";
                HardcoreProgressText.Text = "0%";
                EarnedAchievementsValue.Text = "0";
                TotalAchievementsValue.Text = "0";
                TotalPointsEarnedValue.Text = "0";
                TruePointsEarnedValue.Text = "0";
                HighestAwardKindText.Text =
                    (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";
                HighestAwardDateText.Text =
                    (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";
                HighestAwardIcon.Visibility = Visibility.Collapsed; // Ensure icon is hidden on error

                _logger.Error(ex, "Failed to parse progress data for achievements display");
            }
        });
    }

    private static string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return char.ToUpper(input[0], CultureInfo.InvariantCulture) + input[1..];
    }

    private async void OpenUrlInBrowserAsync(string url)
    {
        try
        {
            _playSoundEffects.PlayNotificationSound();
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error opening URL: {url}");
            await _messageBox.UnableToOpenLinkMessageBoxAsync();
        }
    }

    private void ViewProfileOnRaButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_settings.RaUsername))
        {
            var url = $"https://retroachievements.org/user/{Uri.EscapeDataString(_settings.RaUsername)}";
            OpenUrlInBrowserAsync(url);
        }
    }

    private void ViewGameOnRaButton_Click(object sender, RoutedEventArgs e)
    {
        var url = $"https://retroachievements.org/game/{_gameId}";
        OpenUrlInBrowserAsync(url);
    }

    private async void GameImage_MouseLeftButtonDownAsync(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is Image clickedImage)
            {
                if (clickedImage.Source is BitmapImage bitmapImage && bitmapImage.UriSource != null)
                {
                    _playSoundEffects.PlayNotificationSound();
                    OpenRaImageViewerAsync(bitmapImage.UriSource); // Use the new RetroAchievementsImageViewerWindow
                }
                else
                {
                    // Log and potentially inform the user if the image source is not a valid URI
                    _logger.Warning("Clicked image has no valid URI source to display in viewer");
                    await _messageBox.ErrorMessageBoxAsync(); // Generic error for the user
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method GameImage_MouseLeftButtonDownAsync");
        }
    }

    private void OpenRaImageViewerAsync(Uri imageUri)
    {
        try
        {
            var raImageViewer = App.ServiceProvider.GetRequiredService<ImageViewerWindow>();
            raImageViewer.LoadImageUrl(imageUri);
            raImageViewer.Owner = this;
            raImageViewer.Show();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Failed to open RetroAchievements image viewer for URI: {imageUri}");
            _logger.Debug($"Failed to open RetroAchievements image viewer for URI: {imageUri}");
        }
    }

    private static string FormatDateString(string dateString)
    {
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date))
            return date.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        return dateString;
    }

    private static string GetPermissionDescription(int permissions)
    {
        return permissions switch
        {
            0 => (string)Application.Current.TryFindResource("RaPermissionUnregistered") ?? "Unregistered",
            1 => (string)Application.Current.TryFindResource("RaPermissionRegistered") ?? "Registered",
            2 => (string)Application.Current.TryFindResource("RaPermissionJuniorDeveloper") ?? "Junior Developer",
            3 => (string)Application.Current.TryFindResource("RaPermissionDeveloper") ?? "Developer",
            4 => (string)Application.Current.TryFindResource("RaPermissionAdmin") ?? "Admin",
            _ => $"{(string)Application.Current.TryFindResource("RaStatusUnknown") ?? "Unknown"} ({permissions})"
        };
    }

    private void OpenRaSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = App.ServiceProvider.GetRequiredService<RetroAchievementsSettingsWindow>();
            settingsWindow.Owner = this;
            _playSoundEffects.PlayNotificationSound();
            settingsWindow.ShowDialog();

            // Reload current tab using Tag instead of Header
            if (TabControl.SelectedItem is TabItem selectedTab)
            {
                var tag = selectedTab.Tag?.ToString();
                switch (tag)
                {
                    case "Achievements":
                        _playSoundEffects.PlayNotificationSound();
                        _ = LoadGameAchievementsAsync();
                        break;
                    case "GameInfo":
                        _playSoundEffects.PlayNotificationSound();
                        _ = LoadGameInfoAsync();
                        break;
                    case "GameRanking":
                        _playSoundEffects.PlayNotificationSound();
                        _ = LoadGameRankingAsync();
                        break;
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
            _logger.Error(ex, "Error in OpenRaSettings_Click of RetroAchievementsForAGameWindow");
        }
    }

    private async Task LoadGameAchievementsAsync()
    {
        (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("FetchingGameAchievements") ?? "Fetching game achievements...");

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            NoAchievementsOverlay.Visibility = Visibility.Visible;
            NoAchievementsMessage.Text = (string)Application.Current.TryFindResource("RaErrorCredentialsNotSet") ??
                                         "RetroAchievements username or API key is not set. Configure in settings.";
            await Task.Yield();

            return;
        }

        LoadingOverlay.Content = (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            await Task.Yield();

            NoAchievementsOverlay.Visibility = Visibility.Collapsed; // Hide overlay initially
            AchievementsDataGrid.ItemsSource = null; // Clear previous data

            // Use the injected service
            var (progress, achievements) =
                await _raService.GetGameInfoAndUserProgressAsync(_gameId, _settings.RaUsername, _settings.RaApiKey);

            if (progress != null && achievements is { Count: > 0 })
            {
                // Update progress summary header
                GameTitleTextBlock.Text = string.IsNullOrWhiteSpace(progress.GameTitle)
                    ? (string)Application.Current.TryFindResource("RaFallbackUnknownGame") ?? "Unknown Game"
                    : progress.GameTitle;
                ConsoleNameTextBlock.Text = string.IsNullOrWhiteSpace(progress.ConsoleName)
                    ? (string)Application.Current.TryFindResource("RaFallbackUnknownConsole") ?? "Unknown Console"
                    : progress.ConsoleName;

                if (!string.IsNullOrEmpty(progress.GameIconUrl))
                    GameCoverImage.Source = await LoadImageFromUrlAsync(progress.GameIconUrl);

                // Update progress bars and stats
                UpdateProgressDisplay(progress);

                await PreloadBadgeImagesAsync(achievements);
                AchievementsDataGrid.ItemsSource = achievements;
                NoAchievementsOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoAchievementsOverlay.Visibility = Visibility.Visible;
                // If progress is null, it indicates an API failure (since credentials were provided)
                // If progress is not null but achievements is empty, it means no achievements for the game.
                if (progress == null)
                {
                    NoAchievementsMessage.Text =
                        (string)Application.Current.TryFindResource("RaErrorFailedToLoadAchievements") ??
                        "Failed to load achievements. Please check your RetroAchievements credentials or try again later.";
                }
                else // progress is not null, but achievements is empty
                {
                    NoAchievementsMessage.Text =
                        (string)Application.Current.TryFindResource("RaInfoNoAchievementsForGame") ??
                        "No achievements found for this game.";
                }
            }
        }
        catch (RaUnauthorizedException)
        {
            NoAchievementsOverlay.Visibility = Visibility.Visible;
            NoAchievementsMessage.Text = (string)Application.Current.TryFindResource("RaErrorUnauthorized") ??
                                         "RetroAchievements credentials invalid. Please check your username and API key in settings.";
        }
        catch (Exception ex)
        {
            NoAchievementsOverlay.Visibility = Visibility.Visible;
            NoAchievementsMessage.Text = (string)Application.Current.TryFindResource("RaErrorLoadingAchievements") ??
                                         "An error occurred while loading achievements. Please try again.";
            _logger.Error(ex, $"Failed to load achievements for game ID: {_gameId}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await Task.Yield();
        }
    }

    private async Task LoadGameInfoAsync()
    {
        (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("FetchingExtendedGameInfo") ??
            "Fetching extended game info...");

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            NoGameInfoOverlay.Visibility = Visibility.Visible;
            NoGameInfoMessage.Text = (string)Application.Current.TryFindResource("RaErrorCredentialsNotSet") ??
                                     "RetroAchievements username or API key is not set. Configure in settings.";
            await Task.Yield();

            return;
        }

        LoadingOverlay.Content = (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            NoGameInfoOverlay.Visibility = Visibility.Collapsed; // Hide overlay initially
            GameInfoAchievementsSection.Visibility = Visibility.Collapsed;
            await Task.Yield();

            // Use the injected service
            var gameInfo = await _raService.GetGameExtendedAsync(_gameId, _settings.RaUsername, _settings.RaApiKey);
            if (gameInfo != null)
            {
                // Load game icon (for header and the new image section)
                if (!string.IsNullOrEmpty(gameInfo.ImageIcon))
                {
                    try
                    {
                        GameInfoImageIcon.Source =
                            await LoadImageFromUrlAsync($"https://retroachievements.org{gameInfo.ImageIcon}");
                    }
                    catch
                    {
                        GameInfoImageIcon.Source = null;
                    }
                }
                else
                {
                    GameInfoImageIcon.Source = null;
                }

                // Game images
                if (!string.IsNullOrEmpty(gameInfo.ImageTitle))
                {
                    try
                    {
                        GameInfoTitleImage.Source =
                            await LoadImageFromUrlAsync($"https://retroachievements.org{gameInfo.ImageTitle}");
                    }
                    catch
                    {
                        GameInfoTitleImage.Source = null;
                    }
                }
                else
                {
                    GameInfoTitleImage.Source = null;
                }

                if (!string.IsNullOrEmpty(gameInfo.ImageIngame))
                {
                    try
                    {
                        GameInfoIngameImage.Source =
                            await LoadImageFromUrlAsync($"https://retroachievements.org{gameInfo.ImageIngame}");
                    }
                    catch
                    {
                        GameInfoIngameImage.Source = null;
                    }
                }
                else
                {
                    GameInfoIngameImage.Source = null;
                }

                if (!string.IsNullOrEmpty(gameInfo.ImageBoxArt))
                {
                    try
                    {
                        GameInfoBoxArtImage.Source =
                            await LoadImageFromUrlAsync($"https://retroachievements.org{gameInfo.ImageBoxArt}");
                    }
                    catch
                    {
                        GameInfoBoxArtImage.Source = null;
                    }
                }
                else
                {
                    GameInfoBoxArtImage.Source = null;
                }

                // Basic details
                GameInfoGenre.Text = string.IsNullOrWhiteSpace(gameInfo.Genre)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : gameInfo.Genre;
                GameInfoDeveloper.Text = string.IsNullOrWhiteSpace(gameInfo.Developer)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : gameInfo.Developer;
                GameInfoPublisher.Text = string.IsNullOrWhiteSpace(gameInfo.Publisher)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : gameInfo.Publisher;
                GameInfoReleased.Text = string.IsNullOrWhiteSpace(gameInfo.Released)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : gameInfo.Released;

                // Additional details
                GameInfoConsoleName.Text = string.IsNullOrWhiteSpace(gameInfo.ConsoleName)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : gameInfo.ConsoleName;
                GameInfoPlayers.Text = gameInfo.NumDistinctPlayers.ToString("N0", CultureInfo.InvariantCulture);
                GameInfoAchievementCount.Text = gameInfo.NumAchievements.ToString(CultureInfo.InvariantCulture);
                GameInfoForumTopic.Text = gameInfo.ForumTopicId?.ToString(CultureInfo.InvariantCulture) ??
                                          (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";
                GameInfoUpdated.Text = string.IsNullOrWhiteSpace(gameInfo.Updated)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : FormatDateString(gameInfo.Updated);
                GameInfoConsoleId.Text = gameInfo.ConsoleId.ToString(CultureInfo.InvariantCulture);
                GameInfoId.Text = gameInfo.Id.ToString(CultureInfo.InvariantCulture);
                GameInfoParentGame.Text = gameInfo.ParentGameId?.ToString(CultureInfo.InvariantCulture) ??
                                          (string)Application.Current.TryFindResource("RaStatusNone") ?? "None";
                GameInfoReleaseGranularity.Text = string.IsNullOrWhiteSpace(gameInfo.ReleasedAtGranularity)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : gameInfo.ReleasedAtGranularity;
                GameInfoGuideUrl.Text = string.IsNullOrWhiteSpace(gameInfo.GuideUrl)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : gameInfo.GuideUrl;

                // Player statistics
                DistinctPlayersValue.Text = gameInfo.NumDistinctPlayers.ToString("N0", CultureInfo.InvariantCulture);
                CasualPlayersValue.Text =
                    gameInfo.NumDistinctPlayersCasual.ToString("N0", CultureInfo.InvariantCulture);
                HardcorePlayersValue.Text =
                    gameInfo.NumDistinctPlayersHardcore.ToString("N0", CultureInfo.InvariantCulture);

                // Claims
                var claimsFormat = (string)Application.Current.TryFindResource("RaInfoActiveClaimsCount") ??
                                   "{0} active development claim(s)";
                GameInfoClaims.Text = gameInfo.Claims.Count == 0
                    ? (string)Application.Current.TryFindResource("RaInfoNoActiveClaims") ??
                      "No active development claims"
                    : string.Format(CultureInfo.InvariantCulture, claimsFormat, gameInfo.Claims.Count);

                // Achievements list
                if (gameInfo.Achievements is { Count: > 0 })
                {
                    var achievementsList = gameInfo.Achievements.Values
                        .OrderBy(static a => a.DisplayOrder)
                        .Select(static a => new RaAchievement
                        {
                            Id = a.Id,
                            Title = a.Title,
                            Description = a.Description,
                            Points = a.Points,
                            BadgeUri = a.BadgeUri,
                            TrueRatio = a.TrueRatio,
                            Author = a.Author,
                            DateCreated = a.DateCreated
                        })
                        .ToList();
                    await PreloadBadgeImagesAsync(achievementsList);
                    GameInfoAchievementsDataGrid.ItemsSource = achievementsList;
                    GameInfoAchievementsSection.Visibility = Visibility.Visible;
                }
                else
                {
                    GameInfoAchievementsSection.Visibility = Visibility.Collapsed;
                }

                NoGameInfoOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoGameInfoOverlay.Visibility = Visibility.Visible;
                NoGameInfoMessage.Text = (string)Application.Current.TryFindResource("RaErrorFailedToLoadGameInfo") ??
                                         "Failed to load extended game information. Please check your RetroAchievements credentials or try again later.";
            }
        }
        catch (RaUnauthorizedException)
        {
            NoGameInfoOverlay.Visibility = Visibility.Visible;
            NoGameInfoMessage.Text = (string)Application.Current.TryFindResource("RaErrorUnauthorized") ??
                                     "RetroAchievements credentials invalid. Please check your username and API key in settings.";
        }
        catch (Exception ex)
        {
            NoGameInfoOverlay.Visibility = Visibility.Visible;
            NoGameInfoMessage.Text = (string)Application.Current.TryFindResource("RaErrorLoadingGameInfo") ??
                                     "An error occurred while loading game info. Please try again.";
            _logger.Error(ex, $"Failed to load extended game info for game ID: {_gameId}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await Task.Yield();
        }
    }

    private async Task LoadGameRankingAsync()
    {
        (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("FetchingGameRankings") ?? "Fetching game rankings...");

        // Check credentials first (no overlay flash when they are missing)
        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            LatestMastersDataGrid.ItemsSource = null;
            LatestMastersDataGrid.Visibility = Visibility.Collapsed;
            HighScoresDataGrid.ItemsSource = null;
            HighScoresDataGrid.Visibility = Visibility.Collapsed;

            NoUserRankOverlay.Visibility = Visibility.Visible;
            NoUserRankMessage.Text = (string)Application.Current.TryFindResource("RaErrorCredentialsNotSet") ??
                                     "RetroAchievements username or API key is not set. Configure in settings.";
            NoLatestMastersOverlay.Visibility = Visibility.Visible;
            NoLatestMastersMessage.Text = (string)Application.Current.TryFindResource("RaErrorCredentialsNotSet") ??
                                          "RetroAchievements username or API key is not set. Configure in settings.";
            NoHighScoresOverlay.Visibility = Visibility.Visible;
            NoHighScoresMessage.Text = (string)Application.Current.TryFindResource("RaErrorCredentialsNotSet") ??
                                       "RetroAchievements username or API key is not set. Configure in settings.";

            await Task.Yield();

            return;
        }

        LoadingOverlay.Content = (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            NoUserRankOverlay.Visibility = Visibility.Collapsed; // Hide overlay initially
            NoLatestMastersOverlay.Visibility = Visibility.Collapsed; // Hide overlay initially
            NoHighScoresOverlay.Visibility = Visibility.Collapsed; // Hide overlay initially
            await Task.Yield();

            // Clear previous data
            LatestMastersDataGrid.ItemsSource = null;
            HighScoresDataGrid.ItemsSource = null;

            // Reset user info
            UserRankText.Text = (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";
            UserScoreText.Text = (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";
            UserLastAwardText.Text = (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";

            // Load Latest Masters (t=1)
            var latestMasters =
                await _raService.GetGameRankAndScoreAsync(_gameId, _settings.RaUsername, _settings.RaApiKey, true);
            if (latestMasters is { Count: > 0 })
            {
                for (var i = 0; i < latestMasters.Count; i++) latestMasters[i].Rank = i + 1; // Assign display rank

                LatestMastersDataGrid.ItemsSource = latestMasters;
                LatestMastersDataGrid.Visibility = Visibility.Visible;
                NoLatestMastersOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                LatestMastersDataGrid.ItemsSource = null;
                LatestMastersDataGrid.Visibility = Visibility.Collapsed;
                NoLatestMastersOverlay.Visibility = Visibility.Visible;
                NoLatestMastersMessage.Text = latestMasters == null
                    ? (string)Application.Current.TryFindResource("RaErrorFailedToLoadLatestMasters") ??
                      "Failed to load latest masters. Please check your RetroAchievements credentials or try again later."
                    : (string)Application.Current.TryFindResource("RaInfoNoLatestMasters") ??
                      "No latest masters found for this game.";
            }

            // Load High Scores (t=0, default)
            var rankings = await _raService.GetGameRankAndScoreAsync(_gameId, _settings.RaUsername, _settings.RaApiKey);
            if (rankings is { Count: > 0 })
            {
                for (var i = 0; i < rankings.Count; i++) rankings[i].Rank = i + 1; // Assign display rank

                HighScoresDataGrid.ItemsSource = rankings;
                HighScoresDataGrid.Visibility = Visibility.Visible;
                NoHighScoresOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                HighScoresDataGrid.ItemsSource = null;
                HighScoresDataGrid.Visibility = Visibility.Collapsed;
                NoHighScoresOverlay.Visibility = Visibility.Visible;
                NoHighScoresMessage.Text = rankings == null
                    ? (string)Application.Current.TryFindResource("RaErrorFailedToLoadHighScores") ??
                      "Failed to load high scores. Please check your RetroAchievements credentials or try again later."
                    : (string)Application.Current.TryFindResource("RaInfoNoHighScores") ??
                      "No high scores found for this game.";
            }

            // Load User Rank and Score (for the current user)
            var userGameRankAndScoreList =
                await _raService.GetUserGameRankAndScoreAsync(_gameId, _settings.RaUsername, _settings.RaApiKey);
            if (userGameRankAndScoreList is { Count: > 0 })
            {
                var userData = userGameRankAndScoreList[0];

                // Apply the requested logic: if UserRank is null or 0, display "Unranked"
                if (userData.UserRank is null or 0)
                    UserRankText.Text = (string)Application.Current.TryFindResource("RaStatusUnranked") ?? "Unranked";
                else
                    UserRankText.Text = userData.UserRank.Value.ToString(CultureInfo.InvariantCulture);

                UserScoreText.Text = userData.TotalScore.ToString("N0", CultureInfo.InvariantCulture); // Format score
                UserLastAwardText.Text = string.IsNullOrWhiteSpace(userData.LastAward)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : userData.LastAward;
                NoUserRankOverlay.Visibility = Visibility.Collapsed; // Ensure hidden if data is present
            }
            else // userGameRankAndScoreList is null or empty
            {
                // If the list is empty, it means the user has no rank for this game.
                UserRankText.Text =
                    (string)Application.Current.TryFindResource("RaStatusUnranked") ?? "Unranked"; // As per request
                UserScoreText.Text = "0"; // Assuming 0 score if unranked
                UserLastAwardText.Text = (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A";
                NoUserRankOverlay.Visibility = Visibility.Visible;
                NoUserRankMessage.Text = userGameRankAndScoreList == null
                    ? (string)Application.Current.TryFindResource("RaErrorFailedToLoadUserRank") ??
                      "Failed to load your rank data. Please check your RetroAchievements credentials or try again later."
                    : (string)Application.Current.TryFindResource("RaInfoNoRankDataForGame") ??
                      "No rank data available for this game.";
            }
        }
        catch (RaUnauthorizedException)
        {
            // Apply unauthorized message to all relevant overlays
            var unauthorizedMessage = (string)Application.Current.TryFindResource("RaErrorUnauthorized") ??
                                      "RetroAchievements credentials invalid. Please check your username and API key in settings.";
            NoUserRankOverlay.Visibility = Visibility.Visible;
            NoUserRankMessage.Text = unauthorizedMessage;
            NoLatestMastersOverlay.Visibility = Visibility.Visible;
            NoLatestMastersMessage.Text = unauthorizedMessage;
            NoHighScoresOverlay.Visibility = Visibility.Visible;
            NoHighScoresMessage.Text = unauthorizedMessage;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Failed to load game ranking tab for game ID: {_gameId}");
            // Show error state
            LatestMastersDataGrid.ItemsSource = null;
            LatestMastersDataGrid.Visibility = Visibility.Collapsed;
            HighScoresDataGrid.ItemsSource = null;
            HighScoresDataGrid.Visibility = Visibility.Collapsed;

            UserRankText.Text =
                (string)Application.Current.TryFindResource("RaStatusError") ??
                "Error"; // Set to error state on exception
            UserScoreText.Text = (string)Application.Current.TryFindResource("RaStatusError") ?? "Error";
            UserLastAwardText.Text = (string)Application.Current.TryFindResource("RaStatusError") ?? "Error";

            NoUserRankOverlay.Visibility = Visibility.Visible;
            NoUserRankMessage.Text = (string)Application.Current.TryFindResource("RaErrorLoadingRankingData") ??
                                     "Error loading ranking data. Please try again.";
            NoLatestMastersOverlay.Visibility = Visibility.Visible;
            NoLatestMastersMessage.Text = (string)Application.Current.TryFindResource("RaErrorLoadingLatestMasters") ??
                                          "Error loading latest masters. Please try again.";
            NoHighScoresOverlay.Visibility = Visibility.Visible;
            NoHighScoresMessage.Text = (string)Application.Current.TryFindResource("RaErrorLoadingHighScores") ??
                                       "Error loading high scores. Please try again.";
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await Task.Yield();
        }
    }

    private async Task LoadUserProfileAsync()
    {
        (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("FetchingUserProfile") ?? "Fetching user profile...");

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            NoProfileOverlay.Visibility = Visibility.Visible;
            NoProfileMainMessage.Text = (string)Application.Current.TryFindResource("RaErrorCredentialsNotSetShort") ??
                                        "RetroAchievements username or API key is not set.";
            NoProfileSubMessage.Text = (string)Application.Current.TryFindResource("RaInfoConfigureCredentials") ??
                                       "Please configure your credentials in the RetroAchievements settings.";
            await Task.Yield();

            return;
        }

        LoadingOverlay.Content = (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            NoProfileOverlay.Visibility = Visibility.Collapsed; // Hide overlay initially
            UserProfileRecentlyPlayed.ItemsSource = null; // Clear previous data
            await Task.Yield();

            // Fetch main user profile
            var userProfile = await _raService.GetUserProfileAsync(_settings.RaUsername, _settings.RaApiKey);

            // Fetch detailed recently played games separately (max 50 games)
            var recentlyPlayedGames =
                await _raService.GetUserRecentlyPlayedGamesAsync(_settings.RaUsername, _settings.RaApiKey, 50);

            if (userProfile != null)
            {
                // Basic profile info
                if (!string.IsNullOrEmpty(userProfile.UserPic))
                {
                    UserProfilePic.Source =
                        await LoadImageFromUrlAsync($"https://retroachievements.org{userProfile.UserPic}");
                }
                else
                {
                    UserProfilePic.Source = null; // Clear image if not available
                }

                UserProfileUser.Text = userProfile.User;
                UserProfileMotto.Text = string.IsNullOrWhiteSpace(userProfile.Motto)
                    ? (string)Application.Current.TryFindResource("RaInfoNoMotto") ?? "No motto set"
                    : userProfile.Motto;

                // Current activity
                UserProfileRichPresence.Text = string.IsNullOrWhiteSpace(userProfile.RichPresenceMsg)
                    ? (string)Application.Current.TryFindResource("RaInfoNotCurrentlyPlaying") ??
                      "Not currently playing"
                    : userProfile.RichPresenceMsg;

                // Statistics
                var rankFormat = (string)Application.Current.TryFindResource("RaInfoRankFormat") ?? "#{0}";
                RankValue.Text = string.IsNullOrWhiteSpace(userProfile.Rank)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : string.Format(CultureInfo.InvariantCulture, rankFormat, userProfile.Rank);
                PointsValue.Text = userProfile.TotalPoints.ToString("N0", CultureInfo.InvariantCulture);
                TruePointsValue.Text = userProfile.TotalTruePoints.ToString("N0", CultureInfo.InvariantCulture);

                // Format MemberSince date
                if (DateTime.TryParse(userProfile.MemberSince, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal, out var memberSinceDate))
                {
                    UserProfileMemberSince.Text = memberSinceDate.ToLocalTime()
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                else
                {
                    UserProfileMemberSince.Text = string.IsNullOrWhiteSpace(userProfile.MemberSince)
                        ? (string)Application.Current.TryFindResource("RaStatusUnknown") ?? "Unknown"
                        : userProfile.MemberSince;
                }

                // Additional details
                UserProfileId.Text = userProfile.Id.ToString(CultureInfo.InvariantCulture);
                var contributionsFormat = (string)Application.Current.TryFindResource("RaInfoContributionsFormat") ??
                                          "{0} contributions ({1:N0} points)";
                UserProfileContributions.Text = string.Format(CultureInfo.InvariantCulture, contributionsFormat,
                    userProfile.ContribCount, userProfile.ContribYield);
                UserProfileSoftcorePoints.Text =
                    userProfile.TotalSoftcorePoints.ToString("N0", CultureInfo.InvariantCulture);
                UserProfilePermissions.Text = GetPermissionDescription(userProfile.Permissions);
                UserProfileStatus.Text = userProfile.Untracked == 1
                    ? (string)Application.Current.TryFindResource("RaStatusUntracked") ?? "Untracked"
                    : (string)Application.Current.TryFindResource("RaStatusTracked") ?? "Tracked";
                UserProfileProfileId.Text = string.IsNullOrWhiteSpace(userProfile.Ulid)
                    ? (string)Application.Current.TryFindResource("RaStatusNotApplicable") ?? "N/A"
                    : userProfile.Ulid;
                UserProfileWallActive.Text = userProfile.UserWallActive
                    ? (string)Application.Current.TryFindResource("RaGenericYes") ?? "Yes"
                    : (string)Application.Current.TryFindResource("RaGenericNo") ?? "No";

                switch (recentlyPlayedGames)
                {
                    // Recently played - use the detailed list from GetUserRecentlyPlayedGamesAsync
                    case { Count: > 0 }:
                        // Ensure full URLs are used (handled in model)
                        UserProfileRecentlyPlayed.ItemsSource = recentlyPlayedGames;
                        break;
                    case null:
                        // If recentlyPlayedGames is null, it indicates an API failure for this specific call
                        _logger.Debug(
                            $"[RA Window] Failed to load recently played games for user {_settings.RaUsername}. API returned null.");
                        UserProfileRecentlyPlayed.ItemsSource = null; // Ensure it's cleared
                        // Optionally, add a message to the ListBox itself or a small text below it.
                        // For now, just clear it and log.
                        break;
                    // recentlyPlayedGames is not null but empty
                    default:
                        UserProfileRecentlyPlayed.ItemsSource = null; // No recently played games
                        break;
                }

                NoProfileOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                // If userProfile is null, something went wrong with the main profile fetch
                NoProfileOverlay.Visibility = Visibility.Visible;
                // Update messages for general API failure
                NoProfileMainMessage.Text =
                    (string)Application.Current.TryFindResource("RaErrorFailedToLoadUserProfile") ??
                    "Failed to load user profile.";
                NoProfileSubMessage.Text = (string)Application.Current.TryFindResource("RaInfoCheckCredentials") ??
                                           "Please check your RetroAchievements credentials or try again later.";
            }
        }
        catch (RaUnauthorizedException)
        {
            NoProfileOverlay.Visibility = Visibility.Visible;
            NoProfileMainMessage.Text = (string)Application.Current.TryFindResource("RaErrorUnauthorized") ??
                                        "RetroAchievements credentials invalid. Please check your username and API key in settings.";
            NoProfileSubMessage.Text = (string)Application.Current.TryFindResource("RaInfoConfigureCredentials") ??
                                       "Please configure your credentials in the RetroAchievements settings.";
        }
        catch (Exception ex)
        {
            NoProfileOverlay.Visibility = Visibility.Visible;
            // Update messages for exception
            NoProfileMainMessage.Text = (string)Application.Current.TryFindResource("RaErrorLoadingUserProfile") ??
                                        "An error occurred while loading user profile.";
            NoProfileSubMessage.Text = (string)Application.Current.TryFindResource("RaInfoCheckConnection") ??
                                       "Please try again or check your internet connection.";
            _logger.Error(ex, $"Failed to load user profile for {_settings.RaUsername}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await Task.Yield();
        }
    }

    private async Task LoadUnlocksByDateAsync()
    {
        (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("FetchingEarnedAchievementsByDate") ??
            "Fetching earned achievements by date...");

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            // Display specific message for missing credentials
            NoUnlocksOverlay.Visibility = Visibility.Visible;
            NoUnlocksMessage.Text = (string)Application.Current.TryFindResource("RaErrorCredentialsNotSet") ??
                                    "RetroAchievements username or API key is not set. Configure in settings.";
            await Task.Yield();

            return;
        }

        LoadingOverlay.Content = (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            FetchUnlocksButton.IsEnabled = false; // Disable button during fetch
            NoUnlocksOverlay.Visibility = Visibility.Collapsed; // Hide overlay initially
            UnlocksDataGrid.ItemsSource = null; // Clear previous data
            TotalUnlocksInRangeText.Text = "0";
            TotalPointsEarnedInRangeText.Text = "0";
            await Task.Yield();

            // Set default dates if not already set
            if (FromDatePicker.SelectedDate == null)
                FromDatePicker.SelectedDate = DateTime.Today.AddMonths(-1); // Default to last month

            if (ToDatePicker.SelectedDate == null) ToDatePicker.SelectedDate = DateTime.Today; // Default to today

            var fromDate = FromDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
            var toDate = ToDatePicker.SelectedDate ?? DateTime.Today;

            var unlocks =
                await _raService.GetAchievementsEarnedBetweenAsync(_settings.RaUsername, _settings.RaApiKey, fromDate,
                    toDate);

            if (unlocks is { Count: > 0 })
            {
                UnlocksDataGrid.ItemsSource = unlocks;
                TotalUnlocksInRangeText.Text = unlocks.Count.ToString("N0", CultureInfo.InvariantCulture);
                TotalPointsEarnedInRangeText.Text =
                    unlocks.Sum(static a => a.Points).ToString("N0", CultureInfo.InvariantCulture);
                NoUnlocksOverlay.Visibility = Visibility.Collapsed; // Hide overlay if data is present
            }
            else
            {
                UnlocksDataGrid.ItemsSource = null;
                TotalUnlocksInRangeText.Text = "0";
                TotalPointsEarnedInRangeText.Text = "0";
                NoUnlocksOverlay.Visibility = Visibility.Visible; // Show overlay if no data
                // If unlocks is null, it indicates an API failure (since credentials were provided)
                NoUnlocksMessage.Text = unlocks == null
                    ? (string)Application.Current.TryFindResource("RaErrorFailedToLoadUnlocks") ??
                      "Failed to load unlocks. Please check your RetroAchievements credentials or try again later."
                    : (string)Application.Current.TryFindResource("RaInfoNoUnlocksFound") ??
                      "No unlocks found for the selected date range.";
            }
        }
        catch (RaUnauthorizedException)
        {
            NoUnlocksOverlay.Visibility = Visibility.Visible;
            NoUnlocksMessage.Text = (string)Application.Current.TryFindResource("RaErrorUnauthorized") ??
                                    "RetroAchievements credentials invalid. Please check your username and API key in settings.";
        }
        catch (Exception ex)
        {
            UnlocksDataGrid.ItemsSource = null;
            TotalUnlocksInRangeText.Text = "0";
            TotalPointsEarnedInRangeText.Text = "0";
            NoUnlocksOverlay.Visibility = Visibility.Visible; // Show overlay on error
            NoUnlocksMessage.Text = (string)Application.Current.TryFindResource("RaErrorLoadingUnlocks") ??
                                    "An error occurred while loading unlocks. Please try again.";
            _logger.Error(ex, $"Failed to load unlocks by date for user {_settings.RaUsername}");
            _logger.Debug($"[RA Window] Failed to load unlocks by date for user {_settings.RaUsername}: {ex.Message}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await Task.Yield();
            FetchUnlocksButton.IsEnabled = true; // Re-enable button
        }
    }

    private async void FetchUnlocksClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate dates before fetching
            var fromDate = FromDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
            var toDate = ToDatePicker.SelectedDate ?? DateTime.Today;

            if (fromDate > toDate)
            {
                await _messageBox.ErrorMessageBoxAsync();
                return; // Exit without fetching
            }

            // Proceed with loading
            await LoadUnlocksByDateAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to fetch unlocks by date");
            _logger.Debug($"[RA Window] Failed to fetch unlocks by date for user {_settings.RaUsername}: {ex.Message}");
        }
    }

    private async void ResetDatesClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            FromDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
            ToDatePicker.SelectedDate = DateTime.Today;
            // Optionally clear the grid and summary to reflect reset
            UnlocksDataGrid.ItemsSource = null;
            TotalUnlocksInRangeText.Text = "0";
            TotalPointsEarnedInRangeText.Text = "0";
            NoUnlocksOverlay.Visibility = Visibility.Visible; // Show overlay when cleared
            NoUnlocksMessage.Text = (string)Application.Current.TryFindResource("RaInfoNoUnlocksFound") ??
                                    "No unlocks found for the selected date range."; // Reset message

            // Notify user
            (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
                (string)Application.Current.TryFindResource("ResettingDatesAndFetchingUnlocks") ??
                "Resetting dates and fetching unlocks...");

            await LoadUnlocksByDateAsync(); // Automatically fetch for the new date range
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Failed to reset date range");
            _logger.Debug($"[RA Window] Failed to reset date range for user {_settings.RaUsername}: {ex.Message}");
        }
    }

    private async Task LoadUserProgressAsync()
    {
        (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent(
            (string)Application.Current.TryFindResource("FetchingUserCompletionProgress") ??
            "Fetching user completion progress...");

        if (string.IsNullOrWhiteSpace(_settings.RaUsername) || string.IsNullOrWhiteSpace(_settings.RaApiKey))
        {
            NoUserProgressOverlay.Visibility = Visibility.Visible;
            NoUserProgressMainMessage.Text =
                (string)Application.Current.TryFindResource("RaErrorCredentialsNotSetShort") ??
                "RetroAchievements username or API key is not set.";
            NoUserProgressSubMessage.Text = (string)Application.Current.TryFindResource("RaInfoConfigureCredentials") ??
                                            "Please configure your credentials in the RetroAchievements settings.";
            await Task.Yield();

            return;
        }

        LoadingOverlay.Content = (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            NoUserProgressOverlay.Visibility = Visibility.Collapsed;
            UserProgressDataGrid.ItemsSource = null; // Clear previous data
            await Task.Yield();

            var userProgressList =
                await _raService.GetUserCompletionProgressAsync(_settings.RaUsername, _settings.RaApiKey);

            if (userProgressList is { Count: > 0 })
            {
                UserProgressDataGrid.ItemsSource = userProgressList;
                NoUserProgressOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                UserProgressDataGrid.ItemsSource = null;
                NoUserProgressOverlay.Visibility = Visibility.Visible;
                // If userProgressList is null, it indicates an API failure (since credentials were provided)
                if (userProgressList == null)
                {
                    NoUserProgressMainMessage.Text =
                        (string)Application.Current.TryFindResource("RaErrorFailedToLoadUserProgress") ??
                        "Failed to load user completion progress.";
                    NoUserProgressSubMessage.Text =
                        (string)Application.Current.TryFindResource("RaInfoCheckCredentials") ??
                        "Please check your RetroAchievements credentials or try again later.";
                }
                else // userProgressList is not null but empty
                {
                    NoUserProgressMainMessage.Text =
                        (string)Application.Current.TryFindResource("RaInfoNoUserProgressFound") ??
                        "No user completion progress found.";
                    NoUserProgressSubMessage.Text =
                        (string)Application.Current.TryFindResource("RaInfoNoUserProgressSubMessage") ??
                        "This could be because you haven't played any games yet.";
                }
            }
        }
        catch (RaUnauthorizedException)
        {
            NoUserProgressOverlay.Visibility = Visibility.Visible;
            NoUserProgressMainMessage.Text = (string)Application.Current.TryFindResource("RaErrorUnauthorized") ??
                                             "RetroAchievements credentials invalid. Please check your username and API key in settings.";
            NoUserProgressSubMessage.Text = (string)Application.Current.TryFindResource("RaInfoConfigureCredentials") ??
                                            "Please configure your credentials in the RetroAchievements settings.";
        }
        catch (Exception ex)
        {
            NoUserProgressOverlay.Visibility = Visibility.Visible;
            NoUserProgressMainMessage.Text =
                (string)Application.Current.TryFindResource("RaErrorLoadingUserProgress") ??
                "An error occurred while loading user completion progress.";
            NoUserProgressSubMessage.Text = (string)Application.Current.TryFindResource("RaInfoCheckConnection") ??
                                            "Please try again or check your internet connection.";
            _logger.Error(ex, $"Failed to load user completion progress for user {_settings.RaUsername}");
            _logger.Debug(
                $"[RA Window] Failed to load user completion progress for user {_settings.RaUsername}: {ex.Message}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await Task.Yield();
        }
    }

    private static async Task<BitmapImage?> LoadImageFromUrlAsync(string url)
    {
        try
        {
            var imageBytes = await SharedHttpClient.GetByteArrayAsync(url);
            await using var ms = new MemoryStream(imageBytes);
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    private static Task PreloadBadgeImagesAsync(IEnumerable<RaAchievement> achievements)
    {
        var tasks = achievements.Select(async achievement =>
        {
            if (!string.IsNullOrEmpty(achievement.BadgeUri))
                achievement.BadgeImage = await LoadImageFromUrlAsync(achievement.BadgeUri);
        });
        return Task.WhenAll(tasks);
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        _playSoundEffects.PlayNotificationSound();
        LoadingOverlay.Visibility = Visibility.Collapsed;

        _logger.Debug("[Emergency] User forced overlay dismissal in RetroAchievements Window");
        (Owner as MainWindow)?.UpdateStatusBarService.UpdateContent("Emergency reset performed.");
    }
}