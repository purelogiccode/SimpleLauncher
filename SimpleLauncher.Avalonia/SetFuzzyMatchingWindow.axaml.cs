using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for configuring fuzzy matching settings for game file searches.
/// </summary>
internal partial class SetFuzzyMatchingWindow : Window
{
    private readonly EventHandler _cancelRequestedHandler;
    private readonly EventHandler _saveCompletedHandler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SetFuzzyMatchingWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing fuzzy matching configuration logic.</param>
    public SetFuzzyMatchingWindow(SetFuzzyMatchingViewModel viewModel)
    {
        InitializeComponent();

        _saveCompletedHandler = (_, _) => Close();
        _cancelRequestedHandler = (_, _) => Close();

        viewModel.SaveCompleted += _saveCompletedHandler;
        viewModel.CancelRequested += _cancelRequestedHandler;

        Closing += (_, _) =>
        {
            viewModel.SaveCompleted -= _saveCompletedHandler;
            viewModel.CancelRequested -= _cancelRequestedHandler;
        };

        DataContext = viewModel;
    }
}