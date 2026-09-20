using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Net.Security;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ControlzEx.Theming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Serilog.Events;
using SimpleLauncher.Core;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckForFileLock;
using SimpleLauncher.Core.Services.CheckIfDirectoryIsWritable;
using SimpleLauncher.Core.Services.CheckPaths;
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
using SimpleLauncher.Core.Services.UsageStats;
using SimpleLauncher.Core.Services.WpfServices;
using SimpleLauncher.InjectConfigWindows;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services;
using SimpleLauncher.Services.ContextMenu;
using SimpleLauncher.Services.DebugAndBugReport;
using SimpleLauncher.Services.DisplaySystemInfo;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.GameBrowser;
using SimpleLauncher.Services.GameCache;
using SimpleLauncher.Services.GameFileLoadingOrchestrator;
using SimpleLauncher.Services.GameFilter;
using SimpleLauncher.Services.GameItemRender;
using SimpleLauncher.Services.GameLauncher;
using SimpleLauncher.Services.GameLauncher.Handlers;
using SimpleLauncher.Services.GameLauncher.Strategies;
using SimpleLauncher.Services.GameListUI;
using SimpleLauncher.Services.GameScan;
using SimpleLauncher.Services.HelpUser;
using SimpleLauncher.Services.LanguageMenu;
using SimpleLauncher.Services.LoadingOverlay;
using SimpleLauncher.Services.MenuActionHandler;
using SimpleLauncher.Services.MenuCheckMark;
using SimpleLauncher.Services.MenuOrchestrator;
using SimpleLauncher.Services.MessageBox;
using SimpleLauncher.Services.NotificationToast;
using SimpleLauncher.Services.Pagination;
using SimpleLauncher.Services.PlayHistory;
using SimpleLauncher.Services.QuitOrReinstall;
using SimpleLauncher.Services.RetroAchievements;
using SimpleLauncher.Services.SearchOrchestrator;
using SimpleLauncher.Services.SettingsDatabase;
using SimpleLauncher.Services.StartupInitialization;
using SimpleLauncher.Services.SystemConfiguration;
using SimpleLauncher.Services.SystemImageResolver;
using SimpleLauncher.Services.SystemSelectionOrchestrator;
using SimpleLauncher.Services.TakeScreenshot;
using SimpleLauncher.Services.ThemeMenu;
using SimpleLauncher.Services.UiOrchestrator;
using SimpleLauncher.Services.UIReset;
using SimpleLauncher.Services.UpdateStatusBar;
using SimpleLauncher.Services.UsageStats;
using SimpleLauncher.Services.WpfServices;
using SimpleLauncher.ViewModels;
using DosBoxFileSelectionViewModel = SimpleLauncher.ViewModels.DosBoxFileSelectionViewModel;
using SystemSelectionViewModel = SimpleLauncher.ViewModels.SystemSelectionViewModel;
using UpdateChecker = SimpleLauncher.Services.CheckForUpdatesService;

namespace SimpleLauncher;

/// <summary>
///     Application entry point handling DI container setup, single-instance enforcement, theming, and global error
///     handling.
/// </summary>
public partial class App : IDisposable
{
    private const int SwRestore = 9;
    private EventWaitHandle _instanceSignal = null!;
    private bool _isFirstInstance;

    private Mutex _singleInstanceMutex = null!;

