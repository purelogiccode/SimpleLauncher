# Simple Launcher — Manual Test Plan (areas not covered by unit tests)

**Scope:** This document lists only functionality **not covered** by the automated tests in `SimpleLauncher.Tests`
(150 test files). The unit suite covers the core logic layer (settings, system manager, favorites, play history,
game scanner, file finder, search orchestrator, launch strategies, mount-strategy matching, config-injection services,
models, path/URL/pagination helpers, RetroAchievements manager/matcher/hasher, Steam VDF parser, update-check logic,
API connectivity, converters' strategy classes, parameter resolver API service, `system.xml` writer, emulator XML
helpers, game file watcher, loading overlay / UI reset / status bar / menu check-mark services, credential protector,
etc.) — none of that is repeated here.

Everything below must be verified by hand in the running application. Items marked **[Integration]** require real
external components (emulators, store clients, API endpoints, network) and cannot be fully verified otherwise.

**Method note:** coverage was determined by scanning all production types in `SimpleLauncher` + `SimpleLauncher.Core`
(462 types) against references in `SimpleLauncher.Tests`; 259 types have no test reference. Windows, Views, ViewModels,
and dispatcher-dependent WPF services are inherently untested and are the bulk of this list.

**Suggested workflow:** one full pass through Easy Mode → Edit System → Emulator Settings → launch a game per emulator →
RetroAchievements → a download → a store-game scan covers most of this checklist.

**Recorded platform verification:** SimpleLauncher.Avalonia 5.8.0 (`linux-x64`, current HEAD `1142d050`, packaged with
`scripts/package-release-linux.ps1`) was smoke-tested on **Debian 13 (trixie) x64** in a VMware Workstation VM — GNOME
Wayland session, app rendered through Xwayland. `unzip` restored the executable bits from the ZIP; the first-run
"Welcome" dialog and the main window rendered correctly and the app produced no stdout/stderr errors (2026-09-24).

---

## 1. Startup & app lifecycle

- [X] **First-run flow** (`StartupInitializationService`) — with an empty config, the app auto-scans the machine for installed games (Steam, Epic, GOG, Microsoft Store, etc.) and creates the "Microsoft Windows" system; the Easy Mode welcome prompt appears only if that scan finds no games; with an existing config it loads straight into the main window.
- [X] **Closing behavior** (`MainWindow.CloseWindowEvents`) — close the app then immediately check the settings file: close is deferred until settings are saved. Close while a CHD mount or scan is active → child processes killed, no crash.
- [X] **Minimize to tray** — minimize hides the window from the taskbar, tray icon remains; tray menu has Open / Minimize to Tray / Debug Window / Exit; double-click the tray icon restores the window; Exit fully quits (icon disappears).
- [X] **Quit** (`QuitSimpleLauncher`) — menu quit exits;
- [ ] **Reinstall / Updater** (`ReinstallSimpleLauncher`) — with and without a local `Updater.exe`, with and without network, and with access denied (error 5): correct message box in each case; app closes after the updater launches; no zombie processes.
- [X] **Update flow** (`ShutdownForUpdateAsync`) — with GitHub reachable, a fresh `Updater.exe` is downloaded and launched with the current PID, app exits; with GitHub unreachable, graceful failure.
- [X] **System information display** (`DisplaySystemInformation`) — select a system with a bad ROM folder → red "System Folder" line + "path is not valid" dialog; bad emulator exe → red emulator line; all valid → no dialog; the error list shows every problem.
- [X] **System config load** (`SystemConfigurationService`) — edit `system.xml` adding a valid system → it appears after restart; malformed XML → error dialog + logs, app continues.

## 2. Main window UI

