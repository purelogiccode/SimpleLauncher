using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.EasyMode;

/// <summary>
///     Manages EasyMode system configuration by loading emulator definitions from local XML,
///     a remote API, or a fallback URL. Supports XML serialization for persistence.
/// </summary>
[XmlRoot("EasyMode")]
public class EasyModeManager : IDisposable
{
    private const int DefaultCacheDurationMinutes = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Session cache for API data (in-memory, per application instance)
    private static (EasyModeManager Manager, DateTime Timestamp) _apiCache;
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private readonly IConfiguration _configuration = null!;
    private readonly IHttpClientFactory _httpClientFactory = null!;

    private readonly ILogger _logger = null!;

    /// <summary>
    ///     Initializes a new instance of <see cref="EasyModeManager" /> with the specified dependencies for API loading and
    ///     error logging.
    /// </summary>
    public EasyModeManager(ILogger logErrors, IConfiguration configuration, IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Initializes a new instance of <see cref="EasyModeManager" /> with default settings (used for XML deserialization).
    /// </summary>
    public EasyModeManager()
    {
    }

    /// <summary>
    ///     Gets or sets the list of EasyMode system configurations.
    /// </summary>
    [XmlElement("EasyModeSystemConfig")]
    public List<EasyModeSystemConfig> Systems { get; set; } = null!;

    /// <summary>
    ///     Releases resources used by this <see cref="EasyModeManager" />.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets the platform configuration identifier used to select the EasyMode configuration
    ///     (API route segment, local XML file name, and fallback URL configuration key).
    ///     Windows keeps the legacy "x64"/"arm64" identifiers for backward compatibility with
    ///     the existing API data; Linux uses "linux-x64"/"linux-arm64"; macOS uses
    ///     "macos-x64"/"macos-arm64"; every other platform keeps the legacy "x64" default.
    /// </summary>
    private static string GetPlatformConfigurationId()
    {
        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "macos-arm64" : "macos-x64";
        }

        if (OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            return "arm64";
        }

        return "x64"; // Legacy default (win-x64 configuration)
    }

    /// <summary>
    ///     Gets the local EasyMode XML file name for the current platform configuration.
    /// </summary>
    private static string GetLocalXmlFileName()
    {
        return GetPlatformConfigurationId() switch
        {
            "arm64" => "easymode_arm64.xml",
            "linux-x64" => "easymode_linux_x64.xml",
            "linux-arm64" => "easymode_linux_arm64.xml",
            "macos-x64" => "easymode_macos_x64.xml",
            "macos-arm64" => "easymode_macos_arm64.xml",
            _ => "easymode.xml"
        };
    }

    /// <summary>
    ///     Gets the configuration key of the fallback XML URL for the current platform configuration.
    /// </summary>
    private static string GetFallbackUrlConfigurationKey()
    {
        return GetPlatformConfigurationId() switch
        {
            "arm64" => "Urls:EasyModeFallbackXmlArm64",
            "linux-x64" => "Urls:EasyModeFallbackXmlLinuxX64",
            "linux-arm64" => "Urls:EasyModeFallbackXmlLinuxArm64",
            "macos-x64" => "Urls:EasyModeFallbackXmlMacosX64",
            "macos-arm64" => "Urls:EasyModeFallbackXmlMacosArm64",
            _ => "Urls:EasyModeFallbackXmlX64"
        };
    }

    /// <summary>
    ///     Asynchronously loads the EasyMode configuration. It first tries to load from a local XML file.
    ///     If the file is not found or is empty, it falls back to loading from the web API.
    ///     If the API also fails, it attempts to download from a fallback XML URL.
    /// </summary>
    /// <returns>An EasyModeManager instance if successful, otherwise null.</returns>
    public async Task<EasyModeManager?> LoadAsync()
    {
        // Try loading from XML first
        var manager = LoadFromXml(_logger);
        if (manager != null && manager.Systems.Count != 0)
        {
            _logger.Debug("Loaded EasyMode configuration from local XML file");
            return manager;
        }

        // If XML fails or is empty, try loading from the API
        _logger.Debug("Local EasyMode XML not found or is empty. Attempting to load from API");
        manager = await LoadFromApiAsync();
        if (manager != null && manager.Systems.Count != 0)
        {
            _logger.Debug("Successfully loaded EasyMode configuration from API");
            return manager;
        }

        // If both local XML and API fail, try loading from fallback URL
        _logger.Debug("API load failed. Attempting to load from fallback XML URL");
        manager = await LoadFromFallbackAsync();
        if (manager != null && manager.Systems.Count != 0)
        {
            _logger.Debug("Successfully loaded EasyMode configuration from fallback URL");
            return manager;
        }

        _logger.Debug("Failed to load EasyMode configuration from all sources (local XML, API, and fallback URL)");
        return null; // Return null if all methods fail
    }

