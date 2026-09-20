using System.Text.Json;
using SimpleLauncher.ResourceTranslator.Services;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Regression tests for the translation tooling: duplicate keys keep their first
///     occurrence, and blank values are treated as missing instead of becoming permanent.
/// </summary>
public class ResourceTranslatorMaintenanceTests
{
    [Fact]
    public void UpdateResourceFile_KeepsFirstDuplicateAndIgnoresBlankTranslations()
    {
        var directory = Directory.CreateTempSubdirectory("sol-resx-");
        try
        {
            var filePath = Path.Combine(directory.FullName, "strings.xx.json");
            File.WriteAllText(filePath, """
                                        {
                                          "A": "first",
                                          "B": "bee",
                                          "A": "second"
                                        }
                                        """);

            JsonResourceWriter.UpdateResourceFile(filePath, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["C"] = "see",
                ["B"] = ""
            });

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var entries = doc.RootElement.EnumerateObject()
                .ToDictionary(static p => p.Name, static p => p.Value.GetString() ?? "", StringComparer.Ordinal);

            // The old writer dropped every occurrence of a duplicated key.
            Assert.Equal("first", entries["A"]);

            // A blank fill must not overwrite an existing translation.
            Assert.Equal("bee", entries["B"]);
            Assert.Equal("see", entries["C"]);
            Assert.Equal(3, entries.Count);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void AnalyzeAllLanguages_TreatsBlankValuesAsMissing()
    {
        var directory = Directory.CreateTempSubdirectory("sol-resx-");
        try
        {
            var englishPath = Path.Combine(directory.FullName, "strings.en.json");
            File.WriteAllText(englishPath, """{"A":"a","B":"b"}""");
            File.WriteAllText(Path.Combine(directory.FullName, "strings.xx.json"), """{"A":"","B":"bee"}""");

            var englishKeys = JsonResourceAnalyzer.ReadEnglishKeys(englishPath);
            var batch = Assert.Single(JsonResourceAnalyzer.AnalyzeAllLanguages(directory.FullName, englishKeys));

            // The blank value must be retried; the presence check alone left it "" forever.
            var missing = Assert.Single(batch.MissingKeys);
            Assert.Equal("A", missing.Key);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}