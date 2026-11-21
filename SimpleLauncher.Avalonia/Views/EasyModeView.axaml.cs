using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Views;

/// <summary>
///     Reusable Easy Mode "Add System" content: system picker, folder, component downloads,
///     progress/cancel and the loading overlay. Hosted by <see cref="EasyModeWindow" /> for the
///     standalone dialog and by <see cref="FirstRunWizardWindow" /> as the wizard's Add System
///     step (P1-33), so both flows share one view and one <see cref="EasyModeViewModel" />.
/// </summary>
internal partial class EasyModeView : UserControl
{
    private readonly PropertyChangedEventHandler _onViewModelPropertyChanged;
    private EasyModeViewModel? _viewModel;

    public EasyModeView()
    {
        InitializeComponent();

        _onViewModelPropertyChanged = OnViewModelPropertyChanged;
        DataContextChanged += (_, _) => AttachViewModel(DataContext as EasyModeViewModel);
        AttachedToVisualTree += (_, _) => AttachViewModel(DataContext as EasyModeViewModel);
        DetachedFromVisualTree += (_, _) => AttachViewModel(null);

        // The parent may set the DataContext before the control is attached.
        AttachViewModel(DataContext as EasyModeViewModel);
    }

    /// <summary>
    ///     Hides the Easy Mode title/description when the view is embedded in the wizard,
    ///     whose own header already names the step.
    /// </summary>
    public bool ShowHeader
    {
        get => HeaderPanel.IsVisible;
        set => HeaderPanel.IsVisible = value;
    }

    private void AttachViewModel(EasyModeViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;

        if (_viewModel != null) _viewModel.PropertyChanged -= _onViewModelPropertyChanged;

        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += _onViewModelPropertyChanged;
            ApplyLoadingState();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(EasyModeViewModel.IsLoading), StringComparison.Ordinal))
            return;

        // IsLoading can be set from background tasks: marshal to the UI thread (AV-06).
        if (Dispatcher.UIThread.CheckAccess())
            ApplyLoadingState();
        else
            Dispatcher.UIThread.Post(ApplyLoadingState);
    }

    private void ApplyLoadingState()
    {
        LoadingOverlay.IsVisible = _viewModel?.IsLoading == true;
    }

    private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.Debug("Opening the folder browser dialog for the system folder");
            var filePicker = App.ServiceProvider.GetRequiredService<IFilePickerService>();
            var folder = await filePicker.OpenFolderAsync("Choose a folder with ROMs or ISOs for this system");
            if (!string.IsNullOrEmpty(folder))
            {
                Log.Information("System folder selected: {Folder}", folder);
                if (_viewModel != null) _viewModel.SystemFolderPath = folder;
            }
            else
            {
                Log.Debug("Folder browser dialog was canceled or returned no folder");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method BrowseFolder_Click");
        }
    }

    private void EmergencyOverlay_Click(object? sender, RoutedEventArgs e)
    {
        Log.Debug("[Emergency] User forced overlay dismissal in EasyModeView");
        _viewModel?.StopDownloadCommand.Execute(null);
        LoadingOverlay.IsVisible = false;
    }
}
