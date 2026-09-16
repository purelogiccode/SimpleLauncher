using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Services.QuitOrReinstall;

/// <summary>
///     Handles application shutdown, restart, and update-related exit operations.
/// </summary>
public class QuitSimpleLauncher
{
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuitSimpleLauncher" /> class.
    /// </summary>
    public QuitSimpleLauncher(ILogger logErrors, IApplicationLifetime applicationLifetime,
        IDispatcherService dispatcherService, IServiceProvider serviceProvider)
    {
        _logger = logErrors;
        _applicationLifetime = applicationLifetime;
        _dispatcherService = dispatcherService;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Restarts the application by launching a new process and shutting down the current one.
    /// </summary>
    public async Task RestartApplicationAsync(IMessageBoxLibraryService messageBox)
    {
        var processModule = Process.GetCurrentProcess().MainModule;
        if (processModule == null) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = processModule.FileName,
            Arguments = "--restarting",
            UseShellExecute = true,
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex))
        {
            // Expected user-environment condition (e.g., canceled UAC/security prompt): not a bug.
            _logger.Information(ex, "Application restart was canceled by the user");

            // Notify user and don't shut down the current instance if the new one couldn't start
            await messageBox.FailedToRestartMessageBoxAsync();
            return;
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Failed to start new process during application restart");

            // Notify user
            await messageBox.FailedToRestartMessageBoxAsync();

            // Don't shut down the current instance if the new one couldn't start
            return;
        }


        // Shutdown the current instance
        _applicationLifetime.Shutdown();
    }

    /// <summary>
    ///     Shuts down the application immediately.
    /// </summary>
    public void SimpleQuitApplication()
    {
        _applicationLifetime.Shutdown();
    }

    /// <summary>
    ///     Downloads a fresh Updater.exe, launches it, and forcefully shuts down the application for an update.
    /// </summary>
    public async Task ShutdownForUpdateAsync(string updaterPath, IMessageBoxLibraryService messageBox)
    {
        var appDirectory = Path.GetDirectoryName(updaterPath) ?? AppDomain.CurrentDomain.BaseDirectory;

        // 1. Try to download a fresh Updater.exe from GitHub (overwrites any existing copy)
        var downloaded = false;
        try
        {
            var updateChecker = _serviceProvider.GetRequiredService<CheckForUpdatesService>();
            var (updaterZipUrl, _) = await updateChecker.GetLatestUpdaterInfoAsync();

            if (!string.IsNullOrEmpty(updaterZipUrl))
            {
                await using var memoryStream = new MemoryStream();
                await updateChecker.DownloadUpdateFileToMemoryAsync(updaterZipUrl, memoryStream);
                var extractionSucceeded =
                    CheckForUpdatesService.ExtractAllFromZip(memoryStream, appDirectory, null, _logger);

                // A corrupt archive can leave a truncated Updater.exe behind; never trust
                // File.Exists alone after a failed extraction or the broken binary is
                // launched (the sibling ReinstallSimpleLauncher checks the same result).
                if (!extractionSucceeded)
                {
                    await messageBox.UpdaterLaunchFailedMessageBoxAsync();
                    return;
                }

                if (File.Exists(updaterPath)) downloaded = true;
            }
        }
        catch
        {
            // GitHub unreachable or download failed — will fall back to local copy
        }

        // 2. If neither downloaded nor local file exists, notify and return
        if (!downloaded && !File.Exists(updaterPath))
        {
            await messageBox.UpdaterLaunchFailedMessageBoxAsync();
            return;
        }

        // 3. Launch Updater.exe and shut down
        try
        {
            var startInfo = new ProcessStartInfo(updaterPath)
            {
                // The second argument tells the single updater which app to wait for and
                // restart (it is shared by the WPF and Avalonia apps shipped side by side).
                Arguments =
                    $"{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)} SimpleLauncher.exe",
                UseShellExecute = true,
                WorkingDirectory = appDirectory
            };
            Process.Start(startInfo);

            await _dispatcherService.InvokeAsync(() =>
            {
                _applicationLifetime.Shutdown();
                Process.GetCurrentProcess().Kill();
                Environment.Exit(0);
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5) // Access Denied
        {
            _logger.Error(ex, "Access denied when starting Updater.exe");

            await messageBox.UpdaterLaunchFailedMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start updater and shut down");

            await messageBox.UpdaterLaunchFailedMessageBoxAsync();
        }
    }
}