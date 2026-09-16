using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.InjectConfigWindows;
using MameConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.MameConfigurationService;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the MAME emulator before launching a game.
/// </summary>
public class MameConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameConfigHandler" /> class.
    /// </summary>
    public MameConfigHandler(ILogger logger, IMessageBoxLibraryService messageBoxLibrary,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _messageBoxLibrary = messageBoxLibrary;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("mame.exe", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (emulatorPath?.Contains("mame64.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager.EmulatorLocation);
            if (context.SystemManagerService != null)
            {
                var resolvedSystemFolder =
                    PathHelper.ResolveRelativeToAppDirectory(context.SystemManagerService.PrimarySystemFolder);
                var listOfSecondarySystemFolders = context.SystemManagerService.SystemFolders.ToArray();

                var shouldRun = true;
                if (context.Settings is { Mame.ShowSettingsBeforeLaunch: true })
                {
                    if (context.WindowContext != null)
                    {
                        await context.WindowContext.Dispatcher.InvokeAsync(() =>
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var win = scope.ServiceProvider.GetRequiredService<InjectMameConfigWindow>();
                            win.Owner = (Window)context.WindowContext.PlatformWindow;
                            win.Initialize(resolvedExe, true, resolvedSystemFolder, listOfSecondarySystemFolders);
                            win.ShowDialog();
                            shouldRun = win.ShouldRun;
                        });
                    }
                }
                else
                {
                    try
                    {
                        MameConfigurationService.InjectSettings(resolvedExe!, context.Settings!, _logger,
                            resolvedSystemFolder, listOfSecondarySystemFolders);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        _logger.Debug($"[MameConfigHandler] Failed to inject MAME configuration: {ex.Message}");
                        _logger.Error(ex,
                            "[MameConfigHandler] Failed to inject MAME configuration. The game will launch with existing MAME settings");
                        await _messageBoxLibrary.FailedToInjectMameConfigurationMessageBoxAsync();
                    }
                }

                return shouldRun;
            }
        }

        return false;
    }
}