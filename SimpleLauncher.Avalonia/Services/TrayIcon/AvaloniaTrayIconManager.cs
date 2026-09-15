using Avalonia;
using Avalonia.Controls;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Core.Interfaces;
using TrayIconControl = Avalonia.Controls.TrayIcon;

namespace SimpleLauncher.Avalonia.Services.TrayIcon;

/// <summary>
///     Manages the system tray icon, its native context menu, and related actions for the application.
///     Cross-platform Avalonia port of the WPF TrayIconManager (Avalonia TrayIcon supports Windows and Linux).
/// </summary>
public class AvaloniaTrayIconManager : IDisposable
{
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly ILogger _logger;
    private readonly LocalizationService? _localization;
    private bool _isDisposed;

    private Window? _mainWindow;
    private TrayIconControl? _trayIcon;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaTrayIconManager" /> class.</summary>
    /// <param name="applicationLifetime">The application lifetime service for shutdown control.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="localization">The localization service used for the tray-menu labels (optional for tests).</param>
    public AvaloniaTrayIconManager(IApplicationLifetime applicationLifetime, ILogger logger,
        LocalizationService? localization = null)
    {
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _localization = localization;
    }

    /// <summary>Releases resources used by the tray icon manager.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        try
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error disposing the tray icon");
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Creates the tray icon and registers it on the application.
    ///     Must be called from the UI thread after the main window is shown.
    /// </summary>
    /// <param name="mainWindow">The main application window.</param>
    public void Initialize(Window mainWindow)
    {
        if (_isDisposed) return;

        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        _trayIcon = new TrayIconControl
        {
            ToolTipText = "Simple Launcher",
            Icon = LoadIcon(),
            Menu = CreateContextMenu(),
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => OnOpen();

        TrayIconControl.SetIcons(Application.Current!, [_trayIcon]);

        _logger.Debug("AvaloniaTrayIconManager was initialized");
    }

    private static WindowIcon? LoadIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon", "icon.ico");
            return File.Exists(iconPath) ? new WindowIcon(iconPath) : null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to load the tray icon");
            return null;
        }
    }

    private NativeMenu CreateContextMenu()
    {
        // WPF parity (TrayIconManager.CreateContextMenu): labels resolve from the
        // localized resources (keys Open / MinimizeToTray / DebugWindow / Exit).
        var menu = new NativeMenu();

        var openItem = new NativeMenuItem(LocalizedLabel("Open", "Open"));
        openItem.Click += (_, _) => OnOpen();

        var minimizeToTrayItem = new NativeMenuItem(LocalizedLabel("MinimizeToTray", "Minimize to Tray"));
        minimizeToTrayItem.Click += (_, _) => OnMinimizeToTray();

        var debugWindowItem = new NativeMenuItem(LocalizedLabel("DebugWindow", "Debug Window"));
        debugWindowItem.Click += (_, _) => OnOpenDebugWindow();

        var exitItem = new NativeMenuItem(LocalizedLabel("Exit", "Exit"));
        exitItem.Click += (_, _) => OnExit();

        menu.Items.Add(openItem);
        menu.Items.Add(minimizeToTrayItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(debugWindowItem);
        menu.Items.Add(exitItem);

        return menu;
    }

    /// <summary>
    ///     Resolves a tray-menu label from the localized resources with the WPF fallback
    ///     text. Mnemonic underscores in the resource values (e.g. the shared "Exit"
    ///     resource is "_Exit") are stripped because native tray menus do not reliably
    ///     render them cross-platform.
    /// </summary>
    private string LocalizedLabel(string key, string fallback)
    {
        var label = _localization?.GetString(key, fallback) ?? fallback;
        return label.Replace("_", "", StringComparison.Ordinal);
    }

    private void OnOpen()
    {
        if (_mainWindow is not { } window) return;

        window.ShowInTaskbar = true;
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void OnMinimizeToTray()
    {
        if (_mainWindow is not { } window) return;

        window.Hide();
        window.ShowInTaskbar = false;
    }

    private void OnOpenDebugWindow()
    {
        try
        {
            DebugWindow.ShowDebugWindow();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open the debug window from the tray menu");

            // WPF parity (TrayIconManager.OnOpenDebugWindow): notify the user via a toast.
            if (_mainWindow is MainWindow mainWindow)
                mainWindow.ShowToast("Simple Launcher", "Failed to open debug window", ToastType.Error);
        }
    }

    private void OnExit()
    {
        if (_trayIcon is not null) _trayIcon.IsVisible = false;

        _applicationLifetime.Shutdown();
    }
}