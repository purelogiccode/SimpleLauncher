# 02 — Projects & Solution

> The solution `SimpleLauncher.sln` and the two documented projects in depth.
> Related: [01 — Overview](01-overview.md) · [04 — Architecture](04-architecture.md)

## Solution layout (10 projects)

| Project | Kind | Role |
|---|---|---|
| `SimpleLauncher` | WPF app (WinExe) | **The launcher** — UI, ViewModels, services, launch handlers, scanners, DI composition root |
| `SimpleLauncher.Core` | Class library | **Shared logic** — services, models, interfaces, persistence, emulator config injection |
| `SimpleLauncher.Tests` | xUnit test project | ~152 test files; references `SimpleLauncher` (and transitively Core) |
| `SimpleLauncher.Avalonia.Tests` | xUnit test project | ~48 test files (518 tests, `net10.0`, runs on Windows + Linux/WSL2 via `Avalonia.Headless`) |
| `SimpleLauncher.Avalonia.Updater` | Avalonia app (WinExe) | The single updater (`Updater.exe`) for both apps — downloads the unified release zip, extracts over the app folder, relaunches the app that launched it |
| `SimpleLauncher.Avalonia` | Avalonia UI app | Cross-platform port (Windows + Linux); phases 1–11 of [`References/AvaloniaPlan.md`](../References/AvaloniaPlan.md) done |
| `SimpleLauncher.ResourceTranslator` | Tool | Translates missing keys in the shared packs (`SimpleLauncher.Core\Localization\strings.*.json`) via the OpenRouter API (default `z-ai/glm-5.3-flash`); see its [README](../SimpleLauncher.ResourceTranslator/README.md) |
| `Tools\Mame.DatCreator` | WPF tool | Builds `mame.dat` (MessagePack) from MAME `-listxml` + software lists |
| `Tools\RetroAchievements.DataFetcher` | CLI tool | Fetches the RA game database into `RetroAchievements.dat` |
| `Tools\XmlToBinaryConverter` | WPF tool | Converts `history.xml` ↔ `history.dat` (MessagePack) |

Dependency edges: `SimpleLauncher → SimpleLauncher.Core`; `SimpleLauncher.Tests → SimpleLauncher`; `SimpleLauncher.Avalonia.Tests → SimpleLauncher.Avalonia`; `SimpleLauncher.Avalonia.Updater` standalone (no compile reference — the Avalonia app publishes it and copies `Updater.exe` next to itself); `SimpleLauncher.Avalonia` references Core (via `InternalsVisibleTo`). `SimpleLauncher.Avalonia.Tests` targets `net10.0` (not `-windows`) so it runs on Linux CI/WSL2 without the Windows desktop pack.

## `SimpleLauncher\SimpleLauncher.csproj` (the app)

