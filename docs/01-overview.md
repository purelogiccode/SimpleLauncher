# 01 — Overview

> Simple Launcher - an open-source emulator frontend for Windows and Linux (x64 & ARM64) and macOS (Apple Silicon).
> Related: [02 — Projects & Solution](02-projects-and-solution.md) · [03 — Quickstart](03-quickstart.md) · [README](README.md) (docs index)

## What it is

**Simple Launcher** is an Avalonia desktop application that organizes, browses, and launches a retro (and modern PC) game collection through emulators. It is a *frontend*: it does not ship ROMs, ISOs, or BIOS files, and you must provide your own emulators.

Four application projects implement it:

| Project | Role |
|---|---|
| `SimpleLauncher.Avalonia` (Avalonia UI app) | **The shipped launcher** on Windows, Linux and macOS (x64/ARM64): windows, pages, ViewModels, UI services, launch handlers, game scanners, DI composition root. The Windows release publishes it as `SimpleLauncher.exe` |
| `SimpleLauncher.Core` (class library) | Platform-independent services, models, interfaces, data persistence, emulator config injection |
| `SimpleLauncher` (WPF app) | **Reference implementation, not shipped.** Kept in the source tree and the separate `SimpleLauncher.Wpf.Reference.sln` for parity checks while the Avalonia app remains the active target |
| `SimpleLauncher.Avalonia.Updater` (Avalonia app) | The single `Updater.exe` / `Updater`: checks for updates, swaps the payload, relaunches the app and migrates legacy WPF installs to the Avalonia `SimpleLauncher.exe` |

On Windows the Avalonia app hides Windows-only features (Dokan mounting, emulator config injection, F8 hotkey, storefront scanning) or uses platform equivalents; on Linux archives and disc images mount through FUSE 3, with temp extraction/conversion as automatic fallbacks; macOS ships an unsigned Apple Silicon DMG with extraction/conversion fallbacks (no FUSE) and no in-app updater.

## Key differentiators

- **Configuration injection into 21 emulators** — Ares, Azahar, Blastem, Cemu, Daphne, Dolphin, DuckStation, Flycast, MAME, Mednafen, Mesen, PCSX2, Raine, Redream, RetroArch, RPCS3, Sega Model 2, Stella, Supermodel, Xenia, Yumir. Settings are written into each emulator's own config file before launch (see [06 — Systems & Launch](06-systems-and-launch.md)).
- **Universal CHD support** — the bundled **CHDMounter** mounts CHD files as virtual drives for 15+ emulators without native CHD support (Xenia, RPCS3, Xemu, Cxbx-Reloaded, Mednafen, Mesen, Raine, FinalBurn Neo/Alpha, 4DO, Gens, Blastem, Yabause, PCSX-Redux, CD-i Emulator, Tsugaru, Kega Fusion, DOSBox). It uses Dokan/WinFsp on Windows and FUSE 3 on Linux; when FUSE or the bundled mount payload is unavailable on Linux, the app falls back to the managed CHDSharp conversion automatically.
- **On-the-fly mounting** — launch games directly from `.zip`, `.7z`, `.rar`, `.iso`, `.xiso`, `.cso`, `.zar` and `.chd` without manual extraction: **Dokan** on Windows, **FUSE 3** on Linux (SimpleZipDrive, SimpleXisoDrive, CHDMounter), with automatic temp extraction / CHDSharp conversion fallbacks when a driver or payload is unavailable. DOSBox ISO games use the native `imgmount`.
- **Modern store integration** — automatic scanning for games from Steam, Epic, GOG, Microsoft Store, Amazon, Battle.net, EA App, Humble, itch.io, Rockstar, Uplay (see [10 — Game Scanning](10-game-scanning.md)).
- **RetroAchievements integration** — login, per-game achievements/rankings, profile, completion progress, hashing for complex systems, and automatic credential injection into supported emulators (see [09 — RetroAchievements](09-retroachievements.md)).
- **Easy Mode wizard** — guided download & configuration of emulators, cores, and image packs.
- **Expert Mode** — full manual control of `system.xml`: multiple ROM folders, placeholders (`%BASEFOLDER%`, `%SYSTEMFOLDER%`, `%EMULATORFOLDER%`, `%ROM%`, `%NAME%`, `%ROMSYSTEMFOLDER%`), launch parameters.
- **Performance** — one SQLite `settings.dat` for settings/favorites/history/systems plus MessagePack caches (`history.dat`, `mame.dat`, `RetroAchievements.dat`); async scanning/loading and pagination. The Avalonia startup shows the system-selection grid immediately and scans the ROM folders for sidebar counts in the background.
- **Platform coverage** - native **x64 and ARM64** builds for Windows 10+ and Linux (Avalonia), plus **Apple Silicon macOS**. Linux ships as a self-contained **AppImage**, a **Flatpak** bundle, and portable **tar.gz**/**ZIP** builds (the portable formats require the .NET 11 runtime). macOS ships as an unsigned, self-contained **DMG** (ad-hoc signed, not notarized; manual updates). Verified end-to-end on Ubuntu 24.04 x64, Fedora 44 KDE and Linux Mint 22.3; the `linux-arm64` portable tarball and AppImage launch on Ubuntu 24.04 ARM64; the Windows ARM64 Avalonia app launches on Windows 11 ARM64; the macOS DMG is validated on a NoMac Apple M5 Pro host. See [19 - Linux Packages](19-linux-packages.md) and [20 - macOS Package](20-macos-packages.md).

## Feature surface (summary)

- Dual **Grid / List** views, letter filter bar, system selection screen, pagination, zoom, aspect ratios, filename display modes.
- **Favorites**, **Play History** (play count, play time, last played), **Global Search** with `AND`/`OR`, **Global Statistics**.
- **Fuzzy cover-image matching** with configurable threshold + annotation stripping (`Game (USA)` → `Game`).
- **Themes** (Light, Dark, Adaptive, High Contrast, Midnight) + 27 accent colors; **18 languages** (ar, bn, de, en, es, fr, hi, id, it, ja, ko, nl, pt-br, ru, tr, ur, vi, zh-hans).
- **Gamepad navigation** (Xbox XInput + PlayStation DirectInput on Windows, SDL2 GameController API on Linux/macOS), dead-zone configuration, UI sound effects (NAudio). The shipped app's `GamepadNavigationService` maps the pad to in-app focus navigation, activation, context menu and scrolling; the WPF reference keeps the legacy cursor-injection behavior for parity checks (Wayland-safe, no input-injection permissions).
- **Tray icon**, minimize-to-tray, **F8 global screenshot hotkey**, loading overlays, status bar, debug window (`-debug`).
- **Built-in updater** (`Updater.exe`) with GitHub release assets, `--restarting` restart flow
  (Windows/Linux; the macOS `.app` is replaced manually with the next DMG).
- **Bundled runtime helpers** - CHD/Xbox/archive mounting, 7-Zip extraction and RetroAchievements hashing; the optional converter/cover/validation GUI apps are distributed separately (see [11 - Bundled Tools](11-bundled-tools.md)).
- **100+ supported systems** — Nintendo, Sony, Sega, Atari, NEC, SNK, Commodore, arcade (MAME/FBN/Raine), retro computers, and modern PC storefronts (the authoritative per-system emulator guide is [`parameters.md`](parameters.md); see also [18 — Emulator Parameters](18-emulator-parameters.md)).

## Localization

18 languages ship as **one shared set of JSON packs**: `SimpleLauncher.Core\Localization\strings.{code}.json` (2671 keys per file, UTF-8 without BOM, key-sorted). The shipped Avalonia app embeds them as manifest resources (`SimpleLauncher.Avalonia.Resources.strings.{code}.json`, loaded by `LocalizationService`); the WPF reference embeds the same packs as pack resources for parity checks. Switching language restarts the app. See [08 — UI Layer](08-ui-layer.md#themes--language).

`SimpleLauncher.ResourceTranslator` (OpenRouter API, default `z-ai/glm-5.3-flash`) propagates missing keys from `strings.en.json` to all other languages; see its [README](../SimpleLauncher.ResourceTranslator/README.md).

## Version & license

- Current version: **5.9.0** (`SimpleLauncher.csproj` is canonical; `SimpleLauncher.Core.csproj` and both `app.manifest` files are kept in sync and covered by `VersionConsistencyTests`). The shipped Avalonia app (`SimpleLauncher.Avalonia`) is synced to the same version (5.9.0).
- The secondary server publishes a `version.txt` (e.g. `release5.9.0`) used by the update-check fallback.
- Framework: **.NET 11** (`net11.0-windows`), **C# 15**, nullable reference types enabled.
- License: **GPLv3** (`LICENSE.txt`).
- Repository: https://github.com/purelogiccode/SimpleLauncher

## Release history

See [17 — Release Notes](17-release-notes.md) for a condensed changelog (5.9.0 → 1.1); the canonical file is `SimpleLauncher\WhatsNew.md`.

## Related docs

- [Docs index](README.md)
- [02 — Projects & Solution](02-projects-and-solution.md)
- [04 — Architecture](04-architecture.md)
