# Agent Instructions — SimpleLauncher

Standing rules for AI agents (and anyone) working in this repository.

## 🚫 Never delete files inside `bin\Release`

The `bin\Release` output folders (in any project: `SimpleLauncher`, `SimpleLauncher.Core`,
`SimpleLauncher.Avalonia`, `SimpleLauncher.Avalonia.Updater`, `Tools\*`, etc.) contain the
**built release artifacts used for packaging and publishing**. They are intentionally kept on
disk.

**Rules:**

- **NEVER delete, move, rename, or clean files inside any `bin\Release` folder** — not
  individually, not with recursive cleanup, not with `git clean`, `dotnet clean`, `rm -rf`,
  `rd /s`, or any other bulk operation.
- `bin\Release` folders are **never to be touched by cleanup scripts or agents**, even when
  asked to "clean the build output" or "remove build artifacts" — if such a request comes in,
  clarify it first (the requestor almost certainly means `bin\Debug` / `obj`, or nothing at all).
- If a task requires a fresh release build, rebuild **over** the existing `bin\Release` output
  (`dotnet build -c Release` or `dotnet publish`) — do not delete it first.
- Deleting `bin\Debug`, `obj`, or `bin\Release\...` content that the task itself just created
  during the same session is allowed, but only that.
- These folders are gitignored (`[Bb]in/`), so git operations will never touch them; the rule
  protects against direct filesystem operations.

## ✅ Standing policies (do not revert or bypass)

- **CI scope**: GitHub Actions builds, packages releases and deploys docs — it never runs tests.
  - `.github/workflows/ci.yml` - build-only on pushes to `master` (non-docs paths) and PRs: the
    Avalonia-first `SimpleLauncher.sln` Debug **and Release** builds on `windows-latest` + Avalonia
    `net11.0` Debug/Release and the updater `net11.0` build on `ubuntu-latest`. Builds pass
    `-warnaserror`, so the 0-warning policy is enforced. **Never add tests to it.** The WPF
    reference (`SimpleLauncher.Wpf.Reference.sln`) is frozen reference code: it is deliberately
    **not** built by CI and must **not** be updated to mirror Avalonia/Core changes.
  - `.github/workflows/package-dry-run.yml` — packages every release asset exactly like the
    release workflow (version read from `SimpleLauncher.Avalonia.csproj`): Windows bundles and per-user
    Inno Setup installers on `windows-latest`; portable ZIP/tar.gz + AppImage/Flatpak for both
    Linux RIDs on `ubuntu-latest`; the arm64 DMG on `macos-15`; then verifies the archives, the
    installers, executable bits, AppImage architectures, Flatpak commits and the DMG bundle
    layout, but **never creates or updates a GitHub release**. Runs on PRs/pushes touching the app
    projects or packaging scripts, and on manual dispatch.
  - `.github/workflows/release.yml` - manual (`workflow_dispatch`); packages the Avalonia-only
    Windows bundle for `win-x64` / `win-arm64` via `scripts/package-release.ps1
    -BuildInstaller` on `windows-latest` (Chocolatey installs Inno Setup 6.5+), and every Linux
    bundle for `linux-x64` / `linux-arm64` on `ubuntu-latest`:
    portable `release_{version}_{rid}.zip` + `release_{version}_{rid}.tar.gz` and updater ZIPs
    via `scripts/package-release-linux.ps1`, plus self-contained
    `SimpleLauncher-{version}-{rid}.AppImage` and `.flatpak` via
    `scripts/package-linux-native.sh`. On Windows the payload is the Avalonia app renamed to
    `SimpleLauncher.exe` (framework-dependent); the WPF project stays in the source tree as frozen
    reference code (separate `SimpleLauncher.Wpf.Reference.sln`) and is **not** built by
    CI, published or bundled. The workflow also ships
    `SimpleLauncher-{version}-win-{x64,arm64}-setup.exe` with the .NET 11 bootstrap; the portable
    Linux formats require the .NET 11 runtime, while AppImage/Flatpak bundle it. The same
    workflow builds the **macOS Apple Silicon DMG** on `macos-15` (arm64) via
    `scripts/package-release-macos.ps1`. A publish job then creates/updates the GitHub release
    (with `SHA256SUMS.txt`) and mirrors every asset (including the DMG and the macOS Easy Mode
    catalog) to R2.
  - `.github/workflows/docs.yml` — on `docs/**` pushes to `master`; deploys GitHub Pages from
    `docs/` and syncs the wiki via `scripts/sync-wiki.py` (needs the `WIKI_PAT` secret, skipped
    with a warning when absent).
  - Verification stays local: `dotnet build` / `dotnet test` per project on Windows (plus WSL2
    for the Linux paths), because the suites include live endpoints and real app launches.
    macOS binaries are validated on the rented NoMac host (see `AGENTS.local.md`).
