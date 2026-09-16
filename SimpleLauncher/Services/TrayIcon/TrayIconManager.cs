using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Services.NotificationToast;

namespace SimpleLauncher.Services.TrayIcon;

/// <summary>
///     Manages the system tray icon, its context menu, and related actions for the application.
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly ILogger _logger;
    private readonly Window _mainWindow;
    private readonly RoutedEventHandler _onExitHandler;
    private readonly RoutedEventHandler _onMinimizeToTrayHandler;
    private readonly RoutedEventHandler _onOpenDebugWindowHandler;

    private readonly RoutedEventHandler _onOpenHandler;
    private readonly TaskbarIcon _taskbarIcon;
    private readonly IToastNotificationService _toastNotificationService;
    private readonly System.Windows.Controls.ContextMenu _trayMenu;
    private readonly RoutedEventHandler _trayMouseDoubleClickHandler;
    private bool _isDisposed;

    /// <summary>Initializes a new instance of the <see cref="TrayIconManager" />.</summary>
    /// <param name="mainWindow">The main application window.</param>
    /// <param name="applicationLifetime">The application lifetime service for shutdown control.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="toastNotificationService">The toast notification service.</param>
    public TrayIconManager(Window mainWindow, IApplicationLifetime applicationLifetime, ILogger logger,
        IToastNotificationService toastNotificationService)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toastNotificationService = toastNotificationService ??
                                    throw new ArgumentNullException(nameof(toastNotificationService));

        _onOpenHandler = OnOpen;
        _onMinimizeToTrayHandler = OnMinimizeToTray;
        _onExitHandler = OnExit;
        _onOpenDebugWindowHandler = OnOpenDebugWindow;
        _trayMouseDoubleClickHandler = OnOpen;

        _trayMenu = CreateContextMenu();
        _taskbarIcon = CreateTaskbarIcon();
        _taskbarIcon.TrayMouseDoubleClick += _trayMouseDoubleClickHandler;
    }

    /// <summary>Releases resources used by the tray icon manager.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        if (_taskbarIcon != null)
        {
            _taskbarIcon.TrayMouseDoubleClick -= _trayMouseDoubleClickHandler;
            _taskbarIcon.Dispose();
        }

        if (_trayMenu != null)
        {
            foreach (var item in _trayMenu.Items)
            {
                if (item is not MenuItem menuItem) continue;

                menuItem.Click -= _onOpenHandler;
                menuItem.Click -= _onMinimizeToTrayHandler;
                menuItem.Click -= _onExitHandler;
                menuItem.Click -= _onOpenDebugWindowHandler;
            }
        }

        GC.SuppressFinalize(this);
    }

    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var open = (string)Application.Current.TryFindResource("Open") ?? "Open";
        var minimizeToTray = (string)Application.Current.TryFindResource("MinimizeToTray") ?? "Minimize to Tray";
        var exit = (string)Application.Current.TryFindResource("Exit") ?? "Exit";
        var debugWindow = (string)Application.Current.TryFindResource("DebugWindow") ?? "Debug Window";

        var openMenuItem = new MenuItem
        {
            Header = open,
            Icon = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/play.png")),
                Width = 16,
                Height = 16
            }
        };
        openMenuItem.Click += _onOpenHandler;

        var minimizeToTrayMenuItem = new MenuItem
        {
            Header = minimizeToTray,
            Icon = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/shrink.png")),
                Width = 16,
                Height = 16
            }
        };
        minimizeToTrayMenuItem.Click += _onMinimizeToTrayHandler;

        var debugWindowMenuItem = new MenuItem
        {
            Header = debugWindow,
            Icon = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/bug.png")),
                Width = 16,
                Height = 16
            }
        };
        debugWindowMenuItem.Click += _onOpenDebugWindowHandler;

        var exitMenuItem = new MenuItem
        {
            Header = exit,
            Icon = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/exit.png")),
                Width = 16,
                Height = 16
            }
        };
        exitMenuItem.Click += _onExitHandler;

        menu.Items.Add(openMenuItem);
        menu.Items.Add(minimizeToTrayMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(debugWindowMenuItem);
        menu.Items.Add(exitMenuItem);

        return menu;
    }

    private TaskbarIcon CreateTaskbarIcon()
    {
        return new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/icon/icon.ico")),
            ToolTipText = "Simple Launcher",
            ContextMenu = _trayMenu,
            Visibility = Visibility.Visible
        };
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (!_mainWindow.IsLoaded)
        {
            _logger.Debug("Tray icon Open ignored: the main window has been closed");
            return;
        }

        try
        {
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
        catch (InvalidOperationException ex)
        {
            // Expected: the window closed between the IsLoaded check and Show(), e.g.
            // while the application is shutting down. Not a bug.
            _logger.Information(ex, "Tray icon Open ignored: the main window is closing or closed");
        }
    }

    private void OnMinimizeToTray(object sender, RoutedEventArgs e)
    {
        if (!_mainWindow.IsLoaded)
        {
            _logger.Debug("Tray icon MinimizeToTray ignored: the main window has been closed");
            return;
        }

        _mainWindow.Hide();
        _mainWindow.ShowInTaskbar = false;
    }

    private void OnOpenDebugWindow(object sender, RoutedEventArgs e)
    {
        try
        {
            DebugWindow.ShowDebugWindow();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open debug window from tray menu");
            _toastNotificationService.ShowToast("Simple Launcher", "Failed to open debug window");
        }
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        _taskbarIcon.Visibility = Visibility.Collapsed;
        _applicationLifetime.Shutdown();
    }
}