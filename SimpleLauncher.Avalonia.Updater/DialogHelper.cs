using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SimpleLauncher.Avalonia.Updater;

/// <summary>
///     Minimal message-dialog helper (code-built; the updater does not reference the main app's UI).
/// </summary>
internal static class DialogHelper
{
    /// <summary>
    ///     Shows a modal message box with an OK button.
    /// </summary>
    public static async Task ShowMessageAsync(Window owner, string message, string title)
    {
        await ShowDialogAsync(owner, message, title, false);
    }

    /// <summary>
    ///     Shows a modal Yes/No dialog and returns the user's choice.
    /// </summary>
    public static async Task<bool> ShowYesNoAsync(Window owner, string message, string title)
    {
        return await ShowDialogAsync(owner, message, title, true);
    }

    private static async Task<bool> ShowDialogAsync(Window owner, string message, string title, bool yesNo)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    buttons
                }
            }
        };

        if (yesNo)
        {
            buttons.Children.Add(CreateButton("Yes", true, isDefault: true));
            buttons.Children.Add(CreateButton("No", false, isCancel: true));
        }
        else
        {
            buttons.Children.Add(CreateButton("OK", false, isDefault: true));
        }

        // Closing via the title-bar X must not leave the await pending forever.
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(owner);

        return await tcs.Task;

        // Every button must both record the choice AND close the window: ShowDialog only
        // returns once the dialog is closed, so setting the result alone left the buttons
        // appearing dead (the awaited update flow never resumed).
        Button CreateButton(string content, bool result, bool isDefault = false, bool isCancel = false)
        {
            var button = new Button { Content = content, Width = 90, IsDefault = isDefault, IsCancel = isCancel };
            button.Click += (_, _) =>
            {
                tcs.TrySetResult(result);
                dialog.Close();
            };
            return button;
        }
    }
}