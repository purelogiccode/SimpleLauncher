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
| VM | Hyper-V `LinuxMint` (Gen 2, 8 vCPU, 8 GB RAM, 80 GB VHDX on `D:`) |
| Guest OS | Linux Mint 22.3 Cinnamon (X11, LightDM **autologin** as `vm`) |
| Login | user `vm`, password `vm` (sudo password `vm`) |
| Guest IP | DHCP `172.31.176.191` on the Default Switch (host `172.31.176.1/20`) — re-check after VM reboot |
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

Last full run 2026-09-27 (`scripts\gui-test-harness\reports\run-20260927-232933.md`): **33/33 scenarios
PASS in a single run** on the payload rebuilt from HEAD (`9f684574`, with the BUG-07..BUG-15 fixes) - Hyper-V
`LinuxMint` VM (Linux Mint 22.3, Avalonia `linux-x64` self-contained publish), covering the Linux-applicable
checklist (sections 1-13 of `docs/manual-tests.md`) with the seeded fixture. The same session live-verified
BUG-14/15 on the redeployed payload (see the session notes below). MAME-01/MAME-02 were added 2026-09-27 and
are included in the 33/33 pass; the earlier 31/31 pass is `reports\run-20260926-143336.md`.

The 2026-09-26 full pass had 31 scenarios (`reports\run-20260926-143336.md`); MAME-01/MAME-02 were added
2026-09-27 (`reports\run-20260927-014857.md`, `reports\run-20260927-014646.md`).

A first pass the same day (`reports\run-20260926-133836.md`) hit the known AT-SPI registration flake
during THEME-02's forced restart: 15 scenarios passed, then 16 failed with
`RuntimeError("system card 'Test System' not found")` while the a11y tree was empty; a later restart
restored the tree and DEBUG-01/ROMHIST-01 passed. The 14 failed scenarios were re-run once the tree was
healthy (`reports\run-20260926-135646.md`): **14/14 PASS**. `run-suite.ps1` now checks
`Test-VmNavHealth` after every restart, retries the restart once and aborts with a clear message if the
tree is still missing, instead of recording a block of false failures.

| Id | Covers | Fixture | Status (2026-09-27) | Notes |
|---|---|---|---|---|
| EASY-01 | Welcome -> Easy Mode; dropdown populated + sorted; Add/Download disabled | empty | PASS | waits for the config-loading overlay to clear |
| EASY-02 | Missing emulator (Sony PlayStation 4/shadPS4) enables Download Emulator; Add System + Download Core disabled | seeded | PASS | rewritten 2026-09-27: the VM now has RetroArch and every other Easy Mode emulator installed, so the old Atari 2600 expectation no longer applied; the dropdown is wheel-scrolled to reach PS4 |
| MENU-00 | Main window renders; menu bar exact; no Windows-only Tools | seeded | PASS | |
| MENU-01 | Options menu exact 14 items; no "Inject Emulator Config" | seeded | PASS | |
| MENU-02 | About menu exact 4 items | seeded | PASS | |
| DIALOG-01 | Options > About opens the About window with a version string | seeded | PASS | |
| DIALOG-02 | Options > Sound Configuration > flyout > window | seeded | PASS | two-click submenu sequence |
| DIALOG-03 | Edit System menu exact set; Add New System opens Easy Mode | seeded | PASS | |
| SYS-01 | 3 systems load; grid shows 4 Test System games; covers/placeholders | seeded | PASS | vision checks covers and "1 to 4 out of 4" |
| SYS-02 | Broken System -> "Errors" dialog with invalid folder/image paths | seeded | PASS | |
| FILTER-01 | All shows all; "B" filters to Beta Blaster; status "Filtering by B" | seeded | PASS | |
| SEARCH-01 | "Alpha" -> 1; "zzz" -> "No Games Found" + 0 total; clear restores | seeded | PASS | search triggers on Return |
| VIEW-01 | Toggle grid -> list (GameDataGrid) | seeded | PASS | |
| VIEW-02 | View Mode menu checkmarks track selection; grid restored | seeded | PASS | |
| THEME-01 | Base Theme checkmark; switch to Dark; value persisted in settings.dat | seeded | PASS | |
| THEME-02 | Dark persists across restart; switch back to Adaptive | seeded + Restart | PASS | |
| EDIT-01 | Edit System window opens with the selected system loaded | seeded | PASS | Help found via the `? Help` label; prompt expects only the visible fields |
| HELP-01 | Edit System help pane shows content ("No information available..." for Test System) | seeded | PASS | asserts the Developer Suggestion pane (the `? Help` button opens the wiki, not a pane) |
| DIALOG-04 | Threshold slider 80 -> 90 persisted, restored to 80 | seeded | PASS | |
| DIALOG-05 | Gamepad dead zone dialog: two sliders + Cancel | seeded | PASS | |
| DIALOG-06 | Edit Links window: 2 URL fields + Save/Revert/Cancel | seeded | PASS | |
| SOUND-01 | Enable toggle disables Choose file/Play, re-enable works | seeded | PASS | |
| ABOUT-02 | About version string + Update History renders WhatsNew.md | seeded | PASS | |
| SUPPORT-01 | Empty Send shows the field validation | seeded | PASS | first missing field -> "Please enter the name." |
| FAV-01 | Add To Favorites via context menu; Favorites page lists Alpha Quest | seeded | PASS | preview cover may be blank until the row is selected |
| FAV-02 | Remove From Favorites via context menu; DB empty | seeded | PASS | |
| HIST-01 | Launch (>5 s) records PlayHistory; Play History page shows it | seeded | PASS | DB stores the full ROM path (check uses the basename) |
| ZIP-01 | ZIP launch extracts to `/tmp/SimpleLauncher`; temp cleaned after exit | seeded | PASS | the base temp folder may remain as long as it is empty |
| WATCH-01 | File watcher refreshes the count on external add/remove | seeded | PASS | |
| DEBUG-01 | `-debug` opens the Debug Window (title "Debugger") with live log lines | seeded + `-debug` | PASS | reads the "Debug log" text box |
| ROMHIST-01 | Open ROM History on a non-MAME game -> no-history message | seeded | PASS | |
| MAME-01 | Arcade system (real MAME ROMs) shows machine descriptions and the sort toggle | arcade | PASS | added 2026-09-27; `run-20260927-014857.md` |
| MAME-02 | ROM History shows the real MAME entry and machine description | arcade | PASS | added 2026-09-27; `run-20260927-014646.md` |

### Easy Mode real-install session (2026-09-26, ad-hoc)

