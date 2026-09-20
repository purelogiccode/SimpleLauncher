# 07 — Core Services Catalog

> Every service class in `SimpleLauncher.Core\Services\`, grouped by area.
> Related: [04 — Architecture](04-architecture.md) · [06 — Systems & Launch](06-systems-and-launch.md)

All Core services follow the same conventions: Serilog `ILogger` injected (global `using Serilog`), `InternalsVisibleTo` for app/tests, no WPF dependencies. Interfaces live in `SimpleLauncher.Core\Interfaces\` and are the DI registration types; implementations resolve through `App.ServiceProvider` (see [04 — Architecture](04-architecture.md#dependency-injection)).

## Settings & configuration

| Class | Purpose / key API |
|---|---|
| `SettingsManager\SettingsManagerService` | User preferences (`settings.dat` SQLite; legacy `settings.xml`): load/save with atomic temp-file move, 3 retries, whitelist validation, DPAPI-encrypted RA credentials. See [05 — Configuration](05-configuration.md) |
| `SettingsDatabase\UnifiedSettingsDatabase` | Schema/read/write for the shared `settings.dat` (app + emulator settings, favorites, play history, systems, play times) used by both apps |
| `SettingsDatabase\WpfLegacyMigrator` / `SettingsDatabase\AvaloniaLegacyMigrator` (each app) | First-launch/merge migration of `settings.xml`, `system.xml`, `favorites.dat`, `playhistory.dat` into `settings.dat` with verification and `.bak` shelving |
| `SettingsManager\EmulatorXmlHelpers` | Static typed XML readers (`ReadBool/ReadInt/ReadDouble/ReadString`) with section → flattened-root → default fallback |
| `SettingsManager\EmulatorSettings\*` (21) | `XxxSettings` classes (Ares…Yumir) backing the inject-config dialogs; stored in `settings.dat` (`EmulatorSettings`; legacy `settings.xml`) |
| `SystemConfiguration\SystemConfigurationWriterService` | Read/write/delete/`SystemExists` on `system.xml`; alphabetical sort, retry, temp-file+move. See [05](05-configuration.md#systemxml) |
| `DataFileLocation` | Portable vs `%LocalAppData%\SimpleLauncher` file resolution (portable wins if newer) |

## Paths, files & cleanup

| Class | Purpose |
|---|---|
| `CheckPaths\PathHelper` | `ResolveRelativeToAppDirectory`, `TryGetExistingDirectory`, path normalization (used everywhere); `GetLongPath` applies the Windows `\\?\` prefix only on Windows (POSIX paths are returned untouched) and `%BASEFOLDER%\...` configs with backslashes resolve on Unix |
| `CheckPaths\CheckPath` | Path existence/validity checks incl. extended-length paths (Windows-only prefix) |
| `CheckIfDirectoryIsWritable\DirectoryValidationService` / `CheckIfDirectoryIsWritableService` | Writability probe (temp write+delete) |
| `CheckForFileLock\FileLockService` / `CheckForFileLockService` | Detect/retry on locked files |
| `CleanAndDeleteFiles\DeleteFilesService` (+ legacy `DeleteFiles`) | Best-effort file/dir deletion |
| `CleanAndDeleteFiles\CleanTempFolderService` (+ legacy `CleanTempFolder`) | Delete temp extraction dirs; `.extraction_in_progress` partial cleanup |
| `CleanAndDeleteFiles\CleanSimpleLauncherFolderService` | Cleanup of the app folder (trash/temp) at startup |
| `CheckForRequiredFilesService` | Startup check that shipped files exist → missing-file dialog |
| `CreateDefaultSystemFoldersService` | Creates system/image/additional folders (`AdditionalFolders`) for a new system |

## Launch, mount & convert

| Class | Purpose |
|---|---|
| `GameLauncher\Strategies\DefaultLaunchStrategy` | Fallback strategy (priority 999): bat/lnk/exe/regular emulator launch |
| `GameLauncher\Strategies\XisoMountStrategy` | Cxbx + `.iso` → mount → `default.xbe` |
| `GameLauncher\Strategies\ZipMountStrategy` | RPCS3/ScummVM/XBLA archive mounting (Windows: SimpleZipDrive; Linux/macOS: temp extraction) |
| `GameLauncher\MountFiles\MountChdFiles` / `MountChdDrive` | CHDMounter orchestration (Dokan check, console alias, poll 120 s, kill+20 s unmount) |
| `GameLauncher\MountFiles\MountIsoFiles` | PowerShell `Mount-DiskImage` / `Dismount-DiskImage`, EBOOT.BIN discovery |
| `GameLauncher\MountFiles\MountXisoFiles` / `MountXisoDrive` | SimpleXisoDrive (Dokan), drive letter Z→D, `default.xbe` poll |
| `GameLauncher\MountFiles\MountZipFiles` | Archive mounting (Windows: zip to virtual drive; Linux/macOS: extract to a temp directory and launch from there) |
| `GameLauncher\MountFiles\Iso9660ImageReader` | Lists the files of a cooked ISO9660 image (chdman/CHDSharp `extractcd` data track, DVD ISO) — primary 8.3 names, used by the DOSBox CHD fallback on Linux/macOS |
| `GameLauncher\MountFiles\FindEbootBin`, `FindDefaultXbe`, `FindDefaultXex`, `FindImageIso`, `FindBinFile`, `FindCueFile`, `FileFinderService` | Launch-file discovery inside mounted volumes |
| `GameLauncher\MountFiles\DokanValidation` | P/Invoke `dokan2.dll` version check |
| `GameLauncher\ValidateBatchFile` | Pre-execution validation of batch files (missing paths) |
| `ExtractFiles\ExtractionService` | Archive extraction: lock retry, disk-space check, path-traversal guard, 7-Zip fallback, `.extraction_in_progress` marker. See [06](06-systems-and-launch.md#extraction) |
| `Converters\DiscConverter` | CHD→ISO/CUE-BIN, PBP→CUE-BIN, RVZ/WBFS/GCZ→ISO via bundled tools (5-min timeouts) |
| `ExternalToolLauncher\ExternalToolLauncherService` | Launch bundled tools: arch-aware paths, PE validation, per-tool methods. See [11](11-bundled-tools.md) |

## Search, scan & data

| Class | Purpose |
|---|---|
| `GetListOfFiles\GetListOfFilesService` | Enumerate files (filters, recursion) |
| `FindCoverImage\FindCoverImageService` | Cover lookup with fuzzy matching (Jaro-Winkler) + annotation stripping |
| `MameManager\MameManagerService` | Loads `mame.dat` (MessagePack), machine descriptions |
| `MameData\MameDataService` | App-facing MAME data access (`Machines`, `Lookup`) |
| `RomHistory\RomHistoryLoader` | Loads ROM history: `history.dat` (MessagePack) with `history.xml` fallback |
| `SanitizeInputString\InputSanitizerService` / `SanitizeInputSystemName` | Input/name sanitization |
| `CheckApplicationControlPolicyService` | Win32 error classification (elevation 740, AppLocker 5, canceled 1223) |
| `UsageStats\Stats` | Anonymous usage-statistics API calls (with timeout) |

## Download & Easy Mode

| Class | Purpose |
|---|---|
| `DownloadService\DownloadManager` | HTTP downloads with progress, retry + exponential backoff, disk-space check, cancellation |
| `DownloadService\FormatFileSizeService` (+ `FormatFileSize`) | Human-readable file sizes |
| `EasyMode\EasyModeManager` | Easy Mode manifest: systems → emulator/core/image-pack download links (+ cache) |

## Emulator config injection (`InjectEmulatorConfig\`)

21 `XxxConfigurationService` classes — one per emulator (Ares, Azahar, Blastem, Cemu, Daphne, Dolphin, DuckStation, Flycast, MAME, Mednafen, Mesen, PCSX2, Raine, Redream, RetroArch, RPCS3, SegaModel2, Stella, Supermodel, Xenia, Yumir) — plus `AzaharPermissionException` and `Pcsx2PermissionException`. Each writes the emulator's own config file from the `XxxSettings` object; missing files are restored from `samples\{Emulator}\*`. Details in [06 — Systems & Launch](06-systems-and-launch.md#emulator-config-handlers).

## RetroAchievements

| Class | Purpose |
|---|---|
| `RetroAchievements\RetroAchievementsManager` | RA data store (`RetroAchievements.dat`, MessagePack): game info, achievements, recently played, completion progress |
| `RetroAchievements\RetroAchievementsSystemMatcher` | `SystemMappings` (official name → console ID + aliases), best-match/alias lookups |
| `RetroAchievements\RetroAchievementsFileHasher` | Hash logic: standard MD5, header-based (NES/SNES/Atari…), N64 byte-swap, arcade filename, Arduboy line-endings |
| `RetroAchievements\RetroAchievementsEmulatorConfiguratorService` | Inject RA credentials into RetroArch, PCSX2, DuckStation (encrypted token), PPSSPP (+ `.dat`), Dolphin, Flycast, BizHawk (JSON); restore from samples |
| `RetroAchievements\EncryptDuckStationToken` | DuckStation token encryption |
| `WpfServices\WindowsCredentialProtector` | DPAPI protect/unprotect (`ICredentialProtector`) |

## Input, audio & UI helpers

| Class | Purpose |
|---|---|
| `GamePad\GamePadController` | Windows: XInput + DirectInput (SharpDX) mouse/keyboard simulation, dead zones, reconnect. Linux/macOS: `SdlGamepadBackend` (Hexa.NET.SDL2, bundled native `libSDL2`) raises `InputChanged` snapshots that the Avalonia app's `GamepadNavigationService` turns into focus/activation/context-menu/scroll actions |
| `PlaySound\PlaySoundEffects` | NAudio 3 sound effects — Media Foundation + WaveOut (Windows) / libsndfile + ALSA (Linux); respects settings |
| `AudioInputService` | Audio input abstraction |
| `WpfServices\WpfImageLoader` | Image loading with `default.png` fallback |
| `TakeScreenshot\WindowManager` | Enumerate top-level windows for screenshots |
| `DebugAndBugReport\GetMicrosoftWindowsVersion` / `WindowsVersionService` | Windows version detection for bug reports |

## Debug & bug reports

| Class | Purpose |
|---|---|
| `DebugAndBugReport\BugReportApiSink` | Serilog sink: queues events, POSTs to bug-report API, writes `error.log`/`error_user.log`/`critical_error.log` on failure |
| `Interfaces\NoOpDebugLogger` | No-op `IDebugLogger` fallback |

## Interfaces (`SimpleLauncher.Core\Interfaces\`)

Contracts for all of the above plus app-side contracts shared with the WPF layer: `IApplicationLifetime`, `IAudioInputService`, `ICleanTempFolderService`, `ICredentialProtector`, `IDebugLogger`, `IDeleteFilesService`, `IDirectoryValidationService`, `IDiscConverter`, `IDispatcherService`, `IEmulator`, `IEmulatorConfigHandler`, `IEmulatorSettings`, `IExternalToolLauncher`, `IExtractionService`, `IFileFinderService`, `IFileLockService`, `IFilePickerService`, `IFindCoverImageService`, `IFormatFileSizeService`, `IGetListOfFilesService`, `IIconExtractor`, `IImageLoader`, `IInputSanitizerService`, `ILaunchStrategy`, `ILauncherService`, `ILoadingState`, `IMameDataService`, `IMessageBoxLibraryService`, `IMessageDialogService`, `IMountChdFiles`, `IMountIsoFiles`, `IMountXisoFiles`, `IMountZipFiles`, `IPaginationHost`, `IPaginationService`, `IParameterResolverService`, `IPlaySoundEffects`, `IResourceProvider`, `IRetroAchievementsEmulatorConfiguratorService`, `IRetroAchievementsFileHasher`, `IRetroAchievementsHasherTool`, `IRetroAchievementsSystemMatcher`, `ISearchOrchestratorService`, `ISteamVdfParser`, `ISystemConfigurationWriterService`, `ISystemManager`, `IUiResetHost`, `IUiResetService`, `IWindowContext`, `IWindowsVersionService`.

## Models (`SimpleLauncher.Core\Models\`)

Data types: `LaunchContext`, `Emulator`/`IEmulator`, `SystemManagerConfig`, `GameButtonTag`, `GameListViewItem`, `Favorite`, `PlayHistoryItem`, `DosBoxFileItem`, `HistoryData`/`EntryData`/`ItemData`/`SoftwareData`, `MameMachineData`, `Ra*` (see [09](09-retroachievements.md)), `ParameterResolverRequest/Result`, `GameImageApiResponse`, `GameClassificationItem/Response`, store models (`EpicInstalledApp`, `GogGameInfo`/`GogPlayTask`, `RockstarGameDef`, `BNetAppDef`, `StoreAppInfo`, `TagOption`), `SystemPlayTime`, `SystemStatsData`/`GlobalStatsData`, `SystemValidationResult`, `WindowItem`, `DownloadButtonState`/`DownloadProgressEventArgs`, `GamePlayedEventArgs`, `EventArgs<T>`, `BoolConverter` (0/1 JSON), `MessageBoxButton/Image/Result` (WPF-independent enums).

## Related docs

- [04 — Architecture](04-architecture.md)
- [05 — Configuration](05-configuration.md)
- [06 — Systems & Launch](06-systems-and-launch.md)
- [09 — RetroAchievements](09-retroachievements.md)
- [12 — Data Formats](12-data-formats.md)
