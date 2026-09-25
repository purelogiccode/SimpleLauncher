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

---

## 1. Startup & app lifecycle

- [ ] **First-run flow** (`StartupInitializationService`) — with an empty config, the app auto-scans the machine for installed games (Steam, Epic, GOG, Microsoft Store, etc.) and creates the "Microsoft Windows" system; the Easy Mode welcome prompt appears only if that scan finds no games; with an existing config it loads straight into the main window.
- [ ] **Closing behavior** (`MainWindow.CloseWindowEvents`) — close the app then immediately check the settings file: close is deferred until settings are saved. Close while a CHD mount or scan is active → child processes killed, no crash.
- [ ] **Minimize to tray** — minimize hides the window from the taskbar, tray icon remains; tray menu has Open / Minimize to Tray / Debug Window / Exit; double-click the tray icon restores the window; Exit fully quits (icon disappears).
- [ ] **Quit / Restart** (`QuitSimpleLauncher`) — menu quit exits; restart (`--restarting`) starts a new process and exits the old one; failed restart shows "FailedToRestart" and the app stays alive.
- [ ] **Reinstall / Updater** (`ReinstallSimpleLauncher`) — with and without a local `Updater.exe`, with and without network, and with access denied (error 5): correct message box in each case; app closes after the updater launches; no zombie processes.
- [ ] **Update flow** (`ShutdownForUpdateAsync`) — with GitHub reachable, a fresh `Updater.exe` is downloaded and launched with the current PID, app exits; with GitHub unreachable, graceful failure.
- [ ] **System information display** (`DisplaySystemInformation`) — select a system with a bad ROM folder → red "System Folder" line + "path is not valid" dialog; bad emulator exe → red emulator line; all valid → no dialog; the error list shows every problem.
- [ ] **System config load** (`SystemConfigurationService`) — edit `system.xml` adding a valid system → it appears after restart; malformed XML → error dialog + logs, app continues.

## 2. Main window UI

- [ ] **Theme menu** (`ThemeMenuService`) — pick Light/Dark/Adaptive/HighContrast/Midnight → UI restyles instantly; accent colors apply; checkmark tracks selection; restart → theme persisted.
- [ ] **Language menu** (`LanguageMenuService`) — switch language → notification sound, status "Changing language...", app restarts into that language; checkmark shows current language.
- [ ] **View/Display menu options** — change thumbnail size, games-per-page, show-games, aspect ratio, filename display, font sizes, view mode → the grid re-renders accordingly; restart → checkmarks restored from settings.
- [ ] **Menu actions** (`MenuActionHandlerService`) — every menu item opens the right window / performs the right action with status-bar feedback: Easy/Expert mode, Download Image Pack, Scan for store games, Edit links, Gamepad toggle, Dead zone, fuzzy matching toggle/threshold, annotation stripping, Support, Donate. Rapid clicking → no double handling.
- [ ] **Filter bar** (`FilterMenu`) — A–Z / # / All letters filter the game list with button highlight; "All" resets; keyboard arrows/Home/End navigate the letter buttons; notification sound on click.
- [ ] **Status bar** — actions (sorting, launching, saving) show status text that auto-clears after the timeout.
- [ ] **Gamepad navigation** — see section 10.

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

- [ ] **F8 hotkey** (`GlobalHotkeyService`) — with another app foreground, F8 saves a screenshot of that window to `.\screenshot`; launching a second app instance → F8 conflict logged, no crash; after exit the hotkey is released (F8 behaves normally elsewhere).
- [ ] **Screenshot flow** (`ActiveWindowScreenshotService`, `WindowSelectionDialogWindow` + VM, `WindowManager`, `FlashOverlayWindow` + VM) — right-click a game → screenshot flow: the window-selection dialog lists all titled top-level windows (not hidden launcher windows); selecting one closes the dialog and captures it; shutter sound + single full-screen white flash (~0.6 s); minimized target → "cannot screenshot a minimized window" message; capture of a normal window saves a PNG with a timestamp name and refreshes the game button image; Cancel aborts silently.
- [ ] **[Integration] Window edge cases** (`WindowManager`) — UWP/minimized-to-tray windows are excluded from the list; no crash when enumerating unusual windows.

## 5. Easy Mode & Edit System

