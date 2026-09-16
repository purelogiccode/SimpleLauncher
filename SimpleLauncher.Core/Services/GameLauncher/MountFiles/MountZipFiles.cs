using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using SharpCompress.Archives;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Handles mounting ZIP archives as virtual drives using SimpleZipDrive and launching games from the mounted drive.
/// </summary>
public class MountZipFiles : IMountZipFiles
{
    private readonly ILogger _logger;
    private readonly string _preferredMountDriveLetterOnly;
    private readonly string _zipMountExecutableName;
    private readonly string _zipMountExecutableRelativePath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MountZipFiles" /> class.
    /// </summary>
    /// <param name="configuration">The application configuration for mount settings.</param>
    /// <param name="logger">The logger instance.</param>
    public MountZipFiles(IConfiguration configuration, ILogger logger)
    {
        _logger = logger;

        var mountPathFromConfig = configuration.GetValue("ZipMountOptions:MountDriveLetter", "Z:");

        // Determine the correct executable based on architecture
        _zipMountExecutableName = GetArchitectureSpecificExecutableName();
        _zipMountExecutableRelativePath = Path.Combine("tools", "SimpleZipDrive", _zipMountExecutableName);

        if (string.IsNullOrEmpty(mountPathFromConfig)) mountPathFromConfig = "Z:"; // Fallback

        // Extract just the drive letter
        if (mountPathFromConfig.EndsWith(":\\", StringComparison.Ordinal))
        {
            _preferredMountDriveLetterOnly = mountPathFromConfig.Substring(0, mountPathFromConfig.Length - 2);
        }
        else if (mountPathFromConfig.EndsWith(':'))
        {
            _preferredMountDriveLetterOnly = mountPathFromConfig.Substring(0, mountPathFromConfig.Length - 1);
        }
        else // Assume it's just the letter or an invalid format, try to take the first char if it's a letter
        {
            _preferredMountDriveLetterOnly = mountPathFromConfig.Length > 0 && char.IsLetter(mountPathFromConfig[0])
                ? mountPathFromConfig[0].ToString().ToUpperInvariant()
                : "Z";
        }

        _logger.Debug(
            $"[MountZipFiles] Preferred MountDriveLetter (for {_zipMountExecutableName}): {_preferredMountDriveLetterOnly}");
        _logger.Debug($"[MountZipFiles] Configured ZipMountExecutableName: {_zipMountExecutableName}");
        _logger.Debug($"[MountZipFiles] Configured ZipMountExecutableRelativePath: {_zipMountExecutableRelativePath}");
    }

    /// <summary>
    ///     Gets the configured mount drive root path (e.g., "Z:\").
    /// </summary>
    public string ConfiguredMountDriveRoot => _preferredMountDriveLetterOnly + ":\\";

    /// <summary>
    ///     Terminates all running SimpleZipDrive processes to ensure clean unmounting.
    /// </summary>
    /// <param name="logErrors">The error logger.</param>
    public void KillAllSimpleZipDriveProcesses(ILogger logErrors)
    {
        try
        {
            var processNames = new[] { "SimpleZipDrive", "SimpleZipDrive_arm64" };
            foreach (var processName in processNames)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0) continue;

                _logger.Debug(
                    $"[MountZipFiles.KillAllSimpleZipDriveProcesses] Found {processes.Length} {processName} process(es) to kill.");

                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.Debug(
                                $"[MountZipFiles.KillAllSimpleZipDriveProcesses] Killing {processName} (ID: {process.Id}).");
                            process.Kill(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(
                            $"[MountZipFiles.KillAllSimpleZipDriveProcesses] Error killing process {process.Id}: {ex.Message}");
                        logErrors.Error(ex,
                            $"[MountZipFiles.KillAllSimpleZipDriveProcesses] Error killing process {process.Id}: {ex.Message}");
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
            _logger.Debug($"[MountZipFiles.KillAllSimpleZipDriveProcesses] Error enumerating processes: {ex.Message}");
        }
    }

