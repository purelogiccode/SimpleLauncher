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
- Release zip naming for updates: `release_{version}_{rid}.zip` + `updater_{rid}.zip` (see [16 — Updater](16-updater.md)).
- A `publish-check\` folder with per-RID outputs is used locally to validate published payloads.

### Release packaging script

`scripts/package-release.ps1` reproduces the release artifacts (also used by CI):

```powershell
pwsh scripts/package-release.ps1 -Version 5.7.0
# -> SimpleLauncher\bin\Release\release_5.7.0_win-x64.zip, release_5.7.0_win-arm64.zip
# -> SimpleLauncher\bin\Release\updater_win-x64.zip, updater_win-arm64.zip
```

- Validates the version against `SimpleLauncher.csproj`, `SimpleLauncher.Core.csproj`, `app.manifest` and `SimpleLauncher.Updater\version.txt` before publishing.
- Publishes framework-dependent single-file builds (`--self-contained false -p:PublishSingleFile=true`).
- Prunes the other architecture's bundled tools from each payload (plus the Linux-only extension-less `RetroAchievementsSharp` binaries) and packages `Updater.exe` alone into `updater_{rid}.zip`.

### Publish the Avalonia app (multi-targeted)

The Avalonia app targets both `net10.0` (Linux) and `net10.0-windows` (Windows), so the
target framework **must** be specified when publishing:

```bash
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0-windows -r win-x64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0-windows -r win-arm64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0 -r linux-x64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net10.0 -r linux-arm64

# Verify the output is self-contained and includes the bundled tools + updater
ls SimpleLauncher.Avalonia/bin/Release/net10.0/linux-x64/publish/ | head -20
ls SimpleLauncher.Avalonia/bin/Release/net10.0-windows/win-x64/publish/SimpleLauncher.Avalonia.Updater* 2>/dev/null | head
```

- The `net10.0` TFM is **Linux-only** (audio uses libsndfile/`SoundFileReader`). Publishing it
  with a Windows RID (`-f net10.0 -r win-x64`) is rejected by a build guard: the `WINDOWS`
  symbol would not be defined, so `PlaySoundEffects` would take the Linux path and crash on
  Windows (`DllNotFoundException: libsndfile`, no Windows native binary is shipped).
- The Windows publish uses Media Foundation + WaveOut, so `libsndfile` is not needed there.
- Windows-only features (F8 global hotkey, active-window screenshot) are compiled with
  `#if WINDOWS` (defined only on the `net10.0-windows` TFM) and pull `System.Drawing.Common`
  as a package reference conditional on that TFM; the tray icon is cross-platform.
- **WSL2 smoke test (Linux):** after `publish -f net10.0 -r linux-x64`, run the binary under WSLg: `wsl ./SimpleLauncher.Avalonia/bin/Release/net10.0/linux-x64/publish/SimpleLauncher.Avalonia` — window 1280×800 should map, single-instance mutex enforces one instance, tray icon is NoOp on WSL2. The full headless test suite also runs on WSL2 without a display: `wsl dotnet test SimpleLauncher.Avalonia.Tests/... -c Debug` (482 tests via `Avalonia.Headless`).

## Versioning

Version `5.6.1` must stay in sync across:

- `SimpleLauncher\SimpleLauncher.csproj` (`AssemblyVersion`, `FileVersion`, `Version`)
- `SimpleLauncher.Core\SimpleLauncher.Core.csproj` (same three)
- `SimpleLauncher.Tests\SimpleLauncher.Tests.csproj`
- `SimpleLauncher\app.manifest` (`assemblyIdentity version`)
- `SimpleLauncher.Updater\version.txt` (`release5.6.1`)
- `SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj` (same three, matching the WPF app)

`VersionConsistencyTests` enforces this in local runs. Bump all of them together.

## Localization

