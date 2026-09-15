using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Avalonia.Services.QuitOrReinstall;

/// <summary>
///     Handles application shutdown and restart for the Avalonia port.
///     Port of the WPF QuitSimpleLauncher — Exit_Click routes here for a clean
///     shutdown instead of merely closing the window.
/// </summary>
public class AvaloniaQuitSimpleLauncher
{
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AvaloniaQuitSimpleLauncher" /> class.
    /// </summary>
    public AvaloniaQuitSimpleLauncher(ILogger logErrors, IApplicationLifetime applicationLifetime)
    {
        _logger = logErrors;
        _applicationLifetime = applicationLifetime;
    }

    /// <summary>
    ///     Restarts the application by launching a new process and shutting down the current one.
    /// </summary>
    public async Task RestartApplicationAsync(IMessageBoxLibraryService messageBox)
    {
        var processPath = Environment.ProcessPath;
        if (processPath is null) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
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
    ///     Shuts down the application immediately (clean shutdown path used by Exit).
    /// </summary>
    public void SimpleQuitApplication()
    {
        _applicationLifetime.Shutdown();
    }

    /// <summary>
    ///     Launches a fresh updater and forcefully shuts down the application for an update.
    /// </summary>
    public async Task ShutdownForUpdateAsync(string updaterPath, IMessageBoxLibraryService messageBox)
    {
        var appDirectory = Path.GetDirectoryName(updaterPath) ?? AppDomain.CurrentDomain.BaseDirectory;

        if (!File.Exists(updaterPath))
        {
            await messageBox.UpdaterLaunchFailedMessageBoxAsync();
            return;
        }

        // Launch Updater.exe and shut down
        try
        {
            var startInfo = new ProcessStartInfo(updaterPath)
            {
                Arguments =
                    $"{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)} {AvaloniaCheckForUpdatesService.AppExecutableName}",
                UseShellExecute = true,
                WorkingDirectory = appDirectory
            };
            Process.Start(startInfo);

            _applicationLifetime.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start updater and shut down");

            await messageBox.UpdaterLaunchFailedMessageBoxAsync();
        }
    }
}