    /// <summary>
    ///     Gets the application's dependency injection service provider.
    /// </summary>
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>
    ///     Disposes application resources including the single-instance mutex and signal event.
    /// </summary>
    public void Dispose()
    {
        _instanceSignal?.Dispose();
        _singleInstanceMutex?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Handles application startup including DI registration, single-instance check, and theme initialization.
    /// </summary>
    /// <param name="e">The startup event arguments.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true);

        var configuration = builder.Build();

        // Decrypt the API key once at launch so it is available to every service at runtime.
        AppConstants.InitializeApiKey(configuration["ApiKey"]);

        // Parse args early for DI registration
        var isDebugMode = e.Args.Any(static arg => arg.Equals("-debug", StringComparison.OrdinalIgnoreCase));

        var bugReportSink = new BugReportApiSink();

        var appDataLogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleLauncher");
        Directory.CreateDirectory(appDataLogFolder);

        var logFilePath = PathHelper.ResolveLogFilePath(configuration);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: "[{Level}] {Timestamp:HH:mm:ss.fff} - {Message}{NewLine}{Exception}")
            .WriteTo.Sink(new DebugWindowSink())
            .WriteTo.Async(a => a.File(
                logFilePath,
                LogEventLevel.Warning,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
            .WriteTo.Sink(bugReportSink)
            .CreateLogger();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddHttpClient("LogErrorsClient").ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        serviceCollection.AddHttpClient("StatsClient").ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        serviceCollection.AddHttpClient("UpdateCheckerClient", static client =>
        {
            // Keep the check responsive: a hung GitHub/fallback request must not stall
            // the update check for the default 100 s.
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        serviceCollection.AddHttpClient("SupportWindowClient").ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);
        serviceCollection.AddHttpClient("RetroAchievementsClient", static client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);

        serviceCollection.AddHttpClient("GameImageClient", client =>
        {
            var apiUrl = configuration.GetValue<string>("ApiSettings:GameImageUrl") ??
                         "https://simple-launcher-api.doutorpeterson.workers.dev/";
            client.BaseAddress = new Uri(apiUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);

        serviceCollection.AddHttpClient("EasyModeClient", client =>
        {
            // Set the base address for the EasyMode configuration API
            var easyModeUrl = configuration.GetValue<string>("Urls:EasyModeApi") ??
                              "https://www.purelogiccode.com/simplelauncheradmin/";
            if (!easyModeUrl.EndsWith('/')) easyModeUrl += "/";

            client.BaseAddress = new Uri(easyModeUrl);
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);

        serviceCollection.AddHttpClient("GameClassificationClient", client =>
        {
            var classificationUrl = configuration.GetValue<string>("Urls:GameClassificationApi") ??
                                    "https://www.purelogiccode.com/simplelauncheradmin/";
            client.BaseAddress = new Uri(classificationUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);

        serviceCollection.AddHttpClient("ParameterResolverClient", client =>
        {
            var resolverUrl = configuration.GetValue<string>("Urls:ParameterResolverApi") ??
                              "https://www.purelogiccode.com/simplelauncheradmin/";
            if (!resolverUrl.EndsWith('/')) resolverUrl += "/";

            client.BaseAddress = new Uri(resolverUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "SimpleLauncher/1.0");
        }).ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);

        serviceCollection.AddHttpClient("DownloadClient")
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

        // Register IConfiguration
        serviceCollection.AddSingleton<IConfiguration>(configuration);

        // Register IMemoryCache
        serviceCollection.AddMemoryCache();

        // Register Managers as singletons
        serviceCollection.AddSingleton(Log.Logger);
        serviceCollection.AddSingleton<ICredentialProtector, WindowsCredentialProtector>();
        serviceCollection.AddSingleton(static provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var logger = provider.GetRequiredService<ILogger>();
            var messageBox = provider.GetRequiredService<IMessageBoxLibraryService>();
            var credentialProtector = provider.GetRequiredService<ICredentialProtector>();
            var sm = new SettingsManagerService(config, logger, credentialProtector, messageBox,
                // The WPF app persists settings in the unified SQLite database
                // (settings.dat in AppData), like the Avalonia app.
                useUnifiedDatabase: true);
            sm.Load();
            return sm;
        });
        serviceCollection.AddSingleton<UpdateChecker>();
        serviceCollection.AddSingleton<QuitSimpleLauncher>();
        serviceCollection.AddSingleton<ReinstallSimpleLauncher>();
        serviceCollection.AddSingleton<Stats>();
        serviceCollection.AddSingleton<PlaySoundEffects>();
        serviceCollection.AddSingleton<IPlaySoundEffects>(static sp => sp.GetRequiredService<PlaySoundEffects>());
        serviceCollection.AddSingleton<GamePadController>();
        serviceCollection.AddTransient<DownloadManager>();
        serviceCollection.AddSingleton<GameLauncherService>();
        serviceCollection.AddSingleton<IExternalToolLauncher, ExternalToolLauncherService>();
        serviceCollection.AddSingleton<IDeleteFilesService, DeleteFilesService>();
        serviceCollection.AddSingleton<ICleanTempFolderService, CleanTempFolderService>();
        serviceCollection.AddSingleton<ICleanSimpleLauncherFolderService, CleanSimpleLauncherFolderService>();
        serviceCollection.AddSingleton<IMountXisoFiles, MountXisoFiles>();
        serviceCollection.AddSingleton<IMountChdFiles, MountChdFiles>();
        serviceCollection.AddSingleton<IMountIsoFiles, MountIsoFiles>();
        serviceCollection.AddSingleton<IMountZipFiles, MountZipFiles>();
        serviceCollection.AddSingleton<IExtractionService, ExtractionService>();
        serviceCollection.AddSingleton<RetroAchievementsService>();
        serviceCollection
            .AddSingleton<IRetroAchievementsEmulatorConfiguratorService,
                RetroAchievementsEmulatorConfiguratorService>();
        serviceCollection.AddSingleton<IRetroAchievementsSystemMatcher, RetroAchievementsSystemMatcher>();
        serviceCollection.AddSingleton<IRetroAchievementsFileHasher, RetroAchievementsFileHasher>();
        serviceCollection.AddSingleton<IRetroAchievementsHashStore, RetroAchievementsHashStore>();
        serviceCollection.AddSingleton<IRetroAchievementsHashScanner, RetroAchievementsHashScanner>();
        serviceCollection.AddSingleton<IToastNotificationService, ToastNotificationService>();
        serviceCollection.AddSingleton<IContextMenuService, ContextMenuService>();
        serviceCollection.AddSingleton(static sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            return FavoritesManager.LoadFavorites(logger);
        });
        serviceCollection.AddSingleton(static sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            return PlayHistoryManager.LoadPlayHistory(logger);
        });
        serviceCollection.AddSingleton(static sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            return RetroAchievementsManager.LoadRetroAchievement(logger, logger);
        });
        // Game platform scanners
        serviceCollection.AddSingleton<ISteamVdfParser, SteamVdfParser>();
        serviceCollection.AddSingleton<IIconExtractor, IconExtractor>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanSteamGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanEpicGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanAmazonGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanBattleNetGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanGogGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanHumbleGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanItchioGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanRockstarGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanUplayGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanEaGames>();
        serviceCollection.AddSingleton<IGamePlatformScanner, ScanMicrosoftStoreGames>();
        serviceCollection.AddSingleton<GameScannerService>();
        serviceCollection.AddSingleton<ThemeMenuService>();
        serviceCollection.AddSingleton<LanguageMenuService>();
        serviceCollection.AddSingleton<LoadingOverlayService>();
        serviceCollection.AddSingleton<StartupInitializationService>();
        serviceCollection.AddSingleton<GameListUiService>();
        serviceCollection.AddSingleton<GameFileWatcherService>();
        serviceCollection.AddSingleton<MenuActionHandlerService>();
        serviceCollection.AddSingleton<IContextMenuFunctions, ContextMenuFunctions>();
        serviceCollection.AddSingleton<IDisplaySystemInformation, DisplaySystemInformation>();
        serviceCollection.AddSingleton<IHelpUserService, HelpUserService>();
        serviceCollection.AddSingleton<IGetListOfFilesService, GetListOfFilesService>();
        serviceCollection.AddSingleton<IUpdateStatusBar, UpdateStatusBarService>();
        serviceCollection.AddSingleton<IFindCoverImageService>(static sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger>();
            var settings = sp.GetRequiredService<SettingsManagerService>();
            return new FindCoverImageService(configuration, logger, settings);
        });
        serviceCollection.AddSingleton<IImageLoader, WpfImageLoader>();
        serviceCollection.AddSingleton<IMenuCheckMarkService, MenuCheckMarkService>();
        serviceCollection.AddSingleton<IUiResetService, UiResetService>();
        serviceCollection.AddSingleton<ISystemConfigurationService, SystemConfigurationService>();
        serviceCollection.AddSingleton<IPaginationService, PaginationService>();
        serviceCollection.AddSingleton<IGameCacheService, GameCacheService>();
        serviceCollection.AddSingleton<IGameFilterService, GameFilterService>();
        serviceCollection.AddSingleton<IMameDataService, MameDataService>();
        serviceCollection.AddSingleton<ISearchOrchestratorService, SearchOrchestratorService>();
        serviceCollection.AddSingleton<ISystemImageResolverService, SystemImageResolverService>();

        // Phase 5: WPF platform service implementations
        serviceCollection.AddSingleton<IMessageDialogService, WpfMessageDialogService>();
        serviceCollection.AddSingleton<IResourceProvider, WpfResourceProvider>();
        serviceCollection.AddSingleton<IDispatcherService, WpfDispatcherService>();
        serviceCollection.AddSingleton<IFilePickerService, WpfFilePickerService>();
        serviceCollection.AddSingleton<IApplicationLifetime, WpfApplicationLifetime>();
        serviceCollection.AddSingleton<IMessageBoxLibraryService, MessageBoxLibraryService>();
        serviceCollection.AddSingleton<IParameterResolverService, ParameterResolverService>();
        serviceCollection.AddSingleton<IUiOrchestrator, UiOrchestratorService>();
        serviceCollection.AddSingleton<IGameItemRenderService, GameItemRenderService>();
        serviceCollection.AddSingleton<IRetroAchievementsHasherTool>(static sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            var extractionService = sp.GetRequiredService<IExtractionService>();
            var systemMatcher = sp.GetRequiredService<IRetroAchievementsSystemMatcher>();
            var fileHasher = sp.GetRequiredService<IRetroAchievementsFileHasher>();
            return new RetroAchievementsHasherTool(logger, extractionService, SelectSystemAsync, systemMatcher,
                fileHasher);

            Task<string?> SelectSystemAsync(string guess)
            {
                var win = SystemSelectionWindowFactory();
                win.Owner = MainWindowFactory();
                win.Initialize(guess);
                return Task.FromResult(win.ShowDialog() == true ? win.SelectedSystem : null);
            }

            SystemSelectionWindow SystemSelectionWindowFactory()
            {
                return new SystemSelectionWindow(sp.GetRequiredService<SystemSelectionViewModel>());
            }

            static Window MainWindowFactory()
            {
                if (Current.MainWindow != null) return Current.MainWindow;

                throw new InvalidOperationException();
            }
        });
        serviceCollection.AddSingleton<ISystemSelectionOrchestrator, SystemSelectionOrchestratorService>();
        serviceCollection.AddSingleton<IGameFileLoadingOrchestrator, GameFileLoadingOrchestratorService>();

        // Core service implementations
        serviceCollection.AddSingleton<IWindowsVersionService, WindowsVersionService>();
        serviceCollection.AddSingleton<IDirectoryValidationService, DirectoryValidationService>();
        serviceCollection.AddSingleton<IFileLockService, FileLockService>();
        serviceCollection.AddSingleton<IInputSanitizerService, InputSanitizerService>();
        serviceCollection.AddSingleton<IFileFinderService, FileFinderService>();
        serviceCollection.AddSingleton<IFormatFileSizeService, FormatFileSizeService>();
        serviceCollection.AddSingleton<IDiscConverter, DiscConverter>();

        // Facade services
        serviceCollection.AddSingleton<IAudioInputService, AudioInputService>();
        serviceCollection.AddSingleton<IApplicationLifecycleService, ApplicationLifecycleService>();
        serviceCollection.AddSingleton<IMenuOrchestrator, MenuOrchestratorService>();
        serviceCollection.AddSingleton<IGameBrowserService, GameBrowserService>();

        // F8 Screenshot Hotkey
        serviceCollection.AddSingleton<GlobalHotkeyService>();
        serviceCollection.AddSingleton<ActiveWindowScreenshotService>();

        // ViewModels
        serviceCollection.AddTransient<AboutViewModel>();
        serviceCollection.AddTransient<DebugViewModel>();
        serviceCollection.AddTransient<UpdateHistoryViewModel>();
        serviceCollection.AddTransient<ImageViewerViewModel>();
        serviceCollection.AddTransient<SetFuzzyMatchingViewModel>();
        serviceCollection.AddTransient<SetGamepadDeadZoneViewModel>();
        serviceCollection.AddTransient<SetLinksViewModel>();
        serviceCollection.AddTransient<SoundConfigurationViewModel>();
        serviceCollection.AddTransient<RomHistoryViewModel>();
        serviceCollection.AddTransient<SupportViewModel>();
        serviceCollection.AddTransient<RetroAchievementsSettingsViewModel>();
        serviceCollection.AddTransient<DownloadImagePackViewModel>();
        serviceCollection.AddTransient<FlashOverlayViewModel>();
        serviceCollection.AddTransient<UpdateLogViewModel>();
        serviceCollection.AddTransient<GlobalStatsViewModel>();
        serviceCollection.AddTransient<DosBoxFileSelectionViewModel>();
        serviceCollection.AddTransient<SystemSelectionViewModel>();
        serviceCollection.AddTransient<WindowSelectionDialogViewModel>();
        serviceCollection.AddTransient<InjectAresConfigViewModel>();
        serviceCollection.AddTransient<InjectAzaharConfigViewModel>();
        serviceCollection.AddTransient<InjectBlastemConfigViewModel>();
        serviceCollection.AddTransient<InjectCemuConfigViewModel>();
        serviceCollection.AddTransient<InjectDaphneConfigViewModel>();
        serviceCollection.AddTransient<InjectDolphinConfigViewModel>();
        serviceCollection.AddTransient<InjectDuckStationConfigViewModel>();
        serviceCollection.AddTransient<InjectFlycastConfigViewModel>();
        serviceCollection.AddTransient<InjectMameConfigViewModel>();
        serviceCollection.AddTransient<InjectMednafenConfigViewModel>();
        serviceCollection.AddTransient<InjectMesenConfigViewModel>();
        serviceCollection.AddTransient<InjectPcsx2ConfigViewModel>();
        serviceCollection.AddTransient<InjectRaineConfigViewModel>();
        serviceCollection.AddTransient<InjectRedreamConfigViewModel>();
        serviceCollection.AddTransient<InjectRetroArchConfigViewModel>();
        serviceCollection.AddTransient<InjectRpcs3ConfigViewModel>();
        serviceCollection.AddTransient<InjectSegaModel2ConfigViewModel>();
        serviceCollection.AddTransient<InjectStellaConfigViewModel>();
        serviceCollection.AddTransient<InjectSupermodelConfigViewModel>();
        serviceCollection.AddTransient<InjectXeniaConfigViewModel>();
        serviceCollection.AddTransient<InjectYumirConfigViewModel>();

        // Windows
        serviceCollection.AddTransient<MainWindow>();
        serviceCollection.AddTransient<AboutWindow>();
        serviceCollection.AddTransient<ImageViewerWindow>();
        serviceCollection.AddTransient<FlashOverlayWindow>();
        serviceCollection.AddTransient<UpdateHistoryWindow>();
        serviceCollection.AddTransient<UpdateLogWindow>();
        serviceCollection.AddTransient<SetFuzzyMatchingWindow>();
        serviceCollection.AddTransient<DownloadImagePackWindow>();
        serviceCollection.AddTransient<EasyModeManager>();
        serviceCollection.AddTransient<EasyModeWindow>();
        serviceCollection.AddTransient<GlobalStatsWindow>();
        serviceCollection.AddTransient<RetroAchievementsWindow>();
        serviceCollection.AddTransient<RetroAchievementsForAGameWindow>();
        serviceCollection.AddTransient<InjectAresConfigWindow>();
        serviceCollection.AddTransient<InjectAzaharConfigWindow>();
        serviceCollection.AddTransient<InjectBlastemConfigWindow>();
        serviceCollection.AddTransient<InjectCemuConfigWindow>();
        serviceCollection.AddTransient<InjectDaphneConfigWindow>();
        serviceCollection.AddTransient<InjectDolphinConfigWindow>();
        serviceCollection.AddTransient<InjectDuckStationConfigWindow>();
        serviceCollection.AddTransient<InjectFlycastConfigWindow>();
        serviceCollection.AddTransient<InjectMameConfigWindow>();
        serviceCollection.AddTransient<InjectMednafenConfigWindow>();
        serviceCollection.AddTransient<InjectMesenConfigWindow>();
        serviceCollection.AddTransient<InjectPcsx2ConfigWindow>();
        serviceCollection.AddTransient<InjectRaineConfigWindow>();
        serviceCollection.AddTransient<InjectRedreamConfigWindow>();
        serviceCollection.AddTransient<InjectRetroArchConfigWindow>();
        serviceCollection.AddTransient<InjectRpcs3ConfigWindow>();
        serviceCollection.AddTransient<InjectSegaModel2ConfigWindow>();
        serviceCollection.AddTransient<InjectStellaConfigWindow>();
        serviceCollection.AddTransient<InjectSupermodelConfigWindow>();
        serviceCollection.AddTransient<InjectXeniaConfigWindow>();
        serviceCollection.AddTransient<InjectYumirConfigWindow>();
        serviceCollection.AddTransient<SoundConfigurationWindow>();
        serviceCollection.AddTransient<RetroAchievementsSettingsWindow>();
        serviceCollection.AddTransient<SetLinksWindow>();
        serviceCollection.AddTransient<SetGamepadDeadZoneWindow>();
        serviceCollection.AddTransient<RomHistoryWindow>();
        serviceCollection.AddTransient<DosBoxFileSelectionWindow>();
        serviceCollection.AddTransient<SystemSelectionWindow>();
        serviceCollection.AddTransient<WindowSelectionDialogWindow>();
        serviceCollection.AddTransient<SupportWindow>();

        // Handlers
        serviceCollection.AddSingleton<IEmulatorConfigHandler, AresConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, AzaharConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, BlastemConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, CemuConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, DaphneConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, DolphinConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, DuckStationConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, FlycastConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, MameConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, MednafenConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, MesenConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, Pcsx2ConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, RaineConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, RedreamConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, RetroArchConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, Rpcs3ConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, SegaModel2ConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, StellaConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, SupermodelConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, XeniaConfigHandler>();
        serviceCollection.AddSingleton<IEmulatorConfigHandler, YumirConfigHandler>();

        // Strategies
        serviceCollection.AddSingleton<ILaunchStrategy, ChdToCueStrategy>();
        serviceCollection.AddSingleton<ILaunchStrategy, ChdMountStrategy>();
        serviceCollection.AddSingleton<ILaunchStrategy, PbpToCueStrategy>();
        serviceCollection.AddSingleton<ILaunchStrategy, XisoMountStrategy>();
        serviceCollection.AddSingleton<ILaunchStrategy, ZipMountStrategy>();
        serviceCollection.AddSingleton<ILaunchStrategy, CommanderGeniusLaunchStrategy>();
        serviceCollection.AddSingleton<ILaunchStrategy, DosBoxLaunchStrategy>();
        serviceCollection.AddSingleton<ILaunchStrategy, DefaultLaunchStrategy>();

        // Detect if the application is running from a temporary extraction folder
        // (e.g., user double-clicked the .exe inside a ZIP/RAR archive)
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var tempDir = Path.GetTempPath();
        if (baseDir.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "Please extract the application first.\n\nIt looks like you are running SimpleLauncher from inside a ZIP or RAR archive.\n\nPlease extract the archive to a folder on your computer and run the application from there.",
                "SimpleLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        ServiceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        bugReportSink.Initialize(
            ServiceProvider.GetRequiredService<IHttpClientFactory>(),
            ServiceProvider.GetRequiredService<IConfiguration>(),
            ServiceProvider.GetRequiredService<IDeleteFilesService>(),
            appDataLogFolder);

        // --- Single Instance Check ---
        // The WPF and Avalonia apps share one named mutex (SingleInstance.MutexName), so
        // only one of them can run at a time; a second launch signals the running instance
        // to restore its window and exits.
        // Catch args
        var isRestarting = e.Args.Any(static arg => arg.Equals("--restarting", StringComparison.OrdinalIgnoreCase));
        var displayHistoryWindow =
            e.Args.Any(static arg => arg.Equals("-whatsnew", StringComparison.OrdinalIgnoreCase));

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

        if (!isRestarting) // Only perform the mutex check if NOT restarting
        {
            try
            {
                // Try to create or open the mutex
                // The 'out _isFirstInstance' parameter will be true if the mutex was created (first instance)
                // and false if it already existed (another instance is running).
                _singleInstanceMutex = new Mutex(true, SingleInstance.MutexName, out _isFirstInstance);
            }
            catch (AbandonedMutexException)
            {
                // The mutex was abandoned by a previous instance (e.g., due to a crash).
                // This means we successfully acquired it, and we are now the first instance.
                // The 'out _isFirstInstance' parameter would already be true in this case,
                // but we explicitly set it for clarity and to ensure the flow continues as a first instance.
                _isFirstInstance = true;
                Log.Logger.Debug(
                    "Mutex was abandoned by a previous instance, but successfully acquired by this instance. Proceeding as first instance");
                // No need to call ILogger.LogErrorAsync here, as it's not a critical error preventing startup,
                // but rather an informational event about a previous abnormal shutdown.
            }
            catch (UnauthorizedAccessException ex)
            {
                ServiceProvider.GetRequiredService<ILogger>()
                    .Error(ex, "Failed to create or acquire single instance mutex");
                ShowStartupFailureAndShutdown(ServiceProvider.GetRequiredService<IMessageBoxLibraryService>());
                return;
            }
            catch (IOException ex)
            {
                ServiceProvider.GetRequiredService<ILogger>()
                    .Error(ex, "Failed to create or acquire single instance mutex");
                ShowStartupFailureAndShutdown(ServiceProvider.GetRequiredService<IMessageBoxLibraryService>());
                return;
            }

            // After attempting to acquire the mutex (and handling AbandonedMutexException),
            // check if this is truly the first instance.
            if (!_isFirstInstance)
            {
                // Another instance is running. Signal it to restore its window and exit.
                try
                {
                    using var signal = EventWaitHandle.OpenExisting(SingleInstance.EventName);
                    signal.Set();
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    RestoreExistingWindow();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to signal existing instance");
                    RestoreExistingWindow();
                }

                _singleInstanceMutex?.Dispose();

                // WPF's Application.Shutdown() is not enough here: when it is called before
                // the dispatcher loop starts, RunDispatcher returns early and the process
                // lingers with no window and no message pump. A second launch must die hard
                // after signaling the running instance (WPF or Avalonia).
                Environment.Exit(0);
            }

            // Create the named event so future instances can signal us to restore the window
            try
            {
                _instanceSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstance.EventName);
                _ = Task.Run(InstanceSignalListener);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create instance signal event");
            }
        }
        // --- End Single Instance Check ---