- [X] **Theme menu** (`ThemeMenuService`) — pick Light/Dark/Adaptive/HighContrast/Midnight → UI restyles instantly; accent colors apply; checkmark tracks selection; restart → theme persisted.
- [X] **Language menu** (`LanguageMenuService`) — switch language → notification sound, status "Changing language...", app restarts into that language; checkmark shows current language.
- [ ] **View/Display menu options** — change thumbnail size, games-per-page, show-games, aspect ratio, filename display, font sizes, view mode → the grid re-renders accordingly; restart → checkmarks restored from settings.
- [ ] **Menu actions** (`MenuActionHandlerService`) — every menu item opens the right window / performs the right action with status-bar feedback: Easy/Expert mode, Download Image Pack, Scan for store games, Edit links, Gamepad toggle, Dead zone, fuzzy matching toggle/threshold, annotation stripping, Support, Donate. Rapid clicking → no double handling.
- [X] **Platform-specific menus & data (Avalonia)** — on Windows the Tools menu (11 bundled tools), Options → Inject Emulator Config (21 emulators) and Edit System → "Scan for Microsoft Windows games" are present; on Linux/macOS all three are hidden (Windows executables / registry-based features) and the first-run flow skips the "Scanning for Windows games..." overlay. About → Open AppData Path opens the folder that actually holds `settings.dat`: `%LocalAppData%\SimpleLauncher` on Windows, `~/.local/share/SimpleLauncher` (XDG) on Linux/macOS, where `settings.dat` (+ `-wal`/`-shm`), logs, window bounds and legacy `.bak` files live.
- [X] **Filter bar** (`FilterMenu`) — A–Z / # / All letters filter the game list with button highlight; "All" resets; keyboard arrows/Home/End navigate the letter buttons; notification sound on click.
- [X] **Status bar** — actions (sorting, launching, saving) show status text that auto-clears after the timeout.
- [X] **Gamepad navigation** — see section 10.

## 3. Game list, search & pages

- [ ] **Grid rendering** (`GameItemRenderService`, `GameButtonFactory`) — grid shows cover (or placeholder), favorite star reflects state, video/info/RA shortcut buttons work; page through 1000+ games — UI stays responsive; switch system mid-render → no stale items.
- [ ] **List rendering** (`GameListFactory`) — list rows show MAME machine description, times-played and play-time matching play history.
- [ ] **Loading / no-files states** (`GameListUIService`) — search with no matches → localized "no games matched" text in both views; loading a system scrolls to top and clears preview; during launch all buttons disable then re-enable; two concurrent loads keep the overlay until **both** finish; the emergency return button releases a stuck overlay and re-enables the UI.
- [ ] **Images** (`BitmapImageConverter`) — every cover/preview in grid, list, Favorites, Search, History renders; a corrupt image file → placeholder/blank, no crash; streams disposed (covers deletable right after browsing).
- [ ] **System image resolution** (`SystemImageResolverService`) — exact name match shown; with fuzzy matching ON (threshold 0.7–0.95) a slightly renamed image is found; with annotation stripping ON, `Game (USA)` matches `Game.png`; a too-low threshold can produce a wrong match (acceptable).
- [ ] **Favorites page** (`FavoritesPage` + `FavoritesViewModel`) — favorites load with cover preview on selection; launch via button/double-click/Enter; Delete key removes selected rows with trash sound; right-click works; missing file → "delete favorite?" prompt; empty selection → guidance box.
- [ ] **Global search** (`GlobalSearchPage` + `GlobalSearchViewModel`) — search across systems with system filter and filename/description/folder-name/recursive options; Enter triggers search; overlay "Searching..."; results sorted by relevance; AND (`term1 term2`), `OR`, quoted phrases work; leaving the page cancels an in-flight search without crash or stale results; rows for systems without an emulator show localized "No Default Emulator".
- [ ] **Play history page** (`PlayHistoryPage` + `PlayHistoryViewModel`) — rows show date/time/play-count/play-time; sorting by date / total time / times played keeps selection; launching a game refreshes its row (+1 play) and restores selection; Delete and Remove All ask confirmation.
- [ ] **Right-click context menu** (`ContextMenuService`, `ContextMenuFunctions`) — right-click a game in grid, list, Favorites, Global Search and Play History → correct items for that context; right-click empty area → no menu. Verify: add/remove favorite updates the star instantly and persists across restart; video/info links open the configured URL templates; ROM History opens the history window; delete game asks confirmation and updates the list; delete cover removes it and re-finds another; missing game file → offer to remove favorite/history entry.

## 4. Screenshot & hotkeys

