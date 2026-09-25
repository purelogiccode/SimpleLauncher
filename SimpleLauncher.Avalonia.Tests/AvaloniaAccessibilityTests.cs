using System.Xml;
using System.Xml.Linq;
using SimpleLauncher.Avalonia.Tests.TestHelpers;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Guardrail for the accessibility pass: every interactive control in the Avalonia XAML
///     must expose an accessible name (AutomationProperties.Name), so screen readers and the
///     AT-SPI test harness can identify it instead of reading the content type name.
///     Buttons whose Content is a plain literal string are exempt: their text already becomes
///     the accessible name. Standalone TextBlocks/images are intentionally not required to have
///     names (decorative elements should stay out of the accessibility tree).
/// </summary>
public class AvaloniaAccessibilityTests
{
    private static readonly HashSet<string> InteractiveElements = new(StringComparer.Ordinal)
    {
        "ComboBox",
        "TextBox",
        "PasswordBox",
        "NumericUpDown",
        "DatePicker",
        "Slider",
        "ListBox",
        "DataGrid",
        "Expander",
        "ProgressBar"
    };

    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void EveryInteractiveControlHasAnAccessibleName()
    {
        var projectDir = AvaloniaProjectPathHelper.GetAvaloniaProjectPath();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.axaml", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file)) continue;

            XDocument document;
            try
            {
                document = XDocument.Load(file, LoadOptions.SetLineInfo);
            }
            catch (Exception exception)
            {
                violations.Add($"{Relative(projectDir, file)}: invalid XAML: {exception.Message}");
                continue;
            }

            foreach (var element in document.Descendants())
            {
                var name = element.Name.LocalName;
                var isButton = string.Equals(name, "Button", StringComparison.Ordinal);
                var isInteractive = isButton || InteractiveElements.Contains(name);
                if (!isInteractive) continue;
                if (isButton && ButtonHasLiteralText(element)) continue;
                if (HasAccessibleName(element)) continue;

                var line = element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0;
                var xName = element.Attribute(XamlNamespace + "Name")?.Value ?? "(no x:Name)";
                violations.Add(
                    $"{Relative(projectDir, file)}:{line} <{name} x:Name={xName}> " +
                    "has no AutomationProperties.Name");
            }
        }

        Assert.True(violations.Count == 0,
            "Interactive controls without an accessible name (add AutomationProperties.Name):" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool IsBuildOutput(string file)
    {
        var separator = Path.DirectorySeparatorChar;
        return file.Contains($"{separator}bin{separator}", StringComparison.Ordinal) ||
               file.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static bool HasAccessibleName(XElement element)
    {
        return element.Attributes().Any(attribute =>
            string.Equals(attribute.Name.LocalName, "AutomationProperties.Name", StringComparison.Ordinal));
    }

    private static bool ButtonHasLiteralText(XElement element)
    {
        var content = element.Attribute("Content");
        if (content != null)
            return !content.Value.StartsWith('{') && !string.IsNullOrWhiteSpace(content.Value);

        return !element.Elements().Any() &&
               element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value));
    }

    private static string Relative(string root, string file)
    {
        return Path.GetRelativePath(root, file);
    }
}
