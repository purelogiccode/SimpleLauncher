using System.Windows.Controls;
using System.Windows.Threading;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Interfaces;
using SystemManager = SimpleLauncher.Services.SystemManager.SystemManagerService;

namespace SimpleLauncher;

/// <summary>
///     Partial MainWindow implementing <see cref="IGameFileLoadingHost" /> for game file loading operations.
/// </summary>
public partial class MainWindow : IGameFileLoadingHost
{
    Dispatcher IGameFileLoadingHost.Dispatcher => Dispatcher;

    ComboBox IGameFileLoadingHost.SystemComboBox => SystemComboBox;
    WrapPanel IGameFileLoadingHost.GameFileGrid => GameFileGrid;
    ScrollViewer IGameFileLoadingHost.Scroller => Scroller;
    DataGrid IGameFileLoadingHost.GameDataGrid => GameDataGrid;
    Grid IGameFileLoadingHost.ListViewPreviewArea => ListViewPreviewArea;
    Image IGameFileLoadingHost.PreviewImage => PreviewImage;

    string IGameFileLoadingHost.ViewMode => _settings.ViewMode;

    // ReSharper disable once ConvertToAutoProperty
    bool IGameFileLoadingHost.IsResortOperation => _isResortOperation;

    IList<SystemManager> IGameFileLoadingHost.GetSystemManagers()
    {
        return _systemManagers?.ToList() ?? [];
    }

    Task IGameFileLoadingHost.DisplaySystemSelectionScreenAsync(CancellationToken cancellationToken)
    {
        return _gameBrowser.DisplaySystemSelectionScreenAsync(cancellationToken);
    }

    void IGameFileLoadingHost.SetLoadingState(bool isLoading, string? message)
    {
        SetLoadingState(isLoading, message);
    }

    Task IGameFileLoadingHost.SetUiBeforeLoadGameFilesAsync()
    {
        return SetUiBeforeLoadGameFilesAsync();
    }

    IList<string> IGameFileLoadingHost.SetPaginationOfListOfFiles(IList<string> allFiles)
    {
        return SetPaginationOfListOfFiles(allFiles);
    }

    string? IGameFileLoadingHost.GetCurrentFilter()
    {
        return ((IUiResetHost)this).CurrentFilter;
    }

    string? IGameFileLoadingHost.GetActiveSearchQueryOrMode()
    {
        return ((IUiResetHost)this).ActiveSearchQueryOrMode;
    }

    string IGameFileLoadingHost.GetMameSortOrder()
    {
        return ((IUiResetHost)this).MameSortOrder;
    }

    (string? startLetter, string? searchQuery) IGameFileLoadingHost.GetLoadGameFilesParams()
    {
        return GetLoadGameFilesParams();
    }
}