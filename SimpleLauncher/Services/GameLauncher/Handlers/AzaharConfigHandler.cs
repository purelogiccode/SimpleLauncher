using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Azahar (3DS) emulator before launching a game.
/// </summary>
public class AzaharConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AzaharConfigHandler" /> class.
    /// </summary>
    public AzaharConfigHandler(IMessageBoxLibraryService messageBox, ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _messageBox = messageBox;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Azahar", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("azahar.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = true;

            if (context.Settings is { Azahar.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(() =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var win = scope.ServiceProvider.GetRequiredService<InjectAzaharConfigWindow>();
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
                    AzaharConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger);
                }
                catch (AzaharPermissionException)
                {
                    // Show permission error message but allow the game to launch
                    await _messageBox.AzaharConfigurationInjectionPermissionErrorMessageBoxAsync();
                    // Return true to allow the game to launch with default settings
                }
            }

            return shouldRun;
        }

        return false;
    }
}