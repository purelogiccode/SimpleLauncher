using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace SimpleLauncher.Services;

/// <summary>
///     Supplies a tooltip for every control that exposes an accessible name.
///     The app already requires <c>AutomationProperties.Name</c> on interactive
///     controls for screen readers; reusing that text keeps tooltips and accessibility
///     descriptions consistent without duplicating strings in every window's XAML.
/// </summary>
public static class AccessibilityToolTipService
{
    private static bool _initialized;

    /// <summary>
    ///     Registers a class handler that assigns tooltips as controls are loaded.
    ///     Safe to call more than once; initialization happens only once.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        EventManager.RegisterClassHandler(typeof(Control), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnControlLoaded));
    }

    private static void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Control control) ApplyToolTip(control);
    }

    /// <summary>
    ///     Assigns the control's tooltip from its accessible name (falling back to help text,
    ///     then literal content) when it does not already have one. Internal for unit tests.
    /// </summary>
    /// <param name="control">The control to annotate.</param>
    internal static void ApplyToolTip(Control control)
    {
        // Respect explicit XAML/binding values, including a binding that currently evaluates
        // to null; ReadLocalValue distinguishes "unset" from "set to null".
        if (control.ReadLocalValue(FrameworkElement.ToolTipProperty) != DependencyProperty.UnsetValue) return;

        var tooltip = control.GetValue(AutomationProperties.NameProperty) as string;
        if (string.IsNullOrWhiteSpace(tooltip))
            tooltip = control.GetValue(AutomationProperties.HelpTextProperty) as string;

        if (string.IsNullOrWhiteSpace(tooltip) && control is ContentControl { Content: string content })
            tooltip = content;

        if (string.IsNullOrWhiteSpace(tooltip) && control is HeaderedItemsControl { Header: string header })
            tooltip = header;

        if (string.IsNullOrWhiteSpace(tooltip) && control is HeaderedContentControl { Header: string headeredContent })
            tooltip = headeredContent;

        if (!string.IsNullOrWhiteSpace(tooltip)) control.ToolTip = tooltip.Trim();
    }
}
