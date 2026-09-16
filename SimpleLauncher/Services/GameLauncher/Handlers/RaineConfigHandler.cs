using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Raine (arcade) emulator before launching a game.
/// </summary>
public class RaineConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RaineConfigHandler" /> class.
    /// </summary>
    public RaineConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Raine", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("raine", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            if (context.SystemManagerService != null)
            {
                var resolvedSystemFolder =
                    PathHelper.ResolveRelativeToAppDirectory(context.SystemManagerService.PrimarySystemFolder);
                if (context.Settings != null)
                {
                    var resolvedRaineRomDirectory =
                        PathHelper.ResolveRelativeToAppDirectory(context.Settings.Raine.RomDirectory);
                    var shouldRun = true;

                    if (context.Settings.Raine.ShowSettingsBeforeLaunch)
                    {
                        if (context.WindowContext != null)
                        {
                            await context.WindowContext.Dispatcher.InvokeAsync(() =>
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var win = scope.ServiceProvider.GetRequiredService<InjectRaineConfigWindow>();
                                win.Owner = (Window)context.WindowContext.PlatformWindow;
                                win.Initialize(resolvedExe, true, context.ResolvedFilePath, resolvedSystemFolder);
                                win.ShowDialog();
                                shouldRun = win.ShouldRun;
                            });
                        }
                    }
                    else if (File.Exists(resolvedExe))
                    {
                        // Pass the resolved RaineRomDirectory to the service
                        RaineConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger,
                            context.ResolvedFilePath, resolvedSystemFolder, resolvedRaineRomDirectory);
                    }

                    return shouldRun;
                }
            }
        }

        return false;
    }
}