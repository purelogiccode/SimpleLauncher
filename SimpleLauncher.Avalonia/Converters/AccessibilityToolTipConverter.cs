using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace SimpleLauncher.Avalonia.Converters;

/// <summary>
///     Supplies a tooltip for every control from its accessibility metadata: the accessible
///     name first, then help text, then literal content. Returning null leaves the control
///     without a tooltip (decorative elements and controls with an explicit tooltip).
/// </summary>
internal sealed class AccessibilityToolTipConverter : IValueConverter
{
    /// <summary>
    ///     Resolves the tooltip text for the control passed as the binding value.
    /// </summary>
    /// <param name="value">The control being styled.</param>
    /// <param name="targetType">The target property type (unused).</param>
    /// <param name="parameter">Optional converter parameter (unused).</param>
    /// <param name="culture">The culture used for conversion (unused).</param>
    /// <returns>The tooltip text, or null when the control has no descriptive text.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var control = value as Control;
        if (control is null) return null;

        var name = control.GetValue(AutomationProperties.NameProperty);
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

        var help = control.GetValue(AutomationProperties.HelpTextProperty);
        if (!string.IsNullOrWhiteSpace(help)) return help.Trim();

        var content = (control as ContentControl)?.Content as string;
        if (!string.IsNullOrWhiteSpace(content)) return content.Trim();

        var header = control switch
        {
            MenuItem { Header: string menuHeader } => menuHeader,
            _ => null
        };

        return string.IsNullOrWhiteSpace(header) ? null : header.Trim();
    }

    /// <summary>Not supported: tooltips are one-way.</summary>
    /// <param name="value">The value to convert back.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