- 18 languages as WPF resource dictionaries: `SimpleLauncher\resources\strings.{code}.xaml` (ar, bn, de, en, es, fr, hi, id, it, ja, ko, nl, pt-br, ru, tr, ur, vi, zh-hans).
- 18 languages as Avalonia JSON resources: `SimpleLauncher.Avalonia\Resources\strings.{code}.json` — UTF-8 with BOM, 2-space indent, `StringComparer.OrdinalIgnoreCase` key order. `strings.en.json` is the canonical Avalonia key set (2661 keys).
- `SimpleLauncher.ResourceTranslator` (OpenRouter API, default `z-ai/glm-5.3-flash`) translates missing keys for both projects; see its [README](../SimpleLauncher.ResourceTranslator/README.md).
- Unit tests guard against common translation issues: missing keys in other languages, duplicate/mismatched resource keys, empty values, key-count mismatches (`DetectMissingResourceStringsTests` family — WPF XAML and Avalonia JSON/AXAML source scan).
- `DetectMissingResourceStringsTests` scans the Avalonia source (`.cs` `GetString(...)` calls and `.axaml` `{ext:Translate Key}` usages) and auto-adds missing keys with fallback values to `strings.en.json`; `LocalizationTests.EveryLanguageFileSharesTheEnglishKeySet` fails with a per-language missing-key list when files are out of sync.
- Add a new language: create `strings.{code}.xaml` (WPF) and `strings.{code}.json` (Avalonia, UTF-8 BOM + sorted), register it in `App.ChangeLanguage`/`LanguageMenuService` (WPF) and `AvaloniaLanguageMenuService` (Avalonia), run the translator, and update the resource-key tests if needed.

## Static analysis & code style

- **Meziantou.Analyzer 3.0.139** and **Microsoft.CodeAnalysis.NetAnalyzers 10.0.302** (both `PrivateAssets`).
- `Nullable` enabled everywhere; `LangVersion 14`; implicit usings + global `using System.IO; using System.Net.Http; using Serilog;`.
- `NoWarn` in app: `NU1903;CS0436`.
- Tests must satisfy the analyzers (e.g. `StringComparison` overloads on string assertions).
- Conventions observed in the codebase: services take Serilog `ILogger`; UI services use the host-interface pattern (`Initialize(host)`) instead of receiving windows; ViewModels use CommunityToolkit.Mvvm.

## Continuous integration

GitHub Actions only builds release packages and deploys documentation — it never runs the test
suites (they include live endpoints and real app launches; run them locally). The Avalonia
variants are intentionally not published by CI yet.

| Workflow | Trigger | What it does |
|---|---|---|
| `.github/workflows/release-wpf.yml` | manual (`workflow_dispatch`) with a version | Packages the WPF app for `win-x64` + `win-arm64` via `scripts/package-release.ps1`, uploads the zips as workflow artifacts, and creates/updates the `release{version}` GitHub release (with assets). Options: skip release creation, provide custom release notes. |
| `.github/workflows/docs.yml` | push to `master` touching `docs/**` (or manual) | Deploys GitHub Pages from `docs/` and syncs the wiki via `scripts/sync-wiki.py`. |

To publish a release: bump the version everywhere (see [Versioning](#versioning)), commit and
push, then run **Actions → Publish WPF release → Run workflow** with the new version.

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
2. Bump version in the five places above.
3. Run the full test suite (minus the slow URL test).
4. Run **Actions → Publish WPF release** with the new version — it packages both RIDs (`release_{version}_{rid}.zip` + `updater_{rid}.zip`) and creates the GitHub release. Locally, `pwsh scripts/package-release.ps1 -Version <version>` produces the same packages.
5. The in-app updater and silent update check use the GitHub `releases/latest` API.

## Related docs

- [02 — Projects & Solution](02-projects-and-solution.md)
- [14 — Testing](14-testing.md)
- [16 — Updater](16-updater.md)
- [17 — Release Notes](17-release-notes.md)
