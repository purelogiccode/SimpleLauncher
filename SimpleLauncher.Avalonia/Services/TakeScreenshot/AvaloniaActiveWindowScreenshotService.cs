// ReSharper disable All

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using WindowScreenshotModel = SimpleLauncher.Avalonia.Models.WindowScreenshot;

#if WINDOWS
namespace SimpleLauncher.Avalonia.Services.TakeScreenshot;

/// <summary>
///     Captures a screenshot of the currently active (foreground) window and saves it
///     to the .\screenshot folder relative to the application directory.
///     Windows-only port of the WPF ActiveWindowScreenshotService (net10.0-windows TFM).
/// </summary>
public sealed partial class AvaloniaActiveWindowScreenshotService
{
    private readonly ILogger _logger;
    private readonly IPlaySoundEffects _playSoundEffects;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaActiveWindowScreenshotService" />.</summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="playSoundEffects">The sound effects service for the shutter sound.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    public AvaloniaActiveWindowScreenshotService(
        ILogger logger,
        IPlaySoundEffects playSoundEffects,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _playSoundEffects = playSoundEffects ?? throw new ArgumentNullException(nameof(playSoundEffects));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    /// <summary>
    ///     Captures a screenshot of the current foreground window and saves it as a PNG file
    ///     in the .\screenshot directory (relative to the application base directory).
    /// </summary>
    public Task CaptureActiveWindowAsync()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                _logger.Debug("[AvaloniaActiveWindowScreenshot] No foreground window found.");
                return Task.CompletedTask;
            }

            WindowScreenshotModel.Rectangle rectangle;

            if (!WindowScreenshot.GetClientAreaRect(hWnd, out var clientRect))
            {
                if (!WindowScreenshot.GetWindowRect(hWnd, out rectangle))
                {
                    _logger.Debug("[AvaloniaActiveWindowScreenshot] Failed to retrieve window dimensions.");
                    return Task.CompletedTask;
                }
            }
            else
            {
                rectangle = clientRect;
            }

            var width = rectangle.Right - rectangle.Left;
            var height = rectangle.Bottom - rectangle.Top;

            if (width <= 0 || height <= 0)
            {
                _logger.Debug(
                    "[AvaloniaActiveWindowScreenshot] Cannot take a screenshot of a minimized or zero-size window.");
                return Task.CompletedTask;
            }

            var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshot");
            Directory.CreateDirectory(screenshotDir);

            var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            var screenshotPath = Path.Combine(screenshotDir, fileName);

            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    new Point(rectangle.Left, rectangle.Top),
                    Point.Empty,
                    new Size(width, height));

                bitmap.Save(screenshotPath, ImageFormat.Png);
            }

            _logger.Debug($"[AvaloniaActiveWindowScreenshot] Screenshot saved: {screenshotPath}");

            PlaySoundAndFlash();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[AvaloniaActiveWindowScreenshot] Error capturing the active window screenshot.");
        }

        return Task.CompletedTask;
    }

    private void PlaySoundAndFlash()
    {
        try
        {
            _playSoundEffects.PlayShutterSound();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[AvaloniaActiveWindowScreenshot] Error playing the shutter sound.");
        }

        // The screenshot can be requested from a background thread, but Avalonia windows
        // must be created and shown on the UI thread (bug #68921). Post the flash instead
        // of resolving/constructing FlashOverlayWindow on the calling thread.
        if (Dispatcher.UIThread.CheckAccess())
        {
            _ = ShowFlashOverlayAsync();
        }
        else
        {
            _ = Dispatcher.UIThread.InvokeAsync(ShowFlashOverlayAsync);
        }
    }

    private async Task ShowFlashOverlayAsync()
    {
        try
        {
            var flashWindow = _serviceProvider.GetRequiredService<FlashOverlayWindow>();
            await flashWindow.ShowFlashAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[AvaloniaActiveWindowScreenshot] Error showing the flash overlay.");
        }
    }
}
#endif