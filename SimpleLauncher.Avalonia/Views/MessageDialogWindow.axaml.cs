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
internal partial class MessageDialogWindow : Window
{
    private MessageBoxResult _result = MessageBoxResult.Cancel;

    public MessageDialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     Shows a modal message dialog owned by the specified window and returns the clicked button.
    /// </summary>
    /// <remarks>
    ///     Avalonia refuses to show a modal dialog whose owner is not visible (e.g. the main window was
    ///     closed or minimized to tray while a launch-failure prompt was pending). In that case the dialog
    ///     is shown as an independent top-level window instead of throwing
    ///     "Cannot show window with non-visible owner" (bug 70148).
    /// </remarks>
    public static async Task<MessageBoxResult> ShowAsync(
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

        if (!owner.IsVisible)
        {
            var completion = new TaskCompletionSource<MessageBoxResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            dialog.Closed += (_, _) => completion.TrySetResult(dialog._result);
            dialog.Show();
            return await completion.Task;
        }

        return await dialog.ShowDialog<MessageBoxResult>(owner);
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
            button.Click += (_, _) => Complete(result);
            ButtonsPanel.Children.Add(button);
        }
    }

    private void Complete(MessageBoxResult result)
    {
        _result = result;
        Close(result);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Complete(MessageBoxResult.Cancel);
        }
    }
}