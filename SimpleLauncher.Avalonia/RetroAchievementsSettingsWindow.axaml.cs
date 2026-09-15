using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for configuring RetroAchievements credentials and settings.
/// </summary>
public partial class RetroAchievementsSettingsWindow : Window
{
    private readonly EventHandler _closeRequestedHandler;
    private readonly EventHandler _saveCompletedHandler;
    private readonly RetroAchievementsSettingsViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsSettingsWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing settings logic.</param>
    public RetroAchievementsSettingsWindow(RetroAchievementsSettingsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        _saveCompletedHandler = (_, _) => Close();

        _closeRequestedHandler = (_, _) => Close();

        _viewModel.SaveCompleted += _saveCompletedHandler;
        _viewModel.CloseRequested += _closeRequestedHandler;
        _viewModel.RequestExePath += OnRequestExePath;
        _viewModel.OpenInBrowser = OpenControlPanel;

        Closing += (_, _) =>
        {
            _viewModel.SaveCompleted -= _saveCompletedHandler;
            _viewModel.CloseRequested -= _closeRequestedHandler;
            _viewModel.RequestExePath -= OnRequestExePath;
        };

        ApiKeyPasswordBox.TextChanged += (_, _) => _viewModel.ApiKey = ApiKeyPasswordBox.Text ?? "";
        RaPasswordPasswordBox.TextChanged += (_, _) => _viewModel.Password = RaPasswordPasswordBox.Text ?? "";

        Opened += (_, _) =>
        {
            ApiKeyPasswordBox.Text = _viewModel.ApiKey;
            RaPasswordPasswordBox.Text = _viewModel.Password;
        };

        DataContext = _viewModel;
    }

    private static async Task<string?> OnRequestExePath()
    {
        var filePicker = App.ServiceProvider.GetRequiredService<IFilePickerService>();
        return await filePicker.OpenFileAsync("Select Emulator Executable", "Executable files (*.exe)|*.exe");
    }

    private void OpenControlPanel_Click(object? sender, RoutedEventArgs e)
    {
        OpenControlPanel("https://retroachievements.org/controlpanel.php");
    }

    private void OpenControlPanel(string url)
    {
        _ = OpenControlPanelAsync(url);
    }

    private async Task OpenControlPanelAsync(string url)
    {
        try
        {
            // AV-15: validate the http(s) scheme and open via the cross-platform
            // launcher instead of assuming Windows shell-execute.
            if (!await ExternalLinkHelper.TryOpenUrlAsync(url, TopLevel.GetTopLevel(this)))
                Log.Debug("Failed to open RetroAchievements control panel: invalid or unreachable URL");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to open RetroAchievements control panel");
        }
    }
}