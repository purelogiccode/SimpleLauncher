using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Xenia (Xbox 360) emulator before launching a game.
/// </summary>
public class XeniaConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XeniaConfigHandler" /> class.
    /// </summary>
    public XeniaConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Xenia", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("xenia.exe", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (emulatorPath?.Contains("xenia_canary.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = true;

            if (context.Settings is { Xenia.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(() =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var win = scope.ServiceProvider.GetRequiredService<InjectXeniaConfigWindow>();
                        win.Owner = (Window)context.WindowContext.PlatformWindow;
                        win.Initialize(resolvedExe);
                        win.ShowDialog();
                        shouldRun = win.ShouldRun;
                    });
                }
            }
            else if (File.Exists(resolvedExe))
            {
                try
                {
                    XeniaConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger);
                }
                catch (Exception ex)
                {
                    // Log error but allow game to launch with default Xenia settings
                    _logger.Debug($"[XeniaConfigHandler] Failed to inject settings: {ex.Message}");
                }
            }

            return shouldRun;
        }

        return false;
    }
}