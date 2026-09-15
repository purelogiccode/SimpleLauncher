# 08 — UI Layer

> Windows, pages, ViewModels, UI services, menus, themes, languages, screenshots.
> Related: [04 — Architecture](04-architecture.md) · [07 — Core Services](07-core-services.md)

## MainWindow — the central host

`MainWindow` (with **12 partial class files**) is the composition point for the UI. It implements 10 host interfaces (`IMenuCheckMarkHost`, `IUiResetHost`, `IUiOrchestratorHost`, `IStartupInitializationHost`, `IThemeMenuHost`, `ILanguageMenuHost`, `IStatusBarHost`, …) and wires services via `Initialize(host)` in its constructor (`MainWindow.xaml.cs:252-256`).

Partials:

| Partial file | Responsibility |
|---|---|
| `CloseWindowEvents.cs` | Deferred close (settings saved first), minimize-to-tray, disposal order |
| `GameFileLoadingHost.cs` | Exposes controls + `DisplaySystemSelectionScreenAsync` to the loading orchestrator |
| `GameItemRenderHost.cs` | Control accessors for the render service |
| `HostImplementations.cs` | Startup-init / theme / language / status-bar / tray hosts |
| `LaunchTools.cs` | External-tool click handlers (Xbox360 batch, ISO→XISO, →CHD) |
| `MenuActionHost.cs` | Delegates menu actions to browser/reset |
| `MenuCheckMarkHost.cs` | Menu-item accessors |
| `MenuItems.cs` | Menu click handlers → `_menuOrchestrator` + `SetViewMode` |
| `Pagination.cs` | Prev/next page handlers via `UiOrchestratorService` |
| `Search.cs` | Search button/Enter handlers → `ExecuteSearchAsync` |
| `SystemSelectionHost.cs` | System-selection screen host accessors |
| `UIResetHost.cs` | Reset state properties + cancellation token |

Key behaviors: system-combo change (`:472`), token cancellation (`:504`), page navigation (`:527-552`), mouse-wheel pagination (`:560`), favorites/feeling-lucky filters (`:637`, `:670`), `RefreshGameListAfterPlay` (`:713`), `SetLoadingState` (`:767`), selection/double-click/right-click (`:824-964`), `LoadGameFilesAsync` (`:1071`).

## Windows (22 root + 21 inject)

| Window | Purpose |
|---|---|
| `AboutWindow` | Version, "Check for Updates", update history, links |
| `DebugWindow` | Live Serilog log viewer (singleton; `-debug` flag; hide-on-close) |
| `DosBoxFileSelectionWindow` | DOSBox multi-file picker (`.conf/.bat/.exe/.com`) |
| `DownloadImagePackWindow` | Download & extract image packs |
| `EasyModeWindow` | Guided system setup wizard (downloads, add system) |
| `EditSystemWindow` (+ `EditSystemWindow.SaveSystem` partial) | Expert Mode system editor with validation |
| `FlashOverlayWindow` | Full-screen white flash for screenshots |
| `GlobalStatsWindow` | Play-time statistics per system (with emergency overlay release) |
| `ImageViewerWindow` | Media viewer (covers, snapshots, remote RA badges) |
| `RetroAchievementsWindow` | RA profile, unlocks, completion progress |
| `RetroAchievementsForAGameWindow` | Per-game achievements, rankings, progress |
| `RetroAchievementsSettingsWindow` | RA credentials + per-emulator configuration |
| `RomHistoryWindow` | `history.dat`/`history.xml` text viewer |
| `SetFuzzyMatchingWindow` | Fuzzy-matching threshold slider (70–95%) |
| `SetGamepadDeadZoneWindow` | Gamepad dead-zone sliders + revert confirmation |
| `SetLinksWindow` | Video/info URL templates |
| `SoundConfigurationWindow` | Notification sound picker/preview |
| `SupportWindow` | Bug report form (POSTs to support API) |
| `SystemSelectionWindow` | RA system picker (pre-selected guess) |
| `UpdateHistoryWindow` | `WhatsNew.md` markdown viewer |
| `UpdateLogWindow` | Live update-install log |
| `WindowSelectionDialogWindow` | Pick a window to screenshot |
| `InjectConfigWindows\Inject{Emulator}ConfigWindow` ×21 | Emulator settings dialogs (Ares…Yumir) |

## Pages (`Pages\`)

| Page | Purpose |
|---|---|
| `FavoritesPage` | Favorites grid: preview, launch, Delete-key removal, right-click |
| `GlobalSearchPage` | Cross-system search (filters, relevance sort, cancel) |
| `PlayHistoryPage` | History table with sorting, launch-refresh, remove-all |

## ViewModels (`ViewModels\`, 44 total)

All use **CommunityToolkit.Mvvm** (`ObservableObject` + `[RelayCommand]`). Groups:

