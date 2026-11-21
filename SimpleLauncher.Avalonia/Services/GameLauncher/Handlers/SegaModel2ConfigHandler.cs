using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.InjectConfigWindows;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Sega Model 2 emulator before launching a game.
/// </summary>
internal class SegaModel2ConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SegaModel2ConfigHandler" /> class.
    /// </summary>
    public SegaModel2ConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Model 2", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("emulator.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = false;

            if (context.Settings is { SegaModel2.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(async () =>
                    {
                        var win = _scopeFactory.CreateScope().ServiceProvider
                            .GetRequiredService<InjectSegaModel2ConfigWindow>();
                        win.Initialize(resolvedExe);
                        await win.ShowDialog((Window)context.WindowContext.PlatformWindow);
                        shouldRun = win.ShouldRun;
                    });
                }
            }
            else
            {
                shouldRun = true;
                SegaModel2ConfigurationService.InjectSettings(resolvedExe!, context.Settings!, _logger);
            }

            return shouldRun;
        }

        return false;
    }
}