using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Models;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Partial MainWindow implementing <see cref="ISystemSelectionHost" /> for system
///     selection coordination (WPF MainWindow.SystemSelectionHost.cs parity).
/// </summary>
public partial class MainWindow : ISystemSelectionHost
{
    void ISystemSelectionHost.SetSystemComboBoxItems(IReadOnlyList<string> systemNames)
    {
        SystemComboBox.ItemsSource = systemNames;
    }

    string? ISystemSelectionHost.GetSelectedSystem()
    {
        return SystemComboBox.SelectedItem as string;
    }

    void ISystemSelectionHost.SetEmulatorComboBoxItems(IReadOnlyList<string> emulatorNames)
    {
        EmulatorComboBox.ItemsSource = emulatorNames;
        EmulatorComboBox.SelectedIndex = emulatorNames.Count > 0 ? 0 : -1;
    }

    async Task ISystemSelectionHost.NavigateToSystemAsync(string systemName)
    {
        // WPF parity: the top system-selection bar and the status bar come back once
        // a system is loaded (SystemSelectionOrchestratorService lines 170-171).
        TopSystemSelection.IsVisible = true;
        StatusBarArea.IsVisible = true;

        // Selecting a system always returns to the game browser — otherwise games
        // would load invisibly behind an open Favorites / History / Search section.
        await ShowSectionAsync(MainSection.None);
        await _viewModel.NavigateToSystemCommand.ExecuteAsync(systemName);
    }

    void ISystemSelectionHost.ShowSystemInformation(IReadOnlyList<SystemInfoLine> lines)
    {
        // WPF parity: the configuration summary replaces the game grid until the
        // user loads games with the letter buttons (ShowGames hides it again).
        _viewModel.ShowSystemInformation(lines);
    }

    void ISystemSelectionHost.RefreshSidebar()
    {
        _systemManagerService.InvalidateCache();
        PopulateSidebarFromSystemXml();
    }

    void ISystemSelectionHost.RestartFileWatcher()
    {
        _fileWatcher.StartWatchingForSystems(_systemManagerService.LoadSystems());
    }

    string ISystemSelectionHost.PlayTime
    {
        get => _viewModel.PlayTime;
        set => _viewModel.PlayTime = value;
    }

    bool ISystemSelectionHost.IsPlayTimeVisible
    {
        get => _viewModel.IsPlayTimeVisible;
        set => _viewModel.IsPlayTimeVisible = value;
    }

    string ISystemSelectionHost.MameSortOrder
    {
        get => _viewModel.MameSortOrder;
        set => _viewModel.SetMameSortOrder(value);
    }
}