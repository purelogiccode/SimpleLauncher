using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Compares every resource key referenced via _resourceProvider.GetString in the
///     SimpleLauncher C# source code against the English resource dictionary (strings.en.xaml).
///     Missing keys are automatically appended to the resource file and the test
///     fails so the developer is informed of what was added.
/// </summary>
public partial class DetectMissingResourceStringsTests
{
    /// <summary>
    ///     Verifies that the English resource file contains every key referenced in the source code.
    /// </summary>
    [Fact]
    public void EnglishResourceFileShouldContainAllReferencedKeys()
    {
        var simpleLauncherPath = ProjectPathHelper.GetSimpleLauncherPath();
        var stringsEnPath = Path.Combine(simpleLauncherPath, "resources", "strings.en.xaml");

        if (!File.Exists(stringsEnPath))
            Assert.Fail($"English resource file not found: {stringsEnPath}");

        // Keys already defined in the English resource file.
        var existingKeys = ExtractKeysFromXaml(stringsEnPath);

        // Collect keys referenced in C# together with their fallback value when available.
        var csKeys = CollectCsKeys(simpleLauncherPath);

        // Determine missing keys (only from C# _resourceProvider.GetString calls).
        var missingKeys = csKeys.Keys
            .Except(existingKeys, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (missingKeys.Count == 0)
            return; // nothing missing – pass

        // Separate keys with known fallback values from keys without known values.
        var keysWithValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var keysWithoutValues = new List<string>();

        foreach (var key in missingKeys)
        {
            if (csKeys.TryGetValue(key, out var fallback) && !string.IsNullOrEmpty(fallback))
                keysWithValues[key] = fallback;
            else
                keysWithoutValues.Add(key);
        }

        // Only auto-add keys that have a known non-empty fallback value.
        if (keysWithValues.Count > 0) AppendMissingEntries(stringsEnPath, keysWithValues);

        // Always fail when there are missing keys so the developer knows what happened.
        var message = new StringBuilder();
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Found {missingKeys.Count} resource key(s) referenced in source code but missing from strings.en.xaml.");
        message.AppendLine();

        if (keysWithValues.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithValues.Count} key(s) were automatically added to strings.en.xaml:");
            message.AppendLine();
            foreach (var key in keysWithValues.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  - {key}");

            message.AppendLine();
        }

        if (keysWithoutValues.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithoutValues.Count} key(s) could not be automatically added because no fallback value is known. Please add them manually to strings.en.xaml:");
            message.AppendLine();
            foreach (var key in keysWithoutValues.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  - {key}");
        }

        Assert.Fail(message.ToString());
    }

    /// <summary>
    ///     Scans .cs files for TryFindResource("...") and captures the key.
    ///     When a literal fallback string is present (?? "...") it is stored as the value.
    /// </summary>
    private static Dictionary<string, string> CollectCsKeys(string sourcePath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // Captures key + optional literal fallback value.
        var regex = MyRegex();

        var files = Directory.EnumerateFiles(sourcePath, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !IsBuildOrResourceFolder(f));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in regex.Matches(content))
            {
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Success ? match.Groups[2].Value : "";
                result[key] = value;
            }
        }

        return result;
    }

    private static HashSet<string> ExtractKeysFromXaml(string xamlPath)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var content = File.ReadAllText(xamlPath);
        var regex = MyRegex2();

        foreach (Match match in regex.Matches(content))
            keys.Add(match.Groups[1].Value);

        return keys;
    }

    /// <summary>
    ///     Parses strings.en.xaml, appends the missing entries, sorts everything
    ///     alphabetically by key, and rewrites the file preserving the XML header.
    /// </summary>
    private static void AppendMissingEntries(string filePath, Dictionary<string, string> missingEntries)
    {
        var lines = File.ReadAllLines(filePath).ToList();
        var entryRegex = MyRegex3();

        var existingEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        var firstEntryIndex = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var match = entryRegex.Match(lines[i]);
            if (match.Success)
            {
                existingEntries[match.Groups[1].Value] = UnescapeXml(match.Groups[2].Value);
                if (firstEntryIndex == -1) firstEntryIndex = i;
            }
            else if (string.Equals(lines[i].Trim(), "</ResourceDictionary>", StringComparison.Ordinal))
            {
                if (firstEntryIndex == -1) firstEntryIndex = i;
            }
        }

        // Merge missing entries.
        foreach (var kvp in missingEntries)
        {
            if (!existingEntries.ContainsKey(kvp.Key))
                existingEntries[kvp.Key] = kvp.Value;
        }

        // Rebuild file: header + sorted entries + footer.
        var header = firstEntryIndex >= 0 ? lines.Take(firstEntryIndex).ToList() : lines.ToList();
        var sortedEntries = existingEntries
            .OrderBy(static e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static e => $"    <system:String x:Key=\"{e.Key}\">{EscapeXml(e.Value)}</system:String>")
            .ToList();
        var footer = new List<string> { "</ResourceDictionary>" };

        var encoding = new UTF8Encoding(false);
        File.WriteAllLines(filePath, header.Concat(sortedEntries).Concat(footer), encoding);
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string UnescapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&amp;", "&");
    }

    private static bool IsBuildOrResourceFolder(string path)
    {
        return path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\resources\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\References\\", StringComparison.OrdinalIgnoreCase);
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing groups are needed to extract key and fallback value")]
    [GeneratedRegex("""TryFindResource\(\s*"([^"]+)"\s*\)(?:\s*\?\?\s*"([^"]+)")?""", RegexOptions.Compiled, 1000)]
    private static partial Regex MyRegex();

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the key")]
    [GeneratedRegex("""
                    x:Key="([^"]+)"
                    """, RegexOptions.Compiled, 1000)]
    private static partial Regex MyRegex2();

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing groups are needed to extract key and value")]
    [GeneratedRegex("""^\s*<system:String x:Key="([^"]+)">(.*)</system:String>\s*$""", RegexOptions.None, 1000)]
    private static partial Regex MyRegex3();
}