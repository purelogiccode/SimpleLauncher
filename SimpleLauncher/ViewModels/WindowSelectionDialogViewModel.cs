using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the WindowSelectionDialogWindow.
/// </summary>
public class WindowSelectionDialogViewModel : ObservableObject
{

    /// <summary>
    ///     Gets the collection of window items.
    /// </summary>
    public ObservableCollection<WindowItem> WindowItems { get; private set; } = [];

    /// <summary>
    ///     Gets or sets the selected window item.
    /// </summary>
    public WindowItem? SelectedItem
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && value != null)
            {
                SelectedWindowHandle = value.Handle;
                DialogResultRequested?.Invoke(this, new EventArgs<bool?>(true));
            }
        }
    }

    /// <summary>
    ///     Gets the selected window handle after dialog closes.
    /// </summary>
    public IntPtr SelectedWindowHandle { get; private set; } = IntPtr.Zero;

    /// <summary>Initializes the window list from the provided window handles and titles.</summary>
    /// <param name="windows">A collection of tuples containing window handles and their titles.</param>
    public void Initialize(IEnumerable<(IntPtr Handle, string Title)> windows)
    {
        var windowItems = windows
            .Where(static w => !string.IsNullOrWhiteSpace(w.Title))
            .Select(static w => new WindowItem { Title = w.Title, Handle = w.Handle })
            .ToList();

        WindowItems = new ObservableCollection<WindowItem>(windowItems);
        OnPropertyChanged(nameof(WindowItems));
    }

    /// <summary>
    ///     Event raised when the window should be closed with a dialog result.
    /// </summary>
    public event EventHandler<EventArgs<bool?>>? DialogResultRequested;
}