### Easy Mode wizard (`EasyModeWindow`)
- [ ] With zero systems configured → welcome prompt opens Easy Mode; system dropdown sorted, only systems with download links listed.
- [ ] Selecting a system enables emulator/core/image-pack buttons only when a download link exists; "Add System" stays disabled until the required emulator/core is downloaded or already on disk.
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
- [ ] **Set Fuzzy Matching** (`SetFuzzyMatchingWindow` + VM) — slider snaps to 5% ticks (70–95%), shows percentage; Cancel doesn't save; Save persists; restart → value retained and matching behaves accordingly.
- [ ] **Set Gamepad Dead Zone** (`SetGamepadDeadZoneWindow` + VM) — X/Y sliders; Save → confirmation box; Revert → defaults restored and window closes; with a gamepad connected, verify stick drift is filtered per the new dead zone.
- [ ] **Set Links** (`SetLinksWindow` + VM) — blank URLs fall back to defaults on save; custom template used from the context menu; Revert restores appsettings defaults.
- [ ] **Sound Configuration** (`SoundConfigurationWindow` + VM) — toggle enable → controls enable/disable; Choose File copies an MP3 into `audio\`; Play previews it; Play with sounds disabled → info box; Reset → `click.mp3`; Save persists; a notification (e.g. game launch) then uses the configured sound.
- [ ] **About / Update History / Update Log** (`AboutWindow`, `UpdateHistoryWindow`, `UpdateLogWindow` + VMs) — About shows the correct version; "Check for Updates" disables while running, offline → friendly error, button re-enables; Update History renders `WhatsNew.md` markdown with clickable links; with `WhatsNew.md` deleted → "not found" message; during an update install the log window appends timestamped lines live without freezing.
- [ ] **Support window** (`SupportWindow` + VM) — empty form → per-field validation; valid form → overlay "Sending support request...", form clears on success; emergency return button on the overlay works; closing mid-send doesn't crash; failure → error box + log entries.
- [ ] **Image viewer** (`ImageViewerWindow` + VM) — each artwork context-menu item renders; missing/corrupt file → error box, empty window, no crash; remote URL (RA badge) renders; unreachable URL → blank window, no hang.
- [ ] **ROM History window** (`RomHistoryWindow` + VM) — MAME game with a `history.dat`/`history.xml` entry shows text with clickable URLs; no entry → "No ROM history found…" + Yes/No prompt; Yes opens a Google search; both files deleted → friendly "no history file found" message.
- [ ] **DOSBox file selection** (`DosBoxFileSelectionWindow` + VM) — a DOS game folder with several `.conf/.bat/.exe/.com` files shows the picker with relative subfolder labels; single-click + Launch and double-click both launch the chosen file; Cancel/X → launch aborted silently; files in the base folder show no subfolder text.
- [ ] **System selection** (`SystemSelectionWindow`) — appears when a game's system can't be auto-matched; current guess pre-selected; confirm with no selection does nothing; Cancel returns false.

## 6. Emulator launch & config injection **[Integration]**

### Launch-time handlers (21 emulators — `Services\GameLauncher\Handlers\`)
Common flow for **each** emulator:
- [ ] Add a real ROM + point the launcher at a real emulator install; launch with "Show settings before launch" **off** → the emulator's own config file is rewritten with SimpleLauncher's settings and the game boots.
- [ ] Launch with "Show settings before launch" **on** → the injection dialog appears; "Run" launches, "Cancel" does not launch.
- [ ] Wrong/missing emulator path → the game still attempts launch without crashing; check the Debug log for injection errors.

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

- [ ] **Extraction before launch** (`ExtractionService`) — 7z/ZIP/RAR ROM launches: multi-file archives, archives locked by antivirus (10×1 s retry), insufficient disk space, corrupted archive → 7za fallback (`tools\SevenZip\`) or failure box + partial cleanup, crafted zip-slip archive → "PotentialPathManipulation" box.
- [ ] **CHD→CUE/BIN and PBP→CUE/BIN** (`DiscConverter`) — launch a CHD game on a CUE/BIN-only emulator → temp `.cue/.bin` in `%TEMP%\SimpleLauncher`, game boots, temp files cleaned after exit; same for PBP (PS1); corrupt file → error, no hang; huge files → 5-minute timeout path.
- [ ] **RVZ/WBFS/GCZ→ISO** (via RetroAchievements hasher, `DiscConverter.ConvertToIsoAsync`) — GameCube/Wii game hashed → converted with `DolphinTool.exe`, temp ISO deleted afterwards.
- [ ] **CHD mount** (`MountChdDrive`) — PSX game mounted via CHDMounter: exit game → mount process killed, drive letter disappears within ~20 s; kill CHDMounter externally while the game runs → unmount still cleans up; Dokan not installed → "Dokan driver not found" box.
- [ ] **ISO mount** (`MountIsoFiles`) — PS3 ISO with `EBOOT.BIN` launches and the drive is dismounted after exit; ISO without EBOOT.BIN → error box + dismount; PowerShell execution-policy restricted → `UnabletomountIsOfile` box; 30 s timeout kill.
- [ ] **XISO mount** (`MountXisoDrive`, `MountXisoFiles`) — original-Xbox XISO launches via Dokan; virtual drive letter (picked Z→D) is released after exit; missing `tools\SimpleXisoDrive\*.exe` → mount-error box; no free drive letter → error box; wrong XISO layout → timeout + error, no leaked process.
- [ ] **External tools** (`ExternalToolLauncherService`) — run each tool from the UI: Create Batch Files (PS3/ScummVM/Windows/Xbox 360 XBLA), Batch Convert ISO→XISO, →CHD, →Compressed, →RVZ, Rom Validator, Find Rom Cover, Retro Game Cover Downloader — launches with correct folder/args; missing tool → "not found" box; UAC cancel (error 1223) → "canceled" box; corrupt exe → PE check rejects it.

## 8. Game file watcher (5.6.0) — end-to-end flow

These checks cover the watcher's behavior through the running app (end to end):

- [ ] **Auto-refresh on external changes** (`GameFileWatcherService`) — with a system selected, add/delete/rename a ROM in its folder → the game list refreshes once ~500 ms after the change (not per file during a batch copy/extract).
- [ ] Changes in a **non-selected** system's folder are ignored.
- [ ] Watching starts/stops when switching systems; closing the app unsubscribes all watchers (no exceptions in the debug log).

## 9. Platform game scanners **[Integration]**

Run "Scan for store games" after installing 1–2 real games per store. Verify per platform: shortcut created with correct name/protocol, cover image downloaded, DLC/tools (UE, redistributables) skipped, and re-running the scan is idempotent (no duplicate/corrupted shortcuts when two stores ship the same game name).

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

- [ ] **Gamepad navigation** (`GamePadController`) — with Enable GamePad Navigation on: Xbox pad left stick moves the cursor, A = left click, B = right click, right stick scrolls; PS pad (DirectInput) behaves the same; unplug/replug → reconnects within ~5 s; dead-zone sliders filter stick drift; disabling the setting stops input; exiting the app with a pad connected → no crash.
- [ ] **Sounds** (`PlaySoundEffects`, `AudioInputService`) — click/notification/shutter/trash sounds play; disabling sounds in settings → silent; custom notification file plays; missing file → logged, no crash; rapid clicks → previous sound stops (no overlap, no leak).

## 11. RetroAchievements (app layer) **[Integration]**

- [ ] **Login & API** (`RetroAchievementsService`) — valid credentials → session token; wrong password → silent failure; wrong API key → unauthorized handled as an error dialog; offline → graceful nulls with logged errors; RA windows show correct progress %, points (hardcore vs softcore), badges, rank/score tables; completion-progress pagination works.
- [ ] **Credential protection** — saved RA/DuckStation credentials are stored encrypted (not plaintext); restart → credentials still load; moving settings to another user/machine → graceful null.
- [ ] **Per-game window** (`RetroAchievementsForAGameWindow`, `RaAchievement`) — locked vs unlocked badges, 🏆 hardcore icon, "Hardcore/Casual/Not Earned" labels, rarity "X.X% hardcore", "Unknown" author fallback; remote badge URLs render (unreachable → blank, no hang).
- [ ] **RA settings window** (`RetroAchievementsSettingsWindow` + VM) — save credentials → fields pre-filled on reopen; "Configure Emulator" writes login/token into the emulator config and reports success/failure; wrong password → clear error; restart → credentials retained.
- [ ] **Emulator configurator** (`RetroAchievementsEmulatorConfiguratorService`) — for each supported emulator (RetroArch, PCSX2, DuckStation + encrypted token, PPSSPP + `.dat` session file, Dolphin, Flycast, BizHawk JSON): save credentials then verify the keys (username/token/hardcore) in the emulator's config; delete config → restored from `samples\`; read-only config → false + log, no crash.
- [ ] **Hashing flow** (`RetroAchievementsHasherTool`) — RA icon on: (a) NES ROM → header-strip hash via the bundled RetroAchievementsSharp CLI tool, no dialog; (b) unknown-named system → system picker with pre-selected guess, cancel → "System selection cancelled"; (c) zipped PS1 game → hashed through the CLI (no extraction, no temp files left), temp cleaned; (d) GameCube `.rvz` → hashed directly (no ISO conversion, no temp ISO); (e) unsupported system (e.g. C64) → no RA icon shown.

## 12. Debug window & logging

- [ ] **Debug window** (`DebugWindow`, `DebugWindowSink`) — opening it twice focuses the same window (no duplicate); new log lines auto-scroll; clicking X hides it but logging continues; reopen → buffered history flushed, live entries continue; `-debug` command-line flag opens it alongside the main window; app exit → no leak errors.
- [ ] **Log files** — a rolling daily file sink in the local app data folder (7 days retained) is written; trigger an error → `error.log` contains environment/exception details.
- [ ] **Bug report sink** (`BugReportApiSink`) — with the API reachable, logs are deleted after a successful submit; unreachable → `critical_error.log` written; a burst of warnings doesn't grow unbounded (100-cap).

## 13. Misc services

- [ ] **Help text** (`HelpUserService`, `HelpUserManager`) — Edit System help pane for e.g. "SNES", "Mame", "PSX1": text, bold/headings, clickable links; unknown system → "No details available"; delete `parameters.md` → "file is missing" dialog; empty → "empty" dialog; no `##` headers → "no valid systems" dialog.
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

