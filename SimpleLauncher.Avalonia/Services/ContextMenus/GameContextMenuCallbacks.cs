using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia.Services.ContextMenus;

/// <summary>
///     Avalonia-only extra actions appended after the WPF-parity menu entries.
/// </summary>
public class GameContextMenuCallbacks
{
    private readonly Action<GameCardViewModel> _onShowDetails = null!;
    private readonly Action<GameCardViewModel> _onCopyPath = null!;
    private readonly Action<GameCardViewModel> _onCopyName = null!;
    private readonly Action<GameCardViewModel> _onShowInFolder = null!;
    private readonly Action<GameCardViewModel> _onEditSystem = null!;

    public required Action<GameCardViewModel> OnShowDetails
    {
        get => _onShowDetails;
        init => _onShowDetails = WithLogging("ShowDetails", value);
    }

    public required Action<GameCardViewModel> OnCopyPath
    {
        get => _onCopyPath;
        init => _onCopyPath = WithLogging("CopyPath", value);
    }

    public required Action<GameCardViewModel> OnCopyName
    {
        get => _onCopyName;
        init => _onCopyName = WithLogging("CopyName", value);
    }

    public required Action<GameCardViewModel> OnShowInFolder
    {
        get => _onShowInFolder;
        init => _onShowInFolder = WithLogging("ShowInFolder", value);
    }

    public required Action<GameCardViewModel> OnEditSystem
    {
        get => _onEditSystem;
        init => _onEditSystem = WithLogging("EditSystem", value);
    }

    private static Action<GameCardViewModel> WithLogging(string actionName, Action<GameCardViewModel> action)
    {
        return game =>
        {
            Log.Debug("[ContextMenu] Action '{Action}' invoked for '{File}'", actionName, game.FilePath);
            action(game);
        };
    }
}