- [X] **F8 hotkey** (`GlobalHotkeyService`) — with another app foreground, F8 saves a screenshot of that window to `.\screenshot`; launching a second app instance → F8 conflict logged, no crash; after exit the hotkey is released (F8 behaves normally elsewhere).
- [X] **Screenshot flow** (`ActiveWindowScreenshotService`, `WindowSelectionDialogWindow` + VM, `WindowManager`, `FlashOverlayWindow` + VM) — right-click a game → screenshot flow: the window-selection dialog lists all titled top-level windows (not hidden launcher windows); selecting one closes the dialog and captures it; shutter sound + single full-screen white flash (~0.6 s); minimized target → "cannot screenshot a minimized window" message; capture of a normal window saves a PNG and refreshes the game button image; Cancel aborts silently.
- [X] **[Integration] Window edge cases** (`WindowManager`) — UWP/minimized-to-tray windows are excluded from the list; no crash when enumerating unusual windows.

## 5. Easy Mode & Edit System

### Easy Mode wizard (`EasyModeWindow`)
- [X] With zero systems configured → welcome prompt opens Easy Mode; system dropdown sorted, only systems with download links listed.
- [X] Selecting a system enables emulator/core/image-pack buttons only when a download link exists; "Add System" stays disabled until the required emulator/core is downloaded or already on disk.
- [ ] Start each download → progress bar + status text; **Stop** mid-download → "Download canceled", button re-enables; closing the window during a download cancels cleanly (no crash, temp cleanup).
- [ ] Custom ROM folder picker works; blank → defaults to `%BASEFOLDER%\roms\<System>`; **Add System** → loading overlay, success message, system appears after reload, folders created on disk.
- [ ] Kill the network mid-download → per-component error dialog, button resets to Failed; emergency return button releases a stuck overlay.
- [ ] **[Integration] Download manager** (`DownloadManager`) — progress %/size updates; start a download with <5 GB free → "Insufficient disk space" error; drop the network mid-download → "Download error. Retrying (1/3)…" then success or failure; cancel → partial file removed, clean state.

### Edit System save/validation (`EditSystemWindow.SaveSystem`)
- [ ] Invalid characters in system name → rejected with message + red highlight.
- [ ] Relative paths (`roms`, `.\roms`, `../x`) are rewritten to `%BASEFOLDER%\...` and round-trip after reload; absolute paths untouched; surrounding quotes are trimmed.
- [ ] Empty system image folder or invalid emulator path → field highlighted, save blocked; duplicate emulator names rejected; emulators 2–5 with location/parameters but no name → "name required".
- [ ] GroupByFolder with a non-MAME/DOSBox emulator → warning prompt; "No" aborts the save; with MAME/DOSBox → no warning.
- [ ] Renaming an existing system replaces the old config entry (no duplicate) and creates folders for the new name; save failure (read-only `SimpleLauncher.xml`) → friendly error, window stays open.
- [ ] **AI parameter suggestion** — in Edit System, click the suggest button: suggested parameter + explanation dialog appears; applying saves it to the config; wrong API key / offline → graceful message; loading overlay shown during the call.
- [ ] **AI fix after failed launch** (`AskAiToFixParameters`) — force an emulator launch failure, accept AI help → suggestion dialog; apply → parameters updated in config, system list reloads, relaunch uses the new params; decline → nothing saved; offline/API error → graceful message.