    private static EasyModeManager? LoadFromXml(ILogger logErrors)
    {
        // Determine the XML file based on the platform configuration (OS + architecture)
        var xmlFile = GetLocalXmlFileName();

        var xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, xmlFile);

        // Check if xmlFile exists before proceeding.
        if (!File.Exists(xmlFilePath)) return null; // File not found, which is an expected scenario for API fallback.

        try
        {
            var serializer = new XmlSerializer(typeof(EasyModeManager));

            // Open the file
            using var fileStream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Create XmlReaderSettings to disable DTD processing and set XmlResolver to null
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            // Create XmlReader with the settings
            using var xmlReader = XmlReader.Create(fileStream, settings);

            // Validate configuration if not null.
            if (serializer.Deserialize(xmlReader) is EasyModeManager config)
            {
                config.Validate(); // Exclude invalid systems
                return config;
            }

            return null;
        }
        catch (Exception ex)
        {
            // If the file exists but is corrupt, we log it but still return null to allow API fallback.
            var contextMessage = $"The file '{xmlFile}' could not be loaded. It might be corrupted.";
            logErrors.Error(ex, contextMessage);
            return null;
        }
    }

    private async Task<EasyModeManager?> LoadFromApiAsync()
    {
        // Get cache duration from configuration (default to 60 minutes)
        var cacheDurationMinutes =
            _configuration.GetValue("EasyModeCacheDurationMinutes", DefaultCacheDurationMinutes);

        // Fast path: serve valid cached data while holding the lock only for the check.
        await CacheLock.WaitAsync();
        try
        {
            // Check if we have valid cached data
            if (_apiCache.Manager != null &&
                DateTime.UtcNow - _apiCache.Timestamp < TimeSpan.FromMinutes(cacheDurationMinutes))
            {
                _logger.Debug(
                    $"Returning EasyMode configuration from session cache (valid for {cacheDurationMinutes} minutes).");
                return _apiCache.Manager;
            }
        }
        finally
        {
            CacheLock.Release();
        }

        // Cache miss or expired: fetch WITHOUT holding the global lock so concurrent
        // callers never serialize on 30s of network I/O (CORE-22). Concurrent fetches
        // may overlap; last write wins the cache, which is harmless (same endpoint).
        _logger.Debug("EasyMode session cache miss or expired. Fetching from API...");
        var manager = await FetchFromApiAsync();

        if (manager is { Systems.Count: > 0 })
        {
            await CacheLock.WaitAsync();
            try
            {
                _apiCache = (manager, DateTime.UtcNow);
                _logger.Debug("EasyMode configuration fetched from API and cached for session");
            }
            finally
            {
                CacheLock.Release();
            }
        }

        return manager;
    }

    private async Task<EasyModeManager?> FetchFromApiAsync()
    {
        try
        {
            _logger.Debug("Fetching EasyMode configuration from API...");
            var client = _httpClientFactory.CreateClient("EasyModeClient");

            var platformConfigurationId = GetPlatformConfigurationId();

            // Use a CancellationToken with a timeout (30 seconds to accommodate users with slower connections or VPN)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await client.GetAsync($"api/Systems/{platformConfigurationId}", cts.Token);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            var systems =
                await JsonSerializer.DeserializeAsync<List<EasyModeSystemConfig>>(stream, JsonOptions, cts.Token);

            if (systems == null || systems.Count == 0)
            {
                _logger.Warning("EasyMode API returned no systems");
                return null;
            }

            var manager = new EasyModeManager { Systems = systems };
            manager.Validate();
            return manager;
        }
        catch (Exception ex)
        {
            // Connectivity failures (DNS, no host, timeouts, TLS) are expected user-side
            // conditions with a working fallback. Per repo policy they must not be reported
            // as bugs, so log them at Information (below the Warning+ sink threshold).
            if (IsConnectivityError(ex))
            {
                _logger.Information(ex,
                    "EasyMode configuration could not be loaded from the API due to a connectivity issue. Falling back to local/default configuration");
            }
            else
            {
                _logger.Error(ex, "An error occurred while loading EasyMode configuration from the API");
            }

            return null;
        }
    }

    /// <summary>
    ///     Determines whether an exception represents an expected, user-side connectivity failure
    ///     (DNS resolution failure, no route to host, connection refused, TLS failure, or timeout)
    ///     rather than an application defect.
    /// </summary>
    private static bool IsConnectivityError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            switch (current)
            {
                case HttpRequestException:
                case System.Net.Sockets.SocketException:
                case System.Net.WebException:
                case TaskCanceledException:
                case OperationCanceledException:
                    return true;
            }
        }

        var message = ex.ToString();
        return message.Contains("No such host", StringComparison.OrdinalIgnoreCase)
               || message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
               || message.Contains("connection attempt failed", StringComparison.OrdinalIgnoreCase)
               || message.Contains("The remote name could not be resolved", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<EasyModeManager?> LoadFromFallbackAsync()
    {
        try
        {
            // Determine the appropriate XML file and fallback URL key
            // based on the platform configuration (OS + architecture)
            var xmlFile = GetLocalXmlFileName();

            // Get the fallback URL from configuration
            var fallbackUrl = _configuration.GetValue<string>(GetFallbackUrlConfigurationKey());

            if (string.IsNullOrEmpty(fallbackUrl))
            {
                _logger.Debug("No fallback URL configured for EasyMode XML");
                return null;
            }

            _logger.Debug($"Attempting to download EasyMode XML from fallback URL: {fallbackUrl}");

            // Download the XML file from fallback URL
            var client = _httpClientFactory.CreateClient("EasyModeClient");

            // Use a CancellationToken with a timeout (30 seconds)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await client.GetAsync(fallbackUrl, cts.Token);

            response.EnsureSuccessStatusCode();

            // Read the XML content
            var xmlContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                _logger.Debug("Fallback URL returned empty XML content");
                return null;
            }

            // Save the downloaded XML to the application directory for future use
            var xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, xmlFile);
            await File.WriteAllTextAsync(xmlFilePath, xmlContent, cts.Token);
            _logger.Debug($"Downloaded EasyMode XML saved to: {xmlFilePath}");

            // Load the saved XML file
            return LoadFromXml(_logger);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Fallback XML download timed out (30 seconds)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Debug($"Failed to load EasyMode configuration from fallback URL: {ex.Message}");

            // A failed fallback download is an expected connectivity/offline condition
            // (the user may simply have no internet). Per repo policy it must not be
            // reported as a bug, so log it at Information when it is a connectivity error.
            if (IsConnectivityError(ex))
            {
                _logger.Information(ex,
                    "EasyMode configuration could not be loaded from the Cloudflare fallback URL due to a connectivity issue");
            }
            else
            {
                _logger.Error(ex, "An error occurred while loading EasyMode configuration from the fallback URL");
            }

            return null;
        }
    }

    /// <summary>
    ///     Validates all loaded system configurations, removing any that are invalid.
    /// </summary>
    public void Validate()
    {
        Systems = Systems?.Where(static system => system.IsValid()).ToList() ?? [];
    }

    /// <summary>
    ///     Defines constants representing the types of downloadable assets for EasyMode systems.
    /// </summary>
    public static class DownloadType
    {
        /// <summary>The emulator application binary.</summary>
        public const string Emulator = "Emulator";

        /// <summary>A libretro core for retroarch-based emulators.</summary>
        public const string Core = "Core";

        /// <summary>The primary image pack.</summary>
        public const string ImagePack1 = "ImagePack1";

        /// <summary>The second image pack.</summary>
        public const string ImagePack2 = "ImagePack2";

        /// <summary>The third image pack.</summary>
        public const string ImagePack3 = "ImagePack3";

        /// <summary>The fourth image pack.</summary>
        public const string ImagePack4 = "ImagePack4";

        /// <summary>The fifth image pack.</summary>
        public const string ImagePack5 = "ImagePack5";
    }
}