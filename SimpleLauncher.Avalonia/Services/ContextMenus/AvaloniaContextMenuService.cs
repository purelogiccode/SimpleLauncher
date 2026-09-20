using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;
using ILogger = Serilog.ILogger;

namespace SimpleLauncher.Avalonia.Services.ContextMenus;

/// <summary>
///     Builds right-click context menus for game items (port of the WPF ContextMenuService).
///     The item set, order, separators, conditional achievements entry, and icons mirror
///     the original WPF implementation; the Avalonia-only extras are appended at the end.
/// </summary>
public class AvaloniaContextMenuService
{
    private readonly AvaloniaContextMenuFunctions _functions;
    private readonly LocalizationService _localization;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;

    public AvaloniaContextMenuService(
        LocalizationService localization,
        ILogger logger,
        IMessageBoxLibraryService messageBox,
        AvaloniaContextMenuFunctions functions)
    {
        _localization = localization;
        _logger = logger;
        _messageBox = messageBox;
        _functions = functions;
    }

    /// <summary>
    ///     Builds and opens a context menu for the given game context at the pointer location.
    /// </summary>
    /// <param name="context">The game and services context.</param>
    /// <param name="placementTarget">The control to anchor the menu to.</param>
    /// <param name="extras">Optional Avalonia-only extra actions (details/clipboard/folder/edit-system).</param>
    public void ShowContextMenu(AvaloniaRightClickContext context, Control placementTarget,
        GameContextMenuCallbacks? extras = null)
    {
        _logger.Debug("[AvaloniaContextMenuService] Building context menu for '{File}' (system '{System}')",
            context.FileNameWithExtension, context.SelectedSystemName);

        var contextMenu = new ContextMenu
        {
            Placement = PlacementMode.Pointer
        };

        // Launch Game Context Menu
        AddItem(contextMenu, "LaunchGame", "Launch Game", "launch.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "LaunchGame",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.LaunchGameAsync(context));
        });

