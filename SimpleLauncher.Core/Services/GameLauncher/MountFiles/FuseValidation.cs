using System.Diagnostics;

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

    /// <summary>
    ///     Releases a FUSE mount. Needed when the CHDMounter process was killed instead of
    ///     shutting down cleanly: the kernel keeps the mount registered as a disconnected
    ///     "Transport endpoint is not connected" stub until it is explicitly unmounted, which
    ///     would otherwise leave the mount-point directory behind. Tries fusermount3 first, then
    ///     umount; failures are logged at Debug level and ignored.
    /// </summary>
    /// <param name="mountPoint">The mount-point directory passed to CHDMounter.</param>
    /// <param name="logger">The logger used for Debug-level diagnostics.</param>
    public static void TryUnmount(string mountPoint, ILogger logger)
    {
        var candidates = new[]
        {
            ("fusermount3", $"-u \"{mountPoint}\""),
            ("umount", $"\"{mountPoint}\"")
        };

        foreach (var (command, arguments) in candidates)
        {
            try
            {
                using var unmount = Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                });
                unmount?.WaitForExit(5000);
                if (unmount is { ExitCode: 0 })
                {
                    logger.Debug($"[FuseValidation] Unmounted '{mountPoint}' with {command}.");
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"[FuseValidation] {command} could not unmount '{mountPoint}': {ex.Message}");
            }
        }
    }
}
