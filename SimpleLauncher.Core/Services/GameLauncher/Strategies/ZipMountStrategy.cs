using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;


namespace SimpleLauncher.Core.Services.GameLauncher.Strategies;

/// <summary>
///     Mounts ZIP/7Z/RAR archives as virtual drives for RPCS3 (EBOOT.BIN), ScummVM, and XBLA games.
/// </summary>
public class ZipMountStrategy : ILaunchStrategy
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IMountZipFiles _mountZipFiles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ZipMountStrategy" /> class.
    /// </summary>
    public ZipMountStrategy(IConfiguration configuration, ILogger logErrors, IMessageBoxLibraryService messageBox,
        IMountZipFiles mountZipFiles)
    {
        _configuration = configuration;
        _logger = logErrors;
        _messageBox = messageBox;
        _mountZipFiles = mountZipFiles;
    }

    /// <inheritdoc />
    public int Priority => 30;

    /// <inheritdoc />
    public bool IsMatch(LaunchContext context)
    {
        if (string.IsNullOrEmpty(context.ResolvedFilePath) ||
            string.IsNullOrEmpty(context.EmulatorName) ||
            string.IsNullOrEmpty(context.SystemName))
        {
            return false;
        }

        var extension = Path.GetExtension(context.ResolvedFilePath);
        var isArchive = extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".rar", StringComparison.OrdinalIgnoreCase);

        if (!isArchive) return false;

        return context.EmulatorName.Contains("RPCS3", StringComparison.OrdinalIgnoreCase) ||
               context.SystemName.Contains("Scumm", StringComparison.OrdinalIgnoreCase) ||
               context.SystemName.Contains("xbla", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        var log = PathHelper.ResolveLogFilePath(_configuration);

        // Match the case-insensitive tests in IsMatch: "rpcs3" must take the RPCS3 branch.
        if (context.EmulatorName.Contains("RPCS3", StringComparison.OrdinalIgnoreCase))
        {
            return _mountZipFiles.MountZipFileAndLoadEbootBinAsync(context.ResolvedFilePath, context.SystemName,
                context.EmulatorName, context.SystemManagerService!, context.EmulatorManager!, context.Parameters,
                context.WindowContext!, log, launcher, _logger, _messageBox);
        }

        if (context.SystemName.Contains("Scumm", StringComparison.OrdinalIgnoreCase))
        {
            return _mountZipFiles.MountZipFileAndLoadWithScummVmAsync(context.ResolvedFilePath, context.SystemName,
                context.EmulatorName, context.SystemManagerService!, context.EmulatorManager!, context.Parameters, log,
                _logger, _messageBox);
        }

        return _mountZipFiles.MountZipFileAndSearchForFileToLoadAsync(context.ResolvedFilePath, context.SystemName,
            context.EmulatorName, context.SystemManagerService!, context.EmulatorManager!, context.Parameters,
            context.WindowContext!, log, launcher, _logger, _messageBox);
    }
}