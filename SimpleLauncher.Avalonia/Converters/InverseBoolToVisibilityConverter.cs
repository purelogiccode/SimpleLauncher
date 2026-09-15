using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Converts a boolean to collapsed (true) / IsVisible (false) — inverse of BoolToVisibilityConverter.
///     Non-boolean inputs (including unset bindings) propagate
///     <see cref="AvaloniaProperty.UnsetValue" /> so broken bindings surface
///     diagnostics instead of silently collapsing the UI.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ReferenceEquals(value, AvaloniaProperty.UnsetValue)) return AvaloniaProperty.UnsetValue;
        if (value is bool b) return !b;
        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ReferenceEquals(value, AvaloniaProperty.UnsetValue)) return AvaloniaProperty.UnsetValue;
        if (value is bool b) return !b;
        return AvaloniaProperty.UnsetValue;
    }
}
