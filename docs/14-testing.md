# 14 — Testing

> The test projects, conventions, coverage, and how to run the tests.
> Related: [ManualTests.md](manual-tests.md) (manual checklist) · [02 — Projects & Solution](02-projects-and-solution.md)

## Projects

- `SimpleLauncher.Tests` — xUnit (`net10.0-windows`), references `SimpleLauncher` (and transitively `SimpleLauncher.Core`); `InternalsVisibleTo` gives access to internal members. **Fast suite.**
- `SimpleLauncher.Avalonia.Tests` — xUnit (`net10.0`, runs on Windows **and** Linux/WSL2), references `SimpleLauncher.Avalonia`; uses `Avalonia.Headless 12.1.1` for window construction tests. **Fast suite.**
- `SimpleLauncher.SlowTests` — xUnit (`net10.0-windows`), **slow suite**: live GitHub/PureLogicCode endpoint checks, real `SimpleLauncher.exe` app launches for the language checks (no other instance may run), real ZIP/CHD mount integration via Dokan (self-skips cleanly when the images/media or mount tools are absent).
- `SimpleLauncher.Avalonia.SlowTests` — xUnit (`net10.0`), **slow suite**: cross-platform mount extraction fallback and platform guards.
- Frameworks: **xUnit 2.9.3**, **Moq 4.20.72**, **Avalonia.Headless 12.1.1**, Serilog `ILogger` mocks; Meziantou analyzer enabled.
- **~154 WPF test files + 72 Avalonia test files (~226 total)** — **703 Avalonia tests** + **2207 WPF tests** in the fast suites, plus **83 WPF slow tests** + **11 Avalonia slow tests**. WPF parallelization is disabled (`AssemblyInfo.cs`: `CollectionBehavior(DisableTestParallelization = true)`) because several tests share static/WPF state; Avalonia tests run on a dedicated headless UI thread (`TestEnvironment.cs:HeadlessAvalonia`).
- **Linux regression coverage (LB-01 … LB-23)** — the cross-platform suite pins each fixed finding: ZIP/RPCS3/ScummVM temp extraction (`MountZipFilesCrossPlatformTests`), DOSBox CHD/ISO `imgmount` (`DosBoxChdLinuxTests`, `DosBoxIsoLinuxTests`, `Iso9660ImageReaderTests`), emulator pickers (`ExecutableFileFilterTests`) and `CheckPath` on Unix, case-insensitive extraction (`ExtractionServiceTests`, `DosBoxCaseSensitivityTests`), RA emulator-integration gating (`EmulatorConfigHandlerRegistrationTests`), audio decoders/degradation (`PlaySoundEffectsTests`), mount platform guards (`MountPlatformGuardTests`), archive entry normalization + Unix execute bits (`ExtractionServiceTests`), updater case-variant Zip-Slip and missing-asset Information logging (`AvaloniaCheckForUpdatesServiceTests`), invalid-executable errno + separator normalization (`PathHandlingCrossPlatformTests`), multi-track CHD cleanup (`ChdMountStrategyCleanupTests`).
- **Manual Linux smoke test** — the packaged `linux-x64` bundle (framework-dependent single-file, from `scripts/package-release-linux.ps1`) was run by hand on **Debian 13 (trixie) x64** (GNOME Wayland/Xwayland): first-run "Welcome" dialog and main window render with no errors (2026-09-24) — see [ManualTests.md](manual-tests.md).

## Test helpers

### `SimpleLauncher.Tests\TestHelpers\`

| Helper | Purpose |
|---|---|
| `NoOpLogger` | Serilog `ILogger` that discards everything |
| `NoOpMessageBoxLibraryService` | Stub for `IMessageBoxLibraryService` (large; all methods) |
| `NoOpCredentialProtector` | Plaintext credential protector for settings tests |
| `NoOpGetListOfFiles` / `NoOpResourceProvider` | Other no-op stubs |
| `ServiceProviderMock` | Installs a fake `IServiceProvider` where static access is required |
| `ProjectPathHelper` | Resolves repo-relative paths for tests (e.g. `parameters.md`) |
| `StaApartment` | Runs test actions on an STA thread (with optional WPF `Application`) for headless UI tests |

