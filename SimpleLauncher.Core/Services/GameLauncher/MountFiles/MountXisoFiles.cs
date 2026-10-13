using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.ExternalToolLauncher;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Mounts original Xbox ISO (XISO) images using the bundled SimpleXisoDrive tool: Dokan on
///     Windows, FUSE 3 on Linux. SimpleXisoDrive can also mount Xbox ISO images stored as CHD
///     (on-demand hunk decompression) and compressed CISO/ZArchive images.
/// </summary>
public class MountXisoFiles : IMountXisoFiles
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MountXisoFiles" /> class.
    /// </summary>
    public MountXisoFiles(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Mounts an XISO file and returns a disposable drive handle with the mounted default.xbe path.
    /// </summary>
    public Task<MountXisoDrive> MountAsync(string resolvedIsoFilePath, ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        return MountXisoFileAsync(resolvedIsoFilePath, "default.xbe", string.Empty, logErrors, messageBox);
    }

    /// <summary>
    ///     Mounts an Xbox image (.cso/.zar) with SimpleXisoDrive's virtual image.iso option and
    ///     returns a disposable drive handle with the mounted image.iso path.
    /// </summary>
    public Task<MountXisoDrive> MountImageIsoAsync(string resolvedFilePath, ILogger logErrors,
        IMessageBoxLibraryService messageBox)
    {
        return MountXisoFileAsync(resolvedFilePath, "image.iso", " --image-iso", logErrors, messageBox);
    }

    private async Task<MountXisoDrive> MountXisoFileAsync(string resolvedIsoFilePath, string expectedFileName,
        string extraArguments, ILogger logErrors, IMessageBoxLibraryService messageBox)
    {
        _logger.Debug($"[MountXisoFiles.MountAsync] Starting to mount ISO: {resolvedIsoFilePath}");

        // Windows mounts through Dokan; Linux through FUSE 3 with the bundled SimpleXisoDrive
        // build. macOS has no payload and reports the platform as unsupported (LB-14).
        if (!OperatingSystem.IsWindows())
        {
            if (OperatingSystem.IsLinux() && FuseValidation.IsFuseAvailable())
            {
                return await MountXisoFileWithFuseAsync(resolvedIsoFilePath, expectedFileName, extraArguments,
                    logErrors);
            }

            const string unsupportedMessage = "Mounting XISO is not supported on this platform.";
            _logger.Debug($"[MountXisoFiles.MountAsync] {unsupportedMessage}");
            logErrors.Information(unsupportedMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return new MountXisoDrive(logErrors, _logger);
        }

        if (!DokanValidation.IsDokanInstalled())
        {
            const string errorMessage = "Dokan driver not found. Cannot mount ISO.";
            _logger.Debug($"[MountXisoFiles.MountAsync] Error: {errorMessage}");
            logErrors.Information(errorMessage);
            await messageBox.DokanDriverNotInstalledMessageBoxAsync();
            return new MountXisoDrive(logErrors, _logger);
        }

        var toolRelativePath = GetToolPath();
        if (toolRelativePath == null)
        {
            var unsupportedMessage = "No SimpleXisoDrive payload exists for this platform/architecture.";
            _logger.Debug($"[MountXisoFiles.MountAsync] {unsupportedMessage}");
            logErrors.Information(unsupportedMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return new MountXisoDrive(logErrors, _logger);
        }

        var resolvedToolPath = PathHelper.ResolveRelativeToAppDirectory(toolRelativePath);

        _logger.Debug($"[MountXisoFiles.MountAsync] Path to tool: {resolvedToolPath}");

        if (string.IsNullOrWhiteSpace(resolvedToolPath) || !File.Exists(resolvedToolPath))
        {
            var errorMessage = $"{Path.GetFileName(toolRelativePath)} not found. Cannot mount ISO.";
            _logger.Debug($"[MountXisoFiles.MountAsync] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return new MountXisoDrive(logErrors, _logger);
        }

        var driveLetter = GetAvailableDriveLetter(logErrors);
        if (driveLetter == null)
        {
            const string errorMessage = "No available drive letters found to mount the ISO.";
            _logger.Debug($"[MountXisoFiles.MountAsync] Error: {errorMessage}");
            logErrors.Warning(errorMessage);
            await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
            return new MountXisoDrive(logErrors, _logger);
        }

        var driveLetterOnly = $"{driveLetter.Value}:";
        var expectedPath = $"{driveLetter.Value}:\\{expectedFileName}";
        var driveRoot = $"{driveLetter.Value}:\\";

        var psiMount = new ProcessStartInfo
        {
            FileName = resolvedToolPath,
            Arguments = $"\"{resolvedIsoFilePath}\" \"{driveLetterOnly}\"{extraArguments}",
            WindowStyle = ProcessWindowStyle.Normal,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = Path.GetDirectoryName(resolvedToolPath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        _logger.Debug($"[MountXisoFiles.MountAsync] Attempting to mount on drive {driveLetter.Value}:");
        _logger.Debug($"[MountXisoFiles.MountAsync] Arguments: {psiMount.Arguments}");

        var mountProcess = new Process { StartInfo = psiMount, EnableRaisingEvents = true };
        var toolName = Path.GetFileName(toolRelativePath);

        try
        {
            if (!mountProcess.Start()) throw new InvalidOperationException($"Failed to start the {toolName} process.");

            _logger.Debug($"[MountXisoFiles.MountAsync] {toolName} process started (ID: {mountProcess.Id}).");

            var mountSuccessful = await WaitForDriveMountAsync(expectedPath, driveRoot, mountProcess, toolName,
                mountProcess.Id, logErrors);

            if (!mountSuccessful)
            {
                if (!mountProcess.HasExited) mountProcess.Kill(true);

                mountProcess.Dispose();
                await messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
                return new MountXisoDrive(logErrors, _logger);
            }

            _logger.Debug($"[MountXisoFiles.MountAsync] ISO mounted successfully. Path: {expectedPath}");
            return new MountXisoDrive(mountProcess, expectedPath, logErrors, _logger);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountXisoFiles.MountAsync] Exception during mounting: {ex}");
            var contextMessage = $"Error during ISO mount process for {resolvedIsoFilePath}.\nException: {ex.Message}";
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
            return new MountXisoDrive(logErrors, _logger);
        }
    }

    private static string? GetToolPath()
    {
        var runtimeIdentifier = ExternalToolLauncherService.GetPlatformRuntimeIdentifier();
        if (runtimeIdentifier == null) return null;

        return Path.Combine("tools", "SimpleXisoDrive", runtimeIdentifier,
            ExternalToolLauncherService.GetPlatformExecutableName("SimpleXisoDrive"));
    }

    /// <summary>
    ///     Linux FUSE path: mounts the image through the bundled SimpleXisoDrive build into a
    ///     launcher-owned mount-point directory and returns the same disposable drive handle the
    ///     Windows Dokan path returns. Failures are expected conditions on Linux (no Xbox image,
    ///     no FUSE support in the tool) and are logged at Information level without dialogs: the
    ///     strategies that call this either fall back (CHD) or report the failure themselves.
    ///     The mount point is released by <see cref="MountXisoDrive.DisposeAsync" /> when the
    ///     emulator exits.
    /// </summary>
    private async Task<MountXisoDrive> MountXisoFileWithFuseAsync(string resolvedIsoFilePath,
        string expectedFileName, string extraArguments, ILogger logErrors)
    {
        var toolRelativePath = GetToolPath();
        if (toolRelativePath == null)
        {
            logErrors.Information("No SimpleXisoDrive payload exists for this platform/architecture.");
            return new MountXisoDrive(logErrors, _logger);
        }

        var resolvedToolPath = PathHelper.ResolveRelativeToAppDirectory(toolRelativePath);
        if (string.IsNullOrWhiteSpace(resolvedToolPath) || !File.Exists(resolvedToolPath))
        {
            logErrors.Information($"SimpleXisoDrive not found at {toolRelativePath}. Cannot mount ISO.");
            return new MountXisoDrive(logErrors, _logger);
        }

        var mountPoint = FuseMountHelper.CreateMountPoint("xiso-mount-");

        var (mountProcess, started) = FuseMountHelper.StartFuseMountTool(resolvedToolPath,
            $"\"{resolvedIsoFilePath}\" \"{mountPoint}\"{extraArguments}", "SimpleXisoDrive", logErrors,
            out var errorOutput);

        if (!started || mountProcess == null)
        {
            await FuseMountHelper.ReleaseAsync(null, mountPoint, _logger);
            return new MountXisoDrive(logErrors, _logger);
        }

        try
        {
            if (!await FuseMountHelper.WaitForFuseMountReadyAsync(mountPoint, expectedFileName, mountProcess,
                    "SimpleXisoDrive", Path.GetFileName(resolvedIsoFilePath), logErrors, errorOutput))
            {
                await FuseMountHelper.ReleaseAsync(mountProcess, mountPoint, _logger);
                return new MountXisoDrive(logErrors, _logger);
            }

            var mountedPath = Path.Combine(mountPoint, expectedFileName);
            _logger.Debug($"[MountXisoFiles.MountXisoFileWithFuseAsync] ISO mounted successfully. Path: {mountedPath}");
            return new MountXisoDrive(mountProcess, mountedPath, logErrors, _logger);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountXisoFiles.MountXisoFileWithFuseAsync] Exception during mounting: {ex}");
            logErrors.Information($"Error during ISO mount process for {resolvedIsoFilePath}: {ex.Message}");
            await FuseMountHelper.ReleaseAsync(mountProcess, mountPoint, _logger);
            return new MountXisoDrive(logErrors, _logger);
        }
    }

    private char? GetAvailableDriveLetter(ILogger logErrors)
    {
        try
        {
            var existingDrives = Environment.GetLogicalDrives()
                .Select(static d => char.ToUpper(d[0], CultureInfo.InvariantCulture))
                .ToHashSet();

            for (var letter = 'Z'; letter >= 'D'; letter--)
            {
                if (!existingDrives.Contains(letter))
                    return letter;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[MountXisoFiles.GetAvailableDriveLetter] Error enumerating drives: {ex.Message}");
            logErrors.Error(ex, "Error enumerating available drive letters");
            return null;
        }
    }

    private async Task<bool> WaitForDriveMountAsync(string defaultXbePath, string driveRoot, Process mountProcess,
        string toolName, int processId, ILogger logErrors)
    {
        const int maxRetries = 240;
        const int pollIntervalMs = 500;
        var retryCount = 0;

        _logger.Debug(
            $"[MountXisoFiles.WaitForDriveMountAsync] Polling for '{defaultXbePath}' to appear (max {maxRetries * pollIntervalMs / 1000}s)...");

        while (retryCount < maxRetries)
        {
            if (File.Exists(defaultXbePath))
            {
                _logger.Debug(
                    $"[MountXisoFiles.WaitForDriveMountAsync] Found '{defaultXbePath}' after {retryCount * pollIntervalMs / 1000.0:F1} seconds. Mount successful!");
                return true;
            }

            if (Directory.Exists(driveRoot))
            {
                _logger.Debug(
                    $"[MountXisoFiles.WaitForDriveMountAsync] {driveRoot} drive exists after {retryCount * pollIntervalMs / 1000.0:F1} seconds, but '{defaultXbePath}' not found. Continuing to poll...");
            }

            if (mountProcess.HasExited)
            {
                _logger.Debug(
                    $"[MountXisoFiles.WaitForDriveMountAsync] Mount process {toolName} (ID: {processId}) exited prematurely during polling. Exit Code: {mountProcess.ExitCode}.");
                var contextMessage =
                    $"Failed to mount ISO. The mounting tool '{toolName}' exited prematurely with code {mountProcess.ExitCode}.";
                logErrors.Warning(contextMessage);
                return false;
            }

            retryCount++;
            await Task.Delay(pollIntervalMs);
        }

        _logger.Debug(
            $"[MountXisoFiles.WaitForDriveMountAsync] Timed out waiting for '{defaultXbePath}' after {maxRetries * pollIntervalMs / 1000} seconds.");
        var timeoutContextMessage = $"Timed out waiting for the ISO to mount to '{driveRoot}'.";
        logErrors.Warning(timeoutContextMessage);
        return false;
    }
}