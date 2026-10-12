# XISO Studio

[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)](https://github.com/purelogiccode/XISOStudio/releases)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6.svg?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![Linux](https://img.shields.io/badge/Linux-x64%20%7C%20ARM64-FCC624.svg?logo=linux&logoColor=black)](https://github.com/purelogiccode/XISOStudio/releases)
[![macOS](https://img.shields.io/badge/macOS-Intel%20%7C%20Apple%20Silicon-000000.svg?logo=apple&logoColor=white)](https://github.com/purelogiccode/XISOStudio/releases)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Avalonia](https://img.shields.io/badge/UI-Avalonia-8A2BE2.svg)](https://avaloniaui.net/)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.txt)
[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio/releases)
[![GitHub release date](https://img.shields.io/github/release-date/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio/releases)
[![Downloads](https://img.shields.io/github/downloads/purelogiccode/XISOStudio/total)](https://github.com/purelogiccode/XISOStudio/releases)
[![GitHub stars](https://img.shields.io/github/stars/purelogiccode/XISOStudio?style=social)](https://github.com/purelogiccode/XISOStudio/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/purelogiccode/XISOStudio?style=social)](https://github.com/purelogiccode/XISOStudio/forks)
[![GitHub issues](https://img.shields.io/github/issues/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio/issues)
[![GitHub last commit](https://img.shields.io/github/last-commit/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio/commits/master)
[![Repo size](https://img.shields.io/github/repo-size/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio)
[![Top language](https://img.shields.io/github/languages/top/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio)
[![CI](https://github.com/purelogiccode/XISOStudio/actions/workflows/ci.yml/badge.svg)](https://github.com/purelogiccode/XISOStudio/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-purelogiccode.github.io-blue)](https://purelogiccode.github.io/XISOStudio/)
[![Wiki](https://img.shields.io/badge/wiki-GitHub-181717?logo=github)](https://github.com/purelogiccode/XISOStudio/wiki)
[![Powered by XISOSharp](https://img.shields.io/badge/Powered%20by-XISOSharp-8A2BE2.svg)](https://github.com/purelogiccode/XISOSharp)
[![Powered by CHDSharp](https://img.shields.io/badge/Powered%20by-CHDSharp-8A2BE2.svg)](https://github.com/purelogiccode/CHDSharp)
[![Powered by ZArchiveSharp](https://img.shields.io/badge/Powered%20by-ZArchiveSharp-8A2BE2.svg)](https://github.com/purelogiccode/ZArchiveSharp)
[![Powered by SharpCompress](https://img.shields.io/badge/Powered%20by-SharpCompress-8A2BE2.svg)](https://github.com/adamhathcock/sharpcompress)
[![Formats](https://img.shields.io/badge/formats-ISO%20%7C%20XISO%20%7C%20ZAR%20%7C%20CSO%20%7C%20CHD-orange.svg)](#supported-formats)
[![Tests](https://img.shields.io/badge/tests-1483%20passing-brightgreen.svg)](https://github.com/purelogiccode/XISOStudio/actions/workflows/ci.yml)
[![Code analyzers](https://img.shields.io/badge/analyzers-Meziantou%20%7C%20Roslynator-blueviolet)](docs/Architecture.md)
[![Made with C#](https://img.shields.io/badge/Made%20with-C%23-239120.svg?logo=csharp&logoColor=white)](https://dotnet.microsoft.com/languages/csharp)
[![Nullable](https://img.shields.io/badge/nullable-enabled-blue.svg)](https://learn.microsoft.com/dotnet/csharp/nullable-references)
[![SemVer](https://img.shields.io/badge/SemVer-2.0.0-blue.svg)](https://semver.org/)
[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-yellow.svg?logo=conventionalcommits&logoColor=white)](https://conventionalcommits.org)
[![Contributors](https://img.shields.io/github/contributors/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio/graphs/contributors)
[![Commit activity](https://img.shields.io/github/commit-activity/m/purelogiccode/XISOStudio)](https://github.com/purelogiccode/XISOStudio/commits/master)
[![Code style](https://img.shields.io/badge/code%20style-.editorconfig-ff69b4.svg)](https://editorconfig.org/)
[![Maintenance](https://img.shields.io/badge/maintenance-active-brightgreen.svg)](https://github.com/purelogiccode/XISOStudio/commits/master)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](docs/Repository.md#contributing)

A high-performance, cross-platform desktop utility for the Xbox preservation and emulation community, running on **Windows, Linux, and macOS**. Convert, verify, and explore Xbox and Xbox 360 images (ISO, CSO, ZAR, and CHD), powered by the XISOSharp and CHDSharp libraries.

---

## 📋 Table of Contents

- [Overview](#overview)
- [What's New](#whats-new)
- [Screenshots](#screenshots)
- [Key Features](#key-features)
- [Installation](#installation)
- [Usage](#usage)
- [Supported Formats](#supported-formats)
- [Architecture](#architecture)
- [System Requirements](#system-requirements)
- [Safety & Reliability](#safety--reliability)
- [Acknowledgements](#acknowledgements)
- [License](#license)

---

## Overview

**XISO Studio** streamlines the process of converting standard Xbox and Xbox 360 ISOs into the optimized, trimmed **XISO** format (or compressed ZAR, CSO, and CHD images). Encoding and decoding is delegated to the **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** library, which repacks the XDVDFS game partition, and to **[CHDSharp](https://github.com/purelogiccode/CHDSharp)** for CHD compression, delivering superior performance and modern features like real-time disk write monitoring.

Whether you're managing a large collection of Xbox game backups or verifying the integrity of your dumps, this application provides a user-friendly interface with powerful batch processing capabilities.

---

## What's New

### v3.0.3 — fewer false bug reports for expected input problems

- **Invalid or unsupported images are recognized as user input** — a `.chd` that is a CD/GD-ROM image (for example Sega Chihiro arcade dumps), a PS5 `.pkg`, a truncated download, or a renamed ISO is now logged at Information and explained with a clear message instead of being uploaded as an application bug.
- **Folder-picker failures no longer generate bug reports** — a selected folder that no longer exists (deleted, disconnected drive, unavailable network share) is logged at Information with a friendly message.
- **Drag-and-drop and extract-to-temp skip unwritable drives** — the temp-folder resolver creates the candidate folder up front and falls back to the next drive when a root cannot be written (ACL restrictions, BitLocker-locked or read-only volumes), and environmental I/O failures are logged at Information.
- **Closing during a long operation no longer generates a bug report** — the timeout exit path is normal behavior and now logs at Information.
- Avalonia 12.1.4, SharpCompress 0.50.5, and analyzer/test-runner updates (build-time only).

### v3.0.2 — post-3.0.1 fixes

- **Fewer false bug reports from failing drives** — a drive reporting a *fatal device hardware error* (`ERROR_DEVICE_HARDWARE_ERROR`, Win32 483 / `0x800701E3`) during archive analysis or extraction now stops the batch with the same drive-health guidance as other hardware I/O errors, and is logged at Information instead of Error, so a dying disk no longer uploads one bug report per file.
- **Locked output files no longer generate bug reports** — overwriting an existing result briefly retries the delete while another process holds it (an emulator, antivirus, or Explorer preview); a file that stays locked is reported as failed with guidance to close the application using it. The same applies when **Delete Originals** cannot remove a locked source.
- **XISOSharp 1.4.2** — invalid ZAR inputs are still classified as *invalid image* even though the library now returns a failure result instead of throwing, and device/network failures during archive analysis log at Information.
- Analyzer packages updated (Meziantou 3.0.294, Roslynator 5.0.1) — build-time only.

### v3.0.1 — reliability patch

- **Fewer false bug reports** — a tested file whose move to `_success`/`_failed` fails because another process holds it (antivirus, download manager, cloud sync) is still reported as failed and the batch continues, but it is logged at Information instead of Error, so it is no longer auto-uploaded as an application bug. The same applies when the source cannot be opened and the cloud-copy fallback hits a sharing violation. Unreadable Linux/macOS system mounts (`/root`, `/.snapshots`, `/sys/kernel/tracing`) are skipped at Debug during the startup temp scan instead of warning once per mount point.
- **In-order progress reporting** — the CSO, ZAR, and CHD progress adapters now report synchronously (`SynchronousProgress<T>`) instead of posting callbacks to the thread pool, so percentages can no longer race and appear out of order or move backwards.
- **Green CI on all platforms** — platform-dependent tests skip correctly on Linux/macOS, and the full suite passes on Windows, Linux, and macOS.

### v3.0.0 — cross-platform Avalonia port, CHD support & structured logging

- **Cross-platform UI** — the application was ported from WPF to **[Avalonia](https://avaloniaui.net/)** and now runs on **Windows, Linux, and macOS** (x64 and ARM64) with the same dark theme and layout. All image libraries (XISOSharp, ZArchiveSharp, CHDSharp, SharpCompress) are pure managed code with no native dependencies, and the optional 7-Zip CLI fallback is bundled for every platform (`7za.exe`/`7za_arm64.exe`, `7zz_linux_x64`/`7zz_linux_arm64`, or the universal `7zz_osx`).
- **CHD output format** — convert Xbox and Xbox 360 ISOs to **CHD** (`.chd`, CHD v5 with the chdman `createdvd` preset) alongside XISO/ZAR/CSO.
- **CHD integrity testing** — test Xbox DVD CHD files, including an optional deep scan that verifies every hunk and checksum plus the Xbox filesystem structure.
- **CHD explorer** — browse and extract files from CHD images directly, with hunk decompression on demand.
- **Serilog logging pipeline** — all logging now runs through [Serilog](https://serilog.net/): the on-screen viewer, a rolling daily log file (`%LocalAppData%\XISOStudio\logs`), and automatic forwarding of every Warning-or-higher event to the bug report API.
- **Complete bug reports** — every report includes Environment Details (app name/version, OS and Windows version, architecture, bitness, processor count, base directory, temp path), Error Details, and Exception Details (type, message, source, stack trace, including nested exceptions).
- **Selectable file lists** — after choosing an input folder, the Convert and Test views list every supported file with a **Select** checkbox, file name, and size. Tick the files you want and click **Start**; only ticked files are processed. **Select All** / **Deselect All** toggle the whole list in one click, and **Search Subfolders** rescans the list immediately.
- **Compressed output formats** — the Convert tab produces **ZAR** (`.zar`, ZArchive/zstd, loadable in Xenia canary) and **CSO** (`.cso`, CISO v2/LZ4, byte-identical to `xdvdfs compress`) in addition to optimized XISO; pick the format in the Options panel.
- **ISO/XISO only** — the bundled `bchunk.exe` and `.cue`/`.bin` input support were removed; supported inputs are `.iso` (Redump full-disc images or optimized XISO files) and archives (`.zip`, `.7z`, `.rar`).
- **Reliability pass** — 38 verified defects fixed across the pipeline: archives are deleted only when every entry was converted, same-named inputs can no longer overwrite each other, `Skip $SystemUpdate` is honored for already-optimized CSO/CHD inputs, split CISO cloud copies keep their part markers, a single unreadable file no longer aborts a batch, and environmental errors no longer generate bug reports. A pre-release review added further fixes: explorer extraction runs off the UI thread, drag-and-drop sources survive until the drop target finishes copying, failed extractions clean up their temp folders, split CISO sets keep their extension casing on case-sensitive file systems, and cross-mount-point moves on Linux/macOS check free space. A final pre-release review closed the remaining edge cases: a failed test whose `_failed` move also failed is no longer counted twice, an out-of-space cross-volume move is reported as a failure instead of a success, archives holding a lone split-CISO continuation part are kept, missing `.zar`/`.chd` files no longer count as invalid images, a mis-routed CHD request can no longer produce an XISO under a `.chd` name, macOS folder checks respect case-insensitive volumes, and Cancel/Exit are honored during the pre-operation cleanup scan. The on-screen log viewer is bounded and throttled (newest 2,000 lines), and XISOSharp's chunked console output is reassembled into clean lines, so heavy logging can no longer freeze the window or hide the **Cancel** button. Log messages render as plain text (no JSON-style quotes), and the batch summary drains pending UI updates first, so the last file is always included in the success/fail counts.

### v2.8.0 — powered entirely by XISOSharp

- **One conversion engine** — all ISO → XISO conversion runs in-process through [XISOSharp](https://github.com/purelogiccode/XISOSharp); the bundled `extract-xiso.exe`, `xdvdfs.exe`, and the native writer were removed (smaller download, no external processes).
- **Integrity testing and XISO Explorer** are now backed by the same library (`AuditXiso` and `XisoExplorer`).
- **Fixes** — the main window now appears immediately (startup drive probing no longer blocks it); cloud/OneDrive sources keep their original output name; already-optimized files no longer delete an existing output; **Replace Originals** no longer deletes originals (including archives) when a conversion was skipped; device I/O errors stop the batch with a drive-health message.
- **Hardening** — free-space/FAT32 pre-checks, partial-output cleanup, cross-volume move fallback, locked-archive retries.
- **Bundles** — release ZIPs include `LICENSE.txt`, `ReadMe.md`, and `WhatsNew.md`.

Read the full [What's New](WhatsNew.md) or browse the [Release Notes](docs/Release-Notes.md).

---

## Screenshots

![Convert Tab](screenshot.png)
*Batch conversion interface with real-time progress monitoring*

![Test Tab](screenshot2.png)
*ISO integrity testing with batch organization*

![Explorer Tab](screenshot3.png)
*XISO file browser*

---

## Key Features

### 🔄 Batch Conversion
- **In-Process Engine**: All conversion is performed in-process by [XISOSharp](https://github.com/purelogiccode/XISOSharp) for XISO/ZAR/CSO and by [CHDSharp](https://github.com/purelogiccode/CHDSharp) for CHD — no external conversion binaries required
- **Output Formats**: Optimized **XISO** (`.iso`), **ZAR** (`.zar`, ZArchive/zstd — Xenia canary loads it directly), **CSO** (`.cso`, CISO v2/LZ4), or **CHD** (`.chd`, CHD v5 with the chdman `createdvd` preset) — selected per batch on the Convert tab
- **Smart Processing**: Removes video partitions and padding, converting Redump ISOs to playable XISO format
- **Archive Support**: Process `.zip`, `.7z`, and `.rar` files directly with high-performance extraction via SharpCompress, with automatic 7-Zip CLI fallback for complex `.7z` archives
- **Encrypted Archive Detection**: Automatically detects password-protected archives and provides clear guidance for manual extraction, preventing cryptic extraction failures
- **System Update Removal**: Option to skip the `$SystemUpdate` folder for additional space savings
- **Skip Already Optimized**: Images that already carry the optimized XISO tag are detected and skipped

### ✅ Integrity Testing
- **Structural Validation**: Deep traversal of the XDVDFS file tree (ISO, CSO, and the Xbox filesystem inside CHD images) or the ZAR archive tree to ensure file system validity
- **Deep Surface Scan**: Optional sequential read of the entire image — every sector for ISO/CSO, every decompressed block for ZAR, every CHD hunk and checksum for CHD — to detect physical data corruption or bad sectors
- **All Output Formats**: Test the optimized `.iso`, `.cso`, `.zar`, and `.chd` files this app produces (split `.1.cso` part sets are recognized too)
- **Batch Organization**: Automatically organize "Passed" or "Failed" images into dedicated subfolders

### 🔍 Image Explorer
- **Native Browsing**: Open any Xbox ISO, CSO, ZAR, or CHD to browse files and directories without extraction
- **Metadata View**: View file sizes, attributes, and directory structures directly in the UI
- **Double-Click to Open**: Open files directly from the image with their default associated applications
- **Drag & Drop Extraction**: Drag files out of the explorer to extract them to any folder (your file manager, Desktop, etc.)

### 📊 Advanced Monitoring
- **Real-time Statistics**: Track success/fail counts, elapsed time, and processed files
- **Disk Monitor**: Live monitoring of read/write speeds and drive activity to identify hardware bottlenecks (Windows performance counters; shows N/A on Linux/macOS)
- **Cloud-Aware**: Automatic detection and handling of cloud-stored files (e.g., OneDrive)
- **Structured Logging**: [Serilog](https://serilog.net/) pipeline with an on-screen viewer, a rolling daily log file, and automatic bug-report forwarding for Warning-or-higher events

---

## Installation

### Prerequisites
- **Operating System**: Windows 10 (version 1809) or later / Windows 11, a modern x64/ARM64 Linux distribution, or macOS 12+ (Intel or Apple Silicon)
- **Runtime**: [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Architecture Support**:
    - **Windows**: x64 and ARM64.
    - **Linux**: x64 and ARM64.
    - **macOS**: Intel (x64) and Apple Silicon (ARM64).

### Steps
1. Download the release archive for your platform from the [Releases](https://github.com/purelogiccode/XISOStudio/releases) page
2. Extract the archive to your desired location
3. Run the executable:
    - **Windows**: `XISOStudio.exe`
    - **Linux/macOS**: `./XISOStudio` (run `chmod +x XISOStudio` first if needed)

No installation required – the application is fully portable.

---

## Usage

### Converting ISOs
1. Launch the application
2. Click **"Select Input Folder"** and choose the directory containing your ISO files
3. Click **"Select Output Folder"** to specify where converted files will be saved
4. Configure options:
    - **Remove System Update**: Skip `$SystemUpdate` folder to save space
    - **Replace Originals**: Replace input files with converted versions
    - **Test After Conversion**: Automatically verify converted ISOs
    - **Check Output Integrity**: Structurally validate each converted XISO before reporting success
5. Click **"Convert"** to start the batch process

### Testing Image Integrity
1. Switch to the **"Test Integrity"** tab
2. Select your input folder containing `.iso`, `.cso`, `.zar`, or `.chd` files
3. Enable **"Move Passed Files"** and/or **"Move Failed Files"** to organize results
4. Click **"Start Integrity Test"** to begin validation

### Exploring Image Contents
1. Switch to the **"Explorer"** tab
2. Click **"Browse..."** and select an Xbox `.iso`, `.cso`, `.zar`, or `.chd` file
3. Browse the file tree to view contents without extraction
4. **Open Files**: Double-click any file to open it with its default application
5. **Extract Files**: Drag and drop files from the explorer to your file manager, Desktop, or any folder to extract them

---

## Conversion Engine

All conversion is performed by the **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** library, with CHD output encoded by **[CHDSharp](https://github.com/purelogiccode/CHDSharp)**:

- **Approach**: Repack — reads the XDVDFS game partition and writes a new optimized XISO with files packed tightly together
- **Output Size**: Smallest — video partition, padding, and inter-file gaps are removed
- **No External Tool**: The engine is a managed library bundled with the application
- **Redump-Aware**: Automatically detects XGD1/XGD2/XGD3 and hybrid partition offsets
- **Already Optimized**: Images carrying the optimized tag are skipped automatically

### What Gets Removed

- ✅ **Video Partition** (DVD movie/demonstration) - **~7-387 MB removed**
- ✅ **End Padding** (empty sectors after last file) - **Variable**
- ✅ **System Update** (optional) - **~100-300 MB removed**

### Visual Comparison

```
Redump ISO (Original):
[Video Partition][XDVDFS: Header][Dir][File A][gap][File B][gap][File C][Padding]

XISOSharp Output:
[XDVDFS: Header][Dir][File A][File B][File C] (gaps removed, tightly packed)
                      ↑    ↑    ↑
                 Files repositioned for maximum compression
```

---

## Supported Formats

| Operation      | Supported Formats                              |
|:---------------|:-----------------------------------------------|
| **Conversion** | `.iso` (Redump or optimized XISO), `.zip`, `.7z`, `.rar` |
| **Output**     | `.iso` (XISO), `.zar` (ZAR), `.cso` (CSO), `.chd` (CHD) |
| **Testing**    | `.iso`, `.cso` (CISO, incl. split `.1.cso` part sets), `.zar` (ZAR), `.chd` (Xbox DVD CHDs) |
| **Explorer**   | `.iso`, `.cso` (CISO), `.zar` (ZAR), `.chd` (Xbox DVD CHDs) |

---

## Architecture

The application follows modern software engineering principles with a clean, maintainable architecture based on **Dependency Injection** and **Service-Oriented Design**.

### Dependency Injection
Utilizes `Microsoft.Extensions.DependencyInjection` for comprehensive service management. All core logic is decoupled from the UI, enabling easier testing and modular updates.

### Logging
Logging runs through a single [Serilog](https://serilog.net/) pipeline with three sinks: the on-screen log viewer (`UiLogSink`), a rolling daily file log (`%LocalAppData%\XISOStudio\logs`), and a bug-report sink (`BugReportSink`) that forwards every **Warning-or-higher** event to the Bug Report API. Reports include complete environment, error, and exception sections; expected user/environmental errors are logged at Information level so they never generate noise.

### Testing
A comprehensive [xUnit](https://xunit.net/) test suite (`XISOStudio.Tests`) covers models, services, and image services with 1,450+ tests, using [Moq](https://github.com/devlooped/moq) for mocking.

### Technical Documentation
For a deep dive into the XDVDFS format, binary file structures, and the conversion algorithm, see the [XDVDFS Technical Documentation](docs/XDVDFS-Technical-Documentation.md). The full documentation (including installation, usage, troubleshooting, architecture, and [release notes](docs/Release-Notes.md)) lives in the [docs folder](docs/index.md) and is published both as the repository wiki and as the [GitHub Pages site](https://purelogiccode.github.io/XISOStudio/) — the wiki uses `docs/_Sidebar.md` and the Pages site uses `docs/_layouts/default.html`, so both get the same side menu. Highlights of the latest release are summarized in [What's New](WhatsNew.md).

---

## System Requirements

| Component    | Minimum Requirement                                                   |
|:-------------|:----------------------------------------------------------------------|
| OS           | Windows 10 (1809)+ / Windows 11, modern Linux (x64/ARM64), macOS 12+  |
| .NET Runtime | .NET 10.0 Runtime                                                     |
| Processor    | x64 or ARM64 architecture                                             |
| RAM          | 4 GB recommended                                                      |
| Storage      | Varies based on ISO collection size                                   |

---

## Safety & Reliability

- **Atomic Operations**: Converted files are verified before originals are deleted
- **Archive-Safe Replace Originals**: an archive is deleted only when every entry was extracted and every extracted image was converted; skipped or unprocessed entries keep the archive
- **Automatic Cleanup**: [`TempFolderCleanupHelper`](XISOStudio/Services/TempFolderCleanupHelper.cs) removes orphaned temporary files on startup or after crashes — only app-created GUID work folders older than six hours, so user folders with the same prefix and another instance's active folders are never touched
- **Concurrency-Safe Explorer and Shutdown**: background copy-outs hold an explorer lease so the image can't be disposed mid-extraction, and the close confirmation can't be bypassed or shown twice
- **Fallback Temp Drives**: Automatically searches alternative local drives when the system temp drive lacks sufficient space for archive extraction
- **Robust Error Handling**: Comprehensive exception handling with [Serilog](https://serilog.net/) structured logging; every Warning-or-higher event is automatically forwarded to the bug report API with full environment and exception details
- **Network Resilience**: Full support for UNC paths and mapped network drives with automatic retry logic for transient network failures
- **Cloud-Aware Retry**: Automatic retries with exponential backoff for cloud-synced files (OneDrive, etc.)
- **Encrypted Archive Handling**: Gracefully detects password-protected and encrypted archives, providing clear user guidance instead of cryptic errors
- **Process Isolation**: The 7-Zip CLI fallback for archive extraction runs in an isolated process with cancellation support

---

## Acknowledgements

- **[Avalonia](https://avaloniaui.net/)** - Cross-platform .NET UI framework the application is built on
- **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** - XISO/XDVDFS reading, writing, and conversion library that powers XISO/ZAR/CSO conversion, integrity testing, and exploration
- **[ZArchiveSharp](https://github.com/purelogiccode/ZArchiveSharp)** - ZArchive/zstd compression library used for ZAR output and reading
- **[CHDSharp](https://github.com/purelogiccode/CHDSharp)** - CHD (Compressed Hunks of Data) reading, verification, and creation, used for CHD output and Xbox CHD testing/exploration
- **[Serilog](https://serilog.net/)** - Structured logging pipeline (log viewer, rolling file log, and bug report sinks)
- **[SharpCompress](https://github.com/adamhathcock/sharpcompress)** - High-performance archive extraction
- **[7-Zip](https://7-zip.org/)** - Bundled console fallback used when SharpCompress cannot extract an archive

---

## License

This project is licensed under the GNU General Public License v3.0 – see the [LICENSE.txt](LICENSE.txt) file for details.

---

<p>
  ⭐ <strong>If you find this tool useful, please give us a Star on GitHub!</strong> ⭐
</p>

<p>
  <a href="https://www.purelogiccode.com">Pure Logic Code</a>
</p>