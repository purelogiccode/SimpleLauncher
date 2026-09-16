using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.InjectConfigWindows;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the PCSX2 (PS2) emulator before launching a game.
/// </summary>
public class Pcsx2ConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Pcsx2ConfigHandler" /> class.
    /// </summary>
    public Pcsx2ConfigHandler(IMessageBoxLibraryService messageBox, ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _messageBox = messageBox;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("PCSX2", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("pcsx2.exe", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (emulatorPath?.Contains("pcsx2-qt.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            var shouldRun = true;

            if (context.Settings is { Pcsx2.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(() =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var win = scope.ServiceProvider.GetRequiredService<InjectPcsx2ConfigWindow>();
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
                    Pcsx2ConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger);
                }
                catch (Pcsx2PermissionException)
                {
                    // Show permission error message but allow the game to launch
                    await _messageBox.Pcsx2ConfigurationInjectionPermissionErrorMessageBoxAsync();
                    // Return true to allow the game to launch with default settings
                }
            }

            return shouldRun;
        }

        return false;
    }
}