- **Pages:** `FavoritesViewModel`, `GlobalSearchViewModel` (AND/OR parsing, scoring, cancel), `PlayHistoryViewModel` (multi-format date parsing, sorts).
- **Emulator settings:** `Inject{Emulator}ConfigViewModel` ×21 — each maps `XxxSettings` to fields and calls the matching Core `XxxConfigurationService`.
- **RA:** `RetroAchievementsViewModel`, `RetroAchievementsSettingsViewModel` (saves credentials; "Configure Emulator" dispatch to `RetroAchievementsEmulatorConfiguratorService`).
- **Dialogs:** `AboutViewModel`, `DosBoxFileSelectionViewModel`, `DownloadImagePackViewModel`, `FlashOverlayViewModel`, `ImageViewerViewModel`, `RomHistoryViewModel`, `SetFuzzyMatchingViewModel`, `SetGamepadDeadZoneViewModel`, `SetLinksViewModel`, `SoundConfigurationViewModel`, `SupportViewModel`, `SystemSelectionViewModel`, `UpdateHistoryViewModel`, `UpdateLogViewModel`, `WindowSelectionDialogViewModel`.
- **Infrastructure:** `GameButtonViewModel` (grid item), `DebugViewModel`.

## UI services (by area)

### Menus & navigation

| Service | Purpose |
|---|---|
| `MenuActionHandlerService` | Routes every main-menu action (Easy/Expert mode, image packs, scans, links, gamepad, dead zone, fuzzy matching, support, donate) |
| `MenuCheckMarkService` | "Exactly one checkmark per group" for 46 menu items (sizes, pages, show-games, aspect, filename display, fonts, view mode) |
| `MenuOrchestratorService` | Dispatches menu clicks to the above |
| `ThemeMenuService` | Theme/accent menu state + apply |
| `LanguageMenuService` | Language menu state + restart flow |
| `FilterMenu` | A–Z / # letter filter bar (keyboard navigable) |

### Game list rendering

| Service | Purpose |
|---|---|
| `GameButtonFactory` | Grid buttons: cover, favorite star, video/info/RA shortcut buttons |
| `GameListFactory` | List rows: MAME description, play count/time |
| `GameItemRenderService` | Batched rendering (100 items/batch), view-mode aware |
| `GameListUIService` | "No games matched" message, button enable/disable, pre-load reset |
| `GameBrowserService` | Facade over load/render/search/scan orchestrators (`ScanForStoreGamesAsync`) |
| `GameFileLoadingOrchestratorService` | Load pipeline: cache-or-disk list, GroupByFolder, MAME sort, pagination, RA fuzzy match, `OnGameFilesChangedAsync` auto-refresh |
| `BitmapImageConverter` | Stream/byte[] → frozen `BitmapImage` |
| `SystemImageResolverService` | `images\systems\{name}.png` with annotation stripping + fuzzy fallback |
| `DisplaySystemInformation` | System-config summary + path validation (red markers, error dialog) |
| `LoadingOverlayService` | Reference-counted loading overlay + emergency release |
| `UiResetService` | Full UI reset (filters, selection, pagination, back to system selection) |
| `UpdateStatusBarService` | Status text + auto-clear timer (default 3 s) |

### Context menu

| Service | Purpose |
|---|---|
| `ContextMenuService` | Builds the right-click menu per context (grid/list/favorites/search/history) |
| `ContextMenuFunctions` | Executes actions: favorites, video/info links, ROM history, RA window, media viewers, screenshot, delete game/cover |

### Tray, hotkey, screenshot

| Service | Purpose |
|---|---|
| `TrayIconManager` | Tray menu (Open / Minimize to Tray / Debug Window / Exit), double-click restore |
| `GlobalHotkeyService` | System-wide **F8** registration with conflict warning |
| `ActiveWindowScreenshotService` | Foreground-window PNG capture into `.\screenshot` (via `WindowSelectionDialogWindow` + `FlashOverlayWindow` + Core `WindowManager`) |

## Themes & language

- **Themes (5):** Light, Dark, **Adaptive** (syncs with Windows Light/Dark), **High Contrast**, **Midnight** — applied via MahApps `ThemeManager` (`App.xaml.cs:925-967`); High Contrast and Midnight are Dark + `resources2\Theme.HighContrast.xaml` / `Theme.Midnight.xaml` overrides (`:897-907`); 27 accent colors incl. custom Maroon/OliveDrab/Plum/SkyBlue (`:930-942`); `ApplyThemeToWindow` runs in every window ctor (`:1030-1071`); `ChangeTheme` persists + re-applies to all open windows (`:1099-1117`).
- **Languages (18):** `ApplyLanguage(code)` sets `CurrentCulture`/`CurrentUICulture` and builds the WPF resource dictionary from the shared JSON packs (embedded as pack resources `resources/strings.{code}.json` in `SimpleLauncher.g.resources`, sourced from `SimpleLauncher.Core\Localization`), English fallback on error. Codes like `pt-br`/`zh-hans` are resolved case-insensitively. Language switch triggers an app restart.

## Related docs

- [04 — Architecture](04-architecture.md)
- [05 — Configuration](05-configuration.md)
- [13 — Logging & Debug](13-logging-and-debug.md)
- [ManualTests.md](manual-tests.md) — the UI items that still need manual testing