        // One-time migration from the legacy files (favorites.dat, playhistory.dat,
        // settings.xml, system.xml) into the unified SQLite database (settings.dat in
        // AppData). Idempotent: does nothing when settings.dat is already valid. On
        // success legacy files are shelved as .bak; on failure they are left untouched
        // and the managers fall back to reading them. Runs before the first resolve of
        // SettingsManagerService so the loaded settings come from the database.
        try
        {
            WpfLegacyMigrator.EnsureMigrated(
                ServiceProvider.GetRequiredService<IConfiguration>(),
                ServiceProvider.GetRequiredService<ILogger>(),
                ServiceProvider.GetRequiredService<ICredentialProtector>());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Legacy settings migration failed; continuing with legacy files");
        }

        base.OnStartup(e);

        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Get the singleton SettingsManagerService instance
        var settingsManager = ServiceProvider.GetRequiredService<SettingsManagerService>();
        ApplyTheme(settingsManager.BaseTheme, settingsManager.AccentColor);
        // Command-line language override wins over the configured language.
        // Usage: SimpleLauncher.exe --language es   (or -language es / --language=es)
        var startupLanguage = ResolveStartupLanguage(e.Args, settingsManager.Language);
        ApplyLanguage(startupLanguage);

        // Manually create and show the MainWindow using DI
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        Current.MainWindow = mainWindow;
        mainWindow.Show();

