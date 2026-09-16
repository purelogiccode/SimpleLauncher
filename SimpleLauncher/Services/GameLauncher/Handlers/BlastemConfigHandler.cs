using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.InjectConfigWindows;
using BlastemConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.BlastemConfigurationService;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Handlers;

/// <summary>
///     Handles configuration injection for the Blastem (Genesis) emulator before launching a game.
/// </summary>
public class BlastemConfigHandler : IEmulatorConfigHandler
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BlastemConfigHandler" /> class.
    /// </summary>
    public BlastemConfigHandler(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public bool IsMatch(string emulatorName, string emulatorPath)
    {
        return emulatorName.Contains("Blastem", StringComparison.OrdinalIgnoreCase) ||
               (emulatorPath?.Contains("blastem.exe", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> HandleConfigurationAsync(LaunchContext context)
    {
        if (context.EmulatorManager != null)
        {
            var emulatorLocation = context.EmulatorManager.EmulatorLocation;
            var resolvedExe = PathHelper.ResolveRelativeToAppDirectory(emulatorLocation);

            // Log the path resolution for debugging
            _logger.Debug($"[BlastemConfigHandler] Emulator location: {emulatorLocation ?? "NULL"}");
            _logger.Debug($"[BlastemConfigHandler] Resolved executable: {resolvedExe ?? "NULL"}");

            // Early validation: Check if emulator path is configured
            if (string.IsNullOrWhiteSpace(emulatorLocation))
            {
                _logger.Debug("[BlastemConfigHandler] ERROR: Emulator location is not configured");
                _logger.Error(new InvalidOperationException("Blastem emulator location is not configured"),
                    "BlastemConfigHandler: Emulator location is null or empty in system configuration");
                // Allow game to launch anyway, user will be prompted to select emulator
            }
            // Early validation: Check if resolved path is valid
            else if (string.IsNullOrEmpty(resolvedExe))
            {
                _logger.Debug($"[BlastemConfigHandler] ERROR: Failed to resolve emulator path: {emulatorLocation}");
                _logger.Error(
                    new InvalidOperationException($"Failed to resolve Blastem emulator path: {emulatorLocation}"),
                    $"BlastemConfigHandler: Path resolution failed for '{emulatorLocation}'");
                // Allow game to launch anyway, user will be prompted to select emulator
            }
            // Early validation: Check if file exists
            else if (!File.Exists(resolvedExe))
            {
                _logger.Debug($"[BlastemConfigHandler] WARNING: Emulator not found at: {resolvedExe}");
                // Allow game to launch anyway, user will be prompted to select emulator
            }

            var shouldRun = false;

            if (context.Settings is { Blastem.ShowSettingsBeforeLaunch: true })
            {
                if (context.WindowContext != null)
                {
                    await context.WindowContext.Dispatcher.InvokeAsync(() =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var win = scope.ServiceProvider.GetRequiredService<InjectBlastemConfigWindow>();
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
                if (!string.IsNullOrEmpty(resolvedExe) && File.Exists(resolvedExe))
                {
                    try
                    {
                        BlastemConfigurationService.InjectSettings(resolvedExe, context.Settings!, _logger);
                        _logger.Debug("[BlastemConfigHandler] Configuration injected successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"[BlastemConfigHandler] ERROR: Configuration injection failed: {ex.Message}");
                        _logger.Error(ex,
                            $"BlastemConfigHandler: Configuration injection failed for path: {resolvedExe}");
                        // Continue launching the game even if injection fails
                    }
                }
                else
                {
                    _logger.Debug("[BlastemConfigHandler] Skipping configuration injection - emulator not found");
                }
            }

            return shouldRun;
        }

        return false;
    }
}