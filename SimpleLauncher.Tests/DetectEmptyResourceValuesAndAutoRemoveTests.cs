using System.Globalization;
using System.Text;
using System.Xml.Linq;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Scans every localization resource file (strings.*.xaml) for entries
///     whose value is empty or whitespace and automatically removes them.
///     The test fails so the developer is informed of what was cleaned up.
/// </summary>
public class DetectEmptyResourceValuesAndAutoRemoveTests
{
    /// <summary>
    ///     Verifies that no localization resource file contains empty or whitespace-only values, automatically removing them
    ///     if found.
    /// </summary>
    [Fact]
    public void AllResourceFilesShouldHaveNoEmptyValuesAndAutoRemove()
    {
        var resourcesPath = Path.Combine(ProjectPathHelper.GetSimpleLauncherPath(), "resources");
        var resourceFiles = Directory.EnumerateFiles(resourcesPath, "strings.*.xaml")
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resourceFiles.Count == 0)
            Assert.Fail($"No resource files found in: {resourcesPath}");

        var removedEntriesByFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
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

            var hasChanges = false;

            foreach (var element in elementsWithKey)
            {
                // Self-closing tags or tags with no/whitespace text are considered empty.
                if (element.IsEmpty || string.IsNullOrWhiteSpace(element.Value))
                {
                    // ReSharper disable once NullableWarningSuppressionIsUsed
                    var key = element.Attribute(xNamespace + "Key")!.Value;
                    var fileName = Path.GetFileName(file);

                    if (!removedEntriesByFile.TryGetValue(fileName, out var list))
                    {
                        list = new List<string>();
                        removedEntriesByFile[fileName] = list;
                    }

                    list.Add(key);
                    element.Remove();
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                // Rebuild the file from scratch to avoid orphaned whitespace nodes
                // that XDocument.Load(LoadOptions.PreserveWhitespace) + doc.Save() would leave.
                var remainingEntries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var remainingElements = root.Elements()
                    .Where(e => e.Attribute(xNamespace + "Key") != null)
                    .ToList();

                foreach (var element in remainingElements)
                {
                    // ReSharper disable once NullableWarningSuppressionIsUsed
                    var key = element.Attribute(xNamespace + "Key")!.Value;
                    remainingEntries[key] = element.Value;
                }

                var lines = new List<string>
                {
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                    "",
                    "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\">"
                };

                foreach (var kvp in remainingEntries)
                {
                    var escapedValue = EscapeXml(kvp.Value);
                    lines.Add($"    <system:String x:Key=\"{kvp.Key}\">{escapedValue}</system:String>");
                }

                lines.Add("</ResourceDictionary>");

                var encoding = new UTF8Encoding(false);
                File.WriteAllLines(file, lines, encoding);
            }
        }

        if (removedEntriesByFile.Count == 0)
            return; // pass

        var message = new StringBuilder();
        message.AppendLine("Empty resource values detected and automatically removed:");
        message.AppendLine();

        foreach (var kvp in removedEntriesByFile.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            message.AppendLine(CultureInfo.InvariantCulture, $"  File: {kvp.Key}");
            foreach (var key in kvp.Value.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
                message.AppendLine(CultureInfo.InvariantCulture, $"    - {key}");
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