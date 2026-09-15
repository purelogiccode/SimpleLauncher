namespace SimpleLauncher.ResourceTranslator.Models;

/// <summary>
///     Represents a batch of missing translation keys for a specific language.
/// </summary>
public class MissingKeyBatch
{
    /// <summary>
    ///     Gets or sets the file path of the language resource file.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    ///     Gets or sets the language code (e.g., "de", "fr").
    /// </summary>
    public string LanguageCode { get; set; } = "";

    /// <summary>
    ///     Gets or sets the display name of the language.
    /// </summary>
    public string LanguageName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the list of missing key-value pairs to translate.
    /// </summary>
    public IList<KeyValuePair<string, string>> MissingKeys { get; set; } = [];

    /// <summary>
    ///     Gets or sets the list of duplicate keys that were removed.
    /// </summary>
    public IList<string> DuplicateKeysRemoved { get; set; } = [];
}