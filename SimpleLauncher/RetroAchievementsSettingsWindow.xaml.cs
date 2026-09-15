using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using SimpleLauncher.ViewModels;

namespace SimpleLauncher;

/// <summary>
///     Window for configuring RetroAchievements credentials and settings.
/// </summary>
public partial class RetroAchievementsSettingsWindow
{
    private readonly EventHandler _saveCompletedHandler;
    private readonly RetroAchievementsSettingsViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsSettingsWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing settings logic.</param>
    public RetroAchievementsSettingsWindow(RetroAchievementsSettingsViewModel viewModel)
    {
        InitializeComponent();
        App.ApplyThemeToWindow(this);
        Owner = Application.Current.MainWindow;

        _viewModel = viewModel;

        _saveCompletedHandler = (_, _) =>
        {
            if (IsLoaded) DialogResult = true;

            Close();
        };

        _viewModel.SaveCompleted += _saveCompletedHandler;
        _viewModel.RequestExePath += OnRequestExePath;
        _viewModel.OpenInBrowser = OpenControlPanel;

        Closing += (_, _) =>
        {
            _viewModel.SaveCompleted -= _saveCompletedHandler;
            _viewModel.RequestExePath -= OnRequestExePath;
        };

        ApiKeyPasswordBox.PasswordChanged += (_, _) => _viewModel.ApiKey = ApiKeyPasswordBox.Password;
        RaPasswordPasswordBox.PasswordChanged += (_, _) => _viewModel.Password = RaPasswordPasswordBox.Password;

        Loaded += (_, _) =>
        {
            ApiKeyPasswordBox.Password = _viewModel.ApiKey;
            RaPasswordPasswordBox.Password = _viewModel.Password;
        };

        DataContext = _viewModel;
    }

    private static string? OnRequestExePath()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Emulator Executable",
            Filter = "Executable files (*.exe)|*.exe"
        };

        return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
    }

    private static void OpenControlPanel(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Error in method OpenControlPanel");
        }
    }
}