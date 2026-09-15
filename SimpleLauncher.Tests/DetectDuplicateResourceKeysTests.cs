using System.Globalization;
using System.Text;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Scans every shared localization pack (SimpleLauncher.Core\Localization\strings.*.json)
///     for duplicate keys. Identical duplicates are automatically removed so that only one
///     remains. If duplicate keys have different values, the test fails.
/// </summary>
public class DetectDuplicateResourceKeysTests
{
    /// <summary>
    ///     Verifies that no localization resource file contains duplicate keys.
    /// </summary>
    [Fact]
    public void AllResourceFilesShouldHaveNoDuplicateKeys()
    {
        var resourceFiles = LocalizationResourceFile.GetLanguageFiles();

        if (resourceFiles.Count == 0)
            Assert.Fail("No resource files found in the shared localization folder.");

        var conflicts = new List<(string FileName, string Key, List<string> Values)>();

        foreach (var file in resourceFiles)
        {
            var entries = LocalizationResourceFile.ReadEntries(file);

            var grouped = entries
                .GroupBy(static e => e.Key, StringComparer.Ordinal)
                .Where(static g => g.Count() > 1)
                .ToList();

            var hasChanges = false;

            foreach (var group in grouped)
            {
                var values = group.Select(static e => e.Value).Distinct(StringComparer.Ordinal).ToList();

                if (values.Count == 1)
                {
                    // All duplicates are identical: deduplicate below.
                    hasChanges = true;
                }
                else
                {
                    conflicts.Add((Path.GetFileName(file), group.Key, values));
                }
            }

            if (hasChanges)
            {
                var deduplicated = new List<KeyValuePair<string, string>>();
                var seenKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                    if (seenKeys.Add(entry.Key))
                        deduplicated.Add(entry);

                LocalizationResourceFile.WriteEntries(file, deduplicated);
            }
        }

        if (conflicts.Count == 0)
            return;

        var message = new StringBuilder();
        message.AppendLine("Duplicate resource keys with conflicting values detected:");
        message.AppendLine();
        foreach (var conflict in conflicts)
        {
            message.AppendLine(CultureInfo.InvariantCulture, $"File: {conflict.FileName}, Key: '{conflict.Key}'");
            foreach (var value in conflict.Values) message.AppendLine(CultureInfo.InvariantCulture, $"  - {value}");
        }

        Assert.Fail(message.ToString());
    }
}
