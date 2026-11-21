using Avalonia.Threading;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Services.AvaloniaServices;

/// <summary>
///     Avalonia implementation of IDispatcherService — dispatches work to the UI thread.
/// </summary>
internal class AvaloniaDispatcherService : IDispatcherService
{
    private readonly Dispatcher _dispatcher = Dispatcher.UIThread;

    public void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Invoke(action);
    }

    public Task InvokeAsync(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(action).GetTask();
    }

    public Task InvokeAsync(Func<Task> func)
    {
        if (_dispatcher.CheckAccess()) return func();

        return _dispatcher.InvokeAsync(func);
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (_dispatcher.CheckAccess())
            return Task.FromResult(func());

        return _dispatcher.InvokeAsync(func).GetTask();
    }
}