        if (isDebugMode) DebugWindow.ShowDebugWindow();

        // Call ApplicationStats API on startup
        _ = Task.Run(async () =>
        {
            try
            {
                await ApplicationStats.CallApplicationStatsAsync(configuration,
                    ServiceProvider.GetRequiredService<ILogger>());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to call ApplicationStats API on startup");
            }
        });

        // Show UpdateHistoryWindow if -whatsnew argument is present
        // This is done after ensuring we're the single instance and after initialization
        if (displayHistoryWindow)
        {
            // Use Dispatcher.BeginInvoke to show the window after the main window is loaded
            Dispatcher.BeginInvoke(new Action(static () =>
            {
                try
                {
                    var updateHistoryWindow = ServiceProvider.GetRequiredService<UpdateHistoryWindow>();
                    updateHistoryWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    // Broad catch: a disposed provider or torn-down dispatcher raises
                    // InvalidOperationException/ObjectDisposedException, not SystemException
                    // (WPF-24). A failing -whatsnew dialog must never crash shutdown.
                    try
                    {
                        ServiceProvider.GetRequiredService<ILogger>().Error(ex,
                            "Error showing UpdateHistoryWindow with -whatsnew argument");
                    }
                    catch
                    {
                        // Provider itself is gone; nothing left to log to
                    }
                }
            }));
        }

