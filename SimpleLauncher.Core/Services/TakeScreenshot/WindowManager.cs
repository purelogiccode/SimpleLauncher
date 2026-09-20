#if WINDOWS
using System.Runtime.InteropServices;

namespace SimpleLauncher.Core.Services.TakeScreenshot;

/// <summary>
///     Provides methods to enumerate visible open windows using Win32 API calls.
///     Windows-only: the file is compiled out of the net10.0 (Linux/macOS) assembly so
///     the unguarded user32.dll P/Invokes can never be reached there (LB-12).
/// </summary>
public static class WindowManager
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    ///     Returns a list of all visible open windows with their handles and titles.
    /// </summary>
    public static IList<(IntPtr Handle, string Title)> GetOpenWindows()
    {
        var windows = new List<(IntPtr, string)>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var length = GetWindowTextLength(hWnd);
            if (length <= 0) return true;

            var buffer = new char[length + 1];
            var result = GetWindowText(hWnd, buffer, buffer.Length);
            if (result > 0) windows.Add((hWnd, new string(buffer, 0, result)));

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
#endif