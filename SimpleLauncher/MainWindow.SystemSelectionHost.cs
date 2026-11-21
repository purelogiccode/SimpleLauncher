using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Threading;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Interfaces;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;

namespace SimpleLauncher;

/// <summary>
///     Partial MainWindow implementing <see cref="ISystemSelectionHost" /> for system selection screen operations.
/// </summary>
public partial class MainWindow : ISystemSelectionHost
{
    Dispatcher ISystemSelectionHost.Dispatcher => Dispatcher;

    WrapPanel ISystemSelectionHost.GameFileGrid => GameFileGrid;
    Border ISystemSelectionHost.TopSystemSelection => TopSystemSelection;
    Grid ISystemSelectionHost.StatusBarArea => StatusBarArea;
    Grid ISystemSelectionHost.ListViewPreviewArea => ListViewPreviewArea;
    Image ISystemSelectionHost.PreviewImage => PreviewImage;
    Label ISystemSelectionHost.TotalFilesLabel => TotalFilesLabel;
    Button ISystemSelectionHost.PrevPageButton2 => PrevPageButton2!;
    Button ISystemSelectionHost.NextPageButton2 => NextPageButton2!;
    TextBox ISystemSelectionHost.SearchTextBox => SearchTextBox;
    ComboBox ISystemSelectionHost.SystemComboBox => SystemComboBox;
    ComboBox ISystemSelectionHost.EmulatorComboBox => EmulatorComboBox;
    Button ISystemSelectionHost.SortOrderToggleButton => SortOrderToggleButton;
    ObservableCollection<GameListViewItem> ISystemSelectionHost.GameListItems => GameListItems;

    string? ISystemSelectionHost.SelectedSystem
    {
        get => SelectedSystem;
        set => SelectedSystem = value;
    }

    string ISystemSelectionHost.PlayTime
    {
        get => PlayTime;
        set => PlayTime = value;
    }

    bool ISystemSelectionHost.IsPlayTimeVisible
    {
        get => IsPlayTimeVisible;
        set => IsPlayTimeVisible = value;
    }

    void ISystemSelectionHost.SetLoadingState(bool isLoading, string? message)
    {
        SetLoadingState(isLoading, message);
    }

    void ISystemSelectionHost.CancelAndRecreateToken()
    {
        CancelAndRecreateToken();
    }

    CancellationToken ISystemSelectionHost.CurrentCancellationToken => _cancellationSource.Token;

    Task ISystemSelectionHost.ResetUiAsync()
    {
        return ResetUiAsync();
    }

    void ISystemSelectionHost.ResetPaginationButtons()
    {
        ResetPaginationButtons();
    }

    Task ISystemSelectionHost.LoadAllGamesAsync(CancellationToken cancellationToken)
    {
        // The "All" letter-button path: reuses the cache warmed by the selection
        // pipeline and replaces the summary with the rendered game buttons.
        return LoadGameFilesAsync(null, null, cancellationToken);
    }

    void ISystemSelectionHost.UpdateSortOrderButtonUi()
    {
        UpdateSortOrderButtonUi();
    }

    void ISystemSelectionHost.ClearGameButtonImages(Panel panel)
    {
        ClearGameButtonImages(panel);
    }

    IList<SystemManager> ISystemSelectionHost.GetSystemManagers()
    {
        return _systemManagers?.ToList() ?? [];
    }

    void ISystemSelectionHost.SetSystemManagers(IList<SystemManager> managers)
    {
        _systemManagers = managers.ToList();

        // Migrate old play-history records to full paths once the real system
        // managers are available. Checking the count guards against migrating
        // against an empty list (e.g., on a fresh install with no systems).
        if (!_playHistoryMigrated && _systemManagers.Count > 0)
        {
            _playHistoryMigrated = true;
            _lifecycle.MigratePlayHistory(_systemManagers);
        }
    }

}