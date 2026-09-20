# Simple Zip Drive for Windows

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64%20%7C%20ARM64-blue)](https://github.com/purelogiccode/SimpleZipDrive/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.txt)
[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/SimpleZipDrive)](https://github.com/purelogiccode/SimpleZipDrive/releases)
[![CI](https://github.com/purelogiccode/SimpleZipDrive/actions/workflows/ci.yml/badge.svg)](https://github.com/purelogiccode/SimpleZipDrive/actions/workflows/ci.yml)

**Simple Zip Drive** is a high-performance, user-mode filesystem utility that allows you to mount ZIP, 7Z, RAR, TAR, ZArchive, and Xbox XISO archives as virtual drives or NTFS directory mount points. It provides seamless, read-only access to compressed data without the need for manual extraction.

The solution includes two variants:
*   **SimpleZipDrive** - Built on [DokanNet](https://github.com/dokan-dev/dokan-dotnet)
*   **SimpleZipDrive_WinFsp** - Built on [WinFsp](https://github.com/winfsp/winfsp)

Unlike traditional archive utilities that extract the entire archive to a temporary folder, Simple Zip Drive utilizes a **hybrid streaming engine** to minimize memory overhead and maximize random-access performance.

![Screenshot](screenshot.png)

> **📖 Documentation:** Full documentation lives in the [repository wiki](https://github.com/purelogiccode/SimpleZipDrive/wiki) and on the [documentation site](https://purelogiccode.github.io/SimpleZipDrive/) (source in [`docs/`](docs/)) - installation guides, usage, architecture, troubleshooting, and more.

---

## 🚀 Key Features

*   **Multi-Format Support:** Mount ZIP, 7Z, RAR, and TAR archives seamlessly. TAR support includes compressed variants (`.tar.gz`, `.tar.bz2`, `.tar.xz`, `.tgz`, `.tbz2`, `.txz`). Comic-book archives are supported too: `.cbz` (ZIP), `.cbr` (RAR), and `.cb7` (7Z). Zstd-seekable `.zar` (ZArchive) containers mount with per-block random access, and Xbox disc images mount directly: `.iso` / `.xiso` (XISO) and compressed `.cso` (CISO) containers.
*   **Virtual Drive Mounting:** Mount any supported archive as a dedicated drive letter (e.g., `M:\`) or a folder path. The drive label automatically shows the archive name.
*   **Mount Type Choice:** Choose between drive letter or NTFS folder mounting via Settings, or use the dedicated `Mount as Drive Letter` and `Mount as Folder` menu items.
*   **Hybrid Caching Engine:**
    *   **Stored Entries (ZIP):** Uncompressed entries are read directly from the source archive with zero-copy, zero-cache performance - no RAM or disk overhead.
    *   **Small Files:** Cached in-memory for near-instantaneous access. Each file is decompressed only once and the buffer is shared across all open handles, so memory usage stays at approximately one copy per file regardless of how many applications access it. Decompression writes directly into the final buffer, halving the transient peak memory footprint during extraction of large entries.
    *   **Large Files (≥512 MB by default):** Automatically offloaded to a temporary disk cache to prevent RAM exhaustion. The per-file memory threshold can be adjusted via the Settings window.
*   **Framework-Dependent Single Executable:** Ships as framework-dependent single-file binaries (x64 and ARM64) - the `exe` with the native `7z.dll` / `7z_arm64.dll` fallback libraries (and `winfsp-msil.dll` for the WinFsp variant) beside it, plus this README and the license. No runtime files to carry around; only the [.NET Desktop Runtime](https://dotnet.microsoft.com/download) and the filesystem driver must be installed.
*   **Streaming Architecture:** The source archive is accessed via a direct file stream, supporting archives of virtually any size.
*   **Zero-Configuration UI:** Supports drag-and-drop functionality for automatic mounting to the first available drive letter (M-Q). The mounted drive label displays the archive filename.
*   **Configurable Cache:** Open `Settings > RAM Limit` to adjust the per-file RAM cache limit. The value is automatically clamped to 90% of available system memory to prevent out-of-memory errors.
*   **Configurable Mount Type:** Open `Settings` to choose the default mount type: **Drive Letter** (auto-selects M-Q) or **Folder** (browse for an NTFS directory). You can also use `File > Mount as Drive Letter` or `File > Mount as Folder` for one-time selection.
*   **Encrypted Archive Support:** Prompts for passwords when accessing protected archives.
*   **Cross-Integrity Mount (WinFsp):** When enabled in Settings, mounts archives to a folder path with a permissive security descriptor so that both standard and elevated (Administrator) processes can access the mounted drive. When SimpleZipDrive_WinFsp runs as Administrator, this mode is automatically enforced. Drive letter mounts remain isolated by Windows UAC — this is an OS limitation, not a WinFsp limitation.
*   **Screenshot Capture:** Press `F8` at any time to capture the active window and save it as a PNG in the `Screenshot` folder next to the application — handy for attaching visuals to bug reports.
*   **Automated Maintenance:** Integrated update checker (with MessageBox prompt before opening the browser) and automatic cleanup of temporary cache files upon unmounting. Also cleans up orphaned temp directories from previous sessions on startup.
*   **Enterprise Logging:** Comprehensive error tracking via a unified per-session log file and remote diagnostic reporting.

---

## 🛠 Prerequisites

Before running Simple Zip Drive, ensure your system meets the following requirements:

1.  **.NET 10.0 Runtime:** Download the latest [.NET Desktop Runtime](https://dotnet.microsoft.com/download).
2.  **Filesystem Driver** (depends on which variant you use):
    *   **For SimpleZipDrive (Dokan):** Download and install `DokanSetup.exe` from the [Official Releases](https://github.com/dokan-dev/dokany/releases). **Dokan v2 2.3.0 or later is required** — older driver installs are refused with a *"Dokan Driver Outdated"* dialog.
    *   **For SimpleZipDrive_WinFsp:** Download and install [WinFsp](https://github.com/winfsp/winfsp/releases) **2.1 or later** (2.1 is the latest stable release; 2.2+ are beta versions). Keep `winfsp-msil.dll` beside the executable — it ships with the app and is removed neither by you nor your antivirus without breaking mounts.

---

## 📦 Project Variants

| Variant | Driver | Library | Notes |
|:--------|:-------|:--------|:------|
| **SimpleZipDrive** | Dokan | [DokanNet](https://github.com/dokan-dev/dokan-dotnet) | Original implementation |
| **SimpleZipDrive_WinFsp** | WinFsp | [winfsp.net](https://github.com/winfsp/winfsp) | Alternative implementation |

Both variants share the same UI and feature set; only the underlying filesystem driver differs.

---

## 📖 Usage Guide

### Method 1: Drag-and-Drop (Recommended)
Simply drag any `.zip`, `.7z`, `.rar`, `.tar`, `.tar.gz`, `.cbz`, `.cbr`, `.cb7`, `.zar`, `.iso`, `.xiso`, or `.cso` file and drop it onto `SimpleZipDrive.exe`. The application will automatically attempt to mount the archive to the first available drive letter in the sequence: `M:`, `N:`, `O:`, `P:`, `Q:`.

### Method 2: Menu
Use the `File` menu to mount archives:
*   **Mount Archive** (`Ctrl+M`) - Uses the default mount type from Settings.
*   **Mount as Drive Letter** - Prompts for a file, then auto-selects a drive letter.
*   **Mount as Folder** - Prompts for a file, then lets you browse for an NTFS folder.

### Method 3: Command Line Interface (CLI)
For advanced users or automation, use the following syntax:

```shell
SimpleZipDrive.exe <PathToArchiveFile> <MountPoint>
```

**Examples:**
*   **Mount a ZIP file to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Data\Archive.zip" M
    ```
*   **Mount a 7Z file to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Data\Archive.7z" N
    ```
*   **Mount a RAR file to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Data\Archive.rar" O
    ```
*   **Mount a TAR file to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Data\Archive.tar" O
    ```
*   **Mount a compressed TAR file to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Data\Archive.tar.gz" O
    ```
*   **Mount a ZArchive (.zar) file to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Data\Archive.zar" N
    ```
*   **Mount an Xbox XISO disc image to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Games\MyGame.iso" N
    ```
*   **Mount a compressed Xbox image (.cso) to a drive letter:**
    ```shell
    SimpleZipDrive.exe "C:\Games\MyGame.cso" N
    ```
*   **Mount to an NTFS folder:**
    ```shell
    SimpleZipDrive.exe "C:\Data\Archive.zip" "C:\Mount\MyProject"
    ```

### Unmounting
To safely unmount the drive and clean up temporary resources:
1.  Click the **Unmount** button in the toolbar.
2.  Alternatively, close the application window.

### Capturing a Screenshot
Press `F8` at any time to capture the active window. The image is saved as a PNG in the `Screenshot` folder located next to the application executable. A status message confirms the saved file path.

---

## 🔍 Technical Architecture

*   **Read-Only Integrity:** The filesystem is strictly read-only. No modifications are made to the source archive.
*   **Random-Access Formats:** ZArchive (`.zar`, Zstd seekable) and Xbox XISO images (`.iso`, `.xiso`, `.cso`) are served with block/sector-level random access — every read decodes only the zstd or CISO blocks it touches, and uncompressed Xbox files are read straight from the disc sectors. Large game images seek instantly and never require full extraction.
*   **Memory Efficiency:** The application does not load the entire archive into RAM. It reads the Central Directory into a dictionary for fast lookups and streams file data only when requested. Stored (uncompressed) entries in ZIP archives bypass caching entirely using direct-read with Windows `RandomAccess` for near-zero overhead. The per-file RAM cache limit is configurable via `Settings > RAM Limit` and is automatically clamped to 90% of available system memory. A global memory cap at 90% of available free memory ensures stability even under heavy load. Decompression streams directly into the exact-size cached buffer, avoiding transient double-buffering (see [WhatsNew.md](WhatsNew.md)).
*   **Permissions:** Mounting to drive letters or system-protected directories may require **Administrator Privileges**. If you encounter "Access Denied" errors, right-click the executable and select "Run as Administrator." When running as Administrator with the WinFsp variant, cross-integrity folder mount is automatically enabled so that standard user processes can access the mounted drive.
*   **Temporary Storage:** Disk-based caching for large files occurs in `%LOCALAPPDATA%\SimpleZipDrive\Temp`. These files are purged automatically during graceful shutdown, and orphaned directories from crashed sessions are cleaned up on application startup.

---

## ❓ Troubleshooting

| Issue                             | Solution                                                                                                                      |
|:----------------------------------|:------------------------------------------------------------------------------------------------------------------------------|
| **Dokan Initialization Failed**   | Ensure the Dokan driver is installed and you have restarted your PC after installation. The app detects missing drivers and offers to open the download page automatically. |
| **Dokan Driver Outdated**         | The installed Dokan driver is older than 2.3.0. Update Dokan from [GitHub](https://github.com/dokan-dev/dokany/releases); the app opens the download page for you if you confirm the dialog. |
| **WinFsp Not Found**              | Install WinFsp from [GitHub](https://github.com/winfsp/winfsp/releases). The app detects missing drivers and offers to open the download page automatically. |
| **Missing Application File (winfsp-msil.dll)** | Restore the file beside the executable: re-download the app package and extract **all** files into the same folder. If antivirus keeps removing it, add an exclusion for `winfsp-msil.dll`. |
| **Drive Letter in Use**           | Specify a different drive letter via CLI or ensure letters M-Q are not mapped to network shares.                              |
| **Out of Memory**                 | Occurs if too many large files are opened simultaneously. Close applications accessing the virtual drive to free up cache, or unmount the drive to release all cached memory.    |
| **Archive File Error**            | Simple Zip Drive supports standard ZIP, 7Z, RAR, TAR, and compressed TAR formats (.tar.gz, .tar.bz2, .tar.xz), comic-book archives (.cbz, .cbr, .cb7), ZArchive (.zar), and Xbox XISO disc images (.iso, .xiso, .cso). Other formats like `.gz` or `.bz2` (without tar) are not supported. Generic (non-Xbox) ISO files are rejected because the `.iso` support targets the Xbox XDVDFS format.        |
| **Password Prompt Not Appearing** | Some encrypted archives may use unsupported encryption methods. Ensure your archive uses standard ZIP, 7Z, or RAR encryption. |
| **Drive invisible to elevated/standard processes** | This is Windows UAC isolation. Enable `Settings > Security Settings > Cross-integrity mount` (WinFsp only). When running as Administrator, this is enforced automatically. |

---

## 📜 License & Acknowledgments

This project is licensed under the GPLv3 License – see the [LICENSE](LICENSE.txt) file for details.

**Third-Party Libraries:**
*   [DokanNet](https://github.com/dokan-dev/dokan-dotnet) (MIT) - used by SimpleZipDrive
*   [WinFsp](https://github.com/winfsp/winfsp) (LGPL-3.0) - used by SimpleZipDrive_WinFsp
*   [SharpCompress](https://github.com/adamhathcock/sharpcompress) (MIT)
*   [SharpSevenZip](https://github.com/adoconnection/SevenZipExtractor) (MIT)
*   [ZArchiveSharp](https://github.com/purelogiccode/ZArchiveSharp) (MIT)
*   [XISOSharp](https://github.com/purelogiccode/XISOSharp) (MIT)

---

## 🤝 Contributing & Support

*   **Donate:** If you find this project useful, consider [supporting the developer](https://www.purelogiccode.com/donate).

**⭐ If you like this project, please give us a star on GitHub! ⭐**