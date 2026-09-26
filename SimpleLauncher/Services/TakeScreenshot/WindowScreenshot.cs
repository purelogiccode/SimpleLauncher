using System.Runtime.InteropServices;

namespace SimpleLauncher.Services.TakeScreenshot;

/// <summary>
///     Provides methods to capture screenshots of specific windows using Win32 API calls.
/// </summary>
public static partial class WindowScreenshot
{
    private static ILogger? _logger;

    private static ILogger Logger => _logger ?? Log.Logger;

    /// <summary>Initializes the WindowScreenshot with a debug logger instance.</summary>
    public static void Initialize(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Gets the bounding rectangle of a window including its borders.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hWnd,
        out Models.WindowScreenshot.Rectangle lpRectangle);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(IntPtr hWnd,
        out Models.WindowScreenshot.Rectangle lpRectangle);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(IntPtr hWnd, ref Models.WindowScreenshot.Point lpPoint);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    /// <summary>
    ///     Gets the rectangle of the client area (excluding borders and menu).
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <param name="clientRectangle">The rectangle of the client area in screen coordinates.</param>
    /// <returns>True if the client area was successfully retrieved, false otherwise.</returns>
    public static bool GetClientAreaRect(IntPtr hWnd,
        out Models.WindowScreenshot.Rectangle clientRectangle)
    {
        clientRectangle = new Models.WindowScreenshot.Rectangle();

        // Check if the window is minimized (iconic)
        if (IsIconic(hWnd))
        {
            Logger.Debug($"[WindowScreenshot] Window {hWnd} is iconic (minimized). Cannot get client area.");
            return false; // Indicate failure for minimized windows
        }

        // Get the client area dimensions
        if (!GetClientRect(hWnd, out var localClientRect)) return false;

        // Get the top-left corner of the client area in screen coordinates
        var clientTopLeft = new Models.WindowScreenshot.Point
            { X = localClientRect.Left, Y = localClientRect.Top };
        if (!ClientToScreen(hWnd, ref clientTopLeft))
        {
            Logger.Debug($"[WindowScreenshot] ClientToScreen failed for window {hWnd}.");
            return false;
        }

        // Calculate the client area rectangle in screen coordinates
        clientRectangle.Left = clientTopLeft.X;
        clientRectangle.Top = clientTopLeft.Y;
        clientRectangle.Right = clientTopLeft.X + (localClientRect.Right - localClientRect.Left);
        clientRectangle.Bottom = clientTopLeft.Y + (localClientRect.Bottom - localClientRect.Top);

        return true;
    }
}