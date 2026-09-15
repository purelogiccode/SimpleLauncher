using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Window for submitting support requests and bug reports.
/// </summary>
public partial class SupportWindow : ILoadingState
{
    private readonly EventHandler _formClearedHandler;
    private readonly ILogger _logger;
    private readonly SupportViewModel _viewModel;
    private Button? _emergencyReturnButton;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SupportWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing support form logic.</param>
    /// <param name="logger">The debug logger.</param>
    public SupportWindow(SupportViewModel viewModel, ILogger logger)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _viewModel = viewModel;

        _formClearedHandler = (_, _) =>
        {
            NameTextBox.Text = "";
            EmailTextBox.Text = "";
            SupportTextBox.Text = "";
        };

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.FormCleared += _formClearedHandler;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        Closing += (_, _) =>
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.FormCleared -= _formClearedHandler;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            if (_emergencyReturnButton != null)
            {
                _emergencyReturnButton.Click -= EmergencyOverlayRelease_Click;
                _emergencyReturnButton = null;
            }
        };

        Loaded += (_, _) =>
        {
            LoadingOverlay.ApplyTemplate();
            if (LoadingOverlay.Template.FindName("PART_EmergencyReturnButton", LoadingOverlay) is Button emergencyBtn)
            {
                _emergencyReturnButton = emergencyBtn;
                emergencyBtn.Click += EmergencyOverlayRelease_Click;
            }
        };

        DataContext = _viewModel;
    }

    /// <summary>
    ///     Toggles the loading overlay with an optional message.
    /// </summary>
    /// <param name="isLoading">Whether to show or hide the loading overlay.</param>
    /// <param name="message">Optional message to display while loading.</param>
    public void SetLoadingState(bool isLoading, string? message = null)
    {
        // BeginInvoke (not Invoke): callers include background continuations, and a sync
        // block here deadlocks on reentrancy and throws once the dispatcher shuts down (WPF-17).
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

                MainContentGrid?.IsEnabled = !isLoading;

                if (isLoading)
                {
                    LoadingOverlay.Content =
                        message ?? (string)Application.Current.TryFindResource("Loading") ?? "Loading...";
                }
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            Log.Debug($"SupportWindow SetLoadingState dropped during shutdown: {ex.Message}");
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SupportViewModel.IsLoading), StringComparison.Ordinal))
        {
            var loadingMessage = (string)Application.Current.TryFindResource("SendingSupportRequest") ??
                                 "Sending support request...";
            SetLoadingState(_viewModel.IsLoading, loadingMessage);
        }
    }

    private void EmergencyOverlayRelease_Click(object sender, RoutedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        MainContentGrid?.IsEnabled = true;

        _logger.Debug("[Emergency] User forced overlay dismissal in SupportWindow");
        (Application.Current.MainWindow as MainWindow)?.UpdateStatusBarService.UpdateContent(
            "Emergency reset performed.");
    }
}