using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Partial MainWindow implementing <see cref="IUiResetHost" /> for UI state reset and
///     filter management (WPF MainWindow.UIResetHost.cs parity).
/// </summary>
public partial class MainWindow : IUiResetHost
{
    private CancellationTokenSource _uiResetCancellationSource = new();

    bool IUiResetHost.IsUiUpdating { get; set; }

    bool IUiResetHost.IsLoadingGames
    {
        get => _viewModel.IsLoading;
        set => _viewModel.IsLoading = value;
    }

    string? IUiResetHost.CurrentFilter
    {
        get => _viewModel.LetterFilter;
        set => _viewModel.SetLetterFilter(value ?? "");
    }

    string? IUiResetHost.ActiveSearchQueryOrMode
    {
        get => _viewModel.SearchText;
        set => _viewModel.SearchText = value ?? "";
    }

    string? IUiResetHost.SelectedSystem
    {
        get => string.IsNullOrEmpty(_viewModel.SelectedSystem) ? null : _viewModel.SelectedSystem;
        set => _viewModel.SelectedSystem = value ?? "";
    }

    string IUiResetHost.PlayTime { get; set; } = "00:00:00";

    string IUiResetHost.MameSortOrder
    {
        get => _viewModel.MameSortOrder;
        set => _viewModel.SetMameSortOrder(value);
    }

    CancellationToken IUiResetHost.CurrentCancellationToken => _uiResetCancellationSource.Token;

    void IUiResetHost.CancelAndRecreateToken()
    {
        _uiResetCancellationSource.Cancel();
        _uiResetCancellationSource.Dispose();
        _uiResetCancellationSource = new CancellationTokenSource();
    }

    void IUiResetHost.ResetPaginationButtons()
    {
        SetPrevPageButtonEnabled(false);
        SetNextPageButtonEnabled(false);
    }

    Task IUiResetHost.DisplaySystemSelectionScreenAsync(CancellationToken cancellationToken)
    {
        // WPF parity: restart / home / emergency release always returns to
        // the system selection screen (DisplaySystemSelectionScreenAsync).
        _ = ShowSystemSelectionScreenAsync();
        return Task.CompletedTask;
    }

    void IUiResetHost.SetLoadingOverlayVisible(bool isVisible)
    {
        // Force-reset the visual only: the reference-counted overlay service keeps
        // owning the operation count, so decrementing here could re-show the overlay
        // while another operation is still active.
        _viewModel.IsLoading = isVisible;
    }

    void IUiResetHost.SetSearchTextBoxText(string text)
    {
        SearchBox.Text = text;
        _viewModel.SearchText = text;
    }

    void IUiResetHost.ClearPreviewImage()
    {
        // Avalonia shell has no preview image panel — no-op.
    }

    void IUiResetHost.SetSystemComboBoxSelectedItem(object? item)
    {
        SystemComboBox.SelectedItem = item;
    }

    void IUiResetHost.SetEmulatorComboBoxSelectedItem(object? item)
    {
        EmulatorComboBox.SelectedItem = item;
    }
}