using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;

/// <summary>
///     Converts CHD files to CUE/BIN format for emulators that do not support CHD natively (e.g., 4DO, Raine).
///     COPY of the WPF ChdToCueStrategy — resource lookups replaced with English fallback strings,
///     IConfiguration moved to the constructor (no App.ServiceProvider dependency).
/// </summary>
public class ChdToCueStrategy : ILaunchStrategy
{
    private readonly IConfiguration _configuration;
    private readonly IDiscConverter _discConverter;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChdToCueStrategy" /> class.
    /// </summary>
    public ChdToCueStrategy(IMessageBoxLibraryService messageBox, ILogger logger, IDiscConverter discConverter,
        IConfiguration configuration)
    {
        _messageBox = messageBox;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discConverter = discConverter;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public int Priority => 25;

    /// <inheritdoc />
    public bool IsMatch(LaunchContext context)
    {
        if (string.IsNullOrEmpty(context.ResolvedFilePath) ||
            string.IsNullOrEmpty(context.EmulatorName))
        {
            return false;
        }

        var isChd = Path.GetExtension(context.ResolvedFilePath).Equals(".chd", StringComparison.OrdinalIgnoreCase);
        if (!isChd) return false;

        var is4Do = context.EmulatorName.Contains("4do", StringComparison.OrdinalIgnoreCase) ||
                    (context.EmulatorManager?.EmulatorLocation?.Contains("4do.exe",
                        StringComparison.OrdinalIgnoreCase) ?? false);

        var isRaine = context.EmulatorName.Contains("Raine", StringComparison.OrdinalIgnoreCase) ||
                      (context.EmulatorManager?.EmulatorLocation?.Contains("raine",
                          StringComparison.OrdinalIgnoreCase) ?? false);

        return is4Do || isRaine;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        const string convertingMsg = "Converting CHD...";

        context.LoadingState?.SetLoadingState(true, convertingMsg);

        string? cuePath;
        try
        {
            cuePath = await _discConverter.ConvertChdToCueBinAsync(context.ResolvedFilePath);
        }
        finally
        {
            // Always end conversion loading state before launching
            context.LoadingState?.SetLoadingState(false);
        }

        if (cuePath == null)
        {
            await _messageBox.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
            return;
        }

        try
        {
            await launcher.LaunchRegularEmulatorAsync(cuePath, context.EmulatorName, context.SystemManagerService!,
                context.EmulatorManager!, context.Parameters, context.WindowContext!, context.LoadingState);
        }
        finally
        {
            // CLEANUP: Delete the temporary .cue and .bin files
            try
            {
                var binPath = Path.ChangeExtension(cuePath, ".bin");
                if (File.Exists(cuePath)) File.Delete(cuePath);
                if (File.Exists(binPath)) File.Delete(binPath);
                _logger.Debug($"Cleaned up temporary CHD conversion files: {cuePath}");
            }
            catch (Exception ex)
            {
                _logger.Debug($"Failed to cleanup CHD temp files: {ex.Message}");
            }
        }
    }
}