using System.Diagnostics;
using System.Text;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Handles mounting ISO disc images using PowerShell and launching games from the mounted drive.
/// </summary>
public class MountIsoFiles : IMountIsoFiles
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MountIsoFiles" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public MountIsoFiles(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Mounts an ISO file, locates EBOOT.BIN, and launches it with the specified emulator.
    /// </summary>
    public async Task MountIsoFileAsync(
        string resolvedIsoFilePath,
        string selectedSystemName,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        string logPath,
        ILauncherService gameLauncher,
        ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        _logger.Debug($"[MountIsoFiles] Starting to mount ISO using PowerShell: {resolvedIsoFilePath}");
        _logger.Debug($"[MountIsoFiles] System: {selectedSystemName}, Emulator: {selectedEmulatorName}");

        string? mountPath = null;

        if (resolvedIsoFilePath == null)
        {
            // Notify developer
            var contextMessage = $"Resolved ISO path is null. ISO: {resolvedIsoFilePath}";
            logErrors.Warning(contextMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();

            return;
        }

        try
        {
            // 1. Mount ISO and get drive letter
            var mountedDriveLetter =
                await ExecutePowerShellMountCommandAsync(resolvedIsoFilePath, logErrors, messageBox);

            if (string.IsNullOrEmpty(mountedDriveLetter))
            {
                // Error already logged by ExecutePowerShellMountCommandAsync
                // User already notified by ExecutePowerShellMountCommandAsync or will be here
                await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
                return;
            }

            mountPath = $"{mountedDriveLetter}:\\";
            _logger.Debug(
                $"[MountIsoFiles] ISO reportedly mounted to drive: {mountedDriveLetter}. Mount path: {mountPath}");

            // Poll for the drive to become available with a timeout
            if (!await WaitForDirectoryToExistAsync(mountPath, 10000, 200, logErrors))
            {
                var errorMessage =
                    $"Mount path {mountPath} does not exist after mounting ISO {resolvedIsoFilePath}. PowerShell might have failed silently or the drive is not accessible.";
                _logger.Debug($"[MountIsoFiles] Error: {errorMessage}");

                // Notify developer
                logErrors.Warning(errorMessage);

                // Notify user
                await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();

                // The finally block will attempt to dismount.
                return;
            }

            _logger.Debug($"[MountIsoFiles] Mount path {mountPath} confirmed to exist.");

            // 2. Find EBOOT.BIN in the mounted ISO
            _logger.Debug($"[MountIsoFiles] Searching for EBOOT.BIN in {mountPath}...");

            // Find EBOOT.BIN
            var ebootBinPath = FindEbootBin.FindEbootBinRecursive(mountPath, logErrors, _logger);

            if (string.IsNullOrEmpty(ebootBinPath))
            {
                var errorMessage =
                    $"EBOOT.BIN not found in mounted ISO at {mountPath}. Original ISO: {resolvedIsoFilePath}";
                _logger.Debug($"[MountIsoFiles] Error: {errorMessage}");

                // Notify developer
                logErrors.Error(new FileNotFoundException(errorMessage), errorMessage);

                // Notify user
                await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();

                return;
            }

            _logger.Debug($"[MountIsoFiles] EBOOT.BIN found at: {ebootBinPath}. Proceeding to launch.");

            // 3. Launch the game/emulator with EBOOT.BIN
            // Pass the original ISO file path for display in notifications
            await gameLauncher.LaunchRegularEmulatorAsync(ebootBinPath, selectedEmulatorName, selectedSystemManager,
                selectedEmulatorManager, rawEmulatorParameters, windowContext, null, resolvedIsoFilePath);

            _logger.Debug($"[MountIsoFiles] Emulator for {ebootBinPath} has exited.");
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountIsoFiles] Exception during ISO mount/launch process for {resolvedIsoFilePath}: {ex}");
            var contextMessage =
                $"Error during ISO mount/launch process for {resolvedIsoFilePath}.\nException: {ex.Message}";

            // Notify developer
            logErrors.Error(ex, contextMessage);

            // Notify user
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
        }
        finally
        {
            _logger.Debug($"[MountIsoFiles] Entering finally block for ISO: {resolvedIsoFilePath}.");
            if (!string.IsNullOrEmpty(resolvedIsoFilePath))
            {
                _logger.Debug($"[MountIsoFiles] Attempting to dismount ISO: {resolvedIsoFilePath}");
                await ExecutePowerShellDismountCommandAsync(resolvedIsoFilePath, logErrors, messageBox);

                if (!string.IsNullOrEmpty(mountPath))
                {
                    await Task.Delay(1000);
                    if (Directory.Exists(mountPath))
                    {
                        _logger.Debug(
                            $"[MountIsoFiles] WARNING: Mount path {mountPath} still exists after dismount attempt for ISO: {resolvedIsoFilePath}. Manual dismount might be needed.");
                    }
                    else
                    {
                        _logger.Debug(
                            $"[MountIsoFiles] Mount path {mountPath} successfully unmounted or no longer detected for ISO: {resolvedIsoFilePath}.");
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Waits for a directory to exist by polling at regular intervals until a timeout is reached.
    /// </summary>
    /// <param name="directoryPath">The directory path to wait for.</param>
    /// <param name="maxWaitTimeMs">Maximum wait time in milliseconds.</param>
    /// <param name="pollIntervalMs">Polling interval in milliseconds.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <returns>True if the directory appeared within the timeout; otherwise, false.</returns>
    public async Task<bool> WaitForDirectoryToExistAsync(string directoryPath, int maxWaitTimeMs, int pollIntervalMs,
        ILogger logErrors)
    {
        _logger.Debug(
            $"[MountIsoFiles] Waiting for directory to exist: {directoryPath} (max wait: {maxWaitTimeMs}ms, poll interval: {pollIntervalMs}ms)");

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < maxWaitTimeMs)
        {
            if (Directory.Exists(directoryPath))
            {
                _logger.Debug(
                    $"[MountIsoFiles] Directory confirmed to exist after {stopwatch.ElapsedMilliseconds}ms: {directoryPath}");
                return true;
            }

            await Task.Delay(pollIntervalMs);
        }

        _logger.Debug(
            $"[MountIsoFiles] Timeout waiting for directory to exist after {stopwatch.ElapsedMilliseconds}ms: {directoryPath}");
        return false;
    }

    /// <summary>
    ///     Mounts an ISO file using a PowerShell command and returns the assigned drive letter.
    /// </summary>
    /// <param name="isoPath">The path to the ISO file to mount.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    /// <returns>The drive letter assigned to the mounted ISO, or null if mounting failed.</returns>
    public async Task<string?> ExecutePowerShellMountCommandAsync(string isoPath, ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        var escapedIsoPath = isoPath.Replace("'", "''"); // Escape single quotes for PowerShell
        var command = $"$isoPath = '{escapedIsoPath}'; " +
                      "$diskImage = Mount-DiskImage -ImagePath $isoPath -PassThru -ErrorAction Stop; " +
                      "$driveLetter = ($diskImage | Get-Volume | Where-Object { $_.DriveLetter -ne $null -and $_.DriveType -eq 'CD-ROM' } | Select-Object -First 1).DriveLetter; " +
                      "if (-not $driveLetter) { throw 'Failed to get drive letter for mounted ISO. Ensure the ISO is valid and contains a recognized file system.' } " +
                      "Write-Output $driveLetter";

        _logger.Debug($"[MountIsoFiles] Executing PowerShell Mount Command: {command}");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"& {{ {command} }}\"",
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process();
        process.StartInfo = psi;
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null) outputBuilder.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null) errorBuilder.AppendLine(args.Data);
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30-second timeout

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cts.Token);

            var errors = errorBuilder.ToString().Trim();
            if (process.ExitCode != 0 || !string.IsNullOrEmpty(errors))
            {
                // Check for execution policy restrictions
                if (IsExecutionPolicyRestricted(errors))
                {
                    await messageBox.UnabletomountIsOfileMessageBoxAsync();

                    return null;
                }

                // Notify developer
                var errorMessage =
                    $"PowerShell command to mount ISO failed. Exit Code: {process.ExitCode}.\nPath: {isoPath}\nErrors: {errors}\nOutput: {outputBuilder}";
                _logger.Debug($"[MountIsoFiles] Error: {errorMessage}");
                logErrors.Warning(errorMessage);

                return null;
            }

            var driveLetter = outputBuilder.ToString().Trim();
            if (driveLetter.Length == 1 && char.IsLetter(driveLetter[0]))
            {
                _logger.Debug(
                    $"[MountIsoFiles] Successfully mounted ISO {isoPath} and retrieved drive letter: {driveLetter}");
                return driveLetter.ToUpperInvariant();
            }

            // Notify developer
            var failureMessage =
                $"Failed to parse drive letter from PowerShell output for ISO {isoPath}. Output: '{driveLetter}'\nErrors: {errors}";
            _logger.Debug($"[MountIsoFiles] Error: {failureMessage}");
            logErrors.Warning(failureMessage);

            return null;
        }
        catch (OperationCanceledException) // Catches TaskCanceledException from WaitForExitAsync with timeout
        {
            // Check if the error output contains execution policy restrictions
            var errorOutput = errorBuilder.ToString().Trim();
            if (IsExecutionPolicyRestricted(errorOutput)) await messageBox.UnabletomountIsOfileMessageBoxAsync();

            // Notify developer
            var timeoutMessage = $"PowerShell mount command timed out (30s) for ISO {isoPath}.";
            _logger.Debug($"[MountIsoFiles] Timeout: {timeoutMessage}");
            logErrors.Warning(timeoutMessage);

            if (process.HasExited) return null;

            try
            {
                process.Kill(true);
            }
            catch
            {
                /* Ignore errors killing timed-out process */
            }

            return null;
        }
        catch (Exception ex)
        {
            // Check if the exception message indicates execution policy restrictions
            if (IsExecutionPolicyRestricted(ex.Message)) await messageBox.UnabletomountIsOfileMessageBoxAsync();

            // Notify developer
            var errorMessage =
                $"Exception while executing PowerShell mount command for ISO {isoPath}: {ex.Message}\nOutput: {outputBuilder}\nError: {errorBuilder}";
            _logger.Debug($"[MountIsoFiles] Exception: {errorMessage}");
            logErrors.Error(ex, errorMessage);

            return null;
        }
    }

    /// <summary>
    ///     Dismounts a previously mounted ISO file using a PowerShell command.
    /// </summary>
    /// <param name="isoPath">The path to the ISO file to dismount.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    public async Task ExecutePowerShellDismountCommandAsync(string isoPath, ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        var escapedIsoPath = isoPath.Replace("'", "''");
        var command = $"Dismount-DiskImage -ImagePath '{escapedIsoPath}' -ErrorAction SilentlyContinue";

        _logger.Debug($"[MountIsoFiles] Executing PowerShell Dismount Command: {command}");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"& {{ {command} }}\"",
            // No stdout redirect: it was never drained (Dismount-DiskImage with
            // -ErrorAction SilentlyContinue is silent), a latent pipe-deadlock pattern.
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = psi;
        var errorBuilder = new StringBuilder();
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null) errorBuilder.AppendLine(args.Data);
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30-second timeout

        try
        {
            process.Start();
            process.BeginErrorReadLine(); // Capture errors
            // Standard output is usually not relevant for dismount with SilentlyContinue
            await process.WaitForExitAsync(cts.Token);

            var errors = errorBuilder.ToString().Trim();
            if (process.ExitCode != 0 || !string.IsNullOrEmpty(errors))
            {
                // Check for execution policy restrictions
                if (IsExecutionPolicyRestricted(errors)) await messageBox.UnabletoDismountIsOfileMessageBoxAsync();

                var warningMessage =
                    $"PowerShell dismount command for ISO {isoPath} finished with Exit Code: {process.ExitCode} or reported errors (ErrorAction SilentlyContinue was used).\nErrors: {errors}";
                _logger.Debug($"[MountIsoFiles] Info: {warningMessage}"); // Log as Info/Warning
            }
            else
            {
                _logger.Debug($"[MountIsoFiles] PowerShell dismount command executed successfully for ISO: {isoPath}.");
            }
        }
        catch (OperationCanceledException)
        {
            // Check if the error output contains execution policy restrictions
            var errorOutput = errorBuilder.ToString().Trim();
            if (IsExecutionPolicyRestricted(errorOutput)) await messageBox.UnabletoDismountIsOfileMessageBoxAsync();

            // Notify developer
            var timeoutMessage = $"PowerShell dismount command timed out (30s) for ISO {isoPath}.";
            _logger.Debug($"[MountIsoFiles] Timeout: {timeoutMessage}");
            logErrors.Warning(timeoutMessage); // Log timeout as an error

            if (!process.HasExited)
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    /* Ignore */
                }
            }
        }
        catch (Exception ex)
        {
            // Check if the exception message indicates execution policy restrictions
            if (IsExecutionPolicyRestricted(ex.Message)) await messageBox.UnabletoDismountIsOfileMessageBoxAsync();

            // Notify developer
            var errorMessage = $"Exception while executing PowerShell dismount command for ISO {isoPath}: {ex.Message}";
            _logger.Debug($"[MountIsoFiles] Exception: {errorMessage}");
            logErrors.Error(ex, errorMessage);
        }
    }

    /// <summary>
    ///     Detects if PowerShell error output indicates execution policy restrictions
    /// </summary>
    private static bool IsExecutionPolicyRestricted(string errorOutput)
    {
        if (string.IsNullOrWhiteSpace(errorOutput)) return false;

        var lowerError = errorOutput.ToLowerInvariant();
        return lowerError.Contains("execution of scripts is disabled", StringComparison.Ordinal) ||
               (lowerError.Contains("execution policy", StringComparison.Ordinal) &&
                (lowerError.Contains("prevents execution", StringComparison.Ordinal) ||
                 lowerError.Contains("restricted", StringComparison.Ordinal) ||
                 lowerError.Contains("cannot be loaded", StringComparison.Ordinal))) ||
               (lowerError.Contains("is not digitally signed", StringComparison.Ordinal) &&
                lowerError.Contains("execution policy", StringComparison.Ordinal));
    }
}