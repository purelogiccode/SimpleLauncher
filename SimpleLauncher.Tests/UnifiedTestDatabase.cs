using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Services.SystemManager;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     This collection must never run in parallel with other test classes: its tests
///     redirect the process-global <see cref="UnifiedSettingsDatabase.DatabasePathOverride" />,
///     which every database-backed manager resolves via the default database path.
/// </summary>
[CollectionDefinition(nameof(UsesDatabasePathOverride), DisableParallelization = true)]
public sealed class UsesDatabasePathOverride;

/// <summary>
///     Hermetic database helper for tests: redirects the unified database to an isolated
///     temp path so tests never read or write real user data. Use
///     <see cref="RedirectToMissingDb" /> to keep the legacy (XML/MessagePack) code paths
///     under test, and <see cref="RedirectToTempDb" /> to exercise the database paths.
///     Every test class that touches the managers must use this and join the
///     <c>UsesDatabasePathOverride</c> collection.
/// </summary>
internal static class UnifiedTestDatabase
{
    /// <summary>
    ///     Redirects the default database path to a (non-existent) temp file, so
    ///     <see cref="UnifiedSettingsDatabase.IsValidDatabase" /> returns false and the
    ///     legacy code paths run. Use this in tests that cover the XML/MessagePack
    ///     behavior on machines that already have a real settings.dat.
    /// </summary>
    public static string RedirectToMissingDb(string tempRoot)
    {
        var dbPath = Path.Combine(tempRoot, "settings.dat");
        UnifiedSettingsDatabase.DatabasePathOverride = dbPath;
        return dbPath;
    }

    /// <summary>
    ///     Redirects the default database path to a fresh temp database and returns its path.
    /// </summary>
    public static string RedirectToTempDb(string tempRoot)
    {
        var dbPath = Path.Combine(tempRoot, "settings.dat");
        UnifiedSettingsDatabase.DatabasePathOverride = dbPath;
        UnifiedSettingsDatabase.EnsureCreated(dbPath);
        return dbPath;
    }

    /// <summary>Seeds the redirected database with the supplied systems (first name wins).</summary>
    public static void SeedSystems(IEnumerable<SystemManagerService> systems)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in systems)
        {
            if (!dict.ContainsKey(system.SystemName))
                dict[system.SystemName] =
                    SystemConfigStore.Serialize(SystemManagerService.ToSystemManagerConfig(system));
        }

        UnifiedSettingsDatabase.SaveAllSystems(dict, UnifiedSettingsDatabase.GetDatabasePath());
    }

    /// <summary>Seeds the redirected database from a legacy system.xml fixture.</summary>
    public static void SeedSystemsFromXml(string xmlPath)
    {
        SeedSystems(SystemManagerService.LoadSystemsFromPath(xmlPath));
    }

    /// <summary>Clears the redirection (call from test-class Dispose).</summary>
    public static void ClearRedirect()
    {
        UnifiedSettingsDatabase.DatabasePathOverride = null;
    }
}