### `SimpleLauncher.Avalonia.Tests\TestEnvironment.cs` / `TestDependencies.cs`

| Helper | Purpose |
|---|---|
| `HeadlessAvalonia` | Initializes the Avalonia headless platform on a dedicated UI thread pumping `Dispatcher.UIThread`; `RunOnUiThread` / `WaitUntilAsync` helpers |
| `TestEnvironment` | Portable `settings.xml` in test output (never writes to `%LOCALAPPDATA%`); `ConfigurationFromJson` helper |
| `TestDependencies` | Builders for `SettingsManagerService`, `PlaySoundEffects`, `IResourceProvider` (returns fallback), `IHttpClientFactory` fake, `ILogger`/`IMessageBox` mocks |

## What is covered (summary)

**WPF (`SimpleLauncher.Tests`):** settings & system manager persistence, favorites, play history, game scanner core, file finder, search orchestrator, launch strategies (default, DOSBox, Commander Genius, CHD/CUE, PBP, XISO, Xemu image.iso, ZIP), mount-strategy matching, Core emulator config-injection services, models/DTOs, path/URL/sanitizer/pagination/filter helpers, RetroAchievements manager/matcher/hasher, Steam VDF parser, update-check logic, API connectivity, converters; shared localization packs (`SimpleLauncher.Core\Localization`, used-key scan/auto-add for `GetString`/`TryFindResource`, duplicate keys, alphabetical order, embedded-payload loading/packaging); plus 5.6.x additions: parameter resolver API service, `system.xml` writer + emulator XML helpers, game file watcher, loading overlay, UI reset, status bar, menu check-marks, credential protector (DPAPI), system-selection ViewModel, default-folder/temp/missing-file services. **New in this session:** `RetroAchievementsViewModel` (credentials-missing, success with profile/recently-played, null/unauthorized/network, motto fallback, permission mapping, unlocks/progress branches, `GetProfileUrl` encoding) and `RetroAchievementsSettingsViewModel` (save trimming, `ConfigureEmulator` empty-credentials, RetroArch vs. token-fetch, missing/existing token, no-RequestExePath, configurator success/failure/throws).

