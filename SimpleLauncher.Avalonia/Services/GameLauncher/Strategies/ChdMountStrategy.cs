using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;

/// <summary>
///     Strategy for mounting CHD files and launching them with compatible emulators.
///     Direct COPY of the WPF ChdMountStrategy (namespace only).
/// </summary>
public class ChdMountStrategy : ILaunchStrategy
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IMountChdFiles _mountChdFiles;
    private bool _cDiEmu;

    private bool _is4Do;
    private bool _isBlastem;
    private bool _isCxbxReloaded;
    private bool _isFinalBurnAlpha;
    private bool _isFinalBurnNeo;
    private bool _isGenesisPlusGx;
    private bool _isGens;
    private bool _isKegaFusion;
    private bool _isMednafen;
    private bool _isMesen;
    private bool _isNebula;
    private bool _isPcsxRedux;
    private bool _isPicoDrive;
    private bool _isRaine;
    private bool _isRpcs3;
    private bool _isTsugaru;
    private bool _isXemu;
    private bool _isXenia;
    private bool _isYabause;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChdMountStrategy" /> class with the specified dependencies.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    /// <param name="mountChdFiles">The CHD mounting service.</param>
    /// <param name="logger">The logger instance.</param>
    public ChdMountStrategy(IConfiguration configuration, IMessageBoxLibraryService messageBox,
        IMountChdFiles mountChdFiles, ILogger logger)
    {
        _configuration = configuration;
        _messageBox = messageBox;
        _mountChdFiles = mountChdFiles;
        _logger = logger;
    }

    /// <inheritdoc />
    public int Priority => 10;

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

        var isRetroArch = context.EmulatorName.Contains("RetroArch", StringComparison.OrdinalIgnoreCase) ||
                          (context.EmulatorManager?.EmulatorLocation?.Contains("retroarch.exe",
                              StringComparison.OrdinalIgnoreCase) ?? false);
        if (isRetroArch) return false; // we do not mount chd if emulator is RetroArch

        if (DosBoxLaunchStrategy.IsDosBoxEmulator(context))
            return false; // let DosBoxLaunchStrategy handle CHD for DOSBox

        ResolveEmulatorFlags(context);
        return _isGenesisPlusGx ||
               _is4Do ||
               _isBlastem ||
               _cDiEmu ||
               _isCxbxReloaded ||
               _isFinalBurnAlpha ||
               _isFinalBurnNeo ||
               _isGens ||
               _isKegaFusion ||
               _isMednafen ||
               _isMesen ||
               _isNebula ||
               _isPcsxRedux ||
               _isPicoDrive ||
               _isRaine ||
               _isRpcs3 ||
               _isTsugaru ||
               _isXemu ||
               _isXenia ||
               _isYabause;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        string? gameFilePath;
        ResolveEmulatorFlags(context);

        var logPath = PathHelper.ResolveLogFilePath(_configuration);

        // Get the console alias for CHDMounter based on system and emulator
        var consoleAlias = _mountChdFiles.GetConsoleAliasFromSystemName(context.SystemName, context.EmulatorName,
            context.EmulatorManager?.EmulatorLocation, _logger);

        await using var mountedDrive =
            await _mountChdFiles.MountAsync(context.ResolvedFilePath, consoleAlias, _logger, _messageBox);

        if (!mountedDrive.IsMounted)
        {
            // Mount failed - error message already shown by MountChdFiles
            return;
        }

        if (_isRpcs3)
        {
            // RPCS3 needs the path to EBOOT.BIN
            gameFilePath = FindEbootBin.FindEbootBinRecursive(mountedDrive.MountedPath, _logger, _logger);
        }
        else if (_isXenia)
        {
            // Xenia needs the path to default.xex
            gameFilePath = FindDefaultXex.Find(mountedDrive.MountedPath, _logger);
        }
        else if (_isXemu)
        {
            // Xemu needs the path to image.iso
            gameFilePath = FindImageIso.Find(mountedDrive.MountedPath, _logger);
        }
        else if (_isCxbxReloaded)
        {
            // Cxbx-Reloaded needs the path to default.xbe
            gameFilePath = FindDefaultXbe.Find(mountedDrive.MountedPath, _logger);
        }
        else if (_isGens || _cDiEmu || _isKegaFusion)
        {
            // Path to a .bin file
            gameFilePath = FindBinFile.Find(mountedDrive.MountedPath, _logger);
        }
        else if (_isGenesisPlusGx || _is4Do || _isBlastem || _isFinalBurnAlpha || _isFinalBurnNeo || _isMednafen ||
                 _isMesen || _isNebula ||
                 _isPcsxRedux || _isPicoDrive || _isRaine || _isTsugaru || _isYabause)
        {
            // Path to a .cue file
            gameFilePath = FindCueFile.Find(mountedDrive.MountedPath, _logger);
        }
        else
        {
            gameFilePath = null; // no emulator-specific file finder applies
        }

        if (string.IsNullOrEmpty(gameFilePath))
        {
            _logger.Debug(
                $"[ChdMountStrategy] No suitable game file found in mounted CHD at {mountedDrive.MountedPath}");
            // Expected condition (unsupported input; user already gets UI feedback): not a bug,
            // keep it out of the bug report service.
            _logger.Information($"No game file found in mounted CHD for emulator '{context.EmulatorName}'");
            await _messageBox.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(logPath);
            return; // no launch strategy runs after this one
        }

        // Launch the emulator with the found game file
        // Pass the original CHD file path for display in notifications
        await launcher.LaunchRegularEmulatorAsync(
            gameFilePath,
            context.EmulatorName,
            context.SystemManagerService!,
            context.EmulatorManager!,
            context.Parameters,
            context.WindowContext!,
            context.LoadingState,
            context.ResolvedFilePath);
    }

    private void ResolveEmulatorFlags(LaunchContext context)
    {
        _is4Do = context.EmulatorName.Contains("4do", StringComparison.OrdinalIgnoreCase) ||
                 (context.EmulatorManager?.EmulatorLocation?.Contains("4do.exe", StringComparison.OrdinalIgnoreCase) ??
                  false);

        _isBlastem = context.EmulatorName.Contains("blastem", StringComparison.OrdinalIgnoreCase) ||
                     (context.EmulatorManager?.EmulatorLocation?.Contains("blastem.exe",
                         StringComparison.OrdinalIgnoreCase) ?? false);

        _cDiEmu = context.EmulatorName.Contains("CDiEmu", StringComparison.OrdinalIgnoreCase) ||
                  context.EmulatorName.Contains("CDi Emu", StringComparison.OrdinalIgnoreCase) ||
                  context.EmulatorName.Contains("CDi-Emu", StringComparison.OrdinalIgnoreCase) ||
                  context.EmulatorName.Contains("CDiEmulator", StringComparison.OrdinalIgnoreCase) ||
                  context.EmulatorName.Contains("CDi Emulator", StringComparison.OrdinalIgnoreCase) ||
                  context.EmulatorName.Contains("CDi-Emulator", StringComparison.OrdinalIgnoreCase) ||
                  (context.EmulatorManager?.EmulatorLocation?.Contains("wcdiemu-v053b9.exe",
                      StringComparison.OrdinalIgnoreCase) ?? false) ||
                  (context.EmulatorManager?.EmulatorLocation?.Contains("wcdiemu", StringComparison.OrdinalIgnoreCase) ??
                   false);

        _isCxbxReloaded = context.EmulatorName.Contains("Cxbx", StringComparison.OrdinalIgnoreCase) ||
                          (context.EmulatorManager?.EmulatorLocation?.Contains("cxbx",
                              StringComparison.OrdinalIgnoreCase) ?? false);

        _isFinalBurnAlpha = context.EmulatorName.Contains("FBAlpha", StringComparison.OrdinalIgnoreCase) ||
                            context.EmulatorName.Contains("FB Alpha", StringComparison.OrdinalIgnoreCase) ||
                            context.EmulatorName.Contains("FinalBurnAlpha", StringComparison.OrdinalIgnoreCase) ||
                            context.EmulatorName.Contains("Final Burn Alpha", StringComparison.OrdinalIgnoreCase) ||
                            context.EmulatorName.Contains("FinalBurn Alpha", StringComparison.OrdinalIgnoreCase) ||
                            (context.EmulatorManager?.EmulatorLocation?.Contains("fba64.exe",
                                StringComparison.OrdinalIgnoreCase) ?? false);

        _isFinalBurnNeo = context.EmulatorName.Contains("FBNeo", StringComparison.OrdinalIgnoreCase) ||
                          context.EmulatorName.Contains("FB Neo", StringComparison.OrdinalIgnoreCase) ||
                          context.EmulatorName.Contains("FinalBurnNeo", StringComparison.OrdinalIgnoreCase) ||
                          context.EmulatorName.Contains("Final Burn Neo", StringComparison.OrdinalIgnoreCase) ||
                          context.EmulatorName.Contains("FinalBurn Neo", StringComparison.OrdinalIgnoreCase) ||
                          (context.EmulatorManager?.EmulatorLocation?.Contains("fbneo64.exe",
                              StringComparison.OrdinalIgnoreCase) ?? false);

        _isGenesisPlusGx = context.EmulatorName.Contains("genesis plus gx", StringComparison.OrdinalIgnoreCase) ||
                           (context.EmulatorManager?.EmulatorLocation?.Contains("gen_sdl.exe",
                               StringComparison.OrdinalIgnoreCase) ?? false);

        _isGens = context.EmulatorName.Contains("Gens", StringComparison.OrdinalIgnoreCase) ||
                  (context.EmulatorManager?.EmulatorLocation?.Contains("gens.exe",
                      StringComparison.OrdinalIgnoreCase) ?? false);

        _isKegaFusion = context.EmulatorName.Contains("Kega Fusion", StringComparison.OrdinalIgnoreCase) ||
                        context.EmulatorName.Contains("Fusion", StringComparison.OrdinalIgnoreCase) ||
                        (context.EmulatorManager?.EmulatorLocation?.Contains("fusion.exe",
                            StringComparison.OrdinalIgnoreCase) ?? false);

        _isMednafen = context.EmulatorName.Contains("Mednafen", StringComparison.OrdinalIgnoreCase) ||
                      (context.EmulatorManager?.EmulatorLocation?.Contains("mednafen",
                          StringComparison.OrdinalIgnoreCase) ?? false);

        _isMesen = context.EmulatorName.Contains("Mesen", StringComparison.OrdinalIgnoreCase) ||
                   (context.EmulatorManager?.EmulatorLocation?.Contains("Mesen.exe",
                       StringComparison.OrdinalIgnoreCase) ?? false);

        _isNebula = context.EmulatorName.Contains("Nebula", StringComparison.OrdinalIgnoreCase) ||
                    (context.EmulatorManager?.EmulatorLocation?.Contains("nebula.exe",
                        StringComparison.OrdinalIgnoreCase) ?? false);

        _isPcsxRedux = context.EmulatorName.Contains("PCSX-Redux", StringComparison.OrdinalIgnoreCase) ||
                       context.EmulatorName.Contains("PCSX Redux", StringComparison.OrdinalIgnoreCase) ||
                       (context.EmulatorManager?.EmulatorLocation?.Contains("pcsx-redux",
                           StringComparison.OrdinalIgnoreCase) ?? false);

        _isPicoDrive = context.EmulatorName.Contains("PicoDrive", StringComparison.OrdinalIgnoreCase) ||
                       context.EmulatorName.Contains("Pico Drive", StringComparison.OrdinalIgnoreCase) ||
                       (context.EmulatorManager?.EmulatorLocation?.Contains("PicoDrive.exe",
                           StringComparison.OrdinalIgnoreCase) ?? false);

        _isRaine = context.EmulatorName.Contains("raine", StringComparison.OrdinalIgnoreCase) ||
                   (context.EmulatorManager?.EmulatorLocation?.Contains("raine.exe",
                       StringComparison.OrdinalIgnoreCase) ?? false);

        _isRpcs3 = context.EmulatorName.Contains("RPCS3", StringComparison.OrdinalIgnoreCase) ||
                   (context.EmulatorManager?.EmulatorLocation?.Contains("rpcs3", StringComparison.OrdinalIgnoreCase) ??
                    false);

        _isTsugaru = context.EmulatorName.Contains("Tsugaru", StringComparison.OrdinalIgnoreCase) ||
                     (context.EmulatorManager?.EmulatorLocation?.Contains("Tsugaru_CUI.exe",
                         StringComparison.OrdinalIgnoreCase) ?? false);

        _isXemu = context.EmulatorName.Contains("Xemu", StringComparison.OrdinalIgnoreCase) ||
                  (context.EmulatorManager?.EmulatorLocation?.Contains("xemu", StringComparison.OrdinalIgnoreCase) ??
                   false);

        _isXenia = context.EmulatorName.Contains("Xenia", StringComparison.OrdinalIgnoreCase) ||
                   (context.EmulatorManager?.EmulatorLocation?.Contains("xenia", StringComparison.OrdinalIgnoreCase) ??
                    false);

        _isYabause = context.EmulatorName.Contains("Yabause", StringComparison.OrdinalIgnoreCase) ||
                     (context.EmulatorManager?.EmulatorLocation?.Contains("yabause.exe",
                         StringComparison.OrdinalIgnoreCase) ?? false);
    }
}