Key properties:

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<OutputType>WinExe</OutputType>
<UseWPF>true</UseWPF>
<LangVersion>14</LangVersion>
<Nullable>enable</Nullable>
<RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
<ApplicationManifest>app.manifest</ApplicationManifest>
<StartupObject>SimpleLauncher.App</StartupObject>
<AssemblyVersion>/<FileVersion>/<Version>5.7.0</Version>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<RuntimeFrameworkVersion>10.0.2</RuntimeFrameworkVersion>
<SupportedOSPlatformVersion>7.0</SupportedOSPlatformVersion>
```

- **Versioning:** `5.7.0`; kept consistent with Core, tests and `app.manifest` (`VersionConsistencyTests` enforces it).
- **`InternalsVisibleTo("SimpleLauncher.Tests")`** — tests reach internal members.
- **Copy-to-output payloads** (`<None Update=... CopyToOutputDirectory>`):
  - `appsettings.json` (Always), `WhatsNew.md` (PreserveNewest), `mame.dat` (Always), system images `images\systems\*.png` (Always), `audio\*.mp3`.
  - `tools\*` payloads: `findromcover\`, `createbatchfilesforscummvmgames\`, `createbatchfilesforwindowsgames\`, `BatchConvertTo7z\`, `CreateBatchFilesForPS3Games\` (stale entries for `ps3batchlaunchercreator\` remain in the csproj but have no folder on disk — see [11 — Bundled Tools](11-bundled-tools.md)), plus 7za binaries, system images, and more.
- **Global usings:** `System.IO`, `System.Net.Http`, `Serilog`.
- **`NoWarn`: NU1903;CS0436**.
- Startup object is `SimpleLauncher.App` (see [04 — Architecture](04-architecture.md#startup-sequence)).

### App packages (selected)

| Package | Version | Used for |
|---|---|---|
| MahApps.Metro | 2.4.11 | UI theme/controls |
| CommunityToolkit.Mvvm | 8.4.2 | ViewModels (ObservableObject, RelayCommand) |
| MessagePack | 3.1.8 | Binary data files |
| Microsoft.Extensions.* | 10.0.10 | Configuration, DI, HTTP, resilience |
| Microsoft.Extensions.Http.Resilience | 10.8.0 | Polly retry policy on downloads |
| Microsoft.Data.Sqlite / SourceGear.sqlite3 | 10.0.10 / 3.53.4 | SQLite (Amazon scan, Stella settings) |
| SharpCompress | 0.50.4 | Archive extraction |
| NAudio (Core / Wasapi / WinMM / SoundFile / Alsa) | 3.0.0 | UI sound effects (Windows + Linux) |
| SharpDX + XInput + DirectInput | 4.2.0 | Gamepad input |
| InputSimulatorCore | 1.0.5 | Mouse simulation from gamepad |
| Serilog (+ Sinks.Async/Debug/File) | 4.4.0 | Logging |
| Tomlyn | 2.10.1 | TOML parsing (Xenia, Yumir configs) |
| YamlDotNet | 18.1.0 | YAML parsing (RPCS3 config) |
| Meziantou.Analyzer | 3.0.157 | Static analysis (PrivateAssets) |
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.302 | Static analysis |

## `SimpleLauncher.Core\SimpleLauncher.Core.csproj` (the library)

Key properties: `net10.0-windows`, `IsPackable=true`, `Nullable` enabled, `LangVersion 14`, `DebugType=embedded`, version `5.7.0`.

- **`InternalsVisibleTo`:** `SimpleLauncher.Tests`, `SimpleLauncher`, `SimpleLauncher.New`, `SimpleLauncher.Avalonia`, `SimpleLauncher.New.Tests`.
- **Global usings:** `System.IO`, `System.Net.Http`, `Serilog` — so every Core service takes a Serilog `ILogger` by convention.
- Packages: the same core set as the app (CommunityToolkit.Mvvm, MessagePack, Microsoft.Extensions.*, SharpCompress, NAudio, SharpDX*, Serilog, Tomlyn, YamlDotNet, SourceGear.sqlite3, Meziantou.Analyzer) — no WPF/MahApps (it targets `net10.0-windows` because of DPAPI `ProtectedData`, `System.Drawing`-adjacent helpers, and Windows-specific services, but stays UI-agnostic). RetroAchievements hashing is **not** a package: all hash computation is delegated to the bundled `tools\RetroAchievementsSharp\` CLI binaries (see [09 — RetroAchievements](09-retroachievements.md#hashing)).
- **Shared localization packs:** `Localization\strings.{lang}.json` (18 languages, canonical `strings.en.json`) are the single source for both apps — WPF embeds them, Avalonia links/copies them to `Resources\` (see [15 — Development](15-development.md#localization)).

## `SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj` (the cross-platform port)

Key properties: `net10.0` + `net10.0-windows` (dual target — the `net10.0` TFM is Linux-only and rejects Windows RIDs via a build guard), `Avalonia 12.1.1`, `UseWindowsForms=False`, version synced to the WPF app (5.7.0), `StartupObject = SimpleLauncher.Avalonia.Program`.

- **Reuses `SimpleLauncher.Core`** for all business logic (launch, scanning, persistence, emulator config injection, RA).
- **Windows-only services** (`#if WINDOWS`, `net10.0-windows`): F8 global hotkey (`AvaloniaGlobalHotkeyService`), active-window screenshot (`AvaloniaActiveWindowScreenshotService` + `WindowScreenshot` Win32 helpers, `System.Drawing.Common` package conditional on the windows TFM).
- **Cross-platform services**: `AvaloniaTrayIconManager` (Avalonia `TrayIcon` + `NativeMenu`; `icon\icon.ico` copied to output), `AvaloniaFilePickerService`, `AvaloniaDispatcherService`, JSON localization (`Resources\strings.*.json`, 18 languages, 2669 keys each, all files in full key parity with `strings.en.json`; the packs are shared with WPF from `SimpleLauncher.Core\Localization`).
- Port status and remaining work: [`References/AvaloniaPlan.md`](../References/AvaloniaPlan.md) and [`References/TODO.md`](../References/TODO.md). All 44 windows and 21 `Inject*` dialogs are headless-smoke-tested via `AvaloniaViewSmokeTests` (45 window + 21 inject tests); `RetroAchievementsViewModel` and `RetroAchievementsSettingsViewModel` have full unit coverage in both WPF and Avalonia (30 + 19 tests).

## Folder structure of the app project

```
SimpleLauncher/
├── App.xaml(.cs)            DI composition root, startup sequence
├── MainWindow.xaml(.cs)     + 12 partials (hosts, menus, pagination, search, close events…)
├── *.xaml(.cs)              22 root windows (About, Debug, EasyMode, EditSystem, InjectConfig…)
├── InjectConfigWindows/     21 emulator config-injection dialogs
├── Pages/                   Favorites, GlobalSearch, PlayHistory
├── ViewModels/              44 ViewModels (incl. 21 Inject*ConfigViewModel)
├── Services/                UI services, launch handlers, scanners, RA, favorites, play history…
├── Interfaces/              host & service interfaces (29)
├── Models/                  app-side models (SearchResult, WindowScreenshot, RaAchievement…)
├── resources2/              theme overrides (HighContrast, Midnight)
├── tools/                   bundled executables (see 11)
├── samples/                 emulator config templates (samples\{Emulator}\*)
├── audio/ images/ icon/     shipped assets
└── appsettings.json, app.manifest, mame.dat, parameters.md, WhatsNew.md
```

## Folder structure of the Core project

```
SimpleLauncher.Core/
├── Services/                ~30 areas (see 07 — Core Services)
├── Models/                  data models (Ra*, History, emulator settings, converters…)
├── Interfaces/              service contracts
└── (no UI, no XAML)
```

## Related docs

- [01 — Overview](01-overview.md)
- [07 — Core Services](07-core-services.md)
- [11 — Bundled Tools](11-bundled-tools.md)
- [15 — Development](15-development.md)
