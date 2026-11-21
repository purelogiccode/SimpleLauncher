// ReSharper disable once RedundantUsingDirective

using System.Drawing;
// ReSharper disable once RedundantUsingDirective
using System.Drawing.Imaging;
// ReSharper disable once RedundantUsingDirective
using ILogger = Serilog.ILogger;

#if WINDOWS
namespace SimpleLauncher.Avalonia.Services.TakeScreenshot;

/// <summary>
///     Captures a region of the screen into a PNG file (Windows-only).
///     Mirrors the capture pattern used by <see cref="AvaloniaActiveWindowScreenshotService" />.
/// </summary>
internal static class AvaloniaWindowCapture
{
    /// <summary>
    ///     Captures the given screen rectangle and saves it as a PNG file.
    /// </summary>
    /// <param name="left">Left coordinate of the rectangle in screen pixels.</param>
    /// <param name="top">Top coordinate of the rectangle in screen pixels.</param>
    /// <param name="width">Width of the rectangle in pixels.</param>
    /// <param name="height">Height of the rectangle in pixels.</param>
    /// <param name="screenshotPath">Destination PNG file path.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public static void CaptureRectangleToPng(int left, int top, int width, int height, string screenshotPath,
        ILogger logger)
    {
        using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                new Point(left, top),
                Point.Empty,
                new Size(width, height));

            bitmap.Save(screenshotPath, ImageFormat.Png);
        }

        logger.Debug("[AvaloniaWindowCapture] Screenshot saved: {Path}", screenshotPath);
    }
}
#endif