using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using SimpleLauncher.Avalonia.Tests.TestHelpers;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Compares every resource key referenced in the SimpleLauncher.Avalonia source code
///     (C# LocalizationService.GetString calls and XAML {ext:Translate} usages) against the
///     English resource dictionary (Resources/strings.en.json).
///     Missing keys are automatically appended to the resource file and the test
///     fails so the developer is informed of what was added.
/// </summary>
public partial class DetectMissingResourceStringsTests
{
    /// <summary>
    ///     Verifies that the English resource file contains every key referenced in the source code.
    /// </summary>
    [Fact]
    public void EnglishResourceFileShouldContainAllKeysReferencedInSourceCode()
    {
        var avaloniaPath = AvaloniaProjectPathHelper.GetAvaloniaProjectPath();
        var stringsEnPath = Path.Combine(AvaloniaProjectPathHelper.GetLocalizationResourcesPath(), "strings.en.json");

        Assert.True(File.Exists(stringsEnPath), $"English resource file not found: {stringsEnPath}");

        // Keys already defined in the English resource file.
        var existingKeys = LoadKeys(stringsEnPath).Keys.ToHashSet(StringComparer.Ordinal);

        // Collect keys referenced in source code together with their English fallback value when available.
        var csKeys = CollectCsKeys(avaloniaPath);
        var axamlKeys = CollectAxamlKeys(avaloniaPath);

        // Determine missing keys.
        var missingKeys = csKeys.Keys
            .Concat(axamlKeys)
            .Where(k => !existingKeys.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (missingKeys.Count == 0)
            return; // nothing missing — pass

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
        message.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Found {missingKeys.Count} resource key(s) referenced in source code but missing from strings.en.json."));
        message.AppendLine();

        if (keysWithValues.Count > 0)
        {
            message.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"The following {keysWithValues.Count} key(s) were automatically added to strings.en.json (with their English fallback):"));
            message.AppendLine();
            foreach (var key in keysWithValues.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  - {key}"));

            message.AppendLine();
        }

        if (keysWithoutValues.Count > 0)
        {
            message.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"The following {keysWithoutValues.Count} key(s) could not be automatically added because no fallback value is known. Please add them manually to strings.en.json:"));
            message.AppendLine();
            foreach (var key in keysWithoutValues.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  - {key}"));
        }

        message.AppendLine();
        message.AppendLine(
            "After the English pack is complete, run the SimpleLauncher.ResourceTranslator project to propagate the new keys to the other language files.");

        Assert.Fail(message.ToString());
    }

    /// <summary>
    ///     Scans .cs files for LocalizationService GetString("KEY") and GetString("KEY", "FALLBACK") calls
    ///     and captures the key (and the literal fallback value when present).
    /// </summary>
    private static Dictionary<string, string> CollectCsKeys(string sourcePath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        var files = Directory.EnumerateFiles(sourcePath, "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);

            // GetString("KEY", "FALLBACK") — two-argument call with a string literal binds to
            // GetString(string key, string fallback); three or more arguments bind to the
            // params object[] overload, so those are NOT captured as fallbacks.
            foreach (Match match in GetStringWithFallbackRegex().Matches(content))
            {
                var key = Unescape(match.Groups[1].Value);
                var value = Unescape(match.Groups[2].Value);
                result[key] = value;
            }

            // GetString("KEY") or GetString("KEY", variableOrArg)
            foreach (Match match in GetStringKeyOnlyRegex().Matches(content))
            {
                var key = Unescape(match.Groups[1].Value);
                result.TryAdd(key, "");
            }
        }

        return result;
    }

    /// <summary>
    ///     Scans .axaml files for {ext:Translate KEY} markup extension usages and captures the key.
    /// </summary>
    private static HashSet<string> CollectAxamlKeys(string sourcePath)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        var files = Directory.EnumerateFiles(sourcePath, "*.axaml", SearchOption.AllDirectories)
            .Where(IsSourceFile);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in TranslateExtensionRegex().Matches(content))
                keys.Add(match.Groups[1].Value);
        }

        return keys;
    }

    private static bool IsSourceFile(string path)
    {
        return !path.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase)
               && !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               && !path.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase)
               && !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Unescape(string text)
    {
        return string.IsNullOrEmpty(text) ? text : text.Replace("\\\"", "\"").Replace(@"\\", "\\");
    }

    /// <summary>
    ///     Parses strings.en.json, appends the missing entries, sorts everything
    ///     case-insensitively by key, and rewrites the file.
    ///     Mirrors the output format of the SimpleLauncher.ResourceTranslator JSON writer
    ///     (2-space indented, UTF-8 without BOM, OrdinalIgnoreCase sort).
    ///     The WPF test suite also auto-fixes this shared file, so the mutation is
    ///     serialized with a cross-process mutex.
    /// </summary>
    private static void AppendMissingEntries(string filePath, Dictionary<string, string> missingEntries)
    {
        using var fileLock = new Mutex(false, "SimpleLauncher.Localization.EnglishPack");
        if (!fileLock.WaitOne(TimeSpan.FromMinutes(3)))
            throw new TimeoutException("Timed out waiting for the localization file lock.");

        try
        {
            var existingEntries = LoadKeys(filePath);

            foreach (var kvp in missingEntries)
            {
                if (!existingEntries.ContainsKey(kvp.Key))
                    existingEntries[kvp.Key] = kvp.Value;
            }

            var sorted = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in existingEntries) sorted[kvp.Key] = kvp.Value;

            var json = JsonSerializer.Serialize(sorted, JsonOptions);
            File.WriteAllText(filePath, json + Environment.NewLine, new UTF8Encoding(false));
        }
        finally
        {
            fileLock.ReleaseMutex();
        }
    }

    private static Dictionary<string, string> LoadKeys(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
            result[property.Name] = property.Value.GetString() ?? "";

        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,

        // Keep non-ASCII characters (e.g. em-dashes) as readable UTF-8 text
        // instead of \uXXXX escape sequences. Matches the translator's writer.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing groups are needed to extract key and fallback value")]
    [GeneratedRegex("""GetString\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\)""", RegexOptions.Compiled,
        1000)]
    private static partial Regex GetStringWithFallbackRegex();

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the key")]
    [GeneratedRegex("""GetString\(\s*"((?:[^"\\]|\\.)*)"\s*[,)]""", RegexOptions.Compiled, 1000)]
    private static partial Regex GetStringKeyOnlyRegex();

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the key")]
    [GeneratedRegex("""ext:Translate\s+([A-Za-z0-9_.]+)""", RegexOptions.Compiled, 1000)]
    private static partial Regex TranslateExtensionRegex();
}