using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services.GameLauncher.Handlers;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     LB-08 regression tests: the pre-launch emulator config handlers inject settings into the
///     Windows config locations of each emulator, so they must only be registered on Windows.
/// </summary>
public class EmulatorConfigHandlerRegistrationTests
{
    [Fact]
    public void AddEmulatorConfigHandlers_RegistersHandlersOnWindowsOnly()
    {
        var services = new ServiceCollection();

        services.AddEmulatorConfigHandlers();

        var handlerRegistrations = services.Count(d => d.ServiceType == typeof(IEmulatorConfigHandler));
        Assert.Equal(OperatingSystem.IsWindows() ? 21 : 0, handlerRegistrations);
    }
}
