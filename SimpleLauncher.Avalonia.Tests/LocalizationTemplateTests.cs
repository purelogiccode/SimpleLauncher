using SimpleLauncher.Core.Services.Localization;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Guards the named-placeholder templates that must not be passed to
///     <see cref="string.Format(string, object?)" /> (bug #69951: the Portuguese
///     "Selecionar sistema {config.SystemName}" help text threw a FormatException).
/// </summary>
public class LocalizationTemplateTests
{
    /// <summary>The named system placeholder is replaced literally.</summary>
    [Fact]
    public void ReplaceSystemNameReplacesTheNamedPlaceholder()
    {
        var result = LocalizationTemplate.ReplaceSystemName(
            "Selecionar sistema {config.SystemName}", "PlayStation 2");

        Assert.Equal("Selecionar sistema PlayStation 2", result);
    }

    /// <summary>
    ///     Numeric format placeholders are not touched; this helper is only for named
    ///     placeholders and must not consume or reject <c>{0}</c>-style templates.
    /// </summary>
    [Fact]
    public void ReplaceSystemNameLeavesNumericPlaceholdersUntouched()
    {
        var result = LocalizationTemplate.ReplaceSystemName("Found {0} games for {1}", "PlayStation 2");

        Assert.Equal("Found {0} games for {1}", result);
    }

    /// <summary>A null template is a programming error and is rejected early.</summary>
    [Fact]
    public void ReplaceSystemNameRejectsNullTemplate()
    {
        Assert.Throws<ArgumentNullException>(() => LocalizationTemplate.ReplaceSystemName(null!, "NES"));
    }

    /// <summary>
    ///     The real localized templates contain the named placeholder in every language;
    ///     the helper must handle all of them without a FormatException.
    /// </summary>
    [Theory]
    [InlineData("Select {config.SystemName} system")]
    [InlineData("Selecionar sistema {config.SystemName}")]
    [InlineData("Wählen Sie das {config.SystemName}-System aus")]
    [InlineData("Seleziona sistema {config.SystemName}")]
    [InlineData("Выбрать систему {config.SystemName}")]
    public void ReplaceSystemNameHandlesEveryLanguage(string template)
    {
        var result = LocalizationTemplate.ReplaceSystemName(template, "Mega Drive");

        Assert.Contains("Mega Drive", result, StringComparison.Ordinal);
        Assert.DoesNotContain(LocalizationTemplate.SystemNamePlaceholder, result, StringComparison.Ordinal);
    }
}
