using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     A helper class that orchestrates RetroAchievements hashing for game files:
///     system matching (with a platform-provided system selection prompt), archive
///     extraction, and hash calculation delegated entirely to the bundled
///     RetroAchievementsSharp CLI tool (which replaces the previous in-process
///     library and the external RAHasher binary, including native RVZ/WIA disc
///     hashing).
/// </summary>
public class RetroAchievementsHasherTool : IRetroAchievementsHasherTool
{
    // Systems Not Supported or with UnknowHashLogic
    // These systems will not show the RetroAchievements icon and hashing will be skipped
    private static readonly List<string> SystemWithUnknowHashLogic =
    [
        "sega pico", "xbox", "xbox360",
        "atari st", "commodore 64", "amiga", "zx spectrum",
        "philips cd-i", "sharp x68000", "sharp x1", "oric", "thomson to8", "cassette vision",
        "super cassette vision", "uzebox", "tic-80", "ti-83", "nokia n-gage", "vic-20", "zx81",
        "pc-6000", "game & watch", "elektor tv games computer", "interton vc 4000",
        "arcadia 2001", "fm towns", "hubs", "events", "standalone", "atari 800", "microsoft windows",
        "sega naomi", "mega duck", "atari 5200", "atari 800", "atari 8-bit"
    ];

    private readonly IExtractionService _extractionService;
    private readonly IRetroAchievementsFileHasher _fileHasher;
    private readonly ILogger _logger;
    private readonly IRetroAchievementsSystemMatcher _systemMatcher;
    private readonly Func<string, Task<string?>> _systemSelector;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsHasherTool" /> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="extractionService">The extraction service for decompressing archives before hashing.</param>
    /// <param name="systemSelector">
    ///     A factory that shows the system selection dialog with a pre-selected guess and returns the
    ///     chosen system (or null when cancelled).
    /// </param>
    /// <param name="systemMatcher">The system matcher for fuzzy matching RetroAchievements system names.</param>
    /// <param name="fileHasher">The file hasher that delegates hash calculation to the RetroAchievementsSharp CLI tool.</param>
    public RetroAchievementsHasherTool(
        ILogger logger,
        IExtractionService extractionService,
        Func<string, Task<string?>> systemSelector,
        IRetroAchievementsSystemMatcher systemMatcher,
        IRetroAchievementsFileHasher fileHasher)
    {
        _logger = logger;
        _extractionService = extractionService;
        _systemSelector = systemSelector;
        _systemMatcher = systemMatcher;
        _fileHasher = fileHasher;
    }

    /// <summary>
    ///     Checks if a system is supported for RetroAchievements hashing.
    ///     This is used to determine whether to show the RA icon and attempt hashing.
    ///     Handles name variations by checking against known aliases and using fuzzy matching.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system is supported for RetroAchievements hashing; otherwise, false.</returns>
    public bool IsSystemSupportedForHashing(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            return false;

        var normalizedInput = systemName.Trim().ToLowerInvariant();

        // First, check if the input directly matches any unsupported system (including aliases)
        // This is important to catch variations like "PS3", "Sony PS3", etc.
        foreach (var unsupportedSystem in SystemWithUnknowHashLogic)
        {
            if (IsSystemNameMatch(normalizedInput, unsupportedSystem))
                return false;
        }

        // Get the best match from the system mappings (this handles fuzzy matching for supported systems)
        var matchedSystemName = _systemMatcher.GetBestMatchSystemName(systemName);

        // Check if the matched system is in the unsupported list
        if (SystemWithUnknowHashLogic.Contains(matchedSystemName, StringComparer.OrdinalIgnoreCase))
            return false;

        // The RetroAchievementsSharp CLI tool can hash every system that has a console ID in the mappings
        return _systemMatcher.IsSystemInMappings(systemName);
    }

