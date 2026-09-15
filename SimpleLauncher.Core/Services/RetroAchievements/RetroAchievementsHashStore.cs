using System.Text.Json;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     Persists per-system RetroAchievements hash scans as JSON files under
///     <c>%LocalAppData%\SimpleLauncher\RetroAchievementsHashes\</c>.
///     Each scanned system gets its own <c>{SystemName}.json</c> file.
/// </summary>
public class RetroAchievementsHashStore : IRetroAchievementsHashStore
{
    private const string HashesFolderName = "RetroAchievementsHashes";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Lock _fileLock = new();
    private readonly string? _hashesFolderPathOverride;

    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsHashStore" /> class.
    /// </summary>
    /// <param name="logger">The logger instance used for debugging output.</param>
    /// <param name="hashesFolderPathOverride">
    ///     Optional custom folder for the hash files (used by tests); defaults to the
    ///     application data folder.
    /// </param>
    public RetroAchievementsHashStore(ILogger logger, string? hashesFolderPathOverride = null)
    {
        _logger = logger;
        _hashesFolderPathOverride = hashesFolderPathOverride;
    }

    private string HashesFolderPath => _hashesFolderPathOverride ??
                                       Path.Combine(AppDataPaths.SimpleLauncherDataFolder, HashesFolderName);

    /// <summary>
    ///     Gets the full path of the JSON hash file for the given system.
    /// </summary>
    public string GetSystemHashFilePath(string systemName)
    {
        var safeName = SanitizeFileName(systemName);
        return Path.Combine(HashesFolderPath, $"{safeName}.json");
    }

    /// <summary>
    ///     Determines whether a hash scan result file exists for the given system.
    /// </summary>
    public bool HasSystemHashes(string systemName)
    {
        try
        {
            return File.Exists(GetSystemHashFilePath(systemName));
        }
        catch (Exception ex)
        {
            lock (_fileLock)
            {
                _logger.Debug($"[RA Hash Store] Error checking hash file for '{systemName}': {ex.Message}");
            }

            return false;
        }
    }

    /// <summary>
    ///     Loads the persisted hash scan for the given system.
    /// </summary>
    public RaSystemHashes? LoadSystemHashes(string systemName)
    {
        var filePath = GetSystemHashFilePath(systemName);

        // Read under the same lock writers hold so a concurrent scan can never
        // observe a half-written file (CORE-25). Writes are atomic (temp+move),
        // so a missing temp file here simply means "no scan yet".
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;

                var data = JsonSerializer.Deserialize<RaSystemHashes>(json, JsonOptions);
                return data;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[RA Hash Store] Failed to load hash file for '{systemName}': {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    ///     Persists the hash scan for the given system as a JSON file.
    /// </summary>
    public void SaveSystemHashes(RaSystemHashes systemHashes)
    {
        if (string.IsNullOrWhiteSpace(systemHashes.SystemName)) return;

        lock (_fileLock)
        {
            try
            {
                var directory = HashesFolderPath;
                Directory.CreateDirectory(directory);

                var filePath = GetSystemHashFilePath(systemHashes.SystemName);
                var json = JsonSerializer.Serialize(systemHashes, JsonOptions);

                // Atomic write: readers (under the same lock, or crashing mid-write)
                // never see a truncated file (CORE-25). Same-directory temp keeps the
                // move on one volume so it stays atomic.
                var tempPath = filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, filePath, true);

                _logger.Debug(
                    $"[RA Hash Store] Saved {systemHashes.Hashes.Count} hashes for '{systemHashes.SystemName}' to {filePath}.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"[RA Hash Store] Failed to save hash file for '{systemHashes.SystemName}'.");
            }
        }
    }

    /// <summary>
    ///     Removes characters that are not valid in Windows file names from the system name.
    /// </summary>
    private static string SanitizeFileName(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName)) return "UnknownSystem";

        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(systemName
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());

        return string.IsNullOrWhiteSpace(safeName) ? "UnknownSystem" : safeName;
    }
}