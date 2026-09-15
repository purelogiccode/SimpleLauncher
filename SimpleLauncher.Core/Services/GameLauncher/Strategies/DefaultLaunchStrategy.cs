using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.GameLauncher.Strategies;

/// <summary>
///     Fallback strategy that handles standard file types (.BAT, .LNK, .URL, .EXE) and regular ROM/game launches.
///     Has the lowest priority so all specialized strategies are tried first.
/// </summary>
public class DefaultLaunchStrategy : ILaunchStrategy
{
    /// <inheritdoc />
    public int Priority => 999;

    /// <inheritdoc />
    public bool IsMatch(LaunchContext context)
    {
        return true;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(launcher);

        // Fail with explicit errors instead of NullReferenceExceptions when the
        // launch context is missing required members (CORE-30).
        var emulatorManager = context.EmulatorManager ??
                              throw new InvalidOperationException(
                                  $"Cannot launch '{context.ResolvedFilePath}': LaunchContext.EmulatorManager is null.");
        var windowContext = context.WindowContext ??
                            throw new InvalidOperationException(
                                $"Cannot launch '{context.ResolvedFilePath}': LaunchContext.WindowContext is null.");

        var ext = Path.GetExtension(context.ResolvedFilePath).ToUpperInvariant();

        switch (ext)
        {
            case ".BAT":
                return launcher.RunBatchFileAsync(context.ResolvedFilePath, emulatorManager,
                    windowContext);
            case ".LNK":
            case ".URL":
                return launcher.LaunchShortcutFileAsync(context.ResolvedFilePath, emulatorManager,
                    windowContext);
            case ".EXE":
                return launcher.LaunchExecutableAsync(context.ResolvedFilePath, emulatorManager,
                    windowContext);
            default:
                var systemManagerService = context.SystemManagerService ??
                                           throw new InvalidOperationException(
                                               $"Cannot launch '{context.ResolvedFilePath}': LaunchContext.SystemManagerService is null.");
                // This handles all standard ROMs/Games
                return launcher.LaunchRegularEmulatorAsync(
                    context.ResolvedFilePath,
                    context.EmulatorName,
                    systemManagerService,
                    emulatorManager,
                    context.Parameters,
                    windowContext,
                    context.LoadingState);
        }
    }
}