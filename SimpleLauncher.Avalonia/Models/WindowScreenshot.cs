using System.Runtime.InteropServices;

namespace SimpleLauncher.Avalonia.Models;

/// <summary>
///     Contains native interop structures for window screenshot capture.
///     Avalonia port of the WPF WindowScreenshot (Windows-only, net11.0-windows TFM).
/// </summary>
public static class WindowScreenshot
{
    /// <summary>
    ///     Represents a point with X and Y coordinates, matching the native POINT struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        /// <summary>
        ///     The X coordinate.
        /// </summary>
        public int X;

        /// <summary>
        ///     The Y coordinate.
        /// </summary>
        public int Y;
    }

    /// <summary>
    ///     Represents a rectangle with Left, Top, Right, and Bottom coordinates,
    ///     matching the native RECT struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rectangle
    {
        /// <summary>
        ///     The X coordinate of the left edge.
        /// </summary>
        public int Left;

        /// <summary>
        ///     The Y coordinate of the top edge.
        /// </summary>
        public int Top;

        /// <summary>
        ///     The X coordinate of the right edge.
        /// </summary>
        public int Right;

        /// <summary>
        ///     The Y coordinate of the bottom edge.
        /// </summary>
        public int Bottom;
    }
}