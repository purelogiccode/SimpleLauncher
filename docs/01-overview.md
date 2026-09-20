# 01 — Overview

> Simple Launcher — an open-source emulator frontend for Windows (x64 & ARM64).
> Related: [02 — Projects & Solution](02-projects-and-solution.md) · [03 — Quickstart](03-quickstart.md) · [README](README.md) (docs index)

## What it is

**Simple Launcher** is a WPF desktop application that organizes, browses, and launches a retro (and modern PC) game collection through emulators. It is a *frontend*: it does not ship ROMs, ISOs, or BIOS files, and you must provide your own emulators.

Three code projects implement it:

| Project | Role |
|---|---|
| `SimpleLauncher` (WPF app) | The launcher itself: windows, pages, ViewModels, UI services, launch handlers, game scanners, DI composition root |
| `SimpleLauncher.Core` (class library) | Platform-independent services, models, interfaces, data persistence, emulator config injection |
| `SimpleLauncher.Avalonia` (Avalonia UI app) | Cross-platform port (Windows + Linux) reusing all of Core; port status tracked in [`AvaloniaPlan.md`](../AvaloniaPlan.md) |

## Key differentiators

- **Configuration injection into 21 emulators** — Ares, Azahar, Blastem, Cemu, Daphne, Dolphin, DuckStation, Flycast, MAME, Mednafen, Mesen, PCSX2, Raine, Redream, RetroArch, RPCS3, Sega Model 2, Stella, Supermodel, Xenia, Yumir. Settings are written into each emulator's own config file before launch (see [06 — Systems & Launch](06-systems-and-launch.md)).
- **Universal CHD support** — the bundled **CHDMounter** mounts CHD files as virtual drives for 15+ emulators without native CHD support (Xenia, RPCS3, Xemu, Cxbx-Reloaded, Mednafen, Mesen, Raine, FinalBurn Neo/Alpha, 4DO, Gens, Blastem, Yabause, PCSX-Redux, CD-i Emulator, Tsugaru, Kega Fusion, DOSBox).
- **On-the-fly mounting** — launch games directly from `.zip`, `.iso`, `.xiso`, `.chd` without manual extraction (requires **Dokan**).
- **Modern store integration** — automatic scanning for games from Steam, Epic, GOG, Microsoft Store, Amazon, Battle.net, EA App, Humble, itch.io, Rockstar, Uplay (see [10 — Game Scanning](10-game-scanning.md)).
- **RetroAchievements integration** — login, per-game achievements/rankings, profile, completion progress, hashing for complex systems, and automatic credential injection into supported emulators (see [09 — RetroAchievements](09-retroachievements.md)).
- **Easy Mode wizard** — guided download & configuration of emulators, cores, and image packs.
- **Expert Mode** — full manual control of `system.xml`: multiple ROM folders, placeholders (`%BASEFOLDER%`, `%SYSTEMFOLDER%`, `%EMULATORFOLDER%`, `%ROM%`, `%NAME%`, `%ROMSYSTEMFOLDER%`), launch parameters.
- **Performance** — MessagePack binary storage (`favorites.dat`, `playhistory.dat`, `history.dat`, `mame.dat`, `RetroAchievements.dat`), async scanning/loading, pagination.
- **Platform coverage** — native **x64 and ARM64** builds; Windows 10+; .NET 10 runtime.

## Feature surface (summary)

- Dual **Grid / List** views, letter filter bar, system selection screen, pagination, zoom, aspect ratios, filename display modes.
- **Favorites**, **Play History** (play count, play time, last played), **Global Search** with `AND`/`OR`, **Global Statistics**.
- **Fuzzy cover-image matching** with configurable threshold + annotation stripping (`Game (USA)` → `Game`).
- **Themes** (Light, Dark, Adaptive, High Contrast, Midnight) + 27 accent colors; **18 languages** (ar, bn, de, en, es, fr, hi, id, it, ja, ko, nl, pt-br, ru, tr, ur, vi, zh-hans).
- **Gamepad navigation** (Xbox XInput + PlayStation DirectInput on Windows, SDL2 GameController API on Linux/macOS), dead-zone configuration, UI sound effects (NAudio). On Windows the controller moves the real mouse cursor / clicks (WPF behavior); on Linux/macOS the Avalonia app's `GamepadNavigationService` maps the pad to in-app focus navigation, activation, context menu and scrolling instead (Wayland-safe, no input-injection permissions).
- **Tray icon**, minimize-to-tray, **F8 global screenshot hotkey**, loading overlays, status bar, debug window (`-debug`).
- **Built-in updater** (`Updater.exe`) with GitHub release assets, `--restarting` restart flow.
- **Bundled power tools** — conversion (CHD, RVZ, XISO, 7z/zip), batch-file creators, cover tools, ROM validator (see [11 — Bundled Tools](11-bundled-tools.md)).
- **100+ supported systems** — Nintendo, Sony, Sega, Atari, NEC, SNK, Commodore, arcade (MAME/FBN/Raine), retro computers, and modern PC storefronts (the authoritative per-system emulator guide is [`parameters.md`](parameters.md); see also [18 — Emulator Parameters](18-emulator-parameters.md)).

## Localization

18 languages ship as **one shared set of JSON packs**: `SimpleLauncher.Core\Localization\strings.{code}.json` (2669 keys per file, UTF-8 without BOM, key-sorted). Both apps embed them in their assemblies — Avalonia as manifest resources (`SimpleLauncher.Avalonia.Resources.strings.{code}.json`, loaded by `LocalizationService`) and WPF as pack resources (`resources/strings.{code}.json` in `SimpleLauncher.g.resources`, with `App.ApplyLanguage` building the WPF `ResourceDictionary` from the JSON). Switching language restarts the app. See [08 — UI Layer](08-ui-layer.md#themes--language).

`SimpleLauncher.ResourceTranslator` (OpenRouter API, default `z-ai/glm-5.3-flash`) propagates missing keys from `strings.en.json` to all other languages; see its [README](../SimpleLauncher.ResourceTranslator/README.md).

## Version & license

- Current version: **5.8.0** (`SimpleLauncher.csproj` is canonical; `SimpleLauncher.Core.csproj` and both `app.manifest` files are kept in sync and covered by `VersionConsistencyTests`). The Avalonia app (`SimpleLauncher.Avalonia`) is synced to the same version (5.8.0) and ships in the same release bundle.
- The secondary server publishes a `version.txt` (e.g. `release5.8.0`) used by the update-check fallback.
- Framework: **.NET 10** (`net10.0-windows`), **C# 14**, nullable reference types enabled.
- License: **GPLv3** (`LICENSE.txt`).
- Repository: https://github.com/purelogiccode/SimpleLauncher

## Release history

See [17 — Release Notes](17-release-notes.md) for a condensed changelog (5.8.0 → 1.1); the canonical file is `SimpleLauncher\WhatsNew.md`.

## Related docs

- [Docs index](README.md)
- [02 — Projects & Solution](02-projects-and-solution.md)
- [04 — Architecture](04-architecture.md)
