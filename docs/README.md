# Simple Launcher — Documentation

Developer and user documentation for the main projects:

- [`SimpleLauncher`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher/SimpleLauncher.csproj) — WPF desktop app (the launcher)
- [`SimpleLauncher.Core`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher.Core/SimpleLauncher.Core.csproj) — class library (services, models, persistence)
- [`SimpleLauncher.Avalonia`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj) — cross-platform Avalonia port (Windows + Linux); port plan: [`AvaloniaPlan.md`](../AvaloniaPlan.md)

Companion files: [`ManualTests.md`](manual-tests.md) (manual test checklist for areas without unit tests) · [`WhatsNew.md`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher/WhatsNew.md) (changelog) · [`parameters.md`](parameters.md) (emulator parameter reference).

## Reading order

**User-facing:** 01 → 03 → 05 → 18
**Architecture:** 04 → 02 → 06 → 07
**Feature deep-dives:** 08 → 09 → 10 → 11 → 12 → 13
**Maintenance:** 14 → 15 → 16 → 17

## Document map

| Doc | Content |
|---|---|
| [01 — Overview](01-overview.md) | What Simple Launcher is, differentiators, feature surface, version/license |
| [02 — Projects & Solution](02-projects-and-solution.md) | Solution layout, csproj deep-dive for both projects, packages, folder structure |
| [03 — Quickstart](03-quickstart.md) | Install, prerequisites, first launch, Easy/Expert Mode, folder structure |
| [04 — Architecture](04-architecture.md) | Layers, DI composition root, startup/shutdown sequence, host-interface pattern, MVVM |
| [05 — Configuration](05-configuration.md) | appsettings.json, settings.xml, system.xml schema, placeholders, data-file locations, credentials |
| [06 — Systems & Launch](06-systems-and-launch.md) | System model, launch pipeline, 8 launch strategies, extraction/mounting/conversion, 21 emulator config handlers, AI parameter resolution |
| [07 — Core Services](07-core-services.md) | Complete catalog of `SimpleLauncher.Core\Services\` (grouped) + interfaces + models |
| [08 — UI Layer](08-ui-layer.md) | Windows, pages, ViewModels, UI services, menus, themes, languages |
| [09 — RetroAchievements](09-retroachievements.md) | RA API client, system matcher, hashing, credential injection, RA windows |
| [10 — Game Scanning](10-game-scanning.md) | GameScannerService + the 11 storefront scanners |
| [11 — Bundled Tools](11-bundled-tools.md) | Shipped `tools\` payloads, ExternalToolLauncherService, Tools\ source projects |
| [12 — Data Formats](12-data-formats.md) | XML / MessagePack / SQLite / JSON / YAML / TOML usage across the app |
| [13 — Logging & Debug](13-logging-and-debug.md) | Serilog setup, sinks, Debug Window, bug-report pipeline |
| [14 — Testing](14-testing.md) | Test project, helpers, coverage summary, running tests, known slow test |
| [15 — Development](15-development.md) | Build/publish, versioning, localization, analyzers, release workflow |
| [16 — Updater](16-updater.md) | Update check, Updater.exe flow, restart/reinstall |
| [17 — Release Notes](17-release-notes.md) | Condensed changelog 5.8.0 → 1.1 |
| [18 — Emulator Parameters](18-emulator-parameters.md) | parameters.md conventions + full system → emulator index (84 systems / 352 entries) |

## Conventions used in these docs

- Code facts are cited with `path:line` references into the repository.
- `⚠` marks behavior that should be verified before relying on it.
- Relative links (`xx-*.md`) work from within `docs\`; `../` links point to the repo root.
- Version-specific numbers (package versions, test counts) reflect the repository at doc-write time (5.8.0).