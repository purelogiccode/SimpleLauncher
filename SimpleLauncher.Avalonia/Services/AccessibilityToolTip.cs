using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Keeps a control's tooltip in sync with its accessibility metadata. The global theme
///     enables this for every control; unlike a one-shot binding, it also updates when a
///     bound accessible name (for example the favorite-status heart) changes at runtime.
/// </summary>
internal static class AccessibilityToolTip
{
    /// <summary>Enables accessibility-driven tooltips for the control.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(AccessibilityToolTip));

    private static readonly AttachedProperty<bool> IsManagedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsManaged", typeof(AccessibilityToolTip));

    static AccessibilityToolTip()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(static (control, args) =>
        {
            if (!args.GetNewValue<bool>())
            {
                control.PropertyChanged -= OnControlPropertyChanged;
                return;
            }

            control.PropertyChanged += OnControlPropertyChanged;
            Update(control);
        });
    }

    /// <summary>Enables the behavior from XAML.</summary>
    /// <param name="control">The control to annotate.</param>
    /// <param name="value">True to enable.</param>
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    /// <summary>Gets whether the behavior is enabled.</summary>
    /// <param name="control">The control to inspect.</param>
    /// <returns>True when enabled.</returns>
    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    private static void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Control control) return;

        if (e.Property.Equals(AutomationProperties.NameProperty) ||
            e.Property.Equals(AutomationProperties.HelpTextProperty) ||
            e.Property.Equals(ContentControl.ContentProperty) ||
            e.Property.Equals(HeaderedSelectingItemsControl.HeaderProperty))
        {
            Update(control);
        }
    }

    private static void Update(Control control)
    {
        // Respect an explicitly configured tooltip (including a binding that currently
        // evaluates to null). A tooltip this behavior already owns may be refreshed.
        if (!control.GetValue(IsManagedProperty) && control.IsSet(ToolTip.TipProperty)) return;

        var text = Converters.AccessibilityToolTipConverter.GetText(control);
        control.SetValue(IsManagedProperty, true);
        ToolTip.SetTip(control, string.IsNullOrWhiteSpace(text) ? null : text);
    }
}
