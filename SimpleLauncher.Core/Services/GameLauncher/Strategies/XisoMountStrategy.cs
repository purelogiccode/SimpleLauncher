using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;


namespace SimpleLauncher.Core.Services.GameLauncher.Strategies;

/// <summary>
///     Mounts Xbox images (XISO <c>.iso</c>/<c>.xiso</c>, CISO <c>.cso</c> and ZArchive
///     <c>.zar</c>) as virtual drives and launches them with Cxbx-Reloaded. The CHD variant
///     is handled by the app-level CHD mount strategy, which prefers SimpleXisoDrive and can
///     fall back to CHDMounter when the platform mount backend is unavailable.
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

        // Match the launcher's Cxbx detection: the display name or the executable path may
        // identify the emulator (Cxbx-Reloaded cannot read a raw XISO/CISO/ZArchive, it needs
        // the mounted default.xbe).
        var isCxbx = context.EmulatorName.Contains("Cxbx", StringComparison.OrdinalIgnoreCase) ||
                     (context.EmulatorManager?.EmulatorLocation?.Contains("cxbx",
                         StringComparison.OrdinalIgnoreCase) ?? false);
        if (!isCxbx) return false;

        // SimpleXisoDrive presents the same virtual drive for every Xbox container (the CISO
        // and ZArchive trees are decoded/synthesized on demand), so Cxbx-Reloaded can launch
        // the mounted default.xbe for all of them.
        var extension = Path.GetExtension(context.ResolvedFilePath);
        return extension.Equals(".iso", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xiso", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cso", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".zar", StringComparison.OrdinalIgnoreCase);
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