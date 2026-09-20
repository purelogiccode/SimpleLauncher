using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.CheckPaths;

namespace SimpleLauncher.Avalonia.InjectConfigWindows;

/// <summary>
///     Window for injecting Raine emulator configuration settings.
/// </summary>
public partial class InjectRaineConfigWindow : Window
{
    private readonly IFilePickerService _filePicker;
    private readonly Func<Window> _getOwnerWindowHandler;
    private readonly Func<Task<string?>> _requestEmulatorPathHandler;
    private readonly Func<Task<string?>> _requestFilePathHandler;
    private readonly Func<Task<string?>> _requestFolderPathHandler;
    private readonly InjectRaineConfigViewModel _viewModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InjectRaineConfigWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing configuration logic.</param>
    /// <param name="filePicker">The file picker service used to locate the emulator executable, game file, or ROM folder.</param>
    public InjectRaineConfigWindow(InjectRaineConfigViewModel viewModel, IFilePickerService filePicker)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _filePicker = filePicker;
        _requestEmulatorPathHandler = OnRequestEmulatorPath;
        _requestFilePathHandler = OnRequestFilePath;
        _requestFolderPathHandler = OnRequestFolderPath;
        _getOwnerWindowHandler = () => this;

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.RequestEmulatorPath += _requestEmulatorPathHandler;
        _viewModel.RequestFilePath += _requestFilePathHandler;
        _viewModel.RequestFolderPath += _requestFolderPathHandler;
        _viewModel.GetOwnerWindow += _getOwnerWindowHandler;

        Closing += (_, _) =>
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.RequestEmulatorPath -= _requestEmulatorPathHandler;
            _viewModel.RequestFilePath -= _requestFilePathHandler;
            _viewModel.RequestFolderPath -= _requestFolderPathHandler;
            _viewModel.GetOwnerWindow -= _getOwnerWindowHandler;
        };

        DataContext = _viewModel;
    }

    /// <summary>
    ///     Gets whether the emulator should be launched after configuration.
    /// </summary>
    public bool ShouldRun => _viewModel.ShouldRun;

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    /// <summary>
    ///     Initializes the window with the specified emulator path, launcher mode, and file paths.
    /// </summary>
    /// <param name="emulatorPath">Optional path to the Raine emulator executable.</param>
    /// <param name="isLauncherMode">If true, the window operates in launcher mode.</param>
    /// <param name="gameFilePath">Optional path to the game file.</param>
    /// <param name="systemRomPath">Optional path to the system ROM.</param>
    public void Initialize(string? emulatorPath = null, bool isLauncherMode = true, string? gameFilePath = null,
        string? systemRomPath = null)
    {
        _viewModel.Initialize(emulatorPath, isLauncherMode, gameFilePath, systemRomPath);

        if (!isLauncherMode) BtnSave.IsDefault = true;
    }

    private async Task<string?> OnRequestEmulatorPath()
    {
        return await _filePicker.OpenFileAsync(
            "Select Raine Emulator Executable",
            ExecutableFileFilter.ForNamedExecutable("Raine Executable", "raine*.exe"));
    }

    private async Task<string?> OnRequestFilePath()
    {
        return await _filePicker.OpenFileAsync(
            "Select NeoGeo CD BIOS File",
            "NeoGeo CD BIOS (neocd.bin)|neocd.bin|All Files (*.*)|*.*");
    }

    private async Task<string?> OnRequestFolderPath()
    {
        return await _filePicker.OpenFolderAsync("Select Raine ROM Directory");
    }
}