---

# GUI testing runbook (Avalonia / Linux VM): AT-SPI navigation + vision checks

How to run GUI checks automatically: the harness drives the app through its **AT-SPI accessibility
tree** (find by name/AutomationId, click, expand, read state) and uses a vision-capable LLM only for
*visual* verdicts (rendering, palettes, text legibility). The WPF checklist above is the source of
scenarios; for the **Avalonia/Linux** build use `docs/manual-tests.md` (same list, with Avalonia/Linux
items marked). This runbook describes the tooling built on 2026-09-25 and how to resume it in a new
session. **For a different machine, follow `docs/gui-test-harness.md`** — the portable manual with
prerequisites, harness file inventory, configuration values to adapt, the fixture contract, the
`atspi_nav` API reference, scenario authoring rules and troubleshooting.

## 1. Environment

| Item | Value |
|---|---|
| VM | Hyper-V `LinuxMint` (Gen 2, 4 vCPU, 8 GB RAM, 80 GB VHDX on `D:`) |
| Guest OS | Linux Mint 22.3 Cinnamon (X11, LightDM **autologin** as `vm`) |
| Login | user `vm`, password `vm` (sudo password `vm`) |
| Guest IP | DHCP (was `172.24.166.133`) — re-check after VM reboot |
| App | `~/SimpleLauncher/SimpleLauncher.Avalonia` (self-contained `linux-x64` publish) |
| Fixture | `fixture.py` seeds `~/.local/share/SimpleLauncher/settings.dat` (SQLite): systems Test System / Second System / Broken System, ROMs in `~/roms/<System>`, covers in `~/images/<System>` |
| Dummy emulator | `/home/vm/dummy-emulator.sh` (logs its args to `/tmp/dummy-emulator.log`, sleeps 7 s — play history needs >5 s) |
| Display | `:0`, `XAUTHORITY=/home/vm/.Xauthority` |
| Host harness | `scripts\gui-test-harness\` (`ask_vision.py`, `atspi_nav.py`, `fixture.py`, `lib.ps1`, `run-suite.ps1`) — tracked in git; paths derive from `$PSScriptRoot` |
| Guest a11y deps | `at-spi2-core`, `python3-pyatspi` (already present on Mint 22.3); `wmctrl` for maximize; `at-spi-bus-launcher` auto-starts with the session |
| Shots / reports | `scripts\gui-test-harness\shots\`, `scripts\gui-test-harness\reports\` (gitignored evidence) |
| Host prereqs | PowerShell 7 + `Posh-SSH`, Python 3.14, user env var `OPENROUTER_API_KEY` (`sk-or-...`) |
| Model | `xiaomi/mimo-v2.6-flash` on OpenRouter (accepts image input; 1M context) |

## 2. Pipeline logic

```
run-suite.ps1 scenario
  -> one cached SSH session (Posh-SSH) per run; atspi_nav.py + fixture.py deployed via SCP
  -> the app is restarted whenever the scenario's Fixture changes (fixture.py writes the
     unified SQLite settings.dat while the app is stopped)
  -> scenario = a Python script (heredoc) that:
       * connects to the app accessibility tree (pyatspi)
       * navigates / asserts deterministically (print "SL_RESULT: {...}")
  -> gnome-screenshot inside guest (only for visual scenarios)
  -> SCP the PNG to scripts\gui-test-harness\shots
  -> if the scenario has a Prompt: POST base64 image to OpenRouter and parse
     the first line "VERDICT: PASS|FAIL"
  -> final verdict = deterministic AND vision (when vision is used)
  -> append both evidences to reports\run-<timestamp>.md
