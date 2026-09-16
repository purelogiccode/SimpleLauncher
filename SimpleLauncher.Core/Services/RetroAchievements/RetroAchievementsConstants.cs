namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     Constants shared by the RetroAchievements hashing services.
/// </summary>
public static class RetroAchievementsConstants
{
    /// <summary>
    ///     The largest console id the RetroAchievementsSharp engine can hash
    ///     (<c>RC_CONSOLE_MAX</c> in the rcheevos port). Consoles above this value
    ///     (e.g. the "unsupported" pseudo-system, id 102) have no hash logic.
    /// </summary>
    public const int MaxConsoleId = 90;

    /// <summary>
    ///     The arcade console id (<c>RC_CONSOLE_ARCADE</c> in the rcheevos port). Arcade-based
    ///     systems (MAME, CPS, Neo Geo, Naomi) are hashed by file name instead of content.
    /// </summary>
    public const int ArcadeConsoleId = 27;
}