using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using IApplicationLifetime = SimpleLauncher.Core.Interfaces.IApplicationLifetime;

namespace SimpleLauncher.Avalonia.Services.AvaloniaServices;

/// <summary>
///     Avalonia implementation of IApplicationLifetime — controls app shutdown and restart.
/// </summary>
internal class AvaloniaApplicationLifetime : IApplicationLifetime
{
    private static IClassicDesktopStyleApplicationLifetime? Lifetime =>
        Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    public void Shutdown()
    {
        Lifetime?.Shutdown();
    }

    public void Restart()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is not null) Process.Start(exePath);

        Lifetime?.Shutdown();
    }
}