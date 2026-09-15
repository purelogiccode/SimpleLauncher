namespace SimpleLauncher.Core;

/// <summary>
///     Names of the process-wide single-instance primitives shared by the WPF and
///     Avalonia applications. Both apps ship next to each other in the unified bundle
///     and must never run simultaneously, so they contend for the same named mutex;
///     the named event lets a second launch bring the running instance to the
///     foreground before exiting.
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
}
