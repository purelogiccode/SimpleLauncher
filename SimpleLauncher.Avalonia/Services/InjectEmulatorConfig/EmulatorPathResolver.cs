using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;

/// <summary>
///     Resolves the file path of an emulator executable by searching the configured systems.
///     Registered in DI and reuses the shared SystemManagerService (cached system.xml read).
/// </summary>
internal class EmulatorPathResolver
{
    private readonly SystemManagerService _systemManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EmulatorPathResolver" /> class.
    /// </summary>
    /// <param name="systemManager">The shared system manager service (DI singleton).</param>
    public EmulatorPathResolver(SystemManagerService systemManager)
    {
        _systemManager = systemManager;
    }

    /// <summary>
    ///     Tries to find the resolved executable path of an emulator whose name contains the given hint.
    /// </summary>
    /// <param name="emulatorNameHint">A name fragment used to identify the emulator.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <returns>The resolved emulator executable path, or null if not found.</returns>
    public string? TryFindEmulatorPath(string emulatorNameHint, ILogger logErrors)
    {
        if (string.IsNullOrWhiteSpace(emulatorNameHint))
            return null;

        try
        {
            var systems = _systemManager.LoadSystems().Cast<ISystemManager>().ToList();
            if (systems == null || systems.Count == 0)
                return null;

            foreach (var system in systems)
            {
                if (system.Emulators == null)
                    continue;

                foreach (var emulator in system.Emulators)
                {
                    if (string.IsNullOrWhiteSpace(emulator.EmulatorLocation))
                        continue;

                    if (emulator.EmulatorName?.Contains(emulatorNameHint, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var resolved = PathHelper.ResolveRelativeToAppDirectory(emulator.EmulatorLocation);
                        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved)) return resolved;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, $"Error resolving emulator path for hint: {emulatorNameHint}");
        }

        return null;
    }
}