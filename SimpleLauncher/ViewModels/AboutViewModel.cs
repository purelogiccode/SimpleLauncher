using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Services.GetApplicationVersion;
using UpdateChecker = SimpleLauncher.Services.CheckForUpdatesService;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the AboutWindow.
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly UpdateChecker _updateChecker;


    /// <summary>Initializes a new instance of the <see cref="AboutViewModel" />.</summary>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    /// <param name="updateChecker">The update checker service.</param>
    public AboutViewModel(ILogger logErrors, IMessageBoxLibraryService messageBox, UpdateChecker updateChecker)
    {
        _logger = logErrors;
        _messageBox = messageBox;
        _updateChecker = updateChecker;
        AppVersion = GetApplicationVersionService.GetVersion;
    }

    /// <summary>
    ///     Gets the application version string.
    /// </summary>
    public string AppVersion
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    ///     Gets whether an update check is in progress.
    /// </summary>
    public bool IsCheckingForUpdates
    {
        get;
        private set
        {
            if (SetProperty(ref field, value)) CheckForUpdatesCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    ///     Event raised to request the owner window for dialogs.
    /// </summary>
    public Func<Window>? GetOwnerWindow { get; set; }

    private bool CanCheckForUpdates => !IsCheckingForUpdates;

    /// <summary>
    ///     Event raised when the window should be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Event raised when the update history window should be opened.
    /// </summary>
    public event EventHandler? OpenUpdateHistoryRequested;

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;

        try
        {
            var ownerWindow = GetOwnerWindow?.Invoke();
            if (ownerWindow != null)
                await _updateChecker.ManualCheckForUpdatesAsync(ownerWindow);
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error in the CheckForUpdateAsync_Click method.";
            _logger.Error(ex, contextMessage);

            // Notify user
            await _messageBox.ErrorCheckingForUpdatesMessageBoxAsync();
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private void OpenUpdateHistory()
    {
        OpenUpdateHistoryRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task OpenWebsiteAsync(string url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error in the Hyperlink_RequestNavigate method.";
            _logger.Error(ex, contextMessage);

            // Notify user
            await _messageBox.UnableToOpenLinkMessageBoxAsync();
        }
    }
}