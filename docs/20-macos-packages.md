# 20 — macOS Package (Apple Silicon)

Simple Launcher ships one macOS format:

| Format | File | Architecture | Needs .NET 11? | Updates |
|---|---|---|---|---|
| **DMG** | `SimpleLauncher-{version}-osx-arm64.dmg` | Apple Silicon (arm64) only | No — the runtime is bundled in the `.app` | Manual: install the next DMG |

The DMG contains `Simple Launcher.app` and an **Applications** shortcut. The app inside is
self-contained (the .NET 11 runtime travels with it), so there is no runtime prerequisite.

> **Scope:** the macOS build is **unsigned**. It is ad-hoc signed so Apple Silicon can run
> it and it is **not notarized**, because notarization requires the paid Apple Developer
> Program (US$99/year). It supports **Apple Silicon only**; Intel Macs are not built.

## Install

1. Open `SimpleLauncher-{version}-osx-arm64.dmg`.
2. Drag **Simple Launcher** onto the **Applications** shortcut.
3. First launch only: macOS Gatekeeper blocks an app downloaded from the internet when it
   has no Developer ID signature. Either:
   * right-click (or Control-click) **Simple Launcher** in Applications → **Open** →
     **Open**, or
   * clear the quarantine flag once:
     `xattr -dr com.apple.quarantine "/Applications/Simple Launcher.app"`.
4. The app starts normally on later launches.

The app requires **macOS 15 (Sequoia) or newer** on Apple Silicon (the .NET 11 support
baseline). The build is produced and validated on a NoMac Apple M5 Pro (Virtual) host.

## Where user data lives

Settings, logs, favorites, play history and the unified SQLite database (`settings.dat`)
live in `~/Library/Application Support/SimpleLauncher` (the macOS `LocalApplicationData`
folder). `%BASEFOLDER%` also points there on macOS — not inside the
`.app` — so Easy Mode ROMs, cover images, downloaded emulators and image packs survive app
updates and never require writing inside the bundle. Legacy `settings.xml`, `system.xml`,
`favorites.dat` and `playhistory.dat` are migrated on first launch. Replacing the `.app` with
a newer DMG keeps this folder untouched.

## What works, and what does not

Works on macOS:

* Game library management, covers, favorites, play history and RetroAchievements browsing/API
  features.
* Launching macOS emulators through command-line arguments; Easy Mode installs RetroArch
  and its libretro cores (see below), plus Xemu and Vita3K.
* SDL2 gamepad navigation, themes, localization and the in-app help.
* ZIP/7z/RAR launches through temporary extraction; CHD images through the managed
  CHDSharp conversion. `.iso` is mounted inside DOSBox.

Not available on macOS (hidden or automatic fallback):

* **No Dokan/FUSE drive mounting.** Xbox `.iso`/`.xiso` mounting through SimpleXisoDrive is
  Windows/Linux-only; ZIP/7z/RAR and CHD use extraction/conversion instead.
* **Bundled helpers are not shipped.** There are no macOS payloads for CHDMounter,
  RetroAchievementsSharp, SimpleXisoDrive, SimpleZipDrive or 7-Zip, so the services use their
  extraction/conversion fallbacks or report the platform as unsupported.
* **No in-app updater.** `Updater` is not included in the bundle. Download the next DMG and
  replace the app; user data stays in place. **Help → Check for Updates** points at the
  release page.
* **RetroAchievements hash calculation is unavailable.** The bundled
  `RetroAchievementsSharp` CLI has no macOS payload; RA browsing and API features still work,
  but hashing a new ROM cannot run until a macOS build of that tool is shipped.
* Windows-only storefront scanning and emulator-config injection stay hidden, exactly as on
  Linux.

## Easy Mode catalog

