using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SimpleLauncher.Avalonia.Updater.Services;

/// <summary>
///     Service for detecting whether the Dokan library is installed,
///     and for downloading and installing it if missing.
/// </summary>
internal class DokanService
{
    private const string DokanX64Url =
        "https://github.com/dokan-dev/dokany/releases/download/v2.3.1.1000/Dokan_x64.msi";

    private const string DokanArm64Url =
        "https://github.com/dokan-dev/dokany/releases/download/v2.3.1.1000/Dokan_ARM64.msi";

    private readonly DownloadService _downloadService;

    /// <summary>
    ///     Initializes a new instance of the DokanService class.
    /// </summary>
    /// <param name="downloadService">The download service to use for downloading files.</param>
    public DokanService(DownloadService downloadService)
    {
        _downloadService = downloadService;

        // UPD-15: subscribe once per instance (paired lifetimes — both services are
        // created together per MainWindow). Subscribing inside
        // DownloadAndInstallDokanAsync re-added the handlers on every call,
        // duplicating progress/log output and rooting this instance via the
        // longer-lived DownloadService.
        _downloadService.LogMessage += (_, e) => LogMessage?.Invoke(this, e);
        _downloadService.ProgressChanged += (_, e) => ProgressChanged?.Invoke(this, e);
    }

    /// <summary>
    ///     Event raised when a log message needs to be displayed.
    /// </summary>
    public event EventHandler<EventArgs<string>>? LogMessage;

    /// <summary>
    ///     Event raised when download progress changes.
    /// </summary>
    public event EventHandler<EventArgs<DownloadProgressInfo>>? ProgressChanged;

    /// <summary>
    ///     Checks whether the Dokan library is installed on this system.
    /// </summary>
    /// <returns>True if Dokan is detected, false otherwise.</returns>
    public bool IsDokanInstalled()
    {
        if (!OperatingSystem.IsWindows()) return false; // Dokan is a Windows-only driver

        LogMessage?.Invoke(this, new EventArgs<string>("Checking if Dokan is installed..."));

        // Check 1: Look for Dokan in installed programs (registry)
        if (IsDokanInRegistry())
        {
            LogMessage?.Invoke(this, new EventArgs<string>("Dokan found in installed programs."));
            return true;
        }

        // Check 2: Look for Dokan DLL in System32
        if (IsDokanDllPresent())
        {
            LogMessage?.Invoke(this, new EventArgs<string>("Dokan DLL found in System32."));
            return true;
        }

        LogMessage?.Invoke(this, new EventArgs<string>("Dokan is not installed."));
        return false;
    }

