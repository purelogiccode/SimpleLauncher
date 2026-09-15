using SimpleLauncher.Core.Interfaces;
using MessageBoxButton = SimpleLauncher.Core.Models.MessageBoxButton;
using MessageBoxImage = SimpleLauncher.Core.Models.MessageBoxImage;
using MessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.Services.WpfServices;

/// <summary>
///     WPF implementation of IMessageDialogService, displaying message boxes via the WPF dispatcher.
/// </summary>
public class WpfMessageDialogService : IMessageDialogService
{
    /// <summary>Displays an informational message box.</summary>
    public Task ShowInfoAsync(string message, string title = "")
    {
        const System.Windows.MessageBoxButton wpfButtons = System.Windows.MessageBoxButton.OK;
        const System.Windows.MessageBoxImage wpfIcon = (System.Windows.MessageBoxImage)(int)MessageBoxImage.Information;

        ShowOnCallingThread(message, title, wpfButtons, wpfIcon);

        return Task.CompletedTask;
    }

    /// <summary>Displays a warning message box.</summary>
    public Task ShowWarningAsync(string message, string title = "")
    {
        const System.Windows.MessageBoxButton wpfButtons = System.Windows.MessageBoxButton.OK;
        const System.Windows.MessageBoxImage wpfIcon = (System.Windows.MessageBoxImage)(int)MessageBoxImage.Warning;

        ShowOnCallingThread(message, title, wpfButtons, wpfIcon);

        return Task.CompletedTask;
    }

    /// <summary>Displays an error message box.</summary>
    public Task ShowErrorAsync(string message, string title = "")
    {
        const System.Windows.MessageBoxButton wpfButtons = System.Windows.MessageBoxButton.OK;
        const System.Windows.MessageBoxImage wpfIcon = (System.Windows.MessageBoxImage)(int)MessageBoxImage.Error;

        ShowOnCallingThread(message, title, wpfButtons, wpfIcon);

        return Task.CompletedTask;
    }

    /// <summary>Displays a confirmation dialog with OK/Cancel buttons, returning true if OK is clicked.</summary>
    public Task<bool> ShowConfirmAsync(string message, string title = "")
    {
        var result = ShowMessageBox(message, title, MessageBoxButton.OkCancel, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Ok);
    }

    /// <summary>Displays a Yes/No dialog, returning true if Yes is clicked.</summary>
    public Task<bool> ShowYesNoAsync(string message, string title = "")
    {
        var result = ShowMessageBox(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    /// <summary>Displays a message box with the specified buttons and icon, returning the user's choice.</summary>
    public Task<MessageBoxResult> ShowAsync(string message, string title, MessageBoxButton buttons,
        MessageBoxImage icon)
    {
        var result = ShowMessageBox(message, title, buttons, icon);
        return Task.FromResult(result);
    }

    private static MessageBoxResult ShowMessageBox(string message, string title, MessageBoxButton buttons,
        MessageBoxImage icon)
    {
        var wpfButtons = (System.Windows.MessageBoxButton)(int)buttons;
        var wpfIcon = (System.Windows.MessageBoxImage)(int)icon;

        var wpfResult = ShowOnCallingThread(message, title, wpfButtons, wpfIcon);

        return (MessageBoxResult)(int)wpfResult;
    }

    /// <summary>
    ///     Shows a Win32 message box on the calling thread. Win32 MessageBox pumps its own
    ///     modal loop and is thread-agnostic, so no dispatcher hop is needed — and none is
    ///     safe: marshaling via <c>Dispatcher.Invoke</c> deadlocks when the UI thread is
    ///     blocked in a sync-over-async wait (e.g. <c>SystemManagerService.LoadSystemManagers</c>
    ///     from the MainWindow ctor) while the pool thread needs the UI for the dialog (WPF-02).
    ///     On the UI thread this behaves exactly as before.
    /// </summary>
    private static System.Windows.MessageBoxResult ShowOnCallingThread(string message, string title,
        System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage icon)
    {
        return System.Windows.MessageBox.Show(message, title, buttons, icon);
    }
}