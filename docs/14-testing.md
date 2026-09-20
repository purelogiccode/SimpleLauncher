# 14 — Testing

> The test projects, conventions, coverage, and how to run the tests.
> Related: [ManualTests.md](manual-tests.md) (manual checklist) · [02 — Projects & Solution](02-projects-and-solution.md)

## Projects

- `SimpleLauncher.Tests` — xUnit (`net10.0-windows`), references `SimpleLauncher` (and transitively `SimpleLauncher.Core`); `InternalsVisibleTo` gives access to internal members.
- `SimpleLauncher.Avalonia.Tests` — xUnit (`net10.0`, runs on Windows **and** Linux/WSL2), references `SimpleLauncher.Avalonia`; uses `Avalonia.Headless 12.1.1` for window construction tests.
- Frameworks: **xUnit 2.9.3**, **Moq 4.20.72**, **Avalonia.Headless 12.1.1**, Serilog `ILogger` mocks; Meziantou analyzer enabled.
- **~154 WPF test files + 67 Avalonia test files (~221 total)** — **614 Avalonia tests** + **2109 WPF tests** (21 WPF live mount/integration tests require the local `G:`/`X:`/`J:` test drives and self-skip when absent). WPF parallelization is disabled (`AssemblyInfo.cs`: `CollectionBehavior(DisableTestParallelization = true)`) because several tests share static/WPF state; Avalonia tests run on a dedicated headless UI thread (`TestEnvironment.cs:HeadlessAvalonia`).

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

**WPF (`SimpleLauncher.Tests`):** settings & system manager persistence, favorites, play history, game scanner core, file finder, search orchestrator, launch strategies (default, DOSBox, Commander Genius, CHD/CUE, PBP, XISO, ZIP), mount-strategy matching, Core emulator config-injection services, models/DTOs, path/URL/sanitizer/pagination/filter helpers, RetroAchievements manager/matcher/hasher, Steam VDF parser, update-check logic, API connectivity, converters; shared localization packs (`SimpleLauncher.Core\Localization`, used-key scan/auto-add for `GetString`/`TryFindResource`, duplicate keys, alphabetical order, embedded-payload loading/packaging); plus 5.6.x additions: parameter resolver API service, `system.xml` writer + emulator XML helpers, game file watcher, loading overlay, UI reset, status bar, menu check-marks, credential protector (DPAPI), system-selection ViewModel, default-folder/temp/missing-file services. **New in this session:** `RetroAchievementsViewModel` (credentials-missing, success with profile/recently-played, null/unauthorized/network, motto fallback, permission mapping, unlocks/progress branches, `GetProfileUrl` encoding) and `RetroAchievementsSettingsViewModel` (save trimming, `ConfigureEmulator` empty-credentials, RetroArch vs. token-fetch, missing/existing token, no-RequestExePath, configurator success/failure/throws).

**Avalonia (`SimpleLauncher.Avalonia.Tests`):** all of the above plus Avalonia-specific services: `AvaloniaPaginationService`, `AvaloniaGameCacheService` + orchestrator, `AvaloniaLanguageMenuService` + `GameFileWatcherService`, `AvaloniaHelpUserService`, `AvaloniaCheckForUpdatesService` (GitHub + secondary server, zip-slip, updater launch), `AvaloniaTrayIconManager` (headless), converters (`BoolToVisibility`, `InverseBool`, `NullToVisibility`, `SmartTitleCase`, `BooleanToFavoriteStatus`, `ConsoleToCardHeight`, `PathToImage` with headless Bitmap), `SystemImageResolver`, `SteamVdfParser`, `PlaySoundEffects`, 15 ViewModels (`About`, `Support`, `Debug`, `GlobalStats`, `SoundConfiguration`, `Set*`, `RomHistory`, `DownloadImagePack`, `WindowSelectionDialog`, `FlashOverlay`, `UpdateHistory`, `UpdateLog`, `FavoritesSection`, `EasyMode`, `Sidebar`, `InjectDolphin` + `InjectAres*` etc.), launch strategies (`ChdMount`, `PbpToCue`, `CommanderGenius`, `ChdToCue`, `DosBox` with priority ordering), `StorefrontGameScanner`, `EmulatorPathResolver`, `AppDataPaths`, `DeleteSystem` integration (atomic `system.xml` write via temp file + `SystemManager` reload). **New in this session:** `RetroAchievementsViewModelTests` (30 tests, same branches as WPF plus `FetchUnlocks` date validation, `ResetDates`, `GetProfileUrl` encoding, `Ctor` defaults) and `RetroAchievementsSettingsViewModelTests` (19 tests, async `RequestExePath`, token fetch/save, configurator dispatch for 7 emulators, success/failure/throws), `AvaloniaViewSmokeTests` (45 tests, headless construction of every Window: `About`, `Support`, `Debug`, `GlobalStats`, `UpdateHistory`/`UpdateLog`, `WindowSelectionDialog`, `FlashOverlay`, `DosBoxFileSelection`, `DownloadImagePack`, `ImageViewer`, `Preferences`, `EasyMode`, `SystemSelection`, `RetroAchievements*` (3), `Inject*` 21 windows, `SoundConfiguration`, `Set*`; verifies `!XamlIlPopulate` without a display and guards against `Cursor="SizeWE"` regressions), `DeleteSystemIntegrationTests` (4 tests, atomic delete + `InvalidateCache` + multi-delete), and the resource-coverage suite: `DetectMissingResourceStringsTests` (scans `.cs` `GetString(...)`/`.axaml` `{ext:Translate Key}` usage, auto-adds missing keys + fallbacks to `strings.en.json`, and verifies every language file has the full English key set), `DetectMismatchedResourceStringsTests` (same key, different C# fallback literals across languages) and `LocalizationTests` (key parity with per-language missing-key report + translator hint, empty-value detection, AXAML `TranslateExtension` usage).

**What remains not covered (manual/live):** live file mounting (Dokan `SimpleZipDrive`/`SimpleXisoDrive`/`CHDMounter`), store scanners against real registry installs, gamepad/DirectInput, NAudio device playback, RA live API against real accounts, per-emulator launch with real executables — see **[ManualTests.md](manual-tests.md)** for the full manual checklist.

## Running the tests

```bash
# WPF — all tests, unfiltered (includes live endpoints and real app launches; ~3 min)
dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj

# WPF — single class
dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj \
  --filter "FullyQualifiedName~RetroAchievementsViewModelTests"

# Avalonia — all tests (headless, runs on Windows and Linux/WSL2, no display required)
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj

# Avalonia — single class
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj \
  --filter "FullyQualifiedName~RetroAchievementsViewModelTests"

# Both solutions
dotnet test SimpleLauncher.sln -c Debug
```

> ⚠ **Known slow/live tests (WPF):** `UrlValidationTests.ParametersMdAllUrlsAreReachable` pings every URL in `parameters.md` over the network **without a timeout** — it can take 15+ minutes or hang when a URL is unreachable. `MountZipFilesIntegrationTests` / `MountChdFilesIntegrationTests` require real files on `G:\`, `X:\`, `J:\` drives and fail when missing. All of them run unfiltered in every `dotnet test` (no quarantine — see `AGENTS.md`); a full local run on a machine without that media reports those failures and still exercises everything else.

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
