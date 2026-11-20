# Simple Xiso Drive

[![CI](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/ci.yml/badge.svg)](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/ci.yml)
[![Release](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/release.yml/badge.svg)](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/purelogiccode/SimpleXisoDrive?display_name=release&label=latest)](https://github.com/purelogiccode/SimpleXisoDrive/releases)
[![Downloads](https://img.shields.io/github/downloads/purelogiccode/SimpleXisoDrive/total?label=downloads)](https://github.com/purelogiccode/SimpleXisoDrive/releases)
[![Stars](https://img.shields.io/github/stars/purelogiccode/SimpleXisoDrive?style=flat&logo=github)](https://github.com/purelogiccode/SimpleXisoDrive/stargazers)
[![Issues](https://img.shields.io/github/issues/purelogiccode/SimpleXisoDrive?logo=github)](https://github.com/purelogiccode/SimpleXisoDrive/issues)
[![Last commit](https://img.shields.io/github/last-commit/purelogiccode/SimpleXisoDrive?logo=git)](https://github.com/purelogiccode/SimpleXisoDrive/commits/master)
[![License](https://img.shields.io/badge/license-GPL--3.0-blue.svg)](#license)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
![Windows](https://img.shields.io/badge/Windows-x64_%7C_ARM64-0078D6?logo=windows&logoColor=white)
![Linux](https://img.shields.io/badge/Linux-x64_%7C_ARM64-FCC624?logo=linux&logoColor=black)
![macOS](https://img.shields.io/badge/macOS-x64_%7C_ARM64-000000?logo=apple&logoColor=white)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-2ea44f?logo=githubpages&logoColor=white)](https://purelogiccode.github.io/SimpleXisoDrive/)
[![Wiki](https://img.shields.io/badge/docs-GitHub%20Wiki-0366d6?logo=github&logoColor=white)](https://github.com/purelogiccode/SimpleXisoDrive/wiki)
[![LibFuseSharp](https://img.shields.io/nuget/v/LibFuseSharp.svg?label=LibFuseSharp)](https://www.nuget.org/packages/LibFuseSharp/)
[![NuGet downloads](https://img.shields.io/nuget/dt/LibFuseSharp.svg?label=NuGet%20downloads)](https://www.nuget.org/packages/LibFuseSharp/)
[![Security policy](https://img.shields.io/badge/security-policy-blue)](https://github.com/purelogiccode/SimpleXisoDrive/security/policy)
[![CodeQL](https://img.shields.io/badge/CodeQL-enabled-2ea44f)](https://github.com/purelogiccode/SimpleXisoDrive/security/code-scanning)
[![Dependabot](https://img.shields.io/badge/dependabot-enabled-025E8C?logo=dependabot&logoColor=white)](https://github.com/purelogiccode/SimpleXisoDrive/security/dependabot)
[![Forks](https://img.shields.io/github/forks/purelogiccode/SimpleXisoDrive?style=flat&logo=github)](https://github.com/purelogiccode/SimpleXisoDrive/forks)
[![Contributors](https://img.shields.io/github/contributors/purelogiccode/SimpleXisoDrive?style=flat)](https://github.com/purelogiccode/SimpleXisoDrive/graphs/contributors)
[![Commits per month](https://img.shields.io/github/commit-activity/m/purelogiccode/SimpleXisoDrive?label=commits%2Fmonth)](https://github.com/purelogiccode/SimpleXisoDrive/commits/master)
[![Top language](https://img.shields.io/github/languages/top/purelogiccode/SimpleXisoDrive?logo=csharp&logoColor=white)](https://github.com/purelogiccode/SimpleXisoDrive)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/purelogiccode/SimpleXisoDrive/pulls)

Simple Xiso Drive is a lightweight utility that allows you to mount original Xbox ISO files (`.iso`, `.xiso`, `.cso`, `.cci`), Xbox ISO CHD files (`.chd`), ZArchive (`.zar`) files and Xbox 360 GOD/SVOD containers as read-only virtual drives or directory mount points. Built on Dokan for Windows and FUSE 3 for Linux and macOS, it provides high-performance, read-only access to Xbox Disc Video File System (XDVDFS) contents directly from your file manager.

The application is designed for extreme memory efficiency and supports **Windows x64/ARM64**, **Linux x64/ARM64** and **macOS x64/ARM64**.

![Console](Screenshot.png)

## Features

*   **Cross-Platform:** Native executables for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`.
*   **Broad Format Support:** Handles standard Xbox ISO dumps (Sector 32), rebuilt "XISO" formats (Sector 0), Dual-Layer/Hybrid discs (Game Partition offsets), CISO-compressed images (`.cso`, including split `.1.cso` part sets), and CCI/CCIM images (`.cci`, including split sets with multi-digit parts) through XISOSharp's container detection (CCISharp-backed).
*   **Xbox ISO CHD Support:** Mounts Xbox ISO images stored as CHD (`.chd`) — any CHD format version and codec (zlib, lzma, huffman, flac, zstd) — with on-demand hunk decompression via the CHDSharp library. CD and GD-ROM CHDs are rejected; the decompressed image must contain an XDVDFS filesystem.
*   **ZArchive Support:** Mounts `.zar` archives directly — either the archived game tree or a single embedded XISO image — with on-demand zstd decompression (no extraction or temp files).
*   **GOD/SVOD Support:** Mounts Xbox 360 Games-on-Demand containers (a content-hash header beside its `.data` fragment directory) through XISOSharp/StfsSharp, with on-demand fragment mapping. Pass the header file, the `.data` directory, or the parent folder.
*   **Virtual `image.iso`:** The `--image-iso` option also exposes the decompressed Xbox image as `image.iso` at the mount root, for emulators that only accept a disc image (such as xemu). CISO, CCI, CHD and GOD/SVOD images are decompressed or mapped on demand; a ZArchive directory tree is synthesized into an XISO in memory with XISOSharp's layout primitives — nothing is extracted to disk.
*   **Zero-Config Mounting:** On Windows, drag-and-drop an ISO/XISO/CISO/CCI, CHD, ZAR or GOD/SVOD container onto the executable to automatically mount it to the first available drive letter (M: through R:).
*   **Flexible Mount Points:** Mount ISOs as Windows drive letters (e.g., `Z:`) or NTFS folders, and as any directory on Linux and macOS.
*   **Automated Bug Reporting:** Includes a built-in telemetry system that securely reports filesystem crashes to the developer via the PureLogic Code API.
*   **Update Checker:** Checks for newer versions on GitHub at startup and offers to open the release page; a missing Dokan runtime or driver also offers the Dokan download page.
*   **Read-Only Safety:** Ensures the source ISO or ZAR remains unmodified.

## Install dependencies

The release bundles are **framework-dependent single-file executables**, so the .NET runtime and the
mount driver for your platform must be installed before the app can run.

### 1. Install the .NET 10.0 Runtime (all platforms)

Download and install the **.NET 10.0 Runtime** (the base runtime; the Desktop Runtime is not required):

*   <https://dotnet.microsoft.com/download/dotnet/10.0>

Verify with:

```shell
dotnet --list-runtimes
```

Look for an entry such as `Microsoft.NETCore.App 10.x.x`.

### 2. Install the mount driver

| Platform | What to install | Verify |
| --- | --- | --- |
| **Windows** | [Dokan 2.x](https://github.com/dokan-dev/dokany/releases) - run the installer, then **restart Windows** so the driver loads. | `Test-Path "$env:SystemRoot\System32\dokan2.dll"` returns `True`. |
| **Linux** | FUSE 3 - `sudo apt install libfuse3-3 fuse3` (Fedora: `sudo dnf install fuse3 fuse3-libs`; Arch: `sudo pacman -S fuse3`). | `/dev/fuse` exists (otherwise `sudo modprobe fuse`) and `fusermount3` is on `PATH`. |
| **macOS** | [macFUSE](https://macfuse.io) - allow the system extension when prompted. macOS 15.4 or later can use the FSKit backend, which needs no kernel extension. | The app starts without a "macFUSE (libfuse3) was not found" error. |

### 3. Download and extract

1. Download the archive for your OS and CPU from the [latest release](https://github.com/purelogiccode/SimpleXisoDrive/releases): `win-x64`/`win-arm64`, `linux-x64`/`linux-arm64` or `osx-x64`/`osx-arm64`.
2. Extract it to a folder of your choice (for example `C:\Tools\SimpleXisoDrive` or `~/opt/simplexisodrive`).
3. On Linux and macOS the executable bit is already set; on Windows run `SimpleXisoDrive.exe`.

See [Installation](docs/Installation.md) for the detailed, step-by-step guide.

## Documentation

Comprehensive documentation lives in the [`docs`](docs/Home.md) folder and is published twice from
the same Markdown sources; both publications are rebuilt automatically on every push to `master`
that changes `docs/`:

*   **Documentation site (GitHub Pages):** <https://purelogiccode.github.io/SimpleXisoDrive/> -
    built by `.github/workflows/pages.yml` and rendered with a side menu from
    [`docs/_data/navigation.yml`](docs/_data/navigation.yml) and
    [`docs/_layouts/default.html`](docs/_layouts/default.html).
*   **GitHub wiki:** the same pages, mirrored by `.github/workflows/wiki.yml`, with a side menu
    driven by [`docs/_Sidebar.md`](docs/_Sidebar.md).

Start here:

*   [Installation](docs/Installation.md) - dependency install steps (runtime, Dokan, FUSE), installing, upgrading.
*   [Linux and macOS](docs/Linux-and-macOS.md) - FUSE/macFUSE prerequisites, usage, and platform notes.
*   [Getting Started](docs/Getting-Started.md) - your first mount, drag-and-drop, unmounting.
*   [Command-Line Reference](docs/Command-Line-Reference.md) - arguments, options, exit codes.
*   [Troubleshooting](docs/Troubleshooting.md) - every known error with causes and fixes.
*   [FAQ](docs/FAQ.md) - short answers to common questions.
*   [Architecture](docs/Architecture.md) - components, mount lifecycle, threading.
*   [LibFuseSharp](docs/FuseSharp.md) - the standalone FUSE 3 mount library, now a NuGet package from its own repository.
*   [Building](docs/Building.md) - build, test, and the CI/release workflow.
*   [What's New](WhatsNew.md) - release highlights.
*   [XDVDFS Format](docs/XDVDFS-Format.md) - on-disk structures and supported variants.
*   [Privacy and Networking](docs/Privacy-and-Networking.md) - telemetry, endpoints, offline use.
*   [Release History](docs/Release-History.md) - every tagged release and its notable changes.
*   [Security Policy](SECURITY.md) - supported versions and private vulnerability reporting.

The full documentation map (Virtual File System, Services, Glossary, Testing, Contributing and
Release History) is on [Home](docs/Home.md).

## How to use

### Windows

#### Drag-and-drop (easiest)

1. Drag an `.iso`, `.xiso`, `.cso`, `.cci`, `.chd` or `.zar` file, or a GOD/SVOD header/`.data` folder, onto `SimpleXisoDrive.exe`.
2. The app picks the first free drive letter from `M:` through `R:`, mounts the image read-only and opens File Explorer.
3. **To unmount:** click the console window and press any key (or press `Ctrl+C`).

#### Command line

```shell
SimpleXisoDrive.exe <image-file> <mount-path> [options]
```

| Argument / option | Description |
| --- | --- |
| `<image-file>` | Path to the `.iso`, `.xiso`, `.cso`, `.cci`, `.chd` or `.zar` file, or a GOD/SVOD header or `.data` folder. The extension may be omitted when the file can be resolved. |
| `<mount-path>` | Drive letter such as `Z:` or `Z:\`, or an existing empty NTFS folder. When omitted, drag-and-drop mode picks a free `M:`-`R:` letter and opens Explorer. |
| `-l`, `--launch` | Open Explorer at the mount path after mounting. |
| `-d`, `--debug` | Show verbose Dokan debug output. |
| `-i`, `--image-iso` | Also expose the raw Xbox image as `image.iso` at the mount root (for emulators such as xemu). |
| `-h`, `--help` | Print the usage text and exit (no network call, no Dokan probe). |

Options may appear before or after the mount path (`SimpleXisoDrive.exe game.iso -d Z:` works).

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z:
SimpleXisoDrive.exe "D:\Games\Halo.cso" Z:
SimpleXisoDrive.exe "D:\Games\Halo.cci" Z: --image-iso
SimpleXisoDrive.exe "D:\Games\Halo.chd" Z: --image-iso
SimpleXisoDrive.exe "D:\Games\Halo.zar" "C:\Mounts\Halo" -l
SimpleXisoDrive.exe "D:\Games\58480813\00007000\4279B61925C340CC1F19" Z:
```

Press `Ctrl+C` in the console to unmount.

### Linux and macOS

```shell
SimpleXisoDrive <image-file> [mount-path] [options]
```

*   `<image-file>`: Path to the `.iso`, `.xiso`, `.cso`, `.cci`, `.chd` or `.zar` file, or a GOD/SVOD header or `.data` folder.
*   `<mount-path>`: An existing empty directory. When omitted, a temporary directory is created, printed after mounting, and removed on unmount.
*   Every option listed above works the same on Linux and macOS.

```shell
mkdir -p ~/mnt/halo
SimpleXisoDrive ~/Games/Halo.iso ~/mnt/halo
SimpleXisoDrive ~/Games/Halo.chd ~/mnt/halo --image-iso
SimpleXisoDrive ~/Games/Halo.zar --launch
```

Unmount with `Ctrl+C`, `fusermount3 -u <mount-path>` (Linux) or `umount <mount-path>` (macOS).

See [Getting Started](docs/Getting-Started.md) and the
[Command-Line Reference](docs/Command-Line-Reference.md) for every behavior and exit code.

## Technical Details

*   **XDVDFS Parsing:** Uses the XISOSharp 1.8.0 library (`XisoImage`/`XisoImageSource`) to traverse the Xbox-specific binary tree structure, including rebuilt sector-0 images, CISO/CCI containers and GOD/SVOD containers (StfsSharp-backed).
*   **CCI Discovery:** Uses XISOSharp's `XisoImageSource` detection (CCISharp-backed) for `.cci` split-set resolution, including multi-digit part names and standalone numeric images.
*   **CHD Parsing:** Uses the CHDSharp library to decompress CHD hunks on demand (all CHD versions V1–V5 and codecs), exposing the decompressed Xbox image to the XDVDFS parser. Only CHDs whose decompressed image is a valid Xbox ISO are mounted; differential child CHDs must be merged with their parent first.
*   **ZArchive Parsing:** Mounts the ZArchive directory tree with on-demand zstd block decompression, and detects a single embedded XISO image automatically.
*   **Mount Backends:** Dokan on Windows (`DokanNet`); on Linux and macOS the standalone **LibFuseSharp** package (namespace `FuseSharp`, MIT, source at <https://github.com/purelogiccode/LibFuseSharp>) mounts any `IFuseVolume` through the FUSE 3 high-level API (`libfuse3` / macFUSE), with the platform-specific structures for Linux x64/ARM64 and macOS.
*   **Shared Core:** The image parsing, virtual file system and services live in `SimpleXisoDrive.Core`; the Windows and Unix front ends only implement the mount backend and CLI. `FuseSharp` has no dependency on Core: the Unix app adapts the Xbox volume with `FuseVolumeAdapter`.
*   **Cycle Detection:** Includes safety checks to prevent infinite loops in corrupted or malformed ISO images.
*   **Mount Sanitization:** Automatically handles mount point strings (e.g., converts `Z:\` to `Z:`) to satisfy Dokan driver requirements.
*   **Smart Permissions:** Automatically adjusts Dokan options based on Administrator privileges to ensure the highest success rate for mounting.

## Common failures and fixes

| Symptom | Fix |
| --- | --- |
| `The Dokan runtime library (dokan2.dll) was not found` | Install [Dokan](https://github.com/dokan-dev/dokany/releases) and restart Windows. |
| `Warning: The Dokan driver (dokan2.sys) was not found` | Reinstall Dokan and restart; mounting may fail until the driver loads. |
| `libfuse3 was not found` | Install FUSE 3 for your distribution (see [Install dependencies](#install-dependencies)). |
| `/dev/fuse was not found` | Load the kernel module: `sudo modprobe fuse`. |
| `fusermount3 was not found on PATH` | Install the `fuse3` tools package. |
| `macFUSE (libfuse3) was not found` | Install [macFUSE](https://macfuse.io) and allow the system extension. |
| `Image file not found at '<path>'` | Check the path and quote it if it contains spaces. A directory with exactly one image, or a missing extension, is resolved automatically. |
| `Mount path '<path>' is not an existing directory` | Create the folder first (`mkdir "C:\mount\xiso"`), or pass a drive letter such as `Z:`. |
| `'<path>' is not a valid Xbox ISO/XISO image` | The file is a PC ISO, an encrypted Redump-style dump, incomplete, or uses an unsupported layout. Convert it to XISO first; only GOD/SVOD containers carry an XDVDFS partition. |
| `'<path>' is an Xbox 360 STFS package (XBLA/XBLIG), not an Xbox disc image` | The file is a single-file STFS package; its payload is a file table, not an XDVDFS image. Only GOD/SVOD containers can be mounted. |
| `'<path>' is not an Xbox ISO CHD` | The decompressed CHD is not an Xbox ISO (for example a CD, GD-ROM or Xbox 360 image). A differential child CHD must be merged with its parent first. |
| `'<path>' is not a valid ZArchive (.zar) file` | The archive is corrupt, is not a ZArchive, or uses an unsupported version. Re-create it from the original image. |
| `Could not find an available drive letter (M-R)` | Free one of `M:`-`R:` or pass an explicit mount path. |
| `fuse: mountpoint is not empty` | Use an empty directory as the mount path. |
| `Something's wrong with the Dokan driver` | Right-click and **Run as Administrator**, reinstall or update Dokan, then restart. |
| Write operations fail | The volume is read-only by design; copy files out to a writable location instead. |
| macOS refuses to start the downloaded binary | Remove the quarantine attribute: `xattr -d com.apple.quarantine SimpleXisoDrive`. |

The [Troubleshooting](docs/Troubleshooting.md) page lists every message in detail.

## FAQ

**What formats can I mount?** Xbox ISO/XISO (`.iso`, `.xiso`), CISO (`.cso`, including split `.1.cso`
sets), CCI/CCIM (`.cci`, including split sets), Xbox ISO CHD (`.chd`), ZArchive (`.zar`, a directory tree
or a single embedded XISO) and
Xbox 360 GOD/SVOD containers (the XDVDFS game partition inside the container).

**Does it modify my image?** No. The image or archive is opened read-only and every mutating
operation is denied.

**Can I copy files from the mounted volume?** Yes, copying out to a normal writable location works
like any other read-only drive.

**Can I mount several images at once?** Yes, start one instance per image; each gets its own mount
point and console window.

**Does it support Xbox 360 or Xbox One images?** Xbox 360 GOD/SVOD containers are supported because
their data fragments contain an Xbox 360 XDVDFS game partition. Plain Xbox 360 ISO dumps, STFS
packages (XBLA/XBLIG) and Xbox One images are not XDVDFS and are rejected. A `.zar` tree is exposed
as-is whatever produced it, but its contents are not parsed.

**Does it need an internet connection?** No; the update check, launch statistics and crash reporting
are advisory and can be blocked (see [Privacy and Networking](docs/Privacy-and-Networking.md)).

**Do I need FUSE on Linux and macOS?** Yes. Linux needs FUSE 3 (`libfuse3` plus the `fuse3` tools);
macOS needs macFUSE. See [Install dependencies](#install-dependencies).

More answers are in the [FAQ](docs/FAQ.md).

## Security

Security issues should be reported privately through the repository's
[Security Policy](SECURITY.md) (GitHub's **Report a vulnerability** form), not in public issues.
Dependabot alerts, Dependabot security updates, CodeQL code scanning, secret scanning and push
protection are enabled for this repository.

## Releasing

The complete release and deployment checklist for maintainers and AI agents is in
[`AGENTS.md`](AGENTS.md): version bump, documentation refresh, local bundle creation, CI release,
and post-release verification.

## Support the Project

If you find this tool useful, consider supporting development:
*   **Star the Repo:** [GitHub Repository](https://github.com/purelogiccode/SimpleXisoDrive)
*   **Donate:** [https://purelogiccode.com/Donate](https://purelogiccode.com/Donate)

## License

This project is licensed under **GPL-3.0**.

SimpleXisoDrive is an independent, community project and is **not affiliated with, endorsed by, or
sponsored by Microsoft Corporation**. "Xbox" and "Microsoft" are trademarks of the Microsoft group
of companies and are used here only to describe compatibility and file formats.

*   **DokanNet:** MIT License.
*   **Dokan Library:** LGPL/MIT.
*   **LibFuseSharp:** MIT License (separate package/repository).
*   **CHDSharp:** MIT License.
*   **CCISharp:** MIT License.
*   **StfsSharp:** MIT License.
*   **libfuse:** LGPL-2.1 (Linux).
*   **macFUSE:** BSD-style licenses.

---
*Developed by [Pure Logic Code](https://purelogiccode.com/).*
