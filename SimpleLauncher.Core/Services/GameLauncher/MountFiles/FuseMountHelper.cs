using System.Collections.Concurrent;
using System.Diagnostics;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Shared lifecycle for the bundled Linux FUSE mount tools (SimpleZipDrive, SimpleXisoDrive).
///     Mirrors the verified CHDMounter flow in <see cref="MountChdFiles" />: the launcher owns a
///     mount-point directory, the tool mounts into it, readiness is detected by polling the
///     mount point, and teardown sends SIGTERM first (a clean unmount) before falling back to
///     Kill + fusermount3 and deleting the directory.
/// </summary>
internal static class FuseMountHelper
{
    private const int MountTimeoutAttempts = 120;
    private const int MountPollIntervalMs = 500;

    /// <summary>
    ///     Creates a unique, empty mount-point directory under the per-user temp folder. The
    ///     directory must exist before the FUSE tool starts; it is deleted again after unmount.
    /// </summary>
    internal static string CreateMountPoint(string prefix)
    {
        var mountPoint = Path.Combine(Path.GetTempPath(), "SimpleLauncher", prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mountPoint);
        return mountPoint;
    }

    /// <summary>
    ///     Starts a FUSE mount tool (redirecting stderr into a buffer for diagnostics) and
    ///     returns the created process. A failure to start (missing execute bit, missing .NET
    ///     runtime, ...) is an expected user-environment condition: it is logged at Information
    ///     level and reported as <c>Started=false</c> so the caller can fall back.
    /// </summary>
    internal static (Process? Process, bool Started) StartFuseMountTool(string toolPath, string arguments,
        string toolDisplayName, ILogger logErrors, out ConcurrentQueue<string> errorOutput)
    {
        var errorQueue = new ConcurrentQueue<string>();
        errorOutput = errorQueue;

        try
        {
            EnsureUnixExecuteBit(toolPath);

            var psi = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(toolPath) ?? AppDomain.CurrentDomain.BaseDirectory
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!process.Start())
            {
                logErrors.Information($"Could not start {toolDisplayName} (missing payload or .NET runtime?).");
                process.Dispose();
                return (null, false);
            }

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) errorQueue.Enqueue(e.Data);
            };
            process.BeginErrorReadLine();

            logErrors.Debug($"[FuseMountHelper] {toolDisplayName} process started (ID: {process.Id}).");
            return (process, true);
        }
        catch (Exception ex)
        {
            logErrors.Information($"Could not start {toolDisplayName}: {ex.Message}");
            return (null, false);
        }
    }

    /// <summary>
    ///     Polls until the FUSE session exposes the mounted filesystem. When
    ///     <paramref name="expectedEntryName" /> is given the mount is only considered ready once
    ///     that root entry exists (for example <c>default.xbe</c> or <c>image.iso</c>); otherwise a
    ///     non-empty listing marks readiness. An early process exit or a timeout is an expected
    ///     condition (the caller falls back), logged at Information level with the tool's stderr.
    /// </summary>
    internal static async Task<bool> WaitForFuseMountReadyAsync(string mountPoint, string? expectedEntryName,
        Process process, string toolDisplayName, string sourceFileName, ILogger logErrors,
        ConcurrentQueue<string> errorOutput)
    {
        for (var attempt = 0; attempt < MountTimeoutAttempts; attempt++)
        {
            if (IsFuseMountReady(mountPoint, expectedEntryName))
            {
                logErrors.Debug(
                    $"[FuseMountHelper] {toolDisplayName} mount ready after {attempt * MountPollIntervalMs / 1000.0:F1} seconds ('{sourceFileName}').");
                return true;
            }

            if (process.HasExited)
            {
                logErrors.Information(DescribeFailure(toolDisplayName, sourceFileName,
                    $"exited prematurely with code {process.ExitCode}", errorOutput));
                return false;
            }

            await Task.Delay(MountPollIntervalMs);
        }

        logErrors.Information(DescribeFailure(toolDisplayName, sourceFileName,
            $"timed out after {MountTimeoutAttempts * MountPollIntervalMs / 1000} seconds", errorOutput));
        return false;
    }

    /// <summary>
    ///     Unmounts and cleans up: SIGTERM first (the FUSE tools release the kernel mount when
    ///     they shut down cleanly), then Kill if needed, then a forced fusermount3/umount release
    ///     (a killed daemon leaves the mount behind), and finally removes the mount-point
    ///     directory.
    /// </summary>
    internal static async Task ReleaseAsync(Process? process, string mountPoint, ILogger logger)
    {
        if (process != null)
        {
            try
            {
                if (!process.HasExited) SendUnixSignal(process.Id, "TERM", logger);

                if (!await WaitForExitAsync(process, TimeSpan.FromSeconds(10)))
                {
                    try
                    {
                        if (!process.HasExited) process.Kill(true);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                    {
                        // Already exited between the check and the kill.
                    }

                    if (!await WaitForExitAsync(process, TimeSpan.FromSeconds(20)))
                    {
                        logger.Debug(
                            $"[FuseMountHelper] Mount tool (ID: {process.Id}) did not exit after Kill and 20s wait.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"[FuseMountHelper] Error terminating the mount tool: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        FuseValidation.TryUnmount(mountPoint, logger);
        await TryDeleteMountPointDirectoryAsync(mountPoint, logger);
    }

    /// <summary>
    ///     Checks whether the mount point exposes the mounted filesystem. The directory exists
    ///     (empty) before the FUSE session is up, so a root entry means the mount is live.
    /// </summary>
    internal static bool IsFuseMountReady(string mountPoint, string? expectedEntryName = null)
    {
        try
        {
            if (expectedEntryName != null)
                return File.Exists(Path.Combine(mountPoint, expectedEntryName));

            return Directory.EnumerateFileSystemEntries(mountPoint).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void SendUnixSignal(int processId, string signal, ILogger logger)
    {
        try
        {
            using var kill = Process.Start(new ProcessStartInfo
            {
                FileName = "kill",
                Arguments = $"-{signal} {processId}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            kill?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            logger.Debug($"[FuseMountHelper] Could not send SIG{signal} to {processId}: {ex.Message}");
        }
    }

    private static async Task TryDeleteMountPointDirectoryAsync(string directory, ILogger logger)
    {
        for (var attempt = 0; attempt < 10 && Directory.Exists(directory); attempt++)
        {
            try
            {
                Directory.Delete(directory);
                logger.Debug($"[FuseMountHelper] Removed the temporary mount point '{directory}'.");
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(500);
            }
        }

        if (Directory.Exists(directory))
            logger.Debug(
                $"[FuseMountHelper] WARNING: Mount point '{directory}' still exists after attempting to unmount.");
    }

    /// <summary>
    ///     Git checkouts and SCP deployments do not preserve the Unix execute bit; the release
    ///     ZIPs do (the Linux packaging script stamps them). Best effort — a failure surfaces when
    ///     the tool is started.
    /// </summary>
    private static void EnsureUnixExecuteBit(string toolPath)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            const UnixFileMode executeBits =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            var mode = File.GetUnixFileMode(toolPath);
            if ((mode & executeBits) != executeBits) File.SetUnixFileMode(toolPath, mode | executeBits);
        }
        catch
        {
            // Best effort only.
        }
    }

    private static string DescribeFailure(string toolDisplayName, string sourceFileName, string reason,
        ConcurrentQueue<string> errorOutput)
    {
        var message = $"Failed to mount '{sourceFileName}': {toolDisplayName} {reason}.";
        return errorOutput is { Count: > 0 }
            ? message + "\n\n=== " + toolDisplayName + " error output ===\n" +
              string.Join(Environment.NewLine, errorOutput)
            : message;
    }
}
