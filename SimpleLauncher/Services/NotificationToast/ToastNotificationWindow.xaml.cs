using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimpleLauncher.Services.NotificationToast;

/// <summary>
///     Non-activating overlay window that displays a toast notification in the
///     bottom-right corner of the screen and dismisses itself after a few seconds.
/// </summary>
public partial class ToastNotificationWindow : IDisposable
{
    private const int DisplayDurationMs = 6000;

    private readonly DispatcherTimer _dismissTimer;
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToastNotificationWindow" /> class.
    /// </summary>
    public ToastNotificationWindow()
    {
        InitializeComponent();

        _dismissTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(DisplayDurationMs)
        };
        _dismissTimer.Tick += DismissTimer_Tick;
    }

    /// <summary>
    ///     Disposes the dismiss timer and suppresses finalization.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _dismissTimer.Stop();
        _dismissTimer.Tick -= DismissTimer_Tick;

        // Close can throw when the window is already closing/closed (WPF-19).
        try
        {
            Close();
        }
        catch (InvalidOperationException ex)
        {
            Log.Debug($"ToastNotificationWindow Dispose: Close failed: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Shows the toast with the given title and message, replacing any previous toast.
    /// </summary>
    /// <param name="title">The toast title.</param>
    /// <param name="message">The toast message.</param>
    public void ShowToast(string title, string message)
    {
        if (_isDisposed) return;

        // Marshal background-thread toasts to the UI thread instead of dropping them (WPF-19).
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() => ShowToast(title, message));
            }
            catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
            {
                Log.Debug($"ToastNotificationWindow ShowToast dropped during shutdown: {ex.Message}");
            }

            return;
        }

        _dismissTimer.Stop();

        ToastTitleTextBlock.Text = title;
        ToastMessageTextBlock.Text = message;

        Show();
        UpdateLayout();

        // Position in the bottom-right corner of the primary screen's working area
        var workingArea = SystemParameters.WorkArea;
        var width = Math.Min(MaxWidth, workingArea.Width * 0.9);
        var height = Math.Min(ActualHeight, workingArea.Height * 0.9);

        Left = workingArea.Right - width - 16;
        Top = workingArea.Bottom - height - 16;

        ActivateToastAnimation();
        _dismissTimer.Start();
    }

    private void DismissTimer_Tick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();

        var fadeOut = new DoubleAnimation(Opacity, 0, new Duration(TimeSpan.FromMilliseconds(300)))
        {
            // Stop (not HoldEnd): a held clock stays in the timing tree and roots this
            // window via the Completed closure (WPF-19).
            FillBehavior = FillBehavior.Stop
        };
        EventHandler? completed = null;
        completed = (_, _) =>
        {
            fadeOut.Completed -= completed;
            Hide();
        };
        fadeOut.Completed += completed;
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ActivateToastAnimation()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(250)));
        ToastBorder.BeginAnimation(OpacityProperty, fadeIn);
    }
}