namespace SimpleLauncher.Avalonia.Models;

/// <summary>
///     Represents a RetroAchievements achievement with unlock status, metadata, and display helpers.
/// </summary>
internal class RaAchievement
{
    /// <summary>
    ///     Gets or sets the unique identifier of the achievement.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the title of the achievement.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    ///     Gets or sets the description of the achievement.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     Gets or sets the points awarded for earning the achievement.
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the achievement badge image.
    /// </summary>
    public string BadgeUri { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether the achievement has been unlocked by the user.
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>
    ///     Gets or sets the date the achievement was unlocked.
    /// </summary>
    public DateTime? DateUnlocked { get; set; }

    /// <summary>
    ///     Gets or sets whether the achievement was unlocked in hardcore mode.
    /// </summary>
    public bool UnlockedInHardcore { get; set; }

    /// <summary>
    ///     Gets or sets the display order of the achievement.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    ///     Gets or sets the number of users who have earned the achievement.
    /// </summary>
    public int NumAwarded { get; set; }

    /// <summary>
    ///     Gets or sets the number of users who earned the achievement in hardcore mode.
    /// </summary>
    public int NumAwardedHardcore { get; set; }

    /// <summary>
    ///     Gets or sets the author of the achievement.
    /// </summary>
    public string Author { get; set; } = "";

    /// <summary>
    ///     Gets or sets the ULID identifier of the achievement author.
    /// </summary>
    public string AuthorUlid { get; set; } = "";

    /// <summary>
    ///     Gets or sets the date the achievement was last modified.
    /// </summary>
    public string DateModified { get; set; } = "";

    /// <summary>
    ///     Gets or sets the date the achievement was created.
    /// </summary>
    public string DateCreated { get; set; } = "";

    /// <summary>
    ///     Gets or sets the badge name of the achievement.
    /// </summary>
    public string BadgeName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the type of the achievement.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    ///     Gets or sets the date the achievement was earned in hardcore mode.
    /// </summary>
    public DateTime? DateEarnedHardcore { get; set; }

    /// <summary>
    ///     Gets or sets the date the achievement was earned.
    /// </summary>
    public DateTime? DateEarned { get; set; }

    /// <summary>
    ///     Gets or sets the weighted point value of the achievement.
    /// </summary>
    public int? TrueRatio { get; set; }

    /// <summary>
    ///     Gets a formatted display string for the unlock date, including a trophy icon for hardcore unlocks.
    /// </summary>
    public string DateUnlockedDisplay
    {
        get
        {
            if (!IsUnlocked) return "Locked";
            if (DateUnlocked == null) return "N/A";

            return UnlockedInHardcore ? $"🏆 {DateUnlocked.Value:yyyy-MM-dd}" : $"{DateUnlocked.Value:yyyy-MM-dd}";
        }
    }

    /// <summary>
    ///     Gets a display string indicating whether the achievement was earned in hardcore, casual, or not earned.
    /// </summary>
    public string ModeDisplay => UnlockedInHardcore ? "Hardcore" : IsUnlocked ? "Casual" : "Not Earned";

    /// <summary>
    ///     Gets the author name, defaulting to "Unknown" if empty.
    /// </summary>
    public string AuthorDisplay => string.IsNullOrWhiteSpace(Author) ? "Unknown" : Author;

    /// <summary>
    ///     Gets a formatted display string showing the percentage of hardcore earners.
    /// </summary>
    public string RarityDisplay => NumAwarded > 0 && NumAwardedHardcore > 0
        ? $"{(double)NumAwardedHardcore / NumAwarded * 100:F1}% hardcore"
        : "N/A";
}