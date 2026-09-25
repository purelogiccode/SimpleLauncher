using Avalonia.Styling;
using SimpleLauncher.Avalonia.Services.Theme;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression tests for the Adaptive base theme: it must resolve to the Light or Dark
///     palette from the machine theme (Windows apps theme / Linux GTK-XSETTINGS) instead of
///     silently falling back to the Dark palette while only the Fluent built-ins follow the OS.
/// </summary>
public class AvaloniaThemeServiceTests
{
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("Adaptive")]
    [InlineData("HighContrast")]
    [InlineData("Midnight")]
    [InlineData("UnknownTheme")]
    public void ResolvePalette_ReturnsAPaletteForEveryBaseTheme(string baseTheme)
    {
        var palette = AvaloniaThemeService.ResolvePalette(baseTheme, ThemeVariant.Light);

        Assert.True(palette.ContainsKey("BgPrimaryColor"));
        Assert.True(palette.ContainsKey("TextPrimaryColor"));
    }

    [Fact]
    public void ResolvePalette_AdaptiveFollowsTheMachineVariant()
    {
        var light = AvaloniaThemeService.ResolvePalette("Adaptive", ThemeVariant.Light);
        var dark = AvaloniaThemeService.ResolvePalette("Adaptive", ThemeVariant.Dark);

        Assert.Equal("#FFFFFF", light["BgPrimaryColor"]);
        Assert.Equal("#1E1E1E", dark["BgPrimaryColor"]);
        Assert.NotEqual(light["TextPrimaryColor"], dark["TextPrimaryColor"], StringComparer.Ordinal);
    }

    [Fact]
    public void ResolvePalette_ExplicitThemesIgnoreTheMachineVariant()
    {
        Assert.Equal("#FFFFFF", AvaloniaThemeService.ResolvePalette("Light", ThemeVariant.Dark)["BgPrimaryColor"]);
        Assert.Equal("#1E1E1E", AvaloniaThemeService.ResolvePalette("Dark", ThemeVariant.Light)["BgPrimaryColor"]);
        Assert.Equal("#000B1A",
            AvaloniaThemeService.ResolvePalette("Midnight", ThemeVariant.Light)["BgPrimaryColor"]);
    }

    [Fact]
    public void ResolveSystemThemeVariant_ReturnsAConcreteVariant()
    {
        var variant = HeadlessAvalonia.RunOnUiThread(AvaloniaThemeService.ResolveSystemThemeVariant);

        Assert.Contains(variant, new[] { ThemeVariant.Light, ThemeVariant.Dark });
    }
}
