namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Calculates RetroAchievements hashes for game files. All hash computation is
///     delegated to the bundled RetroAchievementsSharp CLI tool
///     (<c>tools\RetroAchievementsSharp\&lt;rid&gt;\RetroAchievementsSharp.Cli</c>, with the
///     <c>.exe</c> suffix on Windows), which is a 1:1 port of the rcheevos hashing engine
///     and produces the exact same hashes as RAHasher.
/// </summary>
public interface IRetroAchievementsFileHasher
{
    /// <summary>
    ///     Calculates the RetroAchievements hash for a single game file using the console ID of the given system.
    /// </summary>
    /// <param name="filePath">The full path to the game file.</param>
    /// <param name="systemName">The RetroAchievements system name (resolved to a console ID internally).</param>
    /// <param name="cancellationToken">A cancellation token that terminates the hash process.</param>
    /// <returns>The 32-character lowercase hex hash, or null if the file could not be hashed.</returns>
    Task<string?> CalculateHashAsync(string filePath, string systemName, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calculates the RetroAchievements hashes for a set of game files of the same system
    ///     through the CLI tool's positional interface (multiple files per invocation, one
    ///     hash per file parsed from stdout), keyed by full file path.
    /// </summary>
    /// <param name="filePaths">The full paths of the game files to hash.</param>
    /// <param name="systemName">The RetroAchievements system name (resolved to a console ID internally).</param>
    /// <param name="cancellationToken">A cancellation token that terminates the hash process.</param>
    /// <returns>A dictionary of full file path → 32-character lowercase hex hash for every file that could be hashed.</returns>
    Task<IReadOnlyDictionary<string, string>> CalculateHashesAsync(
        IReadOnlyCollection<string> filePaths,
        string systemName,
        CancellationToken cancellationToken = default);
}