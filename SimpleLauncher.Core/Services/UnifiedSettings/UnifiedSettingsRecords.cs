namespace SimpleLauncher.Core.Services.UnifiedSettings;

/// <summary>A single favorite entry stored in the unified database.</summary>
/// <param name="FileName">Bare file name (or legacy full path) of the game.</param>
/// <param name="SystemName">Name of the system the game belongs to.</param>
public sealed record FavoriteRecord(string FileName, string SystemName);

/// <summary>A single play-history entry stored in the unified database.</summary>
/// <param name="FileName">Full file path (or legacy bare file name) of the game.</param>
/// <param name="SystemName">Name of the system the game belongs to.</param>
/// <param name="TimesPlayed">Number of recorded play sessions.</param>
/// <param name="TotalPlayTime">Cumulative play time in seconds.</param>
/// <param name="LastPlayDate">Last play date in ISO format (yyyy-MM-dd).</param>
/// <param name="LastPlayTime">Last play time in ISO format (HH:mm:ss).</param>
public sealed record PlayHistoryRecord(
    string FileName,
    string SystemName,
    int TimesPlayed,
    long TotalPlayTime,
    string LastPlayDate,
    string LastPlayTime);

/// <summary>Cumulative play time for one system.</summary>
/// <param name="SystemName">Name of the system.</param>
/// <param name="PlayTimeSeconds">Total play time in seconds.</param>
public sealed record SystemPlayTimeRecord(string SystemName, long PlayTimeSeconds);

/// <summary>Outcome of a one-time legacy migration into the unified database.</summary>
public enum MigrationStatus
{
    /// <summary>A valid settings.dat already existed; nothing was done.</summary>
    AlreadyCurrent,

    /// <summary>Legacy files were imported into a fresh settings.dat and shelved as .bak.</summary>
    Migrated,

    /// <summary>No legacy files existed; a fresh settings.dat was seeded with defaults.</summary>
    FreshCreated,

    /// <summary>Migration failed; legacy files were left untouched.</summary>
    Failed
}

/// <summary>Result of a legacy migration run.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Favorites">Number of favorites imported.</param>
/// <param name="HistoryEntries">Number of play-history entries imported.</param>
/// <param name="Systems">Number of systems imported.</param>
public sealed record MigrationResult(MigrationStatus Status, int Favorites, int HistoryEntries, int Systems);