The macOS Apple Silicon catalog is
`SimpleLauncher/Services/EasyMode/Samples/easymode_macos_arm64.xml`. It ships inside the DMG
next to the app executable (so Easy Mode works on first launch) and is also published as
[`easymode_macos_arm64.xml`](https://assets.purelogiccode.com/Simple%20Launcher/Simple%20Launcher/easymode_macos_arm64.xml).
It covers 68 systems:

* **RetroArch (Metal build, universal)** with a per-core `_libretro.dylib` downloaded from
  the libretro macOS arm64 buildbot. RetroArch's macOS distribution is a DMG; the Easy Mode
  installer mounts it with `hdiutil`, copies `RetroArch.app`, clears the quarantine flag and
  restores the execute bits.
* **Xemu 0.8.139** (Original Xbox) from the official macOS universal ZIP.
* **Vita3K continuous** (PlayStation Vita) from the official macOS arm64 DMG.

The per-core ZIPs are stored under
`%BASEFOLDER%/emulators/RetroArch/cores/` and RetroArch itself under
`%BASEFOLDER%/emulators/RetroArch/RetroArch.app`.

## Building the DMG

Runs on macOS with the .NET 11 SDK (CI uses the arm64 `macos-15` GitHub runner):

```bash
pwsh scripts/package-release-macos.ps1 -Version 5.8.4
# -> artifacts/release/SimpleLauncher-5.8.4-osx-arm64.dmg
```

The script validates the version against `SimpleLauncher.csproj`/manifests, publishes
`net11.0 -r osx-arm64 --self-contained true`, prunes the Windows/Linux-only helper payloads
and the Updater, builds `Simple Launcher.app` with an `Info.plist` and `.icns`, applies an
ad-hoc signature (`codesign -s -`, skip with `-NoAdHocSign`), and creates and verifies the
DMG with `hdiutil`.

The CI release workflow (`release.yml`) builds the DMG in the `macos-15` job, the publish
job attaches it to the GitHub release and `scripts/publish-fallback-r2.ps1` mirrors it to
the Cloudflare R2 fallback next to the Windows and Linux assets.

## Manual validation runbook (NoMac)

`AGENTS.local.md` has the NoMac connection details. A release validation pass should:

1. `nomac start --wait 600 --max-duration 7200 --max-cost 5`; install the .NET 11 SDK into
   `~/.dotnet` (the host home is fresh after every start).
2. Copy or build the DMG, open it, drag the app to `/Applications`, clear the quarantine
   flag and launch it.
3. Confirm the library window opens and **Easy Mode** lists the 68 systems; install
   RetroArch + a core (for example `gambatte` for Game Boy), Xemu and Vita3K.
4. Launch a game with each installed emulator, check audio, gamepad navigation and the data
   folder (`~/Library/Application Support/SimpleLauncher`).
5. Run the fast suites (`SimpleLauncher.Avalonia.Tests` on macOS, plus
   `SimpleLauncher.Tests` on Windows) and, before publishing, the slow suites with the live
   URL checks.
6. `nomac stop` when finished — this ends billing and deletes the Mac (`~/drive` survives).

**Validation status (2026-11-19, NoMac Apple M5 Pro, macOS 26.6.2, arm64):**

* `scripts/package-release-macos.ps1` built the 107 MB DMG; `hdiutil` verified the mounted
  bundle and Applications shortcut.
* The `.app` launched from `/Applications`; the menu bar shows **Simple Launcher**, and the
  system-image screen, library and settings windows render correctly.
* **Easy Mode loaded all 68 systems** from the bundled catalog. Installing RetroArch (233 MB
  DMG) + the `stella` core through Easy Mode copied `RetroArch.app` and
  `stella_libretro.dylib` into `~/Library/Application Support/SimpleLauncher/emulators`,
  added the Atari 2600 system, scanned a ZIP ROM and launched
  `RetroArch -L …/stella_libretro.dylib -f …/test.zip` successfully.
* `SimpleLauncher.Avalonia.Tests`: **725/725 passed on macOS** on 2026-11-19, including the
  `ExtractFileAsync_MacDiskImage_InstallsAppBundleAndRestoresExecuteBit` test. The cross-platform
  suite has since grown to 768 tests; the next macOS release gate should rerun it.
* macOS UI sounds use the system `/usr/bin/afplay` (CoreAudio) backend on the existing
  audio thread; a harness confirmed it starts and plays `shutter.mp3`. An audible check on
  physical speakers remains.
* Still to exercise on a future run: Xemu/Vita3K installs end-to-end, gamepad navigation
  and RetroAchievements hashing (the tool has no macOS payload).
