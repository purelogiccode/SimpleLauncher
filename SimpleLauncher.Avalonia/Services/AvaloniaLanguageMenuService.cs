namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Maps language menu items (by x:Name) to language codes and back. The canonical
///     18-language set matches the WPF <c>LanguageMenuService.NameToCode</c> and the
///     <see cref="LocalizationService.AvailableLanguages" /> set (canonical codes use
///     'pt-BR' / 'zh-Hans', matching the resource file names).
/// </summary>
public static class AvaloniaLanguageMenuService
{
    /// <summary>
    ///     Maps menu item x:Name to the canonical language code.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> NameToCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LanguageArabic"] = "ar",
            ["LanguageBengali"] = "bn",
            ["LanguageGerman"] = "de",
            ["LanguageEnglish"] = "en",
            ["LanguageSpanish"] = "es",
            ["LanguageFrench"] = "fr",
            ["LanguageHindi"] = "hi",
            ["LanguageIndonesianMalay"] = "id",
            ["LanguageItalian"] = "it",
            ["LanguageJapanese"] = "ja",
            ["LanguageKorean"] = "ko",
            ["LanguageDutch"] = "nl",
            ["LanguagePortugueseBr"] = "pt-BR",
            ["LanguageRussian"] = "ru",
            ["LanguageTurkish"] = "tr",
            ["LanguageUrdu"] = "ur",
            ["LanguageVietnamese"] = "vi",
            ["LanguageChineseSimplified"] = "zh-Hans"
        };

    /// <summary>
    ///     Looks up the language code for a menu item name, or null when the name is not a language item.
    /// </summary>
    /// <param name="menuItemName">The menu item x:Name (may be null).</param>
    public static string? GetLanguageCodeFromMenuItemName(string? menuItemName)
    {
        return menuItemName is not null && NameToCode.TryGetValue(menuItemName, out var code) ? code : null;
    }

    /// <summary>
    ///     Returns the menu item x:Name that should be checked for the given language
    ///     code (case-insensitive), or null when the code is not a supported language.
    /// </summary>
    /// <param name="languageCode">The canonical (or WPF-style lowercase) language code.</param>
    public static string? GetMenuItemNameForLanguageCode(string languageCode)
    {
        return NameToCode
            .FirstOrDefault(kv => string.Equals(kv.Value, languageCode, StringComparison.OrdinalIgnoreCase))
            .Key;
    }

    /// <summary>
    ///     Determines whether the given menu item name belongs to the language submenu.
    /// </summary>
    /// <param name="menuItemName">The menu item x:Name (may be null).</param>
    public static bool IsLanguageMenuItem(string? menuItemName)
    {
        return menuItemName is not null && NameToCode.ContainsKey(menuItemName);
    }
}