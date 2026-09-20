using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Compares every resource key referenced via TryFindResource("Key") in the
///     SimpleLauncher C# source code against the shared English resource pack
///     (SimpleLauncher.Core\Localization\strings.en.json, also consumed by the Avalonia app).
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
        var stringsEnPath = LocalizationResourceFile.GetPath(LocalizationResourceFile.EnglishFileName);

        if (!File.Exists(stringsEnPath))
            Assert.Fail($"English resource file not found: {stringsEnPath}");

        // Collect keys referenced in C# together with their fallback value when available.
        var csKeys = CollectCsKeys(simpleLauncherPath);

        Dictionary<string, string> keysWithValues;
        List<string> keysWithoutValues;
        int missingCount;

        // The WPF and Avalonia suites both auto-fix this shared file; serialize the mutation.
        using (LocalizationResourceFile.AcquireFileLock())
        {
            // Keys already defined in the English resource file.
            var existingKeys = LocalizationResourceFile.ReadEntriesByKey(stringsEnPath).Keys
                .ToHashSet(StringComparer.Ordinal);

            // Determine missing keys (only from C# TryFindResource calls).
            var missingKeys = csKeys.Keys
                .Except(existingKeys, StringComparer.Ordinal)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            missingCount = missingKeys.Count;

            // Separate keys with known fallback values from keys without known values.
            keysWithValues = new Dictionary<string, string>(StringComparer.Ordinal);
            keysWithoutValues = new List<string>();

            foreach (var key in missingKeys)
            {
                if (csKeys.TryGetValue(key, out var fallback) && !string.IsNullOrEmpty(fallback))
                    keysWithValues[key] = fallback;
                else
                    keysWithoutValues.Add(key);
            }

            // Only auto-add keys that have a known non-empty fallback value.
            if (keysWithValues.Count > 0) AppendMissingEntries(stringsEnPath, keysWithValues);
        }

        if (missingCount == 0)
            return; // nothing missing – pass

        // Always fail when there are missing keys so the developer knows what happened.
        var message = new StringBuilder();
        message.AppendLine(CultureInfo.InvariantCulture,
            $"Found {missingCount} resource key(s) referenced in source code but missing from strings.en.json.");
        message.AppendLine();

        if (keysWithValues.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithValues.Count} key(s) were automatically added to strings.en.json:");
            message.AppendLine();
            foreach (var key in keysWithValues.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  - {key}");

            message.AppendLine();
        }

        if (keysWithoutValues.Count > 0)
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"The following {keysWithoutValues.Count} key(s) could not be automatically added because no fallback value is known. Please add them manually to strings.en.json:");
            message.AppendLine();
            foreach (var key in keysWithoutValues.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"  - {key}");
        }

        message.AppendLine();
        message.AppendLine(
            "After the English pack is complete, run the SimpleLauncher.ResourceTranslator project to propagate the new keys to the other language files.");

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

    /// <summary>
    ///     Appends the missing entries to strings.en.json, sorts everything alphabetically
    ///     by key, and rewrites the file.
    /// </summary>
    private static void AppendMissingEntries(string filePath, Dictionary<string, string> missingEntries)
    {
        var existingEntries = LocalizationResourceFile.ReadEntriesByKey(filePath);

        // Merge missing entries.
        foreach (var kvp in missingEntries)
        {
            if (!existingEntries.ContainsKey(kvp.Key))
                existingEntries[kvp.Key] = kvp.Value;
        }

        LocalizationResourceFile.WriteEntries(filePath, existingEntries);
    }

    private static bool IsBuildOrResourceFolder(string path)
    {
        return path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\Localization\\", StringComparison.OrdinalIgnoreCase)
               || path.Contains("\\References\\", StringComparison.OrdinalIgnoreCase);
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing groups are needed to extract key and fallback value")]
    [GeneratedRegex("""TryFindResource\(\s*"([^"]+)"\s*\)(?:\s*\?\?\s*"([^"]+)")?""", RegexOptions.Compiled, 1000)]
    private static partial Regex MyRegex();
}