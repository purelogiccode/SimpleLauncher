using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Converts a system-info line's IsError flag to the brush used to render it:
///     NotificationErrorBrush for invalid folders/emulator paths, TextPrimaryBrush
///     otherwise (WPF DisplaySystemInformation red Run parity).
/// </summary>
public class SystemInfoLineBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isError) return AvaloniaProperty.UnsetValue;

        var key = isError ? "NotificationErrorBrush" : "TextPrimaryBrush";
        if (Application.Current is { } app)
        {
            var found = app.TryFindResource(key, out var resource);
            if (found && resource is IBrush brush) return brush;
        }

        // No application resources (unit tests): fall back to the plain colors.
        return isError ? Brushes.Red : Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
