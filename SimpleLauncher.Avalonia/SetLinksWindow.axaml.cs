using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for configuring external application links and paths.
/// </summary>
internal partial class SetLinksWindow : Window
{
    private readonly EventHandler _saveCompletedHandler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SetLinksWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing link configuration logic.</param>
    public SetLinksWindow(SetLinksViewModel viewModel)
    {
        InitializeComponent();

        _saveCompletedHandler = (_, _) => Close();

        viewModel.SaveCompleted += _saveCompletedHandler;
        viewModel.CloseRequested += OnCloseRequested;

        Closing += (_, _) =>
        {
            viewModel.SaveCompleted -= _saveCompletedHandler;
            viewModel.CloseRequested -= OnCloseRequested;
        };

        DataContext = viewModel;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
}