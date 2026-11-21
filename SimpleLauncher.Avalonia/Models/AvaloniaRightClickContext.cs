using Avalonia.Controls;
using SimpleLauncher.Avalonia.Services.Favorites;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Models;

/// <summary>
///     Contextual information passed to right-click menu handlers for game items.
///     Aggregates game data and services needed by the context menu (WPF RightClickContext port).
/// </summary>
internal class AvaloniaRightClickContext(
    string filePath,
    string fileNameWithExtension,
    string fileNameWithoutExtension,
    string selectedSystemName,
    SystemManagerService selectedSystemManager,
    SettingsManagerService settings,
    FavoritesManager favoritesManager,
    Window ownerWindow,
    MainViewModel mainViewModel,
    GameCardViewModel? sourceCard = null,
    Action? onFavoriteRemoved = null)
{
    /// <summary>Gets the full file path of the game ROM (may not exist on disk for stale entries).</summary>
    public string FilePath { get; } = filePath ?? "";

    /// <summary>Gets the file name with extension.</summary>
    public string FileNameWithExtension { get; } = fileNameWithExtension ?? "";

    /// <summary>Gets the file name without extension.</summary>
    public string FileNameWithoutExtension { get; } = fileNameWithoutExtension ?? "";

    /// <summary>Gets the name of the selected system.</summary>
    public string SelectedSystemName { get; } = selectedSystemName ?? "";

    /// <summary>Gets the system manager instance for the selected system.</summary>
    public SystemManagerService SelectedSystemManager { get; } =
        selectedSystemManager ?? throw new ArgumentNullException(nameof(selectedSystemManager));

    /// <summary>Gets the application settings manager.</summary>
    public SettingsManagerService Settings { get; } = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Gets the favorites manager instance.</summary>
    public FavoritesManager FavoritesManager { get; } =
        favoritesManager ?? throw new ArgumentNullException(nameof(favoritesManager));

    /// <summary>Gets the window that owns dialogs opened from the menu.</summary>
    public Window OwnerWindow { get; } = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));

    /// <summary>Gets the main view model (status text, launching, refreshing).</summary>
    public MainViewModel MainViewModel { get; } =
        mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

    /// <summary>Gets the game card the menu was opened from, when available (for live favorite-star updates).</summary>
    public GameCardViewModel? SourceCard { get; } = sourceCard;

    /// <summary>Gets the callback invoked after a favorite is removed (e.g. refresh the Favorites section).</summary>
    public Action? OnFavoriteRemoved { get; } = onFavoriteRemoved;
}