using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Services.WpfServices;

/// <summary>
///     WPF implementation of IApplicationLifetime, providing application shutdown and restart functionality.
/// </summary>
public class WpfApplicationLifetime(ILogger logErrors, IServiceProvider serviceProvider) : IApplicationLifetime
{
    private readonly ILogger _logger = logErrors;

    private readonly Lazy<IMessageBoxLibraryService> _messageBoxLibraryService =
        new(serviceProvider.GetRequiredService<IMessageBoxLibraryService>);

    /// <summary>Shuts down the WPF application.</summary>
    public void Shutdown()
    {
        Application.Current.Shutdown();
    }

    /// <summary>Restarts the application by launching a new process and shutting down the current one.</summary>
    public void Restart()
    {
        try
        {
            if (Environment.ProcessPath != null) _ = Process.Start(Environment.ProcessPath);

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Restart failed: Process.Start threw an exception");
            _ = _messageBoxLibraryService.Value.FailedToRestartMessageBoxAsync();
        }
    }
}