# Agent Instructions — SimpleLauncher

Standing rules for AI agents (and anyone) working in this repository.

## 🚫 Never delete files inside `bin\Release`

The `bin\Release` output folders (in any project: `SimpleLauncher`, `SimpleLauncher.Core`,
`SimpleLauncher.Updater`, `SimpleLauncher.Avalonia`, `SimpleLauncher.Avalonia.Updater`,
`Tools\*`, etc.) contain the **built release artifacts used for packaging and publishing**.
They are intentionally kept on disk.

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

- **CI scope**: GitHub Actions only packages releases and deploys docs — it never runs tests.
  - `.github/workflows/release-wpf.yml` — manual (`workflow_dispatch`); packages the WPF app
    for `win-x64` / `win-arm64` via `scripts/package-release.ps1`
    (`release_{version}_{rid}.zip` + `updater_{rid}.zip`) and creates/updates the GitHub release.
  - `.github/workflows/release-avalonia.yml` — manual (`workflow_dispatch`); packages the
    Avalonia app for `win-x64` / `win-arm64` via `scripts/package-release.ps1 -App Avalonia`
    (`release_avalonia_{version}_{rid}.zip` + `updater_avalonia_{rid}.zip`) and attaches the
    assets to the same GitHub release tag. The `avalonia_` prefix is required: both apps read
    the same `releases/latest` assets, and unprefixed names would collide with the WPF packages.
  - `.github/workflows/docs.yml` — on `docs/**` pushes to `master`; deploys GitHub Pages from
    `docs/` and syncs the wiki via `scripts/sync-wiki.py` (needs the `WIKI_PAT` secret, skipped
    with a warning when absent).
  - Verification stays local: `dotnet build` / `dotnet test` per project on Windows (plus WSL2
    for the Linux paths), because the suites include live endpoints and real app launches.
- **No quarantine**: there is no test filtering or `[Trait]`-based exclusion — **all tests run
  unfiltered in every `dotnet test`**, including live-endpoint tests and app-launch tests.
  Never add a filter, quarantine, or skip mechanism back.
- **Bug reports**: the `BugReportApiSink` sends Warning+ log events to the bug-report API.
  Expected user-error conditions (missing files, rate limits, timeouts, unsupported input)
  must be logged at **Information** level, not Warning/Error, so they are never reported as bugs.

## 🔧 Build & test

- Windows (per project): `dotnet build <proj>.csproj -c Debug` (or `-c Release`) — expect
  0 warnings / 0 errors.
- Full WPF suite: `dotnet test SimpleLauncher.Tests/SimpleLauncher.Tests.csproj` (~3 min,
  includes live endpoints and real app launches — no other SimpleLauncher instance may run).
- Avalonia suite: `dotnet test SimpleLauncher.Avalonia.Tests/SimpleLauncher.Avalonia.Tests.csproj`.
- Version metadata is centralized: `SimpleLauncher/SimpleLauncher.csproj` is canonical;
  `SimpleLauncher.Core.csproj`, `SimpleLauncher/app.manifest`, and
  `SimpleLauncher.Updater/version.txt` must stay in sync (`VersionConsistencyTests` enforces
  and auto-corrects this).
