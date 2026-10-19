using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia.Services.ContextMenus;

/// <summary>
///     Avalonia-only extra actions appended after the WPF-parity menu entries.
/// </summary>
public class GameContextMenuCallbacks
{
    public required Action<GameCardViewModel> OnShowDetails
    {
        get;
        init => field = WithLogging("ShowDetails", value);
    }

    public required Action<GameCardViewModel> OnCopyPath
    {
        get;
        init => field = WithLogging("CopyPath", value);
    }

    public required Action<GameCardViewModel> OnCopyName
    {
        get;
        init => field = WithLogging("CopyName", value);
    }

    public required Action<GameCardViewModel> OnShowInFolder
    {
        get;
        init => field = WithLogging("ShowInFolder", value);
    }

    public required Action<GameCardViewModel> OnEditSystem
    {
        get;
        init => field = WithLogging("EditSystem", value);
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