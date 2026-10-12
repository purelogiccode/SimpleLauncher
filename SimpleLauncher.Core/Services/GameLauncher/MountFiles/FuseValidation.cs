namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Validates whether FUSE is available for the Linux CHDMounter build. The FUSE device node
///     only exists when the kernel module is loaded and the user can access it, so it doubles as
///     a cheap availability probe before launching the tool.
/// </summary>
public static class FuseValidation
{
    private const string FuseDevicePath = "/dev/fuse";

    /// <summary>
    ///     Checks whether the FUSE device node exists (Linux only). macOS uses macFUSE and is not
    ///     covered because the launcher ships no macOS payload.
    /// </summary>
    public static bool IsFuseAvailable() => OperatingSystem.IsLinux() && File.Exists(FuseDevicePath);
}
