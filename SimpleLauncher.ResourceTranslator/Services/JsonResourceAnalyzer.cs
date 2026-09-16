using System.Text.Json;
using SimpleLauncher.ResourceTranslator.Models;

namespace SimpleLauncher.ResourceTranslator.Services;

/// <summary>
///     Analyzes Avalonia JSON resource files to find missing and duplicate translation keys.
/// </summary>
public static class JsonResourceAnalyzer
{
    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "Arabic",
        ["bn"] = "Bengali",
        ["de"] = "German",
        ["es"] = "Spanish",
        ["fr"] = "French",
        ["hi"] = "Hindi",
        ["id"] = "Indonesian",
        ["it"] = "Italian",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
        ["nl"] = "Dutch",
        ["pt-br"] = "Brazilian Portuguese",
        ["ru"] = "Russian",
        ["tr"] = "Turkish",
        ["ur"] = "Urdu",
        ["vi"] = "Vietnamese",
        ["zh-hans"] = "Simplified Chinese"
    };

    /// <summary>
    ///     Reads all key-value pairs from the English JSON resource file.
    /// </summary>
    /// <param name="englishFilePath">The path to the English JSON resource file.</param>
    /// <returns>A dictionary of translation keys and their English values.</returns>
    public static IDictionary<string, string> ReadEnglishKeys(string englishFilePath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var json = File.ReadAllText(englishFilePath);
        var doc = JsonDocument.Parse(json);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.GetString() ?? "";
        }

        return result;
    }

    /// <summary>
    ///     Normalizes a language code from the file name to lowercase with hyphen.
    ///     e.g., "pt-BR" -> "pt-br", "zh-Hans" -> "zh-hans"
    /// </summary>
    private static string NormalizeLanguageCode(string langCode)
    {
        return langCode.ToLowerInvariant();
    }

    /// <summary>
    ///     Analyzes all JSON language resource files to identify missing and duplicate keys.
    /// </summary>
    /// <param name="resourcesPath">The path to the resources directory.</param>
    /// <param name="englishKeys">The dictionary of English translation keys.</param>
    /// <returns>A list of missing key batches for each language.</returns>
    public static IList<MissingKeyBatch> AnalyzeAllLanguages(string resourcesPath,
        IDictionary<string, string> englishKeys)
    {
        var batches = new List<MissingKeyBatch>();

        var otherFiles = Directory.EnumerateFiles(resourcesPath, "strings.*.json")
            .Where(static f => !f.EndsWith("strings.en.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in otherFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var rawLangCode = fileName.Replace("strings.", "", StringComparison.OrdinalIgnoreCase);
            var langCode = NormalizeLanguageCode(rawLangCode);
            var langName = LanguageNames.GetValueOrDefault(langCode, langCode);

            var existingKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            var duplicateKeys = new List<string>();

            var json = File.ReadAllText(file);
            var doc = JsonDocument.Parse(json);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var key = property.Name;
                if (existingKeys.ContainsKey(key))
                    duplicateKeys.Add(key);
                else
                    existingKeys[key] = property.Value.GetString() ?? "";
            }

            var missing = englishKeys
                .Where(kvp => !existingKeys.TryGetValue(kvp.Key, out var translated) ||
                              string.IsNullOrWhiteSpace(translated))
                .Select(static kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value))
                .ToList();

            if (missing.Count > 0 || duplicateKeys.Count > 0)
            {
                batches.Add(new MissingKeyBatch
                {
                    FilePath = file,
                    LanguageCode = langCode,
                    LanguageName = langName,
                    MissingKeys = missing,
                    DuplicateKeysRemoved = duplicateKeys.Distinct(StringComparer.Ordinal).ToList()
                });
            }
        }

        return batches;
    }
}