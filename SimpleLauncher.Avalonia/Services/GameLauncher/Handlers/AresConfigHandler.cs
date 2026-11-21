using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Ares emulator before launching a game.
/// </summary>
internal class AresConfigHandler : IEmulatorConfigHandler
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
                    await context.WindowContext.Dispatcher.InvokeAsync(async () =>
                    {
                        var aresWindow = _scopeFactory.CreateScope().ServiceProvider
                            .GetRequiredService<InjectAresConfigWindow>();
                        aresWindow.Initialize(resolvedEmulatorExePath);
                        await aresWindow.ShowDialog((Window)context.WindowContext.PlatformWindow);
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