namespace SimpleLauncher.Core.Services;

/// <summary>
///     Detects how the application was packaged so read-only installs, AppImage
///     self-updates and store-managed (Flatpak) updates can be handled correctly.
/// </summary>
/// <remarks>
///     Portable ZIP/tarball installs live in a user-writable folder and keep using the
///     existing in-app updater. AppImage runs from a read-only squashfs mount and updates
///     by replacing the outer <c>.AppImage</c> file. Flatpak runs from a read-only
///     <c>/app</c> mount and is updated by the software center, never by the app. The
///     macOS release is an unsigned DMG whose <c>.app</c> is replaced manually with the
///     next DMG, so macOS also has no in-app updater.
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
    ///     Gets a value indicating whether the application is running from a macOS
    ///     <c>.app</c> bundle (the DMG layout), whose payload is replaced by installing
    ///     the next DMG instead of the in-app updater.
    /// </summary>
    public static bool IsMacOsAppBundle =>
        OperatingSystem.IsMacOS() &&
        AppContext.BaseDirectory.Contains(".app/Contents/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets a value indicating whether the install directory is a read-only package
    ///     (AppImage, Flatpak or a macOS <c>.app</c> bundle), where the regular portable
    ///     update flow cannot write.
    /// </summary>
    public static bool HasReadOnlyInstall => IsAppImage || IsFlatpak || IsMacOsAppBundle;

    /// <summary>
    ///     Gets a value indicating whether application updates are managed outside the app
    ///     (Flatpak's software center or a freshly downloaded macOS DMG) instead of the
    ///     in-app updater.
    /// </summary>
    public static bool UpdatesManagedExternally => IsFlatpak || IsMacOsAppBundle;
}