- **macOS distribution format**: the macOS release is one **unsigned** Apple Silicon DMG
  (`SimpleLauncher-{version}-osx-arm64.dmg`) built on `macos-15` from
  `SimpleLauncher.Avalonia` self-contained. It is ad-hoc signed only (`codesign -s -`) — never
  add notarization or an Apple Developer certificate without the admin's explicit approval.
  The bundle ships no bundled helper payloads and no `Updater`: macOS updates install the next DMG
  manually, and `PackagingEnvironment.UpdatesManagedExternally` keeps the in-app updater off.
  The DMG and `easymode_macos_arm64.xml` are mirrored to R2 by
  `scripts/publish-fallback-r2.ps1`; never drop the Apple Silicon artifact from the release
  workflow or the publisher.
- **Linux distribution formats**: every Linux release ships four formats for both RIDs —
  portable ZIP, portable tar.gz, AppImage and Flatpak. The AppImage/Flatpak are
  self-contained; the portable formats stay framework-dependent. AppImage self-updates by
  replacing the outer file (`APPIMAGE`), Flatpak defers to the software center for repository
  installs (or a newer `.flatpak` reinstall) and launches host emulators through
  `flatpak-spawn --host`, and portable installs keep the existing Updater. Never drop a
  format or an architecture from the release workflow or the R2 publisher
  (`scripts/publish-fallback-r2.ps1`) without updating both.
- **Fast/slow test split (no filtering)**: the suites are split **by project**, never by
  `[Trait]`/filter. The main solution's fast suite is `SimpleLauncher.Avalonia.Tests`; its slow
  suite is `SimpleLauncher.Avalonia.SlowTests` (cross-platform mount extraction/guards). The WPF
  reference's `SimpleLauncher.Tests` / `SimpleLauncher.SlowTests` are frozen legacy suites: do
  not mirror new Avalonia/Core behavior into them and do not treat them as required verification.
  Every test in a project always runs unfiltered with `dotnet test` - never add a filter,
  quarantine, runtime skip, or exclusion mechanism for flaky tests. Run only the **fast suites**
  during iteration; run the slow suites **only before a deploy or when the admin explicitly asks**
  for them - never as part of routine iteration or verification.
- **Bug reports**: the `BugReportApiSink` sends Warning+ log events to the bug-report API.
  Expected user-error conditions (missing files, rate limits, timeouts, unsupported input)
  must be logged at **Information** level, not Warning/Error, so they are never reported as bugs.
- **Audio never blocks the UI thread**: `PlaySoundEffects` owns a dedicated background thread;
  callers only enqueue a request (newest wins). Never call NAudio `Init`/`Play`/`Stop`/`Dispose`
  on the UI thread — `Stop`/`Dispose` join the playback thread, and one wedged ALSA/PipeWire stop
  froze the whole app (BUG-19). `Dispose` must never join the audio thread. Regression guard:
  `PlaySound_WhenAudioStopBlocks_DoesNotBlockCallerOrDispose`.
- **Accessibility instrumentation**: every interactive control must expose an accessible name so
  screen readers (Narrator/NVDA on Windows, Orca on Linux) and the AT-SPI GUI-test harness can
  identify it. Use `AutomationProperties.Name` (`Avalonia.Automation` in Avalonia, the WPF
  equivalent in WPF); buttons whose `Content` is literal text are exempt because the text becomes
  the name. Inputs, sliders, DataGrids and status/progress elements also get
  `AutomationProperties.AutomationId` (stable PascalCase, mirror `x:Name` when present) and
  `AutomationProperties.LiveSetting="Polite"` when they report changing status. Do not name
  decorative TextBlocks/images. Guardrails: `AvaloniaAccessibilityTests` (Avalonia) and
  `WpfAccessibilityTests` (WPF) fail on missing names — never weaken, filter or skip them.
