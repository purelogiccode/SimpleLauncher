using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for configuring gamepad dead zone sensitivity settings.
/// </summary>
internal partial class SetGamepadDeadZoneWindow : Window
{
    private readonly EventHandler _saveCompletedHandler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SetGamepadDeadZoneWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing dead zone configuration logic.</param>
    public SetGamepadDeadZoneWindow(SetGamepadDeadZoneViewModel viewModel)
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