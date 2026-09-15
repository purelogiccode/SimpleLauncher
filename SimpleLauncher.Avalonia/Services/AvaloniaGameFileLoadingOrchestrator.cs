using SimpleLauncher.Core.Models;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Orchestrates game file loading for the library: resolves system folders, scans
///     the disk tolerantly (per-directory access failures never abort the scan), and
///     caches the resulting file lists per system so navigation between systems does
///     not re-enumerate the disk. Also owns cache invalidation for the file watcher.
///     Avalonia port of the WPF <c>GameFileLoadingOrchestratorService</c> + cache logic.
/// </summary>
public class AvaloniaGameFileLoadingOrchestrator
{
    private readonly AvaloniaGameCacheService _cache;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AvaloniaGameFileLoadingOrchestrator" /> class.
    /// </summary>
    /// <param name="cache">The per-system game file cache.</param>
    /// <param name="logger">The Serilog logger.</param>
    public AvaloniaGameFileLoadingOrchestrator(AvaloniaGameCacheService cache, ILogger logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    ///     Returns the game file paths for the system, using the cached list when
    ///     available and scanning the disk otherwise (same rules as the WPF
    ///     GetListOfFilesService: recursion stays on when GroupByFolder is enabled).
    /// </summary>
    /// <param name="system">The system configuration.</param>
    public List<string> GetGameFiles(SystemManagerConfig system)
    {
        return _cache.GetCachedOrScan(system, EnumerateSystemFiles);
    }

    /// <summary>
    ///     Computes per-system game counts from the cached-or-scanned file lists.
    /// </summary>
    /// <param name="systems">The systems to count.</param>
    public Dictionary<string, int> ComputeSystemCounts(List<SystemManagerConfig> systems)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in systems) counts[system.SystemName] = GetGameFiles(system).Count;

        return counts;
    }

    /// <summary>
    ///     Invalidates the cached file list for one system (called when its files
    ///     change on disk).
    /// </summary>
    /// <param name="systemName">The affected system name.</param>
    public void InvalidateSystem(string systemName)
    {
        _cache.Invalidate(systemName);
        _logger.Debug("[AvaloniaGameFileLoadingOrchestrator] Invalidated game file cache for '{System}'", systemName);
    }

    /// <summary>
    ///     Invalidates all cached file lists (called when the system configuration
    ///     changes or a full library refresh is forced).
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
        _logger.Debug("[AvaloniaGameFileLoadingOrchestrator] Cleared all game file caches");
    }

    /// <summary>
    ///     Enumerates game files for a system from its configured folders,
    ///     resolving %BASEFOLDER% / relative paths to real directories first.
    /// </summary>
    private IEnumerable<string> EnumerateSystemFiles(SystemManagerConfig system)
    {
        foreach (var folder in system.SystemFolders)
        {
            var resolvedFolder = PathHelper.ResolveRelativeToAppDirectory(folder);
            if (resolvedFolder == null || !Directory.Exists(resolvedFolder)) continue;

            var extensions = system.FileFormatsToSearch.Count > 0
                ? system.FileFormatsToSearch
                : [".zip", ".7z", ".rar", ".iso", ".chd", ".cue", ".bin", ".exe", ".bat"];

            var extensionSet = extensions
                .Select(static e => e.StartsWith('.') ? e : $".{e}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Same rule as the WPF GetListOfFilesService: recursion stays on when
            // GroupByFolder is enabled even if DisableRecursiveSearch is set — games
            // must be found in subfolders to be grouped by folder.
            var doRecurse = system is not { DisableRecursiveSearch: true, GroupByFolder: false };

            foreach (var file in EnumerateFilesTolerant(resolvedFolder, doRecurse))
            {
                if (extensionSet.Contains(Path.GetExtension(file)))
                    yield return file;
            }
        }
    }

    /// <summary>
    ///     Recursively enumerates files, tolerating per-directory access failures instead
    ///     of aborting the whole scan when one subfolder is inaccessible (mirrors the
    ///     per-directory error handling of the WPF GetListOfFilesService).
    /// </summary>
    private IEnumerable<string> EnumerateFilesTolerant(string directory, bool recurse)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch (Exception ex)
        {
            // Skip inaccessible folders instead of dropping the entire scan
            _logger.Debug(ex, "Skipping inaccessible folder {Folder}", directory);
            yield break;
        }

        foreach (var file in files) yield return file;

        if (!recurse) yield break;

        IEnumerable<string> subDirectories;
        try
        {
            subDirectories = Directory.EnumerateDirectories(directory);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Skipping inaccessible folder {Folder}", directory);
            yield break;
        }

        foreach (var subDirectory in subDirectories)
        {
            foreach (var file in EnumerateFilesTolerant(subDirectory, true))
                yield return file;
        }
    }
}