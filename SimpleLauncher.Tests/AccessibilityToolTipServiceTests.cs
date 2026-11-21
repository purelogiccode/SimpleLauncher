using System.Windows.Automation;
using System.Windows.Controls;
using SimpleLauncher.Services;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Verifies the accessibility-driven tooltip layer that supplies help text for every
///     control exposing an accessible name.
/// </summary>
public class AccessibilityToolTipServiceTests
{
    /// <summary>The accessible name becomes the tooltip.</summary>
    [Fact]
    public void AccessibleNameBecomesToolTip()
    {
        StaApartment.Run(() =>
        {
            var button = new Button();
            AutomationProperties.SetName(button, "Launch the selected game");

            AccessibilityToolTipService.ApplyToolTip(button);

            Assert.Equal("Launch the selected game", button.ToolTip);
        });
    }

    /// <summary>Help text is used when no accessible name is set.</summary>
    [Fact]
    public void HelpTextIsUsedWhenNameIsMissing()
    {
        StaApartment.Run(() =>
        {
            var button = new Button();
            AutomationProperties.SetHelpText(button, "Opens the support page");

            AccessibilityToolTipService.ApplyToolTip(button);

            Assert.Equal("Opens the support page", button.ToolTip);
        });
    }

    /// <summary>Literal button content is the final fallback.</summary>
    [Fact]
    public void LiteralContentIsUsedWhenNoAccessibilityTextExists()
    {
        StaApartment.Run(() =>
        {
            var button = new Button { Content = "Cancel" };

            AccessibilityToolTipService.ApplyToolTip(button);

            Assert.Equal("Cancel", button.ToolTip);
        });
    }

    /// <summary>Menu item headers are used when no accessibility text exists.</summary>
    [Fact]
    public void MenuItemHeaderIsUsed()
    {
        StaApartment.Run(() =>
        {
            var menuItem = new MenuItem { Header = "File" };

            AccessibilityToolTipService.ApplyToolTip(menuItem);

            Assert.Equal("File", menuItem.ToolTip);
        });
    }

    /// <summary>An explicitly configured tooltip is never overwritten.</summary>
    [Fact]
    public void ExistingToolTipIsPreserved()
    {
        StaApartment.Run(() =>
        {
            var button = new Button { ToolTip = "Custom help" };
            AutomationProperties.SetName(button, "Accessible name");

            AccessibilityToolTipService.ApplyToolTip(button);

            Assert.Equal("Custom help", button.ToolTip);
        });
    }

    /// <summary>Controls without any text keep no tooltip.</summary>
    [Fact]
    public void ControlWithoutTextGetsNoToolTip()
    {
        StaApartment.Run(() =>
        {
            var label = new Label();

            AccessibilityToolTipService.ApplyToolTip(label);

            Assert.Null(label.ToolTip);
        });
    }
}
