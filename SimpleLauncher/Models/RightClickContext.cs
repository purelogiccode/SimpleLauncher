using System.Windows.Controls;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.MameManager;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Services.Favorites;
using SimpleLauncher.Services.GameLauncher;
using SimpleLauncher.Services.SystemManager;

namespace SimpleLauncher.Models;

/// <summary>
///     Contextual information passed to right-click menu handlers for game buttons.
///     Aggregates game data, UI controls, and services needed by the context menu.
/// </summary>
public class RightClickContext(
    string filePath,
    string fileNameWithExtension,
    string fileNameWithoutExtension,
    string selectedSystemName,
    SystemManagerService selectedSystemManager,
    IList<MameManagerService> machines,
    FavoritesManager favoritesManager,
    SettingsManagerService settings,
    ComboBox? emulatorComboBox,
    Favorite? favorite,
    Emulator? emulator,
    WrapPanel? gameFileGrid,
    Button? button,
    MainWindow mainWindow,
    GamePadController gamePadController,
    Action? onFavoriteRemoved = null,
    GameLauncherService? gameLauncher = null,
    PlaySoundEffects? playSoundEffects = null,
    ILoadingState? loadingStateProvider = null)
{
    /// <summary>Gets the full file path of the game ROM.</summary>
    public string FilePath { get; } = filePath ?? throw new ArgumentNullException(nameof(filePath));

    /// <summary>Gets the file name with extension.</summary>
    public string FileNameWithExtension { get; } =
        fileNameWithExtension ?? throw new ArgumentNullException(nameof(fileNameWithExtension));

    /// <summary>Gets the file name without extension.</summary>
    public string FileNameWithoutExtension { get; } = fileNameWithoutExtension ??
                                                      throw new ArgumentNullException(nameof(fileNameWithoutExtension));

    /// <summary>Gets the name of the selected system.</summary>
    public string SelectedSystemName { get; } =
        selectedSystemName ?? throw new ArgumentNullException(nameof(selectedSystemName));

    /// <summary>Gets the selected system manager instance.</summary>
    public SystemManagerService SelectedSystemManager { get; } =
        selectedSystemManager ?? throw new ArgumentNullException(nameof(selectedSystemManager));

    /// <summary>Gets the list of MAME machine entries.</summary>
    public IList<MameManagerService> Machines { get; } = machines ?? throw new ArgumentNullException(nameof(machines));

    /// <summary>Gets the favorites manager instance.</summary>
    public FavoritesManager FavoritesManager { get; } =
        favoritesManager ?? throw new ArgumentNullException(nameof(favoritesManager));

    /// <summary>Gets the application settings manager.</summary>
    public SettingsManagerService Settings { get; } = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Gets the emulator combo box control.</summary>
    public ComboBox? EmulatorComboBox { get; } = emulatorComboBox;

    /// <summary>Gets the selected favorite entry, if any.</summary>
    public Favorite? Favorite { get; } = favorite;

    /// <summary>Gets the emulator configuration instance.</summary>
    public Emulator? Emulator { get; } = emulator;

    /// <summary>Gets the game file grid panel.</summary>
    public WrapPanel? GameFileGrid { get; } = gameFileGrid;

    /// <summary>Gets or sets the button that was right-clicked.</summary>
    public Button? Button { get; set; } = button;

    /// <summary>Gets the main application window.</summary>
    public MainWindow MainWindow { get; } = mainWindow;

    /// <summary>Gets the game pad controller instance.</summary>
    public GamePadController GamePadController { get; } = gamePadController;

    /// <summary>Gets the callback invoked when a favorite is removed.</summary>
    public Action? OnFavoriteRemoved { get; } = onFavoriteRemoved;

    /// <summary>Gets the game launcher service, if available.</summary>
    public GameLauncherService? GameLauncherService { get; } = gameLauncher;

    /// <summary>Gets the sound effects service, if available.</summary>
    public PlaySoundEffects? PlaySoundEffects { get; } = playSoundEffects;

    /// <summary>Gets the loading state provider for overlay display.</summary>
    public ILoadingState LoadingStateProvider { get; } = loadingStateProvider
                                                         ?? mainWindow as ILoadingState ?? throw new ArgumentException(
                                                             $"{mainWindow.GetType().Name} does not implement {nameof(ILoadingState)}.",
                                                             nameof(mainWindow));
}