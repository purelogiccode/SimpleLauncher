using Avalonia.Controls;
using Avalonia.Input;
using DosBoxFileSelectionViewModel = SimpleLauncher.Avalonia.ViewModels.DosBoxFileSelectionViewModel;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for selecting a DOSBox file from a list of available files.
/// </summary>
internal partial class DosBoxFileSelectionWindow : Window
{
    private readonly DosBoxFileSelectionViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DosBoxFileSelectionWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing file selection logic.</param>
    public DosBoxFileSelectionWindow(DosBoxFileSelectionViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _viewModel.DialogResultRequested += (_, _) => Close();

        DataContext = _viewModel;
    }

    /// <summary>
    ///     Gets the file path selected by the user.
    /// </summary>
    public string SelectedFilePath => _viewModel.SelectedFilePath;

    /// <summary>
    ///     Initializes the window with the specified file paths and base directory.
    /// </summary>
    /// <param name="filePaths">The list of file paths to display.</param>
    /// <param name="baseDirectory">The base directory for resolving relative paths.</param>
    public void Initialize(IList<string> filePaths, string baseDirectory)
    {
        _viewModel.Initialize(filePaths, baseDirectory);
    }

    private void FileListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        _viewModel.OnItemDoubleClicked();
    }
}