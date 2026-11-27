# 15 — Development

> Build, test, publish, versioning, localization, code style.
> Related: [02 — Projects & Solution](02-projects-and-solution.md) · [14 — Testing](14-testing.md)

## Prerequisites

- .NET SDK **11.0.x** (`global.json` pins `11.0.100`, roll-forward latestMajor, no prereleases).
- Windows (the projects target `net11.0-windows` and use WPF/DPAPI).

## Build & test

```bash
# build the Avalonia-first main solution (Windows, both TFMs)
dotnet build SimpleLauncher.sln -c Debug
dotnet build SimpleLauncher.sln -c Release

# (WPF reference solution is frozen: not in the solution, CI or routine verification)

# build one project
dotnet build SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Debug

# main fast suite - Avalonia (net11.0, headless)
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj -c Debug

# optional frozen WPF reference fast suite
dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj -c Debug

# WSL2 / Linux - Avalonia only (net11.0 TFM, no Windows desktop pack)
dotnet build SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Debug -f net11.0
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj -c Debug
wsl dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj -c Debug
```

See [14 — Testing](14-testing.md) for the fast/slow suite split. Tests are
**not** run by CI (the suites include live endpoints and real app launches); verification stays
local + WSL2. The GitHub Actions workflows only package releases and deploy the docs — see
[Continuous integration](#continuous-integration).

## Publish (win-x64 / win-arm64)

```bash
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net11.0-windows -r win-x64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net11.0-windows -r win-arm64
```

The Windows release publishes only the Avalonia app and renames the output to `SimpleLauncher.exe`
(`scripts/package-release.ps1` does this automatically). The WPF project remains buildable through
`SimpleLauncher.Wpf.Reference.sln`; it is not built by CI, published or bundled.

- `RuntimeIdentifiers` are `win-x64;win-arm64`; the runtime helpers ship one payload per OS/architecture (`tools\<tool>\<rid>\`) and are resolved at runtime (see [11 - Bundled Tools](11-bundled-tools.md)).
- Release zip naming for updates: `release_{version}_{rid}.zip` (the Avalonia payload as `SimpleLauncher.exe`) + `updater_{rid}.zip` (the single `Updater.exe`, also compatible with legacy WPF installs) (see [16 - Updater](16-updater.md)).
- A `publish-check\` folder with per-RID outputs is used locally to validate published payloads.

### Release packaging script

`scripts/package-release.ps1` reproduces the Windows release artifacts (also used by CI):

```powershell
pwsh scripts/package-release.ps1 -Version 5.9.1
# -> artifacts\release\release_5.9.1_win-x64.zip, release_5.9.1_win-arm64.zip
# -> artifacts\release\updater_win-x64.zip, updater_win-arm64.zip

pwsh scripts/package-release.ps1 -Version 5.9.1 -BuildInstaller
# -> the same files plus
#    artifacts\release\SimpleLauncher-5.9.1-win-x64-setup.exe
#    artifacts\release\SimpleLauncher-5.9.1-win-arm64-setup.exe
```

- Publishes only `SimpleLauncher.Avalonia` for Windows and renames the published executable to `SimpleLauncher.exe`; the WPF project is **not** published or bundled and remains frozen reference code in `SimpleLauncher.Wpf.Reference.sln`.
- Copies `LICENSE.txt`, `README.md` and `WhatsNew.md` to the payload root (`Copy-BundleDocuments`).
- The app is published **framework-dependent** (the **.NET 11 runtime** is required on the target machine), so the payload carries no runtime: it is a single executable with its managed assemblies bundled (`PublishSingleFile=true`, `SelfContained=false`), and the small set of native libraries it needs (Skia/HarfBuzz/GLES/SQLite) stays next to the exe. The updater is published the same way but additionally bundles its Avalonia natives for self-extraction, so `updater_{rid}.zip` is one self-sufficient `Updater.exe` that also works on legacy WPF-only installs.
- Validates the version against `SimpleLauncher.Avalonia.csproj` (canonical), `SimpleLauncher.Core.csproj`, the Avalonia `app.manifest` and `SimpleLauncher.Avalonia.Updater.csproj` before publishing. The WPF reference is frozen and excluded from validation.
- The Avalonia appsettings is the only shipped copy; the WPF reference copy is frozen and no longer compared.
- Prunes the other architecture's helper payloads (plus the Linux-only extension-less `7zz` binary) and packages `Updater.exe` with `LICENSE.txt`, `README.md` and `WhatsNew.md` into `updater_{rid}.zip` (the updater's staging tree is cleaned before each publish so stale sidecars cannot accumulate). The per-RID helpers (`CHDMounter`, `RetroAchievementsSharp`, `SimpleXisoDrive` and `SimpleZipDrive`) keep only the target `win-{x64,arm64}` subfolder.
- Prunes debug symbol files (`*.pdb`) — never needed at runtime; the native SkiaSharp/HarfBuzzSharp symbols alone account for roughly 105 MB.

### Windows installer (`installer/SimpleLauncher.iss`)

`-BuildInstaller` compiles a per-user **Inno Setup** installer from the same payload. It
requires Inno Setup 6.5+ (`ISCC.exe` on `PATH`, in the default install folder, or passed with
`-IsccPath`); CI installs it with Chocolatey before packaging.

- **Per-user install**, default `%LocalAppData%\SimpleLauncher`, no elevation for the app files.
  The user can pick another writable folder on the Select Destination page.
- **.NET 11 bootstrap**: Setup looks for a `Microsoft.NETCore.App\11.*` folder under
  `DOTNET_ROOT`, `%ProgramFiles%\dotnet` (native and the matching `x64`/`arm64` subfolder) or
  `%LocalAppData%\Microsoft\dotnet`. If it is missing, it offers to download
  `dotnet-runtime-11.0-win-{x64,arm64}.exe` from Microsoft (or use an offline copy named
  `dotnet-runtime-11.0*-win-{x64,arm64}.exe` placed next to Setup), then elevates only
  that runtime installer. `No` opens the Microsoft download
  page and keeps Setup on the Ready page so the user can retry.
- **Uninstall keeps user data**: Inno removes only the files it installed. `settings.dat`,
  `system.xml`, logs and game media remain in the install folder; the quickstart/FAQ document how
  to delete them.
- Setup and uninstall share the app's single-instance mutex, so a running app is closed before
  files are replaced (the built-in updater still handles normal in-app updates).
- The installer is unsigned like the rest of the release; `SHA256SUMS.txt` is attached to the
  GitHub release and `docs/faq.md` explains the SmartScreen prompt.

Local smoke test (installs to a scratch folder, keeps a fake data file, then uninstalls):

```powershell
$setup = 'artifacts\release\SimpleLauncher-5.9.1-win-x64-setup.exe'
$target = 'D:\Temp\sl-install-test'
Start-Process $setup -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',"/DIR=$target" -Wait
New-Item -ItemType File -Path (Join-Path $target 'settings.dat') -Force | Out-Null
Start-Process (Join-Path $target 'unins000.exe') -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait
Test-Path (Join-Path $target 'settings.dat')   # True: user data kept
```

### Linux release packaging script

`scripts/package-release-linux.ps1` produces the portable Linux pairs (the asset names the
in-app updater looks up in the GitHub release):

```powershell
pwsh scripts/package-release-linux.ps1 -Version 5.9.1 -BuildSelfContainedPayload
# -> artifacts\release\release_5.9.1_linux-x64.zip, release_5.9.1_linux-arm64.zip
# -> artifacts\release\release_5.9.1_linux-x64.tar.gz, release_5.9.1_linux-arm64.tar.gz
# -> artifacts\release\updater_linux-x64.zip, updater_linux-arm64.zip
# -> <WorkDir>\linux-x64\selfcontained, <WorkDir>\linux-arm64\selfcontained
```

> **Planned for the next release cycle:** native `.deb` and `.rpm` packages are not part of
> this script or the release workflow yet. The target matrix, runtime/install-layout decisions
> and acceptance checklist are in
> [21 - Release Artifacts](21-release-artifacts.md). Do not add them to `release.yml`,
> `package-dry-run.yml` or `publish-fallback-r2.ps1` before that checklist is complete.

- The portable payload is published **framework-dependent single-file** for `net11.0` (the
  **.NET 11 runtime** is required; managed assemblies are bundled into the
  `SimpleLauncher.Avalonia` binary with only the native libraries beside it, mirroring the
  Windows exes); the standalone `Updater` is framework-dependent single-file as well.
- `release_{version}_{rid}.tar.gz` is the same payload in a tar archive whose entry modes are
  written explicitly (0755 for the executables, 0644 otherwise), so `tar -xzf` restores the
  executable bits even when the archive was built on Windows.
- `-BuildSelfContainedPayload` also publishes a **self-contained** payload (the .NET 11
  runtime travels with the app) for the AppImage/Flatpak packager, removes the
  framework-dependent `Updater` from it, prunes Windows-only files and applies Unix modes when
  the script runs on Linux.
- Copies `LICENSE.txt`, `README.md` and `WhatsNew.md` to every payload root
  (`Copy-BundleDocuments`, the same helper the Windows script uses).
- Windows-only helper files are pruned (`tools/**/*.exe`, `tools/**/*.dll`, the other
  architecture's `7zz`); the per-RID helpers (`CHDMounter`, `RetroAchievementsSharp`,
  `SimpleXisoDrive`, `SimpleZipDrive`) keep only the target `linux-{x64,arm64}` subfolder; the
  matching `7zz` stays as the 7-Zip extraction fallback.
- ZIP files written on Windows cannot carry Unix permission bits through `unzip`, so the
  script stamps the entries' external attributes (0755 for `SimpleLauncher.Avalonia`,
  `Updater`, `RetroAchievementsSharp` and `7zz`, 0644 otherwise) and patches the entries'
  "version made by" host system to Unix. Extracting with `unzip` restores the executable
  bits; the in-app updater preserves installed modes during a swap, applies the archive's
  mode to files an update adds for the first time, and re-applies the executable bit to a
  freshly downloaded updater.

### Native Linux bundles (AppImage + Flatpak)

`scripts/package-linux-native.sh` runs on a Linux build host (CI uses `ubuntu-latest`) and
turns the self-contained payload into the two store-style bundles:

```bash
./scripts/package-linux-native.sh \
  --version 5.9.1 --rid linux-x64 \
  --payload artifacts/publish-linux/linux-x64/selfcontained \
  --output artifacts/release
# -> SimpleLauncher-5.9.1-linux-x64.AppImage
# -> SimpleLauncher-5.9.1-linux-x64.flatpak
```

- **AppImage** — builds an AppDir (`AppRun`, `.desktop`, icon, payload under `usr/bin`), then
  runs `appimagetool`. The tool is downloaded on first use. For `linux-arm64` on an x86_64
  host it uses `ARCH=aarch64` plus the aarch64 type-2 runtime (`--runtime-file`), so the
  precompiled ARM64 payload can be packaged without ARM64 hardware.
- **Flatpak** — `flatpak build-init` / `build-finish` / `build-export` / `build-bundle` on
  `org.freedesktop.Platform//25.08`. The app runs the emulators on the host via
  `flatpak-spawn --host`, has host filesystem access for ROMs/emulators/BIOS/config files,
  and exposes `org.a11y.Bus` for screen readers. FUSE mounting is disabled inside the sandbox
  (the app uses its extraction/conversion fallbacks) because a sandbox mount is not visible to
  host emulators.
- **Cross-architecture**: both arm64 artifacts are exported from the x86_64 host because the
  payload is already compiled and no target code is executed; CI installs the x86_64 and
  aarch64 Flatpak runtimes.
- Verified on Linux Mint 22.3 x64 (X11): AppImage launched and rendered, Flatpak installed and
  launched, the Flatpak launched the host dummy emulator through `flatpak-spawn`, and the
  portable tarball extracted with correct executable bits and ran with the .NET 11 runtime.
  The arm64 AppImage and Flatpak were built and structurally verified (aarch64 ELF runtime and
  payload, aarch64 OSTree commit). The AppImage was also built and launched natively on the
  Azure `linuxarm-test` ARM64 host (2026-11-19); the Flatpak was not run there because the
  guest lacks `flatpak-builder` and the 25.08 SDK.

### macOS release packaging script

`scripts/package-release-macos.ps1` runs on macOS (CI uses the arm64 `macos-15` runner) and
produces the Apple Silicon DMG:

```powershell
pwsh scripts/package-release-macos.ps1 -Version 5.9.1
# -> artifacts/release/SimpleLauncher-5.9.1-osx-arm64.dmg
```

- Publishes `net11.0 -r osx-arm64 --self-contained true` (the .NET 11 runtime travels inside
  the `.app`) and lays it out as `Simple Launcher.app/Contents/MacOS`.
- Prunes the bundled `tools/` tree and the `Updater`: there are no macOS payloads for the
  runtime helpers, and macOS updates install the next DMG instead of using the in-app updater.
- Generates `Info.plist` (`LSMinimumSystemVersion` 15.0, bundle id
  `com.purelogiccode.simplelauncher`) and a `.icns` from `SimpleLauncher.Avalonia/icon/icon.png`.
- Ad-hoc signs the bundle with `codesign -s -` (skip with `-NoAdHocSign`); **it is not
  Developer ID signed and not notarized**, so no Apple Developer certificate is involved.
- Creates a compressed `UDZO` DMG with an Applications shortcut and verifies its contents
  with `hdiutil`.

### Cloudflare R2 fallback publishing

The secondary download fallback
(`https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/`, read by the updater when
GitHub is unreachable or rate-limited) is backed by the Cloudflare R2 bucket
`simplelauncher-thumbnails` under the prefix `Simple Launcher/Simple Launcher/`.

After the packaging scripts have produced every asset (four Windows/Linux RIDs plus the
Apple Silicon DMG), publish the fallback copy:

```powershell
pwsh scripts/publish-fallback-r2.ps1 -Version 5.9.1
```

- Reads `R2_*` credentials from the environment or the gitignored `AGENTS.local.md` block
  (see the local-only notes); never commit them.
- Uploads the four `release_{version}_{rid}.zip`, the two portable Linux tar.gz files, the four
  `updater_{rid}.zip` assets, the two AppImages, the two Flatpak bundles and the macOS
  `SimpleLauncher-{version}-osx-arm64.dmg`, verifies their public URLs, then updates
  `version.txt` **last**, so clients never see a version before its assets exist. AppImages
  self-update from this copy, so they must be published with the rest. The publisher also
  refreshes `easymode_macos_arm64.xml` from the source catalog.
- `-DryRun` prints the plan; `-ReleaseDir` selects a non-default package folder.
- The manual `release.yml` workflow runs this publisher automatically after creating/updating the
  GitHub release, using the `R2_*` repository secrets. Local/manual releases still run the script
  explicitly.

### Publish the Avalonia app (multi-targeted)

The Avalonia app targets both `net11.0` (Linux) and `net11.0-windows` (Windows), so the
target framework **must** be specified when publishing:

```bash
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net11.0-windows -r win-x64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net11.0-windows -r win-arm64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net11.0 -r linux-x64
dotnet publish SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj -c Release -f net11.0 -r linux-arm64

# Verify the output includes the runtime helpers + updater
ls SimpleLauncher.Avalonia/bin/Release/net11.0/linux-x64/publish/ | head -20
ls SimpleLauncher.Avalonia/bin/Release/net11.0-windows/win-x64/publish/Updater* 2>/dev/null | head
```

The Windows publish output names the app `SimpleLauncher.Avalonia.exe`; the release packaging
script renames it to `SimpleLauncher.exe`.

- The `net11.0` TFM is **Linux/macOS-only** (audio output uses ALSA + managed decoders). Publishing it
  with a Windows RID (`-f net11.0 -r win-x64`) is rejected by a build guard: the `WINDOWS`
  symbol would not be defined, so `PlaySoundEffects` would take the ALSA path and fail on
  Windows (`DllNotFoundException: libasound`, no Windows native binary is shipped).
- MP3 and WAV sound effects need **no native libraries** on Linux/macOS (NLayer +
  `WaveFileReader`); FLAC/Ogg/Opus use the system `libsndfile` (`sudo apt install libsndfile1`
  on Debian/Ubuntu). A missing `libsndfile`/`libasound`, a missing audio device or a corrupt
  sound file is logged at **Information** level (never reported as a bug) and playback is skipped.
- The Windows publish uses Media Foundation + WaveOut, so neither `libsndfile` nor ALSA is needed there.
- Windows-only features (F8 global hotkey, active-window screenshot) are compiled with
  `#if WINDOWS` (defined only on the `net11.0-windows` TFM) and pull `System.Drawing.Common`
  as a package reference conditional on that TFM; the tray icon is cross-platform.
- **WSL2 smoke test (Linux):** after `publish -f net11.0 -r linux-x64`, run the binary under WSLg: `wsl ./SimpleLauncher.Avalonia/bin/Release/net11.0/linux-x64/publish/SimpleLauncher.Avalonia` —   window 1280×800 should map, single-instance mutex enforces one instance, tray icon is NoOp on WSL2. The full headless fast suite also runs on WSL2 without a display: `wsl dotnet test SimpleLauncher.Avalonia.Tests/... -c Debug` (834 tests via `Avalonia.Headless`; the same suite also runs natively on macOS).

## Versioning

Version `5.9.2` must stay in sync across the shipped app and its release components:

- `SimpleLauncher.Avalonia\SimpleLauncher.Avalonia.csproj` (`AssemblyVersion`, `FileVersion`, `Version`) — **canonical**
- `SimpleLauncher.Core\SimpleLauncher.Core.csproj` (same three)
- `SimpleLauncher.Diagnostics\SimpleLauncher.Diagnostics.csproj` (same three)
- `SimpleLauncher.Avalonia\app.manifest` (`assemblyIdentity version`)
- `SimpleLauncher.Avalonia.Updater\SimpleLauncher.Avalonia.Updater.csproj` (same three)

The test projects (`SimpleLauncher.Avalonia.Tests`, `SimpleLauncher.Avalonia.SlowTests`) and
`SimpleLauncher.ResourceTranslator` carry the same version metadata for diagnostics; keep them
aligned when bumping. The WPF reference is frozen and intentionally excluded (its csproj,
manifest and appsettings are not updated or validated).

`SimpleLauncher.Avalonia.Tests\VersionConsistencyTests` enforces the Avalonia manifest, the
shared Core csproj and the updater against the canonical Avalonia version in the main fast
suite. `scripts/package-release.ps1`, the Linux/macOS packaging scripts and the winget
generator read/validate the same set.

## Localization

- **One shared pack set for the shipped app and WPF reference**: `SimpleLauncher.Core\Localization\strings.{code}.json` (ar, bn, de, en, es, fr, hi, id, it, ja, ko, nl, pt-BR, ru, tr, ur, vi, zh-Hans) — UTF-8 without BOM, 2-space indent, `StringComparer.OrdinalIgnoreCase` key order. `strings.en.json` is the canonical key set (2671 keys, all files in full key parity).
- Both **embed** the packs in their assemblies: Avalonia as manifest resources (`SimpleLauncher.Avalonia.Resources.strings.{code}.json`, loaded by `LocalizationService` via `Assembly.GetManifestResourceStream`; no loose `Resources` folder ships); the WPF reference as pack resources (`resources/strings.{code}.json` in `SimpleLauncher.g.resources`, via `<Resource Include="..\SimpleLauncher.Core\Localization\strings.*.json" />` in `SimpleLauncher.csproj`) with `App.ApplyLanguage` building the WPF language `ResourceDictionary` from the JSON, resolving codes like `pt-br`/`zh-hans` case-insensitively.
- `SimpleLauncher.ResourceTranslator` (OpenRouter API, default `z-ai/glm-5.3-flash`) translates missing keys into the shared packs; see its [README](../SimpleLauncher.ResourceTranslator/README.md).
- Unit tests guard against common translation issues: keys used in source but missing from English (auto-added with fallbacks), duplicate keys, mismatched fallbacks, empty values, key parity and key counts.
  - WPF: `DetectMissingResourceProviderKeysTests` and `DetectMissingResourceStringsTests` scan the WPF sources (`_resourceProvider.GetString(...)`, `TryFindResource(...)`) and auto-add missing keys to `strings.en.json`; `DetectDuplicateResourceKeysTests`, `DetectAlphabeticalOrderingTests`, `ResourceFileLoadingTests` and `LocalizationResourcePackagingTests` cover the packs and the embedded payload.
  - Avalonia: `DetectMissingResourceStringsTests` scans the Avalonia source (`.cs` `GetString(...)` calls and `.axaml` `{ext:Translate Key}` usages) and auto-adds missing keys; `LocalizationTests.EveryLanguageFileSharesTheEnglishKeySet` fails with a per-language missing-key list when files are out of sync. The main suite also carries copies of `DetectDuplicateResourceKeysTests`, `DetectAlphabeticalOrderingTests`, `LocalizationTemplateTests` and `VersionConsistencyTests` so those shared guards survive the WPF solution split.
  - The WPF and Avalonia auto-add tests mutate the same `strings.en.json`; they serialize writes with a cross-process named mutex, so the suites are safe to run in parallel.
- Add a new language: create `strings.{code}.json` (UTF-8 without BOM + sorted), add the code to `LanguageMenuService` (WPF) and `LocalizationService.AvailableLanguages`/`AvaloniaLanguageMenuService` (Avalonia), run the translator, and update the resource-key tests if needed.

## Static analysis & code style

- **Meziantou.Analyzer 3.0.342**, **Microsoft.CodeAnalysis.NetAnalyzers 11.0.100** and **Roslynator.Analyzers 5.0.1** (all `PrivateAssets`).
- `Nullable` enabled everywhere; `LangVersion 15`; implicit usings + global `using System.IO; using System.Net.Http; using Serilog;`.
- `NoWarn` in app: `NU1903;CS0436`.
- Tests must satisfy the analyzers (e.g. `StringComparison` overloads on string assertions).
- **Every regex must specify a match timeout** (Meziantou `MA0009`): `[GeneratedRegex(pattern, options, 1000)]` (add a culture before the timeout only when needed: `[GeneratedRegex(pattern, options, 1000, "pt-BR")]`), and pass a `TimeSpan`/milliseconds to `new Regex(...)` and static `Regex.Match/IsMatch/Replace/Split/Matches` calls. `Regex.Escape`/`Unescape` need no timeout.
- Conventions observed in the codebase: services take Serilog `ILogger`; UI services use the host-interface pattern (`Initialize(host)`) instead of receiving windows; ViewModels use CommunityToolkit.Mvvm.

## Continuous integration

GitHub Actions builds the solution, packages releases and deploys documentation — it never runs
the test suites (they include live endpoints and real app launches; run them locally).

| Workflow | Trigger | What it does |
|---|---|---|
| `.github/workflows/ci.yml` | push to `master` (non-docs paths) and pull requests | Build-only: Avalonia-first `SimpleLauncher.sln` Debug **and Release** builds on `windows-latest`, plus the Avalonia `net11.0` Debug/Release and updater `net11.0` builds on `ubuntu-latest`. All builds use `-warnaserror`. The WPF reference solution is not built by CI. Never runs tests. |
| `.github/workflows/package-dry-run.yml` | push/PR touching the app projects or packaging scripts (or manual) | Packages every release asset with the same scripts as the release workflow (Windows, Linux and the macOS DMG on `macos-15`), uploads the archives as workflow artifacts and verifies the Linux payload layout/executable bits and the DMG bundle layout - without creating a GitHub release. The version is read from `SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj`. |
| `.github/workflows/release.yml` | manual (`workflow_dispatch`) with a version | Packages the Avalonia-only Windows bundle (`SimpleLauncher.exe`) for `win-x64` + `win-arm64` via `scripts/package-release.ps1`, the Linux bundles for `linux-x64` + `linux-arm64`, and the Apple Silicon `osx-arm64` DMG via `scripts/package-release-macos.ps1`; uploads the artifacts and creates/updates the `release{version}` GitHub release with every Windows/Linux/macOS asset. Options: skip release creation, provide custom release notes. |
| `.github/workflows/docs.yml` | push to `master` touching `docs/**` (or manual) | Deploys GitHub Pages from `docs/` and syncs the wiki via `scripts/sync-wiki.py`. |

`master` requires the two build checks from `ci.yml` for pull-request merges (direct pushes are
still allowed). The required contexts are the job names **Build solution (Windows)** and
**Build Avalonia net11.0 (Linux)** — keep them stable when editing the workflow.

To publish a release: bump the version everywhere (see [Versioning](#versioning)), commit and
push, then run **Actions → Publish release → Run workflow** with the new version.

## Publishing the docs (Pages + wiki)

The `docs\` folder is published in two places:

### GitHub Pages (via Actions)

The site is served from `docs/` as a **static landing page** plus the **docsify** documentation (client-side rendering, no build step):

1. Enable once in repo settings: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
2. The `docs.yml` workflow deploys `docs/` on every push to `master` that touches docs (or manually).
3. URLs: landing page `https://purelogiccode.github.io/SimpleLauncher/`, docs `https://purelogiccode.github.io/SimpleLauncher/docs.html`.

`docs/index.html` (landing page), `docs/landing.css` / `docs/landing.js` (landing assets), `docs/analytics.js` + `docs/analytics-config.js` (opt-in cookie-free analytics), `docs/media/` (screenshots, 20-second tour, OG image), `docs/docs.html` (docsify loader), `docs/site.css` (the docs dark/light theme), `docs/_sidebar.md` (TOC) and `docs/.nojekyll` are the site assets. The landing page redirects old `#/...` documentation links to `docs.html#/...`. To enable analytics, set the GoatCounter code or Plausible domain in `docs/analytics-config.js` and redeploy; while `provider` is `none`, no analytics requests are made. `docs/_sidebar.md` is the **single source for both side menus**: docsify reads it directly, and the wiki sync preserves its headings, group labels and order when it writes `_Sidebar.md`. `docs/parameters.md` and `docs/manual-tests.md` are **copies** kept for the site and wiki:

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
3. Run the fast suites during iteration; run **both slow suites before deploying** (see [14 — Testing](14-testing.md)).
4. Run **Actions → Publish release** with the new version — it packages the Windows and Linux RIDs (`release_{version}_{rid}.zip` + `updater_{rid}.zip` for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`) and creates the GitHub release. Locally, `pwsh scripts/package-release.ps1 -Version <version>` and `pwsh scripts/package-release-linux.ps1 -Version <version>` produce the same packages. The workflow can also be run with **Create or update the GitHub release** unchecked to only build the zips as workflow artifacts. The optional **release notes** input is written into the release body (for example the current `WhatsNew.md` section).
5. The in-app updater and silent update check use the GitHub `releases/latest` API, with an automatic fallback to the secondary server (`assets.purelogiccode.com/…/version.txt` + the hosted packages) when GitHub is unreachable or rate-limited (HTTP 403/429). Keep `version.txt` and the hosted packages on that server **in sync with each release** — they are maintained outside this repository.

## Related docs

- [02 — Projects & Solution](02-projects-and-solution.md)
- [14 — Testing](14-testing.md)
- [16 — Updater](16-updater.md)
- [17 — Release Notes](17-release-notes.md)
