using MessagePack;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     Manages the local RetroAchievements game database, providing hash-to-game lookups loaded from a MessagePack file.
/// </summary>
[MessagePackObject]
public class RetroAchievementsManager
{
    private static readonly DataFileLocation FileLocation = new("RetroAchievements.dat");

    private Dictionary<string, RaGameInfo> _hashToGameInfoLookup = null!;
    private ILogger _logger = null!;

    /// <summary>
    ///     Gets or sets the complete list of RetroAchievements game info entries.
    /// </summary>
    [Key(0)]
    public IList<RaGameInfo> AllGames { get; set; } = [];

    private static string DatFilePath => FileLocation.FilePath;

    /// <summary>
    ///     Loads the RetroAchievements game database from the MessagePack .dat file.
    /// </summary>
    public static RetroAchievementsManager LoadRetroAchievement(ILogger logErrors, ILogger logger)
    {
        var manager = new RetroAchievementsManager { _logger = logger };
        if (File.Exists(DatFilePath))
        {
            try
            {
                var bytes = File.ReadAllBytes(DatFilePath);
                if (bytes.Length > 0)
                {
                    // The root object in the .dat file is a List<RaGameInfo>,
                    // so we deserialize that directly and wrap it in our manager.
                    // A nil root (version skew) deserializes to null — fall back to empty (CORE-24).
                    manager.AllGames =
                        MessagePackSerializer.Deserialize<List<RaGameInfo>>(bytes) ?? [];
                }
            }
            catch (Exception ex)
            {
                // Notify developer
                const string contextMessage =
                    "Error loading RetroAchievements.dat. The file might be corrupted or invalid. A new empty file will be created.";
                logErrors.Error(ex, contextMessage);

                logger.Debug($"[RA Manager] Failed to load RetroAchievements.dat: {ex.Message}");
            }
        }

        // Populate the hash lookup dictionary after loading AllGames
        manager.PopulateHashLookup();

        // If the file doesn't exist, is empty, or fails to load, log it for debugging
        if (manager.AllGames.Count == 0)
        {
            // Notify developer
            const string contextMessage = "RetroAchievements.dat is missing or empty. Starting with an empty database.";
            logErrors.Warning(contextMessage);

            logger.Debug("[RA Manager] Starting with empty RetroAchievements database");
        }

        return manager;
    }

    /// <summary>
    ///     Populates the internal dictionary for fast hash-to-gameinfo lookups.
    /// </summary>
    private void PopulateHashLookup()
    {
        _hashToGameInfoLookup = new Dictionary<string, RaGameInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in AllGames ?? [])
        {
            // Entries from a skewed .dat version may be null or carry a null hash list (CORE-24).
            if (game?.Hashes == null) continue;

            foreach (var hash in game.Hashes)
            {
                if (string.IsNullOrEmpty(hash)) continue;

                // Add the hash to the dictionary. If a hash maps to multiple games,
                // we'll just take the first one encountered. This is a simplification.
                // RetroAchievements API usually handles this by returning the primary game.
                _hashToGameInfoLookup.TryAdd(hash, game);
            }
        }

        _logger.Debug($"[RA Manager] Populated hash lookup with {_hashToGameInfoLookup.Count} entries.");
    }

    /// <summary>
    ///     Retrieves RaGameInfo by a given hash from the in-memory lookup.
    /// </summary>
    /// <param name="hash">The hash to look up.</param>
    /// <returns>The matching RaGameInfo, or null if not found.</returns>
    public RaGameInfo? GetGameInfoByHash(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return null;

        _hashToGameInfoLookup ??=
            new Dictionary<string, RaGameInfo>(StringComparer.OrdinalIgnoreCase); // Ensure initialized
        _hashToGameInfoLookup.TryGetValue(hash, out var gameInfo);
        return gameInfo;
    }
}