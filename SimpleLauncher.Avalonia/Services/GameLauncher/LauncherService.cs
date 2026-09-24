using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UsageStats;
using CheckApplicationControlPolicyService = SimpleLauncher.Core.Services.CheckApplicationControlPolicyService;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher;

/// <summary>
///     ILauncherService implementation with file-type detection and strategy dispatch.
///     Handles: direct launch (.exe/.bat/.lnk/.url), archive pass-through/extraction,
///     ZIP mounting (RPCS3/ScummVM/XBLA), XISO mount (Cxbx), CHD mount (CHDMounter via Core).
///     Mirrors the original SimpleLauncher launch pipeline: argument fallback (ROM path append),
///     emulator pre-flight flags, real play-time measurement and emulator config injection.
/// </summary>
public class LauncherService : ILauncherService
{
    private readonly AskAiToFixParameters _askAiToFixParameters;
    private readonly IEnumerable<IEmulatorConfigHandler> _configHandlers;
    private readonly IConfiguration _configuration;
    private readonly HashSet<string> _emulatorsToSkipErrorChecking;
    private readonly IExtractionService _extractionService;
    private readonly GamePadController _gamePadController;
    private readonly IEnumerable<ILaunchStrategy> _launchStrategies;
    private readonly LocalizationService _localization;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IMountChdFiles _mountChdFiles;
    private readonly IMountXisoFiles _mountXisoFiles;
    private readonly IMountZipFiles _mountZipFiles;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly SettingsManagerService _settings;
    private readonly Stats _stats;

    public LauncherService(
        IMessageBoxLibraryService messageBox,
        IEnumerable<IEmulatorConfigHandler> configHandlers,
        IConfiguration configuration,
        IExtractionService extractionService,
        IMountXisoFiles mountXisoFiles,
        IMountChdFiles mountChdFiles,
        IMountZipFiles mountZipFiles,
        AskAiToFixParameters askAiToFixParameters,
        SettingsManagerService settings,
        IEnumerable<ILaunchStrategy> launchStrategies,
        PlayHistoryManager playHistoryManager,
        Stats stats,
        GamePadController gamePadController,
        LocalizationService localization)
    {
        _messageBox = messageBox;
        _configHandlers = configHandlers;
        _configuration = configuration;
        _extractionService = extractionService;
        _mountXisoFiles = mountXisoFiles;
        _mountChdFiles = mountChdFiles;
        _mountZipFiles = mountZipFiles;
        _askAiToFixParameters = askAiToFixParameters;
        _settings = settings;
        _launchStrategies = launchStrategies.OrderBy(static s => s.Priority).ToList();
        _localization = localization;

        // WPF parity: read EmulatorsToSkipErrorChecking from config with hardcoded fallback
        var configEmulatorsToSkip = configuration
            .GetSection("EmulatorsToSkipErrorChecking")
            .Get<string[]>();
        if (configEmulatorsToSkip is { Length: > 0 })
        {
            _emulatorsToSkipErrorChecking = configEmulatorsToSkip.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            // Hardcoded fallback matching WPF DoNotCheckErrorsOnSpecificEmulators
            _emulatorsToSkipErrorChecking = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Kega Fusion", "KegaFusion", "Kega", "Fusion", "Fusion.exe",
                "Project64", "Project 64", "Project64.exe",
                "Emulicious", "Emulicious.exe",
                "Speccy", "Speccy.exe",
                "ProSystem.exe", "ProSystem",
                "fMSX.exe", "fMSX"
            };
        }

