using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Compares every resource key referenced via _resourceProvider.GetString("Key") or
///     _resourceProvider.GetString("Key", "default") in the SimpleLauncher source code
///     against the English resource dictionary (strings.en.xaml).
///     Features:
///     - Missing keys with a known default value are automatically appended to strings.en.xaml.
///     - Keys without a default value are reported for manual addition.
///     - Duplicate keys in strings.en.xaml are detected and reported.
/// </summary>
[SuppressMessage("ReSharper", "NullableWarningSuppressionIsUsed")]
public partial class DetectMissingResourceProviderKeysTests
{
    private static readonly XNamespace XNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    ///     Verifies that the English resource file contains every key referenced via _resourceProvider.GetString
    ///     and has no duplicate keys.
    /// </summary>
    [Fact]
    public void EnglishResourceFileShouldContainAllResourceProviderKeys()
    {
        var simpleLauncherPath = ProjectPathHelper.GetSimpleLauncherPath();
        var stringsEnPath = Path.Combine(simpleLauncherPath, "resources", "strings.en.xaml");
        var appXamlPath = Path.Combine(simpleLauncherPath, "App.xaml");

        if (!File.Exists(stringsEnPath))
            Assert.Fail($"English resource file not found: {stringsEnPath}");

        var existingEntries = ExtractEntriesFromXaml(stringsEnPath);
        var appKeys = File.Exists(appXamlPath)
            ? ExtractKeysFromXaml(appXamlPath)
            : new HashSet<string>(StringComparer.Ordinal);

        // Step 1: Detect and report duplicate keys in strings.en.xaml.
        var duplicateKeys = DetectDuplicateKeys(stringsEnPath);
        if (duplicateKeys.Count > 0)
        {
            RemoveDuplicateKeys(stringsEnPath);
            existingEntries = ExtractEntriesFromXaml(stringsEnPath);
        }

        // Step 2: Collect keys referenced via _resourceProvider.GetString with optional default values.
        var providerKeys = CollectResourceProviderKeys(simpleLauncherPath);

        // Step 4: Determine missing keys (exclude keys defined in App.xaml).
        var existingKeys = existingEntries.Keys.ToHashSet(StringComparer.Ordinal);
        var missingKeys = providerKeys.Keys
            .Except(existingKeys, StringComparer.Ordinal)
            .Except(appKeys, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Step 5: Separate keys with known default values from keys without defaults.
        var keysWithDefaults = new Dictionary<string, string>(StringComparer.Ordinal);
        var keysWithoutDefaults = new List<string>();

        foreach (var key in missingKeys)
        {
            if (providerKeys.TryGetValue(key, out var defaultValue) && !string.IsNullOrEmpty(defaultValue))
                keysWithDefaults[key] = defaultValue;
            else
                keysWithoutDefaults.Add(key);
        }

        // Step 6: Auto-add keys that have a known non-empty default value.
        if (keysWithDefaults.Count > 0) AppendMissingEntries(stringsEnPath, keysWithDefaults);

        // Step 7: Build failure message.
        var message = new StringBuilder();

        if (duplicateKeys.Count > 0)
        {
            message.AppendLine(
                "DUPLICATE KEYS detected in strings.en.xaml (duplicates were automatically removed, keeping first occurrence):");
            message.AppendLine();
            foreach (var kvp in duplicateKeys.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  Key: '{kvp.Key}' appeared {kvp.Value} times");

            message.AppendLine();
        }

        if (keysWithDefaults.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithDefaults.Count} key(s) were automatically added to strings.en.xaml:");
            message.AppendLine();
            foreach (var key in keysWithDefaults.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  - {key}");

            message.AppendLine();
        }

        if (keysWithoutDefaults.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithoutDefaults.Count} key(s) could not be automatically added because no default value was provided. Please add them manually to strings.en.xaml:");
            message.AppendLine();
            foreach (var key in keysWithoutDefaults.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  - {key}");
        }

        if (message.Length > 0) Assert.Fail(message.ToString());
    }

    /// <summary>
    ///     Scans .cs files for _resourceProvider.GetString("Key") and _resourceProvider.GetString("Key", "default").
    ///     Returns a dictionary mapping each key to its default value (empty string if no default provided).
    /// </summary>
    private static Dictionary<string, string> CollectResourceProviderKeys(string sourcePath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // Captures key + optional default value.
        var regex = MyRegex();

        var files = Directory.EnumerateFiles(sourcePath, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !IsBuildOrResourceFolder(f));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in regex.Matches(content))
            {
                var key = match.Groups[1].Value;
                var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : "";
                result[key] = defaultValue;
            }
        }

        return result;
    }

    /// <summary>
    ///     Extracts all key-value pairs from a XAML resource file using XML parsing.
    /// </summary>
    private static Dictionary<string, string> ExtractEntriesFromXaml(string xamlPath)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var doc = XDocument.Load(xamlPath, LoadOptions.PreserveWhitespace);
        var root = doc.Root;
        if (root == null)
            return entries;

        var elementsWithKey = root.Elements()
            .Where(static e => e.Attribute(XNamespace + "Key") != null)
            .ToList();

        foreach (var element in elementsWithKey)
        {
            var key = element.Attribute(XNamespace + "Key")!.Value;
            var value = element.Value;
            entries[key] = value;
        }

