using System.Net.Security;
using System.Security.Authentication;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Serilog.Events;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Avalonia.Interfaces;
// ReSharper disable once RedundantUsingDirective
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services;
#if WINDOWS
using WindowScreenshot = SimpleLauncher.Avalonia.Services.TakeScreenshot.WindowScreenshot;
#endif
using SimpleLauncher.Avalonia.Services.AvaloniaServices;
using SimpleLauncher.Avalonia.Services.ContextMenus;
using SimpleLauncher.Avalonia.Services.DisplaySystemInfo;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.GameFilter;
using SimpleLauncher.Avalonia.Services.GameLauncher;
using SimpleLauncher.Avalonia.Services.GameLauncher.Handlers;
using SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;
using SimpleLauncher.Avalonia.Services.GameScan;
using SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;
using SimpleLauncher.Avalonia.Services.LoadingOverlay;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.QuitOrReinstall;
using SimpleLauncher.Avalonia.Services.RetroAchievements;
using SimpleLauncher.Avalonia.Services.SearchOrchestrator;
using SimpleLauncher.Avalonia.Services.SettingsDatabase;
using SimpleLauncher.Avalonia.Services.SystemImageResolver;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Avalonia.Services.SystemSelectionOrchestrator;
using SimpleLauncher.Avalonia.Services.Theme;
using SimpleLauncher.Avalonia.Services.TrayIcon;
using SimpleLauncher.Avalonia.Services.UIReset;
using SimpleLauncher.Avalonia.Services.UpdateStatusBar;
using SimpleLauncher.Avalonia.Services.UsageStats;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckForFileLock;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using SimpleLauncher.Core.Services.Converters;
using SimpleLauncher.Core.Services.DebugAndBugReport;
using SimpleLauncher.Core.Services.DownloadService;
using SimpleLauncher.Core.Services.EasyMode;
using SimpleLauncher.Core.Services.ExternalToolLauncher;
using SimpleLauncher.Core.Services.ExtractFiles;
using SimpleLauncher.Core.Services.FindCoverImage;
using SimpleLauncher.Core.Services.GameFileWatcher;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using SimpleLauncher.Core.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.GetListOfFiles;
using SimpleLauncher.Core.Services.MameData;
using SimpleLauncher.Core.Services.ParameterResolver;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Core.Services.SanitizeInputString;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.SystemConfiguration;
using SimpleLauncher.Core.Services.UsageStats;
using SimpleLauncher.Core.Services.WpfServices;
using IApplicationLifetime = SimpleLauncher.Core.Interfaces.IApplicationLifetime;
using IResourceProvider = SimpleLauncher.Core.Interfaces.IResourceProvider;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
// ReSharper disable once RedundantUsingDirective
using SimpleLauncher.Avalonia.Views;
#if WINDOWS
using SimpleLauncher.Avalonia.Services.TakeScreenshot;
#endif

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Application entry point handling DI container setup, single-instance enforcement, and global error handling.
/// </summary>
public class App : Application, IDisposable
{
    private EventWaitHandle? _instanceSignal;
    private bool _isFirstInstance;

    private Mutex? _singleInstanceMutex;

    /// <summary>
    ///     Gets the application's dependency injection service provider.
    /// </summary>
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    #region IDisposable