- **Tooltips reuse accessible names**: the Avalonia theme binds `ToolTip.Tip` to
  `AutomationProperties.Name` for every control, and WPF's `AccessibilityToolTipService` does the
  same on `Loaded` (falling back to help text/literal content). Do not duplicate the text in each
  view; add the accessible name and the tooltip follows.
- **Serilog is the only logging pipeline**: never add a custom file/console logger or a second
  bug-report client. Warning+ events flow through `BugReportApiSink` to the bug-report API, and
  every report must contain the full environment block (`Date`, `Application Name`,
  `Application Version`, `OS Version`, `Architecture`, `Bitness`,
  `Windows, Linux or MacOsX Version`, `Processor Count`, `Base Directory`, `Temp Path`), the error
  message and the full inner-exception chain. Expected user conditions stay at Information.
- **Async naming and docs**: Task/ValueTask-returning methods end with `Async`; async-void event
  handlers keep their framework names but must catch and log. Document every public type/member —
  `GenerateDocumentationFile` is enabled in Core, WPF, Avalonia and the updater, so a missing
  XML comment fails the warning-free build.

## 🔧 Build & test

- Windows (per project): `dotnet build <proj>.csproj -c Debug` (or `-c Release`) - expect
  0 warnings / 0 errors.
- Main solution (Avalonia-first): `dotnet build SimpleLauncher.sln -c Debug` (and `-c Release`).
- WPF reference: frozen, not part of the solution or CI. Do not build it as part of routine
  verification and do not mirror Avalonia/Core changes into it.
- Fast suites (run during iteration):
  - `dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj` (~75 s) -
    the main fast suite, including the shared version/localization guards.
  - `dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj` (~70 s) - the frozen WPF
    reference suite; not part of routine Avalonia iteration. Run it only when touching the
    reference itself (optional).
- Slow suites (**do not run during iteration** - only before a deploy or when the admin explicitly
  asks for them):
  - `dotnet test SimpleLauncher.Avalonia.SlowTests/SimpleLauncher.Avalonia.SlowTests.csproj`
    (~1 s) - cross-platform mount extraction fallback and platform guards.
  - `dotnet test SimpleLauncher.SlowTests/SimpleLauncher.SlowTests.csproj` (~3.5 min) - WPF
    reference: launches `SimpleLauncher.exe` for the language checks (no other SimpleLauncher
    instance may run), mounts real ZIP/CHD images (needs Dokan + the bundled mount tools; skips
    cleanly when the images/tools are absent) and hits the live GitHub/PureLogicCode endpoints.

### Test temp path (drive D)

Drive `C:` on the development machine is nearly full, so **all test runs must use drive D for
temporary files**. Set `TEMP` and `TMP` in the same shell session as `dotnet test`, for example:

```powershell
$env:TEMP = 'D:\Temp\opencode\test-temp'
$env:TMP = $env:TEMP
dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj
```

Use separate subdirectories when running suites in parallel and remove them afterwards.

Do **not** redirect `TEMP`/`TMP` for `dotnet build` / `dotnet publish`: WPF's generated
`_wpftmp` XAML project stops resolving its generated members when the temp path is moved.
Builds use the normal temp path; only test execution uses drive D.

- Version metadata is centralized in `SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj`
  (canonical). `SimpleLauncher.Core.csproj`, `SimpleLauncher.Diagnostics.csproj`,
  `SimpleLauncher.Avalonia/app.manifest` and `SimpleLauncher.Avalonia.Updater.csproj` must stay in
  sync (`VersionConsistencyTests` in `SimpleLauncher.Avalonia.Tests` enforces and auto-corrects
  this). Packaging scripts read the canonical Avalonia csproj. The WPF reference csproj/manifest and
  `SimpleLauncher/appsettings.json` are frozen and intentionally excluded from version/appsettings
  validation.

## 🖥️ Test environments

Six environments are available for validating the app. **No access detail (address, user,
password, key, token) is stored in this repository**: the single source is the out-of-repo file
`%USERPROFILE%\.secrets\CONNECTIONS.md` (when present — otherwise ask the admin for access). Read
it before touching any host and never copy values from it into a commit, issue, log or chat. This
machine's resume state stays in `AGENTS.local.md`.

