using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Services.UnifiedSettings;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     This collection must never run in parallel with other test classes: its tests
///     redirect the process-global <see cref="UnifiedSettingsDatabase.DatabasePathOverride" />,
///     which every database-backed manager resolves via the default database path.
/// </summary>
[CollectionDefinition(nameof(UsesDatabasePathOverride), DisableParallelization = true)]
public sealed class UsesDatabasePathOverride;

/// <summary>
///     Hermetic database helper for tests: redirects the unified database to an isolated
///     temp file so tests never read or write real user data, and seeds it from the
///     test's legacy system.xml fixture. Every test class that loads systems (or saves
///     through the static configuration paths) must use this and join the
///     <c>UsesDatabasePathOverride</c> collection.
/// </summary>
internal static class UnifiedTestDatabase
{
    /// <summary>
    ///     Redirects the default database path to a fresh temp database under
    ///     <paramref name="tempRoot" /> and returns its path.
    /// </summary>
    public static string RedirectToTempDb(string tempRoot)
    {
        var dbPath = Path.Combine(tempRoot, "settings.dat");
        UnifiedSettingsDatabase.DatabasePathOverride = dbPath;
        UnifiedSettingsDatabase.EnsureCreated(dbPath);
        return dbPath;
    }

    /// <summary>
    ///     Seeds the redirected database from a legacy system.xml fixture, then
    ///     invalidates the manager cache so the next load reads the seeded data.
    /// </summary>
    public static void SeedSystemsFromXml(SystemManagerService manager, string xmlPath)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var systems = manager.LoadSystemsFromPath(xmlPath);
        SaveSystems(systems);
        manager.InvalidateCache();
    }

    /// <summary>
    ///     Seeds the redirected database from a legacy system.xml fixture
    ///     (no manager instance required).
    /// </summary>
    public static void SeedSystemsFromXml(IConfiguration configuration, string xmlPath)
    {
        SeedSystemsFromXml(new SystemManagerService(configuration), xmlPath);
    }

    private static void SaveSystems(List<Core.Models.SystemManagerConfig> systems)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in systems)
        {
            if (!dict.ContainsKey(system.SystemName))
                dict[system.SystemName] = SystemConfigStore.Serialize(system);
        }

        UnifiedSettingsDatabase.SaveAllSystems(dict, UnifiedSettingsDatabase.GetDatabasePath());
    }

    /// <summary>Clears the redirection (call from test-class Dispose).</summary>
    public static void ClearRedirect()
    {
        UnifiedSettingsDatabase.DatabasePathOverride = null;
    }
}
