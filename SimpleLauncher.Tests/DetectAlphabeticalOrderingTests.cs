using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Verifies that every localization resource file (strings.*.xaml) has its entries
///     sorted alphabetically by x:Key using case-insensitive ordinal comparison.
///     Files that are out of order are automatically re-sorted and the test fails
///     so the developer knows the file was modified.
/// </summary>
public partial class DetectAlphabeticalOrderingTests
{
    /// <summary>
    ///     Verifies that all localization resource files have their entries sorted alphabetically by key.
    /// </summary>
    [Fact]
    public void AllResourceFilesShouldBeSortedAlphabeticallyByKey()
    {
        var resourcesPath = Path.Combine(ProjectPathHelper.GetSimpleLauncherPath(), "resources");
        var resourceFiles = Directory.EnumerateFiles(resourcesPath, "strings.*.xaml")
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resourceFiles.Count == 0)
            Assert.Fail($"No resource files found in: {resourcesPath}");

        var entryRegex = MyRegex();
        var unsortedFiles = new List<string>();

        foreach (var file in resourceFiles)
        {
            var lines = File.ReadAllLines(file).ToList();
            var entries = new List<(string Key, string Line)>();
            var firstEntryIndex = -1;

            for (var i = 0; i < lines.Count; i++)
            {
                var match = entryRegex.Match(lines[i]);
                if (match.Success)
                {
                    entries.Add((match.Groups[1].Value, lines[i]));
                    if (firstEntryIndex == -1) firstEntryIndex = i;
                }
                else if (string.Equals(lines[i].Trim(), "</ResourceDictionary>", StringComparison.Ordinal))
                {
                    if (firstEntryIndex == -1) firstEntryIndex = i;
                }
            }

            var isSorted = entries
                .Select(static e => e.Key)
                .SequenceEqual(
                    entries.Select(static e => e.Key).OrderBy(static k => k, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            if (!isSorted)
            {
                unsortedFiles.Add(Path.GetFileName(file));

                var header = firstEntryIndex >= 0 ? lines.Take(firstEntryIndex).ToList() : lines.ToList();
                var sortedEntries = entries
                    .OrderBy(static e => e.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(static e => e.Line)
                    .ToList();
                var footer = new List<string> { "</ResourceDictionary>" };

                var encoding = new UTF8Encoding(false);
                File.WriteAllLines(file, header.Concat(sortedEntries).Concat(footer), encoding);
            }
        }

        if (unsortedFiles.Count == 0)
            return;

        var message = new StringBuilder();
        message.AppendLine(
            "The following resource files were not sorted alphabetically by key and have been auto-sorted:");
        message.AppendLine();
        foreach (var fileName in unsortedFiles) message.AppendLine(CultureInfo.InvariantCulture, $"  - {fileName}");

        Assert.Fail(message.ToString());
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the key")]
    [GeneratedRegex("""^\s*<system:String x:Key="([^"]+)">(.*)</system:String>\s*$""", RegexOptions.None, 1000)]
    private static partial Regex MyRegex();
}