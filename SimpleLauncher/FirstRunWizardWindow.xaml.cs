using System.ComponentModel;
using SimpleLauncher.Core.Services.FirstRun;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     First-run wizard (P1-33) replacing the old Yes/No prompt + separate Easy Mode dialog.
///     Step 2 hosts the reusable <see cref="EasyModeView" />; its success/failure events drive
///     the shared wizard state so the summary and error banner stay in sync.
/// </summary>
internal partial class FirstRunWizardWindow : IDisposable
{
    private readonly EasyModeView _easyModeView;
    private readonly FirstRunWizardViewModel _viewModel;
    private bool _disposed;

    public FirstRunWizardWindow(FirstRunWizardViewModel viewModel, EasyModeView easyModeView)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _viewModel = viewModel;
        _easyModeView = easyModeView;
        DataContext = _viewModel;

        _easyModeView.ShowHeader = false;
        _easyModeView.SystemAdded += OnSystemAdded;
        _easyModeView.OperationFailed += OnOperationFailed;
        _easyModeView.OperationSucceeded += OnOperationSucceeded;

        _viewModel.RequestClose = () => Dispatcher.InvokeAsync(Close);
        _viewModel.State.PropertyChanged += OnStatePropertyChanged;
        Closed += OnClosed;
    }

    /// <summary>True when the wizard added a system and the main window must refresh.</summary>
    public bool SystemAdded => _viewModel.SystemAdded;

    /// <summary>
    ///     Adds the Easy Mode view the first time the Add System step is shown. The view's
    ///     Loaded handler performs the actual configuration load, so the Welcome page stays
    ///     instant and skipping the wizard never fetches the catalog.
    /// </summary>
    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(FirstRunWizardState.CurrentStep), StringComparison.Ordinal))
            return;

        if (_viewModel.IsAddSystemStep)
        {
            if (EasyModeHost.Content == null) EasyModeHost.Content = _easyModeView;
            return;
        }

        // Leaving Add System (Back/Skip/success): cancel a component download instead of
        // letting it keep running behind the Welcome/Finish pages.
        _easyModeView.CancelActiveDownload();
    }

    private void OnSystemAdded(object? sender, EventArgs e)
    {
        _viewModel.State.MarkSystemAdded(_easyModeView.LastAddedSystemName);
    }

    private void OnOperationFailed(object? sender, string message)
    {
        _viewModel.State.ReportError(message);
    }

    private void OnOperationSucceeded(object? sender, EventArgs e)
    {
        _viewModel.State.ClearError();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _viewModel.State.PropertyChanged -= OnStatePropertyChanged;
        _viewModel.RequestClose = null;

        _easyModeView.SystemAdded -= OnSystemAdded;
        _easyModeView.OperationFailed -= OnOperationFailed;
        _easyModeView.OperationSucceeded -= OnOperationSucceeded;
        _easyModeView.Dispose();

        _viewModel.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Dispose();
    }
}
