using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the DosBoxFileSelectionWindow.
/// </summary>
public partial class DosBoxFileSelectionViewModel : ObservableObject
{

    /// <summary>
    ///     Gets the collection of file items.
    /// </summary>
    public ObservableCollection<DosBoxFileItem> FileItems { get; private set; } = [];

    /// <summary>
    ///     Gets or sets the selected file item.
    /// </summary>
    public DosBoxFileItem SelectedItem
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) IsLaunchEnabled = value != null;
        }
    } = null!;

    /// <summary>
    ///     Gets or sets whether the launch button is enabled.
    /// </summary>
    public bool IsLaunchEnabled
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    ///     Gets the selected file path after dialog closes.
    /// </summary>
    public string SelectedFilePath { get; private set; } = "";

    /// <summary>Initializes the file items collection from the given file paths.</summary>
    /// <param name="filePaths">The list of file paths to display.</param>
    /// <param name="baseDirectory">The base directory used to compute relative paths.</param>
    public void Initialize(IList<string> filePaths, string baseDirectory)
    {
        var fileItems = filePaths.Select(path => new DosBoxFileItem
        {
            FullPath = path,
            DisplayName = Path.GetFileName(path),
            RelativePath = GetRelativePath(path, baseDirectory)
        }).ToList();

        FileItems = new ObservableCollection<DosBoxFileItem>(fileItems);
        OnPropertyChanged(nameof(FileItems));
    }

    /// <summary>
    ///     Event raised when the window should be closed with a dialog result.
    /// </summary>
    public event EventHandler<EventArgs<bool?>> DialogResultRequested = null!;

    [RelayCommand]
    private void Launch()
    {
        if (SelectedItem == null) return;

        SelectedFilePath = SelectedItem.FullPath;
        DialogResultRequested?.Invoke(this, new EventArgs<bool?>(true));
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultRequested?.Invoke(this, new EventArgs<bool?>(false));
    }

    /// <summary>
    ///     Handles double-click on an item.
    /// </summary>
    public void OnItemDoubleClicked()
    {
        if (SelectedItem == null) return;

        SelectedFilePath = SelectedItem.FullPath;
        DialogResultRequested?.Invoke(this, new EventArgs<bool?>(true));
    }

    private static string GetRelativePath(string fullPath, string baseDirectory)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(dir) || dir.Equals(baseDirectory, StringComparison.OrdinalIgnoreCase))
            return "";

        var relative = dir.Length > baseDirectory.Length
            ? dir[(baseDirectory.Length + 1)..]
            : dir;

        return relative;
    }
}