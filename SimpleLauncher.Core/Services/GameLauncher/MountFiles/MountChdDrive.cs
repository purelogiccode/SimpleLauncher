using System.Diagnostics;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Represents a temporarily mounted CHD drive.
///     Disposing this object will unmount the drive by terminating the CHDMounter process.
/// </summary>
public class MountChdDrive : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Process? _mountProcess;
    private readonly int _mountProcessId;

    /// <summary>
    ///     Constructor for a successful mount.
    /// </summary>
    public MountChdDrive(Process mountProcess, string mountedPath, string mountedDriveLetter, ILogger logErrors,
        ILogger logger)
    {
        _mountProcess = mountProcess;
        _mountProcessId = mountProcess?.Id ?? -1;
        _logger = logErrors;
        _logger = logger;
        MountedPath = mountedPath;
        MountedDriveLetter = mountedDriveLetter;
        IsMounted = !string.IsNullOrEmpty(mountedPath) && _mountProcess != null;
    }

    /// <summary>
    ///     Constructor for a failed mount.
    /// </summary>
    public MountChdDrive(ILogger logErrors, ILogger logger)
    {
        _logger = logErrors;
        _logger = logger;
        IsMounted = false;
    }

    /// <summary>
    ///     Gets the path where the CHD was mounted.
    /// </summary>
    public string MountedPath { get; } = "";

    /// <summary>
    ///     Gets the drive letter assigned to the mounted CHD.
    /// </summary>
    public string MountedDriveLetter { get; } = "";

    /// <summary>
    ///     Gets a value indicating whether the CHD was successfully mounted.
    /// </summary>
    public bool IsMounted { get; }

    /// <summary>
    ///     Unmounts the CHD drive by terminating the CHDMounter process and waiting for it to exit.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!IsMounted || _mountProcess == null) return;

        var processExitedBeforeKill = false;
        try
        {
            try
            {
                _mountProcess.Kill(true);
                _logger.Debug($"[MountChdDrive.DisposeAsync] Kill signal sent to CHDMounter (ID: {_mountProcessId}).");
            }
            catch (InvalidOperationException)
            {
                processExitedBeforeKill = true;
                _logger.Debug(
                    $"[MountChdDrive.DisposeAsync] CHDMounter (ID: {_mountProcessId}) had already exited before Kill could complete.");
            }
            catch (ArgumentException)
            {
                processExitedBeforeKill = true;
                _logger.Debug(
                    $"[MountChdDrive.DisposeAsync] CHDMounter (ID: {_mountProcessId}) had already exited before explicit unmount was needed.");
            }

            if (!processExitedBeforeKill)
            {
                _logger.Debug(
                    $"[MountChdDrive.DisposeAsync] Waiting for CHDMounter (ID: {_mountProcessId}) to exit (up to 20s).");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try
                {
                    await _mountProcess.WaitForExitAsync(cts.Token);
                }
                catch (TaskCanceledException)
                {
                    _logger.Debug(
                        $"[MountChdDrive.DisposeAsync] Timeout (10s) waiting for CHDMounter (ID: {_mountProcessId}) to exit after Kill.");
                }

                if (_mountProcess.HasExited)
                {
                    _logger.Debug(
                        $"[MountChdDrive.DisposeAsync] CHDMounter (ID: {_mountProcessId}) terminated. Exit code: {_mountProcess.ExitCode}.");
                }
                else
                {
                    _logger.Debug(
                        $"[MountChdDrive.DisposeAsync] CHDMounter (ID: {_mountProcessId}) did NOT terminate after Kill signal and 10s wait.");
                }
            }
        }
        catch (Exception termEx)
        {
            _logger.Debug(
                $"[MountChdDrive.DisposeAsync] Exception while terminating CHDMounter (ID: {_mountProcessId}): {termEx}");
            _logger.Error(termEx, $"Failed to terminate CHDMounter (ID: {_mountProcessId}) for unmounting.");
        }
        finally
        {
            _mountProcess.Dispose();
        }

        if (!string.IsNullOrEmpty(MountedDriveLetter))
        {
            var driveRoot = $"{MountedDriveLetter}:\\";
            await Task.Delay(1000);
            if (Directory.Exists(driveRoot))
            {
                _logger.Debug(
                    $"[MountChdDrive.DisposeAsync] WARNING: Drive {driveRoot} still exists after attempting to unmount.");
            }
            else
            {
                _logger.Debug($"[MountChdDrive.DisposeAsync] Drive {driveRoot} successfully unmounted.");
            }
        }

        // Unix (FUSE) mounts use a mount-point directory the launcher created: once the process
        // exit tears the FUSE session down, the now-empty directory is removed. Retry briefly
        // while the teardown completes.
        if (string.IsNullOrEmpty(MountedDriveLetter) && !string.IsNullOrEmpty(MountedPath))
        {
            for (var attempt = 0; attempt < 10 && Directory.Exists(MountedPath); attempt++)
            {
                try
                {
                    Directory.Delete(MountedPath);
                    _logger.Debug(
                        $"[MountChdDrive.DisposeAsync] Removed the temporary mount point '{MountedPath}'.");
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(500);
                }
            }

            if (Directory.Exists(MountedPath))
            {
                _logger.Debug(
                    $"[MountChdDrive.DisposeAsync] WARNING: Mount point '{MountedPath}' still exists after attempting to unmount.");
            }
        }

        GC.SuppressFinalize(this);
    }
}