    /// <summary>
    ///     Calculates the RetroAchievements hash for a game file, handling system matching and extraction as needed.
    ///     The hash calculation itself is delegated to the RetroAchievementsSharp CLI tool.
    /// </summary>
    /// <param name="filePath">The full path to the game file to hash.</param>
    /// <param name="systemName">The name of the system the game belongs to.</param>
    /// <param name="fileFormatsToLaunch">The list of file extensions considered valid for launching.</param>
    /// <param name="loadingState">The optional loading state to update during hash calculation.</param>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <returns>A <see cref="RaHashResult" /> containing the hash, temp extraction path, and any error information.</returns>
    public async Task<RaHashResult> GetGameHashForRetroAchievementsAsync(string filePath, string systemName,
        IList<string> fileFormatsToLaunch, ILoadingState loadingState, ILogger logErrors)
    {
        // 1. Try to get a 100% certain match
        var confirmedSystem = _systemMatcher.GetExactAliasMatch(systemName);

        // 2. If not 100% certain, ask the user
        if (confirmedSystem == null)
        {
            // Get a "guess" to pre-select in the dialog
            _logger.Debug($"[GetGameHashForRetroAchievementsAsync] Received systemName: {systemName}");
            var guess = _systemMatcher.GetBestMatchSystemName(systemName);
            _logger.Debug($"[GetGameHashForRetroAchievementsAsync] Guess systemName: {guess}");

            var userSelectedSystem = await _systemSelector(guess);
            _logger.Debug($"[GetGameHashForRetroAchievementsAsync] UserSelectedSystem: {userSelectedSystem}");

            if (string.IsNullOrEmpty(userSelectedSystem))
            {
                _logger.Debug("[GetGameHashForRetroAchievementsAsync] User did not choose a system. Returning null");
                return new RaHashResult(null, null, false, "System selection cancelled by user.");
            }

            systemName = userSelectedSystem;
        }
        else
        {
            systemName = confirmedSystem;
        }

        string? tempExtractionPath = null;
        string? hash;
        var isExtractionSuccessful = true; // Assume success initially
        string? extractionErrorMessage = null;

        // Report loading state if provided
        loadingState?.SetLoadingState(true, "Calculating game hash...");

        if (!File.Exists(filePath))
        {
            _logger.Debug($"[RA Hasher Tool] File not found at {filePath}");
            logErrors.Information($"[RA Hasher Tool] File not found at {filePath}");
            loadingState?.SetLoadingState(false);
            return new RaHashResult(null, null, false, "Game file not found.");
        }

        if (string.IsNullOrWhiteSpace(systemName))
        {
            _logger.Debug("[RA Hasher Tool] SystemName is null or empty");
            logErrors.Information("[RA Hasher Tool] SystemName is null or empty");
            loadingState?.SetLoadingState(false);
            return new RaHashResult(null, null, false, "System name is missing.");
        }

        // Systems without a usable console ID (e.g. the "unsupported" pseudo-system) cannot be hashed
        var systemId = _systemMatcher.GetSystemId(systemName);
        if (systemId is <= 0 or > RetroAchievementsConstants.MaxConsoleId)
        {
            _logger.Debug($"[RA Hasher Tool] System '{systemName}' is not supported for RetroAchievements hashing.");
            loadingState?.SetLoadingState(false);
            return new RaHashResult(null, null, false,
                $"System '{systemName}' is not supported for RetroAchievements hashing.");
        }

        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        // Arcade games are hashed by file name (e.g. "game" from "game.zip"); every other system hashes file content
        var isFileNameHashSystem = systemName.Equals("arcade", StringComparison.OrdinalIgnoreCase);

        // --- Pre-processing: only extract when really needed ---
        // .zip archives are handled by the RetroAchievementsSharp CLI tool itself
        // (hash the first entry — no disk extraction needed). Only .7z/.rar
        // archives are extracted.
        var fileToProcess = filePath; // By default, process the original file

        if (fileExtension is ".7z" or ".rar" && !isFileNameHashSystem)
        {
            _logger.Debug($"[RA Hasher Tool] Compressed file detected for hashing: {filePath}. Extracting...");
            var (extractedGameFilePath, extractedTempDirPath) =
                await _extractionService.ExtractToTempAndGetLaunchFileAsync(filePath, fileFormatsToLaunch);
            tempExtractionPath = extractedTempDirPath;

            if (string.IsNullOrEmpty(extractedGameFilePath))
            {
                isExtractionSuccessful = false;
                extractionErrorMessage =
                    $"Failed to extract or find a suitable file in archive for hashing: {filePath}.";
                logErrors.Information($"[RA Hasher Tool] {extractionErrorMessage}");
                _logger.Debug($"[RA Hasher Tool] {extractionErrorMessage}");
                loadingState?.SetLoadingState(false);
                return new RaHashResult(null, tempExtractionPath, isExtractionSuccessful, extractionErrorMessage);
            }

            fileToProcess = extractedGameFilePath;
        }

        // --- Perform Hashing (delegated entirely to the RetroAchievementsSharp CLI tool) ---
        try
        {
            hash = await _fileHasher.CalculateHashAsync(fileToProcess, systemName);
            _logger.Debug($"[RA Hasher Tool] Calculated hash: {hash}");
        }
        catch (Exception ex)
        {
            logErrors.Error(ex,
                $"[RA Hasher Tool] An error occurred during hash calculation for {filePath} (System: {systemName}).");
            _logger.Debug(
                $"[RA Hasher Tool] An error occurred during hash calculation for {filePath} (System: {systemName}).");
            return new RaHashResult(null, tempExtractionPath, false, $"Error during hash calculation: {ex.Message}");
        }
        finally
        {
            loadingState?.SetLoadingState(false);
        }

        if (string.IsNullOrEmpty(hash))
        {
            // The file could not be hashed (unsupported file type, missing 3DS keys, etc.)
            _logger.Debug(
                $"[RA Hasher Tool] Could not calculate a RetroAchievements hash for {filePath} (System: {systemName}).");
            logErrors.Information(
                $"[RA Hasher Tool] Could not calculate a RetroAchievements hash for {filePath} (System: {systemName}).");
            extractionErrorMessage = "Could not calculate a RetroAchievements hash for this game.";
        }

        return new RaHashResult(hash, tempExtractionPath, isExtractionSuccessful, extractionErrorMessage);
    }

