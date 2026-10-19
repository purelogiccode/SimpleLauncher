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
  - `.github/workflows/ci.yml` — build-only on pushes to `master` (non-docs paths) and PRs: full
    `SimpleLauncher.sln` Debug **and Release** builds on `windows-latest` + Avalonia `net10.0`
    Debug/Release and the updater `net10.0` build on `ubuntu-latest`. Builds pass
    `-warnaserror`, so the 0-warning policy is enforced. **Never add tests to it.**
  - `.github/workflows/package-dry-run.yml` — packages all four RIDs exactly like the release
    workflow (version read from `SimpleLauncher.csproj`), uploads the zips and verifies the Linux
    executable bits on `ubuntu-latest`, but **never creates or updates a GitHub release**. Runs on
    PRs/pushes touching the app projects or packaging scripts, and on manual dispatch.
  - `.github/workflows/release.yml` — manual (`workflow_dispatch`); packages the unified WPF +
    Avalonia bundle for `win-x64` / `win-arm64` via `scripts/package-release.ps1` and the
    framework-dependent Linux bundle for `linux-x64` / `linux-arm64` via
    `scripts/package-release-linux.ps1`. On Windows the payload places `SimpleLauncher.exe` and
    `SimpleLauncher.Avalonia.exe` next to each other (both framework-dependent); on Linux it is
    the framework-dependent `SimpleLauncher.Avalonia` (the .NET 10 runtime is required). Both ship
    as `release_{version}_{rid}.zip` plus the standalone `updater_{rid}.zip` (`Updater.exe` /
    `Updater`). Creates/updates the GitHub release.
  - `.github/workflows/docs.yml` — on `docs/**` pushes to `master`; deploys GitHub Pages from
    `docs/` and syncs the wiki via `scripts/sync-wiki.py` (needs the `WIKI_PAT` secret, skipped
    with a warning when absent).
  - Verification stays local: `dotnet build` / `dotnet test` per project on Windows (plus WSL2
    for the Linux paths), because the suites include live endpoints and real app launches.
- **Fast/slow test split (no filtering)**: the suites are split **by project**, never by
  `[Trait]`/filter: fast — `SimpleLauncher.Tests` and `SimpleLauncher.Avalonia.Tests`; slow —
  `SimpleLauncher.SlowTests` (app-launch language checks, real ZIP/CHD mounts, live-endpoint
  suites) and `SimpleLauncher.Avalonia.SlowTests` (cross-platform mount extraction/guards).
  Every test in a project always runs unfiltered with `dotnet test` — never add a filter,
  quarantine, runtime skip, or exclusion mechanism for flaky tests. Run only the **fast suites**
  during iteration; run the slow suites **only before a deploy or when the admin explicitly asks**
  for them — never as part of routine iteration or verification.
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

## 🔧 Build & test

- Windows (per project): `dotnet build <proj>.csproj -c Debug` (or `-c Release`) — expect
  0 warnings / 0 errors.
- Fast suites (run during iteration):
  - `dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj` (~40 s) — everything except
    the slow projects below.
  - `dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj` (~20 s).
- Slow suites (**do not run during iteration** — only before a deploy or when the admin explicitly
  asks for them):
  - `dotnet test SimpleLauncher.SlowTests/SimpleLauncher.SlowTests.csproj` (~3.5 min) — launches
    `SimpleLauncher.exe` for the language checks (no other SimpleLauncher instance may run),
    mounts real ZIP/CHD images (needs Dokan + the bundled mount tools; skips cleanly when the
    images/tools are absent) and hits the live GitHub/PureLogicCode endpoints.
  - `dotnet test SimpleLauncher.Avalonia.SlowTests/SimpleLauncher.Avalonia.SlowTests.csproj`
    (~1 s) — cross-platform mount extraction fallback and platform guards.
- Version metadata is centralized: `SimpleLauncher/SimpleLauncher.csproj` is canonical;
  `SimpleLauncher.Core.csproj`, `SimpleLauncher/app.manifest`, the Avalonia csproj/manifest and
  `SimpleLauncher.Avalonia.Updater.csproj` must stay in sync (`VersionConsistencyTests` enforces
  and auto-corrects this). The two `appsettings.json` files must stay identical — the unified
  bundle ships a single shared copy (`AppSettingsFilesAreIdentical` enforces this).

## 🖥️ Linux VM GUI test harness (AT-SPI + vision)

The Avalonia/Linux manual checklist (`docs/manual-tests.md`) is exercised automatically on a
Hyper-V Linux VM by the harness tracked in `scripts\gui-test-harness\`. The portable manual
(prerequisites, configuration, fixture contract, scenario authoring, troubleshooting) is
`docs/gui-test-harness.md`; the live scenario inventory and session state are in `ManualTests.md`.

Machine-specific details (VM IP/credentials, host share paths) and the current resume checklist
are kept **local only** in `AGENTS.local.md`, which is gitignored — when present, read it before
starting a GUI-test session.
