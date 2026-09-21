# Release 5.8.0
*2026-09-18*
---

## Unified Windows Release (WPF + Avalonia)
- **One download for both apps** — The release zip now places `SimpleLauncher.exe` (WPF) and `SimpleLauncher.Avalonia.exe` (Avalonia) side by side with a single shared content set (`images/`, `tools/`, `samples/`, `appsettings.json`) and ships one `Updater.exe` that updates either app.
- **Shared single-instance guard** — Only one of the two apps can run at a time; launching the other brings the running instance to the foreground.
- **Updater relaunches the app that started it** — and still works on legacy WPF-only installs.

## Unified Settings Database (`settings.dat`)
- **One SQLite store for both apps** — Favorites, play history, application settings, emulator settings, per-system play times, and system configurations now live in a single `settings.dat` in `%LocalAppData%\SimpleLauncher` (or the portable folder).
- **Automatic migration** — The first launch migrates `settings.xml`, `system.xml`, `favorites.dat`, and `playhistory.dat`, verifies the result, and shelves the legacy files as `.bak`; on failure nothing is changed and the next launch retries.
- **Merge support** — If a database already exists, any remaining legacy files are merged into it without losing database-only data or duplicating entries.
- **Read-only database recovery** — A read-only `settings.dat` is detected and recovered instead of crashing; failed saves are observed and logged.
- **Cross-app consistency** — Systems renamed or deleted in one app stay in sync with favorites and play history.

## WPF: Card Size Slider
- **Top-bar slider** — Resize game thumbnails with a slider next to the view controls (debounced when dragging), kept in sync with "Set Button Size" and Ctrl+Mouse wheel zoom.

## Shared Localization
- **One JSON pack set for both apps** — The 18 language packs (2671 keys each) now live once in `SimpleLauncher.Core\Localization`; WPF embeds them as pack resources and Avalonia embeds them in its assembly.
- **Legacy XAML resources removed** — The per-language `strings.*.xaml` files are gone.
- **Avalonia message boxes fully localized**.

## Reliability & Security
- **Extraction** — Zip-Slip validation fails closed, extraction no longer wipes destination folders (only files written by the failed run are removed), partial files are cleaned up, and locked archives are retried.
- **Downloads** — User cancellation is honored promptly, downloaded file names are sanitized against path traversal, and failures retry.
- **Launching** — Launch gating, rename migration, elevation handling, and invalid/missing executable dialogs were repaired; user cancellations and invalid executables are no longer reported as bugs.
- **Updater** — Staged-file swap with rollback safety, path containment checks, safe restart, and retry, so a failed update no longer leaves a half-updated install.
- **Stability** — Fixed a gamepad-reconnect deadlock, a hash-pipe deadlock, a shutdown stall that left a clickable orphan tray icon, and play-history races (now serialized under lock); debug logging buffers are bounded.
- **RetroAchievements** — Hardened account handling, system-name matching, favorites/play-history sync, emulator credential injection, hashing and extraction paths.
- **Avalonia fixes** — Edit System help rendering, status bar visibility in the system selection screen, no-credentials panel in the RA window, loading the clicked system card after closing Edit System, in-app path guards, INI encoding, and a window title matching WPF.
- **Filtering** — Accent-insensitive game filtering restored, and Commander Genius archive deletion fixed.

## Linux Support (Avalonia) — Beta
- **Beta** — Linux support is in beta: it was tested end-to-end on Ubuntu 24.04 x64 and Fedora 44 KDE (GUI smoke-tested on Wayland); the ARM64 bundle is structurally verified only. Please report issues on GitHub.
- **Native Linux builds** — The Avalonia app now ships self-contained single-file bundles for `linux-x64` and `linux-arm64` (one `SimpleLauncher.Avalonia` binary plus its native libraries, like the Windows exes; `release_{version}_linux-*.zip` plus `updater_linux-*.zip`); the release workflow publishes them next to the Windows bundles and the in-app updater resolves the Linux assets.
- **No Dokan needed on Linux** — ZIP/7z/RAR games extract to a temp folder on launch, CHD images are converted on the fly with the managed CHDSharp library and mounted natively in DOSBox (`imgmount`), and DOSBox ISO games use the same native mount instead of the Windows PowerShell path.
- **Native Linux behavior** — App data lives in the XDG folder (`~/.local/share/SimpleLauncher`), Commander Genius uses `~/.CommanderGenius`, paths are handled POSIX-style with case-sensitive matching, and the extension-less bundled 7-Zip fallback (`7zz`) runs correctly. SDL2 gamepad navigation is supported.
- **Sound works out of the box** — MP3/WAV UI sounds decode through managed decoders (NLayer + NAudio), so no extra packages are required; FLAC/Ogg/Opus use the system `libsndfile`. Missing audio libraries/devices are logged as information instead of being reported as bugs.
- **Platform-aware pickers and checks** — The emulator pickers no longer hide extension-less Linux binaries, `.AppImage`/`.sh`/`.run` files are accepted, and file dialogs attach to the active window (Wayland).
- **Windows-only features are hidden on Linux/macOS** — Store scanning, Dokan mounting, Take Screenshot, the RetroAchievements emulator-configuration section and the Windows-only Tools menu entries.
- **23 Linux port findings fixed (LB-01…LB-23)** — Case-insensitive ROM/EXE discovery after extraction, backslash archive entry names, execute bits for bundled tools, CHD temp-file cleanup, mount platform guards, updater Zip-Slip comparison, platform-neutral dialog wording, defensive scan-handler gating, and Information-level logging when a release ships no update assets for the current platform.

## Avalonia Fixes
- **Tray icon** — Embedded in the Avalonia app (and as a pack resource in WPF) with `icon.ico` shipped in the payload, so the tray icon always has an image.
- **Wait overlay** — Shown for all long-running operations, matching the WPF app.
- **System info panel** — The selected system's configuration summary now appears after selection and hides when games load; the game cache is invalidated after system edits instead of showing stale games.
- **Aspect-ratio changes** — Card heights refresh through the binding instead of rescanning the library.
- **Filename preferences stay instant** — Switching the filename display mode (Original / Clean Up / No Filename) or the machine-name toggle no longer rescans the entire library under the loading overlay; captions refresh in place, keeping the open system, filters, pagination and scroll position.

## Migration & Scan Hardening
- **Migration gate** — Database wins on merge (a reappearing stale file can never revert newer settings), a defaults-only `settings.xml` is ignored, newer-schema databases are left untouched, a timestamped pre-merge backup is taken, and the WPF and Avalonia migrations parse the legacy files identically.
- **Store scan handler** — The async handler now catches and logs unexpected failures so they cannot escape as an unhandled exception.

## Tooling
- **PBPSharp** — Replaced the vendored library with the NuGet package **1.1.1**.
- **Bundled tools** — SimpleZipDrive updated to **3.0.1** (x64 and ARM64); refreshed the default game image.
- **Dependencies** — Meziantou.Analyzer **3.0.264**, Microsoft.CodeAnalysis.NetAnalyzers **10.0.401**, Microsoft.Extensions.* / Microsoft.Data.Sqlite **10.0.12**, Microsoft.Extensions.Http.Resilience **10.10.0**, NAudio **3.1.0**, NLayer **3.0.0** + NLayer.NAudioSupport **3.0.0** (managed MP3 decoding on Linux/macOS).
- **Zero analyzer warnings** across all projects; both test suites pass.

---

# Release 5.7.0
*2026-09-07*
---

## RetroAchievements Compatibility Filter (Hash-Based)
- **Hash-based filtering** — The "RetroAchievements-compatible games" filter now matches games by real ROM hash instead of fuzzy name matching.
- **Background hash scanning** — Scans run off the UI thread with progress toasts, so the app never freezes while calculating hashes.
- **Archive-aware hashing** — ROM content is hashed directly from `.zip` archives (and per-game zips) without extraction, so zipped games match their RetroAchievements entries.
- **Bundled hashing tool** — Hashing is delegated to a bundled RetroAchievementsSharp CLI tool for accuracy and performance.
- **Updated RetroAchievements database** — Refreshed RetroAchievements.dat with new systems and games.

## Update Flow
- **Multi-source update fallback chain** — Update checks now try the primary GitHub repository, then the transferred GitHub organization, then the secondary server (`assets.purelogiccode.com`), so updates keep working through the GitHub organization transfer.
- **Updater fixes** — Fixed an updater bug where waiting for the app to exit could throw `InvalidOperationException` and abort the update; the updater now disposes process handles safely.
- **Download resilience** — Raised the download timeout to 5 minutes so slow connections can complete large update packages.
- **Toast notifications** — Replaced tray balloon notifications with modern toast notifications.

## Launch Fixes
- **Missing emulator executable** — Launching with an emulator whose executable no longer exists now shows an actionable dialog (with the configured path) instead of a generic error.
- **Invalid executable format** — Games pointing at invalid executables (Win32 error 193) now show a direct, localized dialog explaining the problem instead of an error-log prompt.
- **Ymir** — Archives are force-extracted before launching Ymir, since it does not support mounted archives.
- **Elevation** — The elevation message now instructs users to disable the "Run as administrator" requirement on the emulator, and the Avalonia app gained an equivalent elevation dialog.
- **Parameter suggestions** — System names are validated before calling the ParameterResolver API; API rejections and network failures are logged as info and handled gracefully.

## Favorites & Play History
- **Rename/remove sync** — Favorites and play history now stay in sync when systems are renamed or removed, in both the WPF and Avalonia apps.
- **Subfolder resolution** — Favorites and play-history entries pointing to files stored in system-folder subfolders resolve correctly again.
- **Protection against wiping favorites** — Favorites are never wiped when the systems file fails to load (e.g., corrupt or transient `system.xml` failure).

## Easy Mode
- **Temp folder fix** — Easy Mode downloads no longer fail when startup cleanup removes the temp folder; the folder is re-created on demand.

## Other Fixes
- **Unified log path resolution** — WPF and Avalonia now resolve log files identically; opening the log from dialogs always opens the actual rolling log file.
- **Storefront icon extraction** — Fixed a `DestroyIconW` entry-point error that threw on every successful icon extraction (handle leak + false error reports).
- **Microsoft Windows folders** — Windows/Microsoft Games folders are created before store scans, with a warning for protected locations.
- **Raine config window** — Opening the Raine configuration window with a null emulator path no longer crashes; it prompts for the executable instead.
- **Edit System stability** — Fixed a crash from WPF-only cursor definitions and refreshed sidebar counts after the dialog closes.
- **Debug window** — No longer owned by the main window, so it stays usable independently.

## Tooling
- **Dependencies** — NAudio 3, Markdown.Avalonia 12, Meziantou.Analyzer 3.0.224 and other library updates.

---

