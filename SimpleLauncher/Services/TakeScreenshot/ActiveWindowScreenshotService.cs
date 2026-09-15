using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace SimpleLauncher.Services.TakeScreenshot;

/// <summary>
///     Captures a screenshot of the currently active (foreground) window and saves it
///     to the .\screenshot folder relative to the application directory.
/// </summary>
public partial class ActiveWindowScreenshotService
{
    private readonly ILogger _logger;
    private readonly IPlaySoundEffects _playSoundEffects;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initializes a new instance of the <see cref="ActiveWindowScreenshotService" />.</summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="playSoundEffects">The sound effects service for shutter sound.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    public ActiveWindowScreenshotService(
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
            try
            {
                var hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                {
                    _logger.Debug("[ActiveWindowScreenshot] No foreground window found");
                    return Task.CompletedTask;
                }

                Models.WindowScreenshot.Rectangle rectangle;

                if (!WindowScreenshot.GetClientAreaRect(hWnd, out var clientRect))
                {
                    if (!WindowScreenshot.GetWindowRect(hWnd, out rectangle))
                    {
                        _logger.Debug("[ActiveWindowScreenshot] Failed to retrieve window dimensions");
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
                        "[ActiveWindowScreenshot] Cannot take a screenshot of a minimized or zero-size window");
                    return Task.CompletedTask;
                }

                var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshot");
                Directory.CreateDirectory(screenshotDir);

                var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                var screenshotPath = Path.Combine(screenshotDir, fileName);

                using (var bitmap =
                       new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(
                            new Point(rectangle.Left, rectangle.Top),
                            Point.Empty,
                            new Size(width, height));
                    }

                    bitmap.Save(screenshotPath, ImageFormat.Png);
                }

                _logger.Debug($"[ActiveWindowScreenshot] Screenshot saved: {screenshotPath}");

                PlaySoundAndFlash();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[ActiveWindowScreenshot] Error capturing the active window screenshot");
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private void PlaySoundAndFlash()
    {
        try
        {
            _playSoundEffects.PlayShutterSound();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var flashWindow = _serviceProvider.GetRequiredService<FlashOverlayWindow>();
                        return flashWindow.ShowFlashAsync();
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "[ActiveWindowScreenshot] Error showing flash overlay");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ActiveWindowScreenshot] Error playing shutter sound or flash");
        }
    }
}