Driven through the AT-SPI harness on the same VM (seeded fixture, then systems added through Easy
Mode; evidence in `scripts\gui-test-harness\shots\`). Real downloads / real ROMs stay manual by
design, so this was an ad-hoc session, not suite scenarios:

- **EASY-PSP (PPSSPP, single-file AppImage):** Easy Mode -> "Sony PSP" -> **Download Emulator**
  downloaded `PPSSPP-v1.20.4-anylinux-x86_64.AppImage` (48 MB) to `~/SimpleLauncher/emulators/PPSSPP/`,
  set the execute bits, showed the success dialog and enabled **Add System**. Add System created the
  system folders (`roms/Sony PSP`, `images/Sony PSP`) and the DB row.
- **IMGPACK-PSP:** Easy Mode -> **Download Image Pack 1** downloaded `sonypsp.zip` (195 MB) and
  extracted **601 covers** into `images/Sony PSP/`, including one matching the real ISO
  (`007 - From Russia with Love (USA).png`).
- **GRID-PSP:** copied the real `007 - From Russia with Love (USA).iso` (732 MB) from `D:\Samples`
  plus 12 dummy `.iso` files named after real pack covers; the grid rendered **13/13 covers**
  (vision PASS, `shots\EASY-PSP-grid13.png`), pagination "1 to 13 out of 13".
- **LAUNCH-PSP:** Launch Game started PPSSPP with the ISO; the game rendered its EA copyright boot
  screen (vision PASS, `shots\EASY-PSP-boot150.png`); play history recorded
  (`TimesPlayed=1`, `TotalPlayTime=214` s).
- **EASY-DC (Redream, tar.gz archive):** the first attempt failed with "The selected file cannot be
  extracted. To extract a file, it needs to be a 7z, zip, or rar file." -> **BUG-03**, fixed the
  same day (§8). After deploying the fix, Download Emulator extracted `emulators/Redream/redream`
  with execute bits and Add System worked.
- **LAUNCH-DC:** copied the real `18 Wheeler - American Pro Trucker (USA)` cue/bins; Launch Game
  started Redream (window + game tile rendered, vision-confirmed `shots\EASY-DC-redream2.png`).
  Redream exits with code 1 without a Dreamcast BIOS, so the app showed its error + AI-suggestion
  dialogs; after dismissing them the play history recorded (`TimesPlayed=1`, `TotalPlayTime=75` s).
- Not exercised: **Stop** mid-download, network loss mid-download, custom ROM folder picker, the
  separate Options -> Download Image Pack window.

### Easy Mode emulator/core install sweep (2026-09-26, ad-hoc)

Every distinct emulator download and the shared RetroArch core in the Linux Easy Mode manifest were
exercised through the UI (select system -> **Download Emulator/Core** -> wait for the dialog ->
verify the installed path and its execute bit). After the three fixes below: **15/15 emulators and
1/1 core install correctly**.

| System used | Emulator | Format | Result | Notes |
|---|---|---|---|---|
| Microsoft Xbox | Xemu | AppImage | PASS | |
| Nintendo 3DS | Azahar | AppImage | PASS | |
| Sony PlayStation Vita | Vita3K | AppImage | PASS | |
| Sony PSP | PPSSPP | AppImage | PASS | also launched a real ISO (above) |
| Sony PlayStation 3 | RPCS3 | AppImage | PASS | |
| Sony PlayStation 2 | PCSX2 | AppImage | PASS | |
| Sony PlayStation 1 | DuckStation | AppImage | PASS | |
| Sega Model 3 | Supermodel | tar.gz | PASS | BUG-03 fix |
| Sega Dreamcast | Redream | tar.gz | PASS | BUG-03 fix; real game launched (above) |
| Microsoft MSX | OpenMSX | zip | PASS | BUG-04 fix (exec bits after archive install) |
| Nintendo WiiU | Cemu | zip | PASS | BUG-04 fix |
| Sony PlayStation 4 | shadPS4 | zip | PASS | BUG-04 fix |
| Microsoft DOS | DOSBox Staging | tar.xz | PASS | BUG-05 fix (XZ support) |
| Sega Saturn | Ymir | tar.xz | PASS | BUG-05 fix |
| Amstrad CPC (57 systems share it) | RetroArch | 7z (solid) | PASS | BUG-06 fix; 118.7 s for the 392 MB download + 1 GB / 19,797-file extraction |
| Amstrad CPC | RetroArch core | 7z (solid) | PASS | 110.9 s for 261 MB / 1.65 GB; 199 `_libretro.so` extracted to the manifest's `.AppImage.home/.config/retroarch/cores` path |

Not exercised in the sweep: launching each installed emulator with a real ROM (only PPSSPP and
Redream were launched - the other systems have no sample ROMs/BIOS on this machine). `Stop`
mid-download and network-loss handling were exercised separately the same day (next section).

### Easy Mode edge-case session (2026-09-26, ad-hoc)

Driven through the AT-SPI harness on the same VM after the full suite, on the payload built from
HEAD (`f68f6c91` + `7ddc6599`); screenshots in `scripts\gui-test-harness\shots\`:

- **Stop mid-download:** Easy Mode -> Arcade -> **Download Image Pack 1** (570 MB) -> **Stop
  Download** while the progress bar was moving. Status showed `Download of Image Pack 1 was
  canceled.`, the Stop button disabled and the pack button re-enabled (Failed state). **BUG-07**:
  the partial archive (29 MB) stayed in `/tmp/SimpleLauncher` - the documented expectation is
  "partial file removed, clean state".
- **Network loss mid-download (connection at start):** with the two `assets.purelogiccode.com` IPs
  blocked with `iptables` (OUTPUT+INPUT REJECT), starting the Atari 5200 pack download showed
  `Download error. Retrying (1/3)...` after ~39 s; unblocking let the same retry complete -
  `Image Pack 1 has been successfully downloaded and installed.` **PASS**.
- **Network loss mid-download (body already streaming):** blocking the IPs during the Atari 2600
  pack download (93.2%, 280/300 MB) **did not** produce the retry message or any error. The status
  froze at the last progress, the TCP connection stayed ESTAB with no data for >6 min, and the
  download resumed and completed (314,904,631 bytes, 2400 covers extracted) only when the IPs were
  unblocked. **BUG-08**: the response-body read loop has no timeout and the resilience pipeline only
  covers `SendAsync` (headers), so a mid-body network drop stalls the download indefinitely (the
  user can still Stop it).
- **Custom ROM folder picker:** Easy Mode -> Sony PSP -> the `...` browse button opened the GTK
  "Choose a folder with ROMs or ISOs for this system" dialog; selecting `/home/vm/custom-roms`
  filled the field, and after installing PPSSPP (48 MB AppImage) **Add System** added the system
  with `SystemFolders: ["/home/vm/custom-roms"]` in the DB and created `images/Sony PSP`. The
  custom picker itself is now fully exercised (the earlier session had only verified the default
  folder).
- **Separate Download Image Pack window** (Edit System -> Download Image Pack): the window opens
  with the system dropdown, selecting Atari 5200 shows the "Image Pack 1" button, and the download
  completed with the success dialog - the window's download path works end-to-end.

### Linux GUI continuation session (2026-09-26, ad-hoc + fixes)

Second ad-hoc pass on the same VM (payload rebuilt from HEAD after each fix), covering checklist items
the 31 scenarios do not reach. Deterministic checks unless noted; screenshots in `shots\`.

- **Global Search (new coverage, 12 checks PASS):** default AND (`alpha zzz` -> 0), explicit AND,
  OR (`alpha or sonic` -> 2), quoted phrase, empty term -> "Please enter a search term.",
  operators-only -> validation box, recursive search on/off (a subfolder ROM is found only when on),
  Folder Name checkbox (folder-name search on -> 4 hits), system filter (Second System: `sonic` -> 1,
  `alpha` -> 0). Counts came from the Debug window log line
  `Global search for '<term>' returned N result(s)` (the results grid cells are not exposed by AT-SPI).
  **Launch Selected Game** works (row select enables it; "Sonic launched with Dummy Emulator"; the
  dummy emulator log records the ROM). A temporary system with no emulator showed "No Default
  Emulator" in the results (vision) and its launch showed the error box (log:
  `selectedEmulatorManager is null`) instead of launching.
- **Global Search cancel (BUG-11, FIXED):** with a 10,000-ROM bulk system, navigating away during a
  search left it enumerating (it completed and logged a count). Re-verified after the fix: leaving
  mid-search produced no completion line.
- **View/Display options:** Set Button Size (300->500 px), Set Button Aspect Ratio (Square->Taller),
  Set Number of Games Per Page and Show Games update the checkmarks and `settings.dat`
  (`ThumbnailSize`, `ButtonAspectRatio`, `GamesPerPage`, `ShowGames`); checkmarks are restored after a
  restart (500 px/Taller/ShowAll verified, then restored to defaults).
- **Show Games filter (BUG-09, FIXED):** verified live Without Cover -> 2, With Cover -> 3,
  ShowAll -> 5 games across systems.
- **Filename Preferences:** "Display Clean Up Filenames" strips annotations ("Captain Nemo (USA)" ->
  "Captain Nemo", vision PASS); original mode restored.
- **Favorites extras:** launch via double-click, the "Launch the selected favorite game" button and
  Enter all start the game (dummy emulator log) and record play history; Delete removes the selected
  row without confirmation; launching a favorite whose ROM was deleted shows the "Game Not Available"
  prompt and Yes removes the favorite.
- **Play History extras:** the three sort buttons do not change the data (no crash); launching via
  Enter refreshes the row (+1 play, `TimesPlayed` 1 -> 2) and keeps the page open; Delete removes the
  selected row; "Remove all from history" shows the confirmation (No keeps / Yes clears).
- **Context menu:** Delete Cover Image asks confirmation, deletes the PNG, shows the "Deleted" box and
  refreshes the list (the card falls back to the placeholder after the dialog is dismissed); Delete
  Game asks confirmation and removes the ROM, dropping the count. Video/info link opening was not
  exercised (depends on the desktop's default browser).
- **Corrupt cover:** overwriting a cover PNG with garbage shows the placeholder for that card, no
  crash, other covers unaffected (vision PASS).
- **File watcher:** adding a ROM to a non-selected system's folder does not change the current view;
  switching to that system and deleting the file refreshes its count (2 -> 1) - the selected system is
  watched.

### MAME data session (2026-09-27, ad-hoc + 2 new scenarios)

Third ad-hoc pass on the same VM, driven by real MAME ROMs copied from the host (`G:\MAME\MAME Roms`,
`G:\MAME\MAME Bios Devices`): `pacman.zip`, `galaga.zip`, `dkong.zip`, `mspacman.zip` into
`~/roms/Arcade` and `neogeo.zip`, `3dobios.zip`, `a1200kbd_rb.zip` into `~/roms/Arcade BIOS`. The new
`arcade` fixture (`fixture.py arcade` = standard seed + a two-folder "Arcade" system with a MAME
emulator and three covers) backs the new **MAME-01/MAME-02** scenarios; the app runs the same payload
as the continuation session (md5s in the resume checklist). Deterministic checks unless noted;
screenshots in `shots\` (`mame-*`, `MAME-01-list.png`, `MAME-02-history.png`).

- **Multi-folder listing:** the Arcade system lists 7 games; the list-view Folder Path column shows
  4 from `/home/vm/roms/Arcade` and 3 from `/home/vm/roms/Arcade BIOS`; pagination "1 to 7 out of 7".
- **Machine Description column (MAME-01):** real `mame.dat` descriptions render in list view -
  `Pac-Man (Midway)`, `Galaga (Namco rev. B)`, `Donkey Kong (US set 1)`, `Ms. Pac-Man`,
  `Neo-Geo MV-6F`, `3DO BIOS`, `Amiga 1200 Keyboard Rev B` (vision PASS).
- **MAME sort toggle (MAME-01):** clicking the sort-az toolbar button shows "Sorted by machine
  description" and re-orders alphabetically by description; clicking again restores "Sorted by file
  name".
- **Game Details:** context menu -> Show Details on `neogeo` shows the Description "Neo-Geo MV-6F"
  (the system name contains "Arcade" so the description path applies).
- **Global Search MAME description:** `Midway` with the default checkboxes (Filename + MAME
  Description) returns exactly 1 result - `pacman.zip`, "Pac-Man (Midway)", MAME, Arcade; unchecking
  MAME Description returns "No results found." with 0 rows.
- **Favorites / Play History description columns:** `pacman.zip` added to Favorites shows
  "Pac-Man (Midway)" under "Machine Description (for MAME files)"; launching it records Play History
  with the same description, 1 play, 0m 7s.
- **ROM History real entry (MAME-02):** context menu -> Open ROM History on `pacman` shows the real
  `history.dat` entry (MSX cart entry with TRIVIA/CONTRIBUTE sections) and the machine description as
  the subtitle; window frame + >200-char history text + title all asserted, vision PASS.
- **`mame.dat` failure paths:** deleted -> the "Missing Required Files" startup dialog (OK), a Warning
  in `error_user*.log`, and the app continues with all 7 games but empty descriptions (graceful);
  4 KB of random bytes -> an Error + MessagePack exception in the log, **no dialog**, same graceful
  empty-description state. The file was restored afterwards (md5 `50050360caa58888836c6bacc9a3045c`,
  backup left at `/home/vm/mame.dat.bak`).
- **GroupByFolder warning (Edit System):** Second System (Dummy Emulator) + Group By Folder = true +
  Save shows the "Configuration Warning" Yes/No box; No aborts the save (DB still `false`). Note: the
  Test System fixture (`Extract File Before Launch = true` with `.nes, .zip`) is rejected by the
  unrelated "must include zip/7z/rar" validation before the warning, by design.
- **Findings -> BUG-12 (FIXED 2026-09-27):** the intended Yes/No reinstall dialogs never appeared at
  startup because the message-box owner is still null while `MameDataService` loads (the missing case
  was covered by the required-files dialog, the corrupt case was silent), and the Avalonia
  "Missing Required Files" dialog showed the "will shutdown" text without shutting down (the WPF
  version offers a reinstall and quits). Fixed and live-verified on the rebuilt payload:
  - `MameDataService` no longer notifies from its constructor; it records
    `MameDataLoadFailure` (None/MissingFile/CorruptedFile, `MameManagerService.LoadFromDat` gained a
    `notifyUser` seam) and both apps' startup initialization call
    `NotifyLoadFailureIfNeededAsync()` after the window exists. A corrupt `mame.dat` now shows
    "'Simple Launcher' could not load the file 'mame.dat' or it is corrupted. Do you want to
    automatically reinstall 'Simple Launcher' to fix it?" (Yes/No); No quits the app.
  - `HandleMissingRequiredFilesMessageBoxAsync` was ported to the WPF flow: "The following required
    file(s) are missing: <paths> Do you want to reinstall 'Simple Launcher' to fix the issue?"
    (Yes -> auto reinstall + shutdown; No -> "Please reinstall manually... will shutdown." then quit).
  - Live checks: corrupt -> dialog + No quits; missing -> file-list Yes/No, No -> error box -> OK
    quits; restored file (md5 `50050360caa58888836c6bacc9a3045c`) -> healthy startup with descriptions.
  - Regression tests: `SimpleLauncher.Avalonia.Tests/MameDataServiceTests.cs` (4 tests). Avalonia
    suite 686/686, WPF suite 2122/2122, builds 0 warnings.
- **Coverage delta:** `docs/manual-tests.md` lines 68 (list rendering / Machine Description), 97
  (GroupByFolder warning), 110 (ROM History - now with a real MAME entry) and 235 (MAME data) are
  verified; the remaining unexercised items are unchanged (see open item 4).

### Emulator sweep session (2026-09-27, ad-hoc + RetroAchievements)

Goal: exercise the real emulator launch pipeline with the user's ROM collection (host `G:\`) and test
RetroAchievements with real credentials (username/password + Web API key supplied by the user; **not
stored in the repo**).

**Setup (reusable):**
- RetroArch 1.22.2 (`RetroArch_Linux_x64.7z` + `RetroArch_cores.7z` from the manifest links) extracted
  into `~/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64/` (the AppImage + `.home` layout
  the Easy Mode manifest expects; `chmod +x` the AppImage). 491 core files.
- OpenMSX 21.0 (`openmsx-21.0-linux-x86_64-bin.zip`) extracted to
  `~/SimpleLauncher/emulators/OpenMSX/` (`bin/openmsx`).
- 50 real ROMs (873 MB) copied from `G:\` for 36 systems; system configs generated from the Linux
  Easy Mode manifest (`vm-systems.json`) and seeded with `seed-batch.py` (new harness helper). Image
  folders must exist (`~/images/<system>`) or opening the system shows "System Image Folder path is
  not valid or does not exist".
- Harness helpers added: `scripts/gui-test-harness/seed-batch.py` (keep base systems + a batch) and
  `vm-systems.json` (36 manifest-derived configs).

**Launch sweep (all through the UI: system card -> letter "All" -> right-click -> Launch Game):**
36/36 systems opened with correct game counts; **29 distinct emulators/cores** started with real ROMs:

| Systems | Emulator/core (window title) |
|---|---|
| NES, FDS | RetroArch Mesen 0.9.9 (FDS ROM needs `disksys.rom`, emulator showed Error) |
| SNES, Satellaview | RetroArch Snes9x 1.63 |
| GB, GBC | RetroArch Gambatte v0.5.0 |
| GBA | RetroArch mGBA 0.11-dev |
| N64 | RetroArch Mupen64Plus-Next 2.8-Vulkan |
| NDS | RetroArch melonDS 0.9.3 |
| Genesis, SMS, GG, SG-1000 | RetroArch PicoDrive 2.05 / Genesis Plus GX v1.7.4 |
| 32X | RetroArch PicoDrive 2.05 |
| PC Engine, SuperGrafx, Virtual Boy, NGP, NGPC, WonderSwan | Beetle PCE / SuperGrafx / VB / NeoPop / WonderSwan |
| Atari 2600/5200/7800/8-Bit/ST/Lynx/Jaguar | Stella 8.0_pre / a5200 / ProSystem / Atari800 / Hatari / Handy / Virtual Jaguar |
| Colecovision, Intellivision, Odyssey 2, C64, Amiga | blueMSX / FreeIntv / O2EM / VICE x64sc 3.9 / PUAE 5.3.0 |
| ZX Spectrum | Fuse 1.6.0 |
| Arcade RA | MAME core started; pacman/dkong need device ROMs from a full MAME set (emulator Error) |
| ScummVM | scummvm core started; zip game data not verified visually |
| MSX | OpenMSX started; MSX BIOS ROMs are not bundled (emulator Error) |

Every launch recorded a PlayHistory row (TimesPlayed=1, ~8 s play time) and the app stayed alive;
closing the emulator re-enabled the UI. MAME/OpenMSX/FDS errors are emulator data gaps (missing
device/BIOS ROMs), not app bugs.

**RetroAchievements (live):**
- Settings window (Options -> Retro Achievements Settings): username `petersonfernandes`, password
  and Web API key saved; reopen pre-fills; the RA profile page loads live data (Points 38, True
  Points 46, Member Since 2025-09-30, User ID 1437505, Currently Playing).
- Per-game flow on Atari 2600 *Adventure*: context menu "View Achievements" -> "Calculating Game
  Hash..." -> window with cover, Casual/Hardcore progress bars, 12 achievements (points, true ratio,
  rarity "55.6% hardcore", Locked, "Not Earned", author) - the bundled
  `tools/RetroAchievementsSharp/RetroAchievementsSharp` CLI computed the hash locally.
- Missing API key -> "You need to add RetroAchievement login information to use this feature." (no
  crash); hasher failure -> "'Simple Launcher' could not calculate the hash value... Do you want to
  open the global RetroAchievements window?" (graceful).
- Credential storage on Linux: `RaPassword`/`RaApiKey` are Base64 (portable fallback, DPAPI is
  Windows-only) - by design, but effectively obfuscation, not encryption (documented limitation).

**Findings:**
- **BUG-13 (FIXED 2026-09-27):** the DPAPI-unavailable message was logged at `Warning`, so the
  bug-report sink filed it as a bug on every Linux RA credential save
  (`error_user.log`: "DPAPI is not available on this platform..."). Now `Information`
  (`WindowsCredentialProtector.WarnPortableFallback`); verified on the VM (no new entry after the
  fixed Core was deployed, md5 `ec5896c77f5e49784c99d886a16bcab1`).
- Harness gotchas (not app bugs): SCP deployment strips the exec bits of the bundled Linux tools
  (`7zz`, `RetroAchievementsSharp`) - `chmod +x` after deploy (the release zip sets 0755 via
  `package-release-linux.ps1`); a single-element `FileFormatsToSearch` array serialized as a JSON
  string makes the app's strict `SystemConfigStore` deserializer silently skip the whole system
  (seed scripts now normalize to arrays); a system needs its image folder to exist before it opens.
- Grid cards only render after a letter filter (`filter_letter("All")`) - the right-click launch
  sweep silently did nothing without it.

### Standalone emulator + BIOS session (2026-09-27, E:/F:/G:/I:/J: collections)

Goal: exercise the remaining standalone emulators (PS1/PS2/PS3/Dreamcast/Saturn/WiiU/3DS/PSP/Xbox/
DOS/Model 3) and the CD-based RetroArch cores with real ROMs, using the user's additional drives
(`E:`, `F:`, `G:`, `I:`, `J:`). The user supplied the BIOS/firmware sources and asked to set PS3/Vita
up with official firmware; the **AI Parameter Suggestion** feature stays out of scope.

**Setup (reusable):**
- BIOS: `D:\Emulators\RetroArch\system` is a complete RetroArch system dir (PS1 scph*, PS2, Dreamcast
  `dc/`, Saturn `sega_101.bin`/`mpr-17933.bin`, PCE CD `syscard3.pce`, 3DO `panafz10.bin`, PC-FX
  `pcfx.rom`, Sega CD `bios_CD_*.bin`, CD32 `kick40060.CD32*`, X68000 `keropi/`, N64DD
  `Mupen64plus/IPL.n64`, CD-i `same_cdi/bios/cdimono1.zip`, Neo CD `neocd/`, MSX ROMs, `tos.img`,
  `disksys.rom`, ...). `D:\Emulators\Xemu` has the Xbox BIOS/HDD (`mcpx_1.0.bin`,
  `Complex_4627.bin`, `eeprom.bin`, `xbox_hdd.qcow2`). MAME-set BIOS (`G:\MAME\MAME Roms`) also
  works (neocd.zip, cdimono1.zip, pcfx.zip, 3dobios.zip, n64dd.zip, x68000.zip, megacd/segacd,
  cd32.zip, jaguarcd.zip).
- Standalone Linux emulators installed per the Easy Mode manifest into
  `~/SimpleLauncher/emulators/<Name>/` (DuckStation, PCSX2, RPCS3, Redream, Ymir, Azahar, Cemu,
  Xemu, Supermodel, DOSBox Staging; PPSSPP was already there). `chmod +x` every binary after SCP.
- Per-emulator data dirs on Linux (learned the hard way):
  - DuckStation: portable mode via `portable.txt` next to the AppImage; BIOS in `<dir>/bios/`.
  - PCSX2: data dir `~/.config/PCSX2` (BIOS files go in `~/.config/PCSX2/bios/`; the `inis/PCSX2.ini`
    `[Filenames] BIOS` path alone did not satisfy the BIOS check).
  - RPCS3: `~/.config/rpcs3` (dev_flash copied from the user's Windows install works).
  - Redream: `redream.cfg` + `dc_boot.bin`/`dc_flash.bin` next to the binary.
  - Ymir: `Ymir.toml` + `saturn_bios.bin` in the emulator dir (IPL override path patched).
  - Cemu: portable mode = a `portable` dir next to `Cemu`; **keys.txt and settings.xml go inside
    `portable/`** (mlc01 too). Title keys: the user's per-game `.key` zips can be hex-encoded into
    keys.txt (one 32-hex key per line).
  - xemu: config is `~/.local/share/xemu/xemu/xemu.toml` (not `~/.config`!), data files in the same
    dir (mcpx/flashrom/eeprom/hdd).
  - Supermodel: `~/.supermodel/{Config,ROMs,Saves,NVRAM,Assets}` (sample `Supermodel.ini` from the
    app's `samples/`).
  - openMSX: `~/.openMSX/share` must resolve to the install's `share` (symlink it) or openMSX cannot
    find `machines/C-BIOS_MSX2+.xml`; BIOS ROMs in `share/systemroms`.
- ROMs staged from the drives (~3 GB, 27 files) and seeded with `seed-batch2.py` +
  `vm-systems2.json` (new harness helpers; `fix-arrays.py` normalizes scalar format strings).

**Coverage (all through the UI, app-driven, play history recorded):**

| System | Emulator | Result |
|---|---|---|
| Sony PlayStation 1 | DuckStation | **launched** (Raiden Project) |
| Sony PlayStation 2 | PCSX2 | **game boots** (Nami/Natsume intro) after placing the BIOS in `~/.config/PCSX2/bios` |
| Sony PlayStation 3 | RPCS3 | app launches RPCS3 with the CHD converted to ISO; RPCS3 rejects the collection's ISO9660 "PS3VOLUME" images ("Invalid file or folder"); first-run welcome wizard also appears |
| Sega Dreamcast | Redream | **launched** (18 Wheeler) |
| Sega Saturn | Ymir | **launched** (3D Baseball, title bar shows the game) |
| Nintendo WiiU | Cemu | **game boots** (Funky Barn) after adding the title keys to `portable/keys.txt`; first-run "Getting started" wizard must be dismissed once |
| Nintendo 3DS | Azahar | **launched** (Puzzler Mind Gym 3D) |
| Sony PSP | PPSSPP | **launched** (NHL 07) |
| Microsoft Xbox | xemu | **game boots** (007: Agent Under Fire EA logo) after fixing the xemu.toml path |
| Microsoft DOS | DOSBox Staging | **game boots** (Wolfenstein 3D) via the app's file-selection dialog |
| Sega Model 3 | Supermodel | the app **ran the .bat** and reported exit code 1; Supermodel rejected the split ROM set (incomplete) |
| Nintendo FDS | RetroArch mesen | still fails: the collection's image is `.qd` (Quick Disk), unsupported by mesen; `disksys.rom` is now in place |
| Atari ST | RetroArch hatari | **launched** with `tos.img` in the system dir |
| GameCube / Wii / N64DD / 3DO / Neo Geo CD / CD-i / PC-FX / Sega CD / CD32 / Jaguar CD / PCE CD / X68000 | RetroArch cores | from the earlier sweep; **N64DD** fails because mupen64plus-next cannot load a standalone `.ndd`, and **X68000** fails because px68k cannot load the zipped split `.raw` disks (extracted `.raw` loads fine) |

**Fixes:**
- **BUG-14 (FIXED 2026-09-27):** a failing batch file/executable logged at `Warning`/`Error`
  (Avalonia `LauncherService`, WPF `GameLauncherService`) so the bug-report sink filed expected
  user conditions (e.g. an incomplete Supermodel ROM set) as bugs. Now `Information`; the user
  still gets the toast/status/message box.
- **BUG-15 (FIXED 2026-09-27):** the DOSBox file-selection list items exposed the CLR type name
  (`SimpleLauncher.Core.Models.DosBoxFileItem`) as their accessible name because
  `DosBoxFileItem` had no `ToString()`; screen readers read garbage. Fixed with a `ToString()`
  override returning `DisplayName`.
- Conversion check: the app's managed CHD→ISO conversion (DiscConverter/CHDSharp) is
  **byte-identical** to `chdman extractdvd` for the PS3 CHD (verified with `cmp`), so the RPCS3
  rejection is the image format, not the conversion.

**Observations:**
- The app's "AI Parameter Suggestion" window appeared during an emulator failure (excluded from
  testing; closed without interaction).
- `.bat` launches work on Linux (`UseShellExecute`) - the Model 3 flow ran the script and surfaced
  its exit code.
- The AT-SPI tree can become truncated after heavy sessions (nodes: 0 while the window is fine);
  app restarts did not clear it, a **guest reboot** did. The reliable pattern is: one system per
  app start, `ensure_ready` (maximize) first, verify the letter bar before right-clicking.
- Stale AT-SPI peers survive the return to the card screen, so card/browser detection must use
  markers that only the real screen has (base-system card labels, letter bar).

### Full-suite pass + BUG-14/15 live verification (2026-09-27, second session)

Continuation session on the same VM, with the payload **rebuilt and redeployed from HEAD** `9f684574`
(`SimpleLauncher.Core.dll` md5 `8e976d1af9ea74dbb8a04379b896c1b9`, `SimpleLauncher.Avalonia.dll` md5
`d10b89b449784d80deebfa4420ed6fc7`), which brought the previously undeployed BUG-14/15 fixes to the VM.

- **BUG-15 live verification (PASS):** app-driven launch of `Microsoft DOS` -> Wolfenstein 3D through the
  AT-SPI harness (`launch-one.py`): the DOSBox file-selection list items now read `run.bat` / `WOLF3D.EXE`
  (previously the CLR type name) and selecting the first entry + Launch booted DOSBox Staging
  (window "WOLF3D.EXE - 3000 cycles/ms").
- **BUG-14 live verification (PASS):** app started with `-debug`; launching `Sega Model 3`/`daytona2` ran the
  `.bat` (exit code 1) and the Debug window shows
  `2026-09-28 01:14:52.037 [Information] Batch file exited with code 1: "/home/vm/roms/Sega Model 3/daytona2.bat"`.
  `error_user*.log` gained no new "Batch file exited" entry (the Warning+ file sink and the bug-report sink are
  no longer hit) - the two pre-fix Warning entries from the standalone session remain as the only occurrences.
- **EASY-02 rewritten (stale expectation, not an app bug):** the VM now has RetroArch (and every other Easy
  Mode emulator except shadPS4) installed from the sweep sessions, so selecting Atari 2600 correctly shows
  "Add System" enabled and the download buttons disabled. The scenario now selects **Sony PlayStation 4**
  (shadPS4 still missing; the dropdown is wheel-scrolled to reach it) and asserts the missing-emulator state:
  Download Emulator enabled, Add System and Download Core disabled (PS4 has no core download). Verified in
  isolation (`reports\run-20260927-230050.md`) and in the full pass.
- **Full 33-scenario pass: 33/33 PASS in a single run** (`reports\run-20260927-232933.md`) - the first
  single-run pass including MAME-01/MAME-02. The known AT-SPI registration flakiness produced several
  "AT-SPI tree not ready" retry episodes (EASY-01->02, THEME-02, ROMHIST-01) but every restart recovered; no
  scenario failed for tree reasons.
- `run-suite.ps1` carries the EASY-02 rewrite (uncommitted at this session's end; committed in
  `73d589e7` on 2026-09-28).

### User-reported fixes + freeze repro (2026-09-28, third session)

Continuation session driven by the developer's manual testing of the Avalonia/Linux build. The
payload was rebuilt from the working tree and redeployed incrementally (`SimpleLauncher.Avalonia.dll`,
then `SimpleLauncher.Core.dll`); both suites are green (Avalonia **696/696**, WPF **2122/2122**).

- **BUG-16 - Easy Mode "Add System" appeared stuck on the loading overlay (FIXED 2026-09-28).**
  Message dialogs were always owned by MainWindow (`AvaloniaWindowContext.OwnerWindow`), while Easy
  Mode is a modal child of the same window; on X11 the WM could stack the success dialog behind it,
  so the overlay never cleared. `MessageBoxLibraryService.GetDialogOwner` now prefers the active
  window (WPF parity); verified with `WM_TRANSIENT_FOR` (MainWindow before, Easy Mode after) and a
  full add (6->7 systems, folders created, list refreshed).
- **BUG-17 - card overlay buttons ignored the Options menu settings (FIXED 2026-09-28).** The
  Avalonia card only had a trophy badge hard-wired to `IsRaSupported`; video/info overlay buttons did
  not exist and toggling did nothing. Cards now render the three WPF-parity overlay buttons
  (RA/video/info, top-right stack; favorite heart moved to the WPF star position top-left), driven by
  `OverlayRetroAchievementButton`/`OverlayOpenVideoButton`/`OverlayOpenInfoButton`; the menu toggle
  refreshes the loaded cards in place (`MainViewModel.RefreshOverlayButtons`) and clicks run the same
  actions as the context menu without launching the game. 5 new tests
  (`MainViewModelOverlayButtonTests`); visible on the VM cards (`shots/after-storm.png`).
- **BUG-18 - the card-size slider did not update the Button Size menu (FIXED 2026-09-28).** The
  slider was bound straight to `CardWidth`, so it neither persisted the size nor moved the menu check
  mark (and off-grid sizes could not match any menu option). All size paths (slider, menu, nav zoom,
  Ctrl+wheel) now go through `MainViewModel.ApplyButtonSize`, which snaps to the 50-px menu options
  (50..800) and persists; the menu check mark and slider handle stay in sync (WPF
  `HandleButtonSizeAsync` parity). 4 new tests in `MainViewModelQuickActionsTests`.
- **BUG-19 - rapid aspect-ratio clicks froze the app permanently (FIXED 2026-09-28).** Reproduced by
  hammering the nav aspect-ratio button (300 clicks @15 ms): the app froze with the UI thread blocked
  in a managed join while the ALSA Playback thread was wedged in `snd_pcm_mmap_writei` (gdb). Root
  cause: `PlaySoundEffects.PlaySound` ran on the UI thread and synchronously called
  `Stop()`/`Dispose()`, which join the playback thread - one wedged ALSA/PipeWire stop froze the whole
  application. Playback now runs on a dedicated background thread ("SimpleLauncher Audio"): callers
  only enqueue (newest wins), lifecycle calls happen on the worker, `Dispose` never joins, and
  identical sounds within 250 ms are coalesced to stop the rapid open/stop churn that wedges
  PipeWire. Regression test with a blocking fake player
  (`PlaySound_WhenAudioStopBlocks_DoesNotBlockCallerOrDispose`). Re-verified live: the same 300-click
  storm left the UI responsive (AT-SPI snapshot returned the full 456-node tree; the pre-fix run
  returned 8 nodes and never recovered).

### Open items for the next session

1. **Full pass - DONE 2026-09-27**: `reports\run-20260927-232933.md` **33/33 PASS in a single run** on the
   payload built from HEAD (`9f684574`, BUG-07..BUG-15 fixes); the previous 31/31 run was 2026-09-26
   (`reports\run-20260926-143336.md`). Keep this runbook and `docs/manual-tests.md` in sync after any app change.
2. **WPF suite re-run - DONE 2026-09-26**: `dotnet test SimpleLauncher.Tests\SimpleLauncher.Tests.csproj`
   **2122/2122 pass, 0 failures** (the 19 environment-only failures from the previous run did not
   reproduce); Avalonia suite `SimpleLauncher.Avalonia.Tests` **677/677 pass**.
3. **AT-SPI registration flakiness (environment)**: after a rapid app restart the app occasionally
   fails to register with the a11y bus (empty tree while the window still renders). It occurred twice
   this session (THEME-02's restart in the first full pass, and the empty->seeded fixture change during
   the guard smoke test); both times another restart recovered it, but the first bad window lasted
   ~15 min while scenarios kept running. `run-suite.ps1` now checks `Test-VmNavHealth` after each
   restart, retries the restart once, and aborts with a clear message if the tree is still missing -
   never trust a red run against an empty tree. `Start-VmApp` still waits up to 60 s per start and
   retries 3 starts. **Do not kill `at-spi-bus-launcher` / `at-spi2-registryd`**: that leaves a stale
   `AT_SPI_BUS` guid on the X root window and every new app then fails to register; recover with a VM
   reboot.
4. **Coverage gap (manual/integration by design)**: store scanners, config injection, RA API/login,
   gamepad hardware, CHD/ISO/XISO mounting, external tools, updater, Commander Genius - keep as
   manual (Linux hides most of them anyway; LB-08/LB-14/LB-22 already verified by code/tests).
   Emulator/core downloads are swept (15/15 + 1/1) and the Easy Mode edge cases plus the continuation
   session were exercised on 2026-09-26 (see the two sections above), which surfaced and fixed
   BUG-07..BUG-11; the MAME data session on 2026-09-27 covered MAME descriptions, sort, search,
   Favorites/Play History columns, ROM History, the `mame.dat` failure paths and the GroupByFolder
   warning; the emulator sweep session on 2026-09-27 launched real ROMs through **29 distinct
   emulators/cores** (see that section) and verified the RetroAchievements settings login, profile
   page, per-game window and local hashing with real credentials. The standalone emulator + BIOS
   session on 2026-09-27 then launched PS1/PS2/Dreamcast/Saturn/WiiU/3DS/PSP/Xbox/DOS/Atari ST with
   real games and exercised the DOSBox file selection (see that section). Still unexercised: the
   `<5 GB free` disk-space error, the final failure after all retries, closing a window during a
   download, the successful Edit System save path and the AI fix flow, Support valid submit, Image
   viewer, System selection, file-picker nuances, Set Links / Sound Configuration / dead-zone
   Save+Revert, About offline update checks, log files and the bug-report sink, config-persistence
   failure paths, gamepad on this VM, video/info context-menu links, the RA per-game window's other
   tabs (Game Info/Ranking/My Profile/Unlocks/User Progress), RA login/API error paths and the
   hashing variants (NES header-strip, system picker, zipped PS1, RVZ, unsupported system), the
   PS3 boot (RPCS3 rejects this collection's ISO9660 images; the conversion is chdman-identical)
   and the Vita (the collection is `.pkg`-based, no `.vpk` to launch).
5. **MAME startup notifications - FIXED 2026-09-27 (BUG-12)**: the corrupt `mame.dat` case was
   silent and the Avalonia required-files dialog claimed a shutdown without quitting. `MameDataService`
   now records the load failure and the startup initialization reports it once the window exists;
   the required-files dialog was ported to the WPF Yes/No + reinstall/quit flow. Live-verified on the
   rebuilt payload (corrupt -> dialog + No quits; missing -> file-list Yes/No -> No -> error -> OK
   quits; restored file -> healthy). See the MAME session section and §8 for details.

### Resume checklist (next session)

**Guest state left by the 2026-09-28 manual-testing session** (all of this survives a host/VM reboot unless noted):

- VM `LinuxMint` running (or start it with `Start-VM LinuxMint`, elevated); guest IP is
  `172.31.176.191` (the Default Switch subnet reverted from `192.168.65.x` back to `172.31.x` at this
  reboot; `lib.ps1` updated accordingly) - always re-check after a VM boot.
- App: `~/SimpleLauncher/SimpleLauncher.Avalonia`, payload rebuilt from the working tree (all fixes
  through BUG-19) and deployed (`SimpleLauncher.Core.dll` md5 `2d7eff75dee0bc6be36ecbf3af976135`;
  `SimpleLauncher.Avalonia.dll` md5 `ac3b2f7961b44fa197d930df4d4bf824`). The developer was manually
  testing when this block was written: the app runs the **Atari 2600** grid (776 files) with the
  overlay buttons enabled, notification sound on, aspect ratio last set by the freeze repro. The
  Avalonia (696/696) and WPF (2122/2122) suites are green on the current working tree; everything
  (EASY-02 rewrite + BUG-16..19 fixes + docs) is **committed and pushed through `04836cbd`** - the
  tree was clean at stop time. The developer was **mid manual test** of the four fixes (see item 6);
  two Nemo file-manager windows (`roms`, `Atari2600`) may still be open on the guest - they pollute
  desktop-wide AT-SPI walks but not `Nav` (which is scoped to `SimpleLauncher.Avalonia`); close them
  before ad-hoc desktop probes.
- Host ROM share mounted on the guest (new 2026-09-28): `\\PETERSONPC\Atari2600` (`G:\Atari 2600`,
  read-only, local user `vmshare`) is mounted at `/home/vm/Atari2600` via
  `/home/vm/.smbcredentials` (root-only) with a `nofail,_netdev` fstab entry; 776 Atari 2600 ROMs
  are visible there and the app's Atari 2600 system points at that folder. Host-side credential copy
  (may be cleaned by temp cleanup): `C:\Users\HomePC\AppData\Local\Temp\opencode\vmshare-cred.txt`.
  Undo: remove the fstab line + `sudo umount`, then on the host `Remove-SmbShare Atari2600` and
  `Remove-LocalUser vmshare`.
- Emulator sweep session artifacts on the guest: RetroArch 1.22.2 + 491 cores under
  `~/SimpleLauncher/emulators/RetroArch/RetroArch-Linux-x86_64/`, OpenMSX 21.0 under
  `~/SimpleLauncher/emulators/OpenMSX/`, real ROMs for 64 systems under `~/roms/` (~3.9 GB from
  `G:\` + `E:/F:/I:/J:`), per-system image folders under `~/images/`, and in `/home/vm/vision/`:
  `vm-systems.json` + `seed-batch.py` (first sweep) and `vm-systems2.json` + `seed-batch2.py` +
  `fix-arrays.py` + `launch-one.py` (standalone session). Bundled Linux tools
  (`tools/SevenZip/7zz`, `tools/RetroAchievementsSharp/RetroAchievementsSharp`) were `chmod +x`-ed
  after SCP deploy. `settings.dat` has the RA username/API key/password configured
  (`petersonfernandes`) - the API key is **not** in this repo; re-enter it if the settings are
  wiped.
- RetroAchievements: the account is the user's `petersonfernandes` (password + Web API key supplied
  by the user in chat; **not stored in this repo**). The VM's `settings.dat` currently holds them;
  ask the user again if the settings are wiped.
- MAME session artifacts on the guest: real ROMs in `~/roms/Arcade` (pacman/galaga/dkong/mspacman)
  and `~/roms/Arcade BIOS` (neogeo/3dobios/a1200kbd_rb), covers in `~/images/Arcade/`, a backup of
  the restored `mame.dat` at `/home/vm/mame.dat.bak` (md5 `50050360caa58888836c6bacc9a3045c`).
- Installed under `~/SimpleLauncher/emulators/`: RetroArch 1.22.2 + 491 cores, OpenMSX 21.0,
  PPSSPP 1.20.4, DuckStation, PCSX2 2.8.2, RPCS3 0.0.42 (+ `~/.config/rpcs3/dev_flash`), Redream
  1.5.0, Ymir 0.3.3, Azahar 2126.1.2, Cemu 2.6 (portable mode: `Cemu_2.6/portable/` holds
  `keys.txt` - the user's per-game `.key` files hex-encoded - `settings.xml` and `mlc01`), Xemu
  0.8.136 (`~/.local/share/xemu/xemu/` holds the BIOS/HDD and `xemu.toml`), Supermodel 0.3a
  (`~/.supermodel/` layout), DOSBox Staging 0.83.0, Vita3K (firmware extracted at `/tmp/vita-fw`,
  not installed - the collection has no `.vpk`). All binaries `chmod +x`-ed after SCP.
- BIOS/system files staged for the standalone session: the RetroArch system dir has the PS1/PS2/
  Dreamcast/Saturn/PCE-CD/3DO/PC-FX/Sega-CD/CD32/X68000/N64DD/CD-i/Neo-CD/MSX/`tos.img`/
  `disksys.rom` set (from `D:\Emulators\RetroArch\system`), `~/.config/PCSX2/bios` has the PS2 BIOS,
  DuckStation has `portable.txt` + `bios/` + `settings.ini`, Redream/Ymir have their BIOS/config in
  the emulator dirs, and `~/.openMSX/share` is a symlink to the OpenMSX install share.
- `~/roms/` now has 64 system folders (~3.9 GB) including the new standalone systems; `~/images/`
  has a folder per system. The new systems were seeded with `seed-batch2.py` + `vm-systems2.json`
  (in `/home/vm/vision/`), then `fix-arrays.py` normalized the scalar format strings.
- Guest screensaver lock was disabled (`gsettings set org.cinnamon.desktop.screensaver lock-enabled
  false` + `idle-activation-enabled false`; persists in dconf) and `xset s off -dpms` was applied
  (does **not** persist across reboot). Without this the physical `:0` console locks and hides the
  desktop from `vmconnect`/screenshots.
- The vmconnect console window may still be open on the host; if it shows the "Connect to LinuxMint"
  display dialog, click **Connect**; if the guest shows the lock screen, log in `vm`/`vm`. The
  enhanced session (xrdp) login fails on this guest - stay on the basic session.

**Steps:**

1. Start the VM (`Start-VM LinuxMint`, elevated); re-check the DHCP IP with
   `Get-NetNeighbor -InterfaceAlias 'vEthernet (Default Switch)'` and update
   `$script:VmHostAddress` in `scripts\gui-test-harness\lib.ps1` if it changed (currently
   `172.31.176.191`).
2. After a guest reboot re-apply 1080p and re-disable the screensaver:
   `DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority xrandr --output Virtual-1 --mode 1920x1080` and
   `gsettings set org.cinnamon.desktop.screensaver lock-enabled false; gsettings set
   org.cinnamon.desktop.screensaver idle-activation-enabled false; xset s off -dpms`
   (the `xrandr-1080p` autostart normally does the resolution already).
3. If every new app start leaves the AT-SPI tree empty (and the `Start-VmApp` retries keep failing),
   reboot the VM - do not restart the a11y bus (see open item 3).
4. Health check: `. scripts\gui-test-harness\lib.ps1; Deploy-VmNav; Test-VmNavHealth` (must be True).
5. **If the app must be rebuilt** (source changed since the DLLs above):
   `dotnet publish SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj -c Release -f net10.0 -r
   linux-x64 --self-contained true -o D:\payload\linux-x64-annot`, then `Stop-VmApp`, SCP
   `SimpleLauncher.Core.dll` + `SimpleLauncher.Avalonia.dll` to `/home/vm/SimpleLauncher`, `Start-VmApp`.
6. **Next work (where the 2026-09-28 session stopped):** the developer was manually testing the four
   fixes from the third session (BUG-16 dialog ownership, BUG-17 overlay buttons, BUG-18 slider/menu
   sync, BUG-19 audio freeze) on the running app. Resume by continuing that manual pass, then:
   - **Re-run the full 33-scenario suite on the new payload** - the last full pass
     (`reports\run-20260927-232933.md`, 33/33) was on `9f684574`; `SimpleLauncher.Avalonia.dll` and
     `SimpleLauncher.Core.dll` have changed since (BUG-16..19), so a fresh report is required.
   - Candidate new scenarios: the card overlay buttons (AT-SPI exposes them as push buttons named
     `View Achievements`/`View Video`/`View Info`) and the button-size slider/menu sync.
   - The freeze repro is reusable: 300 `xdotool` clicks @15 ms on the nav aspect-ratio button, then
     an AT-SPI snapshot probe (see `docs\gui-test-harness.md` §12/§15); the pre-fix build froze
     (8-node tree), the fixed build stays responsive (full tree).
   - The successful Edit System save path, plus the unexercised items from open item 4 (Easy Mode
     disk-space/final-failure/close-during-download paths, AI fix flow, Support valid submit, Image
     viewer, System selection, file-picker nuances, Set Links / Sound Configuration / dead-zone
     Save+Revert, About offline checks, log files and the bug-report sink, config-persistence
     failure paths, gamepad on this VM, video/info context-menu links, the RA per-game tabs/error
     paths and hashing variants). Real-ROM launches are done for 14 systems with 10 booting games;
     PS3/Vita/64DD/FDS/X68000/Model 3 are blocked by image/format/data issues (see the standalone
     session section).
7. Run subsets while iterating and finally the full suite; on failures read the newest
   `reports\run-*.md` first (deterministic JSON + vision answer).
8. Keep this section (and the AGENTS.md pointer) updated after every session; attach the report path
   and the coverage delta against `docs/manual-tests.md`.
9. **Working tree at session end (2026-09-28):** **clean, everything committed and pushed** through
   `04836cbd` on `origin/master`:
   - `c263dcc9` dialog owner fix (BUG-16), `2c933066` card overlay buttons + button-size sync
     (BUG-17/18), `4115bffe` background audio thread (BUG-19) + regression test, `73d589e7` +
     `a5a9b3e9` docs (full-suite pass, EASY-02 rewrite, BUG-16..19, resume state), `9b601eba` +
     `04836cbd` the dead-`async` Button Size handler fix + test comment. Earlier: BUG-07..15 in
     `aa87e469`..`a22d4d32` and the emulator fixtures in `b9f9d157`, `d1818050`, docs through
     `9f684574`.
   - `dotnet test` green: Avalonia **696/696**, WPF **2122/2122** (2026-09-28, Windows host).
   - No pending source changes. The **guest payload is one behavior-neutral commit behind** (the
     dead-`async` Button Size handler fix); rebuild+deploy per step 5 if a byte-exact match is wanted
     before the next full-suite run.
   - `AGENTS.md` is gitignored (local-only, updated with the resume state); everything tracked is
     committed - nothing to commit before starting the next session.

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
- **BUG-03 - Easy Mode Linux: Sega Dreamcast (Redream) `.tar.gz` install failed. FIXED 2026-09-26.**
  The Linux Easy Mode manifest ships `redream.x86_64-linux-v1.5.0.tar.gz` (and Supermodel as a
  `.tar.gz`), but `ExtractionService.IsSupportedArchivePath` accepted only 7z/ZIP/RAR: the download
  succeeded (`/tmp/SimpleLauncher/redream...tar.gz`) and the install failed with the Warning "The
  selected file cannot be extracted. To extract a file, it needs to be a 7z, zip, or rar file.",
  leaving **Add System** disabled. `ArchiveFactory` also cannot open a `.tar.gz` from its path (it
  detects the GZip container but not the nested Tar), so the fix decompresses the GZip layer to a
  seekable temporary `.tar` and opens it with `TarArchive`, and restores the tar Unix permission bits
  (Redream's `redream`, mode 0755) so the emulator is executable. The English message and the Core log
  now list `tar.gz`/`tgz`. Regression tests:
  `ExtractionServiceTests.ExtractToFolderAsync_ExtractsTarGzAndRestoresUnixExecuteBits` plus the
  extended `IsSupportedArchivePath` theory (673 Avalonia tests pass). Verified end-to-end on the VM
  (install, Add System, Redream launch, play history - see the session above).
- **BUG-04 - Easy Mode Linux: zip-based emulators installed without execute bits. FIXED 2026-09-26.**
  OpenMSX (MSX), Cemu (WiiU) and shadPS4 (PS4) are shipped as `.zip`; the archive extracts correctly
  but SharpCompress writes files without Unix modes and those zips carry no modes either (7-Zip shows
  `.....` attributes), so the emulator binary landed non-executable and would fail to launch with
  EACCES. Fix: after a successful **Emulator** install the Easy Mode ViewModel resolves the manifest's
  `EmulatorLocation` and calls `ExtractionService.EnsureExecuteBits` (now public) on non-Windows.
  Verified on the VM: all three re-installed and are executable. No dedicated unit test (ViewModel
  level); the utility itself is covered by `EnsureExecuteBits_AddsTheExecuteBitsOnUnix`.
- **BUG-05 - Easy Mode Linux: `.tar.xz` emulators were rejected as unsupported. FIXED 2026-09-26.**
  DOSBox Staging and Ymir are shipped as `.tar.xz`; `IsSupportedArchivePath` accepted only
  7z/ZIP/RAR/tar.gz/tgz, so the download succeeded and the install failed with the unsupported-format
  Warning. Fix: accept `.tar.xz`/`.txz` and decompress the XZ layer (`SharpCompress.Compressors.Xz.
  XZStream`) to a seekable temporary `.tar` opened with `TarArchive` (same pattern as tar.gz), plus the
  tar permission restore. Message/log wording updated. Regression test:
  `ExtractToFolderAsync_ExtractsTarXzAndRestoresUnixExecuteBits` (builds a real tar.xz with the bundled
  7-Zip). Verified on the VM: both re-installed and executable.
- **BUG-06 - Easy Mode Linux: RetroArch (57 systems) install took hours (solid 7z). FIXED 2026-09-26.**
  `RetroArch_Linux_x64.7z` (392 MB, **19,797 files**, 1 GB uncompressed, solid) and
  `RetroArch_cores.7z` (261 MB, 199 files, 1.65 GB, solid) downloaded fine, but SharpCompress random
  access (`entry.OpenEntryStreamAsync()` per entry) re-decompresses the solid block from the start for
  every entry (O(n²)): measured 24.8 s for just the first 100 files, ~700 files in 25 minutes. Fix:
  `ExtractionService` now detects `archive.IsSolid` and extracts in a single forward pass via
  `archive.ExtractAllEntries()` (same per-entry path-traversal validation, file-time and mode
  handling), in both `ExtractToFolderAsync` and `ExtractToTempAsync`. Benchmark: the reader pass
  extracted all 19,797 files to `Stream.Null` in 21 s. Regression test:
  `ExtractToFolderAsync_ExtractsSolidSevenZipWithAllEntries` (builds a solid 7z with `-ms=on`).
  Verified on the VM: emulator install 118.7 s (download + extraction), core install 110.9 s, 199
  `_libretro.so` present at the manifest's `.AppImage.home/.config/retroarch/cores` path.
- **BUG-07 - Easy Mode cancel leaves the partial download in the temp folder (FIXED 2026-09-26).**
  Clicking **Stop Download** during an Easy Mode image-pack download cancels correctly in the UI
  (`Download of Image Pack 1 was canceled.`, Stop disabled, pack button re-enabled) but the partial
  archive stays in `/tmp/SimpleLauncher` (`arcade.zip`, 29 MB). In
  `DownloadManager.DownloadFileAsync` the user-cancel path returns at the
  `if (IsUserCancellation) return null;` catch without deleting `downloadFilePath`; the partial-file
  cleanup only runs before a retry or when the file is locked. Successful downloads also leave the
  archive in temp (removed at the next app start), but the documented checklist expectation for
  cancel is "partial file removed, clean state". Shared Core service, so the WPF app behaves the
  same. No unit test covers cancel cleanup. **Fix:** the user-cancel path in `DownloadFileAsync` now
  calls `DeleteFiles.TryDeleteFileAsync(downloadFilePath)` before returning; regression test
  `DownloadFileAsync_UserCancelDeletesThePartialFile`; re-verified live (Stop left `/tmp/SimpleLauncher`
  empty).
- **BUG-08 - Mid-body network loss stalls the download with no retry or timeout (FIXED 2026-09-26).**
  Dropping the network while the response body is streaming (iptables REJECT on the asset IPs)
  produces neither `Download error. Retrying (1/3)...` nor an error dialog: the status stays at the
  last progress, the connection stays ESTAB with zero data for >6 minutes, and the download resumes
  and completes only when connectivity returns. `DownloadManager.DownloadWithProgressAsync` reads
  `contentStream.ReadAsync(buffer, cancellationToken)` in a loop with no per-read/overall timeout,
  and the `AddStandardResilienceHandler` pipeline only wraps `SendAsync` (with
  `HttpCompletionOption.ResponseHeadersRead` the body is read after the pipeline completed). If the
  network never returns the download hangs indefinitely; the user can still click Stop. Only a
  connection failure before the body starts (host unreachable/DNS) triggers the retry path, which
  was verified working (`Retrying (1/3)` -> success after unblocking). **Fix:** `DownloadWithProgressAsync`
  now resets an idle timer before every read (`DownloadManager.StallTimeout`, 30 s) and converts the
  timeout into an `IOException`, so the existing retry loop restarts the download; regression test
  `DownloadFileAsync_StalledBodyFailsAfterRetriesAndCleansUp`; re-verified live (blocked mid-download ->
  `Retrying (1/3)` within ~30 s, partial file removed, success after unblocking).
- **BUG-09 - "Show Games" cover filter never filtered on Linux (FIXED 2026-09-26).**
  "Show Only Games Without Cover" always showed 0 games and "Show Only Games With Cover" showed all:
  `GameCardViewModel.HasCover` was `File.Exists(coverPath)` and `FindCoverImagePath` always falls back
  to `images\default.png`, so every card counted as covered. `AvaloniaGameFilterService` now treats a
  resolved default.png (or a missing path) as "without cover", mirroring the WPF `GameFilterService`.
  Verified live (Without Cover -> 2, With Cover -> 3, ShowAll -> 5 across systems); regression tests:
  `AvaloniaGameFilterServiceTests`.
- **BUG-10 - Enter did not launch from the Favorites / Play History DataGrids (FIXED 2026-09-26).**
  Avalonia's DataGrid marks Enter as handled (cell/row navigation), so the XAML `KeyDown` wiring never
  saw it and only Delete worked. Both grids now subscribe to `KeyDownEvent` in code with
  `handledEventsToo: true`; re-verified live (Enter launches from both grids, Delete still removes).
- **BUG-11 - Leaving Global Search did not cancel an in-flight search (FIXED 2026-09-26).**
  The WPF page cancels on `Unloaded`; the Avalonia port never called `GlobalSearchSection.CancelSearch()`,
  so a search kept enumerating folders after navigating away (with a 10,000-ROM system it completed and
  logged a result count after leaving). `ShowSectionAsync` now cancels when leaving the section and the
  emergency overlay release also cancels; re-verified (no completion log line after leaving mid-search).
- **BUG-12 - `mame.dat` startup notifications were skipped and the required-files dialog lied (FIXED 2026-09-27).**
  `MameDataService` was constructed before the main window existed, so `MessageBoxLibraryService.O`
  (`AvaloniaWindowContext.PlatformWindow`) was null and the missing/corrupted dialogs were silently
  skipped (a corrupt `mame.dat` failed with only a log Error). The Avalonia required-files dialog also
  showed the "Please reinstall... The application will shutdown." text with an OK button and never
  quit, while the WPF version offers a reinstall and quits. Fix: `MameManagerService.LoadFromDat` gained
  a `notifyUser` seam and reports a `MameDataLoadFailure`; `MameDataService` records the failure and
  both apps' startup initialization call `NotifyLoadFailureIfNeededAsync()` once the window exists
  (corrupt -> Yes/No auto-reinstall dialog, No quits); `HandleMissingRequiredFilesMessageBoxAsync` was
  ported to the WPF Yes/No + reinstall/quit flow. Live-verified on the rebuilt payload; regression tests
  in `SimpleLauncher.Avalonia.Tests/MameDataServiceTests.cs` (4 tests; Avalonia 686/686, WPF 2122/2122).
- **BUG-13 - the portable credential fallback was logged at Warning, so Linux users filed bug reports (FIXED 2026-09-27).**
  `WindowsCredentialProtector.WarnPortableFallback` logged "DPAPI is not available on this platform;
  RetroAchievements credentials are stored obfuscated (Base64) instead of encrypted" at Warning level.
  DPAPI is Windows-only, so on Linux this fires on the first RA credential save/load and the
  `BugReportApiSink` (Warning+ events) reported it as a bug - it even landed in `error_user.log`.
  Now logged at `Information` (expected platform condition per AGENTS.md); verified on the VM (no new
  entry after the fixed Core was deployed). Note: the credentials really are only Base64-obfuscated on
  Linux (portable fallback, by design) - documented as a limitation, not changed.
- **BUG-14 - failing batch files/executables were logged at Warning/Error, so users filed bug reports (FIXED 2026-09-27).**
  When a launched `.bat` (Sega Model 3) or direct executable exits non-zero - e.g. the emulator
  rejects an incomplete ROM set - the Avalonia `LauncherService` logged `Warning` ("Batch file exited
  with code 1"/"Executable exited with code") and the WPF `GameLauncherService` logged `Error`. Both
  are expected user conditions (the user's data/emulator failed) and reached the `BugReportApiSink`.
  Now `Information`; the user-facing toast/status/message box is unchanged.
- **BUG-15 - DOSBox file-selection list items exposed the CLR type name to screen readers (FIXED 2026-09-27).**
  `DosBoxFileItem` had no `ToString()`, so the Avalonia ListBox items' accessible name (and any
  fallback rendering) was `SimpleLauncher.Core.Models.DosBoxFileItem`. Added a `ToString()` override
  returning `DisplayName`; the dialog's file list now reads the real file names (verified live via
  AT-SPI: the items were `DosBoxFileItem` before, and the DOSBox launch itself worked).
- **BUG-16 - Easy Mode "Add System" appeared stuck on the loading overlay (FIXED 2026-09-28).**
  Message dialogs were always owned by MainWindow while Easy Mode is a modal child of the same window,
  so on X11 the success dialog could stack behind it and the overlay never cleared.
  `MessageBoxLibraryService.GetDialogOwner` now prefers the active window; verified with
  `WM_TRANSIENT_FOR` and a full add. See the third-session section for details.
- **BUG-17 - card overlay buttons ignored the Options menu settings (FIXED 2026-09-28).** Cards now
  render the three WPF-parity overlay buttons (RA/video/info) driven by the menu settings, with an
  in-place refresh on toggle; clicks mirror the context menu without launching the game.
- **BUG-18 - the card-size slider did not update the Button Size menu (FIXED 2026-09-28).** All size
  paths now snap to the 50-px menu options and keep settings, menu check mark and slider in sync
  (`MainViewModel.ApplyButtonSize`).
- **BUG-19 - rapid aspect-ratio clicks froze the app permanently (FIXED 2026-09-28).**
  `PlaySoundEffects` stopped/disposed the NAudio player synchronously on the UI thread; a wedged
  PipeWire ALSA stop (reproduced with 300 clicks @15 ms) blocked the UI forever. Playback now runs on
  a dedicated background thread with a blocking-stop regression test.
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
- **`Nav.open_system()` (rewritten 2026-09-26)** returns to the card screen (Escape until the
  `SystemComboBox` disappears) and clicks the requested card, so it works from the game browser,
  Favorites and Play History pages alike. The status bar (`System:` label) is the authoritative
  "loaded system"; the combo can lag behind it. The filter button "All" means "All Games" across
  systems, and a system's games only appear after that system was opened once in the session.
- **Search triggers on Return** (not the Search button); the no-match state shows "No Games Found"
  and pagination "0 to 0 out of 0". The filter status text is `Filtering by B` (not just `B`).
- **Single-instance enforcement on Linux (FIXED 2026-09-26).** .NET named mutexes are scoped to the
  login session on Unix (`/tmp/.dotnet/shm/sessionNNNN`), so two instances started from different
  sessions - exactly what `setsid` does in the harness - both saw `createdNew=true` and ran. Both apps
  now also take a per-user lock file (`SingleInstance.TryAcquireLockFile`, `FileShare.None`,
  `~/.local/share/SimpleLauncher/single-instance.lock`), which is enforced across processes and
  sessions; the named mutex/event remain for the Windows focus signal. `Stop-VmApp` now kills every
  matching instance (including launches with `-debug`).
- **AT-SPI registration flakiness (environment).** After a rapid app restart the app occasionally
  never registers with the a11y bus (empty tree; the X window still renders). `Start-VmApp` waits up
  to 60 s for the frame and retries the launch (up to 3 starts); the 2026-09-26 full run needed 2
  transparent retries. Never kill `at-spi-bus-launcher`/`at-spi2-registryd` as a "fix": the X root
  `AT_SPI_BUS` property keeps a stale guid and then no new app can register - reboot the VM instead.
- **Accessibility gaps:** the system-selection cards are code-created buttons with a `StackPanel`
  content and no `AutomationProperties.Name` (AT-SPI name is `Avalonia.Controls.StackPanel`), so the
  harness clicks the card's name label; the Edit System Help button is named "Open the parameters
  wiki" with a `? Help` content label (find it via the label). `AvaloniaAccessibilityTests` scans
  XAML only, so code-created controls would need a separate guardrail if these are fixed.
