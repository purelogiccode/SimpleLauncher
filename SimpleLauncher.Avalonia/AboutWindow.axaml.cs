using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window displaying application information, version, and credits.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AboutWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing about-window logic.</param>
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();

        viewModel.CloseRequested += (_, _) => Close();
        viewModel.OpenUpdateHistoryRequested += async (_, _) =>
        {
            try
            {
                var updateHistoryWindow = App.ServiceProvider.GetRequiredService<UpdateHistoryWindow>();
                await updateHistoryWindow.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open update history window");
            }
        };
        viewModel.GetOwnerWindow = () => this;

        DataContext = viewModel;
    }
}