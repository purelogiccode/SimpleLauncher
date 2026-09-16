using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Ares emulator before launching a game.
/// </summary>
public class AresConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AresConfigHandler" /> class.
    /// </summary>
    public AresConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Ares", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("ares.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedEmulatorExePath =
                PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = false;

            if (context.Settings is { Ares.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(() =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var aresWindow = scope.ServiceProvider.GetRequiredService<InjectAresConfigWindow>();
                        aresWindow.Owner = (Window)context.WindowContext.PlatformWindow;
                        aresWindow.Initialize(resolvedEmulatorExePath);
                        aresWindow.ShowDialog();
                        shouldRun = aresWindow.ShouldRun;
                    });
                }
            }
            else
            {
                shouldRun = true;
                if (!string.IsNullOrEmpty(resolvedEmulatorExePath) && File.Exists(resolvedEmulatorExePath))
                    AresConfigurationService.InjectSettings(resolvedEmulatorExePath, context.Settings!, _logger);
            }

            return shouldRun;
        }

        return false;
    }
}