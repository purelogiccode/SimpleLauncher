using System.Text.Json;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Tests.TestHelpers;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Localization parity tests: every strings.{lang}.json must ship the exact same
///     key set as strings.en.json, and the LocalizationService must resolve all of
///     them (including the case-insensitive lookup for WPF-style codes like 'pt-br').
/// </summary>
public class LocalizationTests
{
    // The canonical packs live in SimpleLauncher.Core\Localization and are embedded into
    // the Avalonia assembly; the tests verify both the source files and the embedding.
    private static string ResourcesDir => AvaloniaProjectPathHelper.GetLocalizationResourcesPath();

    private static IEnumerable<string> LanguageFiles()
    {
        var dir = ResourcesDir;
        if (!Directory.Exists(dir)) return [];

        return Directory.EnumerateFiles(dir, "strings.*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, string> LoadKeys(string file)
    {
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }

    [Fact]
    public void EveryLanguageFileExistsForTheCanonicalLanguageSet()
    {
        var expected = LocalizationService.AvailableLanguages.Keys
            .Select(lang => $"strings.{lang}.json")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var actual = LanguageFiles().Select(Path.GetFileName).Where(n => n is not null).Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Guards the PACKAGING step: every selectable language must be embedded in the
    ///     Avalonia assembly (no loose Resources folder ships), otherwise LocalizationService
    ///     silently falls back to English at runtime.
    /// </summary>
    [Fact]
    public void EveryLanguagePackIsEmbeddedInTheAssembly()
    {
        var embeddedNames = typeof(LocalizationService).Assembly.GetManifestResourceNames();

        foreach (var lang in LocalizationService.AvailableLanguages.Keys)
        {
            var expected = LocalizationService.EmbeddedResourcePrefix + lang +
                           LocalizationService.EmbeddedResourceSuffix;
            Assert.True(embeddedNames.Contains(expected, StringComparer.OrdinalIgnoreCase),
                $"'{expected}' is NOT embedded in the assembly. Check SimpleLauncher.Avalonia.csproj: " +
                "the shared packs need the " +
                "<EmbeddedResource Include=\"..\\SimpleLauncher.Core\\Localization\\strings.*.json\" /> entry.");
        }
    }

    [Fact]
    public void EveryLanguageFileSharesTheEnglishKeySet()
    {
        var enFile = Path.Combine(ResourcesDir, "strings.en.json");
        Assert.True(File.Exists(enFile), $"Missing {enFile}");
        var enKeys = LoadKeys(enFile).Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        var failures = new List<string>();

        foreach (var file in LanguageFiles())
        {
            var keys = LoadKeys(file).Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

            var missing = enKeys.Except(keys, StringComparer.OrdinalIgnoreCase).ToList();
            var extra = keys.Except(enKeys, StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Count > 0)
            {
                failures.Add(
                    $"{Path.GetFileName(file)} is missing {missing.Count} key(s): {string.Join(", ", missing)}");
            }

            if (extra.Count > 0)
            {
                failures.Add(
                    $"{Path.GetFileName(file)} has {extra.Count} unexpected key(s): {string.Join(", ", extra)}");
            }
        }

        Assert.True(failures.Count == 0,
            "Resource key mismatch between strings.en.json and the other language files:\n" +
            string.Join("\n", failures) +
            "\n\nRun the SimpleLauncher.ResourceTranslator project to translate the English pack and add the missing keys to every language file.");
    }

    /// <summary>
    ///     No language file may contain empty (or whitespace-only) values: the LocalizationService
    ///     treats a present-but-empty key as existing and renders an empty string instead of the
    ///     English fallback, which leaves blank UI text.
    /// </summary>
    [Fact]
    public void NoLanguageFileShouldContainEmptyValues()
    {
        var failures = new List<string>();

        foreach (var file in LanguageFiles())
        {
            var strings = LoadKeys(file);
            foreach (var (key, value) in strings)
            {
                if (string.IsNullOrWhiteSpace(value))
                    failures.Add($"{Path.GetFileName(file)}: '{key}' has an empty value");
            }
        }

        Assert.True(failures.Count == 0,
            "Empty resource values detected (these render as blank text in the UI):\n" +
            string.Join("\n", failures));
    }

    [Fact]
    public void LocalizationServiceLoadsEveryLanguage()
    {
        var localization = new LocalizationService();

        foreach (var lang in LocalizationService.AvailableLanguages.Keys)
        {
            localization.LoadLanguage(lang);

            Assert.Equal(lang, localization.CurrentLanguage);
            Assert.NotEqual("Sidebar.AllGames", localization.GetString("Sidebar.AllGames"),
                StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual("Toolbar.Search", localization.GetString("Toolbar.Search"),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LocalizationServiceResolvesWpfStyleCodesCaseInsensitively()
    {
        // The WPF app stores 'pt-br' / 'zh-hans' in settings.xml; the Avalonia
        // files are named 'pt-BR' / 'zh-Hans'. On Linux the lookup must still match.
        var localization = new LocalizationService();

        localization.LoadLanguage("pt-br");
        Assert.Equal("pt-BR", localization.CurrentLanguage);
        Assert.NotEqual("Toolbar.Games", localization.GetString("Toolbar.Games"), StringComparer.OrdinalIgnoreCase);

        localization.LoadLanguage("zh-hans");
        Assert.Equal("zh-Hans", localization.CurrentLanguage);
        Assert.NotEqual("Sidebar.AllGames", localization.GetString("Sidebar.AllGames"),
            StringComparer.OrdinalIgnoreCase);
    }
}