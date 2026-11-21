using System.Globalization;
using Avalonia.Data.Converters;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Normalizes game titles: all-uppercase → Title Case, all-lowercase → Title Case, mixed case → as-is.
/// </summary>
internal class SmartTitleCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string title || string.IsNullOrWhiteSpace(title))
            return value;

        var isAllUpper = title.All(c => !char.IsLetter(c) || char.IsUpper(c));
        var isAllLower = title.All(c => !char.IsLetter(c) || char.IsLower(c));

        if (isAllUpper || isAllLower)
        {
            // Always use InvariantCulture: game titles are ASCII-dominated, and
            // culture-sensitive casing (e.g. Turkish dotted/dotless I) could
            // otherwise corrupt the title depending on the UI culture.
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title.ToLowerInvariant());
        }

        return title;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}