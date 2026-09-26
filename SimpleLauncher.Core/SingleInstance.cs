using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Core;

/// <summary>
///     Names and helpers for the process-wide single-instance primitives shared by the WPF and
///     Avalonia applications. Both apps ship next to each other in the unified bundle and must
///     never run simultaneously, so they contend for the same named mutex and lock file; the
///     named event lets a second launch bring the running instance to the foreground before
///     exiting.
/// </summary>
public static class SingleInstance
{
    /// <summary>
    ///     Named mutex that enforces a single running instance across both apps.
    /// </summary>
    public const string MutexName =
        "SimpleLauncher_SingleInstanceMutex_A8E2B9C1-F5D7-4E0A-8B3C-6D1E9F0A7B4C";

    /// <summary>
    ///     Named event (Windows) used by a second launch to signal the running
    ///     instance to restore and focus its main window.
    /// </summary>
    public const string EventName =
        "SimpleLauncher_SingleInstanceEvent_A8E2B9C1-F5D7-4E0A-8B3C-6D1E9F0A7B4C";

    /// <summary>
    ///     Name of the per-user lock file that enforces a single running instance on all
    ///     platforms. On Unix, .NET named mutexes are scoped to the login session, so an
    ///     instance started from another session (or detached with <c>setsid</c>) would not
    ///     see the first; a lock file opened with <see cref="FileShare.None" /> is enforced
    ///     across processes and sessions on both Windows and Unix.
    /// </summary>
    public const string LockFileName = "single-instance.lock";

    /// <summary>
    ///     Resolves the per-user lock file path shared by the WPF and Avalonia apps.
    /// </summary>
    /// <returns>An absolute path to the single-instance lock file.</returns>
    public static string GetLockFilePath()
    {
        return Path.Combine(AppDataPaths.SimpleLauncherDataFolder, LockFileName);
    }

    /// <summary>
    ///     Tries to acquire the process-lifetime single-instance lock. The returned stream
    ///     must be kept alive for as long as the application runs; <c>null</c> means another
    ///     instance already holds the lock.
    /// </summary>
    /// <param name="lockFilePath">The lock file path (see <see cref="GetLockFilePath" />).</param>
    /// <returns>An open, exclusively locked stream, or <c>null</c> when another instance owns it.</returns>
    public static FileStream? TryAcquireLockFile(string lockFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(lockFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            return new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
