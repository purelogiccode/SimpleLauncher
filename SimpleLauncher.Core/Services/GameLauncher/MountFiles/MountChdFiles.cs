using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Mounts CHD (Compressed Hunks of Data) disc images using CHDMounter and the Dokan filesystem driver.
/// </summary>
public class MountChdFiles : IMountChdFiles
{
    private const string ChdMounterRelativeDirectory = @"tools\CHDMounter";

    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MountChdFiles" /> class.
    /// </summary>
    public MountChdFiles(ILogger logger)
    {
        _logger = logger;
    }

    private static string ChdMounterRelativePath => GetArchitectureSpecificExecutableRelativePath();

    /// <summary>
    ///     Mounts a CHD file and returns a disposable drive handle with the mounted path and drive letter.
    /// </summary>
    public async Task<MountChdDrive> MountAsync(string resolvedChdFilePath, string? consoleAlias, ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        _logger.Debug(
            $"[MountChdFiles.MountAsync] Starting to mount CHD: {resolvedChdFilePath} (ConsoleAlias: {consoleAlias ?? "default"})");

        var resolvedToolPath = PathHelper.ResolveRelativeToAppDirectory(ChdMounterRelativePath);

        _logger.Debug($"[MountChdFiles.MountAsync] Path to CHDMounter: {resolvedToolPath}");

        if (string.IsNullOrWhiteSpace(resolvedToolPath) || !File.Exists(resolvedToolPath))
        {
            var errorMessage = $"CHDMounter not found at {ChdMounterRelativePath}. Cannot mount CHD.";
            _logger.Debug($"[MountChdFiles.MountAsync] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return new MountChdDrive(logErrors, _logger);
        }

        if (!OperatingSystem.IsWindows() || !DokanValidation.IsDokanInstalled())
        {
            const string errorMessage = "Dokan driver not found. Cannot mount CHD.";
            _logger.Debug($"[MountChdFiles.MountAsync] Error: {errorMessage}");
            logErrors.Information(errorMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return new MountChdDrive(logErrors, _logger);
        }

        var existingDrives = GetCurrentDriveLetters();
        _logger.Debug($"[MountChdFiles.MountAsync] Existing drives before mount: {string.Join(", ", existingDrives)}");

        var arguments = !string.IsNullOrEmpty(consoleAlias)
            ? $"/a \"{resolvedChdFilePath}\" /s:{consoleAlias}"
            : $"/a \"{resolvedChdFilePath}\"";

        var psiMount = new ProcessStartInfo
        {
            FileName = resolvedToolPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(resolvedToolPath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        _logger.Debug("[MountChdFiles.MountAsync] Using auto-select drive letter (/a)");
        _logger.Debug($"[MountChdFiles.MountAsync] Arguments: {psiMount.Arguments}");

        var mountProcess = new Process { StartInfo = psiMount, EnableRaisingEvents = true };
        var errorOutput = new List<string>();

        try
        {
            if (!mountProcess.Start()) throw new InvalidOperationException("Failed to start the CHDMounter process.");

            mountProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorOutput.Add(e.Data);
            };
            mountProcess.BeginErrorReadLine();

            _logger.Debug($"[MountChdFiles.MountAsync] CHDMounter process started (ID: {mountProcess.Id}).");

            var chdFileName = Path.GetFileName(resolvedChdFilePath);
            var (mountSuccessful, detectedDrive, exitCode) = await WaitForDriveMountAndDetectAsync(existingDrives,
                mountProcess, mountProcess.Id, logErrors, errorOutput, chdFileName);

            if (!mountSuccessful || detectedDrive == null)
            {
                if (!mountProcess.HasExited) mountProcess.Kill(true);

                mountProcess.Dispose();
                await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync(exitCode);
                return new MountChdDrive(logErrors, _logger);
            }

            var driveRoot = $"{detectedDrive.Value}:\\";
            _logger.Debug($"[MountChdFiles.MountAsync] CHD mounted successfully. Drive: {driveRoot}");
            return new MountChdDrive(mountProcess, driveRoot, detectedDrive.Value.ToString(), logErrors, _logger);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountChdFiles.MountAsync] Exception during CHD mounting: {ex}");
            var contextMessage = $"Error during CHD mount process for {resolvedChdFilePath}.\nException: {ex.Message}";
            logErrors.Error(ex, contextMessage);

            // HasExited throws InvalidOperationException when Start() itself failed
            // (no process associated) — treat that as "nothing to kill" so the
            // original error isn't masked (CORE-31).
            bool hasExited;
            try
            {
                hasExited = mountProcess.HasExited;
            }
            catch (InvalidOperationException)
            {
                hasExited = true;
            }

            if (!hasExited)
            {
                try
                {
                    mountProcess.Kill(true);
                }
                catch
                {
                    /* ignore */
                }
            }

            mountProcess.Dispose();

            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return new MountChdDrive(logErrors, _logger);
        }
    }

    /// <summary>
    ///     Mounts a CHD file, locates a game file within the mounted drive, launches the emulator, and unmounts on exit.
    /// </summary>
    public async Task MountChdFileAndLoadAsync(
        string resolvedChdFilePath,
        string selectedSystemName,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        ILauncherService gameLauncher,
        ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        _logger.Debug($"[MountChdFiles] Starting to mount CHD for game loading: {resolvedChdFilePath}");
        _logger.Debug($"[MountChdFiles] System: {selectedSystemName}, Emulator: {selectedEmulatorName}");

        var resolvedToolPath = PathHelper.ResolveRelativeToAppDirectory(ChdMounterRelativePath);

        _logger.Debug($"[MountChdFiles] Path to CHDMounter: {resolvedToolPath}");

        if (string.IsNullOrWhiteSpace(resolvedToolPath) || !File.Exists(resolvedToolPath))
        {
            var errorMessage = $"CHDMounter not found at {ChdMounterRelativePath}. Cannot mount CHD.";
            _logger.Debug($"[MountChdFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return;
        }

        if (!OperatingSystem.IsWindows() || !DokanValidation.IsDokanInstalled())
        {
            const string errorMessage = "Dokan driver not found. Cannot mount CHD.";
            _logger.Debug($"[MountChdFiles] Error: {errorMessage}");
            logErrors.Information(errorMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return;
        }

        var existingDrives = GetCurrentDriveLetters();
        _logger.Debug($"[MountChdFiles] Existing drives before mount: {string.Join(", ", existingDrives)}");

        var psiMount = new ProcessStartInfo
        {
            FileName = resolvedToolPath,
            Arguments = $"/a \"{resolvedChdFilePath}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(resolvedToolPath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        _logger.Debug("[MountChdFiles] ProcessStartInfo:");
        _logger.Debug($"[MountChdFiles] FileName: {psiMount.FileName}");
        _logger.Debug($"[MountChdFiles] Arguments: {psiMount.Arguments}");
        _logger.Debug($"[MountChdFiles] WorkingDirectory: {psiMount.WorkingDirectory}");

        Process? mountProcess = null;
        var mountProcessId = -1;
        string? driveRoot = null;
        var errorOutput = new List<string>();

        try
        {
            mountProcess = new Process { StartInfo = psiMount, EnableRaisingEvents = true };

            _logger.Debug("[MountChdFiles] Starting CHDMounter process...");
            if (!mountProcess.Start()) throw new InvalidOperationException("Failed to start the CHDMounter process.");

            mountProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorOutput.Add(e.Data);
            };
            mountProcess.BeginErrorReadLine();

            mountProcessId = mountProcess.Id;
            _logger.Debug($"[MountChdFiles] CHDMounter process started (ID: {mountProcessId}).");

            var chdFileName = Path.GetFileName(resolvedChdFilePath);
            var (mountSuccessful, drive, exitCode) = await WaitForDriveMountAndDetectAsync(existingDrives, mountProcess,
                mountProcessId, logErrors, errorOutput, chdFileName);

            if (!mountSuccessful || drive == null)
            {
                if (!mountProcess.HasExited) mountProcess.Kill(true);

                mountProcess.Dispose();
                await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync(exitCode);
                return;
            }

            driveRoot = $"{drive.Value}:\\";
            _logger.Debug(
                $"[MountChdFiles] CHD mounted successfully on drive {drive.Value}:. Searching for game file...");

            var gameFilePath = FindGameFile(driveRoot, logErrors);

            if (string.IsNullOrEmpty(gameFilePath))
            {
                _logger.Debug($"[MountChdFiles] No suitable game file found in {driveRoot}.");
                throw new FileNotFoundException($"Could not find a game file within the mounted CHD at {driveRoot}.");
            }

            _logger.Debug(
                $"[MountChdFiles] Game file found at: {gameFilePath}. Proceeding to launch with {selectedEmulatorName}.");

            await gameLauncher.LaunchRegularEmulatorAsync(gameFilePath, selectedEmulatorName, selectedSystemManager,
                selectedEmulatorManager, rawEmulatorParameters, windowContext, null, resolvedChdFilePath);

            _logger.Debug($"[MountChdFiles] Emulator for {gameFilePath} has exited.");
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountChdFiles] Exception during CHD mounting or launching: {ex}");
            var exitCodeInfoInCatch = mountProcess is { HasExited: true }
                ? $"Exit Code: {mountProcess.ExitCode}"
                : "Process was still running or state unknown.";
            var contextMessage = $"Error during CHD mount/launch process for {resolvedChdFilePath}.\n" +
                                 $"Exception: {ex.Message}\n" +
                                 $"{exitCodeInfoInCatch}";
            logErrors.Error(ex, contextMessage);

            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
        }
        finally
        {
            _logger.Debug(
                $"[MountChdFiles] Entering finally block for {resolvedChdFilePath}. Mount Process ID: {mountProcessId}");
            if (mountProcess != null && mountProcessId != -1 && !mountProcess.HasExited)
            {
                _logger.Debug(
                    $"[MountChdFiles] Attempting to unmount by terminating CHDMounter (ID: {mountProcessId}).");
                try
                {
                    mountProcess.Kill(true);
                    _logger.Debug(
                        $"[MountChdFiles] Kill signal sent to CHDMounter (ID: {mountProcessId}). Waiting for process to exit (up to 20s).");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    try
                    {
                        await mountProcess.WaitForExitAsync(cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Debug(
                            $"[MountChdFiles] Timeout (10s) waiting for CHDMounter (ID: {mountProcessId}) to exit after Kill.");
                    }

                    if (mountProcess.HasExited)
                    {
                        _logger.Debug(
                            $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) terminated. Exit code: {mountProcess.ExitCode.ToString(CultureInfo.InvariantCulture)}.");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) did NOT terminate after Kill signal and 10s wait.");
                    }
                }
                catch (InvalidOperationException ioEx)
                {
                    if (ioEx.Message.Contains("process has already exited", StringComparison.OrdinalIgnoreCase) ||
                        ioEx.Message.Contains("No process is associated", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Debug(
                            $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) already exited: {ioEx.Message}");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountChdFiles] InvalidOperationException while terminating CHDMounter (ID: {mountProcessId}): {ioEx}");
                        logErrors.Error(ioEx, "Unexpected InvalidOperationException during CHDMounter termination.");
                    }
                }
                catch (Exception termEx)
                {
                    _logger.Debug(
                        $"[MountChdFiles] Exception while terminating CHDMounter (ID: {mountProcessId}): {termEx}");
                    logErrors.Error(termEx, $"Failed to terminate CHDMounter (ID: {mountProcessId}) for unmounting.");
                }
            }
            else if (mountProcessId != -1)
            {
                var exitCodeStr = mountProcess is { HasExited: true }
                    ? mountProcess.ExitCode.ToString(CultureInfo.InvariantCulture)
                    : "N/A";
                _logger.Debug(
                    $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) had already exited. Exit code likely {exitCodeStr}.");
            }
            else
            {
                _logger.Debug(
                    "[MountChdFiles] CHDMounter process was not started successfully. No termination needed.");
            }

            mountProcess?.Dispose();

            await Task.Delay(2000);
            if (Directory.Exists(driveRoot))
                _logger.Debug($"[MountChdFiles] WARNING: Drive {driveRoot} still exists after attempting to unmount.");
            else
                _logger.Debug($"[MountChdFiles] Drive {driveRoot} successfully unmounted.");
        }
    }

    /// <summary>
    ///     Mounts a CHD file with an explicit console alias, locates a game file, launches the emulator, and unmounts on exit.
    /// </summary>
    public async Task MountChdFileAndLoadWithConsoleAliasAsync(
        string resolvedChdFilePath,
        string selectedSystemName,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        ILauncherService gameLauncher,
        string? consoleAlias,
        ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        _logger.Debug(
            $"[MountChdFiles] Starting to mount CHD with console alias for game loading: {resolvedChdFilePath}");
        _logger.Debug(
            $"[MountChdFiles] System: {selectedSystemName}, Emulator: {selectedEmulatorName}, ConsoleAlias: {consoleAlias ?? "auto"}");

        var resolvedToolPath = PathHelper.ResolveRelativeToAppDirectory(ChdMounterRelativePath);

        _logger.Debug($"[MountChdFiles] Path to CHDMounter: {resolvedToolPath}");

        if (string.IsNullOrWhiteSpace(resolvedToolPath) || !File.Exists(resolvedToolPath))
        {
            var errorMessage = $"CHDMounter not found at {ChdMounterRelativePath}. Cannot mount CHD.";
            _logger.Debug($"[MountChdFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return;
        }

        if (!OperatingSystem.IsWindows() || !DokanValidation.IsDokanInstalled())
        {
            const string errorMessage = "Dokan driver not found. Cannot mount CHD.";
            _logger.Debug($"[MountChdFiles] Error: {errorMessage}");
            logErrors.Information(errorMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return;
        }

        var existingDrives = GetCurrentDriveLetters();
        _logger.Debug($"[MountChdFiles] Existing drives before mount: {string.Join(", ", existingDrives)}");

        var arguments = !string.IsNullOrEmpty(consoleAlias)
            ? $"\"{resolvedChdFilePath}\" /a /s:{consoleAlias}"
            : $"\"{resolvedChdFilePath}\" /a";

        var psiMount = new ProcessStartInfo
        {
            FileName = resolvedToolPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(resolvedToolPath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        _logger.Debug("[MountChdFiles] ProcessStartInfo:");
        _logger.Debug($"[MountChdFiles] FileName: {psiMount.FileName}");
        _logger.Debug($"[MountChdFiles] Arguments: {psiMount.Arguments}");
        _logger.Debug($"[MountChdFiles] WorkingDirectory: {psiMount.WorkingDirectory}");

        Process? mountProcess = null;
        var mountProcessId = -1;
        string? driveRoot = null;
        var errorOutput = new List<string>();

        try
        {
            mountProcess = new Process { StartInfo = psiMount, EnableRaisingEvents = true };

            _logger.Debug("[MountChdFiles] Starting CHDMounter process...");
            if (!mountProcess.Start()) throw new InvalidOperationException("Failed to start the CHDMounter process.");

            mountProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorOutput.Add(e.Data);
            };
            mountProcess.BeginErrorReadLine();

            mountProcessId = mountProcess.Id;
            _logger.Debug($"[MountChdFiles] CHDMounter process started (ID: {mountProcessId}).");

            var chdFileName = Path.GetFileName(resolvedChdFilePath);
            var (mountSuccessful, drive, exitCode) = await WaitForDriveMountAndDetectAsync(existingDrives, mountProcess,
                mountProcessId, logErrors, errorOutput, chdFileName);

            if (!mountSuccessful || drive == null)
            {
                if (!mountProcess.HasExited) mountProcess.Kill(true);

                mountProcess.Dispose();
                await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync(exitCode);
                return;
            }

            driveRoot = $"{drive.Value}:\\";
            _logger.Debug(
                $"[MountChdFiles] CHD mounted successfully on drive {drive.Value}:. Searching for game file...");

            var gameFilePath = FindGameFile(driveRoot, logErrors);

            if (string.IsNullOrEmpty(gameFilePath))
            {
                _logger.Debug($"[MountChdFiles] No suitable game file found in {driveRoot}.");
                throw new FileNotFoundException($"Could not find a game file within the mounted CHD at {driveRoot}.");
            }

            _logger.Debug(
                $"[MountChdFiles] Game file found at: {gameFilePath}. Proceeding to launch with {selectedEmulatorName}.");

            await gameLauncher.LaunchRegularEmulatorAsync(gameFilePath, selectedEmulatorName, selectedSystemManager,
                selectedEmulatorManager, rawEmulatorParameters, windowContext, null, resolvedChdFilePath);

            _logger.Debug($"[MountChdFiles] Emulator for {gameFilePath} has exited.");
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountChdFiles] Exception during CHD mounting or launching: {ex}");
            var exitCodeInfoInCatch = mountProcess is { HasExited: true }
                ? $"Exit Code: {mountProcess.ExitCode}"
                : "Process was still running or state unknown.";
            var contextMessage = $"Error during CHD mount/launch process for {resolvedChdFilePath}.\n" +
                                 $"Exception: {ex.Message}\n" +
                                 $"{exitCodeInfoInCatch}";
            logErrors.Error(ex, contextMessage);

            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
        }
        finally
        {
            _logger.Debug(
                $"[MountChdFiles] Entering finally block for {resolvedChdFilePath}. Mount Process ID: {mountProcessId}");
            if (mountProcess != null && mountProcessId != -1 && !mountProcess.HasExited)
            {
                _logger.Debug(
                    $"[MountChdFiles] Attempting to unmount by terminating CHDMounter (ID: {mountProcessId}).");
                try
                {
                    mountProcess.Kill(true);
                    _logger.Debug(
                        $"[MountChdFiles] Kill signal sent to CHDMounter (ID: {mountProcessId}). Waiting for process to exit (up to 20s).");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    try
                    {
                        await mountProcess.WaitForExitAsync(cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Debug(
                            $"[MountChdFiles] Timeout (10s) waiting for CHDMounter (ID: {mountProcessId}) to exit after Kill.");
                    }

                    if (mountProcess.HasExited)
                    {
                        _logger.Debug(
                            $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) terminated. Exit code: {mountProcess.ExitCode.ToString(CultureInfo.InvariantCulture)}.");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) did NOT terminate after Kill signal and 10s wait.");
                    }
                }
                catch (InvalidOperationException ioEx)
                {
                    if (ioEx.Message.Contains("process has already exited", StringComparison.OrdinalIgnoreCase) ||
                        ioEx.Message.Contains("No process is associated", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Debug(
                            $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) already exited: {ioEx.Message}");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountChdFiles] InvalidOperationException while terminating CHDMounter (ID: {mountProcessId}): {ioEx}");
                        logErrors.Error(ioEx, "Unexpected InvalidOperationException during CHDMounter termination.");
                    }
                }
                catch (Exception termEx)
                {
                    _logger.Debug(
                        $"[MountChdFiles] Exception while terminating CHDMounter (ID: {mountProcessId}): {termEx}");
                    logErrors.Error(termEx, $"Failed to terminate CHDMounter (ID: {mountProcessId}) for unmounting.");
                }
            }
            else if (mountProcessId != -1)
            {
                var exitCodeStr = mountProcess is { HasExited: true }
                    ? mountProcess.ExitCode.ToString(CultureInfo.InvariantCulture)
                    : "N/A";
                _logger.Debug(
                    $"[MountChdFiles] CHDMounter (ID: {mountProcessId}) had already exited. Exit code likely {exitCodeStr}.");
            }
            else
            {
                _logger.Debug(
                    "[MountChdFiles] CHDMounter process was not started successfully. No termination needed.");
            }

            mountProcess?.Dispose();

            await Task.Delay(2000);
            if (Directory.Exists(driveRoot))
                _logger.Debug($"[MountChdFiles] WARNING: Drive {driveRoot} still exists after attempting to unmount.");
            else
                _logger.Debug($"[MountChdFiles] Drive {driveRoot} successfully unmounted.");
        }
    }

    /// <summary>
    ///     Determines the CHDMounter console alias for a given system name and emulator name.
    ///     Preserves the previous mounting routine: most systems mount as virtual CUE/BIN
    ///     (raw 2352) so emulators receive .cue/.bin files; only PS2/PS3/Xbox/Xbox 360 use
    ///     filesystem parsing, PSP and xemu use ISO RAW (single image.iso), 3DO uses
    ///     CUE/BIN 2048 (cooked). Aliases per CHDMounter README.
    /// </summary>
    public string? GetConsoleAliasFromSystemName(string systemName, string emulatorName, string? emulatorLocation,
        ILogger logErrors)
    {
        if (string.IsNullOrEmpty(systemName)) return null;

        // ── Emulator-specific overrides ──────────────────────────────────────────
        // These emulators require a specific virtual export mode to launch CHDs:
        //   Final Burn Alpha (fba64.exe) → CUE/ISO/WAV 2048
        //   Final Burn Neo (fbneo64.exe) → CUE/BIN 2352
        //   Nebula (nebula.exe)           → CUE/ISO 2048
        //   Raine (raine.exe)             → CUE/ISO/WAV 2352
        var emulatorMatch = emulatorName ?? string.Empty;
        var locationMatch = emulatorLocation ?? string.Empty;

        if (emulatorMatch.Contains("FBAlpha", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("FB Alpha", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("FinalBurnAlpha", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("Final Burn Alpha", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("FinalBurn Alpha", StringComparison.OrdinalIgnoreCase) ||
            locationMatch.Contains("fba64.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "cueisowav2048";
        }

        if (emulatorMatch.Contains("FBNeo", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("FB Neo", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("FinalBurnNeo", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("Final Burn Neo", StringComparison.OrdinalIgnoreCase) ||
            emulatorMatch.Contains("FinalBurn Neo", StringComparison.OrdinalIgnoreCase) ||
            locationMatch.Contains("fbneo64.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (emulatorMatch.Contains("Nebula", StringComparison.OrdinalIgnoreCase) ||
            locationMatch.Contains("nebula.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "cueiso2048";
        }

        if (emulatorMatch.Contains("raine", StringComparison.OrdinalIgnoreCase) ||
            locationMatch.Contains("raine.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "cueisowav2352";
        }

        if ((systemName.Contains("AMIGA CD", StringComparison.OrdinalIgnoreCase) ||
             systemName.Contains("AMIGACD", StringComparison.OrdinalIgnoreCase)) &&
            !systemName.Contains("CD32", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("AMIGA CD32", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("AMIGACD32", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("CD32", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("CD-I", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("CDI", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PHILIPS CDI", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PHILIPSCDI", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("DREAMCAST", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("SEGA DREAMCAST", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("FM Towns", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("FMTowns", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("NEOGEO CD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("NEO GEO CD", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("PCE-CD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PC ENGINE CD", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("PC-FX", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PCFX", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if ((systemName.Contains("PS1", StringComparison.OrdinalIgnoreCase) ||
             systemName.Contains("PSX1", StringComparison.OrdinalIgnoreCase) ||
             systemName.Contains("PSX 1", StringComparison.OrdinalIgnoreCase) ||
             systemName.Contains("PLAY 1", StringComparison.OrdinalIgnoreCase) ||
             systemName.Contains("PLAYSTATION 1", StringComparison.OrdinalIgnoreCase) ||
             systemName.Contains("PLAYSTATION", StringComparison.OrdinalIgnoreCase)) &&
            !systemName.Contains('2') &&
            !systemName.Contains('3'))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("PS2", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PSX2", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PSX 2", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PLAY 2", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PLAYSTATION 2", StringComparison.OrdinalIgnoreCase))
        {
            return "ps2";
        }

        if (systemName.Contains("PS3", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PSX3", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PSX 3", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PLAY 3", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PLAYSTATION 3", StringComparison.OrdinalIgnoreCase))
        {
            return "ps3";
        }

        if (systemName.Contains("PSP", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PLAYSTATION PORTABLE", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("SONY PSP", StringComparison.OrdinalIgnoreCase))
        {
            return "isoraw2352";
        }

        if (systemName.Contains("SATURN", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("SEGA SATURN", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        // Sega Genesis CD / Mega Drive CD / Sega CD / Mega CD
        if (systemName.Contains("GENESIS CD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("SEGA CD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("MEGA CD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("GENESISCD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("SEGACD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("MEGACD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("Sega Genesis CD", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("Mega Drive CD", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2352";
        }

        if (systemName.Contains("3DO", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("PANASONIC 3DO", StringComparison.OrdinalIgnoreCase))
        {
            return "cuebin2048";
        }

        if (systemName.Contains("XBOX", StringComparison.OrdinalIgnoreCase) &&
            !systemName.Contains("360", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(emulatorName) &&
                emulatorName.Contains("xemu", StringComparison.OrdinalIgnoreCase))
            {
                return "isoraw2352";
            }

            return "xbox";
        }

        if (systemName.Contains("XBOX 360", StringComparison.OrdinalIgnoreCase) ||
            systemName.Contains("XBOX360", StringComparison.OrdinalIgnoreCase))
        {
            return "xbox360";
        }

        return "cuebin2352";
    }

    /// <summary>
    ///     Terminates all running CHDMounter processes to ensure clean unmounting.
    /// </summary>
    public void KillAllChdMounterProcesses(ILogger logErrors)
    {
        var processNames = new[] { "CHDMounter", "CHDMounter_arm64" };

        try
        {
            foreach (var processName in processNames)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    _logger.Debug($"[MountChdFiles.KillAllChdMounterProcesses] No {processName} processes found.");
                    continue;
                }

                _logger.Debug(
                    $"[MountChdFiles.KillAllChdMounterProcesses] Found {processes.Length} {processName} process(es) to kill.");

                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.Debug(
                                $"[MountChdFiles.KillAllChdMounterProcesses] Killing {processName} (ID: {process.Id}).");
                            process.Kill(true);
                            process.WaitForExit(5000);
                            _logger.Debug(
                                $"[MountChdFiles.KillAllChdMounterProcesses] {processName} (ID: {process.Id}) terminated. Exit code: {process.ExitCode}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(
                            $"[MountChdFiles.KillAllChdMounterProcesses] Failed to kill {processName} (ID: {process.Id}): {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(
                $"[MountChdFiles.KillAllChdMounterProcesses] Error enumerating CHDMounter processes: {ex.Message}");
        }
    }

    /// <summary>
    ///     Determines the CHDMounter executable relative path based on the current process architecture.
    /// </summary>
    private static string GetArchitectureSpecificExecutableRelativePath()
    {
        var arch = RuntimeInformation.ProcessArchitecture;

        var executableName = arch switch
        {
            Architecture.X64 => "CHDMounter.exe",
            Architecture.Arm64 => "CHDMounter_arm64.exe",
            _ => throw new PlatformNotSupportedException($"Architecture {arch} is not supported by CHDMounter.")
        };

        return Path.Combine(ChdMounterRelativeDirectory, executableName);
    }

    private static HashSet<char> GetCurrentDriveLetters()
    {
        return Environment.GetLogicalDrives()
            .Select(static d => char.ToUpper(d[0], CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    private async Task<(bool Success, char? DriveLetter, int? ExitCode)> WaitForDriveMountAndDetectAsync(
        HashSet<char> existingDrives, Process mountProcess, int processId, ILogger logErrors,
        List<string> errorOutput, string chdFileName)
    {
        const int maxRetries = 240;
        const int pollIntervalMs = 500;
        var retryCount = 0;

        _logger.Debug(
            $"[MountChdFiles.WaitForDriveMountAndDetectAsync] Polling for new drive to appear for '{chdFileName}' (max {maxRetries * pollIntervalMs / 1000}s)...");

        while (retryCount < maxRetries)
        {
            var currentDrives = GetCurrentDriveLetters();
            var newDrives = currentDrives.Except(existingDrives).ToList();

            if (newDrives.Count > 0)
            {
                var detectedDrive = newDrives[0];
                _logger.Debug(
                    $"[MountChdFiles.WaitForDriveMountAndDetectAsync] Found new drive '{detectedDrive}:' after {retryCount * pollIntervalMs / 1000.0:F1} seconds. Mount successful!");
                return (true, detectedDrive, null);
            }

            if (mountProcess.HasExited)
            {
                var exitCode = mountProcess.ExitCode;
                var contextMessage = exitCode == -1073741515
                    ? $"Failed to mount CHD. The CHDMounter tool exited prematurely with code {exitCode} (STATUS_DLL_NOT_FOUND). This indicates that the Dokan library is not installed."
                    : $"Failed to mount CHD. The CHDMounter tool exited prematurely with code {exitCode}.";

                if (errorOutput is { Count: > 0 })
                {
                    contextMessage += "\n\n=== CHDMounter error output ===\n" +
                                      string.Join(Environment.NewLine, errorOutput);
                }

                _logger.Debug(
                    $"[MountChdFiles.WaitForDriveMountAndDetectAsync] CHDMounter process (ID: {processId}) exited prematurely during polling. {contextMessage}");
                // Expected condition (mount failed; user already gets UI feedback): not a bug,
                // keep it out of the bug report service.
                logErrors.Information(contextMessage);
                return (false, null, exitCode);
            }

            retryCount++;
            await Task.Delay(pollIntervalMs);
        }

        _logger.Debug(
            $"[MountChdFiles.WaitForDriveMountAndDetectAsync] Timed out waiting for new drive after {maxRetries * pollIntervalMs / 1000} seconds.");
        var timeoutContextMessage = $"Timed out waiting for the CHD to mount '{chdFileName}'. No new drive detected.";

        if (errorOutput is { Count: > 0 })
        {
            timeoutContextMessage +=
                "\n\n=== CHDMounter error output ===\n" + string.Join(Environment.NewLine, errorOutput);
        }

        // Expected condition (mount timeout; user already gets UI feedback): not a bug,
        // keep it out of the bug report service.
        logErrors.Information(timeoutContextMessage);
        return (false, null, null);
    }

    private string? FindGameFile(string driveRoot, ILogger logErrors)
    {
        try
        {
            _logger.Debug($"[MountChdFiles.FindGameFile] Searching for game file in {driveRoot}...");

            if (!Directory.Exists(driveRoot))
            {
                _logger.Debug($"[MountChdFiles.FindGameFile] Drive {driveRoot} does not exist.");
                return null;
            }

            var allFiles = Directory.GetFiles(driveRoot, "*", SearchOption.AllDirectories);
            if (allFiles.Length == 0)
            {
                _logger.Debug($"[MountChdFiles.FindGameFile] No files found in {driveRoot}.");
                return null;
            }

            _logger.Debug($"[MountChdFiles.FindGameFile] Found {allFiles.Length} files in {driveRoot}.");

            foreach (var file in allFiles) _logger.Debug($"[MountChdFiles.FindGameFile] Found: {file}");

            return allFiles[0];
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountChdFiles.FindGameFile] Error searching for game file in {driveRoot}: {ex.Message}");
            logErrors.Error(ex, $"Error in FindGameFile searching {driveRoot}");
            return null;
        }
    }
}