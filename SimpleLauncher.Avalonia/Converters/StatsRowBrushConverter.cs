using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Paints a Global Stats row green when the ROM and cover-image counts match and red
///     when they do not (WPF GlobalStatsWindow row DataTriggers parity). The fill is
///     translucent so it works on both the light and dark base themes.
/// </summary>
public class StatsRowBrushConverter : IValueConverter
{
    private static readonly IBrush EqualBrush = new SolidColorBrush(Color.Parse("#332E7D32"));
    private static readonly IBrush NotEqualBrush = new SolidColorBrush(Color.Parse("#33C62828"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? EqualBrush : NotEqualBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
