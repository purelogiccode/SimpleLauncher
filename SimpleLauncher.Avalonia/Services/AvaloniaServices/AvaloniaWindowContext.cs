using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Services.AvaloniaServices;

/// <summary>
///     Avalonia implementation of IWindowContext — wraps the main window.
/// </summary>
internal class AvaloniaWindowContext : IWindowContext
{
    public AvaloniaWindowContext(IDispatcherService dispatcher)
    {
        Dispatcher = dispatcher;
    }

    public Window? OwnerWindow { get; set; }

    public IntPtr Handle =>
        GetWindow().TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    public void Show()
    {
        GetWindow().Show();
    }

    public void Hide()
    {
        GetWindow().Hide();
    }

    public void Activate()
    {
        var window = GetWindow();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;

        window.Activate();
    }

    public IDispatcherService Dispatcher { get; }

    public object PlatformWindow => GetWindow();

    private Window GetWindow()
    {
        return OwnerWindow
               ?? (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
               ?? throw new InvalidOperationException("No window available");
    }
}