```

The key is never written to disk or logs by the harness: `lib.ps1` reads it from the user env var via
`[Environment]::GetEnvironmentVariable('OPENROUTER_API_KEY','User')` (a new shell started after the
variable was set still sees it that way, even though an already-running process does not).

## 3. Resume / recovery steps (new session)

1. Check the VM: `Get-VM LinuxMint` (elevated) or Hyper-V Manager. If off: `Start-VM LinuxMint` (elevated).
   When resuming non-elevated management, log off/on once if the account was just added to
   `Hyper-V Administrators`; otherwise run Hyper-V cmdlets and `vmconnect.exe` elevated.
2. Open the console: `vmconnect.exe localhost LinuxMint` (elevated if needed).
3. Find the guest IP: `arp -a` / `Get-NetNeighbor` on `vEthernet (Default Switch)` (host is `172.24.160.1/20`),
   or read it from the VM console with `hostname -I`.
4. Test SSH (expects port 22 open) and the app process:
   ```powershell
   Import-Module Posh-SSH
   $cred = [pscredential]::new('vm', (ConvertTo-SecureString 'vm' -AsPlainText -Force))
   $s = New-SSHSession -ComputerName '<guest-ip>' -Credential $cred -AcceptKey
   Invoke-SSHCommand -SessionId $s.SessionId -Command 'pgrep -af SimpleLauncher.Avalonia || echo NOT-RUNNING'
   ```
   If the harness hardcodes the old IP, update `$script:VmHostAddress` in `scripts\gui-test-harness\lib.ps1`.
5. Relaunch the app if needed:
   ```bash
   cd ~/SimpleLauncher
   setsid env DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority ./SimpleLauncher.Avalonia \
     >/tmp/simplelauncher.log 2>&1 < /dev/null &
   ```
6. The console resolution resets to 1024x768 after a guest reboot; re-apply 1080p:
   ```bash
   DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority xrandr --output Virtual-1 --mode 1920x1080
   ```
   (Or make it permanent on the host with `Set-VMVideo -VMName LinuxMint -HorizontalResolution 1920 -VerticalResolution 1080`.)
7. Run the suite (below).

## 4. Running the suite

```powershell
# all scenarios
pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1

