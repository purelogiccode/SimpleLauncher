using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the DuckStation (PS1) emulator before launching a game.
/// </summary>
internal class DuckStationConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DuckStationConfigHandler" /> class.
    /// </summary>
    public DuckStationConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("DuckStation", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("duckstation", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = false;

            if (context.Settings is { DuckStation.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(async () =>
                    {
                        var win = _scopeFactory.CreateScope().ServiceProvider
                            .GetRequiredService<InjectDuckStationConfigWindow>();
                        win.Initialize(resolvedExe);
                        await win.ShowDialog((Window)context.WindowContext.PlatformWindow);
                        shouldRun = win.ShouldRun;
                    });
                }
            }
            else
            {
                shouldRun = true;
                if (File.Exists(resolvedExe))
                    DuckStationConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger);
            }

            return shouldRun;
        }

        return false;
    }
}