using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the AboutWindow.
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly AvaloniaCheckForUpdatesService _updateChecker;
    private string _appVersion = "";

    private bool _isCheckingForUpdates;

    /// <summary>Initializes a new instance of the <see cref="AboutViewModel" />.</summary>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    /// <param name="updateChecker">The update checker service.</param>
    public AboutViewModel(ILogger logErrors, IMessageBoxLibraryService messageBox,
        AvaloniaCheckForUpdatesService updateChecker)
    {
        _logger = logErrors;
        _messageBox = messageBox;
        _updateChecker = updateChecker;

        AppVersion = $"Version: {Assembly.GetExecutingAssembly().GetName().Version}";

        // Root UI icons are embedded in the assembly (AvaloniaResource), not copied next to
        // the exe, so the logo is addressed through the asset loader (PathToImageConverter
        // resolves avares:// URIs). images\default.png stays loose for file-path consumers.
        LogoPath = "avares://SimpleLauncher.Avalonia/images/logo2.png";
    }

    /// <summary>
    ///     Gets the application version string.
    /// </summary>
    public string AppVersion
    {
        get => _appVersion;
        private set => SetProperty(ref _appVersion, value);
    }

    /// <summary>
    ///     Gets the path to the application logo image.
    /// </summary>
    public string LogoPath { get; }

    /// <summary>
    ///     Gets whether an update check is in progress.
    /// </summary>
    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (SetProperty(ref _isCheckingForUpdates, value)) CheckForUpdatesCommand.NotifyCanExecuteChanged();
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
            await _updateChecker.ManualCheckForUpdatesAsync(GetOwnerWindow?.Invoke());
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error in the CheckForUpdateAsync method.";
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