        _playHistoryManager = playHistoryManager;
        _stats = stats;
        _gamePadController = gamePadController;
    }

    /// <summary>
    ///     Real emulator run time of the last launch (from process start to exit).
    ///     Zero for shortcut launches (which do not wait for exit).
    /// </summary>
    public TimeSpan LastPlayTime { get; private set; }

    public async Task LaunchRegularEmulatorAsync(
        string resolvedFilePath,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        ILoadingState? loadingStateProvider,
        string? originalFilePathForDisplay = null)
    {
        LastPlayTime = TimeSpan.Zero;

        // Use the original file path for display when provided (e.g., mounted/extracted
        // files) so toasts show the original archive name (WPF LaunchRegularEmulatorAsync parity).
        var displayFilePath = originalFilePathForDisplay ?? resolvedFilePath;
        var originalFileName = Path.GetFileNameWithoutExtension(displayFilePath);

        var ext = Path.GetExtension(resolvedFilePath).ToUpperInvariant();
        var actualFilePath = resolvedFilePath;
        string? cleanupPath = null; // temp dir to clean up after launch
        var emulatorName = selectedEmulatorManager.EmulatorName ?? "";
        var emulatorLocation = selectedEmulatorManager.EmulatorLocation ?? "";

        // Mount handles live for the whole launch (drive must stay mounted while the
        // emulator runs — mirroring the WPF strategies which dispose after launch).
        MountChdDrive? mountedChd = null;
        MountXisoDrive? mountedXiso = null;

        // The overlay state is reference-counted: every SetLoadingState(true) below has a
        // matching SetLoadingState(false) in a finally block, so no path (including
        // exceptions and early returns) can leave the overlay stuck.
        loadingStateProvider?.SetLoadingState(true, _localization.GetString("Preparing", "Preparing..."));

        try
        {
            // ── Direct-launch games (.bat/.cmd/.lnk/.url/.exe) — the game IS the executable.
            // Matches the original SimpleLauncher dispatch: no emulator, no config handlers.
            switch (ext)
            {
                case ".BAT" or ".CMD":
                    await RunBatchFileAsync(resolvedFilePath, selectedEmulatorManager, windowContext,
                        loadingStateProvider);
                    return;
                case ".LNK" or ".URL":
                    await LaunchShortcutFileAsync(resolvedFilePath, selectedEmulatorManager, windowContext,
                        loadingStateProvider);
                    return;
                case ".EXE":
                    await LaunchExecutableAsync(resolvedFilePath, selectedEmulatorManager, windowContext);
                    return;
            }

            // ── File-type dispatch ──
            switch (ext)
            {
                case ".ZIP":
                case ".7Z":
                case ".RAR":
                {
                    // ZIP mounting strategies (mirror of the WPF ZipMountStrategy): RPCS3
                    // games are mounted and EBOOT.BIN is located, ScummVM games get the
                    // archive extracted to its data folder, XBLA games are searched for a
                    // launchable file. These paths handle the whole launch themselves.
                    var isRpcs3 = emulatorName.Contains("RPCS3", StringComparison.OrdinalIgnoreCase);
                    var isScummVm =
                        selectedSystemManager.SystemName.Contains("Scumm", StringComparison.OrdinalIgnoreCase);
                    var isXbla = selectedSystemManager.SystemName.Contains("xbla", StringComparison.OrdinalIgnoreCase);

                    if (isRpcs3 || isScummVm || isXbla)
                    {
                        var logPath = PathHelper.ResolveLogFilePath(_configuration);
                        loadingStateProvider?.SetLoadingState(true,
                            _localization.GetString("Mountingarchive", "Mounting archive..."));

                        try
                        {
                            if (isRpcs3)
                            {
                                await _mountZipFiles.MountZipFileAndLoadEbootBinAsync(
                                    resolvedFilePath, selectedSystemManager.SystemName, emulatorName,
                                    selectedSystemManager, selectedEmulatorManager, rawEmulatorParameters,
                                    windowContext, logPath, this, Log.Logger, _messageBox);
                            }
                            else if (isScummVm)
                            {
                                await _mountZipFiles.MountZipFileAndLoadWithScummVmAsync(
                                    resolvedFilePath, selectedSystemManager.SystemName, emulatorName,
                                    selectedSystemManager, selectedEmulatorManager, rawEmulatorParameters,
                                    logPath, Log.Logger, _messageBox);
                            }
                            else
                            {
                                await _mountZipFiles.MountZipFileAndSearchForFileToLoadAsync(
                                    resolvedFilePath, selectedSystemManager.SystemName, emulatorName,
                                    selectedSystemManager, selectedEmulatorManager, rawEmulatorParameters,
                                    windowContext, logPath, this, Log.Logger, _messageBox);
                            }
                        }
                        finally
                        {
                            loadingStateProvider?.SetLoadingState(false);
                        }

                        return;
                    }

                    // Emulators that can read archives directly (RetroArch, etc.) receive the
                    // archive path unchanged. Emulators needing real files (or the system's
                    // ExtractFileBeforeLaunch flag) get the archive extracted first.
                    var requiresRealFiles = selectedSystemManager.ExtractFileBeforeLaunch ||
                                            emulatorName.Contains("DuckStation", StringComparison.OrdinalIgnoreCase) ||
                                            emulatorName.Contains("Azahar", StringComparison.OrdinalIgnoreCase) ||
                                            emulatorName.Contains("Citra", StringComparison.OrdinalIgnoreCase) ||
                                            emulatorName.Contains("Ootake", StringComparison.OrdinalIgnoreCase) ||
                                            emulatorName.Contains("SameBoy", StringComparison.OrdinalIgnoreCase) ||
                                            emulatorName.Contains("Ymir", StringComparison.OrdinalIgnoreCase) ||
                                            emulatorName.Contains("Yumir", StringComparison.OrdinalIgnoreCase) ||
                                            IsYmirEmulatorLocation(emulatorLocation);
                    if (requiresRealFiles)
                    {
                        loadingStateProvider?.SetLoadingState(true,
                            _localization.GetString("ExtractingEllipsis", "Extracting..."));
                        try
                        {
                            var (gameFilePath, tempDirectoryPath) =
                                await _extractionService.ExtractToTempAndGetLaunchFileAsync(
                                    resolvedFilePath, selectedSystemManager.FileFormatsToLaunch);
                            if (gameFilePath is not null)
                            {
                                actualFilePath = gameFilePath;
                                cleanupPath = tempDirectoryPath;
                            }
                            else
                            {
                                // No launchable file found inside the archive
                                await _messageBox.CustomErrorMessageBoxAsync(
                                    $"No launchable file was found inside the archive:\n{resolvedFilePath}\n\n" +
                                    "Expected formats: " + string.Join(", ", selectedSystemManager.FileFormatsToLaunch),
                                    "No Launchable File Found");
                                return;
                            }
                        }
                        finally
                        {
                            loadingStateProvider?.SetLoadingState(false);
                        }
                    }
                    // else: pass the archive path to the emulator (RetroArch can read archives directly)

                    break;
                }

                case ".ISO":
                case ".XISO":
                    // Original Xbox images: Cxbx cannot read a raw ISO, so mount it as a
                    // virtual drive (drive stays mounted until the emulator exits, mirroring
                    // the WPF XisoMountStrategy). Emulators like Xemu read the ISO natively.
                    if (emulatorName.Contains("Cxbx", StringComparison.OrdinalIgnoreCase))
                    {
                        loadingStateProvider?.SetLoadingState(true,
                            _localization.GetString("MountingXISO", "Mounting XISO..."));
                        try
                        {
                            mountedXiso = await _mountXisoFiles.MountAsync(
                                resolvedFilePath, Log.Logger, _messageBox);
                            if (mountedXiso.IsMounted)
                            {
                                actualFilePath = mountedXiso.MountedPath;
                            }
                            else
                            {
                                return;
                            }
                        }
                        finally
                        {
                            loadingStateProvider?.SetLoadingState(false);
                        }
                    }
                    // else: pass the ISO/XISO path to the emulator (may handle it directly)

                    break;

                case ".CHD":
                {
                    // Mirror of the WPF ChdMountStrategy: only emulators that cannot read
                    // .chd natively get the image mounted; RetroArch and the rest receive
                    // the raw .chd path. Mounting needs CHDMounter + Dokan, so it is
                    // Windows-only: on Linux/macOS the ChdMountStrategy converts the CHD
                    // with chdman before this inline fallback can be reached, and the raw
                    // path is passed through for the emulators that read CHD natively.
                    var chdKind = GetChdGameFileKind(emulatorName, emulatorLocation);
                    if (chdKind != ChdGameFileKind.None && OperatingSystem.IsWindows())
                    {
                        loadingStateProvider?.SetLoadingState(true,
                            _localization.GetString("MountingCHD", "Mounting CHD..."));
                        try
                        {
                            var consoleAlias = _mountChdFiles.GetConsoleAliasFromSystemName(
                                selectedSystemManager.SystemName, emulatorName, emulatorLocation, Log.Logger);
                            mountedChd = await _mountChdFiles.MountAsync(
                                resolvedFilePath, consoleAlias, Log.Logger, _messageBox);

                            if (!mountedChd.IsMounted)
                            {
                                // Error message already shown by MountChdFiles
                                return;
                            }

                            var gameFilePath = FindGameFileInMountedChd(mountedChd.MountedPath, chdKind);
                            if (string.IsNullOrEmpty(gameFilePath))
                            {
                                // Expected condition (unsupported input; user already gets UI feedback):
                                // not a bug, keep it out of the bug report service.
                                Log.Information("No game file found in mounted CHD for emulator '{Emulator}'",
                                    emulatorName);
                                await _messageBox.CustomErrorMessageBoxAsync(
                                    "No suitable game file was found inside the mounted CHD image.",
                                    "No Game File Found");
                                return;
                            }

                            actualFilePath = gameFilePath;
                        }
                        finally
                        {
                            loadingStateProvider?.SetLoadingState(false);
                        }
                    }
                    // else: the emulator reads .chd natively — pass the path directly

                    break;
                }
            }

            // ── Emulator input validation (ported from the WPF launcher) ──
            // Checked on the post-extraction/mount path (actualFilePath), mirroring the
            // WPF launcher which validates after archive extraction. The extension is
            // recomputed here (lower-case, so ".CHD"/".ZIP" match too) because the
            // pre-extraction one is stale once an archive was extracted to a real file.
            var actualExtension = Path.GetExtension(actualFilePath).ToLowerInvariant();
            var isOotakeEmulator = emulatorName.Contains("Ootake", StringComparison.OrdinalIgnoreCase) ||
                                   emulatorLocation.Contains("ootake.exe", StringComparison.OrdinalIgnoreCase);
            if (isOotakeEmulator && actualExtension is ".chd" or ".bin" or ".cue" or ".iso")
            {
                Log.Information(
                    "Ootake does not support CHD/ISO/CUE-BIN image files. Launch blocked. File: {Path}",
                    actualFilePath);
                await _messageBox.OotakeDoesNotSupportImageFilesMessageBoxAsync();
                return;
            }

            var isGeolithCore = (selectedEmulatorManager.EmulatorParameters ?? "")
                .Contains("geolith_libretro", StringComparison.OrdinalIgnoreCase);
            if (isGeolithCore && actualExtension is ".zip" or ".7z" or ".rar")
            {
                Log.Information(
                    "The Geolith libretro core only supports NEO files. Launch blocked. File: {Path}",
                    actualFilePath);
                await _messageBox.GeolithDoesNotSupportCompressedFilesMessageBoxAsync();
                return;
            }

            // ── Emulator pre-flight checks (from the original launcher) ──
            // (Config handlers already ran in HandleButtonClickAsync, before strategy
            // dispatch — WPF parity. Parameters may have been modified there; they flow
            // here via rawEmulatorParameters.)
            var launchParameters = rawEmulatorParameters;
            var emulatorPath = selectedEmulatorManager.EmulatorLocation ?? "";
            var isRetroArch = emulatorName.Contains("RetroArch", StringComparison.OrdinalIgnoreCase);
            var isMame = emulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase);
            var isRaine = emulatorName.Contains("Raine", StringComparison.OrdinalIgnoreCase);
            var isXemu = emulatorName.Contains("Xemu", StringComparison.OrdinalIgnoreCase);

            if (isRetroArch && !launchParameters.Contains("-L", StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug(
                    "[LaunchRegularEmulatorAsync] RetroArch parameter should contain -L. Parameter field: {Parameters}",
                    launchParameters);
                Log.Information(
                    "[LaunchRegularEmulatorAsync] RetroArch parameter should contain -L. Parameter field: {Parameters}",
                    launchParameters);
                await _messageBox.CustomErrorMessageBoxAsync(
                    "RetroArch parameters must contain \"-L\" pointing to the desired core.\n\n" +
                    "Example: -L \"cores\\snes9x_libretro.dll\"",
                    "RetroArch Parameter Issue");
                return;
            }

            // WPF parity: block when isXemu and parameters don't contain -dvd_path for ANY file
            if (isXemu && !launchParameters.Contains("-dvd_path", StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug(
                    "[LaunchRegularEmulatorAsync] Xemu parameter should contain '-dvd_path'. Parameter field: {Parameters}",
                    launchParameters);
                Log.Information(
                    "[LaunchRegularEmulatorAsync] Xemu parameter should contain '-dvd_path'. Parameter field: {Parameters}",
                    launchParameters);
                await _messageBox.CustomErrorMessageBoxAsync(
                    "Xemu parameters must contain \"-dvd_path\" pointing to the disc image.\n\n" +
                    "Example: -dvd_path \"%ROM%\"",
                    "Xemu Parameter Issue");
                return;
            }

            // ── Launch ──
            // WPF parity: the reference-counted overlay shown at the start of this method stays
            // active for the whole launch, so no second show is taken here. Only the status bar
            // text is advanced (incrementing the counter again would leak an overlay).
            if (loadingStateProvider is ILaunchFeedback launchStageFeedback)
                launchStageFeedback.SetStatusText(_localization.GetString("Launching", "Launching..."));

            if (string.IsNullOrWhiteSpace(emulatorPath))
            {
                Log.Debug("EmulatorLocation is null or empty for emulator '{Emulator}'.", emulatorName);
                Log.Information("EmulatorLocation is null or empty for emulator '{Emulator}'.", emulatorName);
                await _messageBox.CustomErrorMessageBoxAsync(
                    _localization.GetString("Noemulatorpathconfigured", "No emulator path configured."),
                    _localization.GetString("LaunchErrorTitle", "Launch Error"));
                return;
            }

            // Resolve %BASEFOLDER% / relative emulator paths from system.xml to a real path,
            // tolerating long paths (>260 chars) and Unicode normalization differences (NFC vs NFD).
            var resolvedEmulatorPath = PathHelper.ResolveRelativeToAppDirectory(emulatorPath);
            if (string.IsNullOrEmpty(resolvedEmulatorPath) ||
                (!File.Exists(resolvedEmulatorPath) && !File.Exists(PathHelper.GetLongPath(resolvedEmulatorPath))))
            {
                var normalizedEmulatorPath = PathHelper.TryFindFileWithNormalizedPath(resolvedEmulatorPath);
                if (!string.IsNullOrEmpty(normalizedEmulatorPath))
                {
                    Log.Debug("[LaunchRegularEmulatorAsync] Found emulator using Unicode normalization: {Path}",
                        normalizedEmulatorPath);
                    resolvedEmulatorPath = normalizedEmulatorPath;
                }
            }

            if (string.IsNullOrEmpty(resolvedEmulatorPath) || !File.Exists(resolvedEmulatorPath))
            {
                var msg =
                    $"Emulator executable not found after resolving: '{emulatorPath}' -> '{resolvedEmulatorPath}'";

                // OneDrive-specific guidance for the emulator executable
                if (!string.IsNullOrEmpty(resolvedEmulatorPath) &&
                    resolvedEmulatorPath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDir = Path.GetDirectoryName(resolvedEmulatorPath);
                    var oneDriveFolderExists = !string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir);
                    msg += oneDriveFolderExists
                        ? "\nThe emulator file is in a OneDrive folder but could not be found. " +
                          "Ensure the file is synced and downloaded to your device. " +
                          "Right-click the file in File Explorer and select 'Always keep on this device'."
                        : "\nThe parent OneDrive folder does not exist or is not accessible. " +
                          "Ensure OneDrive is signed in and synced, and that the folder is available on this device.";
                }

                // Expected user condition (emulator moved/deleted/unconfigured) — Information level.
                Log.Debug("[LaunchRegularEmulatorAsync] Error: {Message}", msg);
                Log.Information(msg);

                // Direct, actionable message with the configured path (WPF parity)
                await _messageBox.EmulatorExecutableNotFoundMessageBoxAsync(emulatorPath);
                return;
            }

            // Resolve all placeholders (%ROM%, %BASEFOLDER%, %EMULATORFOLDER%, %NAME%,
            // %ROMSYSTEMFOLDER%, ...) exactly like the original SimpleLauncher
            var romName = Path.GetFileNameWithoutExtension(actualFilePath);
            var resolvedEmulatorFolderPath = Path.GetDirectoryName(resolvedEmulatorPath) ?? "";
            if (string.IsNullOrEmpty(resolvedEmulatorFolderPath) ||
                !Directory.Exists(PathHelper.GetLongPath(resolvedEmulatorFolderPath)))
            {
                Log.Information("Could not determine emulator folder path from executable path: '{EmulatorPath}'",
                    resolvedEmulatorPath);
                Log.Debug("Could not determine emulator folder path from executable path: '{EmulatorPath}'",
                    resolvedEmulatorPath);
            }

            var romSystemFolder = PathHelper.FindContainingSystemFolder(
                selectedSystemManager.SystemFolders,
                selectedSystemManager.PrimarySystemFolder,
                actualFilePath);
            var resolvedParameters = PathHelper.ResolveParameterString(
                launchParameters,
                selectedSystemManager.SystemFolders,
                resolvedEmulatorFolderPath,
                actualFilePath,
                romSystemFolder,
                romName);

            // ── Argument fallback (from the original launcher): when the parameter string
            // contains no ROM placeholder, append the ROM path (or bare ROM name for
            // MAME/Raine) so emulators with bare flags still receive the game.
            var containsRomPlaceholder = launchParameters.Contains("%ROM%", StringComparison.OrdinalIgnoreCase);
            var containsNamePlaceholder = launchParameters.Contains("%NAME%", StringComparison.OrdinalIgnoreCase);
            string arguments;
            if (containsRomPlaceholder || containsNamePlaceholder ||
                PathHelper.ContainsGameSpecificPlaceholder(resolvedParameters))
            {
                arguments = resolvedParameters;
            }
            else
            {
                var trimmedParameters = resolvedParameters.TrimEnd();
                var space = string.IsNullOrWhiteSpace(trimmedParameters) || trimmedParameters.EndsWith('=')
                    ? ""
                    : " ";
                // Recompute from the post-extraction path: a .cue extracted from a .zip
                // must still be treated as NeoGeo CD.
                var isNeoGeoCd = actualExtension is ".cue" or ".iso" or ".bin";
                if ((isMame || isRaine) && !isNeoGeoCd)
                {
                    // MAME/Raine: stripped path call — launch by ROM name
                    Log.Debug("Stripped path call detected. Launching: {RomName}", romName);
                    arguments = $"{trimmedParameters}{space}\"{romName}\"";
                }
                else
                {
                    // General call — provide the full file path
                    arguments = $"{trimmedParameters}{space}\"{actualFilePath}\"";
                }
            }

            Log.Debug("LaunchRegularEmulatorAsync:\n\n");
            Log.Debug("Program Location: {ProgramLocation}", resolvedEmulatorPath);
            Log.Debug("Arguments: {Arguments}", arguments);
            Log.Debug("Working Directory: {WorkingDirectory}", resolvedEmulatorFolderPath);
            Log.Debug("File to launch: {FileToLaunch}", actualFilePath);

            Exception? launchException = null;
            var stderrOutput = "";
            var stdoutOutput = "";
            var processExitCode = 0;
            var processStartTime = DateTime.Now;

            // WPF parity: show "X launched with Y" feedback before starting the emulator.
            if (loadingStateProvider is ILaunchFeedback launchFeedback)
            {
                var launchedWith = _localization.GetString("launchedwith", "launched with");
                launchFeedback.ShowToast("Simple Launcher", $"{originalFileName} {launchedWith} {emulatorName}");
                launchFeedback.SetStatusText($"{originalFileName} {launchedWith} {emulatorName}");
            }

            await Task.Run(async () =>
            {
                try
                {
                    var isBatchFile = Path.GetExtension(resolvedEmulatorPath)
                        .Equals(".bat", StringComparison.OrdinalIgnoreCase);
                    var stderrBuffer = new StringBuilder();
                    var stdoutBuffer = new StringBuilder();

                    using var process = new Process();
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = resolvedEmulatorPath,
                        Arguments = arguments,
                        // .bat files require shell execution; .exe works without it
                        UseShellExecute = isBatchFile,
                        WorkingDirectory = resolvedEmulatorFolderPath,
                        CreateNoWindow = true,
                        RedirectStandardOutput = !isBatchFile,
                        RedirectStandardError = !isBatchFile,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    if (!isBatchFile)
                    {
                        // Drain BOTH streams asynchronously: a chatty emulator (MAME, RetroArch
                        // with log verbosity, CLI tools) would otherwise fill the pipe buffer
                        // and block forever, deadlocking WaitForExit (mirrors the WPF launcher).
                        process.OutputDataReceived += (_, e) =>
                        {
                            if (e.Data is not null) stdoutBuffer.AppendLine(e.Data);
                        };
                        process.ErrorDataReceived += (_, e) =>
                        {
                            if (e.Data is not null) stderrBuffer.AppendLine(e.Data);
                        };
                    }

                    process.Start();

                    if (!isBatchFile)
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }

                    // WaitForExitAsync also waits for the async output reads to complete.
                    await process.WaitForExitAsync();
                    stderrOutput = stderrBuffer.ToString();
                    stdoutOutput = stdoutBuffer.ToString();
                    processExitCode = process.ExitCode;
                }
                catch (Exception ex)
                {
                    launchException = ex;
                    Log.Error(ex, "Failed to launch emulator process {EmulatorPath}", resolvedEmulatorPath);
                }
            });

            LastPlayTime = DateTime.Now - processStartTime;

            if (launchException is not null)
            {
                // WPF parity: Win32Exception gets dedicated OS-level handling
                // (App-Control-Policy block, UAC elevation required, user-cancelled UAC)
                // instead of the generic error + AI parameter fix, which cannot help
                // with an OS-level block.
                if (launchException is Win32Exception win32Ex)
                {
                    if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(win32Ex))
                    {
                        await _messageBox.ApplicationControlPolicyBlockedMessageBoxAsync();
                        // Expected user-environment condition (OS policy blocks the emulator): not a bug.
                        Log.Information(win32Ex, "Application control policy blocked launching emulator");
                        return;
                    }
                    else if (CheckApplicationControlPolicyService.IsElevationRequired(win32Ex))
                    {
                        await _messageBox.ElevationRequiredMessageBoxAsync();
                        // Expected user-environment condition (the emulator requires admin rights): not a bug.
                        Log.Information(win32Ex, "Elevation required to launch emulator");
                        return;
                    }
                    else if (CheckApplicationControlPolicyService.IsOperationCanceledByUser(win32Ex))
                    {
                        // User cancelled the operation (e.g., clicked Cancel on the UAC
                        // prompt) — do nothing and don't offer the AI fix.
                        return;
                    }
                    else if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(win32Ex))
                    {
                        // Expected user-error condition (the file is not a valid executable for
                        // this OS platform): not a bug, don't log as error or offer the AI fix.
                        await _messageBox.InvalidExecutableFileMessageBoxAsync();
                        return;
                    }
                }

                await _messageBox.CouldNotLaunchGameMessageBoxAsync(LogFilePath());

                // Offer the AI parameter fix (ported from the original launcher)
                await _askAiToFixParameters.ExecuteAsync(
                    selectedSystemManager, selectedEmulatorManager, loadingStateProvider);

                return;
            }

            // ── Post-exit error analysis (ported from the WPF GameLauncherService) ──
            if (!string.IsNullOrWhiteSpace(stderrOutput))
            {
                Log.Debug("Emulator stderr for {Emulator}: {Stderr}", emulatorName,
                    stderrOutput.Length > 2000 ? stderrOutput[..2000] : stderrOutput);
            }

            await AnalyzeProcessExitAsync(
                processExitCode, stdoutOutput, stderrOutput,
                resolvedEmulatorPath, arguments,
                emulatorName, emulatorLocation,
                selectedEmulatorManager, selectedSystemManager,
                loadingStateProvider);

            // ── Play history & stats integration (ported from WPF) ──
            if (LastPlayTime.TotalSeconds > 5)
            {
                await UpdateStatsAndPlayCountAsync(
                    LastPlayTime, resolvedFilePath, selectedSystemManager.SystemName,
                    emulatorName, loadingStateProvider);
                GamePlayed?.Invoke(this, new GamePlayedEventArgs(resolvedFilePath, selectedSystemManager.SystemName));
            }
        }
        finally
        {
            // Closes the single reference-counted show taken at the start of this method:
            // every return/exception path above leaves the overlay hidden.
            loadingStateProvider?.SetLoadingState(false);

            // Clean up temp extraction directory
            if (cleanupPath is not null)
            {
                try
                {
                    Log.Debug("Attempting to delete temporary extraction directory: {Path}", cleanupPath);
                    Directory.Delete(cleanupPath, true);
                    Log.Debug("Successfully deleted temporary extraction directory: {Path}", cleanupPath);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to delete temp extraction dir {Path}", cleanupPath);
                }
            }

            // Unmount the drives AFTER the emulator exited (kills the mount processes)
            if (mountedChd is not null)
            {
                try
                {
                    await mountedChd.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to unmount CHD drive");
                }
            }

            if (mountedXiso is not null)
            {
                try
                {
                    await mountedXiso.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to unmount XISO drive");
                }
            }
        }
    }

    /// <summary>
    ///     Raised after a game has finished playing with a play time above the 5-second threshold.
    /// </summary>
    public event EventHandler<GamePlayedEventArgs>? GamePlayed;

    /// <summary>
    ///     Game launch pipeline entry point (port of the WPF GameLauncherService.HandleButtonClickAsync):
    ///     resolves the path, builds a <see cref="LaunchContext" />, runs light validation, and dispatches
    ///     to the first matching <see cref="ILaunchStrategy" /> (ascending priority, Default last).
    ///     Play-time tracking stays with <see cref="LastPlayTime" />.
    /// </summary>
    public async Task HandleButtonClickAsync(
        string filePath,
        string selectedEmulatorName,
        string selectedSystemName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        ILoadingState? loadingStateProvider)
    {
        // 1. Create Context
        var context = new LaunchContext
        {
            FilePath = filePath,
            ResolvedFilePath = PathHelper.ResolveRelativeToAppDirectory(filePath) ?? filePath,
            EmulatorName = selectedEmulatorName,
            SystemName = selectedSystemName,
            SystemManagerService = selectedSystemManager,
            EmulatorManager = selectedEmulatorManager,
            Parameters = rawEmulatorParameters,
            Settings = _settings,
            WindowContext = windowContext,
            LoadingState = loadingStateProvider
        };

        // Pause gamepad input while the emulator is running to prevent
        // mouse/scroll leaking to the desktop (mirrors the WPF app behavior).
        var wasGamePadRunning = _gamePadController.IsRunning;
        if (wasGamePadRunning) await _gamePadController.StopAsync();

        try
        {
            // 2. Validate SystemManagerService and Emulators before resolving
            if (context.SystemManagerService?.Emulators == null ||
                context.SystemManagerService.Emulators.Count == 0 ||
                context.EmulatorManager == null)
            {
                Log.Warning("SystemManagerService or Emulators is null/empty when attempting to launch");
                await _messageBox.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(LogFilePath());
                return;
            }

            // 3. Perform Validation (file exists — expected user condition, Information level)
            if (!await ValidateContextAsync(context)) return;

            // 4. Run configuration handlers BEFORE strategy dispatch (WPF parity).
            // WPF HandleButtonClickAsync runs the first matching handler for EVERY
            // launch — including direct .bat/.lnk/.url/.exe — and aborts the launch
            // when a handler returns false (e.g. the user cancelled the settings window).
            var configHandler = _configHandlers.FirstOrDefault(h =>
                h.IsMatch(context.EmulatorName, context.EmulatorManager?.EmulatorLocation ?? ""));
            if (configHandler != null)
            {
                loadingStateProvider?.SetLoadingState(true,
                    _localization.GetString("Configuringemulator", "Configuring emulator..."));
                try
                {
                    if (!await configHandler.HandleConfigurationAsync(context))
                    {
                        Log.Information("Emulator config handler {Handler} aborted launch for {Emulator}",
                            configHandler.GetType().Name, context.EmulatorName);
                        return;
                    }
                }
                finally
                {
                    loadingStateProvider?.SetLoadingState(false);
                }
            }

            // 5. Select and execute the first matching strategy (ascending priority).
            // The inline dispatch inside LaunchRegularEmulatorAsync mirrors the WPF
            // Default/ZIP/XISO/CHD-mount strategies, so only strategies that previously
            // had no Avalonia handling (PBP conversion, DOSBox, Commander Genius,
            // CHD-to-CUE) can match before the Default fallback runs.
            // WPF parity: the overlay is owned here for the whole strategy execution so a
            // strategy that shows nothing still gets a loading state; strategies that take
            // their own show use a balanced nested pair.
            loadingStateProvider?.SetLoadingState(true);
            try
            {
                var strategy = _launchStrategies.FirstOrDefault(s => s.IsMatch(context));
                if (strategy == null)
                {
                    Log.Warning(
                        "No launch strategy found for the context: SystemName='{System}', EmulatorName='{Emulator}', FilePath='{Path}'",
                        context.SystemName, context.EmulatorName, context.FilePath);
                    await _messageBox.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(LogFilePath());
                    return;
                }

                await strategy.ExecuteAsync(context, this);
            }
            finally
            {
                loadingStateProvider?.SetLoadingState(false);
            }
        }
        catch (Exception ex)
        {
            var detailedMessage = "Launch Pipeline Failed.\n" +
                                  $"Exception Type: {ex.GetType().FullName}\n" +
                                  $"SystemName: '{context.SystemName ?? "null"}'\n" +
                                  $"EmulatorName: '{context.EmulatorName ?? "null"}'\n" +
                                  $"FilePath: '{context.FilePath ?? "null"}'\n" +
                                  $"ResolvedFilePath: '{context.ResolvedFilePath ?? "null"}'";
            Log.Error(ex, detailedMessage);
            await _messageBox.CouldNotLaunchGameMessageBoxAsync(LogFilePath());
        }
        finally
        {
            if (wasGamePadRunning) await _gamePadController.StartAsync();
        }
    }

    private async Task<bool> ValidateContextAsync(LaunchContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ResolvedFilePath))
        {
            Log.Warning("Resolved file path is empty");
            await _messageBox.FilePathIsInvalidMessageBoxAsync(LogFilePath());
            return false;
        }

        var standardPath = context.ResolvedFilePath;
        var longPath = PathHelper.GetLongPath(standardPath);

        // Check both standard and long path formats for maximum compatibility (>260 char paths)
        var standardFileExists = File.Exists(standardPath);
        var longFileExists = File.Exists(longPath);
        var standardDirExists = Directory.Exists(standardPath);
        var longDirExists = Directory.Exists(longPath);

        var fileExists = standardFileExists || longFileExists;
        var directoryExists = standardDirExists || longDirExists;

        // If file doesn't exist, try Unicode normalization variations.
        // This handles cases where filenames have different normalization forms (NFC vs NFD),
        // commonly occurring when files are created on different operating systems (macOS vs Windows).
        string? normalizedPath = null;
        if (!fileExists && !directoryExists)
        {
            normalizedPath = PathHelper.TryFindFileWithNormalizedPath(standardPath);
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                fileExists = true;
                context.ResolvedFilePath = normalizedPath;
                Log.Debug("[ValidateContextAsync] Found file using Unicode normalization: {Path}", normalizedPath);
            }
        }

        if (!fileExists && !directoryExists)
        {
            var msg = $"File not found: {context.ResolvedFilePath}";

            // OneDrive-specific guidance: cloud-only placeholders and unsynced folders are
            // the most common cause of "file not found" for otherwise valid library entries.
            if (context.ResolvedFilePath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                var parentDir = Path.GetDirectoryName(context.ResolvedFilePath);
                var oneDriveFolderExists = !string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir);
                msg += oneDriveFolderExists
                    ? "\nThe file is in a OneDrive folder but could not be found. " +
                      "Ensure the file is synced and downloaded to your device. " +
                      "Right-click the file in File Explorer and select 'Always keep on this device'."
                    : "\nThe parent OneDrive folder does not exist or is not accessible. " +
                      "Ensure OneDrive is signed in and synced, and that the folder is available on this device.";
            }

            // Expected condition: the game entry is stale (file deleted/moved since the list
            // was loaded) and the user is already notified via the message box — not a bug report.
            Log.Information(new FileNotFoundException(msg), msg);
            await _messageBox.FilePathIsInvalidMessageBoxAsync(LogFilePath());
            return false;
        }

        // Detect path format mismatch (exists in one format but not the other).
        // Helps identify Unicode normalization or path handling issues; logged for developer
        // investigation but does not block the launch.
        if (standardFileExists != longFileExists || standardDirExists != longDirExists)
        {
            Log.Debug(
                "Path validation mismatch detected:\n" +
                "  Original Path: {Original}\n" +
                "  Resolved Path: {Resolved}\n" +
                "  Long Path: {Long}\n" +
                "  Normalized Path Found: {Normalized}\n" +
                "  Standard File.Exists: {StdFile}, Long Path File.Exists: {LongFile}\n" +
                "  Standard Directory.Exists: {StdDir}, Long Path Directory.Exists: {LongDir}\n" +
                "  This may indicate a Unicode normalization or path handling issue",
                context.FilePath, standardPath, longPath, normalizedPath ?? "N/A",
                standardFileExists, longFileExists, standardDirExists, longDirExists);

            Log.Error(
                "Path validation mismatch detected:\n" +
                "  Original Path: {Original}\n" +
                "  Resolved Path: {Resolved}\n" +
                "  Long Path: {Long}\n" +
                "  Normalized Path Found: {Normalized}\n" +
                "  Standard File.Exists: {StdFile}, Long Path File.Exists: {LongFile}\n" +
                "  Standard Directory.Exists: {StdDir}, Long Path Directory.Exists: {LongDir}\n" +
                "  This may indicate a Unicode normalization or path handling issue",
                context.FilePath, standardPath, longPath, normalizedPath ?? "N/A",
                standardFileExists, longFileExists, standardDirExists, longDirExists);
        }

        if (string.IsNullOrWhiteSpace(context.EmulatorName))
        {
            Log.Warning("Emulator name is empty");
            await _messageBox.CouldNotLaunchGameMessageBoxAsync(LogFilePath());
            return false;
        }

        // Add the GroupByFolder check
        if (context.SystemManagerService is { GroupByFolder: true })
        {
            var emulatorName = context.EmulatorName;
            var emulatorLocation = context.EmulatorManager?.EmulatorLocation ?? "";

            var isMame = emulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
                         emulatorLocation.Contains("mame.exe", StringComparison.OrdinalIgnoreCase) ||
                         emulatorLocation.Contains("mame64.exe", StringComparison.OrdinalIgnoreCase);

            var isDosBox = emulatorName.Contains("DOSBox", StringComparison.OrdinalIgnoreCase) ||
                           emulatorLocation.Contains("dosbox", StringComparison.OrdinalIgnoreCase);

            if (!isMame && !isDosBox)
            {
                await _messageBox.GroupByFolderOnlyForMameAndDosBoxMessageBoxAsync();
                return false;
            }
        }

        return true;
    }

    private string LogFilePath()
    {
        return PathHelper.ResolveLogFilePath(_configuration);
    }

    #region Standard launches

    /// <inheritdoc />
    public Task RunBatchFileAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext)
    {
        return RunBatchFileAsync(resolvedFilePath, selectedEmulatorManager, windowContext, null);
    }

    /// <summary>
    ///     Runs a batch file with launch feedback (toast + status text) when the caller
    ///     provides an <see cref="ILoadingState" /> that also implements
    ///     <see cref="SimpleLauncher.Avalonia.Interfaces.ILaunchFeedback" />.
    /// </summary>
    public async Task RunBatchFileAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext,
        ILoadingState? loadingStateProvider = null)
    {
        // WPF parity (GameLauncherService.LaunchBatchFileAsync): batch launches emit a
        // success/failure toast + status-bar text when the caller provides an
        // ILaunchFeedback surface.
        var launchFeedback = loadingStateProvider as ILaunchFeedback;
        var batchShortName = Path.GetFileName(resolvedFilePath);
        var launchedText = _localization.GetString("Launched", "launched");

        // Detect broken quoted paths inside the .bat file before running it
        // (port of the WPF launcher pre-flight check).
        IList<string> invalidPaths;
        try
        {
            invalidPaths = ValidateBatchFile.FindInvalidQuotedPathsSimple(resolvedFilePath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Batch file validation could not complete for {Path}", resolvedFilePath);
            invalidPaths = [];
        }

        if (invalidPaths.Count > 0)
        {
            // Expected user condition (broken/moved paths referenced by the .bat) — Information level.
            Log.Information("Batch file references {Count} missing path(s): {Paths}",
                invalidPaths.Count, string.Join("; ", invalidPaths));
            var shouldContinue = await _messageBox.BatchFilePathsMissingMessageBoxAsync(invalidPaths);
            if (!shouldContinue) return;
        }

        var batchWorkingDirectory = Path.GetDirectoryName(resolvedFilePath) ?? "";
        if (!string.IsNullOrEmpty(batchWorkingDirectory) && !Directory.Exists(batchWorkingDirectory))
        {
            // Expected user condition (batch file folder moved/deleted) — Information level (WPF parity).
            Log.Information("The working directory for the batch file does not exist: {WorkingDirectory}",
                batchWorkingDirectory);
        }

        Log.Debug("RunBatchFileAsync:\n\n");
        Log.Debug("Command: {Command}", resolvedFilePath);
        Log.Debug("Working Directory: {WorkingDirectory}\n", batchWorkingDirectory);

        Exception? error = null;
        await Task.Run(() =>
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = resolvedFilePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(resolvedFilePath) ?? ""
                };
                process.Start();

                // 5-minute timeout (matches the original launcher), then kill
                if (!process.WaitForExit(300_000))
                {
                    // WPF parity: the timeout path returns without a success toast.
                    try
                    {
                        // Expected user condition (a hung or long-running batch file) —
                        // Information level so it is never reported as a bug (WPF parity).
                        // Kill the whole tree (WPF parity): children would otherwise keep
                        // running and lock the files the batch file started.
                        process.Kill(true);
                        Log.Information("Batch file timed out after 5 minutes and was killed: {Path}",
                            resolvedFilePath);
                    }
                    catch (Exception killEx)
                    {
                        Log.Debug(killEx, "Failed to kill timed-out batch file {Path}", resolvedFilePath);
                    }

                    return;
                }

                if (process.ExitCode != 0 && !IsInEmulatorsToSkipList(selectedEmulatorManager.EmulatorName))
                {
                    Log.Warning("Batch file exited with code {ExitCode}: {Path}", process.ExitCode, resolvedFilePath);
                    LogBatchFileContentsOnError(resolvedFilePath);
                    launchFeedback?.ShowToast("Simple Launcher", $"Error: {batchShortName} failed");
                    launchFeedback?.SetStatusText($"Error: {batchShortName} failed");
                    return;
                }

                launchFeedback?.ShowToast("Simple Launcher", $"{batchShortName} {launchedText}");
                launchFeedback?.SetStatusText($"{batchShortName} {launchedText}");
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception win32Ex)
                {
                    // Expected user-environment/user-error Win32 conditions must not fall
                    // through to the generic Error log below — that reported them as bugs.
                    if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(win32Ex))
                    {
                        Log.Information(win32Ex, "Application control policy blocked launching batch file");
                        error = ex;
                        return;
                    }

                    if (CheckApplicationControlPolicyService.IsElevationRequired(win32Ex))
                    {
                        Log.Information(win32Ex, "Elevation required to launch batch file");
                        error = ex;
                        return;
                    }

                    if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(win32Ex))
                    {
                        Log.Information(win32Ex, "Invalid executable format: {Path}", resolvedFilePath);
                        error = ex;
                        return;
                    }
                }

                Log.Error(ex, "Failed to launch {Path}", resolvedFilePath);
                LogBatchFileContentsOnError(resolvedFilePath);
                error = ex;
            }
        });

        // Show the error on the UI thread (continuation of the awaited Task.Run)
        if (error is not null)
        {
            launchFeedback?.SetStatusText($"Error: {batchShortName} failed");
            await _messageBox.CouldNotLaunchGameMessageBoxAsync(LogFilePath());
        }
    }

    /// <inheritdoc />
    public Task LaunchShortcutFileAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext)
    {
        return LaunchShortcutFileAsync(resolvedFilePath, selectedEmulatorManager, windowContext, null);
    }

    /// <summary>
    ///     Launches a shortcut file (.LNK or .URL) with launch feedback (toast + status
    ///     text) when the caller provides an <see cref="ILoadingState" /> that also
    ///     implements <see cref="SimpleLauncher.Avalonia.Interfaces.ILaunchFeedback" />.
    /// </summary>
    public async Task LaunchShortcutFileAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext,
        ILoadingState? loadingStateProvider = null)
    {
        // WPF parity (GameLauncherService.LaunchShortcutFileAsync): optimistic
        // "{fileName} launched" toast + status text before the launch attempt.
        if (loadingStateProvider is ILaunchFeedback launchFeedback)
        {
            var launchedText = _localization.GetString("Launched", "launched");
            var fileName = Path.GetFileName(resolvedFilePath);
            launchFeedback.ShowToast("Simple Launcher", $"{fileName} {launchedText}");
            launchFeedback.SetStatusText($"{fileName} {launchedText}");
        }

        // Validate the shortcut itself exists (long-path safe on Windows)
        var shortcutExists = File.Exists(resolvedFilePath) ||
                             (OperatingSystem.IsWindows() && File.Exists(PathHelper.GetLongPath(resolvedFilePath)));
        if (!shortcutExists)
        {
            Log.Information("Shortcut file not found: {Path}", resolvedFilePath);
            await _messageBox.CustomErrorMessageBoxAsync($"Shortcut file not found: {resolvedFilePath}",
                _localization.GetString("LaunchErrorTitle", "Launch Error"));
            return;
        }

        string? targetUrl = null;
        if (Path.GetExtension(resolvedFilePath).Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = ExtractUrlFromShortcutFile(resolvedFilePath);
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                Log.Information("Invalid .url file format or missing URL in: {Path}", resolvedFilePath);
                await _messageBox.CustomErrorMessageBoxAsync($"Invalid .url file: {resolvedFilePath}",
                    _localization.GetString("LaunchErrorTitle", "Launch Error"));
                return;
            }

            // Verify the protocol handler is registered ONLY for real URIs (contains "://")
            // so drive letters (C:\) are not treated as protocols. Windows-only: the
            // HKEY_CLASSES_ROOT shell-registration concept does not exist on Linux.
            var protocolIndex = targetUrl.IndexOf("://", StringComparison.Ordinal);
            if (protocolIndex > 0 && OperatingSystem.IsWindows())
            {
                var protocol = targetUrl[..protocolIndex];
                if (!IsProtocolRegistered(protocol))
                {
                    Log.Information(
                        "Protocol handler '{Protocol}' is not registered. Cannot launch: {Url}",
                        protocol, targetUrl);
                    await _messageBox.ProtocolHandlerNotRegisteredMessageBoxAsync(protocol);
                    return;
                }
            }
        }

        if (targetUrl is not null)
        {
            Log.Debug("LaunchShortcutFileAsync (.URL): Shortcut File: {ShortcutFile}, Target URL: {TargetUrl}",
                resolvedFilePath, targetUrl);
        }
        else
        {
            Log.Debug(
                "LaunchShortcutFileAsync (.LNK): Shortcut File: {ShortcutFile}, Working Directory: {WorkingDirectory}",
                resolvedFilePath, Path.GetDirectoryName(resolvedFilePath) ?? AppDomain.CurrentDomain.BaseDirectory);
        }

        Exception? error = null;
        await Task.Run(() =>
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    // Launch the extracted URL directly for .url files, not the shortcut itself
                    FileName = targetUrl ?? resolvedFilePath,
                    UseShellExecute = true
                };
                process.Start();
            }
            catch (Exception ex)
            {
                // Expected user-environment/user-error Win32 conditions must not fall
                // through to the generic Error log below — that reported them as bugs.
                if (ex is Win32Exception win32Ex)
                {
                    if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(win32Ex))
                    {
                        Log.Information(win32Ex, "Application control policy blocked launching shortcut file");
                        error = ex;
                        return;
                    }

                    if (CheckApplicationControlPolicyService.IsElevationRequired(win32Ex))
                    {
                        Log.Information(win32Ex, "Elevation required to launch shortcut file");
                        error = ex;
                        return;
                    }

                    if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(win32Ex))
                    {
                        Log.Information(win32Ex, "Invalid executable format: {Path}", resolvedFilePath);
                        error = ex;
                        return;
                    }
                }

                Log.Error(ex, "Failed to launch {Path}", resolvedFilePath);
                error = ex;
            }
        });

        // Show the error on the UI thread (continuation of the awaited Task.Run)
        if (error is not null) await _messageBox.CouldNotLaunchGameMessageBoxAsync(LogFilePath());
    }

    public async Task LaunchExecutableAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext)
    {
        var startTime = DateTime.Now;
        Log.Debug("LaunchExecutableAsync:\n\n");
        Log.Debug("Executable File: {ExecutableFile}", resolvedFilePath);
        Log.Debug("Working Directory: {WorkingDirectory}\n", Path.GetDirectoryName(resolvedFilePath) ?? "");
        Exception? error = null;
        await Task.Run(() =>
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = resolvedFilePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(resolvedFilePath) ?? ""
                };
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0 && !IsInEmulatorsToSkipList(selectedEmulatorManager.EmulatorName))
                    Log.Warning("Executable exited with code {ExitCode}: {Path}", process.ExitCode, resolvedFilePath);
            }
            catch (Exception ex)
            {
                // Expected user-environment/user-error Win32 conditions must not fall
                // through to the generic Error log below — that reported them as bugs.
                if (ex is Win32Exception win32Ex)
                {
                    if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(win32Ex))
                    {
                        Log.Information(win32Ex, "Application control policy blocked launching executable");
                        error = ex;
                        return;
                    }

                    if (CheckApplicationControlPolicyService.IsElevationRequired(win32Ex))
                    {
                        Log.Information(win32Ex, "Elevation required to launch executable");
                        error = ex;
                        return;
                    }

                    if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(win32Ex))
                    {
                        Log.Information(win32Ex, "Invalid executable format: {Path}", resolvedFilePath);
                        error = ex;
                        return;
                    }
                }

                Log.Error(ex, "Failed to launch {Path}", resolvedFilePath);
                error = ex;
            }
        });
        LastPlayTime = DateTime.Now - startTime;

        // Show the error on the UI thread (continuation of the awaited Task.Run)
        if (error is not null) await _messageBox.CouldNotLaunchGameMessageBoxAsync(LogFilePath());
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Updates play time statistics, records play history, and reports to the stats API.
    ///     Port of the WPF GameLauncherService.UpdateStatsAndPlayCountAsync.
    /// </summary>
    private async Task UpdateStatsAndPlayCountAsync(
        TimeSpan playTime, string filePath, string systemName,
        string emulatorName, ILoadingState? loadingStateProvider = null)
    {
        // Update per-system play time in settings
        try
        {
            _settings.UpdateSystemPlayTime(systemName, playTime);
            await _settings.SaveAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating system play time in settings");
        }

        // WPF parity: show "Playtime: h:mm:ss" toast and clear the status bar.
        if (loadingStateProvider is ILaunchFeedback launchFeedback)
        {
            var totalHours = (int)playTime.TotalHours;
            var playTimeFormatted = string.Format(
                CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}",
                totalHours, playTime.Minutes, playTime.Seconds);
            var playTimeLabel = _localization.GetString("Playtime", "Playtime:");
            launchFeedback.ShowToast("Simple Launcher", $"{playTimeLabel} {playTimeFormatted}");
            launchFeedback.SetStatusText("");
        }

        // Record play history
        try
        {
            await _playHistoryManager.RecordPlayAsync(filePath, systemName, (long)playTime.TotalSeconds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating play history");
        }

        // Report to stats API (fire-and-forget)
        _ = _stats.CallApiAsync(emulatorName);
    }

    /// <inheritdoc />
    public void ReportGameLaunchStarted()
    {
        // No-op in Avalonia: play history is recorded inside LaunchRegularEmulatorAsync,
        // which only runs on paths where a launch actually starts.
    }

    // ── Post-exit error analysis constants ──
    private const int MemoryAccessViolation = -1073741819;
    private const int DepViolation = -1073740791;

    /// <summary>
    ///     Post-exit error analysis — port of the WPF GameLauncherService's
    ///     DoNotCheckErrorsOnSpecificEmulators + CheckForMemoryAccessViolationAsync +
    ///     CheckForDepViolationAsync + CheckForExitCodeWithErrorAnyAsync pipeline.
    /// </summary>
    private async Task AnalyzeProcessExitAsync(
        int exitCode,
        string stdout,
        string stderr,
        string emulatorPath,
        string arguments,
        string emulatorName,
        string? emulatorLocation,
        Emulator selectedEmulatorManager,
        ISystemManager selectedSystemManager,
        ILoadingState? loadingStateProvider)
    {
        // Skip error checking for known problematic emulators
        if (IsInEmulatorsToSkipList(emulatorName) || IsInEmulatorsToSkipList(Path.GetFileName(emulatorPath)))
        {
            Log.Information(
                "User just ran {Emulator}. Simple Launcher does not track error codes for this emulator.\n" +
                "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}",
                emulatorName, exitCode, emulatorPath, arguments);
            return;
        }

        switch (exitCode)
        {
            // Success — nothing to analyze
            case 0:
                return;
            // Memory access violation — log only, no user notification
            case MemoryAccessViolation:
                Log.Warning(
                    "Memory access violation error running the emulator.\n" +
                    "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
                    "Stdout: {Stdout}\nStderr: {Stderr}",
                    exitCode, emulatorPath, arguments, stdout, stderr);
                return;
            // DEP violation — log only, no user notification
            case DepViolation:
                Log.Information("Data Execution Prevention (DEP) violation error running the emulator.\n" +
                            "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
                            "Stdout: {Stdout}\nStderr: {Stderr}",
                    exitCode, emulatorPath, arguments, stdout, stderr);
                return;
        }

        var combinedOutput = stdout + "\n" + stderr;

        // Ignore RetroArch "File open/read error" — not actionable
        if (combinedOutput.Contains("File open/read error", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("Ignored exit code {ExitCode} due to 'File open/read error' in output", exitCode);
            return;
        }

        var isRetroArch = emulatorName.Contains("retroarch", StringComparison.OrdinalIgnoreCase) ||
                          (emulatorLocation ?? "").Contains("retroarch", StringComparison.OrdinalIgnoreCase);
        var isMame = emulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
                     (emulatorLocation ?? "").Contains("mame", StringComparison.OrdinalIgnoreCase) ||
                     (emulatorLocation ?? "").Contains("mame64", StringComparison.OrdinalIgnoreCase);

        switch (isRetroArch)
        {
            // RetroArch mkdir permission denied (special characters in path)
            case true when
                combinedOutput.Contains("mkdir(", StringComparison.OrdinalIgnoreCase) &&
                combinedOutput.Contains("Permission denied", StringComparison.OrdinalIgnoreCase):
            {
                Log.Debug("RetroArch mkdir permission denied due to special characters in path");
                Log.Warning(
                    "RetroArch special characters error.\n" +
                    "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
                    "Stdout: {Stdout}\nStderr: {Stderr}",
                    exitCode, emulatorPath, arguments, stdout, stderr);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBox.RetroArchSpecialCharactersInPathMessageBoxAsync();
                    await _messageBox.WouldYouLikeToOpenTheLogMessageBoxAsync(LogFilePath());
                    await _askAiToFixParameters.ExecuteAsync(
                        selectedSystemManager, selectedEmulatorManager, loadingStateProvider);
                }

                return;
            }
            // RetroArch generic parameter issues
            case true:
            {
                Log.Debug("RetroArch parameter issues detected");
                Log.Warning(
                    "RetroArch parameter issue.\n" +
                    "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
                    "Stdout: {Stdout}\nStderr: {Stderr}",
                    exitCode, emulatorPath, arguments, stdout, stderr);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBox.RetroArchParameterIssueMessageBoxAsync(LogFilePath());
                    await _askAiToFixParameters.ExecuteAsync(
                        selectedSystemManager, selectedEmulatorManager, loadingStateProvider);
                }

                return;
            }
        }

        switch (isMame)
        {
            // MAME ROM set error
            case true when
                combinedOutput.Contains("Not Found", StringComparison.OrdinalIgnoreCase) ||
                combinedOutput.Contains("WRONG LENGTH", StringComparison.OrdinalIgnoreCase) ||
                combinedOutput.Contains("Required files are missing", StringComparison.OrdinalIgnoreCase):
            {
                Log.Debug("MAME ROM set error detected");
                Log.Information("MAME ROM set error.\n" +
                            "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
                            "Stdout: {Stdout}\nStderr: {Stderr}",
                    exitCode, emulatorPath, arguments, stdout, stderr);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBox.MameRomSetErrorMessageBoxAsync();
                    await _messageBox.WouldYouLikeToOpenTheLogMessageBoxAsync(LogFilePath());
                    await _askAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager, loadingStateProvider);
                }

                return;
            }
            // MAME Unknown system
            case true when
                combinedOutput.Contains("Unknown system", StringComparison.OrdinalIgnoreCase) ||
                combinedOutput.Contains("approximately matches the following", StringComparison.OrdinalIgnoreCase):
            {
                Log.Debug("MAME Unknown system error detected");
                Log.Information("MAME Unknown system error.\n" +
                            "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
                            "Stdout: {Stdout}\nStderr: {Stderr}", exitCode, emulatorPath, arguments, stdout, stderr);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBox.MameUnknownSystemErrorMessageBoxAsync();
                    await _messageBox.WouldYouLikeToOpenTheLogMessageBoxAsync(LogFilePath());
                    await _askAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager, loadingStateProvider);
                }

                return;
            }
            // MAME Unable to load image
            case true when
                combinedOutput.Contains("Unable to load image", StringComparison.OrdinalIgnoreCase) ||
                combinedOutput.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase):
            {
                Log.Debug("MAME Unable to load image error detected");
                Log.Information("MAME Unable to load image error.\n" +
                            "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
                            "Stdout: {Stdout}\nStderr: {Stderr}",
                    exitCode, emulatorPath, arguments, stdout, stderr);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBox.MameUnableToLoadImageMessageBoxAsync();
                    await _messageBox.WouldYouLikeToOpenTheLogMessageBoxAsync(LogFilePath());
                    await _askAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager, loadingStateProvider);
                }

                return;
            }
            // MAME corrupted INI (auto-restore from sample)
            case true when
                stderr.Contains("Warning: unknown option in INI", StringComparison.OrdinalIgnoreCase):
            {
                Log.Debug("MAME unknown option in INI detected. Attempting to restore mame.ini from sample");
                var restored = MameConfigurationService.RestoreMameIniFromSample(emulatorPath, Log.Logger);
                if (restored)
                    Log.Debug("mame.ini restored successfully. User should retry");
                else
                    Log.Debug("Failed to restore mame.ini from sample");

                return;
            }
        }

        // Generic fallback — any other non-zero exit code
        Log.Debug("Exit code {ExitCode} detected for {Emulator}", exitCode, emulatorName);
        Log.Warning(
            "Emulator exited with error.\n" +
            "Exit code: {ExitCode}, Emulator: {EmulatorPath}, Parameters: {Arguments}\n" +
            "Stdout: {Stdout}\nStderr: {Stderr}",
            exitCode, emulatorPath, arguments, stdout, stderr);

        if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
        {
            await _messageBox.CouldNotLaunchGameMessageBoxAsync(LogFilePath());
            await _askAiToFixParameters.ExecuteAsync(
                selectedSystemManager, selectedEmulatorManager, loadingStateProvider);
        }
    }

    /// <summary>
    ///     Determines which launchable file to look for inside a mounted CHD image for the
    ///     given emulator, and whether the image should be mounted at all. Emulators that
    ///     read .chd natively (RetroArch, DuckStation, PCSX2, ...) return <see cref="ChdGameFileKind.None" />.
    ///     Mirrors the emulator gate of the WPF ChdMountStrategy.
    /// </summary>
    private static ChdGameFileKind GetChdGameFileKind(string emulatorName, string? emulatorLocation)
    {
        var loc = emulatorLocation ?? string.Empty;

        if (emulatorName.Contains("RetroArch", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("retroarch.exe", StringComparison.OrdinalIgnoreCase))
        {
            return ChdGameFileKind.None;
        }

        if (emulatorName.Contains("RPCS3", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("rpcs3", StringComparison.OrdinalIgnoreCase))
        {
            return ChdGameFileKind.EbootBin;
        }

        if (emulatorName.Contains("Xenia", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("xenia", StringComparison.OrdinalIgnoreCase))
        {
            return ChdGameFileKind.DefaultXex;
        }

        if (emulatorName.Contains("Xemu", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("xemu", StringComparison.OrdinalIgnoreCase))
        {
            return ChdGameFileKind.ImageIso;
        }

        if (emulatorName.Contains("Cxbx", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("cxbx", StringComparison.OrdinalIgnoreCase))
        {
            return ChdGameFileKind.DefaultXbe;
        }

        if (emulatorName.Contains("Gens", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("gens.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("CDi", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("CD-i", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("wcdiemu", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Kega Fusion", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Fusion", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("fusion.exe", StringComparison.OrdinalIgnoreCase))
        {
            return ChdGameFileKind.BinFile;
        }

        if (emulatorName.Contains("Genesis Plus GX", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("gen_sdl.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("4do", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("4do.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("blastem", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("blastem.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("FBAlpha", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("FB Alpha", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("FinalBurnAlpha", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Final Burn Alpha", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("fba64.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("FBNeo", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("FB Neo", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("FinalBurnNeo", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Final Burn Neo", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("fbneo64.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Mednafen", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("mednafen", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Mesen", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("Mesen.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Nebula", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("nebula.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("PCSX-Redux", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("PCSX Redux", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("pcsx-redux", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("PicoDrive", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Pico Drive", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("PicoDrive.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("raine", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("raine.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Tsugaru", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("Tsugaru_CUI.exe", StringComparison.OrdinalIgnoreCase) ||
            emulatorName.Contains("Yabause", StringComparison.OrdinalIgnoreCase) ||
            loc.Contains("yabause.exe", StringComparison.OrdinalIgnoreCase))
        {
            return ChdGameFileKind.CueFile;
        }

        return ChdGameFileKind.None;
    }

    /// <summary>
    ///     Locates the launchable file inside a mounted CHD drive for the given emulator
    ///     (mirror of the WPF ChdMountStrategy file selection).
    /// </summary>
    private static string? FindGameFileInMountedChd(string mountedPath, ChdGameFileKind kind)
    {
        return kind switch
        {
            ChdGameFileKind.EbootBin => FindEbootBin.FindEbootBinRecursive(mountedPath, Log.Logger, Log.Logger),
            ChdGameFileKind.DefaultXex => FindDefaultXex.Find(mountedPath, Log.Logger),
            ChdGameFileKind.ImageIso => FindImageIso.Find(mountedPath, Log.Logger),
            ChdGameFileKind.DefaultXbe => FindDefaultXbe.Find(mountedPath, Log.Logger),
            ChdGameFileKind.BinFile => FindBinFile.Find(mountedPath, Log.Logger),
            ChdGameFileKind.CueFile => FindCueFile.Find(mountedPath, Log.Logger),
            _ => null
        };
    }

    /// <summary>
    ///     Kinds of launchable files that can be located inside a mounted CHD image.
    /// </summary>
    private enum ChdGameFileKind
    {
        None,
        EbootBin,
        DefaultXex,
        ImageIso,
        DefaultXbe,
        BinFile,
        CueFile
    }

    private static void LogBatchFileContentsOnError(string batchFilePath)
    {
        try
        {
            if (File.Exists(batchFilePath))
            {
                var contents = File.ReadAllText(batchFilePath);
                Log.Debug("Batch file contents for '{BatchFilePath}':\n{Contents}", batchFilePath, contents);
                Log.Debug("End of batch file contents");
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read batch file contents for logging: {BatchFilePath}", batchFilePath);
        }
    }

    /// <summary>
    ///     Extracts the URL from a .url internet shortcut file (URL=... line).
    /// </summary>
    private static string? ExtractUrlFromShortcutFile(string shortcutPath)
    {
        try
        {
            foreach (var line in File.ReadAllLines(shortcutPath))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    return line["URL=".Length..].Trim();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read .url shortcut {Path}", shortcutPath);
        }

        return null;
    }

    /// <summary>
    ///     Checks the Windows registry (HKEY_CLASSES_ROOT) for a registered handler for the
    ///     given URL protocol. Port of the WPF GameLauncherService.IsProtocolRegistered.
    ///     Callers must guard with <see cref="OperatingSystem.IsWindows()" />.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool IsProtocolRegistered(string protocol)
    {
        if (string.IsNullOrEmpty(protocol)) return false;

        try
        {
            // Protocol names are case-insensitive in the registry, but typically stored lowercase
            using var protocolKey = Registry.ClassesRoot.OpenSubKey(protocol.ToLowerInvariant());
            if (protocolKey == null)
            {
                Log.Debug("[IsProtocolRegistered] Protocol key '{Protocol}' not found in HKEY_CLASSES_ROOT",
                    protocol.ToLowerInvariant());
                return false;
            }

            // A protocol is considered "registered" if it has a command handler (shell\open\command)
            using var shellOpenCommandKey = protocolKey.OpenSubKey(@"shell\open\command");
            if (shellOpenCommandKey == null)
            {
                Log.Debug(@"[IsProtocolRegistered] 'shell\open\command' subkey not found for protocol '{Protocol}'.",
                    protocol.ToLowerInvariant());
                return false;
            }

            var command = shellOpenCommandKey.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(command))
            {
                Log.Debug("[IsProtocolRegistered] Command handler is empty for protocol '{Protocol}'",
                    protocol.ToLowerInvariant());
                return false;
            }

            Log.Debug("[IsProtocolRegistered] Protocol '{Protocol}' is registered with command: '{Command}'",
                protocol.ToLowerInvariant(), command);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking if protocol '{Protocol}' is registered", protocol);
            Log.Debug(ex, "[IsProtocolRegistered] Error checking protocol '{Protocol}':", protocol);
            return false;
        }
    }

    private bool IsInEmulatorsToSkipList(string? emulatorName)
    {
        if (string.IsNullOrWhiteSpace(emulatorName)) return false;

        // WPF parity: substring matching (any skip-list entry contained in the emulator name)
        return _emulatorsToSkipErrorChecking.Any(skip =>
            emulatorName.Contains(skip, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Determines whether the configured emulator location refers to the Ymir/Yumir
    ///     emulator. Matches the executable's file name or its containing folder name
    ///     exactly ("Ymir"/"Yumir") instead of a substring of the whole path, so unrelated
    ///     paths that merely contain "ymir" (e.g. "...\Skymir\...") do not force extraction.
    /// </summary>
    /// <param name="emulatorLocation">The configured emulator executable path (may be a folder).</param>
    /// <returns>True when the location points at (or into) a Ymir/Yumir installation.</returns>
    private static bool IsYmirEmulatorLocation(string? emulatorLocation)
    {
        if (string.IsNullOrWhiteSpace(emulatorLocation)) return false;

        var fileName = Path.GetFileNameWithoutExtension(emulatorLocation);
        if (fileName.Equals("Ymir", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Yumir", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentPath =
            Path.GetDirectoryName(emulatorLocation.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        var parentName = Path.GetFileName(parentPath ?? string.Empty);
        return parentName.Equals("Ymir", StringComparison.OrdinalIgnoreCase) ||
               parentName.Equals("Yumir", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}