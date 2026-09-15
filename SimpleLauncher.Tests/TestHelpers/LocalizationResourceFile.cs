using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SimpleLauncher.Tests.TestHelpers;

/// <summary>
///     Read/write helpers for the shared JSON localization packs
///     (SimpleLauncher.Core\Localization\strings.*.json), used by both the WPF
///     and Avalonia apps.
/// </summary>
public static class LocalizationResourceFile
{
    public const string EnglishFileName = "strings.en.json";

    private const string FileLockName = "SimpleLauncher.Localization.EnglishPack";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,

        // Keep non-ASCII characters readable instead of \uXXXX escapes.
        // Mirrors the ResourceTranslator JSON writer.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///     Returns the full path of a localization pack inside the shared folder.
    /// </summary>
    public static string GetPath(string fileName)
    {
        return Path.Combine(ProjectPathHelper.GetLocalizationResourcesPath(), fileName);
    }

    /// <summary>
    ///     Returns every strings.*.json pack in the shared folder, ordered by file name.
    /// </summary>
    public static List<string> GetLanguageFiles()
    {
        return Directory.EnumerateFiles(ProjectPathHelper.GetLocalizationResourcesPath(), "strings.*.json")
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    ///     Reads the entries of a JSON pack preserving file order and duplicate keys
    ///     (<see cref="JsonDocument" /> yields every property, including duplicates).
    /// </summary>
    public static List<KeyValuePair<string, string>> ReadEntries(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var entries = new List<KeyValuePair<string, string>>();
        foreach (var property in doc.RootElement.EnumerateObject())
            entries.Add(new KeyValuePair<string, string>(property.Name, property.Value.GetString() ?? ""));

        return entries;
    }

    /// <summary>
    ///     Reads a JSON pack into a key/value dictionary (last duplicate wins).
    /// </summary>
    public static Dictionary<string, string> ReadEntriesByKey(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in ReadEntries(path)) result[key] = value;

        return result;
    }

    /// <summary>
    ///     Writes entries sorted case-insensitively by key using the same format as the
    ///     ResourceTranslator JSON writer (2-space indent, UTF-8 without BOM).
    /// </summary>
    public static void WriteEntries(string path, IEnumerable<KeyValuePair<string, string>> entries)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries) sorted[key] = value;

        var json = JsonSerializer.Serialize(sorted, WriteOptions);
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
    }

    /// <summary>
    ///     Serializes localization-file mutations across test processes: both the WPF and
    ///     Avalonia suites auto-fix strings.en.json, and the test projects may run in parallel.
    /// </summary>
    public static IDisposable AcquireFileLock()
    {
        return new FileLock();
    }

    private sealed class FileLock : IDisposable
    {
        private readonly Mutex _mutex = new(false, FileLockName);

        public FileLock()
        {
            if (!_mutex.WaitOne(TimeSpan.FromMinutes(3)))
                throw new TimeoutException("Timed out waiting for the localization file lock.");
        }

        public void Dispose()
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
