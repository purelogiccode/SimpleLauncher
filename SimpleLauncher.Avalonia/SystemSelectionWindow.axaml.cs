using Avalonia.Controls;
using SystemSelectionViewModel = SimpleLauncher.Avalonia.ViewModels.SystemSelectionViewModel;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for selecting an emulator system from a list of available systems.
/// </summary>
internal partial class SystemSelectionWindow : Window
{
    private readonly SystemSelectionViewModel _viewModel;
    private bool _confirmed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemSelectionWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing system selection logic.</param>
    public SystemSelectionWindow(SystemSelectionViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _viewModel.DialogResultRequested += (_, e) =>
        {
            _confirmed = e.Value == true;

            // A cancelled selection must not yield the pre-selected guess
            if (!_confirmed) _viewModel.SelectedSystem = null;

            Close();
        };

        // Closing via the title-bar X / Alt+F4 must not confirm the pre-selected guess.
        Closed += (_, _) =>
        {
            if (!_confirmed) _viewModel.SelectedSystem = null;
        };

        DataContext = _viewModel;
    }

    /// <summary>
    ///     Gets the system name selected by the user.
    /// </summary>
    public string? SelectedSystem => _viewModel.SelectedSystem;

    /// <summary>
    ///     Initializes the window with a pre-selected system guess.
    /// </summary>
    /// <param name="currentGuess">The initially selected system name.</param>
    public void Initialize(string currentGuess)
    {
        _viewModel.Initialize(currentGuess);
    }
}