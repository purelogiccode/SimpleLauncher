using Moq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using SimpleLauncher.Avalonia.Services;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="AvaloniaHelpUserService" /> (Phase 3). The alias-to-canonical
///     mapping is tested via text equality: an alias must resolve to the same details
///     text as its canonical system name, regardless of whether parameters.md is
///     present in the test output (both fall back identically when the file is absent).
/// </summary>
public class AvaloniaHelpUserServiceTests
{
    private static AvaloniaHelpUserService CreateService(LocalizationService? localization = null)
    {
        var service = new AvaloniaHelpUserService(
            new Mock<ILogger>().Object,
            TestDependencies.MessageBox().Object,
            localization);
        WaitUntilHelpDataSettled(service);
        return service;
    }

    private static void WaitUntilHelpDataSettled(AvaloniaHelpUserService service)
    {
        // The constructor starts a one-shot async load of parameters.md; when the file is
        // present in the test output, Systems flips from empty to parsed asynchronously,
        // which makes text-equality assertions racy. Wait for the load to settle before asserting.
        if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "parameters.md"))) return;

        for (var i = 0; i < 100 && !service.HasSystemDetails("Nintendo 64"); i++) Thread.Sleep(50);
    }

    [Fact]
    public void GetHelpText_NullOrEmpty_ReturnsNoSystemNameProvided()
    {
        var service = CreateService();

        Assert.Equal("No system name provided.", service.GetHelpText(""));
        Assert.Equal("No system name provided.", service.GetHelpText(null!));
    }

    [Fact]
    public void GetHelpText_AliasResolvesToSameTextAsCanonicalName()
    {
        var service = CreateService();

        Assert.Equal(service.GetHelpText("Sony PlayStation 1"), service.GetHelpText("PSX"));
        Assert.Equal(service.GetHelpText("Sony PlayStation 1"), service.GetHelpText("PlayStation 1"));
        Assert.Equal(service.GetHelpText("Nintendo SNES"), service.GetHelpText("Super Nintendo"));
        Assert.Equal(service.GetHelpText("SNK Neo Geo"), service.GetHelpText("NeoGeo"));
        Assert.Equal(service.GetHelpText("Sega Genesis"), service.GetHelpText("Mega Drive"));
        Assert.Equal(service.GetHelpText("Microsoft Windows"), service.GetHelpText("PC"));
    }

    [Fact]
    public void GetHelpText_UnknownSystem_ReturnsNoDetailsFallback()
    {
        var service = CreateService();

        // WPF parity: uses Noinformationavailableforsystem key without quote wrapping
        Assert.Equal("No information available for system Unicorn 2000", service.GetHelpText("Unicorn 2000"));
    }

    [Fact]
    public void GetHelpText_IsCaseInsensitive()
    {
        var service = CreateService();

        Assert.Equal(service.GetHelpText("NES"), service.GetHelpText("nes"));
        Assert.Equal(service.GetHelpText("SNES"), service.GetHelpText("Snes"));
    }

    [Fact]
    public void HasSystemDetails_ReturnsFalseForUnknownSystems()
    {
        var service = CreateService();

        Assert.False(service.HasSystemDetails("Unicorn 2000"));
        Assert.False(service.HasSystemDetails(""));
        Assert.False(service.HasSystemDetails(null!));
    }

    [Fact]
    public void GetHelpText_WithLocalization_ReturnsLocalizedNoSystemNameFallback()
    {
        var service = CreateService(new LocalizationService());

        Assert.Equal("No system name provided.", service.GetHelpText(""));
    }

    [Fact]
    public void UpdateHelpTextBlock_RendersWpfStyleInlineLinks()
    {
        HeadlessAvalonia.EnsureInitialized();
        var service = CreateService();
        var textBlock = HeadlessAvalonia.RunOnUiThread(() => new SelectableTextBlock { Width = 480 });

        HeadlessAvalonia.RunOnUiThread(() => service.UpdateHelpTextBlock(textBlock, "Nintendo 64"));

        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var inlines = textBlock.Inlines!;
            var text = inlines.Text ?? string.Empty;

            // WPF FlowDocument parity: no stray \r (line breaks come from LineBreak inlines),
            // no embedded control placeholders, no leftover markdown bold markers,
            // and line structure is preserved.
            Assert.DoesNotContain('\r', text.Replace("\r\n", string.Empty));
            Assert.DoesNotContain('\uFFFC', text);
            Assert.DoesNotContain("**", text);
            Assert.Contains(inlines, inline => inline is LineBreak);

            // Links are underlined inline spans (WPF Hyperlink parity), never embedded button controls
            Assert.DoesNotContain(inlines, inline => inline is InlineUIContainer);
            var linkSpans = inlines.OfType<Span>()
                .Where(span => span.TextDecorations is { Count: > 0 })
                .ToList();
            Assert.NotEmpty(linkSpans);
            Assert.All(linkSpans, span => Assert.IsType<Run>(Assert.Single(span.Inlines)));
            Assert.Contains(linkSpans, span => string.Equals(((Run)span.Inlines[0]).Text, "Libretro Website", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void UpdateHelpTextBlock_ReusesBlockAndReplacesLinks()
    {
        HeadlessAvalonia.EnsureInitialized();
        var service = CreateService();
        var textBlock = HeadlessAvalonia.RunOnUiThread(() => new SelectableTextBlock { Width = 480 });

        HeadlessAvalonia.RunOnUiThread(() =>
        {
            service.UpdateHelpTextBlock(textBlock, "Nintendo 64");
            var firstText = textBlock.Inlines!.Text;
            Assert.Contains("Nintendo 64", firstText);
            Assert.Contains("BizHawk Website", firstText);

            service.UpdateHelpTextBlock(textBlock, "Sega Dreamcast");
            var secondText = textBlock.Inlines!.Text;
            Assert.Contains("Sega Dreamcast", secondText);
            Assert.DoesNotContain("Nintendo 64", secondText);
            Assert.DoesNotContain("BizHawk Website", secondText);
        });
    }

    [Fact]
    public void UpdateHelpTextBlock_PreservesBoldAndHeadingText()
    {
        HeadlessAvalonia.EnsureInitialized();
        var service = CreateService();
        var textBlock = HeadlessAvalonia.RunOnUiThread(() => new SelectableTextBlock { Width = 480 });

        HeadlessAvalonia.RunOnUiThread(() => service.UpdateHelpTextBlock(textBlock, "Microsoft Windows"));

        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var inlines = textBlock.Inlines!;
            var text = inlines.Text ?? string.Empty;

            // parameters.md wraps placeholders/labels in **bold**: a captured-group regression
            // previously swallowed the inner text and dropped every bold/heading segment.
            Assert.Contains("%BASEFOLDER%", text);
            Assert.Contains("System Folder (Example):", text);
            Assert.Contains("Emulator Name:", text);
            Assert.DoesNotContain("****", text);

            var boldTexts = inlines.OfType<Bold>()
                .SelectMany(bold => bold.Inlines.OfType<Run>())
                .Select(run => run.Text)
                .ToList();
            Assert.Contains("%BASEFOLDER%", boldTexts);
        });
    }

    [Fact]
    public void AllLocaleFiles_ContainSystemHelpKeys()
    {
        // The System Help panel (EditSystemWindow right column) needs these keys
        // in every locale; GetString falls back to English but the parity contract
        // is that translations exist for all 18 locales.
        var keys = new[]
        {
            "DeveloperSuggestion", "TooltipDeveloperSuggestionLabel",
            "Nodetailsavailablefor", "Noinformationavailableforsystem", "Nosystemnameprovided"
        };

        var resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        var files = Directory.EnumerateFiles(resourcesDir, "strings.*.json").ToList();
        Assert.Equal(18, files.Count);

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            foreach (var key in keys)
            {
                Assert.True(json.Contains($"\"{key}\"", StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} is missing key '{key}'");
            }
        }
    }
}