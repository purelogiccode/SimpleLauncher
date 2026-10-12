[![Platform: Windows | Linux | macOS](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078d7.svg)](#requirements)
[![Windows 10+](https://img.shields.io/badge/Windows-10%2B-0078d7?logo=windows&logoColor=white)](#requirements)
[![Linux](https://img.shields.io/badge/Linux-x64%20%7C%20ARM64-FCC624?logo=linux&logoColor=black)](#requirements)
[![macOS](https://img.shields.io/badge/macOS-Intel%20%7C%20Apple%20silicon-000000?logo=apple&logoColor=white)](#requirements)
[![Architecture: x64 | ARM64](https://img.shields.io/badge/Architecture-x64%20%7C%20ARM64-6f42c1)](#requirements)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512bd4.svg?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![UI: Avalonia](https://img.shields.io/badge/UI-Avalonia-8b44ac)](https://github.com/AvaloniaUI/Avalonia)
[![Mount core: Dokan](https://img.shields.io/badge/mount%20core-Dokan-1f6feb)](https://github.com/dokan-dev/dokany)
[![Mount core: WinFsp](https://img.shields.io/badge/mount%20core-WinFsp-2ea44f)](https://github.com/winfsp/winfsp)
[![Mount core: FUSE 3](https://img.shields.io/badge/mount%20core-FUSE%203-orange)](https://github.com/libfuse/libfuse)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.txt)
[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/CHDMounter?label=release)](https://github.com/purelogiccode/CHDMounter/releases)
[![Release date](https://img.shields.io/github/release-date/purelogiccode/CHDMounter)](https://github.com/purelogiccode/CHDMounter/releases)
[![Downloads](https://img.shields.io/github/downloads/purelogiccode/CHDMounter/total)](https://github.com/purelogiccode/CHDMounter/releases)
[![CI](https://github.com/purelogiccode/CHDMounter/actions/workflows/ci.yml/badge.svg)](https://github.com/purelogiccode/CHDMounter/actions/workflows/ci.yml)
[![Release](https://github.com/purelogiccode/CHDMounter/actions/workflows/release.yml/badge.svg)](https://github.com/purelogiccode/CHDMounter/actions/workflows/release.yml)
[![Docs build](https://github.com/purelogiccode/CHDMounter/actions/workflows/docs.yml/badge.svg)](https://github.com/purelogiccode/CHDMounter/actions/workflows/docs.yml)
[![Documentation](https://img.shields.io/badge/Docs-purelogiccode.github.io-2ea44f?logo=readthedocs)](https://purelogiccode.github.io/CHDMounter/)
[![Wiki](https://img.shields.io/badge/Wiki-GitHub-6f42c1?logo=github)](https://github.com/purelogiccode/CHDMounter/wiki)
[![Changelog](https://img.shields.io/badge/Changelog-WhatsNew.md-blue)](WhatsNew.md)
[![GitHub issues](https://img.shields.io/github/issues/purelogiccode/CHDMounter)](https://github.com/purelogiccode/CHDMounter/issues)
[![GitHub pull requests](https://img.shields.io/github/issues-pr/purelogiccode/CHDMounter)](https://github.com/purelogiccode/CHDMounter/pulls)
[![GitHub stars](https://img.shields.io/github/stars/purelogiccode/CHDMounter?style=flat)](https://github.com/purelogiccode/CHDMounter/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/purelogiccode/CHDMounter?style=flat)](https://github.com/purelogiccode/CHDMounter/network/members)
[![Contributors](https://img.shields.io/github/contributors/purelogiccode/CHDMounter)](https://github.com/purelogiccode/CHDMounter/graphs/contributors)
[![Last commit](https://img.shields.io/github/last-commit/purelogiccode/CHDMounter/master)](https://github.com/purelogiccode/CHDMounter/commits/master)
[![Commit activity](https://img.shields.io/github/commit-activity/m/purelogiccode/CHDMounter)](https://github.com/purelogiccode/CHDMounter/commits/master)
[![Code size](https://img.shields.io/github/languages/code-size/purelogiccode/CHDMounter)](https://github.com/purelogiccode/CHDMounter)
[![Top language](https://img.shields.io/github/languages/top/purelogiccode/CHDMounter)](https://github.com/purelogiccode/CHDMounter)
[![Maintained](https://img.shields.io/badge/Maintained-yes-2ea44f)](https://github.com/purelogiccode/CHDMounter)

# CHDMounter

Mount CHD (Compressed Hunks of Data) CD/DVD images as virtual read-only drives on Windows, Linux and macOS. Supports 37 console/format modes (console types plus generic CUE/ISO/BIN/RAW formats), selectable by CLI alias. Choose the mount core in Settings: **Dokan** (default) or **WinFsp** on Windows, **FUSE** (libfuse3 / macFUSE) on Linux and macOS.

![](screenshot.png)

> **Documentation:** [Wiki](https://github.com/purelogiccode/CHDMounter/wiki) &middot;
> [Documentation site](https://purelogiccode.github.io/CHDMounter/) &middot; source of both lives in
> [`docs/`](docs/index.md).

---

## Features

- **Mount CHD files as virtual drives** using [Dokan](https://github.com/dokan-dev/dokany) or [WinFsp](https://winfsp.dev/) on Windows, and [FUSE 3](https://github.com/libfuse/libfuse) (libfuse3 / macFUSE) on Linux and macOS
- **Selectable mount core in Settings** — Dokan (default) or WinFsp on Windows; FUSE on Linux/macOS
- **37 console/format modes** — console types (ISO 9660, UDF, XDVDFS (Xbox), OperaFS (3DO), CD-i Green Book, HFS/HFS+ (Pippin), and more) plus generic CUE/ISO/BIN/WAV export modes
- **Virtual CUE/BIN/ISO/WAV export** — 8 disc image export modes (2352/2048 × CUE/ISO, CUE/ISO/WAV, CUE/BIN, CUE/BIN/WAV) for emulators and burning tools
- **SingleFile ISO passthrough** — single `image.iso` file for emulators that expect raw ISO (xemu, RPCS3)
- **Read-only** — never modifies your CHD files
- **Automatic filesystem detection by track header** — no manual setup needed for most discs
- **Command-line interface** for scripting and frontend integration
- **Avalonia dark theme UI** (cross-platform) with console type dropdown, real-time log output, settings dialog
- **Settings with DPAPI encryption on Windows** (plain JSON elsewhere) — persisted across sessions
- **Update checker** — polls GitHub releases for new versions
- **Serilog** structured logging to file and debug output
- **Single-file framework-dependent publish** — one executable per bundle (requires the .NET 10 runtime)
- **x64 and ARM64** support on Windows, Linux and macOS

---

## Supported Filesystems

| Console / Format     | CLI Alias       | Filesystem              | Notes                                              |
|----------------------|-----------------|-------------------------|----------------------------------------------------|
| 3DO                  | `3do`           | OperaFS                 | Block-based directory chain, ISO 9660 fallback     |
| Amiga CD             | `amigacd`       | ISO 9660                |                                                    |
| Amiga CD32           | `amigacd32`     | ISO 9660                |                                                    |
| Amiga CDTV           | `amigacdtv`     | ISO 9660                |                                                    |
| CD-i                 | `cdi`           | Green Book              | Interleaved stream support, Path Table             |
| CUE/BIN RAW 2048     | `cuebin2048`    | Virtual                 | CUE + BIN at 2048 bytes/sector                     |
| CUE/BIN RAW 2352     | `cuebin2352`    | Virtual                 | CUE + BIN at 2352 bytes/sector                     |
| CUE/BIN/WAV RAW 2048 | `cuebinwav2048` | Virtual                 | CUE + BIN + WAV audio tracks (2048-byte data)      |
| CUE/BIN/WAV RAW 2352 | `cuebinwav2352` | Virtual                 | CUE + BIN + WAV audio tracks (2352-byte data)      |
| CUE/ISO RAW 2048     | `cueiso2048`    | Virtual                 | CUE sheet + ISO at 2048 bytes/sector               |
| CUE/ISO RAW 2352     | `cueiso2352`    | Virtual                 | CUE sheet + ISO at 2352 bytes/sector               |
| CUE/ISO/WAV RAW 2048 | `cueisowav2048` | Virtual                 | CUE + ISO + WAV audio tracks (2048-byte data)      |
| CUE/ISO/WAV RAW 2352 | `cueisowav2352` | Virtual                 | CUE + ISO + WAV audio tracks (2352-byte data)      |
| FM Towns             | `fmtowns`       | ISO 9660                |                                                    |
| ISO 9660             | `iso9660`       | ISO 9660 / High Sierra  | Joliet/UTF-16BE, SUSP/Rock Ridge                   |
| ISO RAW 2048         | `isoraw2048`    | Raw sectors             | Entire CHD as single `image.iso` (2048-byte units) |
| ISO RAW 2352         | `isoraw2352`    | Raw sectors             | Entire CHD as single `image.iso` (2352-byte units) |
| Neo Geo CD           | `neogeocd`      | ISO 9660                |                                                    |
| Nuon                 | `nuon`          | UDF → ISO 9660 fallback | VM Labs Nuon DVD                                   |
| PC Engine CD         | `pcengine`      | ISO 9660                | Boot signature scan, fallback raw track files      |
| PC-98                | `pc98`          | ISO 9660                |                                                    |
| PC-FX                | `pcfx`          | PC-FX ISO               | Dedicated byte-offset VD scanner                   |
| Pico                 | `pico`          | ISO 9660                | Sega Pico                                          |
| Pippin               | `pippin`        | HFS → HFS+ → UDF → ISO  | Apple Bandai Pippin                                |
| PlayStation (Auto)   | `psauto`        | ISO 9660                | Auto-detect PS1/PS2/PS3/PSP                        |
| PS1                  | `ps1`           | ISO 9660                | Track-level parsing, aggressive PVD scan           |
| PS2                  | `ps2`           | ISO 9660                | CD/DVD auto-detection                              |
| PS3                  | `ps3`           | UDF → ISO 9660 fallback | Multi-extent large file support                    |
| PS3 ISO RAW 2352     | `isoraw2352`    | Virtual ISO passthrough | Single `image.iso` for RPCS3                       |
| PSP                  | `psp`           | ISO 9660                | UMD image support                                  |
| Sega Dreamcast       | `segadreamcast` | ISO 9660                | GD-ROM offset search (-45000, -150, 0, etc.)       |
| Sega Genesis         | `segagenesis`   | ISO 9660                |                                                    |
| Sega Saturn          | `segasaturn`    | ISO 9660                |                                                    |
| X68000               | `x68000`        | ISO 9660 → UDF fallback |                                                    |
| Xbox                 | `xbox`          | XDVDFS                  | Binary tree directory structure                    |
| Xbox 360             | `xbox360`       | XDVDFS                  | XGD2/XGD3 offset detection                         |
| Xbox ISO RAW 2352    | `isoraw2352`    | Virtual ISO passthrough | Single `image.iso` for xemu                        |

---

## Requirements

- **.NET 10.0 runtime** — required; the download bundles are framework-dependent and do not embed the runtime ([download](https://dotnet.microsoft.com/download/dotnet/10.0)). On Windows use the .NET 10 Desktop Runtime; on Linux/macOS install the matching .NET 10 runtime.
- **Windows 10 or later** (x64 or ARM64) with one of:
  - **Dokan** — the default mount core ([install Dokany](https://github.com/dokan-dev/dokany/releases)), or
  - **WinFsp** — selectable in Settings → Mount core ([download WinFsp](https://github.com/winfsp/winfsp/releases))
- **Linux** (x64 or ARM64) with **libfuse3** (for example `sudo apt install libfuse3-3 fuse3`) and `/dev/fuse` available
- **macOS** (Intel or Apple silicon) with **[macFUSE](https://macfuse.io)** installed

> On Windows only one driver is needed. Dokan is used by default; switch to WinFsp at any time in Settings → Mount core. On Linux and macOS the FUSE core is always used.

---

## Quick Start

### Download

Grab the latest bundle from [Releases](https://github.com/purelogiccode/CHDMounter/releases):

| Bundle | Platform | Architecture |
|--------|----------|--------------|
| `release_<version>_win-x64.zip` | Windows | x64 |
| `release_<version>_win-arm64.zip` | Windows | ARM64 |
| `release_<version>_linux-x64.zip` | Linux | x64 |
| `release_<version>_linux-arm64.zip` | Linux | ARM64 |
| `release_<version>_osx-x64.zip` | macOS | Intel |
| `release_<version>_osx-arm64.zip` | macOS | Apple silicon |

Every bundle contains `CHDMounter` (`CHDMounter.exe` on Windows), `LICENSE.txt`, `README.md` and
`WhatsNew.md`, and requires the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0).
Extract the bundle anywhere and run `CHDMounter` — no installation required.

> The `CHDMounter_Tester` batch-parsing/benchmarking tool is no longer published in releases.

### GUI Mode

```
CHDMounter.exe
```

Opens the main window. Click **Browse** to select a CHD file, pick a filesystem type from the dropdown, and click **Mount**. The drive appears in your file manager. The mount core (Dokan/WinFsp/FUSE) is chosen in **File → Settings**.

### Command Line

```
CHDMounter.exe [/l] [/a] [/s:<alias>] <chd_file> [mount_point]
```

| Argument | Description |
|----------|-------------|
| `/l` | Launch Explorer after mount |
| `/a` | Auto-select drive letter |
| `/s:<alias>` | Select console system by alias (e.g. `ps2`, `cuebin2352`) |
| `<chd_file>` | Path to the .chd file (required) |
| `[mount_point]` | Drive letter/path for mount (optional, auto-picks if omitted). On Linux/macOS this is a directory path |

**Notes:**
- Arguments can be in any order
- Flags are prefixed with `/`
- A console alias may also be passed as a positional argument (e.g. `CHDMounter.exe game.chd ps2`)
- If only `<chd_file>` is provided without flags, a dialog appears asking you to choose

### Console Type Reference

You can specify the console type using a **string alias** (case-insensitive). Numeric indexes are not supported — the alias list below is defined in the [VideoGameFileSystemParser](https://github.com/drpetersonfernandes/VideoGameFileSystemParser) library and is the single source of truth.

| Console / Format     | CLI Alias       |
|----------------------|-----------------|
| 3DO                  | `3do`           |
| Amiga CD             | `amigacd`       |
| Amiga CD32           | `amigacd32`     |
| Amiga CDTV           | `amigacdtv`     |
| CD-i                 | `cdi`           |
| CUE/BIN RAW 2048     | `cuebin2048`    |
| CUE/BIN RAW 2352     | `cuebin2352`    |
| CUE/BIN/WAV RAW 2048 | `cuebinwav2048` |
| CUE/BIN/WAV RAW 2352 | `cuebinwav2352` |
| CUE/ISO RAW 2048     | `cueiso2048`    |
| CUE/ISO RAW 2352     | `cueiso2352`    |
| CUE/ISO/WAV RAW 2048 | `cueisowav2048` |
| CUE/ISO/WAV RAW 2352 | `cueisowav2352` |
| FM Towns             | `fmtowns`       |
| ISO 9660             | `iso9660`       |
| ISO RAW 2048         | `isoraw2048`    |
| ISO RAW 2352         | `isoraw2352`    |
| Neo Geo CD           | `neogeocd`      |
| Nuon                 | `nuon`          |
| PC Engine CD         | `pcengine`      |
| PC-98                | `pc98`          |
| PC-FX                | `pcfx`          |
| Pico                 | `pico`          |
| Pippin               | `pippin`        |
| PlayStation (Auto)   | `psauto`        |
| PS1                  | `ps1`           |
| PS2                  | `ps2`           |
| PS3                  | `ps3`           |
| PS3 ISO RAW 2352     | `isoraw2352`    |
| PSP                  | `psp`           |
| Sega Dreamcast       | `segadreamcast` |
| Sega Genesis         | `segagenesis`   |
| Sega Saturn          | `segasaturn`    |
| X68000               | `x68000`        |
| Xbox                 | `xbox`          |
| Xbox 360             | `xbox360`       |
| Xbox ISO RAW 2352    | `isoraw2352`    |

**Examples:**
```bash
# Mount a PS2 game as drive M:
CHDMounter.exe /s:ps2 game.chd M:

# Mount an Xbox 360 game (auto-select drive letter)
CHDMounter.exe game.chd xbox360

# Mount a Dreamcast game and launch Explorer
CHDMounter.exe /l /s:segadreamcast game.chd

# Mount as virtual CUE/BIN (2352-byte sectors)
CHDMounter.exe /s:cuebin2352 disc.chd

# Mount with generic ISO 9660 parser
CHDMounter.exe /s:iso9660 data.chd N:

# Mount with console alias as a positional argument
CHDMounter.exe game.chd ps3
```

If no console type is specified, a dialog appears asking you to choose.

## Build from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10+, Linux (x64/ARM64) or macOS (Intel/Apple silicon)

```bash
# Build everything (Windows)
dotnet build CSharp_CHDMounter.sln

# Build just the app (any platform)
dotnet build CHDMounter/CHDMounter.csproj

# Pack the release bundles for the current platform
pwsh ./build/pack-release.ps1 -Version 2.0.0 -Runtime win-x64
```

---

## How It Works

### CHD Reading

CHD (Compressed Hunks of Data) is MAME's lossless compression format for CD/DVD/HDD images. CHDMounter uses [CHDSharp](https://github.com/drpetersonfernandes/CHDSharp) to read all five CHD versions (V1-V5) and all compression codecs:

- **General**: zlib (deflate), LZMA, FLAC (headerless, 16-bit stereo), dynamic Huffman, Zstd
- **CD-sector**: CDZL (zlib), CDLZ (LZMA), CDFL (FLAC), CDZS (Zstd) — with ECC regeneration
- **AV**: AVHuff — audio+video Huffman for laserdisc/arcade captures

### Sector Reading

The `SectorReader` maps logical block addresses (LBAs) to byte offsets within CHD hunks. It:

1. Parses track metadata (`CHT2`/`CHTR`/`CHGD` tags) for multi-track CD images
2. Maps LBA to CHD frame number using track offsets (handles GD-ROM 45000-LBA shift)
3. Reads and caches the compressed hunk via `ReadHunk()`
4. Detects sector mode (Mode 1 = 16-byte header, Mode 2 = 24-byte header) by scanning for the CD sync pattern `00 FF×10 00`
5. Strips headers to deliver 2048 bytes of user data per sector

### File System Parsing

CHDMounter uses the [VideoGameFileSystemParser](https://github.com/drpetersonfernandes/VideoGameFileSystemParser) NuGet package (v1.4.0) to parse various console file systems from raw sectors and reconstruct directory trees. The console alias registry lives in the library and is the single source of truth for console selection.

Supported file systems include ISO 9660 (with Joliet/Rock Ridge), XDVDFS (Xbox), OperaFS (3DO), CD-i Green Book, UDF, and HFS/HFS+.

### VFS Operations

The `ChdContainer` bridges the parsed `FsNode` tree to the driver-specific VFS layer:

- **Dokan**: `ChdFs` implements `IDokanOperations` — `CreateFile` resolves paths, `ReadFile` maps file offsets to sector reads, `FindFiles` lists directories, `GetVolumeInformation` reports "CHDFS" read-only volume.
- **WinFsp**: `ChdFs` extends `FileSystemBase` — `Open` resolves paths and returns `FileEntry` as both file node and descriptor, `Read` copies sector data into native memory via `Marshal.Copy`, `ReadDirectoryEntry` enumerates children with `.` and `..`.
- **FUSE**: `ChdFuseVolume` implements the `IFuseVolume` contract of the bundled `FuseSharp` layer — `GetEntry`/`GetFolderList` map POSIX paths to container entries and `ReadFile` copies sector data into the FUSE read buffer. `FuseMountService` runs the blocking FUSE session on a background thread.

For CUE/BIN/ISO/WAV modes, the container generates virtual entries dynamically:
- The `.cue` file contains standard `FILE`, `TRACK`, and `INDEX` descriptors built from CHD track metadata.
- The `.bin`/`.iso` file maps reads to raw sector data (2352 or 2048 bytes per sector).
- `.wav` files are generated per audio track with standard WAV headers.

For SingleFile mode, the container serves the entire decompressed CHD image as a single `image.iso` file via `CHDFile.Read()`.

---

## Mount Cores

| Aspect | Dokan (Windows, default) | WinFsp (Windows) | FUSE (Linux/macOS) |
|--------|--------------------------|------------------|--------------------|
| Driver | Kernel-mode (Dokan.sys) | User-mode (WinFsp) | libfuse3 / macFUSE |
| NuGet | `DokanNet` 2.3.0.3 | `winfsp.net` 2.1.25156 | bundled `FuseSharp` |
| FileSystem base | `IDokanOperations` | `FileSystemBase` | `IFuseVolume` |
| Admin mount | Standard | Cross-integrity folder mounts with permissive DACL | Standard (mount points are folders) |

All cores share the same functionality and UI. Select one in **Settings → Mount core**; on Linux and macOS FUSE is always used.

---

## Tester Application (CHDMounter_Tester)

> **Not released.** Starting with version 1.2.1 the Tester is no longer published in release
> bundles. It remains in the source tree for developers.

WPF desktop application (Windows only, developer tool) for **batch testing and benchmarking** CHD disc image parsing. Scans folders of `.chd` files, parses each one with a selected console file system parser, and generates summary reports with PDF export.

### Features

- Select a folder of CHD files and a target console type
- Batch-parse every `.chd` file in the folder
- Report success/failure, file count, directory count, volume size, and timing per file
- View aggregated summary statistics (fastest, slowest, average, total throughput)
- Export results to **PDF** with QuestPDF

### Usage

1. Launch `CHDMounter_Tester.exe`
2. Select a folder containing `.chd` files
3. Choose a console type from the dropdown
4. Click **Run Tests** to begin batch parsing
5. Click **Export PDF** to save results

---

## Dependencies

### NuGet Packages

| Package              | Version   | Purpose                                   |
|----------------------|-----------|-------------------------------------------|
| `Avalonia` / `Avalonia.Desktop` | 12.1.4 | Cross-platform UI framework       |
| `Avalonia.Themes.Fluent` | 12.1.4 | Fluent dark theme                     |
| `Avalonia.Fonts.Inter` | 12.1.4  | Bundled UI font fallback                  |
| `CHDSharp`           | 1.4.3     | MAME CHD format reader                    |
| `coverlet.collector` | 10.1.0    | Code coverage (Tests only)                |
| `DokanNet`           | 2.3.0.3   | Dokan virtual filesystem driver bindings  |
| `LibFuseSharp` | 1.0.0 | FUSE 3 high-level mount library ([source](https://github.com/purelogiccode/LibFuseSharp)) |
| `Microsoft.NET.Test.Sdk` | 18.10.1 | Test SDK (Tests only)                    |
| `QuestPDF`           | 2026.9.1  | PDF report generation (Tester only)       |
| `Serilog`            | 4.4.0     | Structured logging                        |
| `Serilog.Sinks.Debug`| 3.0.0     | Visual Studio debug output logging        |
| `Serilog.Sinks.File` | 7.0.0     | File-based log output                     |
| `System.Security.Cryptography.ProtectedData` | 10.0.12 | DPAPI settings encryption (Windows) |
| `VideoGameFileSystemParser` | 1.4.0 | Console filesystem parser (NuGet package) |
| `winfsp.net`         | 2.1.25156 | WinFsp virtual filesystem driver bindings |
| `xUnit`              | 2.9.3     | Test framework (Tests only)               |
| `xunit.runner.visualstudio` | 4.0.1 | VS test adapter (Tests only)              |

---

## Acknowledgments

- **Avalonia** — https://github.com/AvaloniaUI/Avalonia
- **CHDSharp** — https://github.com/drpetersonfernandes/CHDSharp
- **DokanNet** — https://github.com/dokan-dev/dokan-dotnet
- **FUSE / FuseSharp** — https://github.com/libfuse/libfuse
- **VideoGameFileSystemParser** — https://github.com/drpetersonfernandes/VideoGameFileSystemParser
- **MAME** — https://github.com/mamedev/mame
- **WinFsp** — https://github.com/winfsp/winfsp

---

## License

This project is licensed under the GPLv3 License – see the [LICENSE](LICENSE.txt) file for details.