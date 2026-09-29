using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;


namespace SimpleLauncher.Core.Services.GameLauncher.Strategies;

/// <summary>
///     Mounts compressed Xbox images (.cso/.zar) with SimpleXisoDrive's virtual image.iso option
///     and launches xemu with the mounted image. This mount logic is Windows-only: the mount
///     service reports "not supported on this platform" elsewhere.
/// </summary>
public class XemuMountStrategy : ILaunchStrategy
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IMountXisoFiles _mountXisoFiles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XemuMountStrategy" /> class.
    /// </summary>
    public XemuMountStrategy(ILogger logErrors, IMessageBoxLibraryService messageBox,
        IMountXisoFiles mountXisoFiles)
    {
        _logger = logErrors;
        _messageBox = messageBox;
        _mountXisoFiles = mountXisoFiles;
    }

    /// <inheritdoc />
    public int Priority => 20;

    /// <inheritdoc />
    public bool IsMatch(LaunchContext context)
    {
        if (string.IsNullOrEmpty(context.ResolvedFilePath) ||
            string.IsNullOrEmpty(context.EmulatorName))
        {
            return false;
        }

        var extension = Path.GetExtension(context.ResolvedFilePath);
        var isCompressedXboxImage = extension.Equals(".cso", StringComparison.OrdinalIgnoreCase) ||
                                    extension.Equals(".zar", StringComparison.OrdinalIgnoreCase);

        if (!isCompressedXboxImage) return false;

        // Match the launcher's xemu detection: the display name or the executable path may
        // identify the emulator.
        return context.EmulatorName.Contains("xemu", StringComparison.OrdinalIgnoreCase) ||
               (context.EmulatorManager?.EmulatorLocation?.Contains("xemu", StringComparison.OrdinalIgnoreCase) ??
                false);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        await using var mountedDrive = await _mountXisoFiles.MountImageIsoAsync(context.ResolvedFilePath,
            _logger, _messageBox);
        if (mountedDrive.IsMounted)
        {
            await launcher.LaunchRegularEmulatorAsync(mountedDrive.MountedPath, context.EmulatorName,
                context.SystemManagerService!, context.EmulatorManager!, context.Parameters, context.WindowContext!,
                context.LoadingState, context.ResolvedFilePath);
        }
    }
}
