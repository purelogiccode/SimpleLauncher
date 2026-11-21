using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using SimpleLauncher.Avalonia.Converters;
using SimpleLauncher.Avalonia.Tests.TestHelpers;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Verifies the Avalonia tooltip layer that mirrors accessible names into tooltips for
///     every control.
/// </summary>
public class AccessibilityToolTipConverterTests
{
    private readonly AccessibilityToolTipConverter _converter = new();

    /// <summary>The accessible name wins over all other text.</summary>
    [Fact]
    public void AccessibleNameIsPreferred()
    {
        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var button = new Button { Content = "Content" };
            AutomationProperties.SetName(button, "Accessible name");
            AutomationProperties.SetHelpText(button, "Help text");

            Assert.Equal("Accessible name", Convert(button));
        });
    }

    /// <summary>Help text is used when the accessible name is blank.</summary>
    [Fact]
    public void HelpTextIsUsedWhenNameIsMissing()
    {
        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var button = new Button { Content = "Content" };
            AutomationProperties.SetHelpText(button, "Help text");

            Assert.Equal("Help text", Convert(button));
        });
    }

    /// <summary>Literal content is the final fallback.</summary>
    [Fact]
    public void LiteralContentIsUsedWhenAccessibilityTextIsMissing()
    {
        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var button = new Button { Content = "Cancel" };

            Assert.Equal("Cancel", Convert(button));
        });
    }

    /// <summary>Menu item headers are used when no accessibility text exists.</summary>
    [Fact]
    public void MenuItemHeaderIsUsed()
    {
        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var menuItem = new MenuItem { Header = "File" };

            Assert.Equal("File", Convert(menuItem));
        });
    }

    /// <summary>Controls without descriptive text produce no tooltip.</summary>
    [Fact]
    public void ControlWithoutTextReturnsNull()
    {
        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var panel = new StackPanel();

            Assert.Null(Convert(panel));
        });
    }

    /// <summary>
    ///     The theme must keep applying the converter globally; removing the style would
    ///     silently drop tooltips from every window.
    /// </summary>
    [Fact]
    public void ThemeAppliesTheGlobalToolTipStyle()
    {
        var theme = File.ReadAllText(Path.Combine(
            AvaloniaProjectPathHelper.GetAvaloniaProjectPath(), "Themes", "DarkTheme.axaml"));

        Assert.Contains("AccessibilityToolTip", theme, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip", theme, StringComparison.Ordinal);
        Assert.Contains("Selector=\":is(Control)\"", theme, StringComparison.Ordinal);
    }

    private string? Convert(Control control)
    {
        return _converter.Convert(control, typeof(string), null, CultureInfo.InvariantCulture) as string;
    }
}
