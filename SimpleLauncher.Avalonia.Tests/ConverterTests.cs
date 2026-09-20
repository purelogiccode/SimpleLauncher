using System.Globalization;
using Avalonia.Media;
using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Converters;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the Avalonia port converters (Phase 6). Pure boolean/null converters
///     need no platform; BooleanToFavoriteStatusConverter and ConsoleToCardHeightConverter
///     are exercised via their Set* static hooks; PathToImageConverter needs the headless
///     Avalonia platform (Bitmap decoding).
/// </summary>
public class ConverterTests
{
    #region Converters.cs — boolean/null visibility converters

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BoolToVisibilityConverter_MapsBoolDirectly(bool input, bool expected)
    {
        var converter = new BoolToVisibilityConverter();
        Assert.Equal(expected, converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(expected, converter.ConvertBack(expected, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InverseBoolToVisibilityConverter_InvertsBool(bool input, bool expected)
    {
        var converter = new InverseBoolToVisibilityConverter();
        Assert.Equal(expected, converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(input, converter.ConvertBack(expected, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InverseBoolConverter_InvertsBool(bool input, bool expected)
    {
        var converter = new InverseBoolConverter();
        Assert.Equal(expected, converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(input, converter.ConvertBack(expected, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("x", true)]
    [InlineData(null, false)]
    public void NullToVisibilityConverter_NonNullIsVisible(object? input, bool expected)
    {
        var converter = new NullToVisibilityConverter();
        Assert.Equal(expected, converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region SmartTitleCaseConverter

    [Theory]
    [InlineData("SUPER MARIO BROS", "Super Mario Bros")]
    [InlineData("sonic the hedgehog", "Sonic The Hedgehog")]
    public void SmartTitleCase_TitleCasesAllUpperOrAllLower(string input, string expected)
    {
        var converter = new SmartTitleCaseConverter();
        var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Super Mario Bros")]
    [InlineData("Metroid Prime 2: Echoes")]
    public void SmartTitleCase_MixedCaseIsUnchanged(string input)
    {
        var converter = new SmartTitleCaseConverter();
        Assert.Equal(input, converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SmartTitleCase_EmptyOrNullPassesThrough(string? input)
    {
        var converter = new SmartTitleCaseConverter();
        Assert.Equal(input, converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SmartTitleCase_NonStringPassesThrough()
    {
        var converter = new SmartTitleCaseConverter();
        Assert.Equal(42, converter.Convert(42, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SmartTitleCase_DigitsOnlyRemainUnchanged()
    {
        var converter = new SmartTitleCaseConverter();
        Assert.Equal("1234", converter.Convert("1234", typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SmartTitleCase_ConvertBackThrows()
    {
        var converter = new SmartTitleCaseConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack("x", typeof(string), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region BooleanToFavoriteStatusConverter

    [Fact]
    public void BooleanToFavorite_True_ReturnsFavoriteLabel()
    {
        var converter = new BooleanToFavoriteStatusConverter();
        BooleanToFavoriteStatusConverter.SetLocalizationService(new LocalizationService());
        Assert.Equal("Favorite", converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BooleanToFavorite_False_ReturnsNotFavoriteLabel()
    {
        var converter = new BooleanToFavoriteStatusConverter();
        BooleanToFavoriteStatusConverter.SetLocalizationService(new LocalizationService());
        Assert.Equal("Not Favorite", converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-bool")]
    public void BooleanToFavorite_NonBool_ReturnsUnknownLabel(object? value)
    {
        var converter = new BooleanToFavoriteStatusConverter();
        BooleanToFavoriteStatusConverter.SetLocalizationService(new LocalizationService());
        Assert.Equal("Unknown Favorite Status",
            converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BooleanToFavorite_LocalizedValueIsUsedWhenPresent()
    {
        // A localized strings file in an isolated temp dir makes GetString return the
        // translated value — never touches the shared output Resources folder (which
        // LocalizationTests reads in parallel).
        var tempDir = Path.Combine(Path.GetTempPath(), "SimpleLauncherConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "strings.en.json"),
            """{"FavoriteStatusLabel": "Favorite FR", "NotFavoriteStatusLabel": "Not Favorite FR", "UnknownFavoriteStatusLabel": "Unknown FR"}""");

        try
        {
            var localization = new LocalizationService(tempDir);
            localization.LoadLanguage("en");
            var converter = new BooleanToFavoriteStatusConverter();
            BooleanToFavoriteStatusConverter.SetLocalizationService(localization);

            Assert.Equal("Favorite FR", converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Equal("Not Favorite FR",
                converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Equal("Unknown FR", converter.Convert("?", typeof(string), null, CultureInfo.InvariantCulture));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BooleanToFavorite_ConvertBackThrows()
    {
        var converter = new BooleanToFavoriteStatusConverter();
        BooleanToFavoriteStatusConverter.SetLocalizationService(new LocalizationService());
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region SystemInfoLineBrushConverter

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SystemInfoLineBrush_ReturnsBrushForBothStates(bool isError)
    {
        var converter = new SystemInfoLineBrushConverter();

        var result = converter.Convert(isError, typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.IsType<IBrush>(result, exactMatch: false);
    }

    [Fact]
    public void SystemInfoLineBrush_ConvertBackThrows()
    {
        var converter = new SystemInfoLineBrushConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region ConsoleToCardHeightConverter

    private static ConsoleToCardHeightConverter CreateHeightConverter()
    {
        TestEnvironment.EnsurePortableSettings();
        var settings = new SettingsManagerService(
            new ConfigurationBuilder().Build(),
            new Mock<ILogger>().Object,
            new Mock<ICredentialProtector>().Object,
            new Mock<IMessageBoxLibraryService>().Object);
        // No aspect-ratio override → per-system box ratios apply.
        settings.ButtonAspectRatio = "";
        var converter = new ConsoleToCardHeightConverter();
        ConsoleToCardHeightConverter.SetRatioService(new SystemArtRatioService(settings));
        return converter;
    }

    [Fact]
    public void ConsoleToCardHeight_FewerThanTwoValues_ReturnsDefault()
    {
        var converter = CreateHeightConverter();
        var result = converter.Convert([168.0], typeof(double), null, CultureInfo.InvariantCulture);
        Assert.Equal(168.0, result);
    }

    [Fact]
    public void ConsoleToCardHeight_NullWidth_ReturnsDefaultWidth()
    {
        var converter = CreateHeightConverter();
        var result = converter.Convert([null, "NES"], typeof(double), null, CultureInfo.InvariantCulture);
        Assert.Equal((168.0 * 0.75) + 48.0, (double)result!, 3);
    }

    [Fact]
    public void ConsoleToCardHeight_UnknownSystem_UsesSquareRatio()
    {
        var converter = CreateHeightConverter();
        var result = converter.Convert([200.0, "Completely Unknown System"], typeof(double), null,
            CultureInfo.InvariantCulture);
        Assert.Equal(200.0 + 48.0, (double)result!, 3);
    }

    [Fact]
    public void ConsoleToCardHeight_KnownSystem_AppliesBoxRatio()
    {
        var converter = CreateHeightConverter();
        var result = converter.Convert([200.0, "NES"], typeof(double), null, CultureInfo.InvariantCulture);
        Assert.Equal((200.0 * 0.75) + 48.0, (double)result!, 3);
    }

    [Fact]
    public void ConsoleToCardHeight_MixedView_UsesMixedRatio()
    {
        var converter = CreateHeightConverter();
        var result = converter.Convert([200.0, "NES", true], typeof(double), null, CultureInfo.InvariantCulture);
        Assert.Equal((200.0 * 0.73) + 48.0, (double)result!, 3);
    }

    [Fact]
    public void ConsoleToCardHeight_AspectRatioInput_ChangesHeightWithoutRescan()
    {
        // The card Height MultiBinding carries the aspect key as 4th input: changing
        // the ratio must re-evaluate heights in place (regression: the handlers used
        // to ReloadGames, rescanning the library on the UI thread and freezing).
        var converter = CreateHeightConverter();
        var square = converter.Convert([200.0, "NES", true, "Square"], typeof(double), null,
            CultureInfo.InvariantCulture);
        var wider = converter.Convert([200.0, "NES", true, "Wider"], typeof(double), null,
            CultureInfo.InvariantCulture);
        var taller = converter.Convert([200.0, "NES", true, "Taller"], typeof(double), null,
            CultureInfo.InvariantCulture);
        Assert.Equal((200.0 * (1.0 / 1.1)) + 48.0, (double)square!, 3);
        Assert.Equal((200.0 * (1.0 / 1.5)) + 48.0, (double)wider!, 3);
        Assert.Equal((200.0 * 1.3) + 48.0, (double)taller!, 3);
    }

    [Fact]
    public void ConsoleToCardHeight_ConvertBackThrows()
    {
        var converter = CreateHeightConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(100.0, typeof(double), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region PathToImageConverter

    [Fact]
    public void PathToImage_NullOrEmptyPath_ReturnsPlaceholderBitmap()
    {
        HeadlessAvalonia.EnsureInitialized();
        PathToImageConverter.ClearCache();
        var converter = new PathToImageConverter();

        Assert.NotNull(converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture));
        Assert.NotNull(converter.Convert("", typeof(object), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PathToImage_MissingFile_ReturnsPlaceholderBitmap()
    {
        HeadlessAvalonia.EnsureInitialized();
        PathToImageConverter.ClearCache();
        var converter = new PathToImageConverter();

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "definitely-missing-cover.png");
        Assert.NotNull(converter.Convert(path, typeof(object), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PathToImage_ExistingFile_ReturnsBitmapAndCachesIt()
    {
        HeadlessAvalonia.EnsureInitialized();
        PathToImageConverter.ClearCache();

        var imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-cover.png");
        File.WriteAllText(imagePath, "not-a-real-png-but-headless-stubs-decoding");
        try
        {
            var converter = new PathToImageConverter();
            var first = converter.Convert(imagePath, typeof(object), null, CultureInfo.InvariantCulture);
            var second = converter.Convert(imagePath, typeof(object), null, CultureInfo.InvariantCulture);

            Assert.NotNull(first);
            Assert.Same(first, second); // cached instance
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void PathToImage_NonStringValue_ReturnsPlaceholder()
    {
        HeadlessAvalonia.EnsureInitialized();
        PathToImageConverter.ClearCache();
        var converter = new PathToImageConverter();
        Assert.NotNull(converter.Convert(42, typeof(object), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PathToImage_ConvertBackThrows()
    {
        HeadlessAvalonia.EnsureInitialized();
        var converter = new PathToImageConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(new object(), typeof(object), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PathToImage_ClearCacheDoesNotThrow()
    {
        HeadlessAvalonia.EnsureInitialized();
        PathToImageConverter.ClearCache();
    }

    #endregion
}