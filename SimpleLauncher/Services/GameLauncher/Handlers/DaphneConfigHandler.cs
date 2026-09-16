using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Daphne (laserdisc arcade) emulator before launching a game.
/// </summary>
public class DaphneConfigHandler : IEmulatorConfigHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DaphneConfigHandler" /> class.
    /// </summary>
    public DaphneConfigHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Daphne", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("daphne.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        var shouldRun = true;
        if (context is { Settings.Daphne.ShowSettingsBeforeLaunch: true, WindowContext: not null })
        {
            await context.WindowContext.Dispatcher.InvokeAsync(() =>
            {
                using var scope = _scopeFactory.CreateScope();
                var win = scope.ServiceProvider.GetRequiredService<InjectDaphneConfigWindow>();
                win.Owner = (Window)context.WindowContext.PlatformWindow;
                win.Initialize();
                win.ShowDialog();
                shouldRun = win.ShouldRun;
            });
        }

        if (shouldRun)
        {
            var daphneArgs = DaphneConfigurationService.BuildArguments(context.Settings!);
            context.Parameters = $"{context.Parameters} {daphneArgs}".Trim();
        }

        return shouldRun;
    }
}