        return entries;
    }

    /// <summary>
    ///     Extracts just the keys from a XAML resource file using XML parsing.
    /// </summary>
    private static HashSet<string> ExtractKeysFromXaml(string xamlPath)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var doc = XDocument.Load(xamlPath, LoadOptions.None);
        var root = doc.Root;
        if (root == null)
            return keys;

        var elementsWithKey = root.Elements()
            .Where(static e => e.Attribute(XNamespace + "Key") != null)
            .ToList();

        foreach (var element in elementsWithKey) keys.Add(element.Attribute(XNamespace + "Key")!.Value);

        return keys;
    }

    /// <summary>
    ///     Detects duplicate x:Key entries in a XAML file using XML parsing.
    ///     Returns a dictionary mapping each duplicate key to its occurrence count.
    /// </summary>
    private static Dictionary<string, int> DetectDuplicateKeys(string xamlPath)
    {
        var doc = XDocument.Load(xamlPath, LoadOptions.None);
        var root = doc.Root;
        if (root == null)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        var elementsWithKey = root.Elements()
            .Where(static e => e.Attribute(XNamespace + "Key") != null)
            .ToList();

        var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var element in elementsWithKey)
        {
            var key = element.Attribute(XNamespace + "Key")!.Value;
            keyCounts[key] = keyCounts.GetValueOrDefault(key, 0) + 1;
        }

        return keyCounts.Where(static kvp => kvp.Value > 1)
            .ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Removes duplicate keys from a XAML file using XML parsing, keeping only the first occurrence.
    /// </summary>
    private static void RemoveDuplicateKeys(string xamlPath)
    {
        var doc = XDocument.Load(xamlPath, LoadOptions.PreserveWhitespace);
        var root = doc.Root;
        if (root == null)
            return;

        var elementsWithKey = root.Elements()
            .Where(static e => e.Attribute(XNamespace + "Key") != null)
            .ToList();

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elementsWithKey)
        {
            var key = element.Attribute(XNamespace + "Key")!.Value;
            if (!seenKeys.Add(key)) element.Remove();
        }

        WriteXamlFile(doc, xamlPath);
    }

    /// <summary>
    ///     Parses strings.en.xaml using XML parsing, appends the missing entries,
    ///     sorts everything alphabetically by key, and rewrites the file.
    /// </summary>
    private static void AppendMissingEntries(string filePath, Dictionary<string, string> missingEntries)
    {
        var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
        var root = doc.Root;
        if (root == null)
            return;

        var elementsWithKey = root.Elements()
            .Where(static e => e.Attribute(XNamespace + "Key") != null)
            .ToList();

        // Build existing entries from XML elements.
        var existingEntries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in elementsWithKey)
        {
            var key = element.Attribute(XNamespace + "Key")!.Value;
            existingEntries[key] = element.Value;
        }

        // Merge missing entries.
        var addedEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in missingEntries)
        {
            if (!existingEntries.ContainsKey(kvp.Key))
            {
                existingEntries[kvp.Key] = kvp.Value;
                addedEntries[kvp.Key] = kvp.Value;
            }
        }

        // Only rewrite if we actually added entries.
        if (addedEntries.Count == 0)
            return;

        // Remove all existing keyed elements.
        foreach (var element in elementsWithKey) element.Remove();

        // Build the file content manually with proper formatting.
        var lines = new List<string>
        {
            // Add XML declaration.
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            "",
            // Add ResourceDictionary opening tag.
            "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\">"
        };

        // Add each entry on its own line.
        foreach (var kvp in existingEntries)
        {
            var escapedValue = EscapeXml(kvp.Value);
            lines.Add($"    <system:String x:Key=\"{kvp.Key}\">{escapedValue}</system:String>");
        }

        // Add closing tag.
        lines.Add("</ResourceDictionary>");

        // Write the file with proper encoding.
        var encoding = new UTF8Encoding(false);
        File.WriteAllLines(filePath, lines, encoding);
    }

    /// <summary>
    ///     Writes an XDocument to file preserving the original format with entries on separate lines.
    /// </summary>
    private static void WriteXamlFile(XDocument doc, string filePath)
    {
        var root = doc.Root;
        if (root == null)
            return;

        var elementsWithKey = root.Elements()
            .Where(static e => e.Attribute(XNamespace + "Key") != null)
            .ToList();

        // Build entries dictionary.
        var entries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in elementsWithKey)
        {
            var key = element.Attribute(XNamespace + "Key")!.Value;
            entries[key] = element.Value;
        }

        // Build the file content manually with proper formatting.
        var lines = new List<string>
        {
            // Add XML declaration.
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            "",
            // Add ResourceDictionary opening tag.
            "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\">"
        };

        // Add each entry on its own line.
        foreach (var kvp in entries)
        {
            var escapedValue = EscapeXml(kvp.Value);
            lines.Add($"    <system:String x:Key=\"{kvp.Key}\">{escapedValue}</system:String>");
        }

        // Add closing tag.
        lines.Add("</ResourceDictionary>");

        // Write the file with proper encoding.
        var encoding = new UTF8Encoding(false);
        File.WriteAllLines(filePath, lines, encoding);
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

    private static bool IsBuildOrResourceFolder(string path)
    {
        return path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\resources\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\References\\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Matches: _resourceProvider.GetString("KEY") or _resourceProvider.GetString("KEY", "DEFAULT")
    ///     Group 1 = key, Group 2 = optional default value
    /// </summary>
    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing groups are needed to extract key and default value")]
    [GeneratedRegex("""_resourceProvider\.GetString\(\s*"([^"]+)"(?:\s*,\s*"([^"]*)")?\s*\)""", RegexOptions.Compiled,
        1000)]
    private static partial Regex MyRegex();
}