namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Matches local system names to official RetroAchievements system names and console IDs.
/// </summary>
public interface IRetroAchievementsSystemMatcher
{
    /// <summary>
    ///     Finds the best matching RetroAchievements system name using fuzzy matching.
    /// </summary>
    /// <param name="inputSystemName">The system name to match.</param>
    /// <returns>The normalized RetroAchievements system name, or the original if no match found.</returns>
    string GetBestMatchSystemName(string inputSystemName);

    /// <summary>
    ///     Checks whether the given system name is an official RetroAchievements system name (a key in the SystemMappings
    ///     dictionary).
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system name is an official key; otherwise, false.</returns>
    bool IsOfficialSystemName(string systemName);

    /// <summary>
    ///     Gets a sorted list of all official RetroAchievements system names supported by the matcher.
    /// </summary>
    /// <returns>A sorted list of system name strings.</returns>
    IList<string> GetSupportedSystemNames();

    /// <summary>
    ///     Gets the RetroAchievements Console ID for a given system name.
    /// </summary>
    /// <param name="inputSystemName">The system name to look up.</param>
    /// <returns>The console ID, or -1 if not found.</returns>
    int GetSystemId(string inputSystemName);

    /// <summary>
    ///     Attempts to find an exact match for the input system name among all known aliases.
    /// </summary>
    /// <param name="inputSystemName">The system name to match.</param>
    /// <returns>The official system name key if an exact alias match is found; otherwise, null.</returns>
    string? GetExactAliasMatch(string inputSystemName);

    /// <summary>
    ///     Checks whether the given system name exists in the system mappings.
    ///     This checks both the dictionary keys and all aliases for each system.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system exists in SystemMappings; otherwise, false.</returns>
    bool IsSystemInMappings(string systemName);

    /// <summary>
    ///     Determines whether the system can be hashed for RetroAchievements by the bundled
    ///     CLI tool: its resolved console has a usable id and its hash logic is known.
    ///     This is the single source of truth for both the hasher tool UI and the
    ///     background hash scanner.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system is supported for RetroAchievements hashing; otherwise, false.</returns>
    bool IsSystemSupportedForHashing(string systemName);
}