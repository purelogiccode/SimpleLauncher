# 17 — Release Notes

> Condensed changelog. The canonical, fully detailed file is `SimpleLauncher\WhatsNew.md` (shown in-app via **Help → Update History**).

## 5.8.2 — 2026-10-20 (current)

- **Avalonia system loading fixed**: selecting a system no longer leaves the "Loading system..." overlay stuck. The game-card template used runtime type casts (`((vm:MainViewModel)DataContext)...`) that Avalonia cannot resolve inside deferred templates, so realizing the first card threw on the UI thread; the bindings now use `DataContext.Property` and the game grid renders normally. The same unsafe cast was removed from the image-pack download window.
- **Guard test**: XAML binding paths using runtime type casts now fail the Avalonia test suite.
- **Packaging**: Windows and Linux bundles rebuilt as 5.8.2 (framework-dependent single-file, documents included).

## 5.8.1 — 2026-10-19

- **Faster Avalonia startup**: the system-selection grid is shown before the ROM folders are scanned; sidebar count badges refresh in the background and system icons resolve synchronously with a short directory cache.
- **Shutdown deadlock**: exiting while a RetroAchievements hash scan was running no longer leaves the process alive — the shutdown wait resumes on the thread pool instead of the blocked UI thread.
- **Single-instance guard**: the shared named mutex is created and owned only by the instance allowed to run, and released only by its owner, so simultaneous WPF/Avalonia launches no longer abandon it or throw `ApplicationException` on exit (#68430).
- **Updater hardening**: interrupted `.updbak` swaps are recovered only for the staged payload's own destinations (never by scanning the install drive), read-only files are replaced and transient access denials retried, archive-viewer temp folders are refused with extract-first guidance, restart access-denied is treated as an expected condition, and a closed updater window falls back to an ownerless dialog (#68272–#68441).
- **XISO mount diagnostics**: SimpleXisoDrive stdout/stderr and its exit code are captured (thread-safely) and reported at Information level, so unsupported images and missing drivers no longer generate duplicate bug reports (#68434–#68437).
- **Avalonia launcher**: launch exceptions are classified before logging, keeping elevation-required, UAC-cancel and invalid-executable conditions out of bug reports (#68419).
- **Code quality**: third InspectCode cleanup batch (unused dependencies/parameters removed, design-time types and typed binding paths in XAML, field-backed properties, ISO9660 root-record validation tightened); builds remain at zero warnings.
- **Dependencies**: Meziantou.Analyzer 3.0.314, NAudio 3.3.0, SharpCompress 0.50.6, CHDSharp 1.5.0, Markdown.Avalonia 12.0.2, Microsoft.Extensions.Http.Resilience 10.11.0.
- **Packaging**: every release and updater ZIP now carries `LICENSE.txt`, `README.md` and `WhatsNew.md`; the apps remain single-file framework-dependent builds (the .NET 10 runtime is required and never embedded).
- **CI & docs**: CI now builds Debug **and** Release with `-warnaserror`, compiles the Linux updater, and a four-RID packaging dry run verifies the zips and Linux executable bits; the Pages site and wiki share one side menu and the dark/light theme.
- **Tests**: 2208 WPF + 704 Avalonia fast tests; 83 WPF + 11 Avalonia slow tests.

## 5.8.0 — 2026-10-14

- **Unified Windows release**: one zip ships `SimpleLauncher.exe` (WPF) and `SimpleLauncher.Avalonia.exe` (Avalonia) side by side with a single shared `Updater.exe` that relaunches whichever app started it; single-instance guard shared across both apps.
- **Unified SQLite settings database** (`settings.dat`, WPF + Avalonia): favorites, play history, app/emulator settings, per-system play times and system configs in one store; automatic legacy migration (`settings.xml`, `system.xml`, `favorites.dat`, `playhistory.dat`) with verification + `.bak` shelving, merge into an existing database, read-only recovery, and cross-app rename/delete consistency.
- **WPF top-bar card-size slider** for game thumbnails (debounced, synced with the menu and Ctrl+wheel).
- **Shared localization packs**: one JSON pack set (18 languages, 2671 keys) in `SimpleLauncher.Core\Localization`; WPF embeds them, Avalonia embeds them in the assembly; old `strings.*.xaml` packs removed; Avalonia message boxes fully localized.
- **Xbox containers via SimpleXisoDrive**: xemu and Cxbx-Reloaded open Xbox games in `.iso`/`.xiso`/`.cso`/`.zar` — and Xbox-ISO `.chd` (SimpleXisoDrive preferred, CHDMounter fallback) — on Windows (Dokan) and Linux (FUSE 3); xemu receives the virtual `image.iso`, Cxbx the mounted `default.xbe`.
- **Linux FUSE mounting**: ZIP/7Z/RAR launches mount through SimpleZipDrive 3.1.1 and Xbox images through SimpleXisoDrive 1.5.0 instead of extracting to temp; CHDMounter is cross-platform; extraction/conversion remain automatic fallbacks.
- **RetroAchievements hashing**: batch scans use the bundled CLI's positional interface with resume-after-failure (`CurrentHashVersion = 2`; existing caches recalculate automatically), system classification follows the CLI's console switch, and hash-support decisions are resolved once per system instead of once per game card (no more thousands of log lines on PS3 libraries).
- **Avalonia parity fixes**: letter buttons reload the complete library after filtered views; system-icon zoom (Ctrl+wheel / buttons, 50/100/150 px, persisted); Favorites/Play History/Global Search hide the top system bar; Global Statistics buttons auto-size to the localized text; Edit System no longer claims a missing legacy `system.xml`.
- **Updater**: the Avalonia updater's OK/Yes/No dialogs now close on click so the update flow resumes; single-file emulator downloads install directly; cancelled downloads delete partial files and stalled transfers retry.
- **Reliability/security hardening**: Zip-Slip fail-closed extraction without wiping destination folders, partial-file cleanup, download cancel/path sanitization, updater staged-swap rollback, launch gating + invalid executable handling, gamepad/hash-pipe deadlocks, shutdown/tray-icon stall, play-history locking, RA account/system/hash hardening.
- **Avalonia fixes**: Edit System help rendering, system-selection status bar, RA no-credentials panel, clicked-card load after Edit System, in-app path guard, INI encoding, WPF-matching title; embedded tray/about/link icons, wait overlay for long operations, selected-system info panel, owned message dialogs, background-thread UI sounds (a wedged audio stop can no longer freeze the UI).
- **Linux support (beta)**: framework-dependent single-file Avalonia bundles for `linux-x64` / `linux-arm64` (the **.NET 10 runtime** is required) plus the Linux `Updater` in the same GitHub release; native launch pipeline (temp extraction, managed CHDSharp conversion, DOSBox `imgmount`, bundled `7zz`), XDG data folder, managed MP3/WAV audio, SDL2 gamepad navigation, platform-aware pickers/validation. Windows-only features (Dokan mounting, storefront scanning, RA config injection, screenshot/hotkey, Windows-only Tools menu entries) are hidden/no-ops. **Beta** — verified on Ubuntu 24.04 x64 and Fedora 44 KDE; ARM64 structurally verified only; please report issues.
- **Bundled tools**: XISOStudio/CHDStudio/RVZStudio replace the old batch converters; CHDStudio 3.9.4, RomValidator 2.9.2, SimpleZipDrive 3.1.1, FindRomCover 3.3.0 (now also Linux), RetroAchievementsSharp 1.0.0 per-RID CLI; per-RID tool folders; obsolete flat tool payloads are pruned on startup.
- **Release packaging**: both scripts copy `LICENSE.txt` / `README.md` / `WhatsNew.md` into every payload; the Linux bundles ship framework-dependent single-file binaries.
- **Accessibility**: every interactive control exposes an accessible name/automation id (Narrator/NVDA on Windows, Orca/AT-SPI on Linux); guardrail tests fail on missing names.
- **Tooling**: vendored PBPSharp replaced by NuGet 1.1.1 (final: 1.1.4); Avalonia 12.1.4, Roslynator 5.0.1, Meziantou.Analyzer 3.0.303, NetAnalyzers 10.0.402, Microsoft.Extensions.* / Microsoft.Data.Sqlite 10.0.13, MessagePack 3.1.12, Http.Resilience 10.10.0, NAudio 3.1.0, Microsoft.NET.Test.Sdk 18.11.0.

## 5.7.0 — 2026-09-07

- **Hash-based RA compatibility filter**: matches games by real ROM hash (archive-aware, background scanning with progress toasts) via the bundled RetroAchievementsSharp CLI.
- **Update flow**: multi-source fallback chain (primary GitHub repo → transferred org → `assets.purelogiccode.com`), updater process-handle fixes, 5-minute download timeout, toast notifications.
- **Launch fixes**: actionable dialogs for missing emulator executables and invalid executables (Win32 193), Ymir force-extraction, elevation guidance (both apps), parameter API validation.
- **Favorites & play history**: rename/remove sync, subfolder resolution, protection against wiping favorites when `system.xml` fails.
- **Other**: Easy Mode temp-folder fix, unified log-path resolution, storefront icon extraction fix, Windows/Microsoft folders creation, Raine config null-path fix, Edit System stability, standalone Debug window.
- **Tooling**: NAudio 3, Markdown.Avalonia 12, Meziantou.Analyzer 3.0.224.

## 5.6.1 — 2026-08-11

- **Update check fallback**: when the GitHub API is unreachable, the check falls back to the secondary server (`assets.purelogiccode.com` `version.txt`) and resolves both the release and updater packages; the Updater also retries the download from the secondary server when the primary source fails.
- **List-view right-click "Launch Game" fix**: the context menu now uses the selected emulator (falling back to the system's first emulator) instead of always failing.
- **Bug-report noise reduction**: expected conditions (GitHub 403/429, image-download timeouts after retry, missing files on launch/delete, updater "process not found", a GitHub release without packages for the current platform (LB-23), context-menu validation, unsupported `--language` codes) are logged at Information level and no longer sent to the bug report service.
- **`--language` startup argument**: launch with a specific language; unsupported codes fall back to English silently.
- **Localization fixes**: `strings.es.xaml` and `strings.tr.xaml` embedded as pack resources (bugs 64899-64904).
- **Updater UI**: log text and progress bar now white.
- **Misc**: `%ROMSYSTEMFOLDER%` documented in `parameters.md`, `Projec(t)64.exe` added to the emulator error-check skip list, RetroAchievements matcher addition, stale test filter removed (all tests run by default).

## 5.6.0 — 2026-08-06

- **Parameter Resolver API**: "Suggest" buttons in Edit System (AI-powered parameter suggestions with confirmation dialog), `Explanation:` prefix handling, loading overlay, URL normalization with fixed 60 s timeout.
- **Game File Watcher**: `GameFileWatcherService` auto-refreshes the game list on external ROM changes (500 ms debounce, start/stop per system).
- **7za fallback extraction** when SharpCompress fails; bundled 7za binaries across conversion tools.
- **Batch file path validation** before execution; `UseShellExecute` instead of `cmd.exe /c`.
- **OneDrive error guidance**, invalid folder-character validation, CHD support for Kega Fusion + exit-code reporting + Dokan check, **CHD mounting on ARM64** (architecture-specific CHDMounter).
- **PCSX2** permission-error detection + portable config path resolution.
- **History database migration**: `history.xml` → `history.dat` (MessagePack) with fallback.
- **Global search**: results count, AND/OR operators, better loading state.
- **Downloads**: automatic retry with exponential backoff; read-only file handling.
- **Startup robustness**: fallback logger, improved init order, timeouts for silent update check & stats, temp-folder check first.
- **F8 hotkey conflict warning**; **AI parameter suggestions** polish; **UI/UX** (cancel buttons, tooltips, loading overlays, confirmation dialogs); **encrypted RA credentials** (Windows Credential Manager/DPAPI); nested emulator settings structure.
- **Debug Window** restored (constructor + theme), `-debug` flag, `parameters.md` path fix; quote trimming.
- **GameCoverScraper removed** (use Find Rom Cover / Retro Game Cover Downloader).
- **Serilog structured logging** everywhere (`ILogger`, sinks for Debug Window, rolling file 7 days, bug reports).
- Massive **DI** + **MVVM** refactoring; nullable reference types; Meziantou analyzer; Moq added.
- Dependency updates: SharpCompress 0.50.4, Tomlyn 2.6.0, MessagePack 3.1.8, Sqlite/Extensions 10.0.10, Serilog 4.4.0, Meziantou 3.0.139, Moq 4.20.72, and more.

## 5.5.0 — 2026-05-25

Commander Genius support with an intelligent launch strategy (extract to `Documents\Commander Genius\games\`, detect nested game folder, `dir=` parameter, cleanup after exit).

## 5.4.0 — 2026-05-10

`%ROMSYSTEMFOLDER%` placeholder (system folder containing the selected ROM).

## 5.3.x — 2026-04

- **5.3.3**: improved `.bat` file support.
- **5.3.2**: auto-updater refactored into clean service architecture (`UpdateService`, `GitHubService`, `DownloadService`, …).
- **5.3.1**: fixed HTTP request timeouts (15 s) for bug reports, stats, support requests.
- **5.3.0**: **Nintendo Wii** support (official RA support).

## 5.2.0 — 2026-03-28

Virtual-drive system built around Dokan with a custom wrapper tool (SimpleXisoDrive/SimpleZipDrive).

## 5.1.0 — 2026-02-14

Store Xbox/Xbox 360 ISOs in **CHD** format and convert back to ISO on the fly at launch.

## 5.0.0 — 2026-02-11

Replaced the monolithic launcher conditionals with the **strategy-based launch pattern** (`ILaunchStrategy`) for extensibility.

## 4.9.x — 2026-01

- **4.9.1**: Microsoft NuGet package updates.
- **4.9.0**: Emulator auto-configuration on setup (RetroArch etc.).
- **4.8.0**: `ScanSteamGames` (Steam library integration).

## 4.7.0 — 2025-12-07

**Group Files by Folder** for multi-file MAME games (Software List CHDs/ROMs).

## 4.6.0 — 2025-10-30

Introduced `GameCoverScraper` (later removed in 5.6.0).

## 4.5.0 — 2025-10-11

(Error handling / parameter validation / UX improvements.)

## 4.4.x — 2025-09

- **4.4.2 / 4.4.1**: error handling, parameter validation, UX improvements.
- **4.4**: **Windows-arm64** support introduced.

## 4.3 — 2025-07-26

**Multiple System Folders**: each system can scan across multiple ROM folders.

## 4.2.0 / 4.1.0 — 2025-06/07

(Stability and incremental improvements.)

## 4.0.x — 2025-05

- **4.0.1**: parameter handling overhaul — paths in parameters converted to absolute (`ParameterValidator`, `GameLauncher`).
- **4.0**: new visual **System Selection Screen**.

## 3.13 — 2025-04-27

Batch Convert To Compressed File skips already-compressed files.

## 3.12 — 2025-03-31

Per-emulator option to turn off error notifications.

## 3.11.x — 2025-03

- **3.11.1**: file/folder path checks in the parameter field.
- **3.11**: **Play History Window** (games played + duration).

## 3.10.x — 2025-02/03

- **3.10.2**: fixed List View preview images.
- **3.10**: gamepad dead-zone exposed to the user.

## 3.9.x — 2025-02

- **3.9.1**: much faster search engine (main window + global search).
- **3.9**: enhanced Xbox + **PlayStation controller** support.

## 3.8.x — 2025-01

- **3.8.1**: bug fixes.
- **3.8**: **17-language translations** introduced.

## 3.7.x — 2024-12

- **3.7.1**: bug fixes/exception handling.
- **3.7**: BAT file generator for Xbox 360 XBLA games.

## 3.6.x — 2024-11

- **3.6.3**: temp folder moved to the Windows temp folder.
- **3.6.1**: relative `UserImagePath` preview fix.
- **3.6.0**: major `EditSystem` validation refactor.

## 3.5 — 2024-11-09

BAT file generator for Sega Model 3.

## 3.4.x — 2024-11-03

- **3.4.1**: automatic `SystemImageFolder` default (`.\images\SystemName`).
- **3.4**: new **Tools** menu.

## 3.3.x — 2024-10

- **3.3.2**: Edit System input trimming/validation.
- **3.3.1**: MAME warning-only output handling fix.
- **3.3.0**: **Image Packs** for several systems.

## 3.2 — 2024-10-22

Image-pack downloads enabled.

## 3.1 — 2024-07-18

File check before launching a favorite game.

## 3.0 — 2024-07-17

**Themes** introduced.

## 2.x — 2023-2024

- **2.15.1**: favorite star icons · **2.15**: EasyMode emulator updates · **2.14.4**: Global Search `AND`/`OR` · **2.14.3-1**: update-mechanism fixes · **2.14.0**: **Easy Mode** · **2.13.0**: experimental parameter checker · **2.12.1**: error notifications · **2.12.0**: system/image/emulator path checks · **2.11.x**: search engine, cover handling · **2.10.0.10**: vertical scroll fix · **2.9.0.90**: show-all-games option · **2.8.0.5**: **Edit System** menu · **2.7.0.1**: gamepad auto-toggle · **2.6.2.3**: `.bat` files as games · **2.4**: `mame.xml` database · **2.3**: thumbnail/gamepad menu items · **2.2**: **.NET 8.0** upgrade · **2.1**: system.xml fixes · **2.0**: major UI overhaul.

## 1.x — 2023

- **1.3**: Xbox controller support · **1.2**: fixes + CHD support · **1.1** (2023-08-29): initial release.

## Related docs

- [15 — Development](15-development.md) (version bumping)
- [16 — Updater](16-updater.md)
- `SimpleLauncher\WhatsNew.md` (canonical changelog)
