using SimpleLauncher.Core.Interfaces;
// ReSharper disable once RedundantUsingDirective
using System.Runtime.InteropServices;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     A utility class to extract icons from executable files.
/// </summary>
internal class IconExtractor : IIconExtractor
{
    /// <summary>
    ///     Extracts the first icon from an executable and saves it as a PNG file.
    /// </summary>
    /// <param name="exePath">The path to the executable file.</param>
    /// <param name="savePath">The path where the PNG icon should be saved.</param>
    /// <param name="logErrors">The logger for recording errors.</param>
    public void SaveIconFromExe(string exePath, string savePath, ILogger logErrors)
    {
#if WINDOWS
        if (!File.Exists(exePath) || !savePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;

        var hIcon = IntPtr.Zero;
        try
        {
            hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
            if (hIcon != IntPtr.Zero)
            {
                using var icon = Icon.FromHandle(hIcon);
                using var bmp = icon.ToBitmap();

                // Ensure the directory exists before saving
                var directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                bmp.Save(savePath, ImageFormat.Png);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Expected condition: image directory is missing or protected (e.g. app in Program Files).
            // Log at Information level so the bug report API does not pick it up (see bugs 66182-66188).
            logErrors.Information(ex, $"Cannot save icon: image directory is missing or protected. Source: {exePath}");
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, $"Failed to extract icon from {exePath}");
        }
        finally
        {
            // According to documentation, do not call DestroyIcon on an icon retrieved by ExtractIcon.
            // However, some sources suggest it's necessary to avoid leaks.
            // A check for non-zero handle is a safe practice.
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        }
#else
        // Icon extraction uses System.Drawing (Windows-only); no-op on other platforms.
#endif
    }
#if WINDOWS
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
#endif
}