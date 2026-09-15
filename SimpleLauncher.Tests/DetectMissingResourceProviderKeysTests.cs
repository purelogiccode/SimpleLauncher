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
///     against the shared English resource pack (SimpleLauncher.Core\Localization\strings.en.json,
///     also consumed by the Avalonia app).
///     Features:
///     - Missing keys with a known default value are automatically appended to strings.en.json.
///     - Keys without a default value are reported for manual addition.
///     - Duplicate keys in strings.en.json are detected and reported.
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
        var stringsEnPath = LocalizationResourceFile.GetPath(LocalizationResourceFile.EnglishFileName);
        var appXamlPath = Path.Combine(simpleLauncherPath, "App.xaml");

        if (!File.Exists(stringsEnPath))
            Assert.Fail($"English resource file not found: {stringsEnPath}");

        var appKeys = File.Exists(appXamlPath)
            ? ExtractKeysFromXaml(appXamlPath)
            : new HashSet<string>(StringComparer.Ordinal);

        var providerKeys = CollectResourceProviderKeys(simpleLauncherPath);

        Dictionary<string, int> duplicateKeys;
        Dictionary<string, string> existingEntries;
        Dictionary<string, string> keysWithDefaults;
        List<string> keysWithoutDefaults;

        // The WPF and Avalonia suites both auto-fix this shared file; serialize the mutation.
        using (LocalizationResourceFile.AcquireFileLock())
        {
            existingEntries = LocalizationResourceFile.ReadEntriesByKey(stringsEnPath);

            // Step 1: Detect and report duplicate keys, rewriting the deduplicated pack if found.
            duplicateKeys = DetectDuplicateKeys(stringsEnPath);
            if (duplicateKeys.Count > 0)
                LocalizationResourceFile.WriteEntries(stringsEnPath, existingEntries);

            // Step 2: Determine missing keys (exclude keys defined in App.xaml).
            var existingKeys = existingEntries.Keys.ToHashSet(StringComparer.Ordinal);
            var missingKeys = providerKeys.Keys
                .Except(existingKeys, StringComparer.Ordinal)
                .Except(appKeys, StringComparer.Ordinal)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Step 3: Separate keys with known default values from keys without defaults.
            keysWithDefaults = new Dictionary<string, string>(StringComparer.Ordinal);
            keysWithoutDefaults = new List<string>();

            foreach (var key in missingKeys)
            {
                if (providerKeys.TryGetValue(key, out var defaultValue) && !string.IsNullOrEmpty(defaultValue))
                    keysWithDefaults[key] = defaultValue;
                else
                    keysWithoutDefaults.Add(key);
            }

            // Step 4: Auto-add keys that have a known non-empty default value.
            if (keysWithDefaults.Count > 0) AppendMissingEntries(stringsEnPath, keysWithDefaults);
        }

        // Step 5: Build failure message.
        var message = new StringBuilder();

        if (duplicateKeys.Count > 0)
        {
            message.AppendLine(
                "DUPLICATE KEYS detected in strings.en.json (duplicates were automatically removed, keeping the last value):");
            message.AppendLine();
            foreach (var kvp in duplicateKeys.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  Key: '{kvp.Key}' appeared {kvp.Value} times");

            message.AppendLine();
        }

        if (keysWithDefaults.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithDefaults.Count} key(s) were automatically added to strings.en.json:");
            message.AppendLine();
            foreach (var key in keysWithDefaults.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  - {key}");

            message.AppendLine();
        }

        if (keysWithoutDefaults.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithoutDefaults.Count} key(s) could not be automatically added because no default value was provided. Please add them manually to strings.en.json:");
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
    ///     Extracts just the keys from a XAML file (App.xaml) using XML parsing.
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
    ///     Detects duplicate keys in a JSON localization pack. Returns a dictionary mapping
    ///     each duplicate key to its occurrence count.
    /// </summary>
    private static Dictionary<string, int> DetectDuplicateKeys(string jsonPath)
    {
        var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, _) in LocalizationResourceFile.ReadEntries(jsonPath))
            keyCounts[key] = keyCounts.GetValueOrDefault(key, 0) + 1;

        return keyCounts.Where(static kvp => kvp.Value > 1)
            .ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Appends the missing entries to strings.en.json, sorts everything alphabetically
    ///     by key, and rewrites the file.
    /// </summary>
    private static void AppendMissingEntries(string filePath, Dictionary<string, string> missingEntries)
    {
        var existingEntries = LocalizationResourceFile.ReadEntriesByKey(filePath);

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

        LocalizationResourceFile.WriteEntries(filePath, existingEntries);
    }

    private static bool IsBuildOrResourceFolder(string path)
    {
        return path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\Localization\\", StringComparison.OrdinalIgnoreCase)
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
