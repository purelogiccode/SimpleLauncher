using System.Diagnostics;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <inheritdoc />
/// <summary>
///     Represents a temporarily mounted XISO drive.
///     Disposing this object will unmount the drive by terminating the mounting process.
/// </summary>
public class MountXisoDrive : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Process? _mountProcess;
    private readonly int _mountProcessId;
    private readonly bool _isUnixFuseMount;
    private readonly string _mountPointDirectory = "";

    /// <summary>
    ///     Constructor for a successful mount.
    /// </summary>
    public MountXisoDrive(Process mountProcess, string mountedPath, ILogger logErrors, ILogger logger)
    {
        _mountProcess = mountProcess;
        _mountProcessId = mountProcess?.Id ?? -1;
        _logger = logErrors;
        _logger = logger;
        MountedPath = mountedPath;
        IsMounted = !string.IsNullOrEmpty(mountedPath) && _mountProcess != null;

        // Unix (FUSE) mounts expose a file inside a launcher-owned mount-point directory; the
        // directory must be released and removed after the mount tool exits (Windows mounts a
        // drive root instead and needs none of this).
        _isUnixFuseMount = !OperatingSystem.IsWindows() && IsMounted;
        if (_isUnixFuseMount) _mountPointDirectory = Path.GetDirectoryName(mountedPath) ?? "";
    }

    /// <summary>
    ///     Constructor for a failed mount.
    /// </summary>
    public MountXisoDrive(ILogger logErrors, ILogger logger)
    {
        _logger = logErrors;
        _logger = logger;
        IsMounted = false;
    }

    /// <summary>
    ///     Gets the path where the XISO was mounted.
    /// </summary>
    public string MountedPath { get; } = "";

    /// <summary>
    ///     Gets a value indicating whether the XISO was successfully mounted.
    /// </summary>
    public bool IsMounted { get; }

    /// <summary>
    ///     Unmounts the XISO drive by terminating the mounting process and waiting for it to exit.
    ///     Unix FUSE mounts are shut down with SIGTERM first (a clean unmount), the kernel mount is
    ///     released with fusermount3/umount, and the mount-point directory is removed afterwards.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!IsMounted || _mountProcess == null) return;

        var processExitedBeforeKill = false;
        try
        {
            if (_isUnixFuseMount && !_mountProcess.HasExited)
            {
                // SIGTERM lets SimpleXisoDrive unmount its FUSE session; SIGKILL would leave the
                // kernel mount behind as a "Transport endpoint is not connected" stub.
                SendUnixSignal("TERM");
                if (await WaitForExitWithTimeoutAsync(TimeSpan.FromSeconds(10)))
                {
                    _logger.Debug(
                        $"[MountXisoDrive.DisposeAsync] SimpleXisoDrive (ID: {_mountProcessId}) exited after SIGTERM (clean unmount).");
                }
                else
                {
                    _logger.Debug(
                        $"[MountXisoDrive.DisposeAsync] SimpleXisoDrive (ID: {_mountProcessId}) did not exit within 10s after SIGTERM.");
                }
            }

            // Attempt to terminate the process without checking HasExited first.
            // This avoids a race condition where the process exits between the check
            // and the Kill() call. We handle the "already exited" case via catch blocks.
            try
            {
                if (!_mountProcess.HasExited)
                {
                    _mountProcess.Kill(true);
                    _logger.Debug(
                        $"[MountXisoDrive.DisposeAsync] Kill signal sent to mounting tool (ID: {_mountProcessId}).");
                }
            }
            catch (InvalidOperationException)
            {
                // Thrown when the process has already exited before Kill() was invoked
                processExitedBeforeKill = true;
                _logger.Debug(
                    $"[MountXisoDrive.DisposeAsync] Mounting tool (ID: {_mountProcessId}) had already exited before Kill could complete (race condition handled).");
            }
            catch (ArgumentException)
            {
                // Thrown when the process is not associated with a valid handle (already exited/disposed)
                processExitedBeforeKill = true;
                _logger.Debug(
                    $"[MountXisoDrive.DisposeAsync] Mounting tool (ID: {_mountProcessId}) had already exited before explicit unmount was needed.");
            }

            // Only wait for exit if we actually sent a kill signal.
            // If the process was already gone, we skip the wait logic.
            if (!processExitedBeforeKill && !_mountProcess.HasExited)
            {
                _logger.Debug(
                    $"[MountXisoDrive.DisposeAsync] Waiting for mounting tool (ID: {_mountProcessId}) to exit (up to 20s).");

                if (await WaitForExitWithTimeoutAsync(TimeSpan.FromSeconds(20)))
                {
                    _logger.Debug(
                        $"[MountXisoDrive.DisposeAsync] Mounting tool (ID: {_mountProcessId}) terminated. Exit code: {_mountProcess.ExitCode}.");
                }
                else
                {
                    _logger.Debug(
                        $"[MountXisoDrive.DisposeAsync] Mounting tool (ID: {_mountProcessId}) did NOT terminate after Kill signal and 20s wait.");
                }
            }
        }
        catch (Exception termEx)
        {
            _logger.Debug(
                $"[MountXisoDrive.DisposeAsync] Exception while terminating mounting tool (ID: {_mountProcessId}): {termEx}");
            _logger.Error(termEx,
                $"Failed to terminate mounting tool (ID: {_mountProcessId}) for unmounting.");
        }
        finally
        {
            _mountProcess.Dispose();
        }

        if (_isUnixFuseMount)
        {
            // A killed FUSE daemon leaves the kernel mount behind; release it explicitly, then
            // remove the now-empty mount-point directory.
            FuseValidation.TryUnmount(_mountPointDirectory, _logger);

            for (var attempt = 0; attempt < 10 && Directory.Exists(_mountPointDirectory); attempt++)
            {
                try
                {
                    Directory.Delete(_mountPointDirectory);
                    _logger.Debug(
                        $"[MountXisoDrive.DisposeAsync] Removed the temporary mount point '{_mountPointDirectory}'.");
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(500);
                }
            }

            if (Directory.Exists(_mountPointDirectory))
            {
                _logger.Debug(
                    $"[MountXisoDrive.DisposeAsync] WARNING: Mount point '{_mountPointDirectory}' still exists after attempting to unmount.");
            }

            GC.SuppressFinalize(this);
            return;
        }

        if (!string.IsNullOrEmpty(MountedPath))
        {
            var driveRoot = Path.GetPathRoot(MountedPath);
            await Task.Delay(1000); // Give OS a moment to release the drive
            if (Directory.Exists(driveRoot))
            {
                _logger.Debug(
                    $"[MountXisoDrive.DisposeAsync] WARNING: {driveRoot} drive still exists after attempting to unmount.");
            }
            else
            {
                _logger.Debug($"[MountXisoDrive.DisposeAsync] {driveRoot} drive successfully unmounted.");
            }
        }

        GC.SuppressFinalize(this);
    }

    private void SendUnixSignal(string signal)
    {
        try
        {
            using var kill = Process.Start(new ProcessStartInfo
            {
                FileName = "kill",
                Arguments = $"-{signal} {_mountProcessId}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            kill?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            _logger.Debug(
                $"[MountXisoDrive.DisposeAsync] Could not send SIG{signal} to {_mountProcessId}: {ex.Message}");
        }
    }

    private async Task<bool> WaitForExitWithTimeoutAsync(TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await _mountProcess!.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
