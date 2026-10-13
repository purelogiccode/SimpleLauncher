using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;


namespace SimpleLauncher.Core.Services.GameLauncher.Strategies;

/// <summary>
///     Mounts original Xbox ISO images as virtual drives and launches them with Cxbx-Reloaded.
/// </summary>
public class XisoMountStrategy : ILaunchStrategy
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IMountXisoFiles _mountXisoFiles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XisoMountStrategy" /> class.
    /// </summary>
    public XisoMountStrategy(ILogger logErrors, IMessageBoxLibraryService messageBox,
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

        return context.EmulatorName.Contains("Cxbx", StringComparison.OrdinalIgnoreCase) &&
               Path.GetExtension(context.ResolvedFilePath).Equals(".iso", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        await using var mountedDrive = await _mountXisoFiles.MountAsync(context.ResolvedFilePath,
            _logger, _messageBox);
        if (mountedDrive.IsMounted)
        {
            await launcher.LaunchRegularEmulatorAsync(mountedDrive.MountedPath, context.EmulatorName,
                context.SystemManagerService!, context.EmulatorManager!, context.Parameters, context.WindowContext!,
                context.LoadingState, context.ResolvedFilePath);
            return;
        }

        // The Windows mount path reports failures itself; the Linux FUSE path logs quietly, so
        // surface the error dialog here.
        if (!OperatingSystem.IsWindows())
        {
            await _messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
        }
    }
}