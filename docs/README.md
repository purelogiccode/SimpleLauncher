[![CI](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/ci.yml)
[![Docs deploy](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/docs.yml/badge.svg)](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/docs.yml)
[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/releases)
[![Docs: GitHub Pages](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://purelogiccode.github.io/SimpleLauncher/docs.html)
[![Wiki](https://img.shields.io/badge/docs-GitHub%20Wiki-blue)](https://github.com/purelogiccode/SimpleLauncher/wiki)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://github.com/purelogiccode/SimpleLauncher/blob/master/LICENSE.txt)

# Simple Launcher — Documentation

**Start here:** [Quickstart](03-quickstart.md) · [FAQ](faq.md) · [How it compares](comparison.md) · [Privacy](privacy.md)

## 20-second tour

<a href="https://purelogiccode.github.io/SimpleLauncher/media/demo.mp4"><img src="https://purelogiccode.github.io/SimpleLauncher/media/demo-poster.jpg" alt="20-second tour of Simple Launcher" width="720" style="max-width:100%;height:auto;"></a>

System selection, grid and list libraries, PC store games and RetroAchievements.

Developer and user documentation for the main projects:

- [`SimpleLauncher.Avalonia`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher.Avalonia/SimpleLauncher.Avalonia.csproj) - the shipped app for Windows x64/ARM64 (as `SimpleLauncher.exe`), Linux x64/ARM64 (AppImage, Flatpak, tar.gz, ZIP) and macOS Apple Silicon (unsigned DMG); remaining work is tracked in [`References/TODO.md`](https://github.com/purelogiccode/SimpleLauncher/blob/master/References/TODO.md)
- [`SimpleLauncher.Core`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher.Core/SimpleLauncher.Core.csproj) - class library (services, models, persistence)
- [`SimpleLauncher`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher/SimpleLauncher.csproj) - WPF reference implementation, kept in the separate `SimpleLauncher.Wpf.Reference.sln` and reference tests but not shipped or built by CI

Companion files: [`ManualTests.md`](manual-tests.md) (manual test checklist for areas without unit tests) · [`WhatsNew.md`](https://github.com/purelogiccode/SimpleLauncher/blob/master/SimpleLauncher/WhatsNew.md) (changelog) · [`parameters.md`](parameters.md) (emulator parameter reference).

## One source, two publications

These files are published **twice from the same `docs/` folder**:

- **GitHub Pages** - [purelogiccode.github.io/SimpleLauncher](https://purelogiccode.github.io/SimpleLauncher/) serves a static landing page (`index.html` + `landing.css`); the docs are at [docs.html](https://purelogiccode.github.io/SimpleLauncher/docs.html), rendered with docsify (`docs.html` + `site.css`).
- **GitHub Wiki** — [github.com/purelogiccode/SimpleLauncher/wiki](https://github.com/purelogiccode/SimpleLauncher/wiki) is synced by `scripts/sync-wiki.py`, which also translates `docs/README.md` into the wiki `Home` page.

Both publications use a **side menu**: `docs/_sidebar.md` is the docsify navigation, and the sync script converts it into the wiki `_Sidebar.md` (preserving its sections, order and labels). Add or rename a page in `docs/_sidebar.md` and re-run the sync to update both menus; keep every entry pointing at a page that exists in `docs/`.

## Reading order

- **User-facing:** 01 → 03 → 05 → 16 → 18
- **Architecture:** 04 → 02 → 06 → 07
- **Feature deep-dives:** 08 → 09 → 10 → 11 → 12 → 13
- **Maintenance:** 14 → 15 → 16 → 17

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
| [11 - Bundled Tools](11-bundled-tools.md) | Shipped `tools\` helper payloads, BundledToolPathResolver, Tools\ source projects |
| [12 — Data Formats](12-data-formats.md) | XML / MessagePack / SQLite / JSON / YAML / TOML usage across the app |
| [13 — Logging & Debug](13-logging-and-debug.md) | Serilog setup, sinks, Debug Window, bug-report pipeline |
| [14 — Testing](14-testing.md) | Test projects (fast/slow split), helpers, coverage summary, running tests |
| [15 — Development](15-development.md) | Build/publish, versioning, localization, analyzers, release workflow |
| [16 — Updater](16-updater.md) | Update check, Updater.exe flow, restart/reinstall, install hardening |
| [17 — Release Notes](17-release-notes.md) | Condensed changelog 5.9.1 → 1.1 |
| [18 — Emulator Parameters](18-emulator-parameters.md) | parameters.md conventions + full system → emulator index (84 systems / 352 entries) |
| [19 — Linux Packages](19-linux-packages.md) | AppImage, Flatpak and portable Linux formats, install and update behavior |
| [20 - macOS Package](20-macos-packages.md) | Unsigned Apple Silicon DMG, Gatekeeper, Easy Mode catalog, build and NoMac validation |
| [21 - Release Artifacts (planned)](21-release-artifacts.md) | Next-cycle download matrix: Windows ZIP/setup, macOS DMG, DEB/RPM, AppImage, Flatpak, tar.gz |
| [22 - Cover Catalog (R2)](22-cover-catalog.md) | Per-game cover catalog on R2: layout, annotation/alias filtering, 800px derivatives, build and upload scripts |
| [FAQ](faq.md) | Runtime, Dokan/FUSE, first system, data locations, updates, SmartScreen, Steam Deck |
| [Comparison](comparison.md) | Honest table vs LaunchBox, ES-DE, RetroBat and Playnite, with a last-verified date |
| [Privacy](privacy.md) | What the app stores locally, what it sends, and how to disable automatic reports |
| [GUI Test Harness](gui-test-harness.md) | AT-SPI + vision harness for the Linux VM manual checklist |
| [Manual Tests](manual-tests.md) | Manual test checklist for areas without unit tests |
| [Parameters](parameters.md) | Emulator command-line reference with official download links |

## Conventions used in these docs

- Code facts are cited with `path:line` references into the repository.
- `⚠` marks behavior that should be verified before relying on it.
- Relative links (`xx-*.md`) work from within `docs\`; `../` links point to the repo root.
- Version-specific numbers (package versions, test counts) reflect the repository at doc-write time (5.9.1).
