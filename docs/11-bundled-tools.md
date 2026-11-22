# 11 - Bundled Tools

> The shipped `tools\` helper payloads, the path resolver, and the `Tools\` source projects.
> Related: [02 - Projects & Solution](02-projects-and-solution.md) · [10 - Game Scanning](10-game-scanning.md)

## Shipped helper payloads (`SimpleLauncher\tools\`)

Simple Launcher no longer bundles end-user GUI applications. The release ships only the
helpers that the launcher itself calls while mounting images, extracting archives and
calculating RetroAchievements hashes. The **Tools menu has been removed** from the app.

| Folder | Executables | Purpose |
|---|---|---|
| `SevenZip` | `7za.exe`, `7za_arm64.exe` (Windows) / `7zz`, `7zz_arm64` (Linux, static 7-Zip 26.06 builds) + `License.txt` | Fallback 7z extraction (SharpCompress failure path) - `ExtractionService` picks the binary for the running OS/architecture and ensures its execute bits on Unix before launching (LB-15) |
| `CHDMounter` | `win-x64` / `win-arm64` (`CHDMounter.exe`) and `linux-x64` / `linux-arm64` (`CHDMounter`) subfolders (+ licenses/README), version 1.4.0 | Mounts CHD files as virtual drives for emulators without native CHD support - Dokan/WinFsp on Windows, FUSE 3 on Linux (needs `libfuse3` + `/dev/fuse`). It targets .NET 10, but the launcher starts it with `DOTNET_ROLL_FORWARD=LatestMajor` so it runs on the app's .NET 11 runtime. Used by `MountChdFiles`/`ChdMountStrategy`; on Linux the managed CHDSharp conversion is the fallback when FUSE or the runtime is unavailable |
| `SimpleXisoDrive` | `win-x64` / `win-arm64` (`SimpleXisoDrive.exe`) and `linux-x64` / `linux-arm64` (`SimpleXisoDrive`) subfolders (+ licenses/README), version 1.6.0 | Mounts Xbox images - `.iso` / `.xiso`, CISO (`.cso`), Xbox-ISO CHD and ZArchive (`.zar`) - through Dokan (Windows) / FUSE 3 (Linux); `--image-iso` also exposes a virtual `image.iso` inside the mount for xemu. It targets .NET 10 and is launched with `DOTNET_ROLL_FORWARD=LatestMajor` so the app's .NET 11 runtime can host it. Used by `MountXisoFiles` - `XisoMountStrategy`, `XemuMountStrategy` and `ChdMountStrategy` |
| `SimpleZipDrive` | `win-x64` / `win-arm64` (`SimpleZipDrive.exe` + `7za.exe` + `winfsp-msil.dll` + Skia/HarfBuzz natives) and `linux-x64` / `linux-arm64` (`SimpleZipDrive` + `7zzs` + `.so` natives) subfolders (+ licenses/README), version 3.1.3 with the 7-Zip 26.06 helpers | Mounts ZIP/7Z/RAR/TAR archives (and Xbox images) through Dokan/WinFsp on Windows and FUSE 3 on Linux. Used by `MountZipFiles` (RPCS3/ScummVM/XBLA archive launches); on Linux the managed temp extraction remains the fallback when FUSE or the payload is unavailable |
| `RetroAchievementsSharp` | `win-x64` / `win-arm64` (`RetroAchievementsSharp.exe`) and `linux-x64` / `linux-arm64` (`RetroAchievementsSharp`) subfolders (+ README/LICENSE), version 1.1.1 | RetroAchievements hashing engine (rcheevos port) - invoked by `RetroAchievementsFileHasher`/`HashScanner` |

The per-RID helpers resolve `tools\{Tool}\{rid}\<binary>` for the running OS/architecture;
both packaging scripts keep only the packaged RID's subfolder, the Linux script marks the
ELF binaries executable, and macOS ships no payloads (the services fall back to extraction or
report the platform as unsupported). Resolution lives in
`SimpleLauncher.Core\Services\BundledTools\BundledToolPathResolver.cs`.

## Platform availability

The app resolves helper payloads only for Windows and Linux.
`BundledToolPathResolver.GetPlatformRuntimeIdentifier()` has no macOS branch, and
`scripts/package-release-macos.ps1` removes `tools/` from the DMG.

| Helper | Windows | Linux | macOS |
|---|---|---|---|
| `SevenZip` | x64, ARM64 | x64, ARM64 | not bundled |
| `CHDMounter` | x64, ARM64 | x64, ARM64 | not bundled |
| `SimpleXisoDrive` | x64, ARM64 | x64, ARM64 | not bundled |
| `SimpleZipDrive` | x64, ARM64 | x64, ARM64 | not bundled |
| `RetroAchievementsSharp` | x64, ARM64 | x64, ARM64 | not bundled |

Turning on another platform means changing the per-RID folder layout, the packaging/pruning
scripts (including Unix execute bits), and the resolver tests together.

## Optional companion apps

These GUI tools were previously bundled and are now distributed separately from their own
repositories/releases:

- CHDStudio
- XISOStudio
- RVZStudio
- Batch Convert to Compressed File
- Find Rom Cover
- Retro Game Cover Downloader
- ROM Validator
- Create Batch Files for PS3, ScummVM, Windows and Xbox 360 XBLA games

Upgraded installations remove their old `tools\` folders automatically
(`CleanSimpleLauncherFolderService`). Disc conversion no longer depends on bundled helper
executables: `DiscConverter` uses the managed CHDSharp and PBPSharp libraries.

## Easy Mode downloads

`EasyModeManager` (Core `Services\EasyMode\`) + `EasyModeWindow` (app) drive the guided setup: a
system manifest lists, per system, the recommended emulator, RetroArch core, and image-pack
**download links** (fetched from `Urls:EasyModeApi`). Downloads run through `DownloadManager`
with progress, retry, and cache (`EasyModeCacheDur*` keys). See
[03 - Quickstart](03-quickstart.md#easy-mode).

## `Tools\` source projects (in this repo)

| Project | Purpose |
|---|---|
| `Tools\Mame.DatCreator` | WPF tool that builds `mame.dat` (MessagePack) from MAME `-listxml` + software lists |
| `Tools\RetroAchievements.DataFetcher` | CLI tool that fetches the RA game database into `RetroAchievements.dat` |
| `Tools\XmlToBinaryConverter` | WPF tool that converts `history.xml` → `history.dat` (MessagePack) |

These are **development-time tools** - they are not part of the shipped app payload.

## Related docs

- [06 - Systems & Launch](06-systems-and-launch.md) (how tools are invoked at launch)
- [12 - Data Formats](12-data-formats.md) (mame.dat, history.dat)
- [02 - Projects & Solution](02-projects-and-solution.md)