    /// <summary>
    ///     Gets the Dokan MSI download URL for the current processor architecture.
    /// </summary>
    /// <returns>The download URL for the appropriate Dokan MSI installer.</returns>
    private static string GetDokanDownloadUrl()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => DokanArm64Url,
            _ => DokanX64Url
        };
    }

    /// <summary>
    ///     Downloads the Dokan MSI installer and launches it.
    ///     UPD-14: the MSI is staged in the system temp folder, never in AppDirectory
    ///     (often ACL-protected, and the old 10-minute deferred cleanup died with the
    ///     updater so MSIs littered the install dir forever). After the installer exits,
    ///     the MSI is deleted with a short lock-retry; anything left behind sits in temp
    ///     where OS cleanup handles it.
    ///     UPD-19: cancellable — the user token flows into the download and the
    ///     installer wait (a user cancel aborts the wait; the MSI keeps running and
    ///     temp leftovers are reclaimed by OS cleanup).
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the download/install wait.</param>
    public async Task DownloadAndInstallDokanAsync(CancellationToken cancellationToken = default)
    {
        var downloadUrl = GetDokanDownloadUrl();
        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        var msiPath = Path.Combine(Path.GetTempPath(), fileName);

        LogMessage?.Invoke(this, new EventArgs<string>($"Downloading Dokan installer from: {downloadUrl}"));

        try
        {
            // Download the MSI file (UPD-15: event forwarding is wired once in the constructor)
            await using var memoryStream =
                await _downloadService.DownloadToMemoryAsync(downloadUrl, cancellationToken);

            // Temp-drive free-space guard (UPD-06): the MSI must fit where we stage it.
            var tempDrive = new DriveInfo(Path.GetPathRoot(Path.GetTempPath())!);
            if (tempDrive.AvailableFreeSpace < memoryStream.Length + (64L * 1024 * 1024))
            {
                throw new IOException(
                    $"Insufficient disk space to stage the Dokan installer on {tempDrive.Name}.");
            }

            // Save to disk
            LogMessage?.Invoke(this, new EventArgs<string>($"Saving installer to: {msiPath}"));
            await using (var fileStream = File.Create(msiPath))
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            LogMessage?.Invoke(this, new EventArgs<string>("Download complete. Launching Dokan installer..."));

            // Launch the MSI installer (shows UI, handles its own elevation if needed)
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = msiPath,
                UseShellExecute = true
            });

            if (process == null)
            {
                LogMessage?.Invoke(this, new EventArgs<string>("Failed to launch the Dokan installer."));
                DeleteInstallerQuietly(msiPath);
                return;
            }

            LogMessage?.Invoke(this,
                new EventArgs<string>("Dokan installer launched. Please follow the installation wizard."));

            // Wait for the installer to finish (bounded — a forgotten wizard must not hang
            // the updater forever), then remove the staged MSI promptly.
            // UPD-19: linked to the user token — a user cancel aborts the wait and
            // propagates (the installer keeps running); a pure 30-minute timeout
            // just leaves the staged MSI in temp.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            using var waitCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(waitCts.Token);
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogMessage?.Invoke(this,
                    new EventArgs<string>(
                        "Dokan installer is still running — leaving the staged installer in the temp folder."));
                return;
            }

            // The installer may hold a lock briefly after exit — short retry, then give up
            // (temp-folder leftovers are reclaimed by OS disk cleanup).
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (File.Exists(msiPath))
                        File.Delete(msiPath);
                    LogMessage?.Invoke(this, new EventArgs<string>($"Cleaned up Dokan installer: {msiPath}"));
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }

            Log.Warning("Failed to clean up Dokan installer promptly: {MsiPath}", msiPath);
            LogMessage?.Invoke(this,
                new EventArgs<string>(
                    $"Could not remove the staged installer ({msiPath}) — it is in the temp folder and safe to delete."));
        }
        catch (OperationCanceledException)
        {
            // UPD-19: user cancellation is not an error — propagate without a bug report.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            // Offline/transport failures and local disk conditions (e.g. no space to stage
            // the MSI) are expected user errors — Information level, never a bug report.
            Log.Information(ex, "Error downloading or installing Dokan");
            LogMessage?.Invoke(this, new EventArgs<string>($"Error during Dokan installation: {ex.Message}"));
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error downloading or installing Dokan");
            await BugReportService.ReportBugAsync(ex, "Error downloading or installing Dokan");
            LogMessage?.Invoke(this, new EventArgs<string>($"Error during Dokan installation: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Best-effort deletion of the staged installer (never throws).
    /// </summary>
    private static void DeleteInstallerQuietly(string msiPath)
    {
        try
        {
            if (File.Exists(msiPath))
                File.Delete(msiPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clean up Dokan installer: {MsiPath}", msiPath);
        }
    }

    /// <summary>
    ///     Checks the Windows registry for any Dokan installation entry.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool IsDokanInRegistry()
    {
        // Check both native and WOW64 uninstall registry locations
        return CheckUninstallRegistry(RegistryView.Registry64) ||
               CheckUninstallRegistry(RegistryView.Registry32);
    }

    /// <summary>
    ///     Checks a specific registry view for Dokan uninstall entries.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool CheckUninstallRegistry(RegistryView registryView)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
            using var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey == null) return false;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    var displayName = subKey?.GetValue("DisplayName") as string;
                    if (!string.IsNullOrEmpty(displayName) &&
                        displayName.Contains("Dokan", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Skip keys that can't be read
                }
            }
        }
        catch
        {
            // Registry access may fail; treat as not found
        }

        return false;
    }

    /// <summary>
    ///     Checks whether the Dokan DLL exists in the System32 directory.
    /// </summary>
    private static bool IsDokanDllPresent()
    {
        try
        {
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return File.Exists(Path.Combine(system32, "dokan2.dll")) ||
                   File.Exists(Path.Combine(system32, "dokan1.dll"));
        }
        catch
        {
            return false;
        }
    }
}