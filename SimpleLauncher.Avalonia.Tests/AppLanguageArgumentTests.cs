namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests the --language startup argument (WPF parity) used to override the saved
///     language for one launch.
/// </summary>
public class AppLanguageArgumentTests
{
    [Theory]
    [InlineData("--language=es", "es")]
    [InlineData("-language", "fr")]
    [InlineData("--language", "de")]
    public void TryGetLanguageArg_ReadsSupportedForms(string firstArg, string expected)
    {
        var args = string.Equals(firstArg, "-language", StringComparison.Ordinal) ||
                   string.Equals(firstArg, "--language", StringComparison.Ordinal)
            ? new[] { firstArg, expected }
            : new[] { firstArg };

        Assert.Equal(expected, App.TryGetLanguageArg(args));
    }

    [Fact]
    public void ResolveStartupLanguage_UsesConfiguredLanguageWithoutArgument()
    {
        Assert.Equal("pt-BR", App.ResolveStartupLanguage([], "pt-BR"));
    }

    [Fact]
    public void ResolveStartupLanguage_UsesArgumentWhenSupported()
    {
        Assert.Equal("es", App.ResolveStartupLanguage(["--language=ES"], "pt-BR"));
    }

    [Fact]
    public void ResolveStartupLanguage_FallsBackToEnglishForUnknownArgument()
    {
        Assert.Equal("en", App.ResolveStartupLanguage(["--language=zz"], "pt-BR"));
    }
}
