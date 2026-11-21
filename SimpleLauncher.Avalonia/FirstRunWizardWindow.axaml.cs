using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Avalonia.Views;
using SimpleLauncher.Core.Services.FirstRun;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     First-run wizard (P1-33) that replaces the old Yes/No prompt + separate Easy Mode dialog.
///     Step 2 embeds <see cref="EasyModeView" /> with the shared <see cref="EasyModeViewModel" />,
///     lazily created the first time the Add System step is shown.
/// </summary>
internal partial class FirstRunWizardWindow : Window, IDisposable
{
    private readonly FirstRunWizardViewModel _viewModel;
    private bool _disposed;
    private EasyModeView? _easyModeView;

    public FirstRunWizardWindow(FirstRunWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        _viewModel.RequestClose = () => Dispatcher.UIThread.InvokeAsync(Close);
        _viewModel.State.PropertyChanged += OnStatePropertyChanged;
    }

    /// <summary>True when the wizard added a system and the main window must refresh.</summary>
    public bool SystemAdded => _viewModel.SystemAdded;

    /// <summary>
    ///     Creates and loads the Easy Mode view when the Add System step is first shown. Loading
    ///     lazily keeps the Welcome page instant and avoids a network fetch when the user skips.
    /// </summary>
    private async void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (!string.Equals(e.PropertyName, nameof(FirstRunWizardState.CurrentStep), StringComparison.Ordinal))
                return;

            if (!_viewModel.IsAddSystemStep)
            {
                // Leaving Add System (Back/Skip/success): cancel a component download instead of
                // letting it keep running behind the Welcome/Finish pages.
                if (_viewModel.EasyMode.CanStopDownload)
                    _viewModel.EasyMode.StopDownloadCommand.Execute(null);
                return;
            }

            await ShowEasyModeAsync();
        }
        catch (Exception ex)
        {
            // async void event handlers must never let an exception escape to the dispatcher.
            Log.Error(ex, "First-run wizard failed to show the Easy Mode step");
            _viewModel.State.ReportError(ex.Message);
        }
    }

    private async Task ShowEasyModeAsync()
    {
        if (_easyModeView != null) return;

        _easyModeView = new EasyModeView
        {
            // The wizard header already names the step; hide the Easy Mode title/description.
            ShowHeader = false,
            DataContext = _viewModel.EasyMode
        };

        _viewModel.EasyMode.RequestClose = () =>
            _viewModel.State.MarkSystemAdded(_viewModel.EasyMode.LastAddedSystemName);

        EasyModeHost.Content = _easyModeView;
        await _viewModel.EasyMode.LoadCommand.ExecuteAsync(null);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _viewModel.State.PropertyChanged -= OnStatePropertyChanged;
        _viewModel.RequestClose = null;
        _viewModel.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }
}
