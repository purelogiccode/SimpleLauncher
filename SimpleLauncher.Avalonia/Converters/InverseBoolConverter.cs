using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Inverts a boolean value. Used for IsEnabled bindings where a "downloaded" state should disable the button.
///     Non-boolean inputs (including unset bindings) propagate
///     <see cref="AvaloniaProperty.UnsetValue" /> so broken bindings surface
///     diagnostics instead of silently flipping the UI state.
/// </summary>
public class InverseBoolConverter : IValueConverter
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
