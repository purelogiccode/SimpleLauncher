using System.Collections;
using System.Resources;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Loads every localization pack embedded in the built assembly as a pack resource
///     (SimpleLauncher.g.resources: resources/strings.*.json, sourced from
///     SimpleLauncher.Core\Localization) and fails if any pack cannot be parsed,
///     is not a JSON object, or is empty.
/// </summary>
public class ResourceFileLoadingTests
{
    private const string ResourcePrefix = "resources/strings.";

    /// <summary>
    ///     Verifies that all embedded localization packs can be parsed without errors.
    /// </summary>
    [Fact]
    public void AllResourceFilesShouldLoadWithoutErrors()
    {
        var assembly = typeof(App).Assembly;
        using var bundleStream = assembly.GetManifestResourceStream("SimpleLauncher.g.resources");
        Assert.NotNull(bundleStream);

        using var reader = new ResourceReader(bundleStream);

        var entries = reader.Cast<DictionaryEntry>()
            .Where(static entry => entry.Key is string)
            .ToDictionary(static entry => (string)entry.Key!, static entry => entry.Value,
                StringComparer.OrdinalIgnoreCase);

        var resourceNames = entries.Keys
            .Where(static key => key.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)
                                 && key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resourceNames.Count == 0)
            Assert.Fail($"No embedded localization resources found with prefix '{ResourcePrefix}'.");

        var failures = new List<(string FileName, string Error)>();

        foreach (var resourceName in resourceNames)
        {
            try
            {
                var json = entries[resourceName] switch
                {
                    byte[] bytes => Encoding.UTF8.GetString(bytes),
                    Stream stream => ReadAllText(stream),
                    string text => text,
                    var other => throw new InvalidDataException(
                        $"Unexpected resource type '{other?.GetType().Name}'.")
                };

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    failures.Add((resourceName, $"Root element is {doc.RootElement.ValueKind}, expected Object."));
                    continue;
                }

                if (doc.RootElement.EnumerateObject().Any())
                {
                    continue;
                }

                failures.Add((resourceName, "Resource pack contains no entries."));
            }
            catch (JsonException ex)
            {
                failures.Add((resourceName, $"JSON parse error: {ex.Message}"));
            }
            catch (Exception ex)
            {
                failures.Add((resourceName, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        if (failures.Count == 0)
            return;

        var message = "Failed to load the following resource files:\n\n" +
                      string.Join(
                          "\n",
                          failures.Select(static f => $"File: {f.FileName}\nError: {f.Error}")
                      );

        Assert.Fail(message);
    }

    private static string ReadAllText(Stream stream)
    {
        using var textReader = new StreamReader(stream, Encoding.UTF8);
        return textReader.ReadToEnd();
    }
}