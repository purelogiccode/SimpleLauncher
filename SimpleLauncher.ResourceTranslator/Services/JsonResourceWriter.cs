using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SimpleLauncher.ResourceTranslator.Services;

/// <summary>
///     Provides functionality to update Avalonia JSON resource files with translations.
/// </summary>
public static class JsonResourceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,

        // Keep non-ASCII characters (e.g. Arabic, CJK) as readable UTF-8 text
        // instead of \uXXXX escape sequences. The default encoder escapes them.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///     Updates a JSON resource file with new translations and removes duplicate keys.
    /// </summary>
    /// <param name="filePath">The path to the JSON resource file.</param>
    /// <param name="newTranslations">Dictionary of key-value pairs to add or update.</param>
    public static void UpdateResourceFile(
        string filePath,
        IDictionary<string, string> newTranslations)
    {
        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);

        var existingEntries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Keep the first occurrence of every key and drop the later duplicates. The old
        // name-based skip removed every occurrence of a duplicated key, so the key
        // disappeared entirely (and was never re-added because it was not "missing").
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (!existingEntries.ContainsKey(property.Name))
                existingEntries[property.Name] = property.Value.GetString() ?? "";
        }

        // Merge new translations. Blank fills are ignored so an empty value written by an
        // older run can be retried instead of permanently overwriting a good translation.
        foreach (var kvp in newTranslations)
            if (!string.IsNullOrEmpty(kvp.Value))
                existingEntries[kvp.Key] = kvp.Value;

        // Write back as UTF-8 without BOM, matching the committed resource files.
        var output = JsonSerializer.Serialize(existingEntries, JsonOptions) + Environment.NewLine;
        var encoding = new UTF8Encoding(false);
        File.WriteAllText(filePath, output, encoding);
    }
}