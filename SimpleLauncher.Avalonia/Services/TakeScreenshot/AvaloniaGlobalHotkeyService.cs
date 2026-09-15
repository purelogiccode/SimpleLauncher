// ReSharper disable once RedundantUsingDirective

using System.Runtime.InteropServices;

#if WINDOWS
namespace SimpleLauncher.Avalonia.Services.TakeScreenshot;

/// <summary>
///     Registers a system-wide F8 hotkey and raises an event when it is pressed.
///     Windows-only port of the WPF GlobalHotkeyService (net10.0-windows TFM).
///     Uses Win32 RegisterHotKey/UnregisterHotKey with a hidden message-only window,
///     so it does not depend on the host UI framework's message pump.
/// </summary>
public sealed partial class AvaloniaGlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 9001;
    private const uint VkF8 = 0x77;
    private const int GwlWndProc = -4;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly ILogger _logger;
    private IntPtr _hwnd;
    private bool _isDisposed;
    private WndProcDelegate? _wndProcDelegate;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaGlobalHotkeyService" />.</summary>
    /// <param name="logger">The logger instance.</param>
    public AvaloniaGlobalHotkeyService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Whether the F8 global hotkey was successfully registered.
    /// </summary>
    public bool IsRegistered { get; private set; }

    /// <summary>
    ///     Raised when the F8 global hotkey is pressed.
    /// </summary>
    public Func<Task>? F8Pressed { get; set; }

    /// <summary>Releases resources and unregisters the global hotkey.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        try
        {
            if (_hwnd != IntPtr.Zero)
            {
                _ = UnregisterHotKey(_hwnd, HotkeyId);
                _ = DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                _logger.Debug("[AvaloniaGlobalHotkeyService] F8 hotkey unregistered");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"[AvaloniaGlobalHotkeyService] Error unregistering the hotkey: {ex.Message}");
        }

        _wndProcDelegate = null;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowExW", StringMarshalling =
        StringMarshalling.Utf16)]
    private static partial IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string? lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(IntPtr hWnd);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandleW(string? lpModuleName);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongW")]
    private static partial IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll")]
    private static partial IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    ///     Initializes the hotkey service by creating the message-only window and registering F8.
    ///     Must be called from the UI thread.
    /// </summary>
    public void Initialize()
    {
        if (_isDisposed) return;

        var hInstance = GetModuleHandleW(null);
        _wndProcDelegate = WndProc;
        _hwnd = CreateWindowEx(0, "STATIC", null, 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.Debug(
                $"[AvaloniaGlobalHotkeyService] Failed to create the message-only window. Win32 error code: {error}");
            return;
        }

        _ = SetWindowLongPtr(_hwnd, GwlWndProc, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        if (!RegisterHotKey(_hwnd, HotkeyId, 0, VkF8))
        {
            var error = Marshal.GetLastWin32Error();
            _logger.Debug($"[AvaloniaGlobalHotkeyService] Failed to register the F8 hotkey. Win32 error code: {error}");
            IsRegistered = false;
        }
        else
        {
            _logger.Debug("[AvaloniaGlobalHotkeyService] F8 hotkey registered successfully");
            IsRegistered = true;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            var handler = F8Pressed;
            if (handler != null)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await handler();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[AvaloniaGlobalHotkeyService] Error invoking the F8Pressed event");
                    }
                });

            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
#endif