    /// <summary>
    ///     Checks if two system names match, considering various naming conventions and variations.
    /// </summary>
    private static bool IsSystemNameMatch(string input, string pattern)
    {
        // Direct match
        if (input.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if input contains the pattern or vice versa
        if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            pattern.Contains(input, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Remove common separators and normalize
        var cleanInput = NormalizeSystemName(input);
        var cleanPattern = NormalizeSystemName(pattern);

        if (cleanInput.Equals(cleanPattern, StringComparison.OrdinalIgnoreCase))
            return true;

        if (cleanInput.Contains(cleanPattern, StringComparison.OrdinalIgnoreCase) ||
            cleanPattern.Contains(cleanInput, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for common abbreviations and variations
        return AreSystemAbbreviationsEquivalent(input, pattern);
    }

    /// <summary>
    ///     Normalizes a system name by removing common separators and standardizing format.
    /// </summary>
    private static string NormalizeSystemName(string input)
    {
        return input
            .Replace("-", "")
            .Replace("/", "")
            .Replace("&", "")
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("'", "")
            .Replace("™", "")
            .Replace("®", "")
            .ToLowerInvariant();
    }

    /// <summary>
    ///     Checks if two system names are equivalent based on common abbreviations and naming conventions.
    /// </summary>
    private static bool AreSystemAbbreviationsEquivalent(string input, string pattern)
    {
        var normalizedInput = NormalizeSystemName(input);
        var normalizedPattern = NormalizeSystemName(pattern);

        // Common Sony variations
        if (normalizedInput.Contains("ps3", StringComparison.Ordinal) ||
            normalizedInput.Contains("playstation3", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("ps3", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("playstation3", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("ps2", StringComparison.Ordinal) ||
            normalizedInput.Contains("playstation2", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("ps2", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("playstation2", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("ps1", StringComparison.Ordinal) ||
            normalizedInput.Contains("playstation1", StringComparison.Ordinal) ||
            normalizedInput.Contains("psx", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("ps1", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("playstation1", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("psx", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("psp", StringComparison.Ordinal) ||
            normalizedInput.Contains("playstationportable", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("psp", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("playstationportable", StringComparison.Ordinal);
        }

        // Common Nintendo variations
        if (normalizedInput.Contains("nes", StringComparison.Ordinal) ||
            normalizedInput.Contains("nintendoentertainmentsystem", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("nes", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("nintendoentertainmentsystem", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("snes", StringComparison.Ordinal) ||
            normalizedInput.Contains("supernintendo", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("snes", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("supernintendo", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("n64", StringComparison.Ordinal) ||
            normalizedInput.Contains("nintendo64", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("n64", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("nintendo64", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("gc", StringComparison.Ordinal) ||
            normalizedInput.Contains("gamecube", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("gc", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("gamecube", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("gb", StringComparison.Ordinal) ||
            normalizedInput.Contains("gameboy", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("gb", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("gameboy", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("gba", StringComparison.Ordinal) ||
            normalizedInput.Contains("gameboyadvance", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("gba", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("gameboyadvance", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("gbc", StringComparison.Ordinal) ||
            normalizedInput.Contains("gameboycolor", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("gbc", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("gameboycolor", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("nds", StringComparison.Ordinal) ||
            normalizedInput.Contains("nintendods", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("nds", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("nintendods", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("3ds", StringComparison.Ordinal) ||
            normalizedInput.Contains("nintendo3ds", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("3ds", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("nintendo3ds", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("wiiu", StringComparison.Ordinal))
            return normalizedPattern.Contains("wiiu", StringComparison.Ordinal);
        if (normalizedInput.Contains("switch", StringComparison.Ordinal) ||
            normalizedInput.Contains("nintendoswitch", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("switch", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("nintendoswitch", StringComparison.Ordinal);
        }

        // Common Sega variations
        if (normalizedInput.Contains("genesis", StringComparison.Ordinal) ||
            normalizedInput.Contains("megadrive", StringComparison.Ordinal) ||
            normalizedInput.Contains("segagenesis", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("genesis", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("megadrive", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("segagenesis", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("sms", StringComparison.Ordinal) ||
            normalizedInput.Contains("mastersystem", StringComparison.Ordinal) ||
            normalizedPattern.Contains("segamastersystem", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("sms", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("mastersystem", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("segamastersystem", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("gg", StringComparison.Ordinal) ||
            normalizedInput.Contains("gamegear", StringComparison.Ordinal) ||
            normalizedPattern.Contains("segagamegear", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("gg", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("gamegear", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("segagamegear", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("saturn", StringComparison.Ordinal) ||
            normalizedInput.Contains("segasaturn", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("saturn", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("segasaturn", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("dreamcast", StringComparison.Ordinal) ||
            normalizedInput.Contains("segadreamcast", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("dreamcast", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("segadreamcast", StringComparison.Ordinal);
        }

        // Common Microsoft variations
        if (normalizedInput.Contains("xbox360", StringComparison.Ordinal) ||
            normalizedInput.Contains("xbox 360", StringComparison.Ordinal) ||
            normalizedInput.Contains("xb360", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("xbox360", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("xbox 360", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("xb360", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("xboxone", StringComparison.Ordinal) ||
            normalizedInput.Contains("xbox one", StringComparison.Ordinal) ||
            normalizedInput.Contains("xbone", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("xboxone", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("xbox one", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("xbone", StringComparison.Ordinal);
        }

        // Common arcade variations
        if (normalizedInput.Contains("mame", StringComparison.Ordinal) ||
            normalizedInput.Contains("arcade", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("mame", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("arcade", StringComparison.Ordinal);
        }

        if (normalizedInput.Contains("neogeo", StringComparison.Ordinal) ||
            normalizedInput.Contains("neo geo", StringComparison.Ordinal))
        {
            return normalizedPattern.Contains("neogeo", StringComparison.Ordinal) ||
                   normalizedPattern.Contains("neo geo", StringComparison.Ordinal);
        }

        return false;
    }
}