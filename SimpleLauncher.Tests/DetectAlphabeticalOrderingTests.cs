using System.Globalization;
using System.Text;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Verifies that every shared localization pack (SimpleLauncher.Core\Localization\strings.*.json)
///     has its entries sorted alphabetically by key using case-insensitive ordinal comparison.
///     Files that are out of order are automatically re-sorted and the test fails
///     so the developer knows the file was modified.
/// </summary>
public class DetectAlphabeticalOrderingTests
{
    /// <summary>
    ///     Verifies that all localization resource files have their entries sorted alphabetically by key.
    /// </summary>
    [Fact]
    public void AllResourceFilesShouldBeSortedAlphabeticallyByKey()
    {
        var resourceFiles = LocalizationResourceFile.GetLanguageFiles();

        if (resourceFiles.Count == 0)
            Assert.Fail("No resource files found in the shared localization folder.");

        var unsortedFiles = new List<string>();

        foreach (var file in resourceFiles)
        {
            var entries = LocalizationResourceFile.ReadEntries(file);
            var keys = entries.ConvertAll(static e => e.Key);

            var isSorted = keys.SequenceEqual(
                keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            if (!isSorted)
            {
                unsortedFiles.Add(Path.GetFileName(file));
                LocalizationResourceFile.WriteEntries(file, entries);
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
}