using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;
using CheckDirWritable = SimpleLauncher.Core.Services.CheckIfDirectoryIsWritable.CheckIfDirectoryIsWritableService;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed games from various digital storefronts (Steam, Epic, GOG, etc.)
///     and creates shortcuts in the Microsoft Windows system folder.
/// </summary>
public class GameScannerService
{
    /// <summary>Name of the system entry created for storefront-discovered PC games.</summary>
    public const string WindowsSystemName = "Microsoft Windows";

    /// <summary>
    ///     Names of storefront titles that should not be scanned as games.
    /// </summary>
    internal static readonly HashSet<string> IgnoredGameNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Steamworks Common Redistributables",
        "Unreal Engine",
        "Fab UE Plugin",
        "Quixel Bridge",
        "DirectX",
        "Google Earth VR",
        "Spacewar",
        "PC Health Check",
        "Rockstar Games Launcher",
        "Battle.net",
        "Ubisoft Connect"
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIconExtractor _iconExtractor;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly IEnumerable<IGamePlatformScanner> _scanners;
    private readonly SystemManagerService _systemManager;

    private bool _timeoutMessageShown;
    private string _windowsImagesPath = null!;

    private string _windowsRomsPath = null!;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GameScannerService" /> class.
    /// </summary>
    /// <param name="messageBoxLibrary">The message box service for user notifications.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="httpClientFactory">The HTTP client factory for API requests.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="scanners">The collection of platform-specific game scanners.</param>
    /// <param name="iconExtractor">The icon extractor for extracting icons from executables.</param>
    /// <param name="systemManager">The system manager used to read/write system.xml.</param>
    public GameScannerService(IMessageBoxLibraryService messageBoxLibrary, IConfiguration configuration,
        IHttpClientFactory httpClientFactory, ILogger logger, IEnumerable<IGamePlatformScanner> scanners,
        IIconExtractor iconExtractor, SystemManagerService systemManager)
    {
        _logger = logger;
        _messageBoxLibrary = messageBoxLibrary;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _scanners = scanners;
        _iconExtractor = iconExtractor;
        _systemManager = systemManager;
    }

    /// <summary>
    ///     Gets a value indicating whether a new 'Microsoft Windows' system was created during the scan.
    /// </summary>
    internal bool WasNewSystemCreated { get; private set; }

