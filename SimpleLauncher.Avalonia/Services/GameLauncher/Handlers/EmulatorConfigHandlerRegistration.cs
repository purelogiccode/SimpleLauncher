using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Handlers;

/// <summary>
///     Registers the pre-launch emulator configuration handlers. They inject settings into the
///     Windows config locations of each emulator (RetroArch next to the executable, DuckStation
///     PCSX2/Dolphin under Documents, …), so they are Windows-only: the "Inject emulator config"
///     menu is hidden elsewhere and non-Windows launches must never write Windows-style config
///     files next to Linux binaries (LB-08).
/// </summary>
public static class EmulatorConfigHandlerRegistration
{
    /// <summary>
    ///     Registers every <see cref="IEmulatorConfigHandler" /> on Windows; a no-op elsewhere.
    /// </summary>
    public static void AddEmulatorConfigHandlers(this IServiceCollection services)
    {
        if (!OperatingSystem.IsWindows()) return;

        services.AddSingleton<IEmulatorConfigHandler, AresConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, AzaharConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, BlastemConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, CemuConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, DaphneConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, DolphinConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, DuckStationConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, FlycastConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, MameConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, MednafenConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, MesenConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, Pcsx2ConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, RaineConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, RedreamConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, RetroArchConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, Rpcs3ConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, SegaModel2ConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, StellaConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, SupermodelConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, XeniaConfigHandler>();
        services.AddSingleton<IEmulatorConfigHandler, YumirConfigHandler>();
    }
}
