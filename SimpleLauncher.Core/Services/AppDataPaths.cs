namespace SimpleLauncher.Core.Services;

/// <summary>
///     Resolves the SimpleLauncher application-data folder robustly across platforms.
/// </summary>
/// <remarks>
///     On Linux, <see cref="Environment.SpecialFolder.LocalApplicationData" /> can return an
///     empty or relative value when XDG_DATA_HOME is unset, which would silently relocate
///     logs/data into the process working directory. This helper falls back to the XDG
///     default (<c>~/.local/share</c>) and, as a last resort, the app base directory.
/// </remarks>
public static class AppDataPaths
{
    /// <summary>
    ///     Gets the SimpleLauncher data folder (logs, window bounds, data files).
    /// </summary>
    public static string SimpleLauncherDataFolder => GetSimpleLauncherDataFolder();

    /// <summary>
    ///     Resolves the SimpleLauncher data folder.
    /// </summary>
    /// <returns>An absolute path to the SimpleLauncher data folder.</returns>
    public static string GetSimpleLauncherDataFolder()
    {
        return Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            OperatingSystem.IsWindows());
    }

    /// <summary>
    ///     Pure resolution logic (separated for testability).
    /// </summary>
    /// <param name="localAppData">The LocalApplicationData folder, possibly empty/relative on Linux.</param>
    /// <param name="userProfile">The user profile folder.</param>
    /// <param name="isWindows">Whether the current platform is Windows.</param>
    /// <returns>An absolute path to the SimpleLauncher data folder.</returns>
    internal static string Resolve(string? localAppData, string? userProfile, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(localAppData) || !Path.IsPathRooted(localAppData))
        {
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                localAppData = isWindows
                    ? Path.Combine(userProfile, "AppData", "Local")
                    : Path.Combine(userProfile, ".local", "share");
            }
        }

        if (string.IsNullOrWhiteSpace(localAppData) || !Path.IsPathRooted(localAppData))
            localAppData = AppDomain.CurrentDomain.BaseDirectory; // last resort

        return Path.Combine(localAppData, "SimpleLauncher");
    }
}