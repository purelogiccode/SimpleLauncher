using System.Text;
using System.Text.Json;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     JSON-based localization service. Loads strings from the language packs embedded in the
///     assembly as SimpleLauncher.Avalonia.Resources.strings.{lang}.json (the Avalonia equivalent
///     of the WPF pack resources), or from a directory on disk when a test seam is supplied.
///     Falls back to English for missing keys.
/// </summary>
public class LocalizationService
{
    /// <summary>
    ///     Manifest-resource prefix for the embedded language packs. Matches the
    ///     LogicalName in SimpleLauncher.Avalonia.csproj.
    /// </summary>
    internal const string EmbeddedResourcePrefix = "SimpleLauncher.Avalonia.Resources.strings.";

    internal const string EmbeddedResourceSuffix = ".json";

    /// <summary>
    ///     Available languages with display names (canonical set matches the WPF app).
    /// </summary>
    public static readonly Dictionary<string, string> AvailableLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "العربية",
        ["bn"] = "বাংলা",
        ["de"] = "Deutsch",
        ["en"] = "English",
        ["es"] = "Español",
        ["fr"] = "Français",
        ["hi"] = "हिन्दी",
        ["id"] = "Indonesian (Malay)",
        ["it"] = "Italiano",
        ["ja"] = "日本語",
        ["ko"] = "한국어",
        ["nl"] = "Nederlands",
        ["pt-BR"] = "Português",
        ["ru"] = "Русский",
        ["tr"] = "Türkçe",
        ["ur"] = "اردو",
        ["vi"] = "Tiếng Việt",
        ["zh-Hans"] = "简体中文"
    };

    private readonly Dictionary<string, string> _enFallback;
    private readonly string? _resourcesDir;
    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService() : this(null)
    {
    }

    /// <summary>
    ///     Test seam: allows tests to load strings from an isolated directory instead of
    ///     the embedded packs (which mutate nothing and are shared by every test).
    /// </summary>
    internal LocalizationService(string? resourcesDir)
    {
        _resourcesDir = resourcesDir;
        LoadLanguage("en");
        _enFallback = new Dictionary<string, string>(_strings, StringComparer.OrdinalIgnoreCase);
    }

    public string CurrentLanguage { get; private set; } = "en";

    public IReadOnlyDictionary<string, string> AllStrings => _strings;

    /// <summary>
    ///     Loads a language file. Falls back to English for missing keys.
    /// </summary>
    public void LoadLanguage(string lang)
    {
        CurrentLanguage = lang;
        _strings.Clear();

        var (canonicalCode, sourceName, json) = LoadLanguageSource(lang);

        // Canonicalize CurrentLanguage to the actual pack's code (e.g. 'pt-br' -> 'pt-BR')
        if (canonicalCode is not null)
            CurrentLanguage = canonicalCode;

        if (json is not null)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict is not null)
                {
                    foreach (var kvp in dict)
                        _strings[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                // Fall through to English
                Log.Error(ex, "Failed to load language file {Path}", sourceName ?? lang);
            }
        }

        // If not English, merge English fallback for missing keys
        if (!string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) && _enFallback.Count > 0)
        {
            foreach (var kvp in _enFallback)
                _strings.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    ///     Resolves a language pack from the embedded manifest resources (the default) or,
    ///     when the test seam directory is supplied, from files on disk. The lookup is
    ///     case-insensitive because settings may store codes like 'pt-br' or 'zh-hans'
    ///     from the WPF app while the packs use 'pt-BR'/'zh-Hans'.
    /// </summary>
    /// <returns>
    ///     The canonical language code, a human-readable source name for logging, and the
    ///     pack JSON; nulls when the language is not available.
    /// </returns>
    private (string? CanonicalCode, string? SourceName, string? Json) LoadLanguageSource(string lang)
    {
        if (_resourcesDir is not null)
        {
            // Test seam: packs are read from an isolated directory on disk.
            var path = Directory.Exists(_resourcesDir)
                ? Directory.EnumerateFiles(_resourcesDir, "strings.*.json")
                    .FirstOrDefault(f => string.Equals(
                        Path.GetFileNameWithoutExtension(f).Substring("strings.".Length),
                        lang, StringComparison.OrdinalIgnoreCase))
                : null;
            path ??= Path.Combine(_resourcesDir, $"strings.{lang}.json");

            if (!File.Exists(path)) return (null, path, null);

            var fileName = Path.GetFileNameWithoutExtension(path);
            var fileCode = fileName.StartsWith("strings.", StringComparison.Ordinal)
                ? fileName["strings.".Length..]
                : lang;

            try
            {
                return (fileCode, path, File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load language file {Path}", path);
                return (fileCode, path, null);
            }
        }

        var assembly = typeof(LocalizationService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(EmbeddedResourcePrefix, StringComparison.Ordinal)
                                  && name.EndsWith(EmbeddedResourceSuffix, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(name => string.Equals(
                name[EmbeddedResourcePrefix.Length..^EmbeddedResourceSuffix.Length],
                lang, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null) return (null, null, null);

        var canonicalCode = resourceName[EmbeddedResourcePrefix.Length..^EmbeddedResourceSuffix.Length];

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return (canonicalCode, resourceName, null);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return (canonicalCode, resourceName, reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load language file {Path}", resourceName);
            return (canonicalCode, resourceName, null);
        }
    }

    /// <summary>
    ///     Gets a localized string by key. Returns the key itself if not found.
    /// </summary>
    public string GetString(string key)
    {
        return _strings.GetValueOrDefault(key, key);
    }

    /// <summary>
    ///     Gets a localized string by key, returning <paramref name="fallback" /> when the key is missing.
    /// </summary>
    public string GetString(string key, string fallback)
    {
        return _strings.GetValueOrDefault(key, fallback);
    }

    /// <summary>
    ///     Gets a formatted localized string.
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        return args.Length > 0 ? string.Format(template, args) : template;
    }
}