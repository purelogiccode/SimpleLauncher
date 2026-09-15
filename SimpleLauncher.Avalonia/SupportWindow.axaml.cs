using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for submitting support requests and bug reports.
/// </summary>
public partial class SupportWindow : Window
{
    private readonly SupportViewModel _viewModel;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SupportWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing support form logic.</param>
    /// <param name="localization">The localization service used to set localized UI strings.</param>
    public SupportWindow(SupportViewModel viewModel, LocalizationService localization)
    {
        InitializeComponent();

        _viewModel = viewModel;

        // Localize the emergency return button (WPF DynamicResource ReturnButton parity)
        EmergencyButton.Content = localization.GetString("ReturnButton");
        ToolTip.SetTip(EmergencyButton,
            localization.GetString("ClickHereIfTheLoadingScreenIsStuckToReturnToTheMainMenu"));

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.FormCleared += OnFormCleared;
        _viewModelPropertyChangedHandler = (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(SupportViewModel.IsLoading), StringComparison.Ordinal))
                SetLoadingState(_viewModel.IsLoading);
        };
        _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        Closing += (_, _) =>
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.FormCleared -= OnFormCleared;
            _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        };

        DataContext = _viewModel;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnFormCleared(object? sender, EventArgs e)
    {
        NameTextBox.Text = "";
        EmailTextBox.Text = "";
        SupportTextBox.Text = "";
    }

    /// <summary>
    ///     Toggles the loading overlay with an optional message.
    /// </summary>
    /// <param name="isLoading">Whether to show or hide the loading overlay.</param>
    public void SetLoadingState(bool isLoading)
    {
        LoadingOverlay.IsVisible = isLoading;
        MainContentGrid.IsEnabled = !isLoading;
    }

    private void EmergencyOverlayRelease_Click(object? sender, RoutedEventArgs e)
    {
        LoadingOverlay.IsVisible = false;
        MainContentGrid.IsEnabled = true;
        Log.Debug("[Emergency] User forced overlay dismissal in SupportWindow");
    }
}