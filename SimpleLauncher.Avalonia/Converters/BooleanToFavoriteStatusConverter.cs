using System.Globalization;
using Avalonia.Data.Converters;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Converts a boolean value to a localized string indicating favorite status for accessibility.
///     Avalonia port of the WPF BooleanToFavoriteStatusConverter.
/// </summary>
internal class BooleanToFavoriteStatusConverter : IValueConverter
{
    private static LocalizationService? _localization;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var localization = GetLocalizationService();
        if (value is bool isFavorite)
        {
            return isFavorite
                ? Localized("FavoriteStatusLabel", "Favorite")
                : Localized("NotFavoriteStatusLabel", "Not Favorite");
        }

        return Localized("UnknownFavoriteStatusLabel", "Unknown Favorite Status");

        string Localized(string key, string fallback)
        {
            var result = localization?.GetString(key);
            return string.IsNullOrEmpty(result) || string.Equals(result, key, StringComparison.OrdinalIgnoreCase)
                ? fallback
                : result;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public static void SetLocalizationService(LocalizationService service)
    {
        _localization = service;
    }

    private static LocalizationService? GetLocalizationService()
    {
        // Fast path: set by MainWindow (or tests) during initialization.
        if (_localization is not null) return _localization;

        // Fallback: resolve the DI singleton on first use.
        _localization = App.ServiceProvider?.GetService<LocalizationService>();
        return _localization;
    }
}