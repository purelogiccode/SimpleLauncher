# 11 — Bundled Tools

> The shipped `tools\` payloads, the tool-launcher service, Easy Mode downloads, and the `Tools\` source projects.
> Related: [02 — Projects & Solution](02-projects-and-solution.md) · [10 — Game Scanning](10-game-scanning.md)

## Shipped payloads (`SimpleLauncher\tools\`)

Every tool ships x64 + ARM64 variants (`X.exe` + `X_arm64.exe`) unless noted. All are launched through `ExternalToolLauncherService` (below). The Tools menu is Windows-only **except `XISOStudio`, `CHDStudio` and `RVZStudio`**: the Avalonia app keeps only those items visible off Windows (`MainWindow` hides the other entries on Linux/macOS) because the Linux packaging script prunes every Windows `tools/**/*.exe` and `tools/**/*.dll`. They are the cross-platform successors of the old `BatchConvertIsoToXiso` / `BatchConvertToCHD` / `BatchConvertToRVZ` tools and ship one self-contained payload per OS/architecture (`tools\XISOStudio\<rid>\`, `tools\CHDStudio\<rid>\` and `tools\RVZStudio\<rid>\` — `win-x64` / `win-arm64` with the `.exe` plus `7za.exe` / `7za_arm64.exe` (and, for CHDStudio, `chdman` and the Avalonia/Skia/HarfBuzz native libraries; for RVZStudio, `DolphinTool`), `linux-x64` / `linux-arm64` with the extension-less binary, the 7-Zip helper and the `.so` natives), each with the tool's license files and `ReadMe.md`; the launcher resolves the payload for the running OS/architecture and both packaging scripts keep only the packaged RID's subfolder. `CHDMounter` also ships one payload per OS/architecture (`tools\CHDMounter\<rid>\`) but is not a menu entry — it is the CHD mount helper used by the game-launch flow (see below). The mount services (`MountChdFiles`, `MountXisoFiles`) check the OS before resolving the tool path, so on Linux/macOS they log a single Information-level "not supported on this platform" entry instead of a missing-Windows-tool Warning (LB-14).

| Folder | Executables | Purpose |
|---|---|---|
| `SevenZip` | `7za.exe`, `7za_arm64.exe` (Windows) / `7zz`, `7zz_arm64` (Linux, static 7-Zip 26.03 builds) + `License.txt` | Fallback 7z extraction (SharpCompress failure path) — `ExtractionService` picks the binary for the running OS/architecture and ensures its execute bits on Unix before launching (LB-15) |
| `CHDMounter` | `win-x64` / `win-arm64` (`CHDMounter.exe`) and `linux-x64` / `linux-arm64` (`CHDMounter`) subfolders (+ licenses/README) | Mounts CHD files as virtual drives for emulators without native CHD support — Dokan/WinFsp on Windows, FUSE 3 on Linux (needs `libfuse3` + `/dev/fuse` + the .NET 10 runtime). Used by `MountChdFiles`/`ChdMountStrategy` (not a Tools-menu entry); on Linux the managed CHDSharp conversion is the fallback when FUSE or the runtime is unavailable |
| `SimpleXisoDrive` | `SimpleXisoDrive.exe`, `_arm64.exe` | Mounts XISO images (Dokan); 1.4.0+ also exposes a virtual `image.iso` inside the mount with `--image-iso` (CISO decompressed and ZAR trees synthesized on demand — no extraction) for xemu |
| `SimpleZipDrive` | `SimpleZipDrive.exe`, `_arm64.exe` (+ ReadMe) | Mounts ZIP archives (Dokan) |
| `CHDStudio` | `win-x64` / `win-arm64` (`CHDStudio.exe` + `chdman.exe` / `chdman_arm64.exe` + 7za + Avalonia/Skia/HarfBuzz natives) and `linux-x64` / `linux-arm64` (`CHDStudio` + `7zz` + `.so` natives) subfolders | Convert to CHD (the cross-platform successor of `BatchConvertToCHD`, bundled per OS/architecture; see below). `DiscConverter`, `ConvertChdToIso` and `ConvertChdToCueBin` run the bundled `chdman` from `tools\CHDStudio\win-{x64,arm64}\` as the Windows fallback |
| `RVZStudio` | `win-x64` / `win-arm64` (`RVZStudio.exe` + `DolphinTool.exe` / `DolphinTool_arm64.exe` + 7za) and `linux-x64` / `linux-arm64` (`RVZStudio` + `7za` / `7za_arm64`) subfolders | Convert to RVZ (the cross-platform successor of `BatchConvertToRVZ`, bundled per OS/architecture; see below). `DiscConverter.ConvertToIsoAsync` and `ConvertDiscImageToIso` run the bundled `DolphinTool` from `tools\RVZStudio\win-{x64,arm64}\` |
| `XISOStudio` | `win-x64` / `win-arm64` (`XISOStudio.exe`) and `linux-x64` / `linux-arm64` (`XISOStudio`) subfolders, each with the matching 7za/7zz helper + licenses | ISO ↔ XISO conversion (the cross-platform successor of `BatchConvertIsoToXiso`, bundled per OS/architecture; see below) |
| `BatchConvertToCompressedFile` | `BatchConvertToCompressedFile.exe`, `_arm64.exe`, 7z dlls | Convert to 7z/zip |
| `CreateBatchFilesForPS3Games` | `.exe`, `_arm64.exe` | PS3 game launcher batch files |
| `CreateBatchFilesForScummVMGames` | `.exe`, `_arm64.exe` | ScummVM game launchers |
| `CreateBatchFilesForWindowsGames` | `.exe`, `_arm64.exe` | Windows game launchers |
| `CreateBatchFilesForXbox360XBLAGames` | `.exe`, `_arm64.exe` | Xbox 360 XBLA launchers |
| `FindRomCover` | x64/arm64 subfolders | Cover-art finder tool |
| `RetroGameCoverDownloader` | `.exe`, `_arm64.exe` | Bulk retro cover downloads |
| `RomValidator` | `.exe`, `_arm64.exe`, 7z dlls | ROM validation against No-Intro DAT files |
| `RetroAchievementsSharp` | `RetroAchievementsSharp.exe`, `_arm64.exe` (+ README/LICENSE) | RetroAchievements hashing engine (rcheevos port) — invoked by `RetroAchievementsFileHasher`/`HashScanner`, **not** by `ExternalToolLauncherService` |

## Tool launcher (`ExternalToolLauncherService`)

`SimpleLauncher.Core\Services\ExternalToolLauncher\ExternalToolLauncherService.cs`

- **Arch-aware resolution:** tools under `tools\{Tool}\x64\` / `arm64\` subfolders or `_arm64.exe` suffixes, matching the running architecture.
- **PE validation:** bundled executables are checked for a valid MZ/PE signature before launch.
- Public methods (each shows a "not found" / "canceled" / error box on failure):
  - `CreateBatchFilesForXbox360XblaGamesAsync`, `CreateBatchFilesForWindowsGamesAsync`, `CreateBatchFilesForPs3GamesAsync`, `CreateBatchFilesForScummVmGamesAsync`
  - `LaunchXisoStudioAsync`, `LaunchChdStudioAsync`, `LaunchRvzStudioAsync`, `BatchConvertToCompressedFileAsync`
  - `FindRomCoverLaunchAsync`, `RetroGameCoverDownloaderAsync`, `RomValidatorAsync`
- UAC cancel (error 1223) is handled with a "canceled" message.

## Easy Mode downloads

`EasyModeManager` (Core `Services\EasyMode\`) + `EasyModeWindow` (app) drive the guided setup: a system manifest lists, per system, the recommended emulator, RetroArch core, and image-pack **download links** (fetched from `Urls:EasyModeApi`). Downloads run through `DownloadManager` with progress, retry, and cache (`EasyModeCacheDur*` keys). See [03 — Quickstart](03-quickstart.md#easy-mode).

## `Tools\` source projects (in this repo)

| Project | Purpose |
|---|---|
| `Tools\Mame.DatCreator` | WPF tool that builds `mame.dat` (MessagePack) from MAME `-listxml` + software lists |
| `Tools\RetroAchievements.DataFetcher` | CLI tool that fetches the RA game database into `RetroAchievements.dat` |
| `Tools\XmlToBinaryConverter` | WPF tool that converts `history.xml` ↔ `history.dat` (MessagePack) |

These are **development-time tools** — they are not part of the shipped app payload.

## Related docs

- [06 — Systems & Launch](06-systems-and-launch.md) (how tools are invoked at launch)
- [12 — Data Formats](12-data-formats.md) (mame.dat, history.dat)
- [02 — Projects & Solution](02-projects-and-solution.md)