    /// <summary>
    ///     Mounts a ZIP archive and launches the EBOOT.BIN file found within it using the specified emulator.
    /// </summary>
    public async Task MountZipFileAndLoadEbootBinAsync(
        string resolvedZipFilePath,
        string selectedSystemName,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        string? logPath,
        ILauncherService gameLauncher,
        ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        _logger.Debug($"[MountZipFiles] Starting to mount ZIP for EBOOT.BIN: {resolvedZipFilePath}");
        _logger.Debug($"[MountZipFiles] System: {selectedSystemName}, Emulator: {selectedEmulatorName}");

        try
        {
            ValidateZipForPathTraversal(resolvedZipFilePath);
        }
        catch (Exception ex)
        {
            var errorMessage =
                $"The compressed file is corrupted or in an unsupported format and cannot be mounted: {resolvedZipFilePath}";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Error(ex, errorMessage);
            await messageBox.CouldNotLaunchGameMessageBoxAsync(logPath);
            return;
        }

        var resolvedZipMountExePath = PathHelper.ResolveRelativeToAppDirectory(_zipMountExecutableRelativePath);

        _logger.Debug($"[MountZipFiles] Path to {_zipMountExecutableName}: {resolvedZipMountExePath}");

        if (string.IsNullOrWhiteSpace(resolvedZipMountExePath) || !File.Exists(resolvedZipMountExePath))
        {
            // Notify developer
            var errorMessage =
                $"{_zipMountExecutableName} not found at {_zipMountExecutableRelativePath}. Cannot mount ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();

            return;
        }

        if (!OperatingSystem.IsWindows() || !DokanValidation.IsDokanInstalled())
        {
            const string errorMessage = "Dokan driver not found. Cannot mount ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Information(errorMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return;
        }

        // Get an available drive letter dynamically
        var driveLetter = GetAvailableDriveLetter(logErrors);
        if (driveLetter == null)
        {
            const string errorMessage = "No available drive letters found to mount the ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return;
        }

        var mountPathArgument =
            driveLetter.Value.ToString().ToLowerInvariant(); // SimpleZipDrive expects lowercase letter
        var mountDriveRootForChecks = $"{driveLetter.Value}:\\"; // For Directory.Exists checks

        _logger.Debug($"[MountZipFiles] Selected drive letter for mounting: {driveLetter.Value}:");

        var psiMount = new ProcessStartInfo
        {
            FileName = resolvedZipMountExePath,
            // SimpleZipDrive uses positional arguments: "<PathToZipFile>" "<MountPoint>"
            Arguments = $"\"{resolvedZipFilePath}\" \"{mountPathArgument}\"",
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = Path.GetDirectoryName(resolvedZipMountExePath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        _logger.Debug($"[MountZipFiles] ProcessStartInfo for {_zipMountExecutableName}:");
        _logger.Debug($"[MountZipFiles] FileName: {psiMount.FileName}");
        _logger.Debug($"[MountZipFiles] Arguments: {psiMount.Arguments}");
        _logger.Debug($"[MountZipFiles] WorkingDirectory: {psiMount.WorkingDirectory}");

        Process? mountProcess = null;
        var mountProcessId = -1;

        try
        {
            mountProcess = new Process();
            mountProcess.StartInfo = psiMount;
            mountProcess.EnableRaisingEvents = true;

            _logger.Debug($"[MountZipFiles] Starting {_zipMountExecutableName} process...");
            var processStarted = mountProcess.Start();
            if (!processStarted)
                throw new InvalidOperationException($"Failed to start the {_zipMountExecutableName} process.");

            mountProcessId = mountProcess.Id;
            _logger.Debug($"[MountZipFiles] {_zipMountExecutableName} process started (ID: {mountProcessId}).");

            // Polling mechanism to wait for the mount to complete.
            var mountSuccessful = false;
            var timeout = TimeSpan.FromMinutes(1); // 1-minute timeout for zip mounting
            var pollInterval = TimeSpan.FromMilliseconds(500);
            var stopwatch = Stopwatch.StartNew();

            _logger.Debug(
                $"[MountZipFiles] Polling for drive '{mountDriveRootForChecks}' to appear (timeout: {timeout.TotalSeconds}s)...");

            while (stopwatch.Elapsed < timeout)
            {
                if (Directory.Exists(mountDriveRootForChecks))
                {
                    mountSuccessful = true;
                    _logger.Debug(
                        $"[MountZipFiles] Found drive '{mountDriveRootForChecks}' after {stopwatch.Elapsed.TotalSeconds:F1} seconds.");
                    break;
                }

                if (mountProcess.HasExited)
                {
                    var exitCode = mountProcess.ExitCode;
                    _logger.Debug(
                        $"[MountZipFiles] Mount process {_zipMountExecutableName} (ID: {mountProcessId}) exited prematurely during polling. Exit Code: {exitCode}.");
                    break;
                }

                await Task.Delay(pollInterval);
            }

            stopwatch.Stop();

            if (!mountSuccessful)
            {
                if (mountProcess.HasExited)
                {
                    var exitCode = mountProcess.ExitCode;
                    var reason = GetExitCodeReason(exitCode);
                    _logger.Debug(
                        $"[MountZipFiles] Mount check failed. Drive {mountDriveRootForChecks} not found. The process exited with code {exitCode} ({reason}).");
                    throw new InvalidOperationException(
                        $"Failed to mount ZIP. {_zipMountExecutableName} exited with code {exitCode} ({reason}).");
                }

                _logger.Debug(
                    $"[MountZipFiles] Mount check failed. Drive {mountDriveRootForChecks} not found. The process was still running after timeout. Check the console window of {_zipMountExecutableName} for details.");
                throw new TimeoutException(
                    $"Failed to mount ZIP. Drive {mountDriveRootForChecks} not found after timeout.");
            }

            _logger.Debug($"[MountZipFiles] Drive {mountDriveRootForChecks} detected. Searching for EBOOT.BIN...");

            // Find EBOOT.BIN
            var ebootBinPath = FindEbootBin.FindEbootBinRecursive(mountDriveRootForChecks, logErrors, _logger);

            if (string.IsNullOrEmpty(ebootBinPath))
            {
                _logger.Debug($"[MountZipFiles] EBOOT.BIN not found in {mountDriveRootForChecks}.");
                throw new FileNotFoundException(
                    $"EBOOT.BIN not found within the mounted ZIP file at {mountDriveRootForChecks}.");
            }

            _logger.Debug(
                $"[MountZipFiles] EBOOT.BIN found at: {ebootBinPath}. Proceeding to launch with {selectedEmulatorName}.");

            // Pass the original ZIP file path for display in notifications
            await gameLauncher.LaunchRegularEmulatorAsync(ebootBinPath, selectedEmulatorName, selectedSystemManager,
                selectedEmulatorManager, rawEmulatorParameters, windowContext, null, resolvedZipFilePath);

            _logger.Debug($"[MountZipFiles] Emulator for {ebootBinPath} has exited.");
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Debug($"[MountZipFiles] Exception during ZIP mounting or launching: {ex}");
            // HasExited throws InvalidOperationException when Start() itself failed (no
            // process associated); treating "never started" as unknown keeps the original
            // error visible and the user-facing dialog reachable.
            var mountExitCode = GetMountProcessExitCodeOrNull(mountProcess);
            var exitCodeInfoInCatch = mountExitCode.HasValue
                ? $"Exit Code: {mountExitCode.Value}"
                : "Process was still running or state unknown.";
            var contextMessage = $"Error during ZIP mount/launch process for {resolvedZipFilePath}.\n" +
                                 $"Exception: {ex.Message}\n" +
                                 $"The tool's output was not redirected. {exitCodeInfoInCatch}";
            logErrors.Error(ex, contextMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync(
                GetMountProcessExitCodeOrNull(mountProcess));
        }
        finally
        {
            _logger.Debug(
                $"[MountZipFiles] Entering finally block for {resolvedZipFilePath}. Mount Process ID: {mountProcessId}");
            if (mountProcess != null && mountProcessId != -1 && !mountProcess.HasExited)
            {
                _logger.Debug(
                    $"[MountZipFiles] Attempting to unmount by terminating {_zipMountExecutableName} (ID: {mountProcessId}).");
                try
                {
                    mountProcess.Kill(true);
                    _logger.Debug(
                        $"[MountZipFiles] Kill signal sent to {_zipMountExecutableName} (ID: {mountProcessId}). Waiting for process to exit (up to 20s).");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    try
                    {
                        await mountProcess.WaitForExitAsync(cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Debug(
                            $"[MountZipFiles] Timeout (10s) waiting for {_zipMountExecutableName} (ID: {mountProcessId}) to exit after Kill.");
                    }

                    if (mountProcess.HasExited)
                    {
                        _logger.Debug(
                            $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) terminated. Exit code: {mountProcess.ExitCode.ToString(CultureInfo.InvariantCulture)}.");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) did NOT terminate after Kill signal and 10s wait.");
                    }
                }
                catch (InvalidOperationException ioEx)
                {
                    if (ioEx.Message.Contains("process has already exited", StringComparison.OrdinalIgnoreCase) ||
                        ioEx.Message.Contains("No process is associated", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Debug(
                            $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) already exited or no process associated: {ioEx.Message}");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountZipFiles] InvalidOperationException while terminating {_zipMountExecutableName} (ID: {mountProcessId}): {ioEx}");

                        // Notify developer
                        logErrors.Error(ioEx,
                            $"Unexpected InvalidOperationException during {_zipMountExecutableName} termination.");
                    }
                }
                catch (Exception termEx)
                {
                    _logger.Debug(
                        $"[MountZipFiles] Exception while terminating {_zipMountExecutableName} (ID: {mountProcessId}): {termEx}");

                    // Notify developer
                    logErrors.Error(termEx,
                        $"Failed to terminate {_zipMountExecutableName} (ID: {mountProcessId}) for unmounting.");
                }
            }
            else if (mountProcessId != -1)
            {
                var exitCodeStr =
                    GetMountProcessExitCodeOrNull(mountProcess)?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
                _logger.Debug(
                    $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) had already exited or was not running when finally cleanup was attempted. Exit code likely {exitCodeStr}.");
            }
            else
            {
                _logger.Debug(
                    $"[MountZipFiles] {_zipMountExecutableName} process was not started successfully (ID: {mountProcessId}). No termination needed.");
            }

            mountProcess?.Dispose();

            await Task.Delay(2000);
            // Use mountDriveRootForChecks for Directory.Exists
            if (Directory.Exists(mountDriveRootForChecks))
            {
                _logger.Debug(
                    $"[MountZipFiles] WARNING: Drive {mountDriveRootForChecks} still exists after attempting to unmount. {_zipMountExecutableName} might not have unmounted correctly or is still running.");
            }
            else
            {
                _logger.Debug(
                    $"[MountZipFiles] Drive {mountDriveRootForChecks} successfully unmounted (or was not detected).");
            }

            // Safety net: ensure all SimpleZipDrive processes are killed
            KillAllSimpleZipDriveProcesses(logErrors);
        }
    }

    /// <summary>
    ///     Mounts a ZIP archive and searches for a nested file to launch using the specified emulator.
    /// </summary>
    public async Task MountZipFileAndSearchForFileToLoadAsync(
        string resolvedZipFilePath,
        string selectedSystemName,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        string? logPath,
        ILauncherService gameLauncher,
        ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        _logger.Debug($"[MountZipFiles] Starting to mount ZIP for nested file search: {resolvedZipFilePath}");
        _logger.Debug($"[MountZipFiles] System: {selectedSystemName}, Emulator: {selectedEmulatorName}");

        try
        {
            ValidateZipForPathTraversal(resolvedZipFilePath);
        }
        catch (Exception ex)
        {
            var errorMessage =
                $"The compressed file is corrupted or in an unsupported format and cannot be mounted: {resolvedZipFilePath}";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Error(ex, errorMessage);
            await messageBox.CouldNotLaunchGameMessageBoxAsync(logPath);
            return;
        }

        var resolvedZipMountExePath = PathHelper.ResolveRelativeToAppDirectory(_zipMountExecutableRelativePath);

        _logger.Debug($"[MountZipFiles] Path to {_zipMountExecutableName}: {resolvedZipMountExePath}");

        if (string.IsNullOrWhiteSpace(resolvedZipMountExePath) || !File.Exists(resolvedZipMountExePath))
        {
            // Notify developer
            var errorMessage =
                $"{_zipMountExecutableName} not found at {_zipMountExecutableRelativePath}. Cannot mount ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();

            return;
        }

        if (!OperatingSystem.IsWindows() || !DokanValidation.IsDokanInstalled())
        {
            const string errorMessage = "Dokan driver not found. Cannot mount ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Information(errorMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return;
        }

        // Get an available drive letter dynamically
        var driveLetter = GetAvailableDriveLetter(logErrors);
        if (driveLetter == null)
        {
            const string errorMessage = "No available drive letters found to mount the ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return;
        }

        var mountPathArgument = driveLetter.Value.ToString().ToLowerInvariant();
        var mountDriveRootForChecks = $"{driveLetter.Value}:\\";

        _logger.Debug($"[MountZipFiles] Selected drive letter for mounting: {driveLetter.Value}:");

        var psiMount = new ProcessStartInfo
        {
            FileName = resolvedZipMountExePath,
            Arguments = $"\"{resolvedZipFilePath}\" \"{mountPathArgument}\"",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(resolvedZipMountExePath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        _logger.Debug($"[MountZipFiles] ProcessStartInfo for {_zipMountExecutableName}:");
        _logger.Debug($"[MountZipFiles] FileName: {psiMount.FileName}");
        _logger.Debug($"[MountZipFiles] Arguments: {psiMount.Arguments}");
        _logger.Debug($"[MountZipFiles] WorkingDirectory: {psiMount.WorkingDirectory}");

        Process? mountProcess = null;
        var mountProcessId = -1;

        try
        {
            mountProcess = new Process { StartInfo = psiMount, EnableRaisingEvents = true };

            _logger.Debug($"[MountZipFiles] Starting {_zipMountExecutableName} process...");
            if (!mountProcess.Start())
                throw new InvalidOperationException($"Failed to start the {_zipMountExecutableName} process.");

            mountProcessId = mountProcess.Id;
            _logger.Debug($"[MountZipFiles] {_zipMountExecutableName} process started (ID: {mountProcessId}).");

            // Polling mechanism to wait for the mount to complete.
            var mountSuccessful = false;
            var timeout = TimeSpan.FromMinutes(1);
            var pollInterval = TimeSpan.FromMilliseconds(500);
            var stopwatch = Stopwatch.StartNew();

            _logger.Debug(
                $"[MountZipFiles] Polling for drive '{mountDriveRootForChecks}' to appear (timeout: {timeout.TotalSeconds}s)...");

            while (stopwatch.Elapsed < timeout)
            {
                if (Directory.Exists(mountDriveRootForChecks))
                {
                    mountSuccessful = true;
                    _logger.Debug(
                        $"[MountZipFiles] Found drive '{mountDriveRootForChecks}' after {stopwatch.Elapsed.TotalSeconds:F1} seconds.");
                    break;
                }

                if (mountProcess.HasExited)
                {
                    var exitCode = mountProcess.ExitCode;
                    _logger.Debug(
                        $"[MountZipFiles] Mount process {_zipMountExecutableName} (ID: {mountProcessId}) exited prematurely during polling. Exit Code: {exitCode}.");
                    break;
                }

                await Task.Delay(pollInterval);
            }

            stopwatch.Stop();

            if (!mountSuccessful)
            {
                if (mountProcess.HasExited)
                {
                    var exitCode = mountProcess.ExitCode;
                    var reason = GetExitCodeReason(exitCode);
                    _logger.Debug(
                        $"[MountZipFiles] Mount check failed. Drive {mountDriveRootForChecks} not found. The process exited with code {exitCode} ({reason}).");
                    throw new InvalidOperationException(
                        $"Failed to mount ZIP. {_zipMountExecutableName} exited with code {exitCode} ({reason}).");
                }

                _logger.Debug(
                    $"[MountZipFiles] Mount check failed. Drive {mountDriveRootForChecks} not found. The process was still running after timeout. Check the console window of {_zipMountExecutableName} for details.");
                throw new TimeoutException(
                    $"Failed to mount ZIP. Drive {mountDriveRootForChecks} not found after timeout.");
            }

            _logger.Debug($"[MountZipFiles] Drive {mountDriveRootForChecks} detected. Searching for nested file...");
            var fileToLoad = FindNestedFile(mountDriveRootForChecks, logErrors);

            if (string.IsNullOrEmpty(fileToLoad))
            {
                _logger.Debug(
                    $"[MountZipFiles] No suitable file found in nested directory structure in {mountDriveRootForChecks}.");
                throw new FileNotFoundException(
                    $"Could not find a file to launch within the expected nested directory structure of the mounted ZIP at {mountDriveRootForChecks}.");
            }

            _logger.Debug(
                $"[MountZipFiles] Nested file found at: {fileToLoad}. Proceeding to launch with {selectedEmulatorName}.");

            // Pass the original ZIP file path for display in notifications
            await gameLauncher.LaunchRegularEmulatorAsync(fileToLoad, selectedEmulatorName, selectedSystemManager,
                selectedEmulatorManager, rawEmulatorParameters, windowContext, null, resolvedZipFilePath);

            _logger.Debug($"[MountZipFiles] Emulator for {fileToLoad} has exited.");
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountZipFiles] Exception during ZIP mounting or launching: {ex}");

            // Notify developer
            // HasExited throws InvalidOperationException when Start() itself failed (no
            // process associated); treating "never started" as unknown keeps the original
            // error visible and the user-facing dialog reachable.
            var mountExitCode = GetMountProcessExitCodeOrNull(mountProcess);
            var exitCodeInfoInCatch = mountExitCode.HasValue
                ? $"Exit Code: {mountExitCode.Value}"
                : "Process was still running or state unknown.";
            var contextMessage = $"Error during ZIP mount/launch process for {resolvedZipFilePath}.\n" +
                                 $"Exception: {ex.Message}\n" +
                                 $"The tool's output was not redirected. {exitCodeInfoInCatch}";
            logErrors.Error(ex, contextMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync(
                GetMountProcessExitCodeOrNull(mountProcess));
        }
        finally
        {
            _logger.Debug(
                $"[MountZipFiles] Entering finally block for {resolvedZipFilePath}. Mount Process ID: {mountProcessId}");
            if (mountProcess != null && mountProcessId != -1 && !mountProcess.HasExited)
            {
                _logger.Debug(
                    $"[MountZipFiles] Attempting to unmount by terminating {_zipMountExecutableName} (ID: {mountProcessId}).");
                try
                {
                    mountProcess.Kill(true);
                    _logger.Debug(
                        $"[MountZipFiles] Kill signal sent to {_zipMountExecutableName} (ID: {mountProcessId}). Waiting for process to exit (up to 20s).");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    try
                    {
                        await mountProcess.WaitForExitAsync(cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Debug(
                            $"[MountZipFiles] Timeout (10s) waiting for {_zipMountExecutableName} (ID: {mountProcessId}) to exit after Kill.");
                    }

                    if (mountProcess.HasExited)
                    {
                        _logger.Debug(
                            $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) terminated. Exit code: {mountProcess.ExitCode.ToString(CultureInfo.InvariantCulture)}.");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) did NOT terminate after Kill signal and 10s wait.");
                    }
                }
                catch (Exception termEx)
                {
                    _logger.Debug(
                        $"[MountZipFiles] Exception while terminating {_zipMountExecutableName} (ID: {mountProcessId}): {termEx}");

                    // Notify developer
                    logErrors.Error(termEx,
                        $"Failed to terminate {_zipMountExecutableName} (ID: {mountProcessId}) for unmounting.");
                }
            }
            else if (mountProcessId != -1)
            {
                var exitCodeStr =
                    GetMountProcessExitCodeOrNull(mountProcess)?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
                _logger.Debug(
                    $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) had already exited or was not running when finally cleanup was attempted. Exit code likely {exitCodeStr}.");
            }
            else
            {
                _logger.Debug(
                    $"[MountZipFiles] {_zipMountExecutableName} process was not started successfully (ID: {mountProcessId}). No termination needed.");
            }

            mountProcess?.Dispose();

            await Task.Delay(2000);
            if (Directory.Exists(mountDriveRootForChecks))
            {
                _logger.Debug(
                    $"[MountZipFiles] WARNING: Drive {mountDriveRootForChecks} still exists after attempting to unmount.");
            }
            else
            {
                _logger.Debug($"[MountZipFiles] Drive {mountDriveRootForChecks} successfully unmounted.");
            }

            // Safety net: ensure all SimpleZipDrive processes are killed
            KillAllSimpleZipDriveProcesses(logErrors);
        }
    }

    /// <summary>
    ///     Mounts a ZIP archive and launches ScummVM with the mounted game path.
    /// </summary>
    public async Task MountZipFileAndLoadWithScummVmAsync(
        string resolvedZipFilePath,
        string selectedSystemName,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string selectedEmulatorParameters,
        string? logPath,
        ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        _logger.Debug($"[MountZipFiles] Starting to mount ZIP for ScummVM: {resolvedZipFilePath}");
        _logger.Debug($"[MountZipFiles] System: {selectedSystemName}, Emulator: {selectedEmulatorName}");

        try
        {
            ValidateZipForPathTraversal(resolvedZipFilePath);
        }
        catch (Exception ex)
        {
            var errorMessage =
                $"The compressed file is corrupted or in an unsupported format and cannot be mounted: {resolvedZipFilePath}";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Error(ex, errorMessage);
            await messageBox.CouldNotLaunchGameMessageBoxAsync(logPath);
            return;
        }

        var resolvedZipMountExePath = PathHelper.ResolveRelativeToAppDirectory(_zipMountExecutableRelativePath);

        _logger.Debug($"[MountZipFiles] Path to {_zipMountExecutableName}: {resolvedZipMountExePath}");

        if (string.IsNullOrWhiteSpace(resolvedZipMountExePath) || !File.Exists(resolvedZipMountExePath))
        {
            // Notify developer
            var errorMessage =
                $"{_zipMountExecutableName} not found at {_zipMountExecutableRelativePath}. Cannot mount ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();

            return;
        }

        if (!OperatingSystem.IsWindows() || !DokanValidation.IsDokanInstalled())
        {
            const string errorMessage = "Dokan driver not found. Cannot mount ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Information(errorMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return;
        }

        // Get an available drive letter dynamically
        var driveLetter = GetAvailableDriveLetter(logErrors);
        if (driveLetter == null)
        {
            const string errorMessage = "No available drive letters found to mount the ZIP.";
            _logger.Debug($"[MountZipFiles] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return;
        }

        var mountPathArgument = driveLetter.Value.ToString().ToLowerInvariant();
        var mountDriveRootForChecks = $"{driveLetter.Value}:\\";

        _logger.Debug($"[MountZipFiles] Selected drive letter for mounting: {driveLetter.Value}:");

        var psiMount = new ProcessStartInfo
        {
            FileName = resolvedZipMountExePath,
            Arguments = $"\"{resolvedZipFilePath}\" \"{mountPathArgument}\"",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(resolvedZipMountExePath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        _logger.Debug($"[MountZipFiles] ProcessStartInfo for {_zipMountExecutableName}:");
        _logger.Debug($"[MountZipFiles] FileName: {psiMount.FileName}");
        _logger.Debug($"[MountZipFiles] Arguments: {psiMount.Arguments}");
        _logger.Debug($"[MountZipFiles] WorkingDirectory: {psiMount.WorkingDirectory}");

        Process? mountProcess = null;
        var mountProcessId = -1;

        try
        {
            mountProcess = new Process { StartInfo = psiMount, EnableRaisingEvents = true };

            _logger.Debug($"[MountZipFiles] Starting {_zipMountExecutableName} process...");
            if (!mountProcess.Start())
                throw new InvalidOperationException($"Failed to start the {_zipMountExecutableName} process.");

            mountProcessId = mountProcess.Id;
            _logger.Debug($"[MountZipFiles] {_zipMountExecutableName} process started (ID: {mountProcessId}).");

            // Polling mechanism to wait for the mount to complete.
            var mountSuccessful = false;
            var timeout = TimeSpan.FromMinutes(1);
            var pollInterval = TimeSpan.FromMilliseconds(500);
            var stopwatch = Stopwatch.StartNew();

            _logger.Debug(
                $"[MountZipFiles] Polling for drive '{mountDriveRootForChecks}' to appear (timeout: {timeout.TotalSeconds}s)...");

            while (stopwatch.Elapsed < timeout)
            {
                if (Directory.Exists(mountDriveRootForChecks))
                {
                    mountSuccessful = true;
                    _logger.Debug(
                        $"[MountZipFiles] Found drive '{mountDriveRootForChecks}' after {stopwatch.Elapsed.TotalSeconds:F1} seconds.");
                    break;
                }

                if (mountProcess.HasExited)
                {
                    var exitCode = mountProcess.ExitCode;
                    _logger.Debug(
                        $"[MountZipFiles] Mount process {_zipMountExecutableName} (ID: {mountProcessId}) exited prematurely during polling. Exit Code: {exitCode}.");
                    break;
                }

                await Task.Delay(pollInterval);
            }

            stopwatch.Stop();

            if (!mountSuccessful)
            {
                if (mountProcess.HasExited)
                {
                    var exitCode = mountProcess.ExitCode;
                    var reason = GetExitCodeReason(exitCode);
                    _logger.Debug(
                        $"[MountZipFiles] Mount check failed. Drive {mountDriveRootForChecks} not found. The process exited with code {exitCode} ({reason}).");
                    throw new InvalidOperationException(
                        $"Failed to mount ZIP. {_zipMountExecutableName} exited with code {exitCode} ({reason}).");
                }

                _logger.Debug(
                    $"[MountZipFiles] Mount check failed. Drive {mountDriveRootForChecks} not found. The process was still running after timeout. Check the console window of {_zipMountExecutableName} for details.");
                throw new TimeoutException(
                    $"Failed to mount ZIP. Drive {mountDriveRootForChecks} not found after timeout.");
            }

            _logger.Debug(
                $"[MountZipFiles] Drive {mountDriveRootForChecks} detected. Proceeding to launch with {selectedEmulatorName}.");

            // --- Custom ScummVM Launch Logic ---

            // 1. Resolve Emulator Path
            if (string.IsNullOrWhiteSpace(selectedEmulatorManager.EmulatorLocation))
            {
                throw new FileNotFoundException(
                    $"Emulator executable path is not configured for '{selectedEmulatorName}'. " +
                    "Please edit the system configuration and provide a valid emulator path.");
            }

            var resolvedEmulatorExePath =
                PathHelper.ResolveRelativeToAppDirectory(selectedEmulatorManager.EmulatorLocation);
            if (string.IsNullOrEmpty(resolvedEmulatorExePath) || !File.Exists(resolvedEmulatorExePath))
            {
                throw new FileNotFoundException(
                    $"Emulator executable not found: {selectedEmulatorManager.EmulatorLocation}");
            }

            var resolvedEmulatorFolderPath = Path.GetDirectoryName(resolvedEmulatorExePath);
            if (string.IsNullOrEmpty(resolvedEmulatorFolderPath))
                throw new FileNotFoundException("Emulator executable folder could not be determined");

            // 2. Resolve Parameters
            var romSystemFolder = selectedSystemManager != null
                ? PathHelper.FindContainingSystemFolder(selectedSystemManager.SystemFolders,
                    selectedSystemManager.PrimarySystemFolder, resolvedZipFilePath)
                : null;
            var resolvedParameters = PathHelper.ResolveParameterString(
                selectedEmulatorParameters,
                selectedSystemManager?.SystemFolders,
                resolvedEmulatorFolderPath,
                resolvedZipFilePath,
                romSystemFolder
            );

            // Navigate into nested single-folder directories to find the actual game files location
            var gamePath = FindScummVmGamePath(mountDriveRootForChecks, logErrors);
            // ScummVM -p expects just the drive letter (e.g. "Y:") for root paths, not "Y:\"
            var scummVmPath = string.Equals(gamePath, mountDriveRootForChecks, StringComparison.Ordinal)
                ? mountDriveRootForChecks.TrimEnd('\\')
                : gamePath;
            var arguments = $"-p \"{scummVmPath}\" {resolvedParameters} ";

            var psiEmulator = new ProcessStartInfo
            {
                FileName = resolvedEmulatorExePath,
                Arguments = arguments,
                WorkingDirectory = resolvedEmulatorFolderPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _logger.Debug($"[MountZipFiles] Launching ScummVM with mounted ZIP:\n\n" +
                          $"Program Location: {psiEmulator.FileName}\n" +
                          $"Arguments: {psiEmulator.Arguments}\n" +
                          $"Working Directory: {psiEmulator.WorkingDirectory}");

            // 3. Launch Emulator
            using (var emulatorProcess = new Process())
            {
                emulatorProcess.StartInfo = psiEmulator;

                // Both pipes are redirected, so they must be drained: a full pipe would
                // block the child and WaitForExitAsync would never complete.
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                emulatorProcess.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data)) outputBuilder.AppendLine(args.Data);
                };
                emulatorProcess.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data)) errorBuilder.AppendLine(args.Data);
                };

                emulatorProcess.Start();
                emulatorProcess.BeginOutputReadLine();
                emulatorProcess.BeginErrorReadLine();
                await emulatorProcess.WaitForExitAsync();
                _logger.Debug($"[MountZipFiles] ScummVM process has exited with code: {emulatorProcess.ExitCode}.");
                if (outputBuilder.Length > 0 || errorBuilder.Length > 0)
                {
                    _logger.Debug(
                        $"[MountZipFiles] ScummVM output: {outputBuilder} {errorBuilder}");
                }
            }

            _logger.Debug($"[MountZipFiles] Emulator for {mountDriveRootForChecks} has exited.");
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountZipFiles] Exception during ScummVM ZIP mounting or launching: {ex}");

            // Notify developer
            // HasExited throws InvalidOperationException when Start() itself failed (no
            // process associated); treating "never started" as unknown keeps the original
            // error visible and the user-facing dialog reachable.
            var mountExitCode = GetMountProcessExitCodeOrNull(mountProcess);
            var exitCodeInfoInCatch = mountExitCode.HasValue
                ? $"Exit Code: {mountExitCode.Value}"
                : "Process was still running or state unknown.";
            var contextMessage = $"Error during ScummVM ZIP mount/launch process for {resolvedZipFilePath}.\n" +
                                 $"Exception: {ex.Message}\n" +
                                 $"The tool's output was not redirected. {exitCodeInfoInCatch}";
            logErrors.Error(ex, contextMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync(
                GetMountProcessExitCodeOrNull(mountProcess));
        }
        finally
        {
            _logger.Debug(
                $"[MountZipFiles] Entering finally block for {resolvedZipFilePath}. Mount Process ID: {mountProcessId}");
            if (mountProcess != null && mountProcessId != -1 && !mountProcess.HasExited)
            {
                _logger.Debug(
                    $"[MountZipFiles] Attempting to unmount by terminating {_zipMountExecutableName} (ID: {mountProcessId}).");
                try
                {
                    mountProcess.Kill(true);
                    _logger.Debug(
                        $"[MountZipFiles] Kill signal sent to {_zipMountExecutableName} (ID: {mountProcessId}). Waiting for process to exit (up to 20s).");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    try
                    {
                        await mountProcess.WaitForExitAsync(cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Debug(
                            $"[MountZipFiles] Timeout (10s) waiting for {_zipMountExecutableName} (ID: {mountProcessId}) to exit after Kill.");
                    }

                    if (mountProcess.HasExited)
                    {
                        _logger.Debug(
                            $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) terminated. Exit code: {mountProcess.ExitCode.ToString(CultureInfo.InvariantCulture)}.");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) did NOT terminate after Kill signal and 10s wait.");
                    }
                }
                catch (Exception termEx)
                {
                    _logger.Debug(
                        $"[MountZipFiles] Exception while terminating {_zipMountExecutableName} (ID: {mountProcessId}): {termEx}");

                    // Notify developer
                    logErrors.Error(termEx,
                        $"Failed to terminate {_zipMountExecutableName} (ID: {mountProcessId}) for unmounting.");
                }
            }
            else if (mountProcessId != -1)
            {
                var exitCodeStr =
                    GetMountProcessExitCodeOrNull(mountProcess)?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
                _logger.Debug(
                    $"[MountZipFiles] {_zipMountExecutableName} (ID: {mountProcessId}) had already exited or was not running when finally cleanup was attempted. Exit code likely {exitCodeStr}.");
            }
            else
            {
                _logger.Debug(
                    $"[MountZipFiles] {_zipMountExecutableName} process was not started successfully (ID: {mountProcessId}). No termination needed.");
            }

            mountProcess?.Dispose();

            await Task.Delay(2000);
            if (Directory.Exists(mountDriveRootForChecks))
            {
                _logger.Debug(
                    $"[MountZipFiles] WARNING: Drive {mountDriveRootForChecks} still exists after attempting to unmount.");
            }
            else
            {
                _logger.Debug($"[MountZipFiles] Drive {mountDriveRootForChecks} successfully unmounted.");
            }

            // Safety net: ensure all SimpleZipDrive processes are killed
            KillAllSimpleZipDriveProcesses(logErrors);
        }
    }

    private static string GetArchitectureSpecificExecutableName()
    {
        var arch = RuntimeInformation.ProcessArchitecture;

        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return arch switch
        {
            Architecture.X64 => "SimpleZipDrive.exe",
            Architecture.Arm64 => "SimpleZipDrive_arm64.exe",
            _ => throw new PlatformNotSupportedException($"Architecture {arch} is not supported by SimpleZipDrive.")
        };
    }

    private static string GetExitCodeReason(int exitCode)
    {
        return exitCode switch
        {
            -1073741515 => "STATUS_DLL_NOT_FOUND (Dokan library is not installed)",
            -1073741510 =>
                "STATUS_ORDINAL_NOT_FOUND (Dokan library version mismatch — the installed version may be incompatible)",
            _ => "unknown error"
        };
    }

    /// <summary>
    ///     Finds an available drive letter, preferring the configured letter, then searching from Z: down to D:.
    /// </summary>
    /// <returns>An available character for a drive letter, or null if none are available.</returns>
    private char? GetAvailableDriveLetter(ILogger logErrors)
    {
        try
        {
            // Use Environment.GetLogicalDrives() to avoid hanging on disconnected network drives
            var existingDrives = Environment.GetLogicalDrives()
                .Select(static d => char.ToUpper(d[0], CultureInfo.InvariantCulture))
                .ToHashSet();

            // First, try the preferred drive letter from configuration
            var preferredLetter = char.ToUpper(_preferredMountDriveLetterOnly[0], CultureInfo.InvariantCulture);
            if (!existingDrives.Contains(preferredLetter))
            {
                _logger.Debug(
                    $"[MountZipFiles.GetAvailableDriveLetter] Preferred drive letter {preferredLetter}: is available.");
                return preferredLetter;
            }

            _logger.Debug(
                $"[MountZipFiles.GetAvailableDriveLetter] Preferred drive letter {preferredLetter}: is already in use. Searching for alternative...");

            // If preferred is not available, search from Z: down to D:
            for (var letter = 'Z'; letter >= 'D'; letter--)
            {
                if (!existingDrives.Contains(letter))
                {
                    _logger.Debug($"[MountZipFiles.GetAvailableDriveLetter] Found available drive letter: {letter}:");
                    return letter;
                }
            }

            _logger.Debug(
                "[MountZipFiles.GetAvailableDriveLetter] No available drive letters found between D: and Z:");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountZipFiles.GetAvailableDriveLetter] Error enumerating drives: {ex.Message}");
            logErrors.Error(ex, "Error enumerating available drive letters for ZIP mounting");
            return null;
        }
    }

    private void ValidateZipForPathTraversal(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            _logger.Debug($"[MountZipFiles] Compressed file not found: {archivePath}");
            throw new FileNotFoundException($"Compressed file not found: {archivePath}", archivePath);
        }

        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);

            foreach (var entry in archive.Entries)
            {
                // Skip directory entries
                if (entry.IsDirectory)
                    continue;

                var entryName = entry.Key;

                if (string.IsNullOrEmpty(entryName))
                    continue;

                // Check for common path traversal indicators
                if (entryName.Contains("..", StringComparison.Ordinal) ||
                    Path.IsPathRooted(entryName) ||
                    entryName.StartsWith('/') ||
                    entryName.StartsWith('\\'))
                {
                    _logger.Debug($"[MountZipFiles] Archive contains path traversal entry: '{entryName}'");
                    throw new InvalidOperationException($"Archive contains path traversal entry: '{entryName}'");
                }

                // Additional thorough check: simulate extraction path normalization
                var normalizedEntryName = entryName.Replace('/', Path.DirectorySeparatorChar);
                var simulatedFullPath = Path.GetFullPath(Path.Combine("D:\\MOCKROOT", normalizedEntryName));
                if (!simulatedFullPath.StartsWith("D:\\MOCKROOT", StringComparison.Ordinal))
                {
                    _logger.Debug(
                        $"[MountZipFiles] Archive entry escapes simulated root: '{entryName}' -> '{simulatedFullPath}'");
                    throw new InvalidOperationException($"Archive entry escapes root: '{entryName}'");
                }
            }
        }
        catch (Exception ex) when (ex is not (FileNotFoundException or InvalidOperationException))
        {
            // Archive is corrupted or in an unsupported format — skip validation.
            // SharpCompress cannot open the archive, so there are no entries to check
            // for path traversal. The caller should notify the user that the file is corrupt.
            _logger.Debug(
                $"[MountZipFiles] Skipping path traversal validation — unable to open archive: {archivePath}. Error: {ex.Message}");
            throw new InvalidOperationException($"Archive is corrupted or unsupported: {archivePath}", ex);
        }
    }

    private string? FindNestedFile(string directoryPath, ILogger logErrors)
    {
        const string targetFolderName = "000D0000";
        try
        {
            _logger.Debug($"[FindNestedFile] Searching for directory '{targetFolderName}' in {directoryPath}...");
            var targetDirs = Directory.GetDirectories(directoryPath, targetFolderName, SearchOption.AllDirectories);

            if (targetDirs.Length > 0)
            {
                var nestedDirPath = targetDirs[0];
                _logger.Debug(
                    $"[FindNestedFile] Found directory at: {nestedDirPath}. Searching for first file inside...");

                var filesInNestedDir = Directory.GetFiles(nestedDirPath, "*", SearchOption.TopDirectoryOnly);
                if (filesInNestedDir.Length > 0)
                {
                    var fileToLaunch = filesInNestedDir[0];
                    _logger.Debug($"[FindNestedFile] Found file to launch in nested directory: {fileToLaunch}");
                    return fileToLaunch;
                }

                _logger.Debug(
                    $"[FindNestedFile] Directory '{nestedDirPath}' was found but is empty. Will check other folders.");
            }
            else
            {
                _logger.Debug(
                    $"[FindNestedFile] Directory '{targetFolderName}' not found in {directoryPath}. Will check other folders.");
            }

            // Check other folders if the nested folder doesn't exist or is empty
            var filesInRootDir = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            if (filesInRootDir.Length > 0)
            {
                var fileToLaunch = filesInRootDir[0];
                _logger.Debug($"[FindNestedFile] Found file to launch: {fileToLaunch}");
                return fileToLaunch;
            }

            _logger.Debug(
                $"[FindNestedFile] No files found in nested directory '{targetFolderName}' or inside other folders.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[FindNestedFile] Error searching for nested file in {directoryPath}: {ex.Message}");

            // Notify developer
            logErrors.Error(ex, $"Error in FindNestedFile searching {directoryPath}");

            return null;
        }
    }

    private string FindScummVmGamePath(string mountDriveRootForChecks, ILogger logErrors)
    {
        try
        {
            _logger.Debug($"[FindScummVmGamePath] Searching for game files in {mountDriveRootForChecks}...");

            var currentPath = mountDriveRootForChecks;

            while (true)
            {
                var directories = Directory.GetDirectories(currentPath);
                var files = Directory.GetFiles(currentPath);

                if (files.Length > 0)
                {
                    _logger.Debug($"[FindScummVmGamePath] Found files in: {currentPath}");
                    return currentPath;
                }

                switch (directories.Length)
                {
                    case 1:
                        _logger.Debug($"[FindScummVmGamePath] Single folder found, navigating into: {directories[0]}");
                        currentPath = directories[0];
                        continue;
                    case > 1:
                        _logger.Debug(
                            $"[FindScummVmGamePath] Multiple folders found, using current path: {currentPath}");
                        return currentPath;
                    default:
                        _logger.Debug($"[FindScummVmGamePath] Empty directory, returning current path: {currentPath}");
                        return currentPath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"[FindScummVmGamePath] Error: {ex.Message}");

            // Notify developer
            logErrors.Error(ex, "Error in FindScummVmGamePath");

            return mountDriveRootForChecks;
        }
    }

    /// <summary>
    ///     Returns the mount process exit code when the process actually started and has
    ///     exited; otherwise null. <see cref="Process.HasExited" /> throws
    ///     InvalidOperationException when Start() itself failed (no process associated),
    ///     which used to replace the original error and skip the user-facing dialog.
    /// </summary>
    private static int? GetMountProcessExitCodeOrNull(Process? mountProcess)
    {
        if (mountProcess is null) return null;

        try
        {
            return mountProcess.HasExited ? mountProcess.ExitCode : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}