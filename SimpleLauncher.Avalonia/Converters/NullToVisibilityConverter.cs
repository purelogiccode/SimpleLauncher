using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Converts null to collapsed, non-null to IsVisible.
///     An unset binding propagates <see cref="AvaloniaProperty.UnsetValue" /> (it is
///     not a real value, so it must not be reported as visible) instead of silently
///     collapsing the UI without diagnostics.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ReferenceEquals(value, AvaloniaProperty.UnsetValue)) return AvaloniaProperty.UnsetValue;
        return value is not null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