        // Add To Favorites Context Menu
        AddItem(contextMenu, "AddToFavorites", "Add To Favorites", "heart.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "AddToFavorites", context.FileNameWithExtension);
            context.MainViewModel.StatusText = GetStatusOrFallback("AddingToFavorites", "Adding to favorites...");
            _ = SafeAsync(() => _functions.AddToFavoritesAsync(context));
        });

        // Remove From Favorites Context Menu
        AddItem(contextMenu, "RemoveFromFavorites", "Remove From Favorites", "brokenheart.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "RemoveFromFavorites", context.FileNameWithExtension);
            context.MainViewModel.StatusText =
                GetStatusOrFallback("RemovingFromFavorites", "Removing from favorites...");
            _ = SafeAsync(() => _functions.RemoveFromFavoritesAsync(context));
        });

        contextMenu.Items.Add(new Separator());

        // View Achievements Context Menu - Only add for supported systems (WPF parity)
        if (IsSystemSupportedForRetroAchievements(context))
        {
            AddItem(contextMenu, "ViewAchievements", "View Achievements", "trophy.png", () =>
            {
                _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                    "ViewAchievements", context.FileNameWithExtension);
                _ = SafeAsync(() => _functions.OpenRetroAchievementsWindowAsync(context));
            });
            contextMenu.Items.Add(new Separator());
        }

        // Open Video Link Context Menu
        AddItem(contextMenu, "OpenVideoLink", "Open Video Link", "video.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "OpenVideoLink", context.FileNameWithExtension);
            context.MainViewModel.StatusText = GetStatusOrFallback("OpeningVideoLink", "Opening video link...");
            _ = SafeAsync(() => _functions.OpenVideoLinkAsync(context));
        });

        // Open Info Link Context Menu
        AddItem(contextMenu, "OpenInfoLink", "Open Info Link", "info.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "OpenInfoLink", context.FileNameWithExtension);
            context.MainViewModel.StatusText = GetStatusOrFallback("OpeningInfoLink", "Opening info link...");
            _ = SafeAsync(() => _functions.OpenInfoLinkAsync(context));
        });

        // Open History Context Menu
        AddItem(contextMenu, "OpenROMHistory", "Open ROM History", "romhistory.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "OpenROMHistory", context.FileNameWithExtension);
            context.MainViewModel.StatusText = GetStatusOrFallback("OpeningROMHistory", "Opening ROM history...");
            _ = SafeAsync(() => _functions.OpenRomHistoryWindowAsync(context));
        });

        contextMenu.Items.Add(new Separator());

        // Media entries (WPF order: cover, title snapshot, gameplay snapshot, cart,
        // video, manual, walkthrough, cabinet, flyer, pcb)
        AddItem(contextMenu, "Cover", "Cover", "cover.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'", "Cover",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenCoverAsync(context));
        });
        AddItem(contextMenu, "TitleSnapshot", "Title Snapshot", "snapshot.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "TitleSnapshot", context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenTitleSnapshotAsync(context));
        });
        AddItem(contextMenu, "GameplaySnapshot", "Gameplay Snapshot", "snapshot.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "GameplaySnapshot", context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenGameplaySnapshotAsync(context));
        });
        AddItem(contextMenu, "Cart", "Cart", "cart.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'", "Cart",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenCartAsync(context));
        });
        AddItem(contextMenu, "Video", "Video", "video.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'", "Video",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.PlayVideoAsync(context));
        });
        AddItem(contextMenu, "Manual", "Manual", "manual.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'", "Manual",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenManualAsync(context));
        });
        AddItem(contextMenu, "Walkthrough", "Walkthrough", "walkthrough.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "Walkthrough", context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenWalkthroughAsync(context));
        });
        AddItem(contextMenu, "Cabinet", "Cabinet", "cabinet.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'", "Cabinet",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenCabinetAsync(context));
        });
        AddItem(contextMenu, "Flyer", "Flyer", "flyer.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'", "Flyer",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenFlyerAsync(context));
        });
        AddItem(contextMenu, "PCB", "PCB", "pcb.png", () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'", "PCB",
                context.FileNameWithExtension);
            _ = SafeAsync(() => _functions.OpenPcbAsync(context));
        });

        contextMenu.Items.Add(new Separator());

        // Take Screenshot Context Menu
        AddItem(contextMenu, "TakeScreenshot", "Take Screenshot", "snapshot.png", async () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "TakeScreenshot", context.FileNameWithExtension);
            context.MainViewModel.StatusText = GetStatusOrFallback("TakingScreenshot", "Taking screenshot...");
            await _messageBox.TakeScreenShotMessageBoxAsync();
            await _functions.TakeScreenshotOfSelectedWindowAsync(context);
        });

        // Delete Game Context Menu
        AddItem(contextMenu, "DeleteGame", "Delete Game", "delete.png", async () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "DeleteGame", context.FileNameWithExtension);
            context.MainViewModel.StatusText = GetStatusOrFallback("DeletingGame", "Deleting game...");
            var result =
                await _messageBox.AreYouSureYouWantToDeleteTheGameMessageBoxAsync(context.FileNameWithExtension);
            if (result == CoreMessageBoxResult.Yes)
            {
                await _functions.RemoveFromFavoritesAsync(context);
                await Task.Delay(500);
                await _functions.DeleteGameAsync(context);
            }
        });

        // Delete Cover Image Context Menu
        AddItem(contextMenu, "DeleteCoverImage", "Delete Cover Image", "delete.png", async () =>
        {
            _logger.Debug("[AvaloniaContextMenuService] Context menu action '{Action}' invoked for '{File}'",
                "DeleteCoverImage", context.FileNameWithExtension);
            context.MainViewModel.StatusText = GetStatusOrFallback("DeletingCoverImage", "Deleting cover image...");
            var result =
                await _messageBox.AreYouSureYouWantToDeleteTheCoverImageMessageBoxAsync(
                    context.FileNameWithoutExtension);
            if (result == CoreMessageBoxResult.Yes) await _functions.DeleteCoverImageAsync(context);
        });

        // ──── Avalonia-only extras (kept from the earlier port) ────
        if (extras is not null && context.SourceCard is { } card)
        {
            contextMenu.Items.Add(new Separator());

            void AddExtra(string resourceKey, string fallback, string glyph, Action<GameCardViewModel> action)
            {
                var header =
                    _localization.GetString(resourceKey) is { } s &&
                    !string.Equals(s, resourceKey, StringComparison.OrdinalIgnoreCase)
                        ? s
                        : fallback;
                var menuItem = new MenuItem { Header = $"{glyph} {header}" };
                menuItem.Click += (_, _) => action(card);
                contextMenu.Items.Add(menuItem);

                _logger.Debug("[AvaloniaContextMenuService] Adding context menu extra '{Action}' for '{File}'",
                    resourceKey, card.FilePath);
            }

            AddExtra("Context.ShowDetails", "Details", "\u2139", extras.OnShowDetails);
            AddExtra("Context.CopyPath", "Copy Path", "\uD83D\uDCCB", g => extras.OnCopyPath(g));
            AddExtra("Context.CopyName", "Copy Name", "\uD83D\uDCDD", g => extras.OnCopyName(g));
            AddExtra("Context.ShowInFolder", "Show in Folder", "\uD83D\uDCC2", g => extras.OnShowInFolder(g));
            AddExtra("Context.EditSystem", "Edit System", "\u270F", g => extras.OnEditSystem(g));
        }

        _logger.Debug("[AvaloniaContextMenuService] Showing context menu for '{File}'", context.FileNameWithExtension);

        contextMenu.Open(placementTarget);
    }

    private void AddItem(ContextMenu contextMenu, string resourceKey, string fallback, string iconFile, Action click)
    {
        var header =
            _localization.GetString(resourceKey) is { } s &&
            !string.Equals(s, resourceKey, StringComparison.OrdinalIgnoreCase)
                ? s
                : fallback;
        var menuItem = new MenuItem
        {
            Header = header,
            Icon = CreateIcon(iconFile)
        };
        menuItem.Click += (_, _) => click();
        contextMenu.Items.Add(menuItem);
    }

    private void AddItem(ContextMenu contextMenu, string resourceKey, string fallback, string iconFile,
        Func<Task> click)
    {
        var header =
            _localization.GetString(resourceKey) is { } s &&
            !string.Equals(s, resourceKey, StringComparison.OrdinalIgnoreCase)
                ? s
                : fallback;
        var menuItem = new MenuItem
        {
            Header = header,
            Icon = CreateIcon(iconFile)
        };
        menuItem.Click += (_, _) => _ = SafeAsync(click);
        contextMenu.Items.Add(menuItem);
    }

    private string GetStatusOrFallback(string key, string fallback)
    {
        return _localization.GetString(key) is { } s && !string.Equals(s, key, StringComparison.OrdinalIgnoreCase)
            ? s
            : fallback;
    }

    private bool IsSystemSupportedForRetroAchievements(AvaloniaRightClickContext context)
    {
        try
        {
            var hasherTool = App.ServiceProvider.GetRequiredService<IRetroAchievementsHasherTool>();
            var isSupported = hasherTool.IsSystemSupportedForHashing(context.SelectedSystemName);
            if (!isSupported)
            {
                _logger.Debug(
                    "[AvaloniaContextMenuService] RetroAchievements menu item omitted: system '{System}' is not supported",
                    context.SelectedSystemName);
            }

            return isSupported;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[AvaloniaContextMenuService] Error checking RetroAchievements system support");
            return false;
        }
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[AvaloniaContextMenuService] Error executing a context menu action");
        }
    }

    private static readonly Dictionary<string, Bitmap> IconCache = new(StringComparer.Ordinal);

    private static Control CreateIcon(string imageFileName)
    {
        try
        {
            // ~12 icons are requested per right-click; creating (and never disposing) a
            // native Bitmap each time leaked them. Cache one Bitmap per asset and reuse
            // it for every menu instance.
            Bitmap bitmap;
            lock (IconCache)
            {
                if (!IconCache.TryGetValue(imageFileName, out var cached))
                {
                    var uri = new Uri($"avares://SimpleLauncher.Avalonia/images/{imageFileName}");
                    cached = new Bitmap(AssetLoader.Open(uri));
                    IconCache[imageFileName] = cached;
                }

                bitmap = cached;
            }

            return new Image
            {
                Source = bitmap,
                Width = 16,
                Height = 16
            };
        }
        catch (Exception)
        {
            // Icon assets are linked into the bundle; a missing file must never break the menu.
            return new Panel();
        }
    }
}