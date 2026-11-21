# 19 - Linux Packages

> **Planned for the next release cycle:** native Debian (`.deb`) and RPM (`.rpm`) packages for
> x64 and ARM64 are not shipped yet. Their target matrix, design decisions and acceptance
> checklist are documented in [21 - Release Artifacts](21-release-artifacts.md). Until that
> work is complete, use AppImage, Flatpak, the portable tarball or the portable ZIP.

Simple Launcher ships four Linux formats for **x64** (`linux-x64`) and **ARM64**
(`linux-arm64`). They contain the same Avalonia application, runtime helpers and documents;
the difference is how much runtime they carry and how they update.

| Format | File | Needs .NET 11? | Updates |
|---|---|---|---|
| **AppImage** (recommended) | `SimpleLauncher-{version}-linux-{x64,arm64}.AppImage` | No — runtime bundled | In-app: downloads and replaces the AppImage |
| **Flatpak** | `SimpleLauncher-{version}-linux-{x64,arm64}.flatpak` | No — uses `org.freedesktop.Platform` | Software center when installed from a repo; otherwise reinstall the newer bundle |
| **Portable tarball** | `release_{version}_linux-{x64,arm64}.tar.gz` | Yes | In-app updater |
| **Portable ZIP** | `release_{version}_linux-{x64,arm64}.zip` | Yes | In-app updater |

The portable tarball and ZIP are the same payload; the tarball preserves Unix permissions
natively, while the ZIP carries them in the archive metadata so `unzip` restores them.

## AppImage

```bash
chmod +x SimpleLauncher-{version}-linux-x64.AppImage
./SimpleLauncher-{version}-linux-x64.AppImage
```

- The AppImage runs from a read-only squashfs mount; settings, logs and the single-instance
  lock live in `~/.local/share/SimpleLauncher`.
- On **Help → Check for Updates** the app downloads the new AppImage beside the current file,
  atomically replaces it and restarts. Keep the AppImage in a writable folder.
- If FUSE is unavailable (some containers), run it with
  `--appimage-extract-and-run` or set `APPIMAGE_EXTRACT_AND_RUN=1`. This extracts to a
  temporary folder for that launch; settings still live in `~/.local/share/SimpleLauncher`, and
  in-app updates still work because the outer AppImage file is replaced.
- To integrate it with the desktop menu, use AppImageLauncher or create a `.desktop` entry.

## Flatpak

```bash
flatpak install --user ./SimpleLauncher-{version}-linux-x64.flatpak
flatpak run com.purelogiccode.SimpleLauncher
```

- The bundle depends on `org.freedesktop.Platform//25.08`; Flathub is the simplest source:
  `flatpak remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo`.
- The in-app update check is intentionally skipped. A bundle installed from a **repository**
  (Flathub or a hosted OSTree repo) updates through the software center
  (`flatpak update com.purelogiccode.SimpleLauncher`); a bundle installed directly from a
  `.flatpak` file has no update source, so install the newer bundle with
  `flatpak install --user --reinstall ./SimpleLauncher-{version}-linux-x64.flatpak`.
- **Emulators run on the host**, through `flatpak-spawn --host`, so they use the host's
  libraries, GPU drivers and controllers. The app has host filesystem access to reach ROM
  folders, emulator installs, BIOS files and emulator configuration files.
- **FUSE mounts are disabled inside the sandbox.** ZIP/7z/RAR and Xbox/CHD launches use the
  existing temporary extraction and managed CHDSharp conversion fallbacks; the game still
  launches, only the direct-mount optimization is unavailable.
- Flatpak keeps its own data directory:
  `~/.var/app/com.purelogiccode.SimpleLauncher/data/SimpleLauncher` (not the portable
  `~/.local/share/SimpleLauncher`). Copy `settings.dat` there if you want to migrate an
  existing portable library into the Flatpak.

## Portable tarball / ZIP

```bash
mkdir SimpleLauncher
tar -xzf release_{version}_linux-x64.tar.gz -C SimpleLauncher
cd SimpleLauncher
./SimpleLauncher.Avalonia
```

- Requires the **.NET 11 runtime** (`dotnet.microsoft.com/download`) and `libasound2` for
  audio output; `libsndfile1` is optional and only needed for FLAC/Ogg/Opus UI sounds.
- Install into a writable folder; the app checks this at startup and warns when the folder is
  read-only.
- The in-app updater replaces the files in this folder, exactly like the Windows portable
  build.

## Optional system packages

- **FUSE 3** (`libfuse3` + `/dev/fuse`): direct mounting of ZIP/7z/RAR, Xbox images and CHD
  files for the portable/AppImage builds. Without it the automatic extraction/conversion
  fallbacks are used; the Flatpak always uses those fallbacks.
- **libasound2**: ALSA output for UI sounds.
- **libsndfile1**: FLAC/Ogg/Opus UI sounds (MP3/WAV are decoded in-process).

## Verified platforms

Linux x64 is verified end-to-end on:

- Ubuntu 24.04 x64 (GNOME/Wayland) and Fedora 44 KDE;
- Linux Mint 22.3 x64 (X11): AppImage, Flatpak and portable tarball launched from the release
  payloads, including Flatpak host-emulator launches through `flatpak-spawn`.

Linux ARM64 is verified on the Azure `linuxarm-test` host (Ubuntu 24.04.5, Neoverse-N2,
2 vCPU):

- `release_5.8.4_linux-arm64.tar.gz` extracted with AArch64 ELF binaries and executable modes,
  then launched under Xvfb; the library window rendered (2026-11-19).
- `SimpleLauncher-5.8.4-linux-arm64.AppImage`, built natively on that host, launched with
  `APPIMAGE_EXTRACT_AND_RUN=1` and rendered the same window.
- The arm64 Flatpak was not run there because the guest has no `flatpak-builder` or
  `org.freedesktop.Sdk//25.08`; CI still builds it and structurally verifies the aarch64
  commit. The release pipeline itself builds on x86_64.

## Building the bundles

The portable ZIP/tarball are produced by `scripts/package-release-linux.ps1`; the
self-contained AppImage and Flatpak bundles by
`scripts/package-linux-native.sh` (Linux build host, both architectures). See
[15 — Development](15-development.md#linux-release-packaging-script).