| Environment | OS / arch | What it is for |
|---|---|---|
| This PC (`PETERSONPC`) | Windows 11 x64 | build, Avalonia app, fast suites, packaging, control plane |
| `linuxgpu` | Linux Mint x64 (LAN, bare metal) | real X11 + AT-SPI automation, FUSE mounts, real ROM library |
| `LinuxMint` Hyper-V VM | Linux Mint x64 | full GUI harness suite (`scripts\gui-test-harness\`), fixtures, emulator matrix |
| `linuxarm-test` | Ubuntu 24.04 aarch64 (Azure) | Linux arm64 bundles, AppImage/Flatpak runtime checks |
| `winarm-test` | Windows 11 ARM64 (Azure) | Windows arm64 launch checks |
| NoMac cloud Mac | macOS arm64 (Apple Silicon) | DMG build, `.app` launch, Avalonia tests on macOS |

Rules:

- The Azure hosts are **deallocated by default** and have no public IP. Start/stop them with
  `C:\Sincronizar\ArmHosts\ArmHost.ps1 <linux|windows> start|stop|status`; always `stop` when the
  session ends (compute and public-IP billing stop at deallocation; the OS disk keeps billing).
- The NoMac Mac bills per minute: `npx -y @nomac/cli@0.27.3 stop` when done. Only `~/drive`
  persists between sessions.
- Framework-dependent Linux/macOS publishes need the .NET 11 runtime on the target; the Hyper-V VM
  has no SDK/runtime, so deploy a **self-contained** `linux-x64` publish there.
- SSH to `winarm-test` lands in session 0: launch GUI checks with `schtasks /IT /RU azureuser`
  (autologon keeps the console session alive) and wrap helpers in a hidden `.vbs`.
- Exact deploy commands, publish flags and per-host caveats are in
  `%USERPROFILE%\.secrets\CONNECTIONS.md` §2–§6.

## ☁️ Release download fallback (Cloudflare R2)

The in-app update fallback (`https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/`)
is served from the Cloudflare R2 bucket `simplelauncher-thumbnails` under the prefix
`Simple Launcher/Simple Launcher/`.

- After local packaging, publish the fallback copy with:
  `pwsh scripts/publish-fallback-r2.ps1 -Version <version>`
  Build the Windows installers first (`pwsh scripts/package-release.ps1 -Version <version>
  -BuildInstaller`); the publisher requires them alongside the portable bundles.
- The script uploads the four `release_{version}_{rid}.zip` and four `updater_{rid}.zip`
  assets, the two `SimpleLauncher-{version}-win-{x64,arm64}-setup.exe` installers, the Linux
  tar.gz/AppImage/Flatpak bundles, the Apple Silicon `SimpleLauncher-{version}-osx-arm64.dmg`,
  `easymode_macos_arm64.xml` and (when present) `SHA256SUMS.txt`, verifies their public URLs,
  and updates `version.txt` **last** — never update `version.txt` before all bundles exist.
- R2 credentials live in the `R2_*` **user environment variables** (moved out of
  `AGENTS.local.md` on 2026-11-20; `-CredentialFile` still accepts a gitignored ini-style file).
  Never commit them; the host/key map is in `%USERPROFILE%\.secrets\CONNECTIONS.md` §7.
  `-DryRun` prints the upload plan without touching R2.
- The manual `release.yml` workflow runs the publisher automatically after creating/updating the
  GitHub release, using the `R2_*` repository secrets. Local/manual releases still require the
  script to be run explicitly.

## 🖥️ Linux VM GUI test harness (AT-SPI + vision)

The Avalonia/Linux manual checklist (`docs/manual-tests.md`) is exercised automatically on a
Hyper-V Linux VM by the harness tracked in `scripts\gui-test-harness\`. The portable manual
(prerequisites, configuration, fixture contract, scenario authoring, troubleshooting) is
`docs/gui-test-harness.md`; the live scenario inventory and session state are in `ManualTests.md`.

Machine-specific details (VM IP/credentials, host share paths) and the current resume checklist
are kept **local only** in `AGENTS.local.md`, which is gitignored — when present, read it before
starting a GUI-test session. Host access and the per-host deploy/test workflow are in the
out-of-repo `%USERPROFILE%\.secrets\CONNECTIONS.md` (§3 for the VM).
