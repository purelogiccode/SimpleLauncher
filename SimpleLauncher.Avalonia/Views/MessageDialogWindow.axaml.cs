using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Views;

/// <summary>
///     A lightweight message-box-style dialog used by <see cref="Services.AvaloniaServices.MessageBoxLibraryService" />.
/// </summary>
public partial class MessageDialogWindow : Window
{
    public MessageDialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Shows a modal message dialog owned by the specified window and returns the clicked button.
    /// </summary>
    public static Task<MessageBoxResult> ShowAsync(
        Window owner, string message, string caption,
        MessageButtons buttons, MessageIcon icon)
    {
        var dialog = new MessageDialogWindow
        {
            Title = caption
        };

        dialog.MessageText.Text = message;
        dialog.SetIcon(icon);
        dialog.BuildButtons(buttons);

        return dialog.ShowDialog<MessageBoxResult>(owner);
    }

    private void SetIcon(MessageIcon icon)
    {
        var (glyph, brush) = icon switch
        {
            MessageIcon.Information => ("ℹ️", "AccentBrush"),
            MessageIcon.Warning => ("⚠️", "NotificationWarningBrush"),
            MessageIcon.Error => ("❌", "NotificationErrorBrush"),
            MessageIcon.Question => ("❓", "AccentBrush"),
            _ => (string.Empty, null)
        };

        if (string.IsNullOrEmpty(glyph) || brush is null) return;

        IconText.Text = glyph;
        if (this.TryFindResource(brush, out var brushResource) &&
            brushResource is IBrush foundBrush)
        {
            IconText.Foreground = foundBrush;
        }

        IconText.IsVisible = true;
    }

    private void BuildButtons(MessageButtons buttons)
    {
        var localization = App.ServiceProvider?.GetService<LocalizationService>();
        var okText = localization?.GetString("OkButtonText", "OK") ?? "OK";
        var yesText = localization?.GetString("YesButtonText", "Yes") ?? "Yes";
        var noText = localization?.GetString("NoButtonText", "No") ?? "No";
        var cancelText = localization?.GetString("CancelButtonText", "Cancel") ?? "Cancel";

        var definitions = buttons switch
        {
            MessageButtons.Ok => new[] { (Result: MessageBoxResult.Ok, Text: okText, IsDefault: true, Primary: true) },
            MessageButtons.OkCancel => new[]
            {
                (Result: MessageBoxResult.Ok, Text: okText, IsDefault: true, Primary: true),
                (Result: MessageBoxResult.Cancel, Text: cancelText, IsDefault: false, Primary: false)
            },
            MessageButtons.YesNo => new[]
            {
                (Result: MessageBoxResult.Yes, Text: yesText, IsDefault: true, Primary: true),
                (Result: MessageBoxResult.No, Text: noText, IsDefault: false, Primary: false)
            },
            _ => new[]
            {
                (Result: MessageBoxResult.Yes, Text: yesText, IsDefault: true, Primary: true),
                (Result: MessageBoxResult.No, Text: noText, IsDefault: false, Primary: false),
                (Result: MessageBoxResult.Cancel, Text: cancelText, IsDefault: false, Primary: false)
            }
        };

        foreach (var definition in definitions)
        {
            var button = new Button
            {
                Content = definition.Text,
                IsDefault = definition.IsDefault,
                MinWidth = 86,
                Padding = new Thickness(14, 6)
            };

            if (definition.Primary) button.Classes.Add("primary");

            var result = definition.Result;
            button.Click += (_, _) => Close(result);
            ButtonsPanel.Children.Add(button);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(MessageBoxResult.Cancel);
        }
    }
}