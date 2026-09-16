using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Scans game paths in the background and calculates RetroAchievements hashes
///     for every game file, persisting the results through <see cref="IRetroAchievementsHashStore" />.
///     Only one scan can run at a time; concurrent requests are rejected.
/// </summary>
public interface IRetroAchievementsHashScanner
{
    /// <summary>
    ///     Gets a value indicating whether a hash scan is currently running.
    /// </summary>
    bool IsScanning { get; }

    /// <summary>
    ///     Determines whether the given system can be hashed for RetroAchievements.
    ///     Delegates to the system matcher so the background scan and the hasher tool UI
    ///     always agree on which systems are supported.
    /// </summary>
    /// <param name="systemName">The name of the system.</param>
    /// <returns>True if the system is supported for RetroAchievements hashing; otherwise, false.</returns>
    bool IsSystemScannable(string systemName);

    /// <summary>
    ///     Determines whether an existing hash scan for the given system was produced by the
    ///     current hash logic (same <see cref="RaSystemHashes.HashVersion" />).
    /// </summary>
    /// <param name="systemName">The name of the system.</param>
    /// <returns>True if the stored scan is up to date; false if missing or stale.</returns>
    bool IsScanUpToDate(string systemName);

    /// <summary>
    ///     Scans the game folders of a single system and persists the calculated hashes.
    /// </summary>
    /// <param name="systemName">The name of the system to scan.</param>
    /// <param name="systemFolders">The list of configured system folders (relative or absolute).</param>
    /// <param name="fileFormatsToSearch">The list of file extensions to search for.</param>
    /// <param name="fileFormatsToLaunch">The list of file extensions to look for inside compressed files before hashing.</param>
    /// <param name="disableRecursiveSearch">True to skip subfolders during the scan.</param>
    /// <param name="groupByFolder">True if the system groups game entries by folder.</param>
    /// <param name="onCompleted">Optional callback invoked (on a background thread) when the scan completes.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="force">
    ///     True to re-hash every file even when the game count and hash logic are unchanged
    ///     (used by the explicit "Rescan RetroAchievements" command).
    /// </param>
    /// <returns>True if the scan started; false if a scan is already running or the system is not scannable.</returns>
    Task<bool> ScanSystemAsync(
        string systemName,
        IList<string> systemFolders,
        IList<string> fileFormatsToSearch,
        IList<string> fileFormatsToLaunch,
        bool disableRecursiveSearch,
        bool groupByFolder,
        Action<string>? onCompleted = null,
        CancellationToken cancellationToken = default,
        bool force = false);

    /// <summary>
    ///     Scans the game folders of multiple systems sequentially and persists the calculated hashes.
    /// </summary>
    /// <param name="targets">The systems to scan, in order.</param>
    /// <param name="onCompleted">Optional callback invoked (on a background thread) after each system completes.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="force">True to re-hash every file even when nothing changed.</param>
    /// <returns>True if the scan started; false if a scan is already running.</returns>
    Task<bool> ScanAllSystemsAsync(
        IEnumerable<RaHashScanTarget> targets,
        Action<string>? onCompleted = null,
        CancellationToken cancellationToken = default,
        bool force = false);

    /// <summary>
    ///     Cancels the running hash scan (if any) and waits up to <paramref name="timeout" />
    ///     for it to finish, killing any CLI hashing processes along the way. Intended to be
    ///     called during application shutdown so no scan threads or CLI processes are orphaned.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the scan to finish before continuing anyway.</param>
    /// <returns>A task representing the asynchronous cancel-and-wait operation.</returns>
    Task CancelScanAndWaitAsync(TimeSpan timeout);
}