# only some scenarios (comma-separated; also works with -Only A,B from -File)
pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1 -Only MENU-01,EASY-01

# different model
pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1 -Model 'google/gemini-3.8-flash'
```

Ad-hoc check without touching the suite:
```powershell
. scripts\gui-test-harness\lib.ps1
$shot = Save-VmShot -Name 'adhoc'
Invoke-VisionCheck -Image $shot -Prompt 'Expected ... First line: VERDICT: PASS or VERDICT: FAIL'
```

## 5. Scenario anatomy (`run-suite.ps1`) and the AT-SPI nav layer

Each scenario is a PowerShell hashtable:

| Field | Meaning |
|---|---|
| `Id` / `Name` | Report identity |
| `Py` | Python script executed in the guest. Imports `/home/vm/vision/atspi_nav.py`, navigates and asserts, prints one `SL_RESULT: {"ok":bool,"checks":{...},"extra":{...}}` line |
| `Shot` | Optional screenshot base name (PNG lands in `shots\`); taken after the script leaves the UI in the asserted state |
| `Prompt` | Optional vision question; omit for purely deterministic scenarios (no LLM call) |
| `Fixture` | Guest data state applied before the scenario: `empty` (no systems, first-run Welcome) or `seeded` (default; 3 systems from `fixture.py`). Changing it restarts the app |
| `Restart` | Force an app restart before the scenario (THEME-02 verifies theme persistence) |
| `LaunchArgs` | Extra app arguments (DEBUG-01 uses `-debug`); changing it restarts the app |

Verdict rules in the harness: the deterministic part must PASS (`ok == true`, at least one check, no
`exception` in `extra`); when a `Prompt` exists, the vision `VERDICT` must also be PASS.

Prompt contract (parsed by `Get-SuiteVerdict`):
```
First line of your answer must be exactly "VERDICT: PASS" or "VERDICT: FAIL".
Then report ... and any mismatch. Be brief.
```

### AT-SPI navigation layer (`atspi_nav.py`)

Avalonia 12.1 starts its AT-SPI server unconditionally on X11, so the app tree is available without
changing the app. Key API (all handles are re-resolved on every call — cached peers go stale after
layout changes):

| Method | Purpose |
|---|---|
| `snapshot()` / `find(id=,name=,role=,contains=)` / `wait_for(...)` | Tree walk + lookup (AutomationId falls back to `x:Name`) |
| `click(entry)` / `toggle` / `expand` | Native action when available (IInvoke/IToggle/IExpandCollapse); otherwise click at live extents |
| `open_menu("Options")`, `popup_items()`, `click_menu_item(name)` | Menu bar + popup navigation. Popup entries are the menu items below the `[menu]` node's bottom edge |
| `set_text`, `read_text`, `read_value` | TextBox / Slider interfaces |
| `activate_window`, `maximize_window`, `close_welcome`, `close_dialogs`, `ensure_ready` | Deterministic app state |
| `press(keys)` | xdotool fallback (Escape, etc.) |

Lessons baked into the library:
- **Menu items have no AT-SPI action** (`MenuItemAutomationPeer` implements only `IToggleProvider`), so
  they are clicked via their extents after activating the window.
- **First popup item matters:** bounds are the menubar bottom, not "top-level items < y+40" (the first
  item sits ~4 px below the bar). Use the `[menu]` node extents.
- **Duplicated names:** a submenu item can repeat its parent's name (Sound Configuration); the rightmost
  match is the flyout entry. Click the parent once to open the flyout, then click again.
- **Modal dialogs:** the first-run `Welcome` dialog is modal; dismiss it (`No`) before clicking. Menus,
  Easy Mode and dialogs stay usable afterwards (see BUG-02 for the main-content caveat).
- **One assertion per scenario** and prefer tree assertions (names, enabled/disabled via
  `pyatspi.STATE_ENABLED`, sorted item lists) over screenshots; keep vision for visual-only claims.
- **Reasoning model:** `xiaomi/mimo-v2.6-flash` spends tokens on hidden reasoning. If `content` comes
  back empty, raise `--max-tokens` (lib.ps1 default is now 8000).
- **Interference:** Mint Update may pop up or run an `apt` upgrade; it starves the app and the AT-SPI
  tree degrades. Wait for `apt`/`dpkg` to finish and restart the app before trusting a red run.
- **Evidence:** keep every screenshot; the report references it.

### Legacy coordinate map (fallback only)

Kept for ad-hoc xdotool fallbacks and for interpreting old screenshots (app maximized, 1920x1080):
menu bar `y=44`: Options `x=45`, Edit System `x=112`, Select Window `x=211`, Donate `x=309`,
About `x=370`. Window handle: `xdotool search --name "^Simple Launcher$"`. Extents from the AT-SPI
tree are authoritative and layout-independent — prefer them.

## 6. Scenario inventory and current status

Last runs 2026-09-25 (subsets, `scripts\gui-test-harness\reports\run-<timestamp>.md`): the original 7
scenarios were green; the suite was then extended to **31 scenarios** covering the Linux-applicable
checklist (sections 1-13 of `docs/manual-tests.md`) using the seeded fixture. Current status:

| Id | Covers | Fixture | Status (2026-09-25) | Notes |
|---|---|---|---|---|
| EASY-01 | Welcome -> Easy Mode; dropdown populated + sorted; Add/Download disabled | empty | PASS (7/7 run) | re-verify in the full pass |
| EASY-02 | Selecting "Atari 2600" enables Download Emulator/Core; Add System stays disabled | seeded | PASS | |
| MENU-00 | Main window renders; menu bar exact; no Windows-only Tools | seeded | PASS (7/7 run) | re-verify in the full pass |
| MENU-01 | Options menu exact 14 items; no "Inject Emulator Config" | seeded | PASS (7/7 run) | re-verify in the full pass |
| MENU-02 | About menu exact 4 items | seeded | PASS (7/7 run) | re-verify in the full pass |
| DIALOG-01 | Options > About opens the About window with a version string | seeded | PASS (7/7 run) | re-verify in the full pass |
| DIALOG-02 | Options > Sound Configuration > flyout > window | seeded | PASS (7/7 run) | two-click submenu sequence |
| DIALOG-03 | Edit System menu exact set; Add New System opens Easy Mode | seeded | PASS (7/7 run) | re-verify in the full pass |
| SYS-01 | 3 systems load; grid shows 4 Test System games; covers/placeholders | seeded | PASS | vision checks covers and "1 to 4 out of 4" |
| SYS-02 | Broken System -> "Errors" dialog with invalid folder/image paths | seeded | not run yet | |
| FILTER-01 | All shows all; "B" filters to Beta Blaster; status "Filtering by B" | seeded | PASS | |
| SEARCH-01 | "Alpha" -> 1; "zzz" -> "No Games Found" + 0 total; clear restores | seeded | PASS | search triggers on Return |
| VIEW-01 | Toggle grid -> list (GameDataGrid) | seeded | det PASS | vision prompt fixed (was expecting Sonic); re-run |
| VIEW-02 | View Mode menu checkmarks track selection; grid restored | seeded | PASS | |
| THEME-01 | Base Theme checkmark; switch to Dark; value persisted in settings.dat | seeded | PASS | |
| THEME-02 | Dark persists across restart; switch back to Adaptive | seeded + Restart | PASS | |
| EDIT-01 | Edit System window opens with the selected system loaded | seeded | OPEN | Help control lookup + vision prompt |
| HELP-01 | Edit System help pane shows content ("No information available..." for Test System) | seeded | OPEN | Help control lookup |
| DIALOG-04 | Threshold slider 80 -> 90 persisted, restored to 80 | seeded | PASS | |
| DIALOG-05 | Gamepad dead zone dialog: two sliders + Cancel | seeded | PASS | |
| DIALOG-06 | Edit Links window: 2 URL fields + Save/Revert/Cancel | seeded | PASS | |
| SOUND-01 | Enable toggle disables Choose file/Play, re-enable works | seeded | PASS | |
| ABOUT-02 | About version string + Update History renders WhatsNew.md | seeded | not run yet | |
| SUPPORT-01 | Empty Send shows the "enter the details" validation | seeded | not run yet | |
| FAV-01 | Add To Favorites via context menu; Favorites page lists Alpha Quest | seeded | not run yet | |
| FAV-02 | Remove From Favorites via context menu; DB empty | seeded | not run yet | |
| HIST-01 | Launch (>5 s) records PlayHistory; Play History page shows it | seeded | not run yet | |
| ZIP-01 | ZIP launch extracts to `/tmp/SimpleLauncher`; temp cleaned after exit | seeded | not run yet | |
| WATCH-01 | File watcher refreshes the count on external add/remove | seeded | not run yet | |
| DEBUG-01 | `-debug` opens the Debug Window; daily log files exist | seeded + `-debug` | not run yet | |
| ROMHIST-01 | Open ROM History on a non-MAME game -> no-history message | seeded | not run yet | |

### Open items for the next session

1. **EDIT-01 / HELP-01**: the Help control is an unnamed button with a `? Help` label (AT-SPI shows
   the label only). Change both scenarios to find the label (`find(name="Help", contains=True)`, any
   role) and click its extents. Also relax EDIT-01: the Edit System window shows no System Image text
   field (only "Choose Image...") and no emulator section - update the vision prompt/checks to what is
   actually visible (System Name, System Folder, buttons, help pane).
2. **VIEW-01**: re-run with the fixed vision prompt (expects the 4 Test System games, not Sonic).
3. **First runs**: SYS-02, ABOUT-02, SUPPORT-01, FAV-01, FAV-02, HIST-01, ZIP-01, WATCH-01, DEBUG-01,
   ROMHIST-01 - iterate until green.
4. **Full pass**: `pwsh -NoProfile -File scripts\gui-test-harness\run-suite.ps1` (31 scenarios, ~40-60 min
   with vision), then update the `docs/manual-tests.md` checkboxes only for items the suite verified.
5. **Coverage gap (manual/integration by design)**: store scanners, emulator downloads, config
   injection, RA API/login, gamepad hardware, CHD/ISO/XISO mounting, external tools, updater,
   Commander Genius - keep as manual (Linux hides most of them anyway; LB-08/LB-14/LB-22 already
   verified by code/tests).

### Resume checklist (next session)

1. Start the VM (`Start-VM LinuxMint`, elevated); re-check the DHCP IP with
   `Get-NetNeighbor -InterfaceAlias 'vEthernet (Default Switch)'` and update
   `$script:VmHostAddress` in `scripts\gui-test-harness\lib.ps1` if it changed.
2. After a guest reboot re-apply 1080p:
   `DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority xrandr --output Virtual-1 --mode 1920x1080`.
3. Fix the open items above, then run subsets while iterating and finally the full suite.
4. Keep this section (and the AGENTS.md pointer) updated after every session; attach the report path
   and the coverage delta against `docs/manual-tests.md`.

## 7. Cost

One check = one image (~2.1-2.2k prompt tokens) + up to 4k completion tokens; observed cost
**USD 0.0004-0.0007** per scenario with `xiaomi/mimo-v2.6-flash`. A full 30-scenario pass is a few cents.

## 8. Known findings (vision runs)

- **BUG-01 - Adaptive base theme rendered a mixed dark/light UI (Linux/Avalonia). FIXED 2026-09-25.**
  Confirmed and fixed the same day on Linux Mint 22.3. `AvaloniaThemeService` now resolves `Adaptive`
  against the machine theme: Avalonia `PlatformSettings` on Windows/macOS, and on Linux the GTK preference
  (`org.gnome.desktop.interface color-scheme`, then the GTK theme name via `gsettings` for
  GNOME/Cinnamon/MATE/XFCE) because Avalonia's X11 backend reports Light for some desktops. Runtime
  changes re-apply the palette (`ActualThemeVariantChanged`, plus a 15 s poll while Adaptive is active on
  Linux). Regression tests: `SimpleLauncher.Avalonia.Tests/AvaloniaThemeServiceTests.cs`. Vision
  verification: light machine theme -> all light PASS, dark machine theme at startup -> all dark PASS,
  live dark->light switch while running -> all light PASS. Full report:
  `scripts\gui-test-harness\reports\BUG-adaptive-theme.md`.
- **BUG-02 (upstream, Avalonia 12.1) - main-content AT-SPI subtree collapses after the first modal dialog
  closes.** On Linux Mint the app exposes a full tree (350 nodes: nav rail, combos, slider, game grid,
  status) while the first-run `Welcome` dialog is up. Dismissing it (clicking No or pressing Escape)
  permanently drops `MainContentGrid`'s children from the tree (97 nodes remain; only the menu bar and
  the first nav button are exposed) even though the UI renders normally (evidence:
  `scripts\gui-test-harness\shots\state.png`). Menus, Easy Mode and every dialog window stay exposed, so the
  suite navigates via those; main-content verification stays with vision. Suspected cause: Avalonia's
  AT-SPI server holds a stale peer/root when the visual tree is rebuilt on dialog close
  (`X11AtSpiAccessibility` / peer re-registration). Restarting the app restores the tree, so scenarios
  that need main-content exposure must run before any modal is dismissed. Worth reporting upstream.
- **Accessibility instrumentation (2026-09-25).** Interactive controls in both apps now carry
  `AutomationProperties.Name` (and WPF inputs/DataGrids an `AutomationId`), so screen readers and the
  AT-SPI harness see real labels instead of `Avalonia.Controls.Image`. Guardrails:
  `SimpleLauncher.Avalonia.Tests/AvaloniaAccessibilityTests.cs` and
  `SimpleLauncher.Tests/WpfAccessibilityTests.cs` fail the build when a new interactive control has no
  accessible name (buttons with literal text are exempt). Counts: Avalonia ~205, WPF ~320 annotations.
- **Fixture / unified DB (2026-09-25).** Systems, favorites, play history, settings and emulator
  configs live in the SQLite `settings.dat`. `fixture.py` seeds it while the app is stopped:
  `Systems.ConfigJson` is `SystemConfigData` JSON (e.g. `EmulatorLocation` =
  `/home/vm/dummy-emulator.sh`, `EmulatorParameters` = `"%ROM%"`). View mode and theme persist there
  too, so grid-dependent scenarios call `ensure_grid_view()`.
- **Play history needs >5 s** (`SimpleLauncher.Avalonia/Services/GameLauncher/LauncherService.cs:675`);
  the dummy emulator sleeps 7 s so launches record history. Play history/DataGrid rows are not exposed
  as named AT-SPI rows (only "DataGridRow" cells) - deterministic checks read the DB, vision confirms
  rendering.
- **Grid item peers lose their names after the first render** (clicking a letter/All rebuilds the
  containers; same Avalonia peer-staleness family as BUG-02). Assertions use `PaginationLabel` /
  `StatusLeft` / the DB / the emulator log; card interactions use fixed coordinates (1920x1080: first
  card after a letter filter is centred around (195, 330)).
- **Context menus** open as a `PopupRoot` frame whose `menu item`s ARE exposed; right-click must be
  done with xdotool at live extents (`Nav.right_click`). "Edit Links" and "Sound Configuration" need
  the two-click submenu sequence (parent opens a flyout with the same name).
- **Escape in the browser** returns to the system-selection screen; `Nav.open_system()` handles both
  states. The filter button "All" means "All Games" across systems, and a system's games only appear
  after that system was opened once in the session.
- **Search triggers on Return** (not the Search button); the no-match state shows "No Games Found"
  and pagination "0 to 0 out of 0". The filter status text is `Filtering by B` (not just `B`).
- **Accessibility gaps found (not fixed yet):** the system-selection cards are code-created buttons
  with a `StackPanel` content and no `AutomationProperties.Name` (AT-SPI name is
  `Avalonia.Controls.StackPanel`), and the Edit System Help button is unnamed (only a `? Help` label
  is exposed). `AvaloniaAccessibilityTests` scans XAML only, so code-created controls would need a
  separate guardrail if these are fixed.
