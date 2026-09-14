using System.Text;
using System.Text.RegularExpressions;

namespace SimpleLauncher.ResourceTranslator.Services;

/// <summary>
///     Provides functionality to update XAML resource files with translations.
/// </summary>
public static class XamlResourceWriter
{
    /// <summary>
    ///     Updates a XAML resource file with new translations and removes duplicate keys.
    /// </summary>
    /// <param name="filePath">The path to the XAML resource file.</param>
    /// <param name="newTranslations">Dictionary of key-value pairs to add or update.</param>
    /// <param name="duplicatesToRemove">List of duplicate keys to remove.</param>
    public static void UpdateResourceFile(
        string filePath,
        IDictionary<string, string> newTranslations,
        IList<string> duplicatesToRemove)
    {
        var content = File.ReadAllText(filePath);

        var entryRegex = XmlHelper.EntryRegex();

        // Extract header (before first entry)
        var firstMatch = entryRegex.Match(content);
        string header;
        if (firstMatch.Success)
        {
            header = content[..firstMatch.Index];
        }
        else
        {
            var closingIndex = content.LastIndexOf("</ResourceDictionary>", StringComparison.Ordinal);
            if (closingIndex < 0)
                throw new InvalidOperationException($"Could not parse XAML structure in {filePath}");

            header = content[..closingIndex];
        }

        // Parse existing entries, skipping duplicates
        var existingEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicatesRemoved = new HashSet<string>(duplicatesToRemove, StringComparer.Ordinal);

        foreach (Match match in entryRegex.Matches(content))
        {
            var key = match.Groups[1].Value;
            if (!duplicatesRemoved.Contains(key) && !existingEntries.ContainsKey(key))
                existingEntries[key] = XmlHelper.UnescapeXml(match.Groups[2].Value);
        }

        // Merge new translations
        foreach (var kvp in newTranslations) existingEntries[kvp.Key] = kvp.Value;

        // Sort by key and build output
        var sb = new StringBuilder();
        sb.Append(header);
        if (!header.EndsWith('\n'))
            sb.AppendLine();

        foreach (var e in existingEntries.OrderBy(static e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("    <system:String x:Key=\"");
            sb.Append(e.Key);
            sb.Append("\">");
            sb.Append(XmlHelper.EscapeXml(e.Value));
            sb.AppendLine("</system:String>");
        }

        sb.AppendLine("</ResourceDictionary>");

        var encoding = new UTF8Encoding(false);
        File.WriteAllText(filePath, sb.ToString(), encoding);
    }
}