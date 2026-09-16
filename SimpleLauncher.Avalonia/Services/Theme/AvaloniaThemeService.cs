using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace SimpleLauncher.Avalonia.Services.Theme;

/// <summary>
///     Applies base theme + accent color at runtime by overriding the
///     <c>Color</c> resources consumed by <c>Themes/DarkTheme.axaml</c>.
///     Parity with the WPF <c>ThemeMenuService</c> (5 base themes + 27 accents).
/// </summary>
public static class AvaloniaThemeService
{
    public static readonly IReadOnlyList<string> BaseThemeNames =
        ["Light", "Dark", "Adaptive", "HighContrast", "Midnight"];

    public static readonly IReadOnlyList<string> AccentColorNames =
    [
        "Amber", "Blue", "Brown", "Cobalt", "Crimson", "Cyan", "Emerald",
        "Green", "Indigo", "Lime", "Magenta", "Maroon", "Mauve", "Olive",
        "OliveDrab", "Orange", "Pink", "Plum", "Purple", "Red", "Sienna",
        "SkyBlue", "Steel", "Taupe", "Teal", "Violet", "Yellow"
    ];

    private static readonly Dictionary<string, string> AccentHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Amber"] = "#FFC200", ["Blue"] = "#0078D7", ["Brown"] = "#825A2C",
        ["Cobalt"] = "#0050EF", ["Crimson"] = "#DC143C", ["Cyan"] = "#1BA1E2",
        ["Emerald"] = "#008A00", ["Green"] = "#107C10", ["Indigo"] = "#1B3C97",
        ["Lime"] = "#A4C400", ["Magenta"] = "#D80073", ["Maroon"] = "#7C0000",
        ["Mauve"] = "#76608A", ["Olive"] = "#6B6B00", ["OliveDrab"] = "#5B6640",
        ["Orange"] = "#E6790B", ["Pink"] = "#E5006D", ["Plum"] = "#73004C",
        ["Purple"] = "#6A00FF", ["Red"] = "#CC0000", ["Sienna"] = "#9C5400",
        ["SkyBlue"] = "#87CEEB", ["Steel"] = "#647687", ["Taupe"] = "#87794E",
        ["Teal"] = "#008080", ["Violet"] = "#AA40FF", ["Yellow"] = "#FFD800"
    };

    // Color resource keys defined in Themes/DarkTheme.axaml (surfaces, text, borders, etc.)
    private static readonly string[] PaletteKeys =
    {
        "BgPrimaryColor", "BgSecondaryColor", "BgTertiaryColor", "BgQuaternaryColor",
        "TextPrimaryColor", "TextSecondaryColor", "TextMutedColor",
        "BorderSubtleColor", "BorderNormalColor",
        "PlaceholderFillColor", "PlaceholderStrokeColor",
        "OverlayProcessingColor", "LinkColor", "ShadowColor",
        "NotificationInfoColor", "NotificationSuccessColor", "NotificationWarningColor",
        "NotificationErrorColor", "FavoriteHeartColor", "SelectionBackgroundColor"
    };

    private static readonly Dictionary<string, string> DarkPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BgPrimaryColor"] = "#1E1E1E", ["BgSecondaryColor"] = "#252525",
        ["BgTertiaryColor"] = "#2E2E2E", ["BgQuaternaryColor"] = "#383838",
        ["TextPrimaryColor"] = "#FFFFFF", ["TextSecondaryColor"] = "#98989D",
        ["TextMutedColor"] = "#6E6E73",
        ["BorderSubtleColor"] = "#2A2A2D", ["BorderNormalColor"] = "#3A3A3D",
        ["PlaceholderFillColor"] = "#15FFFFFF", ["PlaceholderStrokeColor"] = "#1AFFFFFF",
        ["OverlayProcessingColor"] = "#B3000000", ["LinkColor"] = "#526BA0",
        ["ShadowColor"] = "#000000",
        ["NotificationInfoColor"] = "#0A84FF", ["NotificationSuccessColor"] = "#30D158",
        ["NotificationWarningColor"] = "#FF9F0A", ["NotificationErrorColor"] = "#FF453A",
        ["FavoriteHeartColor"] = "#FF6B6B", ["SelectionBackgroundColor"] = "#0A84FF"
    };

    private static readonly Dictionary<string, string> LightPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BgPrimaryColor"] = "#FFFFFF", ["BgSecondaryColor"] = "#F3F3F3",
        ["BgTertiaryColor"] = "#E8E8E8", ["BgQuaternaryColor"] = "#DADADA",
        ["TextPrimaryColor"] = "#1E1E1E", ["TextSecondaryColor"] = "#505050",
        ["TextMutedColor"] = "#767676",
        ["BorderSubtleColor"] = "#D0D0D0", ["BorderNormalColor"] = "#B0B0B0",
        ["PlaceholderFillColor"] = "#15000000", ["PlaceholderStrokeColor"] = "#1A000000",
        ["OverlayProcessingColor"] = "#B3000000", ["LinkColor"] = "#1A5FB4",
        ["ShadowColor"] = "#000000",
        ["NotificationInfoColor"] = "#0A84FF", ["NotificationSuccessColor"] = "#30D158",
        ["NotificationWarningColor"] = "#FF9F0A", ["NotificationErrorColor"] = "#FF453A",
        ["FavoriteHeartColor"] = "#D12733", ["SelectionBackgroundColor"] = "#0A84FF"
    };

    private static readonly Dictionary<string, string> HighContrastPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BgPrimaryColor"] = "#000000", ["BgSecondaryColor"] = "#000000",
        ["BgTertiaryColor"] = "#1A1A1A", ["BgQuaternaryColor"] = "#333333",
        ["TextPrimaryColor"] = "#FFFFFF", ["TextSecondaryColor"] = "#FFFFFF",
        ["TextMutedColor"] = "#CCCCCC",
        ["BorderSubtleColor"] = "#FFFFFF", ["BorderNormalColor"] = "#FFFFFF",
        ["PlaceholderFillColor"] = "#22FFFFFF", ["PlaceholderStrokeColor"] = "#55FFFFFF",
        ["OverlayProcessingColor"] = "#CC000000", ["LinkColor"] = "#00FFFF",
        ["ShadowColor"] = "#000000",
        ["NotificationInfoColor"] = "#00FFFF", ["NotificationSuccessColor"] = "#00FF00",
        ["NotificationWarningColor"] = "#FFFF00", ["NotificationErrorColor"] = "#FF0000",
        ["FavoriteHeartColor"] = "#FFFF00", ["SelectionBackgroundColor"] = "#555555"
    };

    private static readonly Dictionary<string, string> MidnightPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BgPrimaryColor"] = "#000B1A", ["BgSecondaryColor"] = "#00142E",
        ["BgTertiaryColor"] = "#00224D", ["BgQuaternaryColor"] = "#00316E",
        ["TextPrimaryColor"] = "#FFFFFF", ["TextSecondaryColor"] = "#B0C4DE",
        ["TextMutedColor"] = "#6A8BB5",
        ["BorderSubtleColor"] = "#004080", ["BorderNormalColor"] = "#0066CC",
        ["PlaceholderFillColor"] = "#150066CC", ["PlaceholderStrokeColor"] = "#330066CC",
        ["OverlayProcessingColor"] = "#B3000B1A", ["LinkColor"] = "#4A90D9",
        ["ShadowColor"] = "#000000",
        ["NotificationInfoColor"] = "#4A90D9", ["NotificationSuccessColor"] = "#30D158",
        ["NotificationWarningColor"] = "#FF9F0A", ["NotificationErrorColor"] = "#FF453A",
        ["FavoriteHeartColor"] = "#FF6B8A", ["SelectionBackgroundColor"] = "#00316E"
    };

    /// <summary>
    ///     Applies the base theme and accent color. Unknown base themes fall back to Dark;
    ///     unknown accents fall back to Blue. <c>Adaptive</c> follows the OS theme.
    /// </summary>
    public static void ApplyTheme(string? baseTheme, string? accentColor)
    {
        if (Application.Current is null) return;

        var effectiveBase = string.IsNullOrWhiteSpace(baseTheme) ? "Dark" : baseTheme;
        var effectiveAccent = string.IsNullOrWhiteSpace(accentColor) ? "Blue" : accentColor;

        var palette = effectiveBase switch
        {
            "Light" => LightPalette,
            "HighContrast" => HighContrastPalette,
            "Midnight" => MidnightPalette,
            _ => DarkPalette
        };

        foreach (var key in PaletteKeys)
        {
            if (palette.TryGetValue(key, out var hex))
                Application.Current.Resources[key] = Color.Parse(hex);
        }

        if (!AccentHex.TryGetValue(effectiveAccent, out var accentHex)) accentHex = AccentHex["Blue"];

        var accent = Color.Parse(accentHex);
        Application.Current.Resources["AccentColor"] = accent;
        Application.Current.Resources["AccentHoverColor"] = Lighten(accent, 0.15);
        Application.Current.Resources["AccentPressedColor"] = Darken(accent, 0.15);
        Application.Current.Resources["AccentDisabledColor"] = WithAlpha(accent, 0x66);
        Application.Current.Resources["SelectionRingColor"] = accent;

        Application.Current.RequestedThemeVariant = effectiveBase switch
        {
            "Light" => ThemeVariant.Light,
            "Adaptive" => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }

    private static Color Lighten(Color c, double amount)
    {
        return Color.FromArgb(c.A, L(c.R), L(c.G), L(c.B));

        byte L(byte v)
        {
            return (byte)Math.Min(255, v + ((255 - v) * amount));
        }
    }

    private static Color Darken(Color c, double amount)
    {
        return Color.FromArgb(c.A, D(c.R), D(c.G), D(c.B));

        byte D(byte v)
        {
            return (byte)(v * (1 - amount));
        }
    }

    private static Color WithAlpha(Color c, byte a)
    {
        return Color.FromArgb(a, c.R, c.G, c.B);
    }
}