using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Services.QuitOrReinstall;

/// <summary>
///     Handles reinstalling SimpleLauncher by downloading and launching the Updater.exe.
/// </summary>
public class ReinstallSimpleLauncher
{
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReinstallSimpleLauncher" /> class.
    /// </summary>
    public ReinstallSimpleLauncher(ILogger logErrors, IApplicationLifetime applicationLifetime,
        IDispatcherService dispatcherService, IServiceProvider serviceProvider)
    {
        _logger = logErrors;
        _applicationLifetime = applicationLifetime;
        _dispatcherService = dispatcherService;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Launches Updater.exe (downloading it from GitHub if missing) and shuts down the application.
    /// </summary>
    public async void StartUpdaterAndShutdownAsync()
    {
        try
        {
            var messageBoxLibrary = _serviceProvider.GetRequiredService<IMessageBoxLibraryService>();
            try
            {
                var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");

                if (File.Exists(updaterPath))
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo(updaterPath)
                        {
                            // The second argument tells the single updater which app to wait
                            // for and restart (shared by both apps in the unified bundle).
                            Arguments =
                                $"{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)} SimpleLauncher.exe",
                            UseShellExecute = true,
                            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                        };
                        Process.Start(startInfo);

                        ShutdownApplication();
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5) // Access Denied
                    {
                        // Log the access denied error
                        _logger.Error(ex, "Access denied when starting Updater.exe");

                        // Notify user that update failed
                        await messageBoxLibrary.UpdaterLaunchFailedMessageBoxAsync();
                    }
                }
                else
                {
                    try
                    {
                        var updateChecker = _serviceProvider.GetRequiredService<CheckForUpdatesService>();

                        // 1. Get the URL from GitHub
                        var (updaterZipUrl, _) = await updateChecker.GetLatestUpdaterInfoAsync();

                        if (string.IsNullOrEmpty(updaterZipUrl))
                        {
                            await messageBoxLibrary.CouldNotFindUpdaterOnGitHubMessageBoxAsync();
                            return;
                        }

                        // 2. Download the updater file into memory
                        await using var memoryStream = new MemoryStream();
                        await updateChecker.DownloadUpdateFileToMemoryAsync(updaterZipUrl, memoryStream);

                        // 3. Extract the contents to the application directory
                        var extractionSuccess = CheckForUpdatesService.ExtractAllFromZip(memoryStream,
                            AppDomain.CurrentDomain.BaseDirectory, null, _logger);

                        if (!extractionSuccess)
                        {
                            // Notify user
                            await messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();

                            return;
                        }

                        // 4. Verify Updater.exe now exists and launches it
                        if (File.Exists(updaterPath))
                        {
                            try
                            {
                                var startInfo = new ProcessStartInfo(updaterPath)
                                {
                                    Arguments = $"{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)} SimpleLauncher.exe",
                                    UseShellExecute = true,
                                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                                };
                                Process.Start(startInfo);

                                ShutdownApplication();
                            }
                            catch (Win32Exception ex) when (ex.NativeErrorCode == 5) // Access Denied
                            {
                                // Log the access denied error
                                _logger.Error(ex, "Access denied when starting Updater.exe after download");

                                // Notify user that update failed
                                await messageBoxLibrary.UpdaterLaunchFailedMessageBoxAsync();
                            }
                        }
                        else
                        {
                            // Notify user
                            await messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Notify developer
                        _logger.Error(ex, "Failed to download and reinstall the updater");

                        // Notify user
                        await messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex, "Failed to reinstall SimpleLauncher");

                // Notify user
                await messageBoxLibrary.InstallUpdateManuallyMessageBoxAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in the method StartUpdaterAndShutdownAsync");
        }
    }

    private void ShutdownApplication()
    {
        // Use Dispatcher to ensure shutdown happens on the UI thread.
        _dispatcherService.Invoke(() =>
        {
            _applicationLifetime.Shutdown();
            Process.GetCurrentProcess().Kill();
            Environment.Exit(0);
        });
    }
}