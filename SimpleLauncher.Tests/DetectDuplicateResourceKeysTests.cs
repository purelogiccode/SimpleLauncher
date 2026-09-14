using System.Globalization;
using System.Text;
using System.Xml.Linq;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Scans every localization resource file (strings.*.xaml) for duplicate x:Key entries.
///     Duplicate keys with identical XML representations are automatically removed so that
///     only one remains. If duplicate keys have different values, the test fails.
/// </summary>
public class DetectDuplicateResourceKeysTests
{
    /// <summary>
    ///     Verifies that no localization resource file contains duplicate x:Key entries.
    /// </summary>
    [Fact]
    public void AllResourceFilesShouldHaveNoDuplicateKeys()
    {
        var resourcesPath = Path.Combine(ProjectPathHelper.GetSimpleLauncherPath(), "resources");
        var resourceFiles = Directory.EnumerateFiles(resourcesPath, "strings.*.xaml")
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resourceFiles.Count == 0)
            Assert.Fail($"No resource files found in: {resourcesPath}");

        var conflicts = new List<(string FileName, string Key, List<string> Values)>();
        XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (var file in resourceFiles)
        {
            var doc = XDocument.Load(file, LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root == null)
                continue;

            var elementsWithKey = root.Elements()
                .Where(e => e.Attribute(xNamespace + "Key") != null)
                .ToList();

            var grouped = elementsWithKey
                // ReSharper disable once NullableWarningSuppressionIsUsed
                .GroupBy(e => e.Attribute(xNamespace + "Key")!.Value, StringComparer.Ordinal)
                .Where(static g => g.Count() > 1)
                .ToList();

            var hasChanges = false;

            foreach (var group in grouped)
            {
                var key = group.Key;
                var elements = group.ToList();

                var distinctRepresentations = elements
                    .Select(static e => e.ToString(SaveOptions.DisableFormatting))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (distinctRepresentations.Count == 1)
                {
                    // All duplicates are identical: keep the first, remove the rest.
                    for (var i = 1; i < elements.Count; i++)
                    {
                        elements[i].Remove();
                        hasChanges = true;
                    }
                }
                else
                {
                    var values = elements
                            .ConvertAll(static e => e.ToString(SaveOptions.DisableFormatting))
                        ;
                    conflicts.Add((Path.GetFileName(file), key, values));
                }
            }

            // Ensure alphabetical order (case-insensitive) after duplicate removal.
            var allKeyedElements = root.Elements()
                .Where(e => e.Attribute(xNamespace + "Key") != null)
                .ToList();

            var sortedElements = allKeyedElements
                .OrderBy(
                    // ReSharper disable once NullableWarningSuppressionIsUsed
                    e => e.Attribute(xNamespace + "Key")!.Value,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!allKeyedElements.SequenceEqual(sortedElements)) hasChanges = true;

            if (hasChanges)
            {
                // Rebuild the file from scratch to avoid orphaned whitespace nodes
                // that XDocument.Load(LoadOptions.PreserveWhitespace) + doc.Save() would leave.
                var entries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var freshElements = root.Elements()
                    .Where(e => e.Attribute(xNamespace + "Key") != null)
                    .ToList();

                foreach (var element in freshElements)
                {
                    // ReSharper disable once NullableWarningSuppressionIsUsed
                    var key = element.Attribute(xNamespace + "Key")!.Value;
                    entries[key] = element.Value;
                }

                var lines = new List<string>
                {
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                    "",
                    "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\">"
                };

                foreach (var kvp in entries)
                {
                    var escapedValue = EscapeXml(kvp.Value);
                    lines.Add($"    <system:String x:Key=\"{kvp.Key}\">{escapedValue}</system:String>");
                }

                lines.Add("</ResourceDictionary>");

                var encoding = new UTF8Encoding(false);
                File.WriteAllLines(file, lines, encoding);
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

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}