using System.Diagnostics;

namespace SimpleLauncher.Avalonia.Updater.Services;

/// <summary>
///     Service for managing process operations like waiting for process exit and restarting applications.
/// </summary>
internal class ProcessService
{
    private const int ProcessExitTimeoutMs = 30000; // 30 seconds timeout for main app to exit
    private const int ProcessExitPollIntervalMs = 500; // Poll every 500ms to check if process exited

    /// <summary>
    ///     Process names (without extension) of the applications the single updater serves.
    ///     PIDs and application arguments are only trusted when they match one of these.
    /// </summary>
    internal const string WpfAppProcessName = "SimpleLauncher";

    internal const string AvaloniaAppProcessName = "SimpleLauncher.Avalonia";

    /// <summary>
    ///     Event raised when a log message needs to be displayed.
    /// </summary>
    public event EventHandler<EventArgs<string>>? LogMessage;

    /// <summary>
    ///     Returns whether the given process name belongs to one of the applications the
    ///     updater serves.
    /// </summary>
    internal static bool IsKnownAppProcessName(string processName)
    {
        return string.Equals(processName, WpfAppProcessName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(processName, AvaloniaAppProcessName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Resolves which application the updater targets. New releases pass the application
    ///     executable name as the second argument; older WPF releases passed only the PID, so
    ///     the application is detected from the process name as a fallback.
    /// </summary>
    /// <param name="appArgument">The optional application argument (e.g. "SimpleLauncher.exe").</param>
    /// <param name="processId">The optional candidate process ID.</param>
    /// <returns>The known application process name, or null when it cannot be resolved.</returns>
    internal static string? TryResolveAppProcessName(string? appArgument, int? processId)
    {
        if (!string.IsNullOrWhiteSpace(appArgument))
        {
            var candidate = Path.GetFileName(appArgument.Trim());
            if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                candidate = candidate[..^4];

            if (IsKnownAppProcessName(candidate))
                return candidate;

            // Expected condition (a manually launched updater or a stray argument):
            // Information level so it is never reported as a bug.
            Log.Information("Ignoring unknown application argument '{Argument}'", appArgument);
        }

        if (processId.HasValue)
        {
            try
            {
                using var process = Process.GetProcessById(processId.Value);
                if (IsKnownAppProcessName(process.ProcessName))
                    return process.ProcessName;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // The process already exited — the caller falls back to the default.
            }
            catch (Exception ex)
            {
                // E.g. access denied reading an elevated process — never trust it, but this is
                // an expected environment condition: Information level so it is not reported.
                Log.Information(ex, "Could not detect the application from process ID {Pid}", processId);
            }
        }

        return null;
    }

    /// <summary>
    ///     Validates a candidate main-application PID taken from the command line (UPD-08).
    ///     The process must exist, be alive, and carry the expected main-app process name —
    ///     PID reuse or a malicious argument must never steer the exit-wait onto an
    ///     unrelated process (30s stall then abort, or proceeding while the real app
    ///     still holds file locks). Returns null when the PID is unusable, in which case
    ///     the caller falls back to the by-name wait.
    /// </summary>
    /// <param name="pid">The candidate process ID.</param>
    /// <param name="expectedProcessName">The expected application process name.</param>
    /// <returns>The PID when valid; otherwise null.</returns>
    internal static int? ValidateProcessId(int pid, string expectedProcessName)
    {
        if (pid <= 0)
            return null;

        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited)
                return null;

            if (!string.Equals(process.ProcessName, expectedProcessName,
                    StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning(
                    "Ignoring process ID argument {Pid}: process name is '{Actual}' (expected '{Expected}')",
                    pid, process.ProcessName, expectedProcessName);
                return null;
            }

            return pid;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // No such process, or it exited mid-check — fall back to the by-name wait.
            Log.Information("Ignoring process ID argument {Pid}: process not running", pid);
            return null;
        }
        catch (Exception ex)
        {
            // E.g. access denied reading an elevated process — never trust it.
            Log.Warning(ex, "Ignoring process ID argument {Pid}: could not verify the process", pid);
            return null;
        }
    }

    /// <summary>
    ///     Waits for the main application process to exit.
    ///     UPD-09: the null-PID path waits for ALL matching instances (not just the first)
    ///     and throws <see cref="TimeoutException" /> on timeout — identical semantics to
    ///     the PID path — instead of logging "proceeding anyway" into live file locks.
    /// </summary>
    /// <param name="processId">The process ID of the main application, or null if not available.</param>
    /// <param name="expectedProcessName">The expected application process name (used for PID validation and by-name lookup).</param>
    /// <param name="cancellationToken">Token to cancel the wait operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">Thrown when the process does not exit within the timeout period.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task WaitForProcessExitAsync(int? processId, string expectedProcessName,
        CancellationToken cancellationToken = default)
    {
        if (processId.HasValue)
        {
            try
            {
                using var mainAppProcess = Process.GetProcessById(processId.Value);
                LogMessage?.Invoke(this,
                    new EventArgs<string>($"Waiting for {expectedProcessName} (PID: {processId}) to exit..."));

                var stopwatch = Stopwatch.StartNew();
                while (!mainAppProcess.HasExited && stopwatch.ElapsedMilliseconds < ProcessExitTimeoutMs)
                {
                    await Task.Delay(ProcessExitPollIntervalMs, cancellationToken);
                    mainAppProcess.Refresh();
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!mainAppProcess.HasExited)
                {
                    throw new TimeoutException(
                        $"{expectedProcessName} (PID: {processId}) did not exit within {ProcessExitTimeoutMs / 1000} seconds. " +
                        "The process may be unresponsive or still shutting down.");
                }

                // Add a small delay to ensure file handles are released
                await Task.Delay(500, cancellationToken);
                LogMessage?.Invoke(this, new EventArgs<string>($"{expectedProcessName} has exited."));
            }
            catch (ArgumentException)
            {
                // Expected condition: the application already exited before the poll started —
                // log at Information level, not a bug report.
                Log.Information("{ProcessName} process not found (PID: {ProcessId}). Assuming it has already exited",
                    expectedProcessName, processId);
                LogMessage?.Invoke(this,
                    new EventArgs<string>($"{expectedProcessName} process not found. Assuming it has already exited."));
            }
        }
        else
        {
            LogMessage?.Invoke(this,
                new EventArgs<string>(
                    $"No PID provided. Searching for {expectedProcessName} process by name..."));

            var processes = Process.GetProcessesByName(expectedProcessName);
            if (processes.Length > 0)
            {
                try
                {
                    LogMessage?.Invoke(this,
                        new EventArgs<string>(
                            $"Found {processes.Length} {expectedProcessName} process(es). Waiting for all to exit..."));

                    var stopwatch = Stopwatch.StartNew();
                    bool allExited;
                    do
                    {
                        allExited = true;
                        foreach (var process in processes)
                        {
                            process.Refresh();
                            if (!process.HasExited)
                            {
                                allExited = false;
                                break;
                            }
                        }

                        if (!allExited)
                            await Task.Delay(ProcessExitPollIntervalMs, cancellationToken);
                    } while (!allExited && stopwatch.ElapsedMilliseconds < ProcessExitTimeoutMs);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (!allExited)
                    {
                        throw new TimeoutException(
                            $"{expectedProcessName} did not exit within {ProcessExitTimeoutMs / 1000} seconds. " +
                            "The process may be unresponsive or still shutting down.");
                    }

                    LogMessage?.Invoke(this, new EventArgs<string>($"{expectedProcessName} has exited."));
                }
                catch (InvalidOperationException)
                {
                    // Expected condition: process exited between GetProcessesByName and HasExited check
                    Log.Information(
                        "{ProcessName} process disappeared during wait. Assuming it has already exited",
                        expectedProcessName);
                    LogMessage?.Invoke(this,
                        new EventArgs<string>(
                            $"{expectedProcessName} process disappeared. Assuming it has already exited."));
                }
                finally
                {
                    foreach (var p in processes) p.Dispose();
                }
            }
            else
            {
                LogMessage?.Invoke(this,
                    new EventArgs<string>($"{expectedProcessName} process not found. Proceeding immediately."));
            }

            // Small delay to ensure file handles are released
            await Task.Delay(500, cancellationToken);
        }
    }

    /// <summary>
    ///     Restarts the main application after an update.
    /// </summary>
    /// <param name="appDirectory">The directory containing the application executable.</param>
    /// <param name="executableName">The name of the executable to start (without .exe extension).</param>
    /// <param name="arguments">Command line arguments to pass to the executable.</param>
    /// <returns>True if the process was started successfully, false otherwise.</returns>
    public bool RestartApplication(string appDirectory, string executableName, string arguments)
    {
        try
        {
            var baseName = executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? executableName[..^4]
                : executableName;
            var executableFileName = OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;
            var exePath = Path.Combine(appDirectory, executableFileName);

            // Check if the executable exists before attempting to start it
            if (!File.Exists(exePath))
            {
                LogMessage?.Invoke(this,
                    new EventArgs<string>($"{executableFileName} not found. Cannot restart automatically."));
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                // Shell-execute is Windows-only behavior (UAC/verb handling); on Unix the
                // application is executed directly.
                UseShellExecute = OperatingSystem.IsWindows(),
                WorkingDirectory = appDirectory
            };

            // UPD-16: Process.Start can return null (e.g. shell-execute to an existing
            // instance handler) — that is NOT a successful restart.
            using var startedProcess = Process.Start(startInfo);
            if (startedProcess == null)
            {
                Log.Warning("Restart of {Executable} reported no new process handle", executableFileName);
                LogMessage?.Invoke(this,
                    new EventArgs<string>(
                        $"Could not restart {executableFileName}: the process did not start."));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Fire-and-forget async bug report with exception logging
            _ = ReportBugFireAndForgetAsync(ex, $"Failed to restart application: {executableName}");

            LogMessage?.Invoke(this, new EventArgs<string>($"Failed to restart the main application: {ex.Message}"));
            return false;
        }
    }

    /// <summary>
    ///     Opens a URL in the default web browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open URL: {Url}", url);
            LogMessage?.Invoke(this, new EventArgs<string>($"Failed to open URL: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Fire-and-forget helper for reporting bugs from synchronous contexts.
    ///     Logs exceptions to Debug output if the bug report itself fails.
    /// </summary>
    private static async Task ReportBugFireAndForgetAsync(Exception exception, string context)
    {
        try
        {
            await BugReportService.ReportBugAsync(exception, context);
        }
        catch (Exception ex)
        {
            // If bug reporting fails, log via Serilog - don't throw
            Log.Warning(ex, "Failed to report bug for context: {Context}", context);
            Log.Warning(exception, "Original exception");
        }
    }
}