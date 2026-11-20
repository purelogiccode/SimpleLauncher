using System.Windows;

namespace SimpleLauncher;

/// <summary>
///     Thin window shell for the standalone Easy Mode "Add System" dialog. The content lives in
///     the reusable <see cref="EasyModeView" /> (also embedded by the first-run wizard).
/// </summary>
internal partial class EasyModeWindow : IDisposable
{
    private readonly EasyModeView _view;
    private bool _disposed;

    public EasyModeWindow(EasyModeView view)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);

        _view = view;
        ViewHost.Content = _view;

        // Easy Mode signals a successful add: close the standalone dialog. The wizard
        // subscribes to the same event to advance to its summary step instead.
        _view.SystemAdded += OnSystemAdded;
        Closed += OnClosed;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _view.SystemAdded -= OnSystemAdded;
        _view.Dispose();
    }

    private void OnSystemAdded(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Dispose();
    }
}