**Avalonia (`SimpleLauncher.Avalonia.Tests`):** all of the above plus Avalonia-specific services: `AvaloniaPaginationService`, `AvaloniaGameCacheService` + orchestrator, `AvaloniaLanguageMenuService` + `GameFileWatcherService`, `AvaloniaHelpUserService`, `AvaloniaCheckForUpdatesService` (GitHub + secondary server, zip-slip, updater launch), `AvaloniaTrayIconManager` (headless), converters (`BoolToVisibility`, `InverseBool`, `NullToVisibility`, `SmartTitleCase`, `BooleanToFavoriteStatus`, `ConsoleToCardHeight`, `PathToImage` with headless Bitmap), `SystemImageResolver`, `SteamVdfParser`, `PlaySoundEffects` (managed MP3/WAV decoder dispatch, expected-failure classification, corrupt-file Information logging, graceful no-device playback), 15 ViewModels (`About`, `Support`, `Debug`, `GlobalStats`, `SoundConfiguration`, `Set*`, `RomHistory`, `DownloadImagePack`, `WindowSelectionDialog`, `FlashOverlay`, `UpdateHistory`, `UpdateLog`, `FavoritesSection`, `EasyMode`, `Sidebar`, `InjectDolphin` + `InjectAres*` etc.), launch strategies (`ChdMount`, `PbpToCue`, `CommanderGenius`, `ChdToCue`, `DosBox`, `XemuMount` with priority ordering), `StorefrontGameScanner`, `EmulatorPathResolver`, `AppDataPaths`, `DeleteSystem` integration (atomic `system.xml` write via temp file + `SystemManager` reload). **New in this session:** `RetroAchievementsViewModelTests` (30 tests, same branches as WPF plus `FetchUnlocks` date validation, `ResetDates`, `GetProfileUrl` encoding, `Ctor` defaults) and `RetroAchievementsSettingsViewModelTests` (19 tests, async `RequestExePath`, token fetch/save, configurator dispatch for 7 emulators, success/failure/throws), `AvaloniaViewSmokeTests` (45 tests, headless construction of every Window: `About`, `Support`, `Debug`, `GlobalStats`, `UpdateHistory`/`UpdateLog`, `WindowSelectionDialog`, `FlashOverlay`, `DosBoxFileSelection`, `DownloadImagePack`, `ImageViewer`, `Preferences`, `EasyMode`, `SystemSelection`, `RetroAchievements*` (3), `Inject*` 21 windows, `SoundConfiguration`, `Set*`; verifies `!XamlIlPopulate` without a display and guards against `Cursor="SizeWE"` regressions), `DeleteSystemIntegrationTests` (4 tests, atomic delete + `InvalidateCache` + multi-delete), and the resource-coverage suite: `DetectMissingResourceStringsTests` (scans `.cs` `GetString(...)`/`.axaml` `{ext:Translate Key}` usage, auto-adds missing keys + fallbacks to `strings.en.json`, and verifies every language file has the full English key set), `DetectMismatchedResourceStringsTests` (same key, different C# fallback literals across languages) and `LocalizationTests` (key parity with per-language missing-key report + translator hint, empty-value detection, AXAML `TranslateExtension` usage).

**What remains not covered (manual/live):** live file mounting (Dokan `SimpleZipDrive`/`SimpleXisoDrive`/`CHDMounter`), store scanners against real registry installs, gamepad/DirectInput, NAudio device playback, RA live API against real accounts, per-emulator launch with real executables — see **[ManualTests.md](manual-tests.md)** for the full manual checklist.

## Running the tests

```bash
# WPF — fast suite, unfiltered (~40 s)
dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj

# Avalonia — fast suite (headless, runs on Windows and Linux/WSL2, no display required, ~20 s)
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj

# WPF — single class
dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj \
  --filter "FullyQualifiedName~RetroAchievementsViewModelTests"

# Slow suites (only before a deploy or when explicitly asked, ~3.5 min / ~1 s)
dotnet test SimpleLauncher.SlowTests/SimpleLauncher.SlowTests.csproj
dotnet test SimpleLauncher.Avalonia.SlowTests/SimpleLauncher.Avalonia.SlowTests.csproj
```

> ⚠ **Fast/slow split (no filtering):** the suites are split **by project**, never by `[Trait]`/filter. During iteration run only the two fast suites; the slow suites (live endpoints, real app launches, real ZIP/CHD mounts) run **only before a deploy or when the admin asks**. Every test in a project always runs unfiltered with `dotnet test` — never add a filter, quarantine or runtime skip. Slow tests self-skip cleanly when their media/tools are absent (mount integration needs the Dokan driver and the bundled mount tools).

## Writing new tests — conventions

- One test file per production class: `XxxTests.cs`; test class name matches the target.
- `using Xunit;` explicitly in WPF (no global using); Avalonia has `<Using Include="Xunit" />` globally so `using Xunit;` is optional.
- Mock interfaces with Moq; use real temp directories for filesystem services (`Path.GetTempPath()` + GUID, cleanup in `Dispose`).
- **WPF**-touching tests run inside `StaApartment.Run/RunAsync` (see `LoadingOverlayServiceTests`, `MenuCheckMarkServiceTests`, `UiResetServiceTests`).
- **Avalonia**-touching tests use `HeadlessAvalonia.RunOnUiThread` / `HeadlessAvalonia.EnsureInitialized` + `TestDependencies` builders (see `ConverterTests`, `AvaloniaViewSmokeTests`, `RetroAchievementsViewModelTests`). Bitmap/TrayIcon/Dispatcher require the headless platform.
- Timing-based tests (watcher debounce) keep delays small and use `TaskCompletionSource` + `WaitAsync` timeouts.
- String assertions use `StringComparer.Ordinal` overloads (Meziantou MA0002/MA0074).
- For HTTP-dependent services (`AvaloniaCheckForUpdatesService`, `RetroAchievementsService`), inject a fake `HttpMessageHandler` via `TestDependencies.HttpClientWith` / `TestDependencies.HttpFactory` — never hit live endpoints.

## Related docs

- [ManualTests.md](manual-tests.md)
- [15 — Development](15-development.md)
- [13 — Logging & Debug](13-logging-and-debug.md)
