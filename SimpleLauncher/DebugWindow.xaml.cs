using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Services.DebugAndBugReport;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Window that displays real-time debug log output.
/// </summary>
public partial class DebugWindow
{
    private static readonly Lock InstanceLock = new();
    private bool _isReallyClosing;
    private PropertyChangedEventHandler? _logTextPropertyChangedHandler;
    private DebugViewModel _viewModel = null!;

    /// <summary>
    ///     Initializes the window XAML and applies the current app theme.
    ///     NOTE: This constructor is required — without it WPF never calls InitializeComponent()
    ///     and the window opens empty (lost during the Serilog/DI refactor in d206277b).
    /// </summary>
    public DebugWindow()
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);
    }

    /// <summary>
    ///     Gets the current singleton instance of the debug window, or <c>null</c> when it has not been created.
    /// </summary>
    internal static DebugWindow? Instance { get; private set; }

    /// <summary>
    ///     Creates and shows the singleton debug window, wiring it to the <see cref="DebugViewModel" /> and auto-scrolling
    ///     the log text box whenever new output arrives. If the window already exists it is restored and activated instead.
    /// </summary>
    internal static void Initialize()
    {
        DebugWindow window;
        lock (InstanceLock)
        {
            if (Instance != null)
            {
                window = Instance;
            }
            else
            {
                var viewModel = App.ServiceProvider.GetRequiredService<DebugViewModel>();

                var created = new DebugWindow
                {
                    _viewModel = viewModel,
                    DataContext = viewModel
                };

                // NOTE: deliberately NOT setting window.Owner. An owned window is pinned above
                // its owner by Win32, which made the debug window float permanently in front of
                // the main window and other apps' windows. Keep it a normal top-level window.

                PropertyChangedEventHandler logTextPropertyChangedHandler = (_, args) =>
                {
                    if (string.Equals(args.PropertyName, nameof(DebugViewModel.LogText), StringComparison.Ordinal))
                    {
                        if (Instance is { IsLoaded: true } debugWindow)
                        {
                            debugWindow.Dispatcher.BeginInvoke(() =>
                            {
                                if (debugWindow.IsLoaded) debugWindow.LogTextBox?.ScrollToEnd();
                            });
                        }
                    }
                };

                // Subscribed exactly once per window lifetime; ShutdownWindow unsubscribes
                // before teardown so recreates never stack handlers (WPF-18).
                viewModel.PropertyChanged += logTextPropertyChangedHandler;

                created._viewModel = viewModel;
                created._logTextPropertyChangedHandler = logTextPropertyChangedHandler;
                Instance = created;
                window = created;
            }
        }

        // Show outside the lock: Show() pumps window messages, and any reentrant code
        // must be able to take InstanceLock (System.Threading.Lock is non-reentrant) (WPF-18).
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>
    ///     Shows the debug window, creating it if necessary, or brings it to the foreground if already open.
    /// </summary>
    public static void ShowDebugWindow()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher?.CheckAccess() == false)
        {
            dispatcher.Invoke(ShowDebugWindow);
            return;
        }

        Initialize();
    }

    /// <summary>
    ///     Detaches the view model event handler, disconnects the debug log sink, closes the singleton debug window
    ///     and clears the cached instance.
    /// </summary>
    internal static void ShutdownWindow()
    {
        lock (InstanceLock)
        {
            switch (Instance)
            {
                case null:
                    return;
                case { _logTextPropertyChangedHandler: not null }:
                    Instance._viewModel.PropertyChanged -= Instance._logTextPropertyChangedHandler;
                    Instance._logTextPropertyChangedHandler = null;
                    break;
            }

            DebugWindowSink.Disconnect();
            Instance._isReallyClosing = true;
            Instance.Close();
            Instance = null;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isReallyClosing)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}