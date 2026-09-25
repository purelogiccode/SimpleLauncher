using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

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

    private static readonly string[] GtkThemeSchemas =
    [
        "org.gnome.desktop.interface",
        "org.cinnamon.desktop.interface",
        "org.mate.interface",
        "org.xfce.interface"
    ];

    private static string _currentBaseTheme = "Dark";
    private static bool _adaptiveVariantHookAttached;
    private static ThemeVariant _lastAppliedVariant = ThemeVariant.Default;
    private static Timer? _adaptiveThemePollTimer;

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
    ///     unknown accents fall back to Blue. <c>Adaptive</c> follows the machine theme on
    ///     Windows and Linux: Avalonia's platform settings expose the OS light/dark preference
    ///     (Windows apps theme, GTK/XSETTINGS on Linux, macOS appearance), the matching palette
    ///     is applied, and a runtime OS theme change re-applies it while <c>Adaptive</c> is selected.
    /// </summary>
    public static void ApplyTheme(string? baseTheme, string? accentColor)
    {
        if (Application.Current is null) return;

        var effectiveBase = string.IsNullOrWhiteSpace(baseTheme) ? "Dark" : baseTheme;
        var effectiveAccent = string.IsNullOrWhiteSpace(accentColor) ? "Blue" : accentColor;

        _currentBaseTheme = effectiveBase;

        Application.Current.RequestedThemeVariant = effectiveBase switch
        {
            "Light" => ThemeVariant.Light,
            "Adaptive" => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };

        ApplyPalette(effectiveBase);
        ApplyAccentColor(effectiveAccent);
        EnsureAdaptiveVariantHook();
    }

    /// <summary>
    ///     Resolves the machine's current light/dark variant. On Windows this is the apps
    ///     light/dark setting exposed through <c>App.PlatformSettings</c>. On Linux the GTK/XSETTINGS
    ///     preference is authoritative (Avalonia's X11 backend reports Light for some desktops such as
    ///     Cinnamon, so the gsettings color-scheme / GTK theme name are consulted first). macOS uses
    ///     the system appearance through the platform settings.
    /// </summary>
    internal static ThemeVariant ResolveSystemThemeVariant()
    {
        if (OperatingSystem.IsLinux())
        {
            var gtkVariant = ResolveLinuxGtkThemeVariant();
            if (gtkVariant is { } resolvedGtk) return resolvedGtk;
        }

        var app = Application.Current;
        if (app is null) return ThemeVariant.Light;

        var platformVariant = app.PlatformSettings?.GetColorValues().ThemeVariant;
        if (platformVariant == PlatformThemeVariant.Dark) return ThemeVariant.Dark;
        if (platformVariant == PlatformThemeVariant.Light) return ThemeVariant.Light;

        return app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    /// <summary>
    ///     Reads the GTK/desktop appearance on Linux. Returns <c>null</c> when gsettings is not
    ///     available or reports nothing usable, so callers fall back to Avalonia's platform settings.
    /// </summary>
    private static ThemeVariant? ResolveLinuxGtkThemeVariant()
    {
        var colorScheme = ReadGsettingsValue("org.gnome.desktop.interface", "color-scheme");
        if (string.Equals(colorScheme, "prefer-dark", StringComparison.OrdinalIgnoreCase)) return ThemeVariant.Dark;
        if (string.Equals(colorScheme, "prefer-light", StringComparison.OrdinalIgnoreCase)) return ThemeVariant.Light;

        foreach (var schema in GtkThemeSchemas)
        {
            var themeName = ReadGsettingsValue(schema, "gtk-theme");
            if (string.IsNullOrWhiteSpace(themeName)) continue;

            return themeName.Contains("dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }

        return null;
    }

    private static string? ReadGsettingsValue(string schema, string key)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gsettings",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("get");
            startInfo.ArgumentList.Add(schema);
            startInfo.ArgumentList.Add(key);

            using var process = Process.Start(startInfo);
            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); }
                catch (InvalidOperationException) { }
                return null;
            }

            if (process.ExitCode != 0) return null;

            return output.Trim('\'', '"');
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or IOException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Maps a base theme name to its palette. Explicit themes ignore the machine variant;
    ///     <c>Adaptive</c> picks Light or Dark from the current machine variant.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ResolvePalette(string baseTheme, ThemeVariant systemVariant)
    {
        return baseTheme switch
        {
            "Light" => LightPalette,
            "HighContrast" => HighContrastPalette,
            "Midnight" => MidnightPalette,
            "Adaptive" => systemVariant == ThemeVariant.Dark ? DarkPalette : LightPalette,
            _ => DarkPalette
        };
    }

    private static void ApplyPalette(string baseTheme)
    {
        if (Application.Current is null) return;

        var systemVariant = ResolveSystemThemeVariant();
        var palette = ResolvePalette(baseTheme, systemVariant);
        _lastAppliedVariant = systemVariant;

        foreach (var key in PaletteKeys)
        {
            if (palette.TryGetValue(key, out var hex))
                Application.Current.Resources[key] = Color.Parse(hex);
        }
    }

    private static void ApplyAccentColor(string accentColor)
    {
        if (Application.Current is null) return;

        if (!AccentHex.TryGetValue(accentColor, out var accentHex)) accentHex = AccentHex["Blue"];

        var accent = Color.Parse(accentHex);
        Application.Current.Resources["AccentColor"] = accent;
        Application.Current.Resources["AccentHoverColor"] = Lighten(accent, 0.15);
        Application.Current.Resources["AccentPressedColor"] = Darken(accent, 0.15);
        Application.Current.Resources["AccentDisabledColor"] = WithAlpha(accent, 0x66);
        Application.Current.Resources["SelectionRingColor"] = accent;
    }

    private static void EnsureAdaptiveVariantHook()
    {
        var app = Application.Current;
        if (app is null) return;

        if (!_adaptiveVariantHookAttached)
        {
            app.ActualThemeVariantChanged += OnActualThemeVariantChanged;
            _adaptiveVariantHookAttached = true;
        }

        // Linux desktops such as Cinnamon do not notify Avalonia when the GTK theme changes,
        // so poll the machine theme while Adaptive is selected and re-apply the palette on a change.
        if (OperatingSystem.IsLinux() && _adaptiveThemePollTimer is null)
        {
            _adaptiveThemePollTimer = new Timer(
                _ => PollAdaptiveTheme(),
                null,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(15));
        }
    }

    private static void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (!string.Equals(_currentBaseTheme, "Adaptive", StringComparison.OrdinalIgnoreCase)) return;

        if (Dispatcher.UIThread.CheckAccess()) ApplyPalette(_currentBaseTheme);
        else Dispatcher.UIThread.Post(() => ApplyPalette(_currentBaseTheme));
    }

    private static void PollAdaptiveTheme()
    {
        if (!string.Equals(_currentBaseTheme, "Adaptive", StringComparison.OrdinalIgnoreCase)) return;
        if (ResolveSystemThemeVariant() == _lastAppliedVariant) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!string.Equals(_currentBaseTheme, "Adaptive", StringComparison.OrdinalIgnoreCase)) return;
            if (ResolveSystemThemeVariant() == _lastAppliedVariant) return;

            ApplyPalette(_currentBaseTheme);
        });
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