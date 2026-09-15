using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Converts (cardWidth, systemName, isMixedView) → card height using SystemArtRatioService.
///     Used in MultiBinding for the game card DataTemplate.
/// </summary>
public class ConsoleToCardHeightConverter : IMultiValueConverter
{
    private const double DefaultCardWidth = 168.0;
    private const double CaptionHeight = 48.0; // 48px for caption area (title + rating)
    private static readonly Lock ServiceLock = new();
    private static SystemArtRatioService? _ratioService;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return DefaultCardWidth;

        // AV-16: propagate unset bindings instead of silently falling back to the
        // default width (which produced wrong card heights with no diagnostics).
        if (ReferenceEquals(values[0], AvaloniaProperty.UnsetValue) ||
            ReferenceEquals(values[1], AvaloniaProperty.UnsetValue))
        {
            return AvaloniaProperty.UnsetValue;
        }

        if (!TryGetCardWidth(values[0], culture, out var cardWidth)) return AvaloniaProperty.UnsetValue;

        string systemName;
        if (values[1] is null)
        {
            systemName = "";
        }
        else if (values[1] is string name)
        {
            systemName = name;
        }
        else
        {
            return AvaloniaProperty.UnsetValue;
        }

        var isMixedView = values.Count > 2 && values[2] is true;

        var ratioService = GetRatioService();
        if (ratioService is null) return cardWidth * 0.73;

        var artHeight = ratioService.GetArtHeight(cardWidth, systemName, isMixedView);
        return artHeight + CaptionHeight;
    }

    public static void SetRatioService(SystemArtRatioService service)
    {
        lock (ServiceLock)
        {
            _ratioService = service;
        }
    }

    private static SystemArtRatioService? GetRatioService()
    {
        // Fast path: set by MainWindow during construction.
        lock (ServiceLock)
        {
            if (_ratioService is not null) return _ratioService;
        }

        // Fallback: resolve the DI singleton on first use so the converter never
        // silently produces wrong heights if it is used before MainWindow init.
        var resolved = App.ServiceProvider?.GetService<SystemArtRatioService>();
        if (resolved is null) return null;

        lock (ServiceLock)
        {
            _ratioService ??= resolved;
            return _ratioService;
        }
    }

    /// <summary>
    ///     Coerces the bound card width to a usable positive finite value.
    ///     Accepts any numeric binding source (double, int, float, ...) — the old
    ///     <c>as double?</c> cast silently dropped int/float bindings to 168.0.
    ///     Null (binding not yet evaluated) keeps the historical default width.
    /// </summary>
    private static bool TryGetCardWidth(object? value, CultureInfo culture, out double cardWidth)
    {
        if (value is null)
        {
            cardWidth = DefaultCardWidth;
            return true;
        }

        cardWidth = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            short s => s,
            byte b => b,
            _ => double.NaN
        };

        if (double.IsNaN(cardWidth))
        {
            try
            {
                cardWidth = System.Convert.ToDouble(value, culture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                cardWidth = double.NaN;
                return false;
            }
        }

        if (double.IsNaN(cardWidth) || double.IsInfinity(cardWidth) || cardWidth <= 0) return false;

        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
