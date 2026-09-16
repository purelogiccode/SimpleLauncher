using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;

/// <summary>
///     Converts PSP .pbp files to CUE/BIN format for emulators that do not support PBP natively (e.g., Mednafen).
///     COPY of the WPF PbpToCueStrategy — resource lookups replaced with English fallback strings,
///     IConfiguration moved to the constructor (no App.ServiceProvider dependency).
/// </summary>
public class PbpToCueStrategy : ILaunchStrategy
{
    private readonly IConfiguration _configuration;
    private readonly IDiscConverter _discConverter;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PbpToCueStrategy" /> class.
    /// </summary>
    public PbpToCueStrategy(IMessageBoxLibraryService messageBox, ILogger logger, IDiscConverter discConverter,
        IConfiguration configuration)
    {
        _messageBox = messageBox;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discConverter = discConverter;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public int Priority => 15; // Higher priority than Default (999) but lower than CHD (10) to handle specific case

    /// <inheritdoc />
    public bool IsMatch(LaunchContext context)
    {
        if (string.IsNullOrEmpty(context.ResolvedFilePath) ||
            string.IsNullOrEmpty(context.EmulatorName))
        {
            return false;
        }

        var isPbp = Path.GetExtension(context.ResolvedFilePath).Equals(".pbp", StringComparison.OrdinalIgnoreCase);
        if (!isPbp) return false;

        // Check if emulator is Mednafen (which doesn't support PBP files)
        var isMednafen = context.EmulatorName.Contains("Mednafen", StringComparison.OrdinalIgnoreCase) ||
                         (context.EmulatorManager?.EmulatorLocation?.Contains("mednafen",
                             StringComparison.OrdinalIgnoreCase) ?? false);

        {
            return isMednafen;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        const string convertingMsg = "Converting PBP to CUE/BIN...";

        context.LoadingState?.SetLoadingState(true, convertingMsg);

        string? cuePath;
        try
        {
            cuePath = await _discConverter.ConvertPbpToCueBinAsync(context.ResolvedFilePath);
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
                // Delete the main .cue and .bin files
                if (File.Exists(cuePath)) File.Delete(cuePath);
                var binPath = Path.ChangeExtension(cuePath, ".bin");
                if (File.Exists(binPath)) File.Delete(binPath);

                _logger.Debug(
                    $"Cleaned up temporary PBP conversion files: {Path.GetFileNameWithoutExtension(cuePath)}");
            }
            catch (Exception ex)
            {
                _logger.Debug($"Failed to cleanup PBP temp files: {ex.Message}");
            }
        }
    }
}