### Other dialogs
- [X] **Set Fuzzy Matching** (`SetFuzzyMatchingWindow` + VM) — slider snaps to 5% ticks (70–95%), shows percentage; Cancel doesn't save; Save persists; restart → value retained and matching behaves accordingly.
- [ ] **Set Gamepad Dead Zone** (`SetGamepadDeadZoneWindow` + VM) — X/Y sliders; Save → confirmation box; Revert → defaults restored and window closes; with a gamepad connected, verify stick drift is filtered per the new dead zone.
- [ ] **Set Links** (`SetLinksWindow` + VM) — blank URLs fall back to defaults on save; custom template used from the context menu; Revert restores appsettings defaults.
- [ ] **Sound Configuration** (`SoundConfigurationWindow` + VM) — toggle enable → controls enable/disable; Choose File copies an MP3 into `audio\`; Play previews it; Play with sounds disabled → info box; Reset → `click.mp3`; Save persists; a notification (e.g. game launch) then uses the configured sound.
- [X] **About / Update History / Update Log** (`AboutWindow`, `UpdateHistoryWindow`, `UpdateLogWindow` + VMs) — About shows the correct version; "Check for Updates" disables while running, offline → friendly error, button re-enables; Update History renders `WhatsNew.md` markdown with clickable links; with `WhatsNew.md` deleted → "not found" message; during an update install the log window appends timestamped lines live without freezing.
- [ ] **Support window** (`SupportWindow` + VM) — empty form → per-field validation; valid form → overlay "Sending support request...", form clears on success; emergency return button on the overlay works; closing mid-send doesn't crash; failure → error box + log entries.
- [ ] **Image viewer** (`ImageViewerWindow` + VM) — each artwork context-menu item renders; missing/corrupt file → error box, empty window, no crash; remote URL (RA badge) renders; unreachable URL → blank window, no hang.
- [X] **ROM History window** (`RomHistoryWindow` + VM) — MAME game with a `history.dat`/`history.xml` entry shows text with clickable URLs; no entry → "No ROM history found…" + Yes/No prompt; Yes opens a Google search; both files deleted → friendly "no history file found" message.
- [ ] **DOSBox file selection** (`DosBoxFileSelectionWindow` + VM) — a DOS game folder with several `.conf/.bat/.exe/.com` files shows the picker with relative subfolder labels; single-click + Launch and double-click both launch the chosen file; Cancel/X → launch aborted silently; files in the base folder show no subfolder text.
- [ ] **System selection** (`SystemSelectionWindow`) — appears when a game's system can't be auto-matched; current guess pre-selected; confirm with no selection does nothing; Cancel returns false.
- [ ] **File pickers** (`AvaloniaFilePickerService`) — on Linux the emulator picker shows all files (no `*.exe` restriction) while Windows still offers "Executable Files"; a picker filter with a specific entry plus "All files" (e.g. Sound Configuration MP3) still shows the specific entry (LB-10); opening a picker from a child window (RA settings, Edit System) makes the dialog modal to that window, not the MainWindow (LB-21, noticeable on Wayland/GNOME).

## 6. Emulator launch & config injection **[Integration]**

### Launch-time handlers (21 emulators — `Services\GameLauncher\Handlers\`)
Common flow for **each** emulator:
- [ ] Add a real ROM + point the launcher at a real emulator install; launch with "Show settings before launch" **off** → the emulator's own config file is rewritten with SimpleLauncher's settings and the game boots.
- [ ] Launch with "Show settings before launch" **on** → the injection dialog appears; "Run" launches, "Cancel" does not launch.
- [ ] Wrong/missing emulator path → the game still attempts launch without crashing; check the Debug log for injection errors.
- [ ] **Linux/macOS (Avalonia)** — the handlers are not registered and the "Inject emulator config" menu is hidden: launching any game writes no Windows-style config and opens no injection dialog (LB-08); check that no files appear under `~/Documents` or next to the emulator binary.

Per-emulator specifics:
- [ ] **Ares** — config applied; dialog cancel aborts.
- [ ] **Azahar** — make `qt-config.ini` read-only → permission message box (`AzaharPermissionException`), game still starts.
- [ ] **Blastem** — unconfigured emulator path → logs warning, launch proceeds.
- [ ] **Cemu** / **Mednafen** / **Mesen** / **Redream** / **Stella** / **Supermodel** / **Yumir** / **Rpcs3** — settings visible in the emulator after launch; dialog cancel aborts.
- [ ] **Daphne** — no config file is written: CLI args are appended to the command line (framefile etc.); verify the emulator command line and that the game boots; no exe-path check at all.
- [ ] **Dolphin** — settings (gfx backend, Wiimote) visible in `Dolphin.ini` after launch.
- [ ] **DuckStation** — BIOS/fullscreen settings applied before PS1 launch.
- [ ] **Flycast** — per-game settings written to `emu.cfg`.
- [ ] **MAME** — secondary system folders are injected into `rompath`; read-only `mame.ini` → "Failed to inject" box, game still runs.
- [ ] **PCSX2** — read-only ini → permission message (`Pcsx2PermissionException`), launch continues.
- [ ] **Raine** — ROM dir configured; `rompath` in `raine.cfg`; game boots.
- [ ] **RetroArch** — `retroarch.cfg` rewritten; cores still run.
- [ ] **Sega Model 2** — missing emulator path → no crash, launch attempted.
- [ ] **Xenia** — read-only config → game still launches with defaults (all exceptions swallowed).

### Emulator settings windows (21 dialogs — `InjectConfigWindows\` + VMs)
Shared flow per emulator (sample 3–4 in depth, then spot-check the rest):
- [ ] Menu → Emulator settings with the emulator not yet configured → path picker appears; Cancel closes silently; a wrong exe → generic "injection failed" message.
- [ ] Each field loads from saved settings; Save → success; reopen → values persisted; restart app → still persisted.
- [ ] Verify the written config file (path in table below): UTF-8 without BOM, other pre-existing keys/comments preserved, values match the UI.
- [ ] Delete the config file → next save recreates it from `samples\<Emulator>\<file>`; if the sample is missing → graceful failure message.
- [ ] Set the config file read-only → Save shows a failure box, window closes, no crash. Azahar/PCSX2 show their dedicated permission messages.
- [ ] Launch a game with "Show settings before launch" on → Run injects then launches; Save injects but does NOT launch; Cancel writes nothing.

| Emulator | Config file written | Notes / risks |
|---|---|---|
| Ares | `settings.bml` | video driver, shader, rewind, run-ahead, auto-save memory |
| Azahar | `qt-config.ini` | graphics/resolution/fullscreen; file in use → `AzaharPermissionException` |
| Blastem | `default.cfg` | fullscreen, vsync, scanlines, aspect, audio |
| Cemu | `settings.xml` | fullscreen, graphics API, async compile, Discord |
| Daphne | *(none — launcher settings only)* | fullscreen, bilinear, resX/Y, sound, crosshairs, overlays |
| Dolphin | `Dolphin.ini` (portable `User\` first, else `Documents\Dolphin Emulator\`) | gfx backend, DSP thread, Wiimote scanning/speaker — test both layouts |
| DuckStation | `settings.ini` | renderer, res scale, widescreen hack, PGXP, rewind, run-ahead |
| Flycast | `emu.cfg` | fullscreen, maximized, width/height |
| MAME | `mame.ini` | also injects system ROM path + secondary folders into `rompath`; temp-file+move write; "Restore from sample" creates `mame.ini.bak` |
| Mednafen | `mednafen.cfg` | video driver, shader, cheats, rewind |
| Mesen | `settings.json` (JSON) | corrupt the JSON first → graceful failure |
| PCSX2 | `PCSX2.ini` (portable `portable.ini` → `inis\`; else emu dir; else `Documents\PCSX2\inis\`) | renderer, upscale, widescreen patches, cheevos; test all 3 locations incl. OneDrive-redirected Documents |
| Raine | `config\raine32_sdl.cfg` | injects current game file + system ROM path; NeoCD BIOS |
| Redream | `redream.cfg` | renderer, region, language, latency |
| RetroArch | `retroarch.cfg` | aspect-ratio tags map correctly; cheevos enable/hardcore; menu driver |
| RPCS3 | `config.yml` (YAML) | verify YAML structure preserved |
| Sega Model 2 | `EMULATOR.INI` | widescreen, FSAA, XInput, force feedback |
| Stella | `stella.sqlite3` (SQLite upserts) | sample DB copied if missing; DB locked while Stella runs → graceful failure |
| Supermodel | `Config\Supermodel.ini` (`[Global]`) | new 3D engine, quad rendering, PowerPC frequency |
| Xenia | `xenia-canary.config.toml` + `xenia.config.toml` (else `Documents\Xenia\`) | both TOMLs updated, syntax intact; no config found → warning, no crash |
| Yumir | `Ymir.toml` | force aspect, latency, auto region, video standard |

## 7. Extraction, conversion & mounting **[Integration]**

- [ ] **Extraction before launch** (`ExtractionService`) — 7z/ZIP/RAR ROM launches: multi-file archives, archives locked by antivirus (10×1 s retry), insufficient disk space, corrupted archive → 7-Zip fallback (`tools\SevenZip\`: `7za` on Windows, `7zz` on Linux) or failure box + partial cleanup, crafted zip-slip archive → "PotentialPathManipulation" box.
- [ ] **Commander Genius launch** (`CommanderGeniusLaunchStrategy`) — archive extracted to `games/<zipname>` and CG opens the game; on Linux/macOS the data folder resolves to `~/.CommanderGenius` and no stray `~/Commander Genius` folder is created (LB-11); Windows keeps `Documents\Commander Genius`.
- [ ] **CHD→CUE/BIN and PBP→CUE/BIN** (`DiscConverter`) — launch a CHD game on a CUE/BIN-only emulator → temp `.cue/.bin` in `%TEMP%\SimpleLauncher`, game boots, temp files cleaned after exit; same for PBP (PS1); corrupt file → error, no hang; huge files → 5-minute timeout path.
- [ ] **RVZ/WBFS/GCZ→ISO** (via RetroAchievements hasher, `DiscConverter.ConvertToIsoAsync`) — GameCube/Wii game hashed → converted with `DolphinTool.exe`, temp ISO deleted afterwards.
- [ ] **CHD mount** (`MountChdDrive`) — PSX game mounted via CHDMounter: exit game → mount process killed, drive letter disappears within ~20 s; kill CHDMounter externally while the game runs → unmount still cleans up; Dokan not installed → "Dokan driver not found" box.
- [ ] **ISO mount** (`MountIsoFiles`) — PS3 ISO with `EBOOT.BIN` launches and the drive is dismounted after exit; ISO without EBOOT.BIN → error box + dismount; PowerShell execution-policy restricted → `UnabletomountIsOfile` box; 30 s timeout kill.
- [ ] **XISO mount** (`MountXisoDrive`, `MountXisoFiles`) — original-Xbox XISO launches via Dokan; virtual drive letter (picked Z→D) is released after exit; missing `tools\SimpleXisoDrive\*.exe` → mount-error box; no free drive letter → error box; wrong XISO layout → timeout + error, no leaked process.
- [ ] **External tools** (`ExternalToolLauncherService`) — run each tool from the UI: Create Batch Files (PS3/ScummVM/Windows/Xbox 360 XBLA), Batch Convert ISO→XISO, →CHD, →Compressed, →RVZ, Rom Validator, Find Rom Cover, Retro Game Cover Downloader — launches with correct folder/args; missing tool → "not found" box; UAC cancel (error 1223) → "canceled" box; corrupt exe → PE check rejects it.
- [ ] **Linux/macOS launch pipeline (Avalonia)** — ZIP/7Z/RAR for ScummVM/RPCS3/XBLA extract to `%TEMP%/SimpleLauncher/ZipLaunch/<guid>` (upper-case ROM/EXE names found, Windows-authored `DIR\FILE.BIN` entries land in a real subfolder) and the temp folder is deleted after exit; a `.chd` for a non-DOSBox system is converted with CHDSharp and every temp file (including multi-track bins) is cleaned up (LB-19); a DOSBox `.iso`/`.chd` is `imgmount`ed directly or converted, with the temp conf deleted after exit; a mount service path on Linux logs a single Information-level "not supported on this platform" (no Warning, no bug report — LB-14); the `7zz` fallback extracts a broken 7z from a Debug build without a manual `chmod +x` (LB-15).

## 8. Game file watcher (5.6.0) — end-to-end flow

These checks cover the watcher's behavior through the running app (end to end):

- [X] **Auto-refresh on external changes** (`GameFileWatcherService`) — with a system selected, add/delete/rename a ROM in its folder → the game list refreshes once ~500 ms after the change (not per file during a batch copy/extract).
- [ ] Changes in a **non-selected** system's folder are ignored.
- [ ] Watching starts/stops when switching systems; closing the app unsubscribes all watchers (no exceptions in the debug log).

## 9. Platform game scanners **[Integration]**

Run "Scan for store games" after installing 1–2 real games per store. Verify per platform: shortcut created with correct name/protocol, cover image downloaded, DLC/tools (UE, redistributables) skipped, and re-running the scan is idempotent (no duplicate/corrupted shortcuts when two stores ship the same game name). On Linux/macOS the menu item is hidden and the click handler returns immediately, so no Windows ROM folders are created even if the handler is invoked programmatically (LB-22).

- [ ] **Amazon** (`ScanAmazonGames`) — `amazon-games://play/{id}` URLs from the Amazon Games SQLite DB; DB locked → no crash.
- [ ] **Battle.net** (`ScanBattleNetGames`) — WoW/Diablo IV etc. detected; classics (Diablo II, WC3) get working `.bat` launchers; titles not in the table skipped.
- [ ] **EA App** (`ScanEaGames`) — `origin2://game/launch` URLs from the registry + game-classification API (`GameClassificationClient` → `api/GameIdentification/IsAGame`, same as Microsoft Store); non-game EA software filtered; offline → graceful (no shortcuts).
- [ ] **Epic** (`ScanEpicGames`) — `LauncherInstalled.dat` + manifest fallback; UE tools/DLC/non-games filtered.
- [ ] **GOG** (`ScanGogGames`) — base games only (DLC skipped via `goggame-*.info`); `.bat` launches the game directly.
- [ ] **Humble** (`ScanHumbleGames`) — installed and downloaded-but-present games appear via `humble://launch/{machineName}`.
- [ ] **itch.io** (`ScanItchioGames`) — games with/without `.itch.toml`; slug fallback naming; `.bat` runs the game exe.
- [ ] **Microsoft Store** (`ScanMicrosoftStoreGames`) — Game Pass games detected and classified via the HTTP API; non-game UWP apps filtered; offline → graceful.
- [ ] **Rockstar** (`ScanRockstarGames`) — GTA V / RDR2 detected via uninstall strings; Definitive Editions use correct exe paths.
- [ ] **Steam** (`ScanSteamGames`) — games on secondary library drives detected; source mods (HL2 mods) included; `steamapps` on a non-existent drive skipped without crash; Steam artwork copied.
- [ ] **Ubisoft Connect** (`ScanUplayGames`) — `uplay://launch/{id}` URLs; `\` vs `/` path normalization; " Edition" suffix stripped.
- [ ] **Icon extraction** (`IconExtractor`) — store-game shortcuts get PNGs for exes with and without embedded icons (no crash, no leak).

## 10. Gamepad & audio

- [ ] **Gamepad navigation — Windows** (`GamePadController`) — with Enable GamePad Navigation on: Xbox pad left stick moves the cursor, A = left click, B = right click, right stick scrolls; PS pad (DirectInput) behaves the same; unplug/replug → reconnects within ~5 s; dead-zone sliders filter stick drift; disabling the setting stops input; exiting the app with a pad connected → no crash.
- [ ] **Gamepad navigation — Linux/macOS** (`SdlGamepadBackend` + `GamepadNavigationService`, Avalonia) — with Gamepad Support on: left stick/D-pad move the keyboard focus ring (held stick repeats after a delay), A activates the focused control, B opens the selected game's context menu or sends Escape, right stick scrolls the focused `ScrollViewer`; Xbox/PS/Switch pads and unknown joysticks work (SDL GameController mapping with a generic fallback); unplug/replug or a virtual `uinput` pad → reconnects within ~5 s and no Warning+ log is written; disabling the setting stops input. Verified on Ubuntu 24.04 GNOME Wayland with a `uinput` virtual Xbox 360 pad (no OS-level input permissions needed).
- [ ] **Sounds** (`PlaySoundEffects`, `AudioInputService`) — click/notification/shutter/trash sounds play; disabling sounds in settings → silent; custom notification file plays; missing file → logged, no crash; rapid clicks → previous sound stops (no overlap, no leak); on Linux/macOS MP3/WAV play with no extra packages, FLAC/Ogg/Opus need `libsndfile1`, and a missing library/device or corrupt file is logged at Information level (not Error).

## 11. RetroAchievements (app layer) **[Integration]**

- [ ] **Login & API** (`RetroAchievementsService`) — valid credentials → session token; wrong password → silent failure; wrong API key → unauthorized handled as an error dialog; offline → graceful nulls with logged errors; RA windows show correct progress %, points (hardcore vs softcore), badges, rank/score tables; completion-progress pagination works.
- [ ] **Credential protection** — saved RA/DuckStation credentials are stored encrypted (not plaintext); restart → credentials still load; moving settings to another user/machine → graceful null.
- [ ] **Per-game window** (`RetroAchievementsForAGameWindow`, `RaAchievement`) — locked vs unlocked badges, 🏆 hardcore icon, "Hardcore/Casual/Not Earned" labels, rarity "X.X% hardcore", "Unknown" author fallback; remote badge URLs render (unreachable → blank, no hang).
- [ ] **RA settings window** (`RetroAchievementsSettingsWindow` + VM) — save credentials → fields pre-filled on reopen; "Configure Emulator" writes login/token into the emulator config and reports success/failure; wrong password → clear error; restart → credentials retained.
- [ ] **Emulator configurator** (`RetroAchievementsEmulatorConfiguratorService`, Windows only — the Emulator Integration section and the pre-launch config handlers are hidden/not registered on Linux/macOS) — for each supported emulator (RetroArch, PCSX2, DuckStation + encrypted token, PPSSPP + `.dat` session file, Dolphin, Flycast, BizHawk JSON): save credentials then verify the keys (username/token/hardcore) in the emulator's config; delete config → restored from `samples\`; read-only config → false + log, no crash.
- [ ] **Hashing flow** (`RetroAchievementsHasherTool`) — RA icon on: (a) NES ROM → header-strip hash via the bundled RetroAchievementsSharp CLI tool, no dialog; (b) unknown-named system → system picker with pre-selected guess, cancel → "System selection cancelled"; (c) zipped PS1 game → hashed through the CLI (no extraction, no temp files left), temp cleaned; (d) GameCube `.rvz` → hashed directly (no ISO conversion, no temp ISO); (e) unsupported system (e.g. C64) → no RA icon shown.

## 12. Debug window & logging

- [X] **Debug window** (`DebugWindow`, `DebugWindowSink`) — opening it twice focuses the same window (no duplicate); new log lines auto-scroll; clicking X hides it but logging continues; reopen → buffered history flushed, live entries continue; `-debug` command-line flag opens it alongside the main window; app exit → no leak errors.
- [ ] **Log files** — a rolling daily file sink in the local app data folder (7 days retained) is written; trigger an error → `error.log` contains environment/exception details.
- [ ] **Bug report sink** (`BugReportApiSink`) — with the API reachable, logs are deleted after a successful submit; unreachable → `critical_error.log` written; a burst of warnings doesn't grow unbounded (100-cap).

## 13. Misc services

- [X] **Help text** (`HelpUserService`, `HelpUserManager`) — Edit System help pane for e.g. "SNES", "Mame", "PSX1": text, bold/headings, clickable links; unknown system → "No details available"; delete `parameters.md` → "file is missing" dialog; empty → "empty" dialog; no `##` headers → "no valid systems" dialog.
- [ ] **Config persistence (failure paths)** — read-only `system.xml` → 3 retries then "failed to save" logged; no `.tmp` file left behind; concurrent saves from two windows don't corrupt the file.
- [ ] **MAME data** (`MameDataService`) — with a valid `mame.dat`, MAME games show descriptions; deleted `mame.dat` → startup dialog, app still runs; corrupt/0-byte file → graceful failure.
- [ ] **Dialog services / file pickers / dispatcher** (`WpfMessageDialogService`, `WpfFilePickerService`, `WpfDispatcherService`, `WpfResourceProvider`, `WpfWindowContext`) — browse-for-folder/exe dialogs populate fields; cancel returns null cleanly; dialogs show correct icon/buttons and Yes/No/OK return values; language switch resolves all strings (missing key → key text, no deadlock); no cross-thread exceptions during downloads.
- [ ] **Image loading fallback** (`WpfImageLoader`) — missing/corrupt cover → `images\default.png` shown; delete the default → "default image not found" dialog; long paths (>260 chars) load.

