using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the RPCS3 (PS3) emulator before launching a game.
/// </summary>
public class Rpcs3ConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rpcs3ConfigHandler" /> class.
    /// </summary>
    public Rpcs3ConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("RPCS3", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("rpcs3.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = true;

            if (context.Settings is { Rpcs3.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(() =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var win = scope.ServiceProvider.GetRequiredService<InjectRpcs3ConfigWindow>();
                        win.Owner = (Window)context.WindowContext.PlatformWindow;
                        win.Initialize(resolvedExe);
                        win.ShowDialog();
                        shouldRun = win.ShouldRun;
                    });
                }
            }
            else if (File.Exists(resolvedExe))
            {
                Rpcs3ConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger);
            }

            return shouldRun;
        }

        return false;
    }
}