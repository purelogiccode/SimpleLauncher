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
    ///     The decision is delegated to the system matcher, which is the single source of
    ///     truth shared with the background hash scanner.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system is supported for RetroAchievements hashing; otherwise, false.</returns>
    public bool IsSystemSupportedForHashing(string systemName)
    {
        return _systemMatcher.IsSystemSupportedForHashing(systemName);
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

        // Arcade-based systems are hashed by file name (e.g. "game" from "game.zip");
        // every other system hashes file content.
        var isFileNameHashSystem = systemId == RetroAchievementsConstants.ArcadeConsoleId;

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
}