        return;

        // Register IHttpClientFactory and named clients
        // Each client gets its own SocketsHttpHandler with explicit TLS 1.2/1.3 support
        static HttpMessageHandler CreateHttpHandler()
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
    }

    /// <summary>
    ///     Displays the failed-to-start message box and waits it to be dismissed before shutting the application down.
    /// </summary>
    /// <param name="messageBox">The message box library service.</param>
    private async void ShowStartupFailureAndShutdown(IMessageBoxLibraryService messageBox)
    {
        try
        {
            try
            {
                // Bound the dialog: if it can never complete, shutdown must still proceed (WPF-23).
                await messageBox.FailedToStartSimpleLauncherMessageBoxAsync()
                    .WaitAsync(TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show the failed-to-start message box");
            }

            _singleInstanceMutex?.Dispose();

            try
            {
                Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to shut down after startup failure");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show the failed-to-start message box");
        }
    }

    private static void ReportException(Exception ex, string contextMessage)
    {
        try
        {
            if (ex is COMException { HResult: unchecked((int)0x88980406) })
            {
                contextMessage =
                    $"[RenderingEngineFailure] {contextMessage} | HResult=0x88980406 (UCEERR_RENDERTHREADFAILURE). Commonly triggered by GPU driver issues or WPF per-pixel transparency.";
            }

            Log.Error(ex, contextMessage);
        }
        catch
        {
            // If even Serilog fails, fall back to debug output
            Debug.WriteLine($"[SimpleLauncher] {contextMessage}: {ex}");
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ??
                        new InvalidOperationException($"Unhandled non-exception object: {e.ExceptionObject}");
        ReportException(exception, "Unhandled AppDomain exception.");
    }

    private static void App_DispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        ReportException(e.Exception, "Unhandled dispatcher exception.");

        // Don't swallow critical exceptions that indicate memory corruption or resource exhaustion
        if (e.Exception is OutOfMemoryException or AccessViolationException or InvalidProgramException) return;

        e.Handled = true;
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ReportException(e.Exception, "Unhandled task exception.");
        e.SetObserved();
    }

    /// <summary>
    ///     Handles application exit, cleaning up gamepad resources, CHD mounter processes, and the single-instance mutex.
    /// </summary>
    /// <param name="e">The exit event arguments.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        // Cancel any running RetroAchievements hash scan and wait (bounded) so no
        // scan threads or CLI hashing processes are orphaned by the shutdown.
        try
        {
            ServiceProvider.GetService<IRetroAchievementsHashScanner>()
                ?.CancelScanAndWaitAsync(TimeSpan.FromSeconds(10))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            ServiceProvider.GetRequiredService<ILogger>()
                .Information(ex, "Failed to cancel the RetroAchievements hash scan on exit");
        }

        // Kill any lingering CHDMounter processes as a safety net
        try
        {
            ServiceProvider.GetRequiredService<IMountChdFiles>()
                .KillAllChdMounterProcesses(ServiceProvider.GetRequiredService<ILogger>());
        }
        catch (InvalidOperationException ex)
        {
            ServiceProvider.GetRequiredService<ILogger>()
                .Error(ex, "Failed to kill lingering CHDMounter processes on exit");
        }
        catch (SystemException ex)
        {
            ServiceProvider.GetRequiredService<ILogger>()
                .Error(ex, "Failed to kill lingering CHDMounter processes on exit");
        }

        try
        {
            var gamePadController = ServiceProvider.GetRequiredService<GamePadController>();

            // StopAsync normally returns an already-completed task because the stop
            // logic itself runs synchronously. On failure it returns a task that
            // shows a modal dialog on the UI thread. Blocking the UI thread with
            // .GetAwaiter().GetResult() while waiting for that dialog would deadlock
            // shutdown, so only log the fault and let the app exit.
            var stopTask = gamePadController.StopAsync();
            if (!stopTask.IsCompleted)
            {
                _ = stopTask.ContinueWith(static (t, state) =>
                {
                    if (t.IsFaulted)
                        (state as ILogger)?.Error(t.Exception, "Failed to stop the gamepad controller on exit");
                }, ServiceProvider.GetRequiredService<ILogger>(), TaskContinuationOptions.OnlyOnFaulted);
            }

            // Dispose gamepad resources
            gamePadController.Dispose();
        }
        catch (InvalidOperationException ex)
        {
            // Notify developer
            ServiceProvider.GetRequiredService<ILogger>().Error(ex, "Failed to dispose gamepad resources");
        }
        catch (SystemException ex)
        {
            // Notify developer
            ServiceProvider.GetRequiredService<ILogger>().Error(ex, "Failed to dispose gamepad resources");
        }

        // Release the mutex if this was the first instance and the mutex was successfully created
        // The new instance (started with --restarting) didn't acquire the mutex, so _isFirstInstance will be false,
        // and it won't try to release it.
        if (_isFirstInstance)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException ex)
            {
                // Notify developer
                ServiceProvider.GetRequiredService<ILogger>()
                    .Error(ex, "Failed to release single instance mutex on exit");
            }
            catch (ObjectDisposedException ex)
            {
                // Notify developer
                ServiceProvider.GetRequiredService<ILogger>()
                    .Error(ex, "Failed to release single instance mutex on exit");
            }
            catch (InvalidOperationException ex)
            {
                // Notify developer
                ServiceProvider.GetRequiredService<ILogger>()
                    .Error(ex, "Failed to release single instance mutex on exit");
            }
            finally
            {
                _singleInstanceMutex?.Dispose();
            }
        }

        DebugWindow.ShutdownWindow();
        Log.CloseAndFlush();
        Dispose();
        base.OnExit(e);
    }

    /// <summary>
    ///     Extracts a language code from the "--language"/"-language"/"--language=" launch argument.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The language code, or null when no language argument is present.</returns>
    internal static string? TryGetLanguageArg(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--language=", StringComparison.OrdinalIgnoreCase)) return arg["--language=".Length..];

            if (arg.Equals("--language", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-language", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    ///     Resolves the startup language: an explicit --language launch argument wins
    ///     (validated against the supported languages, case-insensitive), otherwise the
    ///     configured language is used. Unsupported argument codes fall back to English
    ///     directly (an expected user-input condition — logged at Information level).
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="configuredLanguage">The language from the settings.</param>
    /// <returns>The canonical language code to apply at startup.</returns>
    internal static string ResolveStartupLanguage(string[] args, string configuredLanguage)
    {
        var argLanguage = TryGetLanguageArg(args);
        if (!string.IsNullOrWhiteSpace(argLanguage))
        {
            var canonical = LanguageMenuService.NameToCode.Values
                .FirstOrDefault(code => string.Equals(code, argLanguage, StringComparison.OrdinalIgnoreCase));
            if (canonical is not null) return canonical;

            // Expected user input (e.g. --language zz): fall back to English without
            // going through ApplyLanguage, which would log an Error for the missing
            // resource and trigger a bug report.
            Log.Information("Unsupported language launch argument '{Language}'. Falling back to English", argLanguage);
            return "en";
        }

        return configuredLanguage;
    }

    /// <summary>
    ///     The language dictionary currently merged into <see cref="Application.Resources" />.
    ///     The JSON-built dictionary has no Source URI, so it is tracked here for replacement.
    /// </summary>
    private static ResourceDictionary? _currentLanguageDictionary;

    private static void ApplyLanguage(string languageCode)
    {
        try
        {
            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Build the dictionary for the selected language from the shared JSON pack
            // (SimpleLauncher.Core\Localization, embedded as the pack resource resources/strings.*.json).
            var resourceDictionary = BuildLanguageDictionary(languageCode);

            // Replace the previously applied language dictionary, if any.
            if (_currentLanguageDictionary != null)
                Current.Resources.MergedDictionaries.Remove(_currentLanguageDictionary);

            Current.Resources.MergedDictionaries.Add(resourceDictionary);
            _currentLanguageDictionary = resourceDictionary;
        }
        catch (Exception ex)
        {
            // Log the error using the LogErrorsService
            ServiceProvider.GetRequiredService<ILogger>().Error(ex, "Failed to Apply Language");

            // Fallback to English if loading the specified language fails
            if (!string.Equals(languageCode, "en", StringComparison.Ordinal))
            {
                try
                {
                    var fallbackDictionary = BuildLanguageDictionary("en");

                    if (_currentLanguageDictionary != null)
                        Current.Resources.MergedDictionaries.Remove(_currentLanguageDictionary);

                    Current.Resources.MergedDictionaries.Add(fallbackDictionary);
                    _currentLanguageDictionary = fallbackDictionary;
                }
                catch (Exception fallbackEx)
                {
                    // If even English fails, something is seriously wrong
                    ServiceProvider.GetRequiredService<ILogger>()
                        .Error(fallbackEx, "Failed to apply English as fallback language");
                }

                // Notify developer
                ServiceProvider.GetRequiredService<ILogger>()
                    .Warning("Fallback to English language resources due to initial culture error");
            }
        }
    }

    /// <summary>
    ///     Builds a WPF <see cref="ResourceDictionary" /> from the shared JSON localization pack
    ///     embedded as the pack resource <c>resources/strings.{language}.json</c>.
    /// </summary>
    private static ResourceDictionary BuildLanguageDictionary(string languageCode)
    {
        const string resourcePrefix = "resources/strings.";
        var assembly = typeof(App).Assembly;

        using var bundleStream = assembly.GetManifestResourceStream("SimpleLauncher.g.resources")
                                 ?? throw new FileNotFoundException(
                                     "SimpleLauncher.g.resources not found. Check SimpleLauncher.csproj: the shared localization " +
                                     "packs need a <Resource Include=\"..\\SimpleLauncher.Core\\Localization\\strings.*.json\" /> entry.");

        using var reader = new ResourceReader(bundleStream);

        // Settings may store WPF-style codes ('pt-br', 'zh-hans') while the shared files use the
        // canonical casing ('pt-BR', 'zh-Hans'). Resolve case-insensitively, like the Avalonia app.
        var expectedFileName = $"{languageCode}.json";
        object? resourceValue = null;
        string? resourceName = null;
        foreach (DictionaryEntry entry in reader)
        {
            if (entry.Key is not string name)
                continue;

            if (!name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(name[resourcePrefix.Length..], expectedFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resourceValue = entry.Value;
            resourceName = name;
            break;
        }

        if (resourceName == null)
            throw new FileNotFoundException(
                $"Embedded localization resource not found for language '{languageCode}' in SimpleLauncher.g.resources.");

        var json = resourceValue switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            Stream stream => ReadAllText(stream),
            string text => text,
            var other => throw new InvalidDataException(
                $"Localization resource '{resourceName}' has unexpected type '{other?.GetType().Name}'.")
        };

        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (strings == null)
            throw new InvalidDataException($"Localization resource '{resourceName}' contains no JSON object.");

        var dictionary = new ResourceDictionary();
        foreach (var (key, value) in strings) dictionary[key] = value;

        return dictionary;
    }

    private static string ReadAllText(Stream stream)
    {
        using var textReader = new StreamReader(stream, Encoding.UTF8);
        return textReader.ReadToEnd();
    }

    private static void ApplyTheme(string baseTheme, string accentColor)
    {
        try
        {
            // Handle Theme Sync Mode (Adaptive)
            ThemeManager.Current.ThemeSyncMode = string.Equals(baseTheme, "Adaptive", StringComparison.Ordinal)
                ? ThemeSyncMode.SyncAll
                : ThemeSyncMode.DoNotSync;
            switch (baseTheme)
            {
                case "Adaptive":
                    ThemeManager.Current.SyncTheme();
                    return;
                // Handle High Contrast
                case "HighContrast":
                {
                    InternalChangeTheme(Current, "Dark", accentColor);
                    ApplyCustomThemeOverride("Theme.HighContrast.xaml");
                    return;
                }
                // Handle Custom Theme (Midnight)
                case "Midnight":
                {
                    InternalChangeTheme(Current, "Dark", accentColor);
                    ApplyCustomThemeOverride("Theme.Midnight.xaml");
                    return;
                }
                default:
                    // Standard Themes (Light/Dark)
                    RemoveCustomThemeOverrides();
                    InternalChangeTheme(Current, baseTheme, accentColor);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Failed to Apply Theme.";
            ServiceProvider.GetRequiredService<ILogger>().Error(ex, contextMessage);
        }
    }

    private static void InternalChangeTheme(object target, string baseTheme, string accentColor)
    {
        baseTheme = string.IsNullOrWhiteSpace(baseTheme) ? "Dark" : baseTheme;
        accentColor = string.IsNullOrWhiteSpace(accentColor) ? "Blue" : accentColor;

        if (IsCustomAccent(accentColor))
        {
            var color = GetColorForAccent(accentColor);
            var theme = new Theme(
                $"{baseTheme}.{accentColor}",
                $"{baseTheme} ({accentColor})",
                baseTheme,
                accentColor,
                color,
                new SolidColorBrush(color),
                true,
                false
            );

            switch (target)
            {
                case Application app:
                    ThemeManager.Current.ChangeTheme(app, theme);
                    break;
                case Window win:
                    ThemeManager.Current.ChangeTheme(win, theme);
                    break;
            }
        }
        else
        {
            switch (target)
            {
                case Application app:
                    ThemeManager.Current.ChangeTheme(app, $"{baseTheme}.{accentColor}");
                    break;
                case Window win:
                    ThemeManager.Current.ChangeTheme(win, $"{baseTheme}.{accentColor}");
                    break;
            }
        }
    }

    private static void ApplyCustomThemeOverride(string fileName)
    {
        try
        {
            RemoveCustomThemeOverrides();
            var resourceDictionary = new ResourceDictionary
            {
                Source = new Uri($"/SimpleLauncher;component/resources2/{fileName}", UriKind.Relative)
            };
            Current.Resources.MergedDictionaries.Add(resourceDictionary);
        }
        catch (Exception ex)
        {
            ServiceProvider.GetRequiredService<ILogger>()
                .Error(ex, $"Failed to apply custom theme override: {fileName}");
        }
    }

    private static void RemoveCustomThemeOverrides()
    {
        var customThemes = Current.Resources.MergedDictionaries
            .Where(static d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("Theme.HighContrast.xaml", StringComparison.Ordinal) ||
                 d.Source.OriginalString.Contains("Theme.Midnight.xaml", StringComparison.Ordinal)))
            .ToList();

        foreach (var dict in customThemes) Current.Resources.MergedDictionaries.Remove(dict);
    }

    private static void ApplyCustomThemeOverrideToWindow(Window window, string fileName)
    {
        try
        {
            RemoveCustomThemeOverridesFromWindow(window);
            var resourceDictionary = new ResourceDictionary
            {
                Source = new Uri($"/SimpleLauncher;component/resources2/{fileName}", UriKind.Relative)
            };
            window.Resources.MergedDictionaries.Add(resourceDictionary);
        }
        catch (Exception ex)
        {
            ServiceProvider.GetRequiredService<ILogger>().Error(ex,
                $"Failed to apply custom theme override to window {window.GetType().Name}: {fileName}");
        }
    }

    private static void RemoveCustomThemeOverridesFromWindow(Window window)
    {
        var customThemes = window.Resources.MergedDictionaries
            .Where(static d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("Theme.HighContrast.xaml", StringComparison.Ordinal) ||
                 d.Source.OriginalString.Contains("Theme.Midnight.xaml", StringComparison.Ordinal)))
            .ToList();

        foreach (var dict in customThemes) window.Resources.MergedDictionaries.Remove(dict);
    }

    /// <summary>
    ///     Applies the current theme to the specified window based on application settings.
    /// </summary>
    /// <param name="window">The window to apply the theme to.</param>
    public static void ApplyThemeToWindow(Window window)
    {
        // Get the singleton SettingsManagerService instance
        var settings = ServiceProvider.GetRequiredService<SettingsManagerService>();
        var baseTheme = settings.BaseTheme;
        var accentColor = settings.AccentColor;
        try
        {
            switch (baseTheme)
            {
                case "Adaptive":
                    var detectedTheme = ThemeManager.Current.DetectTheme();
                    if (detectedTheme != null) ThemeManager.Current.ChangeTheme(window, detectedTheme);

                    return;
                case "HighContrast":
                {
                    InternalChangeTheme(window, "Dark", accentColor);
                    ApplyCustomThemeOverrideToWindow(window, "Theme.HighContrast.xaml");
                    return;
                }
                case "Midnight":
                {
                    InternalChangeTheme(window, "Dark", accentColor);
                    ApplyCustomThemeOverrideToWindow(window, "Theme.Midnight.xaml");
                    return;
                }
                default:
                    RemoveCustomThemeOverridesFromWindow(window);
                    InternalChangeTheme(window, baseTheme, accentColor);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            ServiceProvider.GetRequiredService<ILogger>()
                .Error(ex, $"Failed to apply theme to window {window.GetType().Name}.");
        }
    }

    private static bool IsCustomAccent(string accentColor)
    {
        return accentColor switch
        {
            "Maroon" or "OliveDrab" or "Plum" or "SkyBlue" => true,
            _ => false
        };
    }

    private static Color GetColorForAccent(string accentColor)
    {
        return accentColor switch
        {
            "Maroon" => Colors.Maroon,
            "OliveDrab" => Colors.OliveDrab,
            "Plum" => Colors.Plum,
            "SkyBlue" => Colors.SkyBlue,
            _ => Colors.Blue // Default fallback
        };
    }

    /// <summary>
    ///     Changes the application theme and applies it to all open windows.
    /// </summary>
    /// <param name="baseTheme">The base theme name (e.g., "Light", "Dark", "Adaptive").</param>
    /// <param name="accentColor">The accent color name.</param>
    public static void ChangeTheme(string baseTheme, string accentColor)
    {
        // Get the singleton SettingsManagerService instance
        var settings = ServiceProvider.GetRequiredService<SettingsManagerService>();
        settings.BaseTheme = baseTheme;
        settings.AccentColor = accentColor;
        _ = settings.SaveAsync();

        ApplyTheme(baseTheme, accentColor);

        // Apply theme to all currently open windows
        foreach (Window window in Current.Windows) ApplyThemeToWindow(window);

        Log.Logger.Debug("Theme has been applied to all windows");
        Log.Logger.Debug($"Saved theme settings: {baseTheme}.{accentColor}");
    }

    private void InstanceSignalListener()
    {
        while (true)
        {
            var signal = _instanceSignal;

            try
            {
                signal.WaitOne();
                Dispatcher.Invoke(static () =>
                {
                    if (Current.MainWindow is null) return;

                    Current.MainWindow.ShowInTaskbar = true;
                    Current.MainWindow.Show();
                    Current.MainWindow.WindowState = WindowState.Normal;
                    Current.MainWindow.Activate();
                });
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in instance signal listener");
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private static void RestoreExistingWindow()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
            {
                if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    var hWnd = process.MainWindowHandle;
                    if (IsIconic(hWnd))
                        ShowWindow(hWnd, SwRestore);
                    SetForegroundWindow(hWnd);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restore existing SimpleLauncher window");
        }
    }
}