# Release 5.6.1
*2026-08-11*
---

## Update Check Fallback (Secondary Server)
- **Fallback when GitHub is unreachable** — If the GitHub API cannot be reached (offline, rate-limited, blocked), the update check now falls back to the secondary server (`assets.purelogiccode.com`) using its `version.txt` file and hosted release packages.
- **Full update support on fallback** — The fallback path resolves both the release package and the updater package, so the update window and automatic update work exactly as when GitHub is reachable.
- **Updater download fallback** — If downloading the release from the primary source fails, the Updater retries from the secondary server automatically (progress bar resets between attempts).

## Launch Fixes
- **List-view right-click "Launch Game"** — Right-clicking a game in the list view and choosing "Launch Game" now launches with the emulator selected in the UI (falling back to the system's first configured emulator). Previously this path always failed with "SelectedEmulatorName is null or empty".

## Language Launch Argument
- **`--language` startup argument** — Launch SimpleLauncher with a specific language, e.g. `SimpleLauncher.exe --language es`. Supported codes load at startup; unsupported codes fall back to English without error.

## Localization Fixes
- **Spanish & Turkish resources** — `strings.es.xaml` and `strings.tr.xaml` are now embedded as pack resources and load correctly (bugs 64899-64904).

## Updater UI
- **White log text and progress bar** — The Updater window's log text and progress bar foreground are now white for a cleaner look.

---

# Release 5.6.0
*2026-08-09*

## Parameter Resolver API
- **"Suggest" buttons** — Added to the Edit System window for every emulator parameter field.
- **AI-powered suggestions** — An AI-powered API suggests the best emulator parameters based on the system configuration.
- **Confirmation before applying** — Shows a dialog with the suggested parameter and an explanation before applying it.
- **`Explanation:` prefix handling** — If the AI response starts with "Explanation:", that text is treated as the explanation and is no longer inserted into the emulator parameter field.
- **Loading overlay** — Added a loading overlay to AI parameter resolution.
- **URL normalization** — The AI parameter resolver URL is normalized (trailing slash) and uses a fixed 60-second timeout.

## Image Matching Improvements
- **Annotation stripping** — New `EnableAnnotationStripping` option strips region tags, revision numbers, and other annotations from filenames for better cover image matching.

## Game File Watcher
- **New `GameFileWatcherService`** — Monitors ROM system folders for external file changes (create, delete, rename) and automatically refreshes the game list when changes are detected.
- **Debouncing (500ms)** — Avoids rapid re-scans during batch file operations like archive extraction.
- **Automatic start/stop** — Watching starts and stops automatically when switching between systems.

## 7za Fallback Extraction
- **Automatic fallback to 7za** — When SharpCompress fails to extract `.7z` files, the application now automatically retries using the bundled `7za.exe` (or `7za_arm64.exe` on ARM64).
- **Bundled binaries** — Added 7za binaries to `tools/SevenZip/` and `tools/BatchConvertIsoToXiso/`, plus `7za.exe` / `7za_arm64.exe` to `BatchConvertToRVZ` and `BatchConvertToCHD` for fallback extraction/conversion.

## Batch File Path Validation
- **Pre-execution validation** — Batch files are now validated before execution to detect referenced paths that do not exist.
- **Warning dialog** — Shows a dialog listing the missing paths with the option to continue or cancel.
- **Direct execution** — Batch files are now executed with `UseShellExecute` instead of `cmd.exe /c` for better compatibility.

## OneDrive Error Handling
- **Specific OneDrive guidance** — When a game file fails to launch and its path contains "OneDrive", the error message now provides specific instructions about syncing and downloading the file.
- **Localized message** — Added the localized `oneDriveIssue` string across all supported languages.

## Invalid Folder Character Validation
- **Path character validation** — System folder and image folder paths are now validated for invalid path characters before attempting directory creation.
- **Clear error message** — Shows a message listing the invalid characters found.

## CHD Support
- **Kega Fusion** — Added CHD support for the Kega Fusion emulator.
- **Exit code reporting** — CHD mount failures now include the process exit code in error messages for easier debugging.
- **Dokan check** — Added a Dokan installation check before CHD mount operations.
- **ARM64 support** — CHD images are now mounted using the architecture-specific CHDMounter binary (`CHDMounter.exe` on x64, `CHDMounter_arm64.exe` on ARM64), enabling CHD mounting on Windows ARM64. The tool now ships with a README.

## PCSX2 Improvements
- **Permission error detection** — PCSX2 configuration injection now detects file permission errors and shows a user-friendly notification instead of crashing.
- **Portable mode** — PCSX2 configuration injection now resolves the config file correctly when PCSX2 runs in portable mode.

## History Database Format Migration
- **MessagePack format** — Migrated `history.xml` to `history.dat` (MessagePack binary format) for faster loading and smaller file size.
- **Backward compatibility** — Falls back to `history.xml` if `history.dat` is not found.

## Global Search Enhancements
- **Results count** — Shows the number of matching results in the global search window.
- **AND/OR operators** — Supports boolean operators for more flexible search queries.
- **Improved loading state** — Better visual feedback during search operations.

## Debug Logger Improvements
- **Buffered logging** — Debug log messages are now buffered for better performance.

## Download Reliability
- **Automatic retry** — Downloads now automatically retry with exponential backoff on failure.
- **Read-only file handling** — Extraction now handles read-only files gracefully.

## Startup Robustness
- **Fallback logger** — If Serilog initialization fails, the app now falls back to debug output instead of crashing at startup.
- **Initialization order** — The service provider is built only after the temp-folder check, and startup cleanup (trash + temp files) is more resilient.
- **Timeout handling** — Added timeouts for the silent update check and usage statistics calls so they can no longer hang indefinitely.

## F8 Hotkey Warning
- **Hotkey in use detection** — When the F8 global hotkey cannot be registered because another program is already using it, Simple Launcher now shows a clear warning dialog and disables the screenshot functionality instead of failing silently. Localized in all supported languages.

## RetroAchievements
- **Improved system matcher** — Updated the RetroAchievements system matcher for better game matching.

## Credential Protection
- **Encrypted RetroAchievements credentials** — RA credentials are now encrypted using Windows Credential Manager.
- **Nested settings structure** — Emulator configuration now uses a nested settings structure for better organization.

## UI/UX Improvements
- **Cancel buttons** — Added cancel buttons to the SetGamepadDeadZone, Debug, SetLinks, and EditSystem dialogs.
- **Tooltips** — Added tooltips for data grids, preview images, and viewer windows.
- **Dead zone revert confirmation** — Added a confirmation message box when reverting dead zone settings.
- **Game list refresh** — The game list now automatically refreshes when deleting a file that no longer exists on disk.
- **Emergency overlay release** — Added an emergency overlay release button to GlobalStatsWindow.

## Debug Window Improvements
- **Restored initialization** — The Debug Window opens correctly again: its constructor (lost during the Serilog/DI refactor) was restored, and the window now applies the current app theme on open.
- **Styling & closing behavior** — Improved Debug Window styling, log text color, and closing/exception behavior.
- **`-debug` command-line flag** — Launching SimpleLauncher with the `-debug` argument now opens the Debug Window alongside the main window.
- **`parameters.md` path fix** — The parameters guide is now resolved relative to the application folder instead of the current working directory. Previously, launching the app from another working directory (shortcuts, scripts, IDE) made `parameters.md` appear "missing" and triggered a modal reinstall dialog that froze the app and the Debug Window.

## Path Handling
- **Quote trimming** — Surrounding quotes are now automatically trimmed from paths.

## Structured Logging
- **Serilog** — All logging now flows through Serilog (`ILogger`), and the custom `ILogErrors` abstraction was removed. The Debug Window receives log entries through a dedicated Serilog sink, a rolling daily file sink (7 days retained) is written to the local app data folder, and bug report sinks were added to all projects (including the tools and Updater).

## Game List & UI
- **Async system manager loading** — System managers load asynchronously, preventing UI blocking at startup.
- **Embedded images fix** — Corrected pack URI paths for embedded images and icons.
- **Minimize-to-tray handling** — Improved minimize-to-tray behavior via proper window state change handling.

## Dependency Injection Refactoring
- **Massive DI migration** — Replaced static service dependencies with dependency injection throughout the entire application.
- **Constructor injection** — All services, ViewModels, and windows now use constructor injection.
- **`MessageBoxLibrary.cs` removed** — Replaced with `IMessageBoxLibraryService`.
- **`DebugLogger` replaced** — Static `DebugLogger` was replaced with the `IDebugLogger` interface.
- **`ContextMenuFunctions` replaced** — Static `ContextMenuFunctions` was replaced with the `IContextMenuFunctions` interface.

## MVVM Refactoring
- Refactored all configuration windows to the MVVM pattern.
- Refactored the RetroAchievements window to MVVM.
- Added ViewModels for multiple windows, including SupportWindow.
- Introduced the `IWindowContext` abstraction to decouple the game launcher from MainWindow.

## Removed Tool
- **GameCoverScraper removed** — The Game Cover Scraper tool has been removed from the Tools menu, along with all its bundled binaries and resources. Use **Find Rom Cover** or **Retro Game Cover Downloader** instead.

## Other Improvements
- **Emulator documentation** — Updated `parameters.md` (e.g., new CPCEC download link).
- **Code quality** — The codebase now compiles with nullable reference types enabled.

## Dependency Updates
- `SharpCompress` 0.48.1 → **0.50.4**
- `Tomlyn` 2.4.0 → **2.6.0**
- `YamlDotNet` updated to latest version
- `MessagePack` 3.1.6 → **3.1.8**
- `Microsoft.Data.Sqlite` 10.0.8 → **10.0.10**
- `Microsoft.Extensions.*` 10.0.8 → **10.0.10**
- `Microsoft.Extensions.Http.Resilience` 10.6.0 → **10.8.0**
- `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.300 → **10.0.302**
- `Microsoft.NET.Test.Sdk` 18.5.1 → **18.6.0**
- `SourceGear.sqlite3` updated to **3.53.4**
- Added `Moq` **4.20.72** *(new, for unit testing)*
- Added `Serilog` **4.4.0** with `Serilog.Sinks.Async` 2.1.0, `Serilog.Sinks.Debug` 3.0.0, and `Serilog.Sinks.File` 7.0.0 *(new)*
- Added `Meziantou.Analyzer` **3.0.139** *(new)*

# Release 5.5.0
*2026-05-25*
---

## New System Support
- **Commander Genius** — Added support for the Commander Keen engine recreation, including an intelligent launch strategy that:
  - Automatically parses the `cgenius.cfg` config to resolve the data/games directory.
  - Handles `${HOME}` and `${BIN}` variable expansion in config paths.
  - Extracts ZIP/7z/RAR archives and automatically locates Keen game data files (`.CK1`–`.CK6`).
  - Flattens nested folder structures and cleans up temp files after launch.
  - Includes dedicated system image and setup documentation.

## Improved DOSBox Support
- **Full DOSBox launch strategy** with intelligent game file detection — automatically finds `.conf`, `.bat`, `.exe`, and `.com` files inside archives or folders.
- **ISO and CHD support for DOSBox** — Mounts ISO files to scan for executables, then generates a DOSBox config with native `imgmount` commands. CHD files are mounted via CHDMounter and launched via DOSBox cdrom mount.
- **DOSBox file selection dialog** — When multiple game files are found, presents a clean selection window with file names and relative paths.
- **"Group Files by Folder" extended to DOSBox** — Now works with DOSBox-based emulators (DOSBox, DOSBox-X, DOSBox Staging, dosbox-pure).
- **`-conf %ROM%` auto-injection** — Automatically inserts the `-conf` flag when launching via DOSBox if not already present.

## UI/UX Improvements
- **New minimize-to-tray function** — Allow Simple Launcher to remain open in the tray.
- **System context menu** — Right-click on system buttons now shows Edit and Delete actions, passing the pre-selected system name.
- **Dedicated context menu icons** — New icons for PS3, ScummVM, and Xbox 360 XBLA system menu items.
- **Markdown rendering enhancements** — Improved Markdown styling with proper headings, code blocks, tables, lists, blockquotes, and horizontal rules in release notes and help content.

## New Parameter Placeholder
- **`%NAME%`** — Resolves to the ROM filename without path or extension. Useful for emulators that expect just the game name (e.g., Commander Genius `dir="games/%NAME%"`).

## Improved Error Handling
- **Batch file error reporting** — Batch file failures now show a detailed error dialog with the option to open the error log. Batch file contents are also logged on failure for easier debugging.
- **Batch file working directory validation** — Validates that the working directory exists before executing batch files.
- **Unicode normalization for file paths** — Files created on macOS (NFD) can now be found on Windows (NFC) by trying multiple Unicode normalization forms, preventing "file not found" errors.
- **Access denied handling for Updater** — The updater now handles Win32 access denied errors gracefully with a user-friendly message and download page redirect.
- **COMException rendering detection** — Detects WPF render thread failures (UCEERR_RENDERTHREADFAILURE) caused by GPU driver issues or per-pixel transparency.

## HTTP Resilience
- **Replaced custom retry logic with Polly resilience handler** — HTTP downloads now use `Microsoft.Extensions.Http.Resilience` with exponential backoff and jitter (5 retry attempts, 2-second base delay).

## Single Instance Improvements
- **Improved single-instance behavior** — Launching a second instance now restores the existing window instead of showing an error message. Uses named events for cross-process signaling with a fallback to window enumeration.

## Sound Service
- **Replaced Windows MediaPlayer with NAudio 2.3.0** — Sound playback no longer requires UI thread dispatcher invocations, improving reliability and eliminating thread-affinity issues.
- **Registered `IPlaySoundEffects` interface** — Added proper DI registration for sound effects service.

## MVVM Migration to CommunityToolkit.Mvvm
- Migrated all ViewModels from custom `ViewModelBase` and `RelayCommand` infrastructure to **CommunityToolkit.Mvvm 8.4.2**.
- Created dedicated ViewModels for AboutWindow (`AboutViewModel`), ImageViewer (`ImageViewerViewModel`), SetFuzzyMatching (`SetFuzzyMatchingViewModel`), DebugWindow, and GlobalStatsWindow.
- Added unit tests for `DebugViewModel`, `GameVerificationViewModel`, and `GlobalStatsViewModel`.

## Service Extraction from MainWindow
- **`LoadingOverlayService`** — Extracted loading overlay management with operation counting (`SetLoadingState`, `EmergencyRelease`).
- **`StartupInitializationService`** — Consolidated startup logic (first-run wizard, MS Store scanning, game loading).
- **`GameListUIService`** — Extracted UI state management (grid/list mode, game button image cleanup, pagination).
- **`ThemeMenuService`** — Extracted theme switching logic (base themes + accent colors).
- **`LanguageMenuService`** — Extracted language switching with checkmark management.

## Code Refactoring
- **Injected `ILogErrors` via constructor/parameter** — Replaced `ServiceProvider` resolution with proper constructor and parameter injection across favorites page, global search, context menu services, game button factory, and mount services.
- **Standardized emulator detection checks** — Unified null-conditional access pattern for emulator detection checks (Azahar, Citra, DuckStation, Geolith, MAME, Ootake, Raine, RetroArch, Sameboy, Xemu).

## Dokan Driver Validation
- **New `DokanValidation` class** — Uses P/Invoke directly against `dokan2.dll` to check if Dokan is installed before any mount operation.
- Provides clear error messages when Dokan is missing for ZIP/CHD mounts.
- **Dokan localization** — Added localized strings for Dokan driver not found dialog with option to open download page.

## Dependency Updates
- `CommunityToolkit.Mvvm` **8.4.2** *(new)*
- `NAudio` **2.3.0** *(new, replaces Windows MediaPlayer)*
- `Microsoft.Extensions.Http.Resilience` **10.6.0** *(new, Polly resilience handler)*
- `MessagePack` 3.1.4 → **3.1.6**
- `YamlDotNet` 17.1.0 → **18.0.0**
- `SharpCompress` → **0.48.1**
- `Tomlyn` 2.3.2 → **2.4.0**
- `Microsoft.Data.Sqlite` 10.0.7 → **10.0.8**
- `Microsoft.Extensions.*` 10.0.7 → **10.0.8**
- `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.203 → **10.0.300**

# Release 5.4.0
*2026-05-10*
---

## New Parameter Placeholder
- Added `%ROMSYSTEMFOLDER%` placeholder that resolves to the specific system folder containing the ROM being launched.

## RetroAchievements Integration
- Improve the hashing of GameCube and Wii games with better support for RVZ, WBFS, GCZ, CISO, and WIA file formats.
- Significantly expanded system name aliases for better matching (Nintendo N64, SEGA MEGA-CD, ARCADE - MAME, Nintendo - Game Boy Advance, and many more).

## Dokan Installation Check
- Integrated `DokanService` into the Updater that detects missing Dokan library and offers to download and install it after updates (x64/ARM64 support).
- Enhanced CHD/ZIP mount error messages with specific Dokan exit code reasons (STATUS_DLL_NOT_FOUND, STATUS_ORDINAL_NOT_FOUND).

## Enhanced Error Handling & Robustness
- New RetroArch special characters in path detection (shows a clear message about mkdir permission denied).
- MAME corrupted INI auto-restore from bundled sample when unknown option warnings are detected.
- MAME error detection expanded to catch `mame64`, `WRONG LENGTH`, and `Required files are missing` patterns.
- Protocol handler errors now show a localized message box instead of throwing an exception.
- Guard against directory disappearance races during file enumeration.

## Code Refactoring
- Extracted `RomHistoryLoader` service from `RomHistoryWindow` for cleaner separation of concerns.

## System Image Handling
- Fix a bug while loading System image preview in the Edit System Window.

## Unit Tests
- New test suites for `MameManager` (corrupted/XML/empty dat files), `RetroAchievementsManager`, and `RomHistoryLoader`.
- Added `ServiceProviderMock` helper for mocking `App.ServiceProvider` in unit tests.
- Added test for XAML string key regex extraction.
- Added auto-sorting tests for detecting and fixing alphabetical ordering issues in resource files.

## Dependency Updates
- SharpCompress 0.47.4 → 0.48.0
- YamlDotNet 17.0.1 → 17.1.0
- Microsoft.NET.Test.Sdk 18.4.0 → 18.5.1

# Release 5.3.3
*2026-04-25*
---

- Improved support for `.bat` files.
- Fixed handling of placeholders during path validation.
- Fixed an edge case in the path containment check where an exact match between the resolved path and base folder was incorrectly rejected.
- Added required resource files for PSX packaging, including icons, images, a game information database, and encryption keys.
- Added comprehensive unit tests for ZIP path traversal validation, temporary directory cleanup, `PathHelper` resolution methods, and `.bat` file validation.

# Release 5.3.2
*2026-04-24*
---

## 🔄 Updater Overhaul
- Completely refactored the auto-updater with a clean service-oriented architecture (`UpdateService`, `GitHubService`, `DownloadService`, `ZipService`)
- Added real-time download progress bar and status text during updates
- Added global exception handling and graceful fallback to manual download

## 🌍 Localization & Translation
- Added more localized tooltips across the main interface

## 🎨 UI/UX Improvements
- System image selection with live preview and validation in the system editor

## 🛡️ Reliability & Robustness
- Added `LocalAppData` fallback for data files
- Enhanced emulator path validation and UAC cancel handling
- Improved path validation with long path support and mismatch detection
- Explicit TLS 1.2/1.3 support and enhanced HTTP error logging

## 🧪 Testing
- Added comprehensive unit test suite for core services

## 📚 Documentation & Tools
- Standardized emulator paths and added official download links in `parameters.md`
- Updated `BatchConvertToCHD` and `RomValidator` tool binaries
- Added timeout messaging for image downloads and USB path guidance

# Release 5.3.1
*2026-04-18*
---

This release focuses on bug fixes, performance improvements, memory leak prevention, and enhanced async/await handling throughout the application.

## 🔧 Bug Fixes

### Network & HTTP Improvements
- **Fixed HTTP Request Timeouts** - Added 15-second timeouts to all HTTP requests (bug reports, stats, support requests) to prevent indefinite hangs when network issues occur
- **Added Operation Cancellation Handling** - HTTP requests now handle timeout exceptions gracefully without crashing

### Microsoft Store Game Scanning
- **Fixed JSON Parsing Issues** - Added sanitization for JSON control characters that could cause crashes when parsing game data with special characters
- **Fixed Directory Creation** - Ensured destination directories exist before saving extracted icons

### Thread Safety & UI Stability
- **Fixed Thread Safety Issues** - Wrapped UI updates in dispatcher invocations in MainWindow to prevent cross-thread access exceptions
- **Fixed String Comparison Issues** - Implemented case-insensitive string comparisons throughout the application for system names, file names, and emulator matching

### Memory Management
- **Fixed Memory Leaks** - Added proper image source clearing to prevent BitmapImage reference leaks when clearing game grids
- **Fixed Async Cleanup** - Replaced synchronous cleanup methods with async versions to prevent blocking
- **Fixed Event Handler Leaks** - Properly unsubscribe event handlers when closing windows

## ✨ Improvements

### RetroArch Integration
- **Enhanced RetroArch Validation** - Changed parameter validation from "must start with -L" to "must contain -L" for more flexible core configuration
- **Localization Updates** - Updated RetroArch parameter error messages in all 16 supported languages

### Blastem Emulator
- **Improved Blastem Error Handling** - Added comprehensive error handling for Blastem configuration injection with specific exception types (FileNotFound, UnauthorizedAccess, IOException)

### Batch File Execution
- **Better Batch File Error Reporting** - Batch file failures now create proper exceptions for better debugging

### Async/Await Refactoring
- **Removed ConfigureAwait(false)** - Removed `.ConfigureAwait(false)` from await calls across the application (LogErrorsService, EasyModeManager, DownloadManager, UpdateChecker, SystemManager, ContextMenuFunctions, App)
- **Proper Dispatcher Invokes** - Added proper dispatcher invocations for UI updates in async methods

### Security
- **Path Traversal Prevention** - Added validation to ensure resolved paths stay within intended base folders

## 📦 Tools & Dependencies

- **Updated BatchConvertToRVZ and RomValidator binaries**

## 📚 Documentation

- **Fixed Tsugaru emulator platform documentation** (PC-9800 series → FM Towns)

## 🏗️ Technical Improvements

### Code Quality
- Enhanced exception handling throughout async operations
- Improved null checks and defensive programming
- Better logging for debugging purposes
- Added proper disposal of resources (mutex, cancellation tokens)

### Performance
- Thread-safe loading state management with locks
- Atomic operations for cancellation token source replacement
- Optimized file deletion with async retry logic

# Release 5.3.0
*2026-04-16*
---

# 🚀 Universal CHD Support & Enhanced RetroAchievements!

### 💿 CHD Support for Everyone!
We've massively expanded CHD support to emulators that lack native CHD format support. Simple Launcher now grants these emulators full CHD capability by leveraging our built-in **CHDMounter** tool. When you launch a CHD, it automatically mounts as a virtual drive in the background, allowing these emulators to run your compressed library seamlessly!

**Newly Supported Emulators:**

| Emulator         | CHD Support |
|------------------|-------------|
| RPCS3            | Working since version 5.2       |
| Xemu             | Working since version 5.2       |
| Xenia            | Working since version 5.2       |
| Cxbx-Reloaded    | Working since version 5.2       |
| Mednafen         | NEW - Working       |
| PCSX-Redux       | NEW - Working       |
| 4DO              | NEW - Working       |
| Gens             | NEW - Working       |
| Blastem          | NEW - Working       |
| Yabause          | NEW - Working       |
| Mesen            | NEW - Working       |
| FinalBurn Neo    | NEW - Working       |
| FinalBurn Alpha  | NEW - Working       |
| Raine            | NEW - Working       |
| CD-i Emulator ¹ | NEW - Working       |
| Tsugaru          | NEW - Working       |

¹ This emulator has official CHD support that doesn't work properly - Simple Launcher fixes that for you!

### 🏆 RetroAchievements: Welcome to the Wii & More!
Achievement hunters, rejoice! We've overhauled our RetroAchievements integration:
- **Nintendo Wii Support:** Full support for the Wii, which recently received official RetroAchievements support
- **Expanded System Aliases:** Better matching for NEC PC-FX, SNES MSU1, and many more systems

### 🎨 Your Launcher, Your Style
New customization options put you in control:
- **New Themes:** Beautiful **Midnight** (Deep Blue) theme and **High Contrast** mode for better accessibility
- **Adaptive Mode:** Automatically syncs with your Windows Light/Dark mode settings
- **Filename Preferences:** "Clean Up" filenames in Grid View (removing tags like `[!]` or `(USA)`) or hide filenames entirely for a pure, box-art-focused look
- **Font Size Control:** Customize filename and machine name font sizes in game buttons
- **Separate Thumbnail Sizes:** Independent zoom controls for system selection and game view

### 🛡️ Smarter & Safer
- **Elevation Detection:** Detects games requiring Administrator privileges and notifies you immediately
- **Safety Mechanisms:** Automatically kills lingering `CHDMounter` and `SimpleZipDrive` processes when closing games or the launcher - no more "ghost" virtual drives!
- **Startup Validation:** Detects if running from a temporary folder (like inside a ZIP) and prompts you to extract it first

### 📚 Documentation & Help Improvements
- **Enhanced Parameters Guide:** Enhanced `parameters.md` with official emulator download links and comprehensive parameter documentation
- **Markdown-Based Help:** Migrated help system from XML to Markdown format for better readability

### 🔧 Technical Improvements
- **API-Based Scanning:** Microsoft Store game detection now uses a dynamic API for lighter, more accurate results
- **Secure Connections:** All metadata and image downloads now use **TLS 1.2/1.3**
- **Resilient Saving:** Improved file lock handling during saves for `settings.xml`, `system.xml`, `favorites.dat`, and `playhistory.dat` with retry logic and exponential backoff
- **Enhanced Error Handling:** Better exception handling throughout the app with specific error types and improved logging
- **Play Time Tracking:** Refactored play time storage from strings to seconds for better accuracy
- **Better Accessibility:** Added automation properties to images and access keys to menu items

# Release 5.2.0
*2026-03-28*
---

# 🚀 Breaking the "Impossible": CHD Support for Xbox, Xbox 360, and PS3 is Here!

Today marks a major milestone for the retro gaming community. Support for CHD (Compressed Hunks of Data) files on original Xbox, Xbox 360, and PlayStation 3 has been a long-awaited and heavily requested feature—Xemu users in particular have been vocal about it—but native support never materialized.

**Until now.**

With the latest update to **Simple Launcher**, you can now launch your games directly from CHD files for **Xemu, Xenia, RPCS3, and Cxbx-Reloaded.**

### 📋 Prerequisite: Dokan Library Required

To mount CHD files as virtual drives, **you must install the Dokan library** on your system. Dokan is a user-mode filesystem driver for Windows that enables the virtual drive mounting functionality.

**Download and install Dokan from:** https://github.com/dokan-dev/dokany/releases

Simple Launcher will not mount CHD files without Dokan installed.

### 🛠️ How It Works

- The virtual drive system is built around Dokan. I wrote the tool that provides a wrapper layer around Dokan that handles mounting the virtual drive and parsing the file tree inside the CHD image, enabling seamless CHD integration with the supported emulators.
- Perhaps the most significant achievement is that I did all of this **without touching a single line of the emulators' source code.** Simple Launcher acts as the "brain," handling the heavy lifting externally so you can use standard, official emulator builds.

### 📦 What This Means for You

- **Massive Space Savings:** Stop wasting hundreds of gigabytes on uncompressed ISOs. CHD offers incredible compression without losing data.
- **Instant Play:** No more waiting for slow "CHD to ISO" conversions in temporary folders. The mounting is nearly instantaneous.

### 🎮 Supported Emulators

- **Xemu** (Original Xbox) - *The long-awaited community request!*
- **Xenia** (Xbox 360)
- **RPCS3** (PlayStation 3)
- **Cxbx-Reloaded** (Original Xbox)

This refactor wasn't just a code update; I am thrilled to put this power into your hands.

**Download the latest version now and reclaim your hard drive space!**

### 🚀 Major Features & Refactoring

- **CHD Virtual Mounting:** Refactored the launcher to use `CHDMounter` for **Xemu, Xenia, RPCS3, and Cxbx-Reloaded**. Instead of converting large `.chd` files to `.iso` (which was slow and consumed disk space), the app now mounts them as virtual drives and automatically locates the necessary boot files (e.g., `EBOOT.BIN`, `default.xex`, , `default.xbe`, `image.iso`).
- **Atomic File Saving:** Implemented atomic write operations for `settings.xml` and `system.xml`. The app now saves to a temporary file before replacing the original, significantly reducing the risk of configuration corruption during crashes or power loss.
- **Enhanced ZIP Security:** Added "Zip Slip" (path traversal) validation to all extraction and mounting services to prevent malicious archives from writing files outside of intended directories.
- **PBP Support for Mednafen:** Added a new strategy to convert `.pbp` files to `.cue/.bin` on the fly specifically for the Mednafen emulator.

### 🎨 UI & UX Enhancements

- **Global Tooltip System:** Added descriptive tooltips to almost every UI element across the application, including the Main Window, Edit System, and Settings windows.
- **Centralized Styling:** Refactored XAML resources to centralize styles and brushes in `App.xaml`, ensuring a more consistent look and feel across all windows.
- **Improved Loading States:** Replaced direct flag manipulation with atomic lifecycle methods for UI updates, preventing race conditions during system scans and game loading.

### 🕹️ Emulator Compatibility & Logic

- **RetroAchievements Filtering:** The RetroAchievements icon and context menu items are now intelligently restricted to supported systems. Systems like PS3, Xbox 360, and Switch are now filtered out of the hashing logic.
- **RetroArch Validation:** Added early validation for RetroArch parameters. The app now warns users if the `-L` core parameter is missing before attempting to launch.
- **MAME Error Handling:** Improved detection for MAME-specific errors (ROM set mismatches, unknown systems, or missing images) with user-friendly dialogs and links to compatibility guides.
- **Xenia Configuration:** Extended the Xenia config handler to support both portable and standard `Documents\Xenia` locations.

### 🔍 Game Scanning & Metadata

- **Microsoft Store Improvements:** 
  *   Significantly expanded the app exclusion filter to hide system utilities and non-game apps.
  *   Added EFS encryption fallback for copying game logos.
  *   Improved icon extraction logic to prioritize high-resolution assets.
- **Recursive Search Control:** Added a "Disable Recursive Search" option to system configurations, allowing users to limit scans to top-level directories.
- **Robust Image Downloading:** Added retry logic and a 15-second timeout to the game image API service to handle intermittent network issues.

### 🛠️ Maintenance & Build

- **Dependency Updates:** Updated `SharpCompress` to v0.47.3 and `Tomlyn` to v1.0.0.
- **Library Migration:** Replaced `SharpZipLib` and `SevenZipSharp` with `SharpCompress` to simplify the codebase and remove the dependency on external 7z DLLs.
- **Long Path Support:** Improved path handling across the service layer using the `\\?\` prefix to support file paths exceeding 260 characters.

# Release 5.1.0
*2026-02-14*
---

### Created logic to convert CHD files to ISO on the fly for the "Xemu" and "Xenia" emulators, since both emulators do not natively support CHD files.
- This allows users to store their Xbox and Xbox 360 ISOs in CHD format and convert them back to ISO on the fly when launching games.
- 'Simple Launcher' includes a bundled tool called "BatchConvertToXiso" that can convert original REDUMP Xbox ISOs to emulator-ready optimized XISO files. This tool removes unneeded files and optimizes the XISO file for emulators.
- 'Simple Launcher' also includes a bundled tool called "BatchConvertToCHD" that can convert ISO files to CHD format.
- Using both tools, you can significantly reduce the size of your Xbox and Xbox 360 game library. In my tests, I achieved a size reduction of approximately 30% over the optimized XISO files.
- 'Simple Launcher' will convert files back to ISO format on the fly when you launch games.

### Created a launch handler for the Ootake emulator, a PC Engine emulator, and for SameBoy, a Game Boy emulator.
- It will force the extraction of compressed files, since both emulators do not natively support compressed files. This will prevent launch failures.

### Created logic to convert CHD files to CUE/BIN on the fly for the Mednafen emulator and the 4DO emulator.
- Both emulators do not natively support CHD files. This will prevent launch failures.

### Fixed MAME emulator handler to properly inject ROM paths into the emulator configuration file.
- From now on, you no longer need to provide the '-rompath' parameter to run MAME, as the logic will automatically inject the ROM paths from the 'System Folder' into the MAME configuration file. This will prevent launch failures.

### Improved logic for generating aliases for Microsoft Windows games, which will enhance game detection.

# Release 5.0.0
*2026-02-11*
---

## Refactored GameLauncher Architecture
- Replaced the monolithic conditional structure with a flexible, strategy-based pattern that dramatically improves extensibility for new emulators and platforms.
- Created emulator-specific configuration handlers (e.g., `AresConfigHandler`, `XeniaConfigHandler`, `RaineConfigHandler`) that manage pre-launch configuration dialogs and dynamically modify emulator settings files.
- Implemented launch strategy pattern (e.g., `ChdToCueStrategy`, `XisoMountStrategy`, `ZipMountStrategy`, `DefaultLaunchStrategy`) to handle diverse launch scenarios—including ISO mounting, archive extraction, format conversion, and direct execution—with clean, maintainable code.
- Integrated format conversion services for CHD-to-CUE/BIN, CHD-to-ISO, and RVZ-to-ISO transformations, expanding compatibility across emulators. For example, it can convert a CHD file to CUE/BIN on the fly if the emulator does not support CHD.

## Configuration Injection Service
- Built a centralized service that programmatically injects settings into emulator configuration files.
- Supports 20+ emulators: Ares, Azahar, Blastem, Cemu, Daphne, Dolphin, DuckStation, Flycast, MAME, Mednafen, Mesen, PCSX2, Raine, Redream, RetroArch, RPCS3, Sega Model 2, Stella, Supermodel, Xenia, and Yumir.
- Xenia integration mirrors Xenia Manager's internal configuration injection functionality.

## Enhanced RetroAchievements Integration
- Automatically injects RetroAchievements credentials into PCSX2, DuckStation, PPSSPP, Dolphin, Flycast, BizHawk, and RetroArch.
- Added RetroAchievements filter in `MainWindow` to quickly identify and display probable supported titles.
- Refined game hashing algorithms across multiple systems for accurate achievement tracking.

## UI Enhancements
- Unified UI busy-state handling throughout the application to provide consistent blocking behavior and progress feedback across all major windows (MainWindow, Favorites, Search, etc.).

## Code Quality Improvements
- Enhanced error handling for XML corruption.
- Fixed potential deadlocks in play history updates.
- Centralized `system.xml` load, edit, and delete operations into the `SystemManager` class.

# Release 4.9.1
*2026-01-18*
---

-   **Package Updates:** Several Microsoft-related NuGet packages (Data.Sqlite, Extensions, CodeAnalysis) were updated to their latest patch versions (e.g., from `10.0.1` to `10.0.2`).
-   **Long Path Support:** Enhanced file and directory existence checks in `GameLauncher.cs` and `PathHelper.cs` by explicitly handling the long-path prefix (`\\?\`). This ensures compatibility across different drive types (local, network, removable) and paths exceeding 260 characters.
-   **Shortcut Validation:**
    -   Refined `.URL` file launching to only validate protocol handlers (like `steam://`) if the target is a true URI. This prevents drive letters (e.g., `C:\`) from being incorrectly identified as protocols.
    -   Improved error logging for shortcuts by preventing the app from attempting to read binary content from `.LNK` files during an exception.
-   **Thread Safety:** Added lock mechanisms in `DownloadManager.cs` around the `CancellationTokenSource`. This ensures that starting, canceling, and disposing of downloads is thread-safe.
-   **Enhanced Logging**
-   **Microsoft Store Filtering:** Significantly expanded the exclusion list in `ScanMicrosoftStoreGames.cs`.
-   **Tool Updates:** Updated several external tools bundled with the launcher, including `BatchConvertToCHD`, `chdman`, `GameCoverScraper` (arm64/x64), `SimpleZipDrive`, and `BatchConvertIsoToXiso`.

# Release 4.9.0
*2026-01-13*
---

## 🎮 RetroAchievements Enhancements
- **Emulator Auto-Configuration**: Streamlined setup process with automatic configuration for retroarch
- **UI Localization**: Comprehensive localization support across all RetroAchievements interfaces
- **Messaging**: Enhanced error and success feedback for better user experience

## 🏪 Microsoft Store Game Scanning
- **User Verification Window**: New interface for confirming detected games

## 🛡️ Error Handling & Robustness
- **Win32Exception Handling**: Improved support for application control policies
- **PowerShell Restrictions**: Better handling of execution policy limitations
- **File Lock Management**: User-friendly prompts for locked file scenarios
- **Download Resilience**: Enhanced retry mechanisms for download and extraction operations

## 🌍 Localization & UI Improvements
- **Full Localization**: Extended support across multiple windows:
    - GlobalStatsWindow
    - RetroAchievementsForAGameWindow
    - FavoritesWindow
    - FilterMenu
- **Dynamic Resources**: Refactored UI text to use dynamic resource binding
- **Consistent Styling**: Updated button styles in EasyModeWindow

## ⚡ Performance & Thread Safety
- **Cancellation Tokens**: Integrated across asynchronous operations for better responsiveness
- **Thread Safety**: Added explicit locks to critical sections (SettingsManager, search results)

# Release 4.8.0
*2026-01-04*
---

# Major Overhaul: Microsoft Windows Game Scanning & Enhanced Error Handling

Introduces a major overhaul of Microsoft Windows Game scanning, significantly enhancing error handling, and refining the user interface.

## Key Changes

### 1. Game Scanning Refactoring & Expansion

The monolithic `GameScannerService` was broken down into modular, platform-specific classes:

- `ScanSteamGames`
- `ScanEpicGames`
- `ScanGogGames`
- `ScanMicrosoftStoreGames`
- `ScanAmazonGames`
- `ScanBattleNetGames`
- `ScanHumbleGames`
- `ScanItchioGames`
- `ScanRockstarGames`
- `ScanUplayGames`

This adds support for new platforms and improves existing scanning logic with:
- Better filtering
- DLC detection
- Executable identification
- Icon extraction heuristics

### 2. API-based Artwork Download

A new API integration (`GameImageClient`) was added to download game artwork for Microsoft Windows Games, with robust fallback mechanisms to local icon extraction if the API fails.

### 3. Enhanced Error Handling & Robustness

- **Long path support:** Implemented `\\?\` prefix for reliable file and directory operations across services
- **Download and extraction error handling:** Including retry logic, disk space checks, user cancellation management, and a new `ShowExtractionFailedMessageBoxAsync` for manual intervention
- **Game launching error handling:** Refined in `GameLauncher`, adding `.URL` validation, protocol checks, and a new `ShowCustomMessageBox` for detailed notifications
- **UIPI exceptions:** Gracefully handled in `GamePadController` with refined error logging

### 4. UI/UX Improvements

- Introduced a new global `RetroAchievementsWindow` for user profiles and unlocks
- **Automated first-run game scanning** for Microsoft Windows games
- Updated menu structures, replacing dynamic headers with static strings for consistency
- Added a new **SupportOptionWindow** for AI-based troubleshooting and developer contact

### 5. Binary & Dependency Updates

Updated various external tools:
- RetroGameCoverDownloader
- GameCoverScraper
- BatchConvertToCHD

### 6. Code Cleanup & Localization

- Cleaned up unused code
- Updated localization strings across all supported languages

# Release 4.7.0
*2025-12-07*
---

## 🚀 New Features
- **Group Files by Folder**: Groups multi-file MAME games (e.g., Software List CHDs/ROMs from [PleasureDome](https://pleasuredome.github.io/pleasuredome/index.html)) into single UI entries. Compatible **only with MAME**—warnings shown for non-MAME setups. Handy to launch MAME Software List CHDs.
- **RetroGameCoverDownloader**: New tool to help users get cover art for their games.
- **Automatic Creation of 'Microsoft Windows' System**: Introduced an algorithm to automatically add links to Epic Games, Steam Games, and Windows Store Games to the frontend.
- **Advanced Global Search**: Filter by system, filename, MAME description, folder name, and recursive search.
- **First-Run Welcome Flow**: Guides new users to Easy Mode for quick setup.
- **Kebab Menu**: "..." button on game entries for quick context actions.
- **Configurable Status Bar**: Timeout (default 3s) with detailed feedback for loading/saving/actions.

## 🔧 Core Refactoring
- **Dependency Injection (DI)**: `GameLauncher`, `GamePadController`, `UpdateChecker`, `ExtractionService`, `PlaySoundEffects`, etc., now instance-based services.
- **ExtractionService**: Refactored from `ExtractCompressedFile` with `IExtractionService` interface; retry logic for file locks.
- **Thread Safety**: `SemaphoreSlim` for game caches; `Dispatcher.InvokeAsync` preferred.
- **.NET 10**: Target framework upgraded; C# 14 features.

## 🎨 UI/UX Improvements
- **UpdateHistoryWindow**: Native Markdown rendering (no external deps).
- **EditSystemWindow**: `GroupBox` → `Expander` (collapsible); persist states.
- **Resizable Windows**: `SetFuzzyMatchingWindow`, `SetGamepadDeadZoneWindow`.
- **Dark Mode Toggle**: Navigation menu icon/handler.
- **Loading Overlays**: Consistent across windows; disable UI during ops.

## 🛠️ Tools & Emulators
- **Updated Binaries**: tools (`bchunk.exe`, `GameCoverScraper`, `FindRomCover.exe`, `BatchConvertIsoToXiso.exe` etc.).
- **New Emulators**: Ymir (Saturn), NooDs (DS), Gearlynx/Gearboy (Lynx).
- **Docs**: Updated `parameters.md`/`helpuser.xml` with Amiga models, MAME `rompath` examples.

## 🛡️ Robustness & Perf
- **Cancellation Tokens**: `GlobalSearchWindow`, `PlayHistoryWindow`.
- **Atomic Saves**: Favorites prevent corruption via temp files.
- **Cache Invalidation**: Clear RA cache on credential changes.
- **Localization**: 100+ new strings (artwork, Easy Mode, errors).

# Release 4.6.0
*2025-10-30*
---

-   Introduced tool `GameCoverScraper` to allow users to scrape cover images online.
-   Updated tool `BatchConvertToRVZ` with bug fixes.
-   Updated tool `RomValidator` to allow users to generate No-Intro compatible dat files.
-   Updated emulator configurations for multiple systems.
-   Added options to increase the number of games per page, as requested by users.
-   Added new aspect ratios for button generation.
-   Added caching for the "Feeling Lucky" feature for improved performance.
-   Enhanced `SupportWindow` with detailed error report generation and improved support request handling.
-   Replaced `HelpUserTextBlock` with `RichTextBox` for enhanced formatting and interaction.
-   Enhanced RetroAchievements integration with improved error handling for unauthorized API responses.
-   Updated MAME emulator setup and streamlined argument handling.
-   Updated documentation for multiple systems.
-   Refactored the parameters and paths validation logic.
-   Added multi-image pack download support.
-   Bug fixes.

# Release 4.5.0
*2025-10-11*
---

🏆 **Introduced RetroAchievements Integration**
*   Full support for viewing RetroAchievements profiles, game achievements, latest masters, and user rankings within a new dedicated window.
*   Advanced hashing logic for accurate game matching.
*   System-aware matching for RetroAchievements console IDs and aliases.

✨ **UI/UX Enhancements**
*   New overlay buttons on game items for quick access to RetroAchievements, video links, and info links (user-configurable).
*   Introduced MAME sort order toggle (by filename or machine description).

🎮 **Emulator & System Support**
*   Added MAME a7800 emulator configuration and documentation.
*   Updated `easymode.xml` and `easymode_arm64.xml` with the latest emulator versions and new system configurations.

🏗️ **Infrastructure**
*   Updated `RomValidator` and `SimpleZipDrive` executables for x86 and ARM64 platforms.

🔧 **Codebase Refinements & Bug Fixes**

# Release 4.4.2
*2025-09-29*
---

-   Improvements to error handling, parameter validation, and user experience.
-   Updated `SimpleZipDrive` to the latest version.
-   Updated emulator references and download links to the latest version.
-   Improved parameter validation to reduce false positives on non-path arguments.
-   Fixed double-click handling in game list mode to prevent errors on placeholder items.

# Release 4.4.1
*2025-09-19*
---

### 🔧 Bug Fixes
-   Improvements to error handling, parameter validation, and user experience across the SimpleLauncher application.
-   Fix the `Supermodel` emulator link.

# Release 4.4
*2025-09-17*
---

### 🖥️ Platform Support
-   Introduced support for Windows-arm64

### 🎨 User Interface
-   Improved UI responsiveness in `PlayHistoryWindow` and `FavoritesWindow`.
-   Replaced `PleaseWaitWindow` with a consistent, embedded `LoadingOverlay`.
-   Added "Delete Cover Image" option in the right-click context menu.

### 🔧 Technical Improvements
-   Improved error handling and user notifications.
-   Updated existing emulator versions.
-   Enabled MAME description support in `FindRomCover` tool.
-   Upgraded NuGet packages `MahApps.Metro` and `Microsoft.Extensions`.
-   Transitioned `SettingsManager` and `FavoritesManager` to Dependency Injection (DI).
-   Improved localization resource management.

# Release 4.3
*2025-07-26*
---

### Major Feature: Multi-Folder Support
-   **Multiple System Folders:** The core logic has been refactored to allow each system to scan for games across multiple folders instead of just one.

### UI & UX Enhancements
-   **Improved Button Styles:** Game and system buttons now feature refined 3D styles with hover and press animations for a more dynamic user experience.

### Stability Improvements
-   **Improved Mounting Logic:** The on-the-fly mounting for ZIP and XISO files no longer uses fixed delays. It now uses a more robust polling mechanism that actively checks for successful mounts, improving reliability and reducing unnecessary waiting.

### Tooling
-   **New Tool Integration:** A new `RomValidator` tool has been added. This tool can compare user game files with No-Intro dat files.

### Other Enhancements
-   **Emulator Updates:** Documentation and help files have been updated with information for new emulators (`Gopher64`, `Hades`, `VisualBoy Advance M`, `Retroarch SwanStation`).
-   **Emulator Updates:** Emulators from Easy Mode have been updated to the latest versions.
-   **MAME Database Update:** The `mame.dat` file has been updated to MAME 0.278.
-   **History Database Update:** The `history.xml` file has been updated to the latest version.

# Release 4.2.0
*2025-07-10*
---
### Core Feature Enhancements: On-the-Fly File Mounting
The most significant change is the introduction of on-the-fly file mounting, which allows users to launch games directly from compressed or disk image files without needing to manually extract them first.
**Note:** You need to install the Dokan from [GitHub](https://github.com/dokan-dev/dokany) for ZIP and XISO file mounting.

*   **ISO & ZIP Mounting for RPCS3:** The launcher can now mount `.iso` and `.zip` files for the PlayStation 3 emulator (`RPCS3`). It uses PowerShell for native ISO mounting and a new `SimpleZipDrive.exe` tool for ZIP files. After mounting, it automatically finds and launches the required `EBOOT.BIN` file.
*   **XISO Mounting for Cxbx-Reloaded:** Support has been added to mount Xbox ISO (`.xiso`) files for the `Cxbx-Reloaded` emulator. This is handled by a new `MountXisoFiles` service that uses the `xbox-iso-vfs.exe` tool to create a virtual drive and launch the `default.xbe` file.
*   **XBLA ZIP Mounting:** The system can now mount `.zip` files for Xbox Live Arcade (`XBLA`) games, searching for a specific nested file structure required to launch them.
*   **ScummVM ZIP Mounting:** The system can now mount `.zip` files for `ScummVM` games and automatically launch the game.

### Major Refactoring & Dependency Changes
The project's core dependencies and internal logic for handling files have been substantially overhauled to improve robustness and unify functionality.

*   **Unified Archive Handling:** The application has migrated away from using multiple libraries and external executables for file extraction. It now primarily uses `Squid-Box.SevenZipSharp` and `ICSharpCode.SharpZipLib`, providing a more integrated and reliable way to handle `.zip`, `.7z`, and `.rar` files. The `ExtractCompressedFile` service was refactored to use these new libraries.
*   **Removal of Caching System:** The `CacheManager` has been completely removed. The previous system of caching game lists for each system has been replaced with a more direct, on-demand file scanning approach. This simplifies the application's logic, eliminates the `cache` directory, and ensures the game list is always up to date.

### Update Tools
*   **BatchConvertToCHD:** Improved UI. Added the ability to check the integrity of CHD files and convert CSO files to CHD.
*   **BatchConvertToCompressedFile:** Improved UI. Added the ability to verify the integrity of compressed files.
*   **BatchConvertIsoToXiso:** Improved UI. Added the ability to verify the integrity of XISO files.
*   **BatchConvertToRvz:** Improved UI. Added the ability to test the integrity of the RVZ files.

### Bug Fixes and Other Enhancements
*   **Fixed Threading Issue:** The `PlayTimeWindow` and `FavoritesWindow` have been refactored to handle file operations and UI updates more safely across different threads, preventing crashes and ensuring a smoother user experience.
*   **UI Consistency:** The system selection screen now limits the maximum size of system thumbnails to 100 px to prevent oversized images from distorting the layout and to ensure a consistent look.

# Release 4.1.0
*2025-06-08*
---
*   Added a Sound Configuration feature allowing customizable notification sounds.
*   Implement a Debug Logger with a dedicated UI window for enhanced logging.
*   Major overhaul and standardization of path resolution and handling, including support for placeholders `%BASEFOLDER%`, `%SYSTEMFOLDER%` and `%EMULATORFOLDER%`.
*   Refactored Parameter Validation logic for clarity and robustness, particularly concerning paths.
*   Improved Error Handling, Logging, and messaging across various components (`GameLauncher`, `Services`, `Managers`), replacing `Debug.WriteLine` with the new `DebugLogger`.
*   Added Status Bar updates to show current actions (e.g., game launched).
*   Introduced 3D button styles for navigation buttons.
*   Updated Tray Icon messaging and functionality (including Debug Window access).
*   Refactored `Edit System Window` logic and validation for better user feedback on paths.

# Release 4.0.1
*2025-05-26*
---
### Bug Fix Version
-   **Parameter Handling Overhaul (`ParameterValidator.cs`, `GameLauncher.cs`):** Paths within parameters are converted to absolute paths.
-   **File Path Standardization (`GameLauncher.cs`, `GameButtonFactory.cs`, `GameListFactory.cs`):** File paths used for launching games and creating UI elements are consistently resolved to absolute paths relative to the application directory, improving reliability.
-   **UI Enhancements (`MainWindow.xaml`):** A new custom-styled `GridSplitter` has been added to the list view in the main window, allowing for better resizing of the game list and preview areas.
-   **Improved Window Closing (`DownloadImagePackWindow.xaml.cs`, `EasyModeWindow.xaml.cs`):** Added logic to the `CloseWindowRoutine` in `DownloadImagePackWindow` and `EasyModeWindow` to stop any ongoing operations (like downloads) before the window closes, preventing potential errors.
-   **Configuration Change (`SystemManager.cs`):** The default value for `ReceiveANotificationOnEmulatorError` in system configurations has been changed from `false` to `true`, meaning users will now receive error notifications by default.

# Release 4.0
*2025-05-24*
---
This release brings significant user-interface improvements, new features for game browsing and management, enhanced stability, and various under-the-hood technical refinements.

### Improvements
-   **New System Selection Screen:** Displayed a more visual screen when no system is chosen to simplify platform selection.
-   **Revamped Navigation Panel:** Added a panel on the left side of the main window with quick-access buttons for Restart, Global Search, Favorites, Play History, Edit System (Expert Mode), Toggle View Mode, Toggle Aspect Ratio, Zoom In, and Zoom Out.
-   **Switchable View Modes:** Enabled toggling between Grid View (with game-cover images) and List View (showing game details in a table).
-   **Enhanced Zooming:** Allowed zooming in and out of game thumbnails in Grid View via the new buttons or the `Ctrl` + `mouse-wheel` shortcut.
-   **Displayed Game File Sizes:** Showed file sizes in List View.
-   **Overhauled UI Layout:** Redesigned the main window and multiple dialogs (Easy Mode, Download Image Pack, Global Search, Favorites, Play History, Edit System).
-   **Refined Error Handling:** Improved error messages and user prompts throughout the application.
-   **Updated Documentation Links:** Improved Help User section and parameters' guide links.
-   **Enhanced Gamepad Logic:** Made controller navigation more reliable.
-   **Standardized Sound Effects:** Switched to a consistent notification sound across the app.
-   **Updated Batch Convert To CHD:** Added support for `.7z` and `.rar` files, enabled parallel processing, and improved multithreading.
-   **Added Batch Convert To RVZ:** Introduced a tool to convert GameCube and Wii ISO files to RVZ format.
-   **Updated Emulators:** Upgraded to the latest emulator versions.
-   **Updated MAME Data File:** Bumped to version `0.277`.

### Bug Fixes & Internal Changes
-   Fixed various Gamepad Controller issues.
-   Corrected typos and added null checks.
-   Addressed default-logic errors in emulator error notifications.
-   Prevented potential XML vulnerabilities during file loading/saving.
-   Fixed context-menu parameter-handling bugs.
-   Refactored and reorganized code for better maintainability and performance.
-   Replaced `Newtonsoft.Json` with `System.Text.Json` for JSON processing.
-   Improved file-path handling: more robust relative-path resolution and folder-name sanitization.
-   Enhanced asynchronous operations for smoother UI responsiveness.
-   Implemented stricter code-quality checks.
-   Consolidated "Please Wait" dialog logic.
-   Removed redundant or unused code.
-   Improved the structure and reusability of context-menu creation logic.
-   Integrated `IHttpClientFactory` for HTTP client management.

# Release 3.13
*2025-04-27*
---
-   Updated the **Batch Convert To Compressed File** tool to skip files that are already compressed.
-   Improved the UI in the **Easy Mode** and **Download Image Pack** windows.
-   Added code to enforce a single instance of the application.
-   Implemented fuzzy image matching to enhance the user experience. This logic finds a cover image that partially matches the ROM filename. Users can disable this feature if desired.
-   Updated emulators to the latest version.
-   Updated `history.xml` (available at arcade-history.com) to the latest version.

# Release 3.12
*2025-03-31*
---
-   Added an option to turn off emulator error notifications from 'Simple Launcher'. Users can configure this per emulator in the **Edit System - Expert Mode**.
-   Updated the **Batch Convert to CHD** tool application to accept new input formats, including ZIP files.
-   Added the **Batch Convert to Compressed File** tool.
-   Added the **Batch Verify CHD Files** tool.
-   Added the **Batch Verify Compressed Files** tool.
-   Added a **Support Request** window. Users can send questions to the developers.
-   Updated emulators to the latest version.
-   Updated the MAME database to the latest version (v0.276).
-   Updated `history.xml`, available at arcade-history.com, to the latest version.

# Release 3.11.1
*2025-03-22*
---
-   Added logic to check file and folder paths in the parameter field. Users will be notified if the check returns invalid.
-   Updated the `LogError` class to use a new, more flexible API.
-   Added a bug report feature for all the tools bundled with 'Simple Launcher'.
-   Improved the `BatchConvertToCHD` tool to better handle the deletion of original files after conversion to CHD.
-   Fixed some bugs.

# Release 3.11
*2025-03-12*
---
-   Added **Play History Window** and all the logic to implement it. Users can now see which games have been played and for how long.
-   Implemented the **Feeling Lucky Button** in the Main Window UI. This button will pick a random game for the selected system.
-   Updated the MAME database to the latest version (v0.275).
-   Changed the logic to load the MAME database into Simple Launcher. Instead of using a large XML file, it will now use a binary DAT file. This improves the database loading time and reduces the database size.
-   Updated emulators to the latest version.
-   Fixed some bugs.
-   Improved the `LogError` logic to prevent UI freezing.
-   Updated the logic behind the tray icon to a more modern approach.

# Release 3.10.2
*2025-03-03*
---
-   Fixed an issue in `List View` mode where preview images failed to load.

# Release 3.10.1
*2025-03-02*
---
-   Fixed some bugs.
-   Enhanced methods and functions to improve efficiency and reduce errors.

# Release 3.10
*2025-02-23*
---
-   Exposed the GamePad DeadZone variable to the user.
-   Improved the Game Button generation for the `Grid View` option. Now users can customize the aspect ratio of the buttons.
-   Updated the emulators to their latest releases.
-   Updated `history.xml`, available at arcade-history.com, to the latest version.
-   Fixed some bugs.
-   Enhanced methods and functions to improve efficiency and reduce errors.

# Release 3.9.1
*2025-02-08*
---
-   Significantly improved the speed of the search engine in the `Main Window` and in the `Global Search Window`.
-   Fixed some bugs.
-   Enhanced methods and functions to improve efficiency and reduce errors.

# Release 3.9
*2025-02-07*
---
-   Enhanced support for Xbox controllers and added support for PlayStation controllers. I tested it with the following controllers: `Xbox 360`, `Xbox One`, `Xbox Series X/S`, `PlayStation 4 DualShock`, and `PlayStation 5 DualSense`.
-   Improved the text formatting of developer suggestions in the `Edit Window`.
-   Enhanced the translation resources: even error messages are now translated into 17 languages.
-   Updated the emulators to their latest releases.
-   Updated `history.xml` to the latest version.
-   Updated `mame.xml` to the latest version (0.274).
-   Added a tool to batch convert regular ISO files to the `XISO` (Xbox) format.
-   Added a tool to batch convert CUE, BIN, ISO, and IMG files to the `CHD` (MAME) format.
-   Added a tool to batch compress files into ZIP or 7Z archives.
-   Fixed some bugs.
-   Improved several methods and functions to enhance efficiency and reduce errors.

# Release 3.8.1
*2025-01-16*
---
-   Bug fixes.
-   Improved some methods and functions to enhance efficiency and reduce errors.

# Release 3.8
*2025-01-15*
---
### Multilingual Version – A Major Update!
-   **Experience the Application in Your Language!** We've added translations for 17 languages, making the application accessible to users worldwide. Supported languages now include: Arabic, Bengali, German, Spanish, French, Hindi, Indonesian (Malay), Italian, Japanese, Korean, Dutch, Portuguese, Russian, Turkish, Urdu, Vietnamese, Simplified Chinese, and Traditional Chinese.
-   Updated to the latest technologies: `.NET Core 9` and `C# 13`, ensuring better performance and stability.
-   Fixed several entries in `helpuser.xml` for improved user assistance.
-   Updated all emulators to their latest releases.
-   Updated `mame.xml` to version 0.273.
-   Enhanced support for 'Kega Fusion' and 'Mastergear'.
-   Added more emulators to `system_model.xml`.

# Release 3.7.1
*2024-12-12*
---
-   Fixed bugs and improved exception handling in certain methods.
-   Added image packs for specific systems.

# Release 3.7
*2024-12-08*
---
-   Added a tool to generate `BAT files` for `Xbox 360 XBLA` games.
-   Added a `Helper class` to help users during system edits.
-   Improved the UI in the `Edit window`.
-   Added support for launching `Mattel Aquarius` games on `MAME` using the frontend.
-   Improved the code that gets the application version from GitHub.
-   Updated the emulator version of the Easy Mode emulators to the latest version.
-   Updated `mame.xml` to the latest version (MAME 0.272). I have also added Software List games to this list.
-   Updated `parameters.md` and `system_model.xml`.
-   Updated the binaries of the tool `FindRomCover` to the latest version.
-   Added image packs for `Philips CD-i` and `Atari ST`.
-   Added a new menu item to download image packs.
-   Added `Mattel Aquarius` and `Sharp x68000` to `Easy Mode`.
-   Updated the `rom history database` to the latest version (ver. 2.72) from [arcade-history.com](https://www.arcade-history.com/).
-   Fixed bugs and improved exception handling in some methods.

# Release 3.6.3
*2024-11-26*
---
-   Changed the temp folder from within the application folder to the `Windows temp folder` to prevent access issues.
-   Implemented methods to handle the cleanup of temp files and folders.

# Release 3.6.2
*2024-11-25*
---
-   Improved error handling in some methods.

# Release 3.6.1
*2024-11-24*
---
-   Fixed a bug in several methods that attempt to load a preview image or open a cover image when the `UserImagePath` is a relative path. These methods now convert all relative paths to absolute paths before execution.
-   Moved the `DownloadAndExtractInMemory` method to a separate class.
-   Improved error handling in some methods.

# Release 3.6.0
*2024-11-23*
---
-   Performed a major refactoring of the `EditSystem` class to implement better validation in the input fields of the `EditSystem` window.
-   Improved the system validation mechanism of the `system.xml` file when 'Simple Launcher' loads.
-   Implemented `in-memory download and extraction` of files as a backup method in case the regular extraction method fails.
-   Added different versions of the `7z` executable for `x86` and `x64`.
-   Added code to automatically convert URL-formatted text into real links in the `RomHistory` window.
-   Improved the `log class` to send more debug information to the developer.
-   Added functionality to `delete a game file` from within the UI, available in the right-click context menu.
-   Added functionality to `auto-generate image previews` for the loaded games.
-   Added an image pack for `Atari 8-Bit`.
-   Updated the emulator links to the latest version.
-   Bug fixes.

# Release 3.5.0
*2024-11-09*
---
-   Added a tool to generate `BAT files` for `Sega Model 3`.
-   Added a `local ROM history database` using `history.xml` from [arcade-history.com](https://www.arcade-history.com/).
-   Added a new way to view the games in the main UI. Users can now choose between `Grid View` and `List View`.
-   Added a `Star button` to the `Main Window top menu`, which selects all the favorites for the selected system.
-   Improved error and exception notifications in several methods for both users and developers.
-   Added 'Sony PlayStation 3' image pack.
-   Increased the font size for the game filenames in the `GameButtonFactory`.
-   Added `Sega Model 3` to `Easy Mode` with an `ImagePack` available for this system.
-   Updated emulator download links to the latest version.

# Release 3.4.1
*2024-11-03*
---
-   Automatically set the `SystemImageFolder` if the user does not specify it. It will be set to `.\images\SystemName`.
-   Implemented a way to retry extraction of the downloaded file in case it is locked by `antivirus software`.
-   Updated the download link for the emulators to the latest version in `EasyMode`.
-   Added 'Sony PlayStation 3' to the `EasyMode` installation window.
-   Updated `mame.xml` to the latest version.

# Release 3.4
*2024-11-03*
---
-   Added a new menu item called **Tools**. In it, you will find tools related to emulation.
-   Added a tool to `Create Batch Files for PS3 Games`.
-   Added a tool to `Create Batch Files for ScummVM Games`.
-   Added a tool to `Create Batch Files for Windows Games`.
-   Added a tool to `Organize System Images`.
-   Implemented code to automatically redirect the user to the `Simple Launcher Wiki page` if the provided parameters for emulator launch fail.
-   Implemented the automatic launch of the `UpdateHistory` window after an update so the user can see what is new in the release.
-   Implemented logic to automatically reinstall 'Simple Launcher' if the required files are missing.
-   Improved the `Updater` application. It will always automatically install the latest version of 'Simple Launcher' even if the parameters provided are invalid.
-   Fixed the `UpdateHistory` window to wrap the text. It is better for the user.

# Release 3.3.2
*2024-10-27*
---
-   Improved the field validation in the "Edit System" window to trim input values and prevent the input of empty spaces.
-   Enhanced support for `Xbox 360 XBLA` games in the `Launcher`.
-   Fixed the download of the 'ares' emulator.
-   Fixed the download of the 'Redream' emulator.
-   Fixed a bug in the `Easy Mode` window to prevent the extraction of partially downloaded files.
-   Added `Xbox 360 XBLA` into `easymode.xml` and `system_model.xml`.

# Release 3.3.1
*2024-10-24*
---
-   Fixed a minor issue in the `GameLauncher` class that was triggered when `MAME` output warning messages about the ROM. It will now only consider error messages if the emulator outputs an error code.

# Release 3.3.0
*2024-10-24*
---
-   Added `Image Packs` for `Amstrad CPC`, `Amstrad CPC GX4000`, `Arcade (MAME)`, `Commodore 64`, `Microsoft MSX1`, `Microsoft MSX2`, `Sony PlayStation 1`, `Sony PlayStation 2`, and `Sony PSP`.
-   Added a new `UpdateHistory` window that can be launched from the `About` window.
-   Added a button in the `GlobalStats` window that allows the user to save the generated report.
-   Fixed an error in the `GameLauncher` class that was generating an `InvalidOperationException`.
-   Improved the error logging in the `EditSystemEasyModeAddSystem` class.
-   Added logic to `delete the favorite` if the favorite file is not found.
-   Added new functionality to `Updater.exe`.

# Release 3.2.0
*2024-10-22*
---
-   Enabled download of `ImagePacks` for some systems.
-   Implemented a `Tray Icon` for the application.
-   Added the function to present the `System Play Time` for each System.
-   Made the filenames and file descriptions on each button in the `Main window` a little bigger.
-   Updated all emulators to the latest versions.
-   Updated the `mame.xml` file to the latest release.
-   Improved error handling in the `Easy Add System` window.
-   Enhanced error handling for missing the `default.png` file.
-   Enhanced the `Global Stats` class and `XAML`.
-   Improved error handling throughout the application.
-   Improved code in the `GameLauncher` and `ExtractCompressedFile` classes.

# Release 3.1.0
*2024-07-18*
---
-   Implemented a `file check` before launching a `favorite game`.
-   Fixed the freezing issue of the `Please Wait` window during a search in the `Main` window.
-   Capped the number of games per page at 500.
-   Included a second log window in the update process.
-   Enhanced the `global search` functionality to also search within the machine description of `MAME` files.
-   Updated the emulator download links to the latest version.
-   Fixed some random code issues.

# Release 3.0.0
*2024-07-17*
---
-   Implemented a `theme functionality` that enables users to personalize the application's appearance.
-   The user interface of the `main window` has been enhanced.
-   Added `icons` to some menu items.
-   Some images in the `Edit window` have been modified.
-   Created a `context menu` in the `main user interface` for the 'Remove From Favorites' option.
-   An `image preview functionality` has been implemented in the `Favorite` and `Global Search` windows.
-   The management of `mame.xml` and `system.xml` has been improved.

# Release 2.15.1
*2024-07-14*
---
-   Added a `star icon` to each game in the `favorites' list`.
-   Implemented a `preview window` that appears when users hover over a list item.
-   Enhanced logic for generating `favorites.xml` and `settings.xml` when missing.
-   Removed `Video` and `Info` links from the generated buttons (still accessible via right-click).

# Release 2.15
*2024-07-04*
---
-   Updated emulator versions in `EasyMode`.
-   Added `Amstrad CPC` to `EasyMode`.
-   Updated `mame.xml` to the latest release.
-   Updated `system_model.xml` to include Amstrad CPC.
-   Enhanced the `Update Class`.
-   Introduced a `Cart Context Menu`.
-   Improved the `Global Search Window` to include the `System Name` field.
-   Improved the `Global Stats Window` with a `pie chart`.
-   Fixed the `default image viewer` for various media types.
-   Implemented a `Favorite function`.
-   Added icons to the `Right-Click Context Menu` items.

# Release 2.14.4
*2024-06-06*
---
-   Improved the `Global Search algorithm` to accept logical operators `AND` and `OR`.
-   Fixed a hidden bug in the `Main Window search engine`.
-   Added an option for the user to select a `ROM Folder` in the `Easy Mode - Add System` Window.
-   Improved the algorithm for detection of `emulator parameters`.
-   Removed the `parameter validation requirement` to save a System in the `Expert Mode` Window.
-   Updated the emulator version and download links in `easymode.xml`.
-   Developed a mechanism to detect the version of `installed emulators` in the `Easy Mode - Add System` Class.

# Release 2.14.3
*2024-06-04*
---
-   Improved the `automatic update mechanism` and fixed a `pagination bug`.

# Release 2.14.2
*2024-06-02*
---
-   Fixed the `automatic update process`.

# Release 2.14.1
*2024-06-02*
---
-   Fixed a small bug in the `About` window.

# Release 2.14.0
*2024-06-02*
---
### Big Update Today!
-   Added `Easy Mode` to `Add a System`.
-   Improved error detection of `parameter values`.
-   Added a `Global Search` function.
-   Added a `Global Stats` Window.
-   Added a `right-click Context Menu` to every generated button.
-   Implemented an `auto-update feature` for the application.
-   Updated `mame.xml` to the latest version.
-   Improved `extraction method`.
-   Fixed all bugs found.

# Release 2.13.0
*2024-05-17*
---
-   Implemented an `experimental algorithm` to check the `emulator parameters`.
-   Improved the `extraction method` that extracts files to a temporary folder.
-   Made minor bug fixes.

# Release 2.12.1
*2024-03-31*
---
-   Improved the `error notification system`.
-   Fixed a bug with the `emulator location check`.
-   Implemented code to use the `MAME game description` instead of the filename in the `Video` and `Info` links.
-   Implemented code to handle missing `mame.xml` and `system_model.xml` files.

# Release 2.12.0
*2024-03-25*
---
-   Implemented basic checks for the `System Folder`, `System Image Folder`, and `Emulator Location`.
-   Made the `exception notifications` more user-friendly.
-   Devised a smart method for generating the `system.xml` file.

# Release 2.11.2.23
*2024-03-20*
---
-   Fixed a bug in the 'Show Games' option.

# Release 2.11.1.15
*2024-03-19*
---
-   Improved mechanism to handle games without `cover`.
-   Enhanced `error logging mechanism`.
-   Implemented handling for the loading of `corrupted images` into the `UI`.
-   Fixed some bugs and corrected variable names.

# Release 2.11.0.5
*2024-03-17*
---
-   Added a `Search Engine` to the application.
-   Updated `mame.xml` to the latest version.
-   Improved the launch error message.

# Release 2.10.0.10
*2024-03-12*
---
-   Fixed the `vertical scroll`.
-   The app now retains the `window size and state` between instances.
-   Users can now use a `default.png` file from the `System Image Folder`.
-   Users can now see all the `parameters` associated with the system in the `main UI`.
-   Added a `numeric value for pagination`.
-   Fixed some bugs.

# Release 2.9.0.90
*2024-03-10*
---
-   Added the option to display `all games` for the selected system.
-   Implemented `pagination` in the `main UI`.
-   Added an option to edit the `Video Link` and `Info Link`.
-   Added an option to `back up system.xml` from within the `Edit System Window`.
-   Added the option to use a `custom System Image Folder`.

# Release 2.8.2.10
*2024-03-06*
---
-   Bug fix.
-   Limit `Edit System Window` size.
-   Update values in `system.xml`.

# Release 2.8.1.6
*2024-03-03*
---
-   Bug fix.
-   Removed the `RefreshGameButtons` method.

# Release 2.8.0.5
*2024-03-03*
---
-   Added a new menu item called 'Edit System.'
-   Updated the method for counting files within the `System Folder`.
-   Optimized the current code.

# Release 2.7.0.1
*2024-02-07*
---
-   Added intelligent toggling of the `GamePadController` state.
-   Fixed the implementation of `GamePadController` in the `MainWindow`.
-   Added support for launching `.LNK` and `.EXE` files.
-   Added `Name` and `Email` fields to the `Bug Report Window`.
-   Other bug fixes.

# Release 2.6.3.4
*2024-01-28*
---
-   Bug fix.

# Release 2.6.2.3
*2024-01-28*
---
-   Adds support for loading `BAT files` or treating them as `games` in the `Emulator Frontend`.

# Release 2.6.0.1
*2024-01-24*
---
-   Fixed some code.
-   Improved error checking for `mame.xml`, `settings.xml`, and `system.xml`.
-   Added a `BugReport` window.

# Release 2.5.0.0
*2024-01-21*
---
-   Fixed code and updated libraries.
-   Added support for `JPG` and `JPEG` image formats.
-   Added `custom update notification`.
-   Improved `logging mechanism`.
-   Updated some systems in `system.xml`.

# Release 2.4
*2023-12-30*
---
-   Added a `mame.xml` database.
-   Updated the `UI` to display the `System Directory` and the `number of games`.
-   Updated the `parameters` within the `system.xml`.

# Release 2.3
*2023-12-26*
---
-   Added menu items for `Thumbnail Size`, `game visibility`, and `GamePad support`.
-   Added a `settings.xml` file to save `user preferences`.
-   Refined code for 'extract before launch' functionality.
-   Updated `libraries` and fixed some code.

# Release 2.2
*2023-11-25*
---
-   Updated to `.NET 8.0`.
-   Fixed some code and updated `libraries`.

# Release 2.1
*2023-11-02*
---
-   Fixed some code and `system.xml`.

# Release 2.0
*2023-10-29*
---
-   Major `UI improvements`.
-   Revised the method for loading `game files`.
-   Updated the process for loading `cover image files`.
-   Reworked the system for selecting `gaming systems` and `emulators`.
-   Implemented a `system.xml` for `system and emulator settings`.
-   Introduced 'extract before launch' functionality.
-   Implemented a `click sound feature`.
-   Incorporated `Video` and `Info` buttons into the UI.
-   Implemented `asynchronous image loading`.
-   Separated functions into `distinct classes`.

# Release 1.3
*2023-10-19*
---
-   Added `Xbox controller support`.
-   Added support for `CSO files`.
-   Improved and fixed code.

# Release 1.2
*2023-09-24*
---
-   Fixed a lot of code.
-   Added `Utilities` and `Show` menus.
-   Improved `UI`.
-   Added support for `CHD files`.

# Release 1.1
*2023-08-29*
---
-   Initial release.
