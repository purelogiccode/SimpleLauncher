using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Stella (Atari 2600) emulator before launching a game.
/// </summary>
public class StellaConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StellaConfigHandler" /> class.
    /// </summary>
    public StellaConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Stella", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("stella.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = false;

            if (context.Settings is { Stella.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(() =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var win = scope.ServiceProvider.GetRequiredService<InjectStellaConfigWindow>();
                        win.Owner = (Window)context.WindowContext.PlatformWindow;
                        win.Initialize(resolvedExe);
                        win.ShowDialog();
                        shouldRun = win.ShouldRun;
                    });
                }
            }
            else
            {
                shouldRun = true;
                if (File.Exists(resolvedExe))
                    StellaConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger);
            }

            return shouldRun;
        }

        return false;
    }
}