    /// <summary>
    ///     Scans all registered storefront scanners and creates shortcuts for the games they find.
    ///     Storefront scanning reads the Windows registry and storefront install databases; it is a
    ///     no-op on non-Windows platforms.
    /// </summary>
    public async Task<StorefrontScanResult> ScanForStoreGamesAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.Debug("[GameScannerService] Storefront scanning is Windows-only. Skipping");
            return new StorefrontScanResult(0, 0, false);
        }

        return await ScanForStoreGamesCoreAsync();
    }

    /// <summary>
    ///     Platform-agnostic core of <see cref="ScanForStoreGamesAsync" /> (no Windows gate) so the
    ///     orchestration is testable on any OS.
    /// </summary>
    internal async Task<StorefrontScanResult> ScanForStoreGamesCoreAsync()
    {
        try
        {
            // Initialize paths based on existing system configuration or create default
            var pathResult = await InitializeWindowsPathsAsync();
            _windowsRomsPath = pathResult.RomsPath ?? "";
            _windowsImagesPath = pathResult.ImagesPath ?? "";
            WasNewSystemCreated = pathResult.WasNewSystemCreated;

            // Ensure the target directories exist before scanners write shortcuts/images.
            // The ROMs directory can be missing when the app runs from a protected location
            // (e.g. Program Files) or after the user deletes it. Best-effort create it and
            // verify it is writable; when it is not, tell the user to move the application
            // to a writable path instead of silently failing to create every shortcut
            // (see bugs 66182-66188).
            var romsWritable = TryEnsureDirectory(_windowsRomsPath, "ROMs", _logger) &&
                               CheckDirWritable.IsWritableDirectory(_windowsRomsPath, _logger);
            var imagesReady = TryEnsureDirectory(_windowsImagesPath, "images", _logger);

            if (!romsWritable || !imagesReady)
                await _messageBoxLibrary.MoveToWritableFolderMessageBoxAsync();

            if (!romsWritable) return new StorefrontScanResult(0, 0, WasNewSystemCreated);

            var shortcutsBefore = EnumerateShortcutFiles(_windowsRomsPath);

            var tasks = _scanners
                .Select(s => s.ScanAsync(this, _logger, _windowsRomsPath, _windowsImagesPath, IgnoredGameNames))
                .ToList();

            await Task.WhenAll(tasks);

            var shortcutsAfter = EnumerateShortcutFiles(_windowsRomsPath);
            var shortcutsCreated = shortcutsAfter.Except(shortcutsBefore, StringComparer.OrdinalIgnoreCase).Count();

            _logger.Debug("[GameScannerService] All store game scans completed");

            return new StorefrontScanResult(shortcutsCreated, shortcutsCreated, WasNewSystemCreated);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred during the game scanning process");
            return new StorefrontScanResult(0, 0, WasNewSystemCreated);
        }
    }

    /// <summary>
    ///     Ensures a directory used by the store-game scanners exists, creating it best-effort.
    /// </summary>
    /// <param name="path">The directory to create.</param>
    /// <param name="kind">A short label used for logging (e.g. "ROMs" or "images").</param>
    /// <param name="logger">The logger used to record expected creation failures at Information level.</param>
    /// <returns>True when the directory exists or was created; false when creation failed.</returns>
    private static bool TryEnsureDirectory(string path, string kind, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Expected condition: app is in a protected directory (e.g. Program Files) or the
            // path points to an unavailable drive. Log at Information level so the bug report
            // API does not pick it up (see bugs 66182-66188).
            logger.Information(ex,
                "Cannot create the '{Kind}' directory '{Path}' for the 'Microsoft Windows' system. Store-game shortcuts will not be created",
                kind, path);
            return false;
        }
    }

    private static HashSet<string> EnumerateShortcutFiles(string romsPath)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(romsPath) || !Directory.Exists(romsPath)) return files;

        try
        {
            foreach (var file in Directory.GetFiles(romsPath, "*.url"))
                files.Add(Path.GetFileName(file));
            foreach (var file in Directory.GetFiles(romsPath, "*.bat"))
                files.Add(Path.GetFileName(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Folder vanished or became inaccessible between the existence check and the enumeration
        }

        return files;
    }

    private async Task<(string? RomsPath, string? ImagesPath, bool WasNewSystemCreated)> InitializeWindowsPathsAsync()
    {
        try
        {
            // Check if the system already exists
            var existingSystems = _systemManager.LoadSystems();
            var existingWindowsSystem = existingSystems.FirstOrDefault(static s =>
                s.SystemName.Equals(WindowsSystemName, StringComparison.OrdinalIgnoreCase));

            if (existingWindowsSystem != null)
            {
                // Use existing paths from the system configuration
                var existingRomsPath = existingWindowsSystem.PrimarySystemFolder;
                var existingImagesPath = existingWindowsSystem.SystemImageFolder;

                // Resolve the paths (handle %BASEFOLDER% placeholder)
                var resolvedRomsPath = PathHelper.ResolveRelativeToAppDirectory(existingRomsPath);
                var resolvedImagesPath = PathHelper.ResolveRelativeToAppDirectory(existingImagesPath);

                _logger.Debug(
                    $"[GameScannerService] Using existing '{WindowsSystemName}' system paths: ROMs='{resolvedRomsPath}', Images='{resolvedImagesPath}'");

                return (resolvedRomsPath, resolvedImagesPath, false);
            }

            // System doesn't exist, create it with default paths
            _logger.Debug($"[GameScannerService] '{WindowsSystemName}' system not found. Creating it now.");

            var defaultRomsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roms", WindowsSystemName);
            var defaultImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", WindowsSystemName);

            await SystemManagerService.SaveSystemConfigurationAsync(
                WindowsSystemName,
                [$@"%BASEFOLDER%\roms\{WindowsSystemName}"],
                $@"%BASEFOLDER%\images\{WindowsSystemName}",
                ["url", "lnk", "bat"],
                [],
                false,
                new Emulator
                {
                    EmulatorName = "Direct Launch",
                    EmulatorLocation = "",
                    EmulatorParameters = "",
                    ReceiveANotificationOnEmulatorError = true
                },
                WindowsSystemName,
                _configuration,
                _logger,
                _systemManager);

            // The SystemManagerService cache is stale now — subsequent scans must see
            // the entry as existing instead of re-creating it.
            _systemManager.InvalidateCache();

            // Create the necessary directories
            Directory.CreateDirectory(defaultRomsPath);
            Directory.CreateDirectory(defaultImagesPath);

            _logger.Debug(
                $"[GameScannerService] Created new '{WindowsSystemName}' system with default paths: ROMs='{defaultRomsPath}', Images='{defaultImagesPath}'");

            return (defaultRomsPath, defaultImagesPath, true);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Expected condition: app is in a protected directory (e.g. Program Files).
            // Log at Information level so the bug report API does not pick it up.
            _logger.Information(ex,
                "Cannot create 'Microsoft Windows' system directories in protected location. Falling back to default paths");

            // Fall back to default paths even on error
            var fallbackRomsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roms", WindowsSystemName);
            var fallbackImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", WindowsSystemName);

            return (fallbackRomsPath, fallbackImagesPath, false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize 'Microsoft Windows' system paths");

            // Fall back to default paths even on error
            var fallbackRomsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roms", WindowsSystemName);
            var fallbackImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", WindowsSystemName);

            return (fallbackRomsPath, fallbackImagesPath, false);
        }
    }

    /// <summary>
    ///     Attempts to download a cover image for the given game name from the image API,
    ///     retrying once after a delay on timeout or network errors.
    /// </summary>
    /// <param name="gameName">The name of the game to search for.</param>
    /// <param name="destinationPath">The file path where the downloaded image should be saved.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <returns>True if the image was downloaded successfully; otherwise, false.</returns>
    internal async Task<bool> TryDownloadImageFromApiAsync(string gameName, string destinationPath, ILogger logErrors)
    {
        if (string.IsNullOrWhiteSpace(gameName)) return false;

        // Try up to 2 times (initial attempt + 1 retry after 5 seconds)
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("GameImageClient");

                var encodedGameName = WebUtility.UrlEncode(gameName);
                var response = await client.GetAsync($"api/v1/games/search?name={encodedGameName}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode != HttpStatusCode.NotFound)
                    {
                        _logger.Debug(
                            $"[GameScannerService] API query for '{gameName}' failed with status: {response.StatusCode}");
                    }

                    return false;
                }

                await using var jsonStream = await response.Content.ReadAsStreamAsync();
                var apiResponse = await JsonSerializer.DeserializeAsync<GameImageApiResponse>(jsonStream);

                if (apiResponse is { Success: true } &&
                    Uri.IsWellFormedUriString(apiResponse.ImageUrl, UriKind.Absolute))
                {
                    // HttpClient supports absolute URLs directly, even when BaseAddress is configured
                    var imageBytes = await client.GetByteArrayAsync(apiResponse.ImageUrl);
                    await File.WriteAllBytesAsync(destinationPath, imageBytes);
                    _logger.Debug($"[GameScannerService] Successfully downloaded image for '{gameName}' from API.");
                    return true;
                }
            }
            catch (OperationCanceledException) when (attempt == 0)
            {
                // Timeout on first attempt - wait and retry
                _logger.Debug(
                    $"[GameScannerService] Image download timeout for '{gameName}', retrying in 5 seconds...");
                await Task.Delay(5000);
            }
            catch (HttpRequestException ex) when (attempt == 0)
            {
                // Network error on first attempt - wait and retry
                var innerMessage = ex.InnerException?.Message ?? "none";
                _logger.Debug(
                    $"[GameScannerService] Image download network error for '{gameName}': {ex.Message}. Inner: {innerMessage}. Retrying in 5 seconds...");
                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                // On second attempt or unexpected errors, fail silently and let the caller fall back to icon extraction
                var errorType = ex switch
                {
                    OperationCanceledException => "timeout",
                    HttpRequestException => "network error",
                    _ => "error"
                };
                var innerDetails = GetInnerExceptionDetails(ex);
                var logMessage =
                    $"[GameScannerService] Image download failed for '{gameName}' after retry ({errorType}: {ex.Message}).{innerDetails} Falling back to icon extraction.";
                _logger.Debug(logMessage);

                // Log persistent network errors at Information level: expected condition (flaky
                // network / slow API) with fallback to icon extraction and a user message box —
                // not a bug report.
                if (ex is HttpRequestException or OperationCanceledException)
                {
                    logErrors?.Information(ex, $"Failed to download image for '{gameName}' from API after retry.");

                    // Show message box for timeout/network errors on final attempt (attempt == 1)
                    if (attempt == 1 && !_timeoutMessageShown)
                    {
                        _timeoutMessageShown = true;
                        await _messageBoxLibrary.ShowImageDownloadTimeoutMessageBoxAsync();
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Finds or downloads a cover image for a scanned game, falling back to icon extraction from its executable.
    /// </summary>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="originalGameName">The original storefront name of the game.</param>
    /// <param name="gameInstallPath">The installation directory of the game.</param>
    /// <param name="sanitizedGameName">The sanitized name used for the image file.</param>
    /// <param name="windowsImagesPath">The directory where game images are stored.</param>
    /// <param name="specificExePath">Optional specific executable path to extract the icon from.</param>
    internal async Task FindAndSaveGameImageAsync(ILogger logErrors, string originalGameName, string gameInstallPath,
        string sanitizedGameName, string windowsImagesPath, string? specificExePath = null)
    {
        try
        {
            var imagePath = Path.Combine(windowsImagesPath, $"{sanitizedGameName}.png");
            if (File.Exists(imagePath)) return;

            // 1. Try to download from API
            if (await TryDownloadImageFromApiAsync(originalGameName, imagePath, logErrors)) return;

            // 2. Fallback to extracting icon from EXE
            var mainExe = FindMainExecutable(gameInstallPath, sanitizedGameName, specificExePath);
            if (mainExe != null) _iconExtractor.SaveIconFromExe(mainExe, imagePath, logErrors);
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, $"Failed to find/save image for {sanitizedGameName} in {gameInstallPath}");
        }
    }

    /// <summary>
    ///     Final fallback that extracts the icon from the game's executable when no cover image can be found or downloaded.
    /// </summary>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="gameInstallPath">The installation directory of the game.</param>
    /// <param name="sanitizedGameName">The sanitized name used for the image file.</param>
    /// <param name="windowsImagesPath">The directory where game images are stored.</param>
    /// <param name="specificExePath">Optional specific executable path to extract the icon from.</param>
    internal Task ExtractIconFromGameFolderAsync(ILogger logErrors, string gameInstallPath, string sanitizedGameName,
        string windowsImagesPath, string? specificExePath = null)
    {
        try
        {
            try
            {
                var iconPath = Path.Combine(windowsImagesPath, $"{sanitizedGameName}.png");
                if (File.Exists(iconPath)) return Task.CompletedTask;

                var mainExe = FindMainExecutable(gameInstallPath, sanitizedGameName, specificExePath);
                if (mainExe != null) _iconExtractor.SaveIconFromExe(mainExe, iconPath, logErrors);
            }
            catch (Exception ex)
            {
                // Missing/inaccessible install folder (e.g., protected Microsoft Store package
                // folders) is an expected fallback condition — log at Debug so it does not
                // generate bug reports (see bug 61956).
                logErrors.Debug(ex, $"Failed to extract icon for {sanitizedGameName} in {gameInstallPath}");
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private static string? FindMainExecutable(string gameInstallPath, string sanitizedGameName,
        string? specificExePath = null)
    {
        if (!Directory.Exists(gameInstallPath)) return null;

        // 1. Use the specific path if provided and it exists.
        if (!string.IsNullOrEmpty(specificExePath) && File.Exists(specificExePath)) return specificExePath;

        // 2. Heuristics to find the main EXE
        var exeFiles = TryGetExeFiles(gameInstallPath);
        if (exeFiles is not { Length: > 0 }) return null;

        // 2a. Name match
        var mainExe = exeFiles.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Equals(sanitizedGameName, StringComparison.OrdinalIgnoreCase));
        if (mainExe != null) return mainExe;

        // 2b. Contains name
        mainExe = exeFiles.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Contains(sanitizedGameName, StringComparison.OrdinalIgnoreCase));
        if (mainExe != null) return mainExe;

        // 2c. Largest EXE (ignoring common non-game executables)
        return exeFiles
            .Where(static f => !f.Contains("unins", StringComparison.OrdinalIgnoreCase) &&
                               !f.Contains("setup", StringComparison.OrdinalIgnoreCase) &&
                               !f.Contains("crash", StringComparison.OrdinalIgnoreCase) &&
                               !f.Contains("redist", StringComparison.OrdinalIgnoreCase) &&
                               !f.Contains("dxsetup", StringComparison.OrdinalIgnoreCase) &&
                               !f.Contains("update", StringComparison.OrdinalIgnoreCase) &&
                               !f.Contains("unity", StringComparison.OrdinalIgnoreCase) &&
                               !f.Contains("launcher", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static f =>
            {
                try
                {
                    var longPath = PathHelper.GetLongPath(f);
                    return longPath != null ? new FileInfo(longPath).Length : 0L;
                }
                catch
                {
                    return 0L;
                }
            })
            .FirstOrDefault();
    }

    /// <summary>
    ///     Enumerates the executable files in a game folder, returning null if the folder vanished
    ///     or became inaccessible between the <see cref="Directory.Exists" /> check and the enumeration.
    ///     This is a real race for Microsoft Store package folders, which are routinely removed and
    ///     recreated while the Store stages, updates, or uninstalls an app (see bug 61956).
    /// </summary>
    private static string[]? TryGetExeFiles(string gameInstallPath)
    {
        try
        {
            return Directory.GetFiles(gameInstallPath, "*.exe", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // DirectoryNotFound/FileNotFound derive from IOException; UnauthorizedAccessException
            // covers ACL-protected folders such as C:\Program Files\WindowsApps.
            return null;
        }
    }

    private static string GetInnerExceptionDetails(Exception ex)
    {
        var inner = ex.InnerException;
        if (inner == null) return "";

        var details = " Inner exceptions:";
        var current = inner;
        for (var depth = 1; current != null && depth <= 3; depth++)
        {
            details += $" [{depth}] {current.GetType().Name}: {current.Message}";
            current = current.InnerException;
        }

        return details;
    }
}