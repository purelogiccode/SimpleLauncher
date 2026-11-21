using Avalonia.Controls;
using WindowSelectionDialogViewModel = SimpleLauncher.Avalonia.ViewModels.WindowSelectionDialogViewModel;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Dialog window for selecting a running application window by handle.
/// </summary>
internal partial class WindowSelectionDialogWindow : Window
{
    private readonly WindowSelectionDialogViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WindowSelectionDialogWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing window selection logic.</param>
    public WindowSelectionDialogWindow(WindowSelectionDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _viewModel.DialogResultRequested += (_, _) => Close();

        DataContext = _viewModel;
    }

    /// <summary>
    ///     Gets the handle of the window selected by the user.
    /// </summary>
    public IntPtr SelectedWindowHandle => _viewModel.SelectedWindowHandle;

    /// <summary>
    ///     Initializes the window with a list of available application windows.
    /// </summary>
    /// <param name="windows">Collection of window handles and titles to display.</param>
    public void Initialize(IEnumerable<(IntPtr Handle, string Title)> windows)
    {
        _viewModel.Initialize(windows);
    }
}