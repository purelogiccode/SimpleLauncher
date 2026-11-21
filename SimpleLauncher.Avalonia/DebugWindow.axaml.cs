using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window that displays real-time debug log output.
/// </summary>
internal partial class DebugWindow : Window
{
    private static readonly Lock InstanceLock = new();
    private bool _isReallyClosing;
    private PropertyChangedEventHandler? _logTextPropertyChangedHandler;
    private DebugViewModel _viewModel = null!;

    /// <summary>
    ///     Initializes the window XAML.
    /// </summary>
    public DebugWindow()
    {
        InitializeComponent();
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
        lock (InstanceLock)
        {
            if (Instance != null)
            {
                Instance.Show();
                Instance.WindowState = WindowState.Normal;
                Instance.Activate();
                return;
            }

            var viewModel = App.ServiceProvider.GetRequiredService<DebugViewModel>();

            var window = new DebugWindow
            {
                _viewModel = viewModel,
                DataContext = viewModel
            };

            PropertyChangedEventHandler logTextPropertyChangedHandler = (_, args) =>
            {
                if (string.Equals(args.PropertyName, nameof(DebugViewModel.LogText), StringComparison.Ordinal))
                {
                    if (Instance is { IsLoaded: true } debugWindow)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (debugWindow is { IsLoaded: true, LogTextBox: { } textBox })
                            {
                                // Move the caret to the end so the view scrolls to the newest line
                                textBox.CaretIndex = textBox.Text?.Length ?? 0;
                            }
                        });
                    }
                }
            };

            viewModel.PropertyChanged += logTextPropertyChangedHandler;

            window._viewModel = viewModel;
            window._logTextPropertyChangedHandler = logTextPropertyChangedHandler;
            Instance = window;

            Instance.Show();
        }
    }

    /// <summary>
    ///     Shows the debug window, creating it if necessary, or brings it to the foreground if already open.
    /// </summary>
    public static void ShowDebugWindow()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(ShowDebugWindow);
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

    protected override void OnClosing(WindowClosingEventArgs e)
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

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }
}