# 15 — Development

> Build, test, publish, versioning, localization, code style.
> Related: [02 — Projects & Solution](02-projects-and-solution.md) · [14 — Testing](14-testing.md)

## Prerequisites

- .NET SDK **10.0.x** (`global.json` pins `10.0.0`, roll-forward latestMajor, no prereleases).
- Windows (the projects target `net10.0-windows` and use WPF/DPAPI).

## Build & test

```bash
# build everything (Windows, both TFMs)
dotnet build SimpleLauncher.sln -c Debug
dotnet build SimpleLauncher.sln -c Release

# build one project
dotnet build SimpleLauncher/SimpleLauncher.csproj -c Debug
dotnet build SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Debug

# run unit tests — WPF (net10.0-windows) and Avalonia (net10.0, headless)
dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj -c Debug
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj -c Debug

# fast local WPF run (skip live mount + network tests that need G:\/X:\/J:\ or internet)
dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj -c Debug \
  --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~ApiConnectivity&FullyQualifiedName!~UrlValidation&FullyQualifiedName!~MountChd&FullyQualifiedName!~MountZip"

# WSL2 / Linux — Avalonia only (net10.0 TFM, no Windows desktop pack)
dotnet build SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Debug -f net10.0
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj -c Debug
wsl dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj -c Debug
```

See [14 — Testing](14-testing.md) for test filters and the known slow network test. Tests are
**not** run by CI (the suites include live endpoints and real app launches); verification stays
local + WSL2. The GitHub Actions workflows only package releases and deploy the docs — see
[Continuous integration](#continuous-integration).

## Publish (win-x64 / win-arm64)

```bash
dotnet publish SimpleLauncher/SimpleLauncher.csproj -c Release -r win-x64
dotnet publish SimpleLauncher/SimpleLauncher.csproj -c Release -r win-arm64
```

- `RuntimeIdentifiers` are `win-x64;win-arm64`; every bundled tool ships both variants (`X.exe` + `X_arm64.exe`) and is resolved per architecture at runtime (see [11 — Bundled Tools](11-bundled-tools.md)).
- Release zip naming for updates: `release_{version}_{rid}.zip` (the unified WPF + Avalonia payload) + `updater_{rid}.zip` (the single `Updater.exe` shared by both apps) (see [16 — Updater](16-updater.md)).
- A `publish-check\` folder with per-RID outputs is used locally to validate published payloads.

### Release packaging script

`scripts/package-release.ps1` reproduces the unified release artifacts (also used by CI):

```powershell
pwsh scripts/package-release.ps1 -Version 5.8.0
# -> artifacts\release\release_5.8.0_win-x64.zip, release_5.8.0_win-arm64.zip
# -> artifacts\release\updater_win-x64.zip, updater_win-arm64.zip
```

- The bundle places `SimpleLauncher.exe` (WPF) and `SimpleLauncher.Avalonia.exe` (Avalonia) next to each other and ships one shared set of content files (`images/`, `tools/`, `samples/`, `appsettings.json`, …). Users choose which app to run.
- Both apps are published **framework-dependent** (the **.NET 10 Desktop Runtime** is required on the target machine), so the payload carries no runtime: each is a single executable with its managed assemblies bundled (`PublishSingleFile=true`, `SelfContained=false`), and the small set of native libraries it needs (SQLite for WPF; Skia/HarfBuzz/GLES/SQLite for Avalonia) stays next to the exe. The updater is published the same way but additionally bundles its Avalonia natives for self-extraction, so `updater_{rid}.zip` is one self-sufficient `Updater.exe` that also works on legacy WPF-only installs.
- Validates the version against `SimpleLauncher.csproj` (canonical), `SimpleLauncher.Core.csproj`, both app csproj files/manifests and `SimpleLauncher.Avalonia.Updater.csproj` before publishing.
- Merges the two publish outputs and fails loudly when a shared file has different content (for example `appsettings.json` drift); `AppSettingsFilesAreIdentical` guards the two appsettings sources in the test suite.
- Prunes the other architecture's bundled tools from the payload (plus the Linux-only extension-less `RetroAchievementsSharp` and `7zz` binaries) and packages `Updater.exe` alone into `updater_{rid}.zip` (the updater's staging tree is cleaned before each publish so stale sidecars cannot accumulate).
- Prunes debug symbol files (`*.pdb`) — never needed at runtime; the native SkiaSharp/HarfBuzzSharp symbols alone account for roughly 105 MB.

### Linux release packaging script

`scripts/package-release-linux.ps1` produces the Linux counterparts (also used by CI; same
asset names the in-app updater looks up in the GitHub release):

```powershell
pwsh scripts/package-release-linux.ps1 -Version 5.8.0
# -> artifacts\release\release_5.8.0_linux-x64.zip, release_5.8.0_linux-arm64.zip
# -> artifacts\release\updater_linux-x64.zip, updater_linux-arm64.zip
```

- The Avalonia app is published **self-contained single-file** for `net10.0` (Linux users are not
  expected to install the .NET runtime; the managed assemblies are bundled into the
  `SimpleLauncher.Avalonia` binary with only the native libraries beside it, mirroring the
  Windows exes); the standalone `Updater` is a self-contained
  single file so it can run without a runtime too.
- Windows-only bundled tools are pruned (`tools/**/*.exe`, `tools/**/*.dll`,
  `tools/FindRomCover/**`, the other architecture's `RetroAchievementsSharp` and `7zz`);
  the matching `7zz` stays as the 7-Zip extraction fallback.
- ZIP files written on Windows cannot carry Unix permission bits through `unzip`, so the
  script stamps the entries' external attributes (0755 for `SimpleLauncher.Avalonia`,
  `Updater`, `RetroAchievementsSharp` and `7zz`, 0644 otherwise) and patches the entries'
  "version made by" host system to Unix. Extracting with `unzip` restores the executable
  bits; the in-app updater preserves installed modes during a swap, applies the archive's
  mode to files an update adds for the first time, and re-applies the executable bit to a
  freshly downloaded updater.
- Verified on Ubuntu 24.04 GNOME Wayland (VMware VM): packaged payload extracted with
  `unzip`, launched and rendered correctly; the `linux-arm64` artifacts were structurally
  verified (aarch64 ELF app/updater) but not run (no ARM64 hardware).
- Also verified on Fedora 44 KDE Wayland (VMware VM, distro .NET 10 SDK): all 640 Avalonia
  tests pass with `dotnet test -c Debug -f net10.0 -r linux-x64` (Fedora's SDK defaults to the
  `fedora.44-x64` RID, whose apphost pack is not on nuget.org, so the `-r linux-x64` override is
  required to restore/build), and the published app launches and renders through XWayland
  (`DISPLAY=:0` plus the login session's `XAUTHORITY`, e.g. from `systemctl --user show-environment`).

### Publish the Avalonia app (multi-targeted)

The Avalonia app targets both `net10.0` (Linux) and `net10.0-windows` (Windows), so the
target framework **must** be specified when publishing:

```bash
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0-windows -r win-x64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0-windows -r win-arm64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0 -r linux-x64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0 -r linux-arm64

# Verify the output includes the bundled tools + updater
ls SimpleLauncher.Avalonia/bin/Release/net10.0/linux-x64/publish/ | head -20
ls SimpleLauncher.Avalonia/bin/Release/net10.0-windows/win-x64/publish/Updater* 2>/dev/null | head
```

- The `net10.0` TFM is **Linux/macOS-only** (audio output uses ALSA + managed decoders). Publishing it
  with a Windows RID (`-f net10.0 -r win-x64`) is rejected by a build guard: the `WINDOWS`
  symbol would not be defined, so `PlaySoundEffects` would take the ALSA path and fail on
  Windows (`DllNotFoundException: libasound`, no Windows native binary is shipped).
- MP3 and WAV sound effects need **no native libraries** on Linux/macOS (NLayer +
  `WaveFileReader`); FLAC/Ogg/Opus use the system `libsndfile` (`sudo apt install libsndfile1`
  on Debian/Ubuntu). A missing `libsndfile`/`libasound`, a missing audio device or a corrupt
  sound file is logged at **Information** level (never reported as a bug) and playback is skipped.
- The Windows publish uses Media Foundation + WaveOut, so neither `libsndfile` nor ALSA is needed there.
- Windows-only features (F8 global hotkey, active-window screenshot) are compiled with
  `#if WINDOWS` (defined only on the `net10.0-windows` TFM) and pull `System.Drawing.Common`
  as a package reference conditional on that TFM; the tray icon is cross-platform.
- **WSL2 smoke test (Linux):** after `publish -f net10.0 -r linux-x64`, run the binary under WSLg: `wsl ./SimpleLauncher.Avalonia/bin/Release/net10.0/linux-x64/publish/SimpleLauncher.Avalonia` —   window 1280×800 should map, single-instance mutex enforces one instance, tray icon is NoOp on WSL2. The full headless test suite also runs on WSL2 without a display: `wsl dotnet test SimpleLauncher.Avalonia.Tests/... -c Debug` (640 tests via `Avalonia.Headless`).

## Versioning

Version `5.8.0` must stay in sync across:

- `SimpleLauncher\SimpleLauncher.csproj` (`AssemblyVersion`, `FileVersion`, `Version`)
- `SimpleLauncher.Core\SimpleLauncher.Core.csproj` (same three)
- `SimpleLauncher.Tests\SimpleLauncher.Tests.csproj`
- `SimpleLauncher\app.manifest` (`assemblyIdentity version`)
- `SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj` (same three, matching the WPF app)
- `SimpleLauncher.Avalonia\app.manifest` (`assemblyIdentity version`)
- `SimpleLauncher.Avalonia.Updater\SimpleLauncher.Avalonia.Updater.csproj` (`Version`, matching the WPF app)

`VersionConsistencyTests` enforces the manifests, the Avalonia csproj/manifest and the shared
`appsettings.json` in local runs; `scripts/package-release.ps1` validates all of the above when
packaging. Bump all of them together.

## Localization

- **One shared pack set for both apps**: `SimpleLauncher.Core\Localization\strings.{code}.json` (ar, bn, de, en, es, fr, hi, id, it, ja, ko, nl, pt-BR, ru, tr, ur, vi, zh-Hans) — UTF-8 without BOM, 2-space indent, `StringComparer.OrdinalIgnoreCase` key order. `strings.en.json` is the canonical key set (2671 keys, all files in full key parity).
- Both apps **embed** the packs in their assemblies: Avalonia as manifest resources (`SimpleLauncher.Avalonia.Resources.strings.{code}.json`, loaded by `LocalizationService` via `Assembly.GetManifestResourceStream`; no loose `Resources` folder ships); the WPF app as pack resources (`resources/strings.{code}.json` in `SimpleLauncher.g.resources`, via `<Resource Include="..\SimpleLauncher.Core\Localization\strings.*.json" />` in `SimpleLauncher.csproj`) with `App.ApplyLanguage` building the WPF language `ResourceDictionary` from the JSON, resolving codes like `pt-br`/`zh-hans` case-insensitively.
- `SimpleLauncher.ResourceTranslator` (OpenRouter API, default `z-ai/glm-5.3-flash`) translates missing keys into the shared packs; see its [README](../SimpleLauncher.ResourceTranslator/README.md).
- Unit tests guard against common translation issues: keys used in source but missing from English (auto-added with fallbacks), duplicate keys, mismatched fallbacks, empty values, key parity and key counts.
  - WPF: `DetectMissingResourceProviderKeysTests` and `DetectMissingResourceStringsTests` scan the WPF sources (`_resourceProvider.GetString(...)`, `TryFindResource(...)`) and auto-add missing keys to `strings.en.json`; `DetectDuplicateResourceKeysTests`, `DetectAlphabeticalOrderingTests`, `ResourceFileLoadingTests` and `LocalizationResourcePackagingTests` cover the packs and the embedded payload.
  - Avalonia: `DetectMissingResourceStringsTests` scans the Avalonia source (`.cs` `GetString(...)` calls and `.axaml` `{ext:Translate Key}` usages) and auto-adds missing keys; `LocalizationTests.EveryLanguageFileSharesTheEnglishKeySet` fails with a per-language missing-key list when files are out of sync.
  - The WPF and Avalonia auto-add tests mutate the same `strings.en.json`; they serialize writes with a cross-process named mutex, so the suites are safe to run in parallel.
- Add a new language: create `strings.{code}.json` (UTF-8 without BOM + sorted), add the code to `LanguageMenuService` (WPF) and `LocalizationService.AvailableLanguages`/`AvaloniaLanguageMenuService` (Avalonia), run the translator, and update the resource-key tests if needed.

## Static analysis & code style

- **Meziantou.Analyzer 3.0.259**, **Microsoft.CodeAnalysis.NetAnalyzers 10.0.401** and **Roslynator.Analyzers 5.0.0** (all `PrivateAssets`).
- `Nullable` enabled everywhere; `LangVersion 14`; implicit usings + global `using System.IO; using System.Net.Http; using Serilog;`.
- `NoWarn` in app: `NU1903;CS0436`.
- Tests must satisfy the analyzers (e.g. `StringComparison` overloads on string assertions).
- Conventions observed in the codebase: services take Serilog `ILogger`; UI services use the host-interface pattern (`Initialize(host)`) instead of receiving windows; ViewModels use CommunityToolkit.Mvvm.

## Continuous integration

GitHub Actions only builds release packages and deploys documentation — it never runs the test
suites (they include live endpoints and real app launches; run them locally).

| Workflow | Trigger | What it does |
|---|---|---|
| `.github/workflows/release.yml` | manual (`workflow_dispatch`) with a version | Packages the unified WPF + Avalonia bundle for `win-x64` + `win-arm64` via `scripts/package-release.ps1` and the self-contained single-file Linux bundle for `linux-x64` + `linux-arm64` via `scripts/package-release-linux.ps1`, uploads the zips as workflow artifacts, and creates/updates the `release{version}` GitHub release with `release_{version}_{rid}.zip` + `updater_{rid}.zip` for all four RIDs. Options: skip release creation, provide custom release notes. |
| `.github/workflows/docs.yml` | push to `master` touching `docs/**` (or manual) | Deploys GitHub Pages from `docs/` and syncs the wiki via `scripts/sync-wiki.py`. |

To publish a release: bump the version everywhere (see [Versioning](#versioning)), commit and
push, then run **Actions → Publish release → Run workflow** with the new version.

## Publishing the docs (Pages + wiki)

The `docs\` folder is published in two places:

### GitHub Pages (via Actions)

The site is served from `docs/` via **docsify** (client-side rendering, no build step):

1. Enable once in repo settings: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
2. The `docs.yml` workflow deploys `docs/` on every push to `master` that touches docs (or manually).
3. URL: `https://purelogiccode.github.io/SimpleLauncher/`.

`docs/index.html` (docsify loader), `docs/_sidebar.md` (TOC) and `docs/.nojekyll` are the site assets. `docs/parameters.md` and `docs/manual-tests.md` are **copies** kept for the site and wiki:

- `docs/parameters.md` ← `SimpleLauncher/parameters.md` — refresh it whenever the canonical file changes.
- `docs/manual-tests.md` ← `ManualTests.md` (repo root) — refresh likewise.

### GitHub Wiki (CI + local script)

The wiki is a separate git repo (`SimpleLauncher.wiki.git`); the default `GITHUB_TOKEN` cannot push to it, so the `docs.yml` workflow syncs it with a `WIKI_PAT` secret (classic PAT, `repo` scope). Without that secret the wiki job warns and skips.

```bash
python scripts/sync-wiki.py --dry-run   # preview
python scripts/sync-wiki.py             # clone/pull, rewrite, commit, push
```

The script maps `docs/README.md` → `Home`, copies all `docs/NN-*.md` as pages, and **protects the `parameters` page** (`https://github.com/purelogiccode/SimpleLauncher/wiki/parameters`) — the app opens this URL (`EditSystemWindow.xaml.cs`, config key `WikiParametersUrl`), so it is never deleted and is refreshed from `docs/parameters.md`. Stale pages are deleted, `_Sidebar.md` is regenerated, and markdown links are rewritten to the flat wiki namespace.

## Release workflow (from git history & What's New)

1. Implement features/fixes; keep `WhatsNew.md` updated with a release section.
2. Bump version in the places listed under [Versioning](#versioning).
3. Run the full test suite (minus the slow URL test).
4. Run **Actions → Publish release** with the new version — it packages the Windows and Linux RIDs (`release_{version}_{rid}.zip` + `updater_{rid}.zip` for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`) and creates the GitHub release. Locally, `pwsh scripts/package-release.ps1 -Version <version>` and `pwsh scripts/package-release-linux.ps1 -Version <version>` produce the same packages. The workflow can also be run with **Create or update the GitHub release** unchecked to only build the zips as workflow artifacts.
5. The in-app updater and silent update check use the GitHub `releases/latest` API.

## Related docs

- [02 — Projects & Solution](02-projects-and-solution.md)
- [14 — Testing](14-testing.md)
- [16 — Updater](16-updater.md)
- [17 — Release Notes](17-release-notes.md)
