using System.Collections;
using System.Resources;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Guards the localization PACKAGING step: every language the app can select must be
///     embedded as a pack resource in SimpleLauncher.g.resources (the packs live in
///     SimpleLauncher.Core\Localization and are shared with the Avalonia app), otherwise
///     App.ApplyLanguage falls back to English at runtime.
/// </summary>
public class LocalizationResourcePackagingTests
{
    private const string ResourcePrefix = "resources/strings.";

    // Mirrors LanguageMenuService.NameToCode (the 18 selectable languages)
    private static readonly string[] SupportedLanguageCodes =
    [
        "ar", "bn", "de", "en", "es", "fr", "hi", "id", "it",
        "ja", "ko", "nl", "pt-br", "ru", "tr", "ur", "vi", "zh-hans"
    ];

    [Fact]
    public void AllSupportedLanguages_AreEmbeddedAsPackResources()
    {
        var embeddedNames = ReadPackResourceNames();

        foreach (var code in SupportedLanguageCodes)
        {
            var expectedResource = ResourcePrefix + code + ".json";
            Assert.True(
                embeddedNames.Contains(expectedResource, StringComparer.OrdinalIgnoreCase),
                $"'{expectedResource}' is NOT embedded in the assembly. " +
                "Check SimpleLauncher.csproj: the shared pack needs to match the " +
                "<Resource Include=\"..\\SimpleLauncher.Core\\Localization\\strings.*.json\" /> entry.");
        }
    }

    [Fact]
    public void EveryEmbeddedStringsResource_HasAMatchingSourceLanguage()
    {
        var embedded = ReadPackResourceNames()
            .Where(static k => k.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(embedded);

        foreach (var resource in embedded)
        {
            var code = resource[ResourcePrefix.Length..]
                .Replace(".json", "", StringComparison.OrdinalIgnoreCase);
            Assert.True(SupportedLanguageCodes.Contains(code, StringComparer.OrdinalIgnoreCase),
                $"Embedded resource '{resource}' does not map to a known language.");
        }
    }

    private static HashSet<string> ReadPackResourceNames()
    {
        var assembly = typeof(App).Assembly;

        using var stream = assembly.GetManifestResourceStream("SimpleLauncher.g.resources");
        Assert.NotNull(stream);

        using var reader = new ResourceReader(stream);
        return reader.Cast<DictionaryEntry>()
            .Select(static entry => entry.Key as string)
            .Where(static key => key != null)
            .Select(static key => key!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