    /// <summary>
    ///     Cleans up the single-instance mutex and event handle, and disposes the tray icon.
    /// </summary>
    public void Dispose()
    {
        // Unsubscribe global exception handlers
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        Dispatcher.UIThread.UnhandledException -= App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

        try
        {
            ServiceProvider?.GetService<AvaloniaTrayIconManager>()?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error disposing the tray icon manager");
        }

        // Cancel any running RetroAchievements hash scan and wait (bounded) so no
        // scan threads or CLI hashing processes are orphaned by the shutdown.
        try
        {
            ServiceProvider?.GetService<IRetroAchievementsHashScanner>()
                ?.CancelScanAndWaitAsync(TimeSpan.FromSeconds(10))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error canceling the RetroAchievements hash scan on shutdown");
        }

        try
        {
            var gamePadController = ServiceProvider?.GetService<GamePadController>();
            if (gamePadController is not null)
            {
                // Observe faults but don't block: Dispose would destroy the controller
                // while an awaited StopAsync is still in flight (AV-11).
                _ = gamePadController.StopAsync().ContinueWith(static t =>
                {
                    if (t.IsFaulted) Log.Debug(t.Exception, "Failed to stop the gamepad controller on shutdown");
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                gamePadController.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error disposing the gamepad controller");
        }

        try
        {
            ServiceProvider?.GetService<AvaloniaGameFileWatcherService>()?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error disposing the game file watcher service");
        }

        _singleInstanceMutex?.Dispose();
        _instanceSignal?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion

    /// <summary>
    ///     Handles application startup including DI registration, single-instance check, and theme initialization.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        // The XAML Previewer (Avalonia.Designer.HostApp) runs this App class too.
        // It must never acquire the single-instance mutex or start DI/logging,
        // otherwise real launches silently exit as "second instance".
        if (Design.IsDesignMode) return;

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        Dispatcher.UIThread.UnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // Parse command-line arguments up front (WPF parity).
        // -debug    → open the debug window alongside the main window (WPF isDebugMode).
        // -whatsnew → show the update-history window; the updater restarts the app with
        //             this flag after a successful update (WPF displayHistoryWindow).
        var desktopLifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var startupArgs = desktopLifetime?.Args ?? [];
        var isDebugMode = startupArgs.Any(static arg => arg.Equals("-debug", StringComparison.OrdinalIgnoreCase));
        var displayHistoryWindow =
            startupArgs.Any(static arg => arg.Equals("-whatsnew", StringComparison.OrdinalIgnoreCase));

        // Single-instance enforcement. The mutex name is shared with the WPF app, so only
        // one SimpleLauncher (WPF or Avalonia) can run at a time; a second launch signals
        // the running instance to come to the foreground and exits. --restarting skips the
        // check (WPF parity) so the app can replace itself while the previous process is
        // still shutting down.
        var isRestarting =
            startupArgs.Any(static arg => arg.Equals("--restarting", StringComparison.OrdinalIgnoreCase));
        if (!isRestarting)
        {
            try
            {
                _singleInstanceMutex = new Mutex(true, SingleInstance.MutexName, out _isFirstInstance);
            }
            catch (AbandonedMutexException)
            {
                // The mutex was abandoned by a previous instance (e.g., due to a crash).
                // This means we successfully acquired it, and we are now the first instance.
                // The 'out _isFirstInstance' parameter would already be true in this case,
                // but we explicitly set it for clarity and to ensure the flow continues as a first instance.
                _isFirstInstance = true;
                Log.Debug(
                    "Mutex was abandoned by a previous instance, but successfully acquired by this instance. Proceeding as first instance");
            }

            // Named EventWaitHandle is Windows-only; on Linux the named Mutex still enforces
            // single-instance, only the "bring first instance to foreground" signal is lost.
            if (OperatingSystem.IsWindows())
                _instanceSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstance.EventName);

            if (!_isFirstInstance)
            {
                // Signal the first instance to come to foreground
                try
                {
                    _instanceSignal?.Set();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to signal first instance");
                }

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime nonFirstInstanceLifetime)
                {
                    // Do NOT call Shutdown() synchronously here: the dispatcher main loop has not
                    // started yet, and DoShutdown() -> Dispatcher.UIThread.InvokeShutdown() leaves
                    // the dispatcher permanently shut down, so StartCore() then throws
                    // "Cannot perform requested operation because the Dispatcher shut down" when it
                    // calls Dispatcher.UIThread.MainLoop(...). Post the shutdown instead so it runs
                    // once the main loop is pumping (same clean-exit path as closing the main window).
                    Dispatcher.UIThread.Post(() => nonFirstInstanceLifetime.Shutdown());
                }

                return;
            }
        }

        // Configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true);

        var configuration = builder.Build();

        // Decrypt the API key once at launch so it is available to every service at runtime.
        AppConstants.InitializeApiKey(configuration["ApiKey"]);

        // Serilog setup
        var appDataLogFolder = AppDataPaths.SimpleLauncherDataFolder;
        Directory.CreateDirectory(appDataLogFolder);

        // Sink that forwards Warning+ events to the bug report API
        var bugReportSink = new BugReportApiSink();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: "[{Level}] {Timestamp:HH:mm:ss.fff} - {Message}{NewLine}{Exception}")
            .WriteTo.Async(a => a.File(
                PathHelper.ResolveLogFilePath(configuration),
                LogEventLevel.Warning,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
            .WriteTo.Sink(new DebugWindowSink())
            .WriteTo.Sink(bugReportSink)
            .CreateLogger();
        Log.Information("SimpleLauncher.Avalonia starting up");

        // DI container
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection, configuration, bugReportSink);

        ServiceProvider = serviceCollection.BuildServiceProvider();

        // One-time migration from the legacy files (favorites.dat, playhistory.dat,
        // settings.xml, system.xml) into the unified SQLite database (settings.dat in
        // AppData). Idempotent: does nothing when settings.dat is already valid. On
        // success legacy files are shelved as .bak; on failure they are left untouched
        // and the managers fall back to reading them.
        try
        {
            AvaloniaLegacyMigrator.EnsureMigrated(
                ServiceProvider.GetRequiredService<IConfiguration>(),
                ServiceProvider.GetRequiredService<ILogger>(),
                ServiceProvider.GetRequiredService<ICredentialProtector>());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Legacy settings migration failed; continuing with legacy files");
        }

        // Apply the saved language (from settings.xml) once at startup, BEFORE any UI is
        // constructed. Resolving SettingsManagerService here also forces the settings ->
        // message-box-library -> localization chain to build once, in a safe order.
        var localizationService = ServiceProvider.GetRequiredService<LocalizationService>();
        var savedLanguage = ServiceProvider.GetRequiredService<SettingsManagerService>().Language;
        if (!string.IsNullOrEmpty(savedLanguage) &&
            !string.Equals(savedLanguage, "en", StringComparison.OrdinalIgnoreCase))
        {
            localizationService.LoadLanguage(savedLanguage);
        }


        // Initialize the bug report sink with DI services (queues Warning+ events to the API)
        bugReportSink.Initialize(
            ServiceProvider.GetRequiredService<IHttpClientFactory>(),
            ServiceProvider.GetRequiredService<IConfiguration>(),
            ServiceProvider.GetRequiredService<IDeleteFilesService>(),
            appDataLogFolder);

        // Delete temp folders and unneeded files in the background.
        // Resolve the service up front so the fire-and-forget task never reaches into
        // App.ServiceProvider later (it may be disposed once the application shuts down).
        try
        {
            var cleanupService = ServiceProvider.GetRequiredService<ICleanSimpleLauncherFolderService>();
            _ = Task.Run(() =>
            {
                try
                {
                    cleanupService.CleanupTrash();
                    cleanupService.CleanupTempFiles();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to cleanup trash in SimpleLauncher folder");
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to resolve the background folder cleanup service");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.ShutdownMode = ShutdownMode.OnMainWindowClose;
            lifetime.Exit += (_, _) => Dispose();

            // Start listening for second-instance signals
            _ = ListenForSecondInstanceAsync(lifetime);

            // Show main window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            lifetime.MainWindow = mainWindow;
            mainWindow.Show();

            // WPF parity: open the debug window when launched with -debug
            if (isDebugMode) DebugWindow.ShowDebugWindow();

            // F8 global hotkey → active window screenshot (Windows-only)
#if WINDOWS
            try
            {
                var hotkeyService = ServiceProvider.GetRequiredService<AvaloniaGlobalHotkeyService>();
                var screenshotService = ServiceProvider.GetRequiredService<AvaloniaActiveWindowScreenshotService>();
                hotkeyService.F8Pressed += async () =>
                {
                    try
                    {
                        await screenshotService.CaptureActiveWindowAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in the F8 screenshot handler");
                    }
                };
                hotkeyService.Initialize();

                if (!hotkeyService.IsRegistered)
                {
                    var localization = ServiceProvider.GetService<LocalizationService>();
                    var msg = localization?.GetString("F8ShortcutInUse")
                              ??
                              "The F8 shortcut key is already in use by another program. Because of this, the screenshot functionality is turned off.";
                    _ =
                        MessageDialogWindow.ShowAsync(mainWindow, msg, "SimpleLauncher", MessageButtons.Ok,
                            MessageIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing the F8 global hotkey. The screenshot functionality is turned off");
            }
#endif

            // Tray icon (cross-platform)
            try
            {
                var trayManager = ServiceProvider.GetRequiredService<AvaloniaTrayIconManager>();
                trayManager.Initialize(mainWindow);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing the tray icon. The application will continue without it");
            }

            // Phase 3 lifecycle: status-bar timer, write-access + required-files checks,
            // play-history migration, usage stats, and the silent update check.
            try
            {
                var lifecycle = ServiceProvider.GetRequiredService<AvaloniaApplicationLifecycleService>();
                var startupService = ServiceProvider.GetRequiredService<AvaloniaStartupInitializationService>();

                // Status text auto-clears after the configured timeout (default 3 s)
                startupService.StatusBarTimeout += mainWindow.ResetStatusText;
                // Pagination buttons start disabled until the first library load
                startupService.PaginationReset += () =>
                {
                    mainWindow.SetPrevPageButtonEnabled(false);
                    mainWindow.SetNextPageButtonEnabled(false);
                };

                _ = RunStartupTasksAsync(lifecycle, ServiceProvider);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing the application lifecycle service");
            }

            // WPF parity: the updater restarts the app with -whatsnew after a successful
            // update; show the update-history window (modal over the main window) once the
            // main loop is pumping.
            if (displayHistoryWindow)
                Dispatcher.UIThread.Post(() => _ = ShowUpdateHistoryWindowAsync(mainWindow));
        }

        base.OnFrameworkInitializationCompleted();

        // Apply the saved base theme + accent color (parity with WPF ThemeMenuService).
        try
        {
            var settingsManager = ServiceProvider.GetRequiredService<SettingsManagerService>();
            AvaloniaThemeService.ApplyTheme(settingsManager.BaseTheme, settingsManager.AccentColor);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply the saved theme at startup");
        }
    }

    /// <summary>
    ///     Registers all services, ViewModels, and windows in the DI container.
    /// </summary>
    internal static void ConfigureServices(IServiceCollection services, IConfiguration configuration,
        BugReportApiSink? bugReportSink = null)
    {
        // Register configuration
        services.AddSingleton(configuration);

        // Register the Serilog logger so ILogger ctor parameters resolve to Log.Logger
        services.AddSingleton(Log.Logger);

        // ── Named HttpClient factories ──
        // Each client gets its own SocketsHttpHandler with explicit TLS 1.2/1.3 support
        // (mirror of the WPF App.xaml.cs registrations).
        services.AddHttpClient("LogErrorsClient").ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("StatsClient").ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("UpdateCheckerClient", static client =>
        {
            // Keep the check responsive: a hung GitHub/fallback request must not stall
            // the update check for the default 100 s.
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("RetroAchievementsClient", static client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher.Avalonia/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("GameImageClient", client =>
        {
            var apiUrl = configuration.GetValue<string>("ApiSettings:GameImageUrl")
                         ?? "https://simple-launcher-api.doutorpeterson.workers.dev/";
            client.BaseAddress = new Uri(apiUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher.Avalonia/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("SupportWindowClient").ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("EasyModeClient", client =>
        {
            // Set the base address for the EasyMode configuration API
            var easyModeUrl = configuration.GetValue<string>("Urls:EasyModeApi")
                              ?? "https://www.purelogiccode.com/simplelauncheradmin/";
            if (!easyModeUrl.EndsWith('/')) easyModeUrl += '/';

            client.BaseAddress = new Uri(easyModeUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("GameClassificationClient", client =>
        {
            // Set the base address for the Microsoft Store game classification API
            var classificationUrl = configuration.GetValue<string>("Urls:GameClassificationApi")
                                    ?? "https://www.purelogiccode.com/simplelauncheradmin/";
            client.BaseAddress = new Uri(classificationUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher.Avalonia/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        services.AddHttpClient("ParameterResolverClient", client =>
        {
            // Set the base address for the parameter resolver API (same as the WPF app)
            var resolverUrl = configuration.GetValue<string>("Urls:ParameterResolverApi")
                              ?? "https://www.purelogiccode.com/simplelauncheradmin/";
            if (!resolverUrl.EndsWith('/')) resolverUrl += '/';

            client.BaseAddress = new Uri(resolverUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher.Avalonia/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);

        // Downloads get the standard resilience pipeline (retry with exponential backoff +
        // jitter) — image packs are large, multi-hundred-MB files over flaky home connections.
        // Same options as the WPF app. Retries are limited to this client because download
        // GETs are idempotent; API POSTs (bug reports, stats) must not be replayed.
        services.AddHttpClient("DownloadClient")
            .ConfigurePrimaryHttpMessageHandler(CreateHttpHandler)
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddStandardResilienceHandler(static options =>
            {
                options.Retry.MaxRetryAttempts = 5;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // The default total request timeout is 30 seconds — too short for large
                // emulator/image downloads on slow or balky connections (see bug 65965).
                // Downloads stream the body outside the pipeline, so this only bounds the
                // redirect chain + TLS handshake + response headers.
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            });

        // ── Host services (implement Core interfaces) ──
        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<IResourceProvider, AvaloniaResourceProvider>();
        services.AddSingleton<IWindowContext, AvaloniaWindowContext>();
        services.AddSingleton<IApplicationLifetime, AvaloniaApplicationLifetime>();
        services.AddSingleton<IMessageBoxLibraryService, MessageBoxLibraryService>();

        // ── Core services (from SimpleLauncher.Core) ──
        services.AddSingleton<DataFileLocation>();
        services.AddSingleton<InputSanitizerService>();
        services.AddSingleton<WindowsVersionService>();
        // Register the SAME instance wired to Serilog (see OnFrameworkInitializationCompleted)
        // so DI consumers share one sink instead of an uninitialized dead instance.
        if (bugReportSink is not null) services.AddSingleton(bugReportSink);

        services.AddSingleton<SettingsManagerService>(sp =>
        {
            var sm = new SettingsManagerService(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<ICredentialProtector>(),
                sp.GetRequiredService<IMessageBoxLibraryService>(),
                // Avalonia persists settings in the unified SQLite database
                // (settings.dat in AppData), like the WPF app.
                useUnifiedDatabase: true);
            // Load settings once at startup — same as the WPF app. Without this the
            // in-memory defaults would overwrite the persisted settings on the first save.
            sm.Load();
            return sm;
        });
        services.AddSingleton<MameDataService>();
        services.AddSingleton<IMameDataService>(sp => sp.GetRequiredService<MameDataService>());
        services.AddSingleton<IRetroAchievementsFileHasher, RetroAchievementsFileHasher>();
        services
            .AddSingleton<IRetroAchievementsEmulatorConfiguratorService,
                RetroAchievementsEmulatorConfiguratorService>();
        services.AddSingleton<IRetroAchievementsSystemMatcher, RetroAchievementsSystemMatcher>();
        services.AddSingleton<IRetroAchievementsHashStore, RetroAchievementsHashStore>();
        services.AddSingleton<IRetroAchievementsHashScanner, RetroAchievementsHashScanner>();
        services.AddSingleton<ParameterResolverService>();
        services.AddSingleton<IParameterResolverService>(sp => sp.GetRequiredService<ParameterResolverService>());
        services.AddSingleton<GetListOfFilesService>();
        services.AddSingleton<IGetListOfFilesService>(sp => sp.GetRequiredService<GetListOfFilesService>());
        services.AddSingleton<FindCoverImageService>();
        services.AddSingleton<IFindCoverImageService>(sp => sp.GetRequiredService<FindCoverImageService>());
        services.AddSingleton<ExtractionService>();
        services.AddSingleton<IExtractionService>(sp => sp.GetRequiredService<ExtractionService>());
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<DiscConverter>();
        services.AddSingleton<IDiscConverter>(sp => sp.GetRequiredService<DiscConverter>());
        services.AddSingleton<EasyModeManager>();
        services.AddSingleton<ExternalToolLauncherService>();
        services.AddSingleton<PlaySoundEffects>();
        services.AddSingleton<IPlaySoundEffects>(sp => sp.GetRequiredService<PlaySoundEffects>());
        services.AddSingleton<GamePadController>();
        services.AddSingleton<SystemConfigurationWriterService>(sp =>
            new SystemConfigurationWriterService(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ILogger>(),
                // Avalonia persists systems in the unified SQLite database
                // (settings.dat in AppData), like the WPF app.
                useUnifiedDatabase: true));
        services.AddSingleton<ISystemConfigurationWriterService>(sp =>
            sp.GetRequiredService<SystemConfigurationWriterService>());
        services.AddSingleton<Stats>();

        // Mount services
        services.AddSingleton<IMountZipFiles, MountZipFiles>();
        services.AddSingleton<IMountIsoFiles, MountIsoFiles>();
        services.AddSingleton<IMountChdFiles, MountChdFiles>();
        services.AddSingleton<IMountXisoFiles, MountXisoFiles>();
        services.AddSingleton<FileFinderService>();
        services.AddSingleton<IFileLockService, FileLockService>();

        // File/disk services (non-static wrappers)
        services.AddSingleton<CleanTempFolderService>();
        services.AddSingleton<ICleanTempFolderService>(sp => sp.GetRequiredService<CleanTempFolderService>());
        services.AddSingleton<CleanSimpleLauncherFolderService>();
        services.AddSingleton<ICleanSimpleLauncherFolderService>(sp =>
            sp.GetRequiredService<CleanSimpleLauncherFolderService>());
        services.AddSingleton<DeleteFilesService>();
        services.AddSingleton<IDeleteFilesService>(sp => sp.GetRequiredService<DeleteFilesService>());
        services.AddSingleton<FormatFileSizeService>();
        services.AddSingleton<IFormatFileSizeService>(sp => sp.GetRequiredService<FormatFileSizeService>());

        // ── Credential protector ──
        services.AddSingleton<ICredentialProtector, WindowsCredentialProtector>();

        // ── App-specific services ──
        services.AddSingleton<SystemArtRatioService>();
        services.AddSingleton<IPaginationService, AvaloniaPaginationService>();
        // Game file watcher: Core service monitors the folders, the Avalonia wrapper
        // re-raises events for the main window's live library refresh.
        services.AddSingleton<GameFileWatcherService>();
        services.AddSingleton<AvaloniaGameFileWatcherService>();
        // Game file loading: per-system file list cache + scan orchestration.
        services.AddSingleton<AvaloniaGameCacheService>();
        services.AddSingleton<AvaloniaGameFileLoadingOrchestrator>();
        // System image resolver: fuzzy-matching system icon lookup (WPF SystemImageResolverService parity).
        services.AddSingleton<ISystemImageResolverService, SystemImageResolverService>();
        // UI reset: clears filters, selections, and returns to All Games (WPF UiResetService parity).
        services.AddSingleton<UiResetService>();
        // System selection orchestrator: coordinates System/Emulator combos, sidebar, and watcher (WPF SystemSelectionOrchestratorService parity).
        services.AddSingleton<AvaloniaSystemSelectionOrchestratorService>();

        // ── Extracted services (WPF parity — Section 10 of AvaloniaMissingFeatures.md) ──
        // Game filter: letter filter, MAME sort, search query, show-games visibility.
        services.AddSingleton<AvaloniaGameFilterService>();
        // Status bar: status text updates with auto-clear timeout.
        services.AddSingleton<AvaloniaUpdateStatusBarService>();
        // Search orchestrator: validates search queries before execution.
        services.AddSingleton<AvaloniaSearchOrchestratorService>();
        // Context menu: builds game right-click context menus.
        services.AddSingleton<AvaloniaContextMenuFunctions>();
        services.AddSingleton<AvaloniaContextMenuService>();
        // Display system info: validates system configuration and produces display models.
        services.AddSingleton<AvaloniaDisplaySystemInformation>();
        // Loading overlay: thread-safe reference-counted loading state.
        services.AddSingleton<AvaloniaLoadingOverlayService>();

        // ── ViewModels ──
        services.AddSingleton<MainViewModel>();
        services.AddTransient<EasyModeViewModel>();

        // ── MainWindow page sections (WPF FavoritesPage / PlayHistoryPage / GlobalSearchPage equivalents) ──
        services.AddSingleton<FavoritesSectionViewModel>();
        services.AddSingleton<PlayHistorySectionViewModel>();
        services.AddSingleton<GlobalSearchSectionViewModel>();

        // ── App services (Phase 4–6) ──
        services.AddSingleton(_ => FavoritesManager.LoadFavorites(Log.Logger));
        services.AddSingleton(_ => PlayHistoryManager.LoadPlayHistory(Log.Logger));
        services.AddSingleton<SystemManagerService>();
        // Single shared launcher instance: MainViewModel reads LastPlayTime from the
        // concrete type, so ILauncherService and MinimalLauncherService must resolve
        // to the SAME instance.
        services.AddSingleton<LauncherService>();
        services.AddSingleton<ILauncherService>(sp => sp.GetRequiredService<LauncherService>());

        // ── Launch strategies (full WPF pipeline: ascending priority, Default last) ──
        services.AddSingleton<ILaunchStrategy, ChdMountStrategy>();
        services.AddSingleton<ILaunchStrategy, PbpToCueStrategy>();
        services.AddSingleton<ILaunchStrategy, CommanderGeniusLaunchStrategy>();
        services.AddSingleton<ILaunchStrategy, ChdToCueStrategy>();
        services.AddSingleton<ILaunchStrategy, DosBoxLaunchStrategy>();
        services.AddSingleton<ILaunchStrategy, XisoMountStrategy>();
        services.AddSingleton<ILaunchStrategy, ZipMountStrategy>();
        services.AddSingleton<ILaunchStrategy, DefaultLaunchStrategy>();
        services.AddSingleton<AskAiToFixParameters>();
        services.AddSingleton<EmulatorPathResolver>();

        // NOTE: LocalizationService must stay parameterless in DI. Its factory used to
        // resolve SettingsManagerService here, which created a factory-level cycle
        // (LocalizationService -> SettingsManagerService -> IMessageBoxLibraryService ->
        // LocalizationService). MS.DI cannot see cycles through factory lambdas, so
        // startup deadlocked inside StackGuard (no exception, hidden main window).
        // The saved language is applied below, right after the container is built.
        services.AddSingleton<LocalizationService>();
        // ── Game platform scanners (WPF parity: Steam, Epic, Amazon, Battle.net, GOG,
        // Humble, itch.io, Rockstar, Ubisoft, EA, Microsoft Store) ──
        services.AddSingleton<ISteamVdfParser, SteamVdfParser>();
        services.AddSingleton<IIconExtractor, IconExtractor>();
        services.AddSingleton<IGamePlatformScanner, ScanSteamGames>();
        services.AddSingleton<IGamePlatformScanner, ScanEpicGames>();
        services.AddSingleton<IGamePlatformScanner, ScanAmazonGames>();
        services.AddSingleton<IGamePlatformScanner, ScanBattleNetGames>();
        services.AddSingleton<IGamePlatformScanner, ScanGogGames>();
        services.AddSingleton<IGamePlatformScanner, ScanHumbleGames>();
        services.AddSingleton<IGamePlatformScanner, ScanItchioGames>();
        services.AddSingleton<IGamePlatformScanner, ScanRockstarGames>();
        services.AddSingleton<IGamePlatformScanner, ScanUplayGames>();
        services.AddSingleton<IGamePlatformScanner, ScanEaGames>();
        services.AddSingleton<IGamePlatformScanner, ScanMicrosoftStoreGames>();
        services.AddSingleton<GameScannerService>();
        services.AddSingleton<RetroAchievementsService>();
        services.AddSingleton<AvaloniaCheckForUpdatesService>();

        // ── Quit / reinstall (WPF QuitSimpleLauncher / ReinstallSimpleLauncher parity) ──
        services.AddSingleton<AvaloniaQuitSimpleLauncher>();

        // ── Phase 3 lifecycle services ──
        services.AddSingleton<CheckForRequiredFilesService>();
        services.AddSingleton<AvaloniaStartupInitializationService>();
        services.AddSingleton<AvaloniaApplicationLifecycleService>();
        // Parameters help (parameters.md) for the Edit System window
        services.AddSingleton<AvaloniaHelpUserService>();
        // Language menu + option menu check marks (used by the main window menu bar)
        services.AddSingleton<AvaloniaLanguageMenuService>();
        services.AddSingleton<AvaloniaMenuCheckMarkService>();

        // ── Emulator config injection (21 emulators) ──
        // ViewModels (transient — one per window instance)
        services.AddTransient<InjectAresConfigViewModel>();
        services.AddTransient<InjectAzaharConfigViewModel>();
        services.AddTransient<InjectBlastemConfigViewModel>();
        services.AddTransient<InjectCemuConfigViewModel>();
        services.AddTransient<InjectDaphneConfigViewModel>();
        services.AddTransient<InjectDolphinConfigViewModel>();
        services.AddTransient<InjectDuckStationConfigViewModel>();
        services.AddTransient<InjectFlycastConfigViewModel>();
        services.AddTransient<InjectMameConfigViewModel>();
        services.AddTransient<InjectMednafenConfigViewModel>();
        services.AddTransient<InjectMesenConfigViewModel>();
        services.AddTransient<InjectPcsx2ConfigViewModel>();
        services.AddTransient<InjectRaineConfigViewModel>();
        services.AddTransient<InjectRedreamConfigViewModel>();
        services.AddTransient<InjectRetroArchConfigViewModel>();
        services.AddTransient<InjectRpcs3ConfigViewModel>();
        services.AddTransient<InjectSegaModel2ConfigViewModel>();
        services.AddTransient<InjectStellaConfigViewModel>();
        services.AddTransient<InjectSupermodelConfigViewModel>();
        services.AddTransient<InjectXeniaConfigViewModel>();
        services.AddTransient<InjectYumirConfigViewModel>();

        // Windows (transient — new instance each resolve, same as the WPF app)
        services.AddTransient<InjectAresConfigWindow>();
        services.AddTransient<InjectAzaharConfigWindow>();
        services.AddTransient<InjectBlastemConfigWindow>();
        services.AddTransient<InjectCemuConfigWindow>();
        services.AddTransient<InjectDaphneConfigWindow>();
        services.AddTransient<InjectDolphinConfigWindow>();
        services.AddTransient<InjectDuckStationConfigWindow>();
        services.AddTransient<InjectFlycastConfigWindow>();
        services.AddTransient<InjectMameConfigWindow>();
        services.AddTransient<InjectMednafenConfigWindow>();
        services.AddTransient<InjectMesenConfigWindow>();
        services.AddTransient<InjectPcsx2ConfigWindow>();
        services.AddTransient<InjectRaineConfigWindow>();
        services.AddTransient<InjectRedreamConfigWindow>();
        services.AddTransient<InjectRetroArchConfigWindow>();
        services.AddTransient<InjectRpcs3ConfigWindow>();
        services.AddTransient<InjectSegaModel2ConfigWindow>();
        services.AddTransient<InjectStellaConfigWindow>();
        services.AddTransient<InjectSupermodelConfigWindow>();
        services.AddTransient<InjectXeniaConfigWindow>();
        services.AddTransient<InjectYumirConfigWindow>();

        // Handlers (singletons, same as the WPF app) — MinimalLauncherService runs
        // every matching handler before a game launches (silent injection, or the
        // config window when "Show settings before launch" is enabled).
        services.AddSingleton<IEmulatorConfigHandler, AresConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, AzaharConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, BlastemConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, CemuConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, DaphneConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, DolphinConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, DuckStationConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, FlycastConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, MameConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, MednafenConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, MesenConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, Pcsx2ConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, RaineConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, RedreamConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, RetroArchConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, Rpcs3ConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, SegaModel2ConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, StellaConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, SupermodelConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, XeniaConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, YumirConfigHandler>();

        // ── RetroAchievements UI ──
        services.AddTransient<SystemSelectionViewModel>();
        services.AddTransient<SystemSelectionWindow>();
        services.AddTransient<RetroAchievementsSettingsViewModel>();
        services.AddTransient<RetroAchievementsSettingsWindow>();
        services.AddTransient<ImageViewerViewModel>();
        services.AddTransient<ImageViewerWindow>();
        services.AddTransient<RetroAchievementsWindow>();
        services.AddTransient<RetroAchievementsForAGameWindow>();
        // Load the RA game database once (hash -> game lookups need the .dat file)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            return RetroAchievementsManager.LoadRetroAchievement(logger, logger);
        });
        services.AddSingleton<IRetroAchievementsHasherTool>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            return new RetroAchievementsHasherTool(
                logger,
                sp.GetRequiredService<IExtractionService>(),
                SelectSystemAsync,
                sp.GetRequiredService<IRetroAchievementsSystemMatcher>(),
                sp.GetRequiredService<IRetroAchievementsFileHasher>());

            async Task<string?> SelectSystemAsync(string guess)
            {
                var win = sp.GetRequiredService<SystemSelectionWindow>();
                win.Initialize(guess);
                await win.ShowDialog(
                    (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                    ?? throw new InvalidOperationException("Main window not available"));
                return win.SelectedSystem;
            }
        });

        // ── Windows (transient — new instance each resolve) ──
        // NOTE: GameDetailWindow is intentionally NOT registered — it takes per-game
        // constructor arguments (GameCardViewModel + MainViewModel) and is created manually.
        services.AddTransient<MainWindow>();
        services.AddTransient<PreferencesWindow>();
        services.AddTransient<EasyModeWindow>();
        services.AddTransient<EditSystemWindow>();
        // Factory so callers can pass a pre-selected system name to EditSystemWindow
        // (the plain AddTransient above always resolves with the optional null default).
        services.AddTransient<Func<string?, EditSystemWindow>>(sp =>
            preSelectedSystemName => new EditSystemWindow(
                sp.GetRequiredService<PlaySoundEffects>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IMessageBoxLibraryService>(),
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<ISystemConfigurationWriterService>(),
                sp.GetRequiredService<SystemManagerService>(),
                sp.GetRequiredService<IFilePickerService>(),
                sp.GetRequiredService<FavoritesManager>(),
                sp.GetRequiredService<PlayHistoryManager>(),
                sp.GetRequiredService<IParameterResolverService>(),
                sp.GetRequiredService<AvaloniaHelpUserService>(),
                sp.GetRequiredService<LocalizationService>(),
                sp.GetRequiredService<SettingsManagerService>(),
                preSelectedSystemName));

        // ── Phase 4.1 windows (ViewModels transient — one per window instance) ──
        services.AddTransient<AboutViewModel>();
        services.AddTransient<AboutWindow>();
        services.AddTransient<UpdateHistoryViewModel>();
        services.AddTransient<UpdateHistoryWindow>();
        services.AddTransient<UpdateLogViewModel>();
        services.AddTransient<UpdateLogWindow>();
        services.AddTransient<DebugViewModel>();
        services.AddTransient<DebugWindow>();
        services.AddTransient<SupportViewModel>();
        services.AddTransient<SupportWindow>();
        services.AddTransient<SoundConfigurationViewModel>();
        services.AddTransient<SoundConfigurationWindow>();
        services.AddTransient<SetLinksViewModel>();
        services.AddTransient<SetLinksWindow>();
        services.AddTransient<SetFuzzyMatchingViewModel>();
        services.AddTransient<SetFuzzyMatchingWindow>();
        services.AddTransient<SetGamepadDeadZoneViewModel>();
        services.AddTransient<SetGamepadDeadZoneWindow>();
        services.AddTransient<RomHistoryViewModel>();
        services.AddTransient<RomHistoryWindow>();
        services.AddTransient<GlobalStatsViewModel>();
        services.AddTransient<GlobalStatsWindow>();
        services.AddTransient<DownloadImagePackViewModel>();
        services.AddTransient<DownloadImagePackWindow>();
        services.AddTransient<DosBoxFileSelectionViewModel>();
        services.AddTransient<DosBoxFileSelectionWindow>();
        services.AddTransient<FlashOverlayViewModel>();
        services.AddTransient<FlashOverlayWindow>();
        services.AddTransient<WindowSelectionDialogViewModel>();
        services.AddTransient<WindowSelectionDialogWindow>();

        // ── Phase 7: cross-platform + Windows-only services ──
        // Tray icon: cross-platform (Avalonia TrayIcon supports Windows and Linux)
        services.AddSingleton<AvaloniaTrayIconManager>();
#if WINDOWS
        // F8 global hotkey + active-window screenshot: Windows-only (net10.0-windows TFM)
        WindowScreenshot.Initialize(Log.Logger);
        services.AddSingleton<AvaloniaGlobalHotkeyService>();
        services.AddSingleton<AvaloniaActiveWindowScreenshotService>();
#endif
    }

    /// <summary>
    ///     Creates the primary handler for named HttpClients: explicit TLS 1.2/1.3,
    ///     5-minute pooled connection lifetime, and a 20-second connect timeout.
    ///     Mirror of the WPF App.xaml.cs CreateHttpHandler.
    /// </summary>
    private static HttpMessageHandler CreateHttpHandler()
    {
        return new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            },
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(20)
        };
    }

    /// <summary>
    ///     Listens for a signal from a second instance and brings the main window to foreground.
    /// </summary>
    private async Task ListenForSecondInstanceAsync(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        while (_instanceSignal is not null)
        {
            try
            {
                await Task.Run(() => _instanceSignal.WaitOne());
            }
            catch (ObjectDisposedException)
            {
                Log.Debug("Instance signal disposed; stopping second-instance listener");
                break;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (lifetime.MainWindow is { } window)
                {
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;

                    window.Activate();
                    window.Topmost = true;
                    window.Topmost = false;
                    window.Focus();
                }
            });
        }
    }

    /// <summary>
    ///     Shows the update-history window when the app is restarted with the -whatsnew
    ///     argument (WPF parity: the Avalonia updater passes -whatsnew on restart).
    /// </summary>
    private static async Task ShowUpdateHistoryWindowAsync(Window owner)
    {
        try
        {
            var updateHistoryWindow = ServiceProvider.GetRequiredService<UpdateHistoryWindow>();
            await updateHistoryWindow.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error showing UpdateHistoryWindow with -whatsnew argument");
        }
    }

    /// <summary>
    ///     Runs the startup sequence in the background: play-history migration, startup
    ///     initialization checks, usage stats, and the silent update check. All failures
    ///     are logged — none of them should block the main window from showing.
    /// </summary>
    private static async Task RunStartupTasksAsync(AvaloniaApplicationLifecycleService lifecycle,
        IServiceProvider services)
    {
        try
        {
            // Migrate legacy play-history entries (filename → full path) before the
            // first library scan so "Recently Played" resolves old entries correctly.
            var systems = services.GetRequiredService<SystemManagerService>().LoadSystems();
            await lifecycle.MigratePlayHistoryAsync(systems);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to migrate play history on startup");
        }

        try
        {
            await lifecycle.RunStartupInitializationAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to run the startup initialization tasks");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await lifecycle.ReportUsageAsync();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Usage stats reporting failed on startup");
            }
        });

        // Call ApplicationStats API on startup (WPF parity)
        _ = Task.Run(async () =>
        {
            try
            {
                var configuration = services.GetRequiredService<IConfiguration>();
                var logger = services.GetRequiredService<ILogger>();
                await ApplicationStats.CallApplicationStatsAsync(configuration, logger);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ApplicationStats API call failed on startup");
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await lifecycle.SilentCheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Silent update check failed on startup");
            }
        });
    }

    #region Global Exception Handlers

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Fatal(ex, "Unhandled domain exception");
        if (ex != null)
            LogExceptionToFile(ex);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception");
        LogExceptionToFile(e.Exception);

        var lifetime = Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

        // If the main window is no longer visible (startup template crash, window already
        // closed, etc.), shutting down avoids a headless process running in the background.
        if (lifetime?.MainWindow is not { IsVisible: true })
        {
            Log.Fatal(e.Exception, "Main window is not visible; shutting down to avoid a background process");
            lifetime?.Shutdown();
            return;
        }

        e.Handled = true; // Prevent crash; app continues
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        LogExceptionToFile(e.Exception);
        e.SetObserved();
    }

    private static void LogExceptionToFile(Exception ex)
    {
        try
        {
            var crashPath = Path.Combine(
                AppDataPaths.SimpleLauncherDataFolder, "crash_new.log");
            File.AppendAllText(crashPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logEx)
        {
            Log.Error(logEx, "Failed to write crash log");
        }
    }

    #endregion
}