# 11 — Bundled Tools

> The shipped `tools\` payloads, the tool-launcher service, Easy Mode downloads, and the `Tools\` source projects.
> Related: [02 — Projects & Solution](02-projects-and-solution.md) · [10 — Game Scanning](10-game-scanning.md)

## Shipped payloads (`SimpleLauncher\tools\`)

Every tool ships x64 + ARM64 variants (`X.exe` + `X_arm64.exe`) unless noted. All are launched through `ExternalToolLauncherService` (below). In `BatchConvertIsoToXiso`, the `bchunk.exe`, `extract-xiso.exe` and `xdvdfs.exe` helpers are x64-only and are invoked by the wrapper (on Windows 11 ARM64 they run under x64 emulation).

| Folder | Executables | Purpose |
|---|---|---|
| `SevenZip` | `7za.exe`, `7za_arm64.exe` | Fallback 7z extraction (SharpCompress failure path) |
| `CHDMounter` | `CHDMounter.exe`, `_arm64.exe` (+ README) | Mounts CHD files as virtual drives for emulators without native CHD support |
| `SimpleXisoDrive` | `SimpleXisoDrive.exe`, `_arm64.exe` | Mounts XISO images (Dokan) |
| `SimpleZipDrive` | `SimpleZipDrive.exe`, `_arm64.exe` (+ ReadMe) | Mounts ZIP archives (Dokan) |
| `BatchConvertToCHD` | `BatchConvertToCHD.exe`, `_arm64.exe`, `chdman.exe`, `chdman_arm64.exe`, 7za | Convert to CHD; `DiscConverter` extracts CHD→ISO/CUE with the managed **CHDSharp** decoder on every platform (Linux/macOS have no `chdman`) and falls back to the bundled `chdman` on Windows |
| `BatchConvertToRVZ` | `BatchConvertToRVZ.exe`, `_arm64.exe`, `DolphinTool.exe`, `_arm64.exe`, 7za | Convert to RVZ; `DolphinTool` is used by `DiscConverter.ConvertToIsoAsync` |
| `BatchConvertIsoToXiso` | `BatchConvertIsoToXiso.exe`, `_arm64.exe`, `bchunk.exe`, `extract-xiso.exe`, 7za | ISO ↔ XISO conversion |
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
  - `BatchConvertIsoToXisoAsync`, `BatchConvertToChdAsync`, `BatchConvertToRvzAsync`, `BatchConvertToCompressedFileAsync`
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
