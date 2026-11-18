namespace SimpleLauncher.Core.Services;

/// <summary>
///     Detects how the application was packaged so read-only installs, AppImage
///     self-updates and store-managed (Flatpak) updates can be handled correctly.
/// </summary>
/// <remarks>
///     Portable ZIP/tarball installs live in a user-writable folder and keep using the
///     existing in-app updater. AppImage runs from a read-only squashfs mount and updates
///     by replacing the outer <c>.AppImage</c> file. Flatpak runs from a read-only
///     <c>/app</c> mount and is updated by the software center, never by the app.
/// </remarks>
public static class PackagingEnvironment
{
    private const string FlatpakInfoPath = "/.flatpak-info";

    /// <summary>
    ///     Gets a value indicating whether the application is running from an AppImage.
    /// </summary>
    public static bool IsAppImage => OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(AppImagePath);

    /// <summary>
    ///     Gets the path of the outer AppImage file, or <see langword="null" /> when the
    ///     application is not running from an AppImage.
    /// </summary>
    public static string? AppImagePath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("APPIMAGE");
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the application is running inside a Flatpak sandbox.
    /// </summary>
    public static bool IsFlatpak =>
        OperatingSystem.IsLinux() &&
        (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID")) ||
         File.Exists(FlatpakInfoPath));

    /// <summary>
    ///     Gets a value indicating whether the install directory is a read-only package
    ///     mount (AppImage or Flatpak), where the regular portable update flow cannot write.
    /// </summary>
    public static bool HasReadOnlyInstall => IsAppImage || IsFlatpak;

    /// <summary>
    ///     Gets a value indicating whether application updates are managed by an external
    ///     package manager instead of the in-app updater.
    /// </summary>
    public static bool UpdatesManagedExternally => IsFlatpak;
}