---

## Appendix A — Orphaned code (no runtime path, no manual test needed)

- `ConvertChdToCueBin`, `ConvertChdToIso`, `ConvertDiscImageToIso` (`SimpleLauncher\Services\Converters\`) — static duplicates of `DiscConverter`; nothing in the app calls them. Only a code-cleanup check is needed (compile, remove or redirect).
- `Point`, `Rectangle` (P/Invoke structs in `WindowScreenshot`) — exercised via the screenshot flow in section 4.

## Appendix B — Coverage summary

- Covered by unit tests (do **not** re-test manually): settings & system manager persistence, favorites, play history, game scanner core, file finder, search orchestrator, launch strategies (default, DOSBox, Commander Genius, CHD/CUE, PBP, XISO, ZIP), mount-strategy matching, Core-side emulator config-injection services, models/DTOs, path/URL/sanitizer/pagination/filter helpers, RetroAchievements manager/matcher/hasher, Steam VDF parser, update-check, API connectivity, converters' strategy classes — plus the recently added: parameter resolver API service, `system.xml` writer + emulator XML helpers, game file watcher, loading overlay, UI reset, status bar, menu check-marks, credential protector (DPAPI), system-selection ViewModel, search-result model, default-folder/temp/missing-file services.
- Not covered and listed above: all WPF windows/Views/ViewModels, UI services, app lifecycle, per-emulator launch handlers, live file operations (extraction, mounting, conversion tools), platform scanners, downloads, RA API layer, gamepad/audio, debug/bug-report pipeline.
