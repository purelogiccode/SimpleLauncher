using Xunit;

namespace SimpleLauncher.Tests;

public class LanguageLaunchArgumentTests
{
    // Mirrors LanguageMenuService.NameToCode (18 selectable languages) — the slow suite's
    // LanguageLaunchTestsBase keeps an identical list for its app-launch checks.
    private static readonly string[] SupportedLanguageCodes =
    [
        "ar", "bn", "de", "en", "es", "fr", "hi", "id", "it",
        "ja", "ko", "nl", "pt-br", "ru", "tr", "ur", "vi", "zh-hans"
    ];

    public static TheoryData<string[], string> ParsingCases => new()
    {
        { ["--language", "es"], "es" },
        { ["-language", "TR"], "TR" }, // raw value returned; canonicalized later
        { ["--language=fr"], "fr" },
        { ["-debug", "--language", "pt-br"], "pt-br" } // mixed with other args
    };

    public static TheoryData<string[]> AbsentCases => new()
    {
        Array.Empty<string>(),
        { ["-debug"] },
        { ["--language"] }, // missing value
        { ["--restarting"] }
    };

    public static TheoryData<string[], string, string> ResolutionCases => new()
    {
        { ["--language", "es"], "en", "es" }, // arg wins
        { ["-language", "TR"], "en", "tr" }, // canonicalized
        { [], "fr", "fr" }, // configured used
        { ["--language", "zz"], "fr", "en" }, // unsupported -> falls back to English directly
        { ["-debug"], "de", "de" } // unrelated args ignored
    };

    [Theory]
    [MemberData(nameof(ParsingCases))]
    public void TryGetLanguageArg_ParsesKnownForms(string[] args, string expected)
    {
        Assert.Equal(expected, App.TryGetLanguageArg(args));
    }

    [Theory]
    [MemberData(nameof(AbsentCases))]
    public void TryGetLanguageArg_ReturnsNull_WhenAbsent(string[] args)
    {
        Assert.Null(App.TryGetLanguageArg(args));
    }

    [Theory]
    [MemberData(nameof(ResolutionCases))]
    public void ResolveStartupLanguage_PrecedenceAndFallback(string[] args, string configured, string expected)
    {
        Assert.Equal(expected, App.ResolveStartupLanguage(args, configured));
    }

    [Fact]
    public void ResolveStartupLanguage_AllSupportedCodes_AreAccepted()
    {
        foreach (var code in SupportedLanguageCodes)
            Assert.Equal(code, App.ResolveStartupLanguage(["--language", code.ToUpperInvariant()], "en"));
    }
}