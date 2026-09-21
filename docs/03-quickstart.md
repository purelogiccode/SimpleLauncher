# 03 — Quickstart

> Install, first launch, Easy Mode, folder structure, launching a first game.
> Related: [01 — Overview](01-overview.md) · [05 — Configuration](05-configuration.md)

## Prerequisites

- **Windows 10 or 11** (x64 or ARM64 — native ARM64 builds are provided).
- **.NET 10 Desktop Runtime** (the app targets `net10.0-windows`, runtime `10.0.2`).
- **Dokan** (optional but recommended) — required for on-the-fly mounting of `.iso`, `.xiso`, `.zip`, and universal CHD mounting. The app detects Dokan and shows a specific message when it is missing.
- **Linux/macOS (Avalonia app):** no .NET install needed for the self-contained release bundles; **`libasound2`** for audio output and the optional **`libsndfile1`** for FLAC/Ogg/Opus sound effects (MP3/WAV need no extra packages). Mounting tools are Windows-only — `.zip`/`.7z`/`.rar` launch extracts to a temp folder, CHD is converted on the fly, and `.iso` is mounted inside DOSBox. **Linux support is beta** (verified on Ubuntu 24.04 x64 and Fedora 44 KDE; please report issues).
- A writable folder for the app (the launcher refuses to run from a temp folder and detects non-writable installs).
- Your own **emulators and ROMs** (the launcher ships none).

## Install

1. Download the latest release zip (`release_{version}_{rid}.zip`) for your platform from the [GitHub releases page](https://github.com/purelogiccode/SimpleLauncher/releases): `win-x64` / `win-arm64` (WPF + Avalonia) or `linux-x64` / `linux-arm64` (self-contained Avalonia).
2. Extract to a folder (e.g. `C:\SimpleLauncher`). Do **not** run from a temporary/Downloads extraction folder.
3. Run either `SimpleLauncher.exe` (WPF) or `SimpleLauncher.Avalonia.exe` (Avalonia) — both ship in the same zip, next to each other, and share the same content and settings files. Pick whichever you prefer; there is no need to install both. On Linux, extract the `linux-*` zip and run `./SimpleLauncher.Avalonia` — the payload is self-contained and ships `Updater` plus the Linux `tools/` binaries alongside.

On first launch the app:

1. checks required files and folder writability;
2. scans installed storefront games (Steam, Epic, GOG, …) into a **"Microsoft Windows"** system — this is skipped if systems already exist, and is a Windows-only step (other platforms go straight to step 3);
3. if still no systems, shows the welcome message and opens **Easy Mode** (or you can close it and use Expert Mode).

## Folder structure

```
SimpleLauncher/
├── SimpleLauncher.exe         WPF app
├── SimpleLauncher.Avalonia.exe  Avalonia app (same folder, shared files)
├── Updater.exe                the single updater used by both apps
├── appsettings.json, mame.dat, parameters.md, WhatsNew.md
├── audio/                     UI sound effects (click, notification, shutter, trash)
├── emulators/                 recommended place for emulators
├── images/
│   ├── systems/               system logos for the selection screen
│   └── [SystemName]/          game cover art
├── roms/[SystemName]/         default game location per system
├── title_snapshots/ gameplay_snapshots/ videos/ manuals/ walkthrough/
│   └── [SystemName]/          additional media (context menu → View Media)
├── cabinets/ carts/ flyers/ pcbs/   arcade media folders
├── tools/                     bundled utilities (see 11 — Bundled Tools)
└── samples/                   emulator config templates
```

Data files: user data lives in the unified SQLite database `settings.dat` — next to the exe in portable mode on Windows, otherwise in `%LocalAppData%\SimpleLauncher\` (Windows) or `~/.local/share/SimpleLauncher\` (Linux/macOS); legacy `settings.xml`, `system.xml`, `favorites.dat`, and `playhistory.dat` are migrated automatically on first launch — see [05 — Configuration](05-configuration.md).

## Easy Mode (guided setup)

1. **Edit System → Easy Mode.**
2. Pick a system from the dropdown (presets contain recommended settings + download links).
3. Optionally choose a ROMs folder (blank → `SimpleLauncher\roms\[SystemName]`).
4. **Download Emulator** (and **Download Core** for RetroArch, **Download Image Pack** optionally) — downloads show progress and can be stopped.
5. **Add System** — saves the configuration to `system.xml` and creates the default folders.

## Expert Mode (manual)

**Edit System → Expert Mode** opens the full `system.xml` editor: system name, multiple ROM folders, image folder, formats, extract-before-launch, group-by-folder, and up to 5 emulators with parameters. Path fields support the placeholders `%BASEFOLDER%`, `%SYSTEMFOLDER%`, `%EMULATORFOLDER%`, `%ROM%`, `%NAME%`, `%ROMSYSTEMFOLDER%` (see [05 — Configuration](05-configuration.md#path-placeholders) and [18 — Emulator Parameters](18-emulator-parameters.md)).

## Launching your first game

1. Select a system on the system-selection screen.
2. Pick an emulator in the emulator dropdown (or use the system's default).
3. Double-click a game (or single-click + **Launch**) — compressed archives are extracted or mounted automatically per the system's settings (see [06 — Systems & Launch](06-systems-and-launch.md)).

## Where to find help

- Emulator parameters per system: `parameters.md` in the app folder (also shown via the Help pane in Edit System) — see [18 — Emulator Parameters](18-emulator-parameters.md).
- Update history: **Help → Update History** (`WhatsNew.md`).
- Bugs/support: the built-in **Support Window** or the [GitHub issues](https://github.com/purelogiccode/SimpleLauncher/issues).
- Debug logs: see [13 — Logging & Debug](13-logging-and-debug.md).

## Related docs

- [01 — Overview](01-overview.md)
- [05 — Configuration](05-configuration.md)
- [06 — Systems & Launch](06-systems-and-launch.md)
- [08 — UI Layer](08-ui-layer.md)
