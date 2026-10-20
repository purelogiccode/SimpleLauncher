[![CI](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/ci.yml)
[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/releases)
[![Latest release downloads](https://img.shields.io/github/downloads/purelogiccode/SimpleLauncher/latest/total)](https://github.com/purelogiccode/SimpleLauncher/releases/latest)
[![Total downloads](https://img.shields.io/github/downloads/purelogiccode/SimpleLauncher/total)](https://github.com/purelogiccode/SimpleLauncher/releases)
[![Release date](https://img.shields.io/github/release-date/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/releases)
[![Last commit](https://img.shields.io/github/last-commit/purelogiccode/SimpleLauncher/master)](https://github.com/purelogiccode/SimpleLauncher/commits/master)
[![Commit activity](https://img.shields.io/github/commit-activity/m/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/graphs/commit-activity)

[![Stars](https://img.shields.io/github/stars/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/stargazers)
[![Forks](https://img.shields.io/github/forks/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/forks)
[![Watchers](https://img.shields.io/github/watchers/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/watchers)
[![Contributors](https://img.shields.io/github/contributors/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/graphs/contributors)
[![Open issues](https://img.shields.io/github/issues/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/purelogiccode/SimpleLauncher)](https://github.com/purelogiccode/SimpleLauncher/pulls)
[![Maintained](https://img.shields.io/badge/Maintained%3F-yes-brightgreen)](https://github.com/purelogiccode/SimpleLauncher/commits/master)

[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20(x64%20%7C%20ARM64)-blue)](https://github.com/purelogiccode/SimpleLauncher/releases)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download)
[![Made with C#](https://img.shields.io/badge/Made%20with-C%23-239120)](https://learn.microsoft.com/dotnet/csharp/)
[![Built with Avalonia](https://img.shields.io/badge/Built%20with-Avalonia-8B44AC)](https://avaloniaui.net/)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.txt)

[![Docs: GitHub Pages](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://purelogiccode.github.io/SimpleLauncher/)
[![Wiki](https://img.shields.io/badge/docs-GitHub%20Wiki-blue)](https://github.com/purelogiccode/SimpleLauncher/wiki)
[![Docs deploy](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/docs.yml/badge.svg)](https://github.com/purelogiccode/SimpleLauncher/actions/workflows/docs.yml)

# 🎮 Simple Launcher

**Simple Launcher** is an open-source emulator frontend. Designed for both casual gamers and power users, it provides a seamless interface to organize, browse, and launch your retro gaming collection with deep integration for modern emulators and RetroAchievements.

---

## 📚 Documentation

- **📖 Documentation site (GitHub Pages)** — [purelogiccode.github.io/SimpleLauncher](https://purelogiccode.github.io/SimpleLauncher/): [Quickstart](https://purelogiccode.github.io/SimpleLauncher/#/03-quickstart) · [Configuration](https://purelogiccode.github.io/SimpleLauncher/#/05-configuration) · [Bundled Tools](https://purelogiccode.github.io/SimpleLauncher/#/11-bundled-tools) · [Updater](https://purelogiccode.github.io/SimpleLauncher/#/16-updater) · [Testing](https://purelogiccode.github.io/SimpleLauncher/#/14-testing) · [Release Notes](https://purelogiccode.github.io/SimpleLauncher/#/17-release-notes). The site has a labeled side menu (also available on mobile) and a persisted dark/light theme.
- **📚 GitHub Wiki** — [github.com/purelogiccode/SimpleLauncher/wiki](https://github.com/purelogiccode/SimpleLauncher/wiki): the same guides in wiki form with the **same labeled side menu**, plus the [Emulator Parameters reference](https://github.com/purelogiccode/SimpleLauncher/wiki/parameters) with official emulator download links (the app links to this page too)
- **📝 Changelog** — [SimpleLauncher/WhatsNew.md](SimpleLauncher/WhatsNew.md), also shown in-app via **Help → Update History**

---

## 📸 Screenshots

![System Selection](screenshot.png)

![List Of Games in Grid Mode](screenshot2.png)

![List Of Games in List Mode](screenshot3.png)

![RetroAchievements Window](screenshot4.png)

![RetroAchievements Window](screenshot5.png)

---

## ✨ Feature List

### 🚀 Performance & Core Infrastructure
* **Unified Settings Database:** Both apps share one SQLite `settings.dat` (settings, favorites, play history, systems) with automatic migration of the legacy XML/dat files
* **MessagePack Engine:** High-speed MessagePack serialization for near-instant settings loading
* **Native ARM64 Support:** ARM64 builds of the app and bundled tools for Windows on ARM (Surface Pro, Snapdragon X Elite) alongside x64
* **Asynchronous Architecture:** Multi-threaded game scanning and validation with fluid UI — the Avalonia app shows the system-selection grid immediately and refreshes library counts in the background
* **Fast Startup:** System icons resolve through a short directory cache, and sidebar counts no longer block the first frame
* **Single Instance Enforcement:** Prevents resource conflicts by ensuring only one instance runs — the WPF and Avalonia apps share the guard, so only one of them runs at a time (launching the other brings the running one to the foreground)
* **Elevation Detection:** Automatically detects games requiring Administrator privileges
* **Startup Validation:** Detects if running from temporary folders and prompts for proper extraction
* **Secure Connections:** All metadata and image downloads use TLS 1.2/1.3
* **Resilient Saving:** Improved file lock handling with retry logic and exponential backoff

### 🎮 Deep Emulator Integration
* **Configuration Injection:** Manage and inject settings directly into 20+ popular emulators:
  - Ares, Azahar, Blastem, Cemu, Daphne, Dolphin, DuckStation, Flycast, MAME, Mednafen, Mesen, PCSX2, Raine, Redream, RetroArch, RPCS3, Sega Model 2, Stella, Supermodel, Xenia, Yumir
* **Universal CHD Support:** Built-in **CHDMounter** utility brings CHD support to emulators without native support — Dokan/WinFsp on Windows, FUSE 3 on Linux (falls back to on-the-fly conversion when FUSE is unavailable):
  - RPCS3, Xemu, Xenia, Cxbx-Reloaded, Mednafen, PCSX Redux, 4DO, Gens, Blastem, Yabause, Mesen, FinalBurn Neo, FinalBurn Alpha, Raine, CD-i Emulator, Tsugaru
* **On-the-Fly Mounting:** Launch games directly from compressed (`.zip`) or disk image (`.iso`, `.xiso`) files using **Dokan** integration (Windows); Xbox games in every SimpleXisoDrive container (`.iso`/`.xiso`/`.cso`/`.zar`, plus Xbox-ISO `.chd`) mount through **SimpleXisoDrive** for Xemu (virtual `image.iso`) and Cxbx-Reloaded (mounted `default.xbe`). On Linux the Avalonia app mounts ZIP/7z/RAR launches through **SimpleZipDrive** (FUSE 3) and Xbox images through **SimpleXisoDrive** (FUSE 3), mounts CHD images through **CHDMounter**/FUSE, and keeps temp-extraction and managed conversion as automatic fallbacks — no Dokan needed
* **Expert Mode:** Granular control over launch parameters, multiple ROM paths per system, and custom environment variables
* **Format Conversion:** Built-in converters for CHD, RVZ, XISO, 7z/Zip with integrity verification
* **File Mounting Strategies:** Advanced strategies for Xbox images (`.iso`/`.xiso`/`.cso`/`.zar`/`.chd`), CHD (mount or convert), ZIP/7z/RAR (mount or extract), DOSBox `imgmount`, PBP-to-CUE, and CHD-to-CUE conversions

### 🏆 RetroAchievements (RA) Integration
* **Rich User Profiles:** View RA stats, recently played games, and global rankings directly in UI
* **Auto-Credential Injection:** Automatically syncs RA login to supported emulators (PCSX2, DuckStation, PPSSPP, RetroArch)
* **Advanced Hashing:** Built-in logic for precise hashes for complex systems (PS1, Saturn, Dreamcast)
* **Hash-Based Compatibility Filter:** The RA filter button matches games by file hash (scanned in the background and cached per system in `%LocalAppData%\SimpleLauncher\RetroAchievementsHashes\`), replacing unreliable filename matching
* **Nintendo Wii Support:** Full support for Wii RetroAchievements
* **Expanded System Aliases:** Better matching for NEC PC-FX, SNES MSU1, and many more systems
* **RA Data Fetcher Tool:** Separate tool for fetching RetroAchievements data

### 📂 Library Management & Game Scanning
* **Dual View Modes:** Toggle between visual **Grid View** with customizable aspect ratios and detailed **List View** with sortable metadata
* **Modern Game Store Integration:** Automatic scanning for games from:
  - Microsoft Store (Windows Apps)
  - Steam
  - Epic Games Store
  - GOG
  - Amazon Games
  - Battle.net
  - EA App
  - Humble Bundle
  - itch.io
  - Rockstar Games
  - Uplay
* **Fuzzy Image Matching:** Intelligent algorithm to find cover art even with imperfect filename matches
* **Play History Tracking:** Detailed logs of play count, total playtime, and last played timestamps
* **Global Search:** Advanced search engine supporting `AND`/`OR` operators across entire multi-system library
* **Favorites System:** Mark and organize favorite games with persistent storage
* **Gamepad Navigation:** Full navigation support for Xbox and PlayStation controllers
* **Pagination System:** Efficient handling of large game libraries with batch loading

### 🎨 Customization & UI Features
* **Multiple Themes:** Beautiful **Midnight** (Deep Blue) theme, **High Contrast** mode, and Adaptive mode syncing with Windows Light/Dark settings
* **Filename Preferences:** "Clean Up" filenames in Grid View or hide filenames entirely for pure box-art look
* **Font Size Control:** Customize filename and machine name font sizes in game buttons
* **Separate Thumbnail Sizes:** Independent zoom controls for system selection and game view
* **Aspect Ratio Control:** Customize game button aspect ratios
* **Sound Configuration:** Customizable sound effects for UI interactions
* **Tray Icon Support:** Minimize to system tray with quick access
* **Right-Click Context Menus:** Extensive context menu options for games and systems
* **Filter Menu System:** Advanced filtering options for game libraries

### 🛠️ Bundled Power Tools
Simple Launcher includes a comprehensive suite of specialized utilities:

* **CHDMounter:** Automatically handles CHD file mounting for emulators without native support
* **Format Converters:**
  - CHDStudio: Convert, verify, and explore CHD images (Windows/Linux)
  - RVZStudio: Convert, verify, and explore GameCube/Wii disc images (Windows/Linux)
  - XISOStudio: Convert, verify, and explore Xbox/Xbox 360 images (Windows/Linux)
  - BatchConvertToCompressedFile: Convert to 7z/Zip format
* **Metadata & Cover Tools:**
  - FindRomCover: Intelligent cover art finder
  - RetroGameCoverDownloader: Download retro game covers
* **Batch File Creators:**
  - CreateBatchFilesForPS3Games: PS3 game launchers
  - CreateBatchFilesForScummVMGames: ScummVM game launchers
  - CreateBatchFilesForWindowsGames: Windows game launchers
  - CreateBatchFilesForXbox360XBLAGames: Xbox 360 XBLA launchers
* **Validation & Organization:**
   - RomValidator: Validate ROMs against No-Intro DAT files
   - MAME.DatCreator: Create MAME DAT files
   - SimpleZipDrive: ZIP file mounting utility
   - SimpleXisoDrive: Xbox image mounting utility (XISO/CISO/ZAR/CHD)
* **File System Tools:**
  - 7z integration for compression/decompression

### 🔧 Advanced Features
* **Easy Mode:** One-click automatic download and configuration of emulators for chosen systems
* **System Selection Screen:** Visual system selection with customizable thumbnails
* **Gamepad Dead Zone Configuration:** Customize controller dead zones for precise navigation
* **Fuzzy Matching Configuration:** Adjust matching thresholds for cover art and metadata
* **Play Time Tracking:** Refactored from strings to seconds for better accuracy
* **Global Statistics:** View usage statistics and play time across all systems
* **Image Viewer:** Built-in image viewer for game art and screenshots
* **Debug Window:** Advanced debugging and error reporting interface
* **Update System:** Built-in updater with a resilient update check (GitHub `releases/latest` with multi-source fallback to the secondary server when GitHub is unreachable or rate-limited) and one shared `Updater.exe` that updates either app (WPF or Avalonia) and relaunches the app that started it. Updates are refused when the app runs from a WinRAR/7-Zip temporary folder, interrupted swaps are recovered safely, and transient file locks/access denials are retried
* **Multi-language Support:** 18 languages with localization system
* **Accessibility Features:** Automation properties, access keys, and high contrast mode

### 📊 Supported Systems (100+ Systems)
The launcher supports an extensive range of gaming systems including:
- **Nintendo:** NES, SNES, N64, GameCube, Wii, Wii U, Switch, Game Boy, GBC, GBA, DS, 3DS, Virtual Boy, Famicom, Satellaview, 64DD
- **Sony:** PlayStation 1-4, PSP, PS Vita
- **Microsoft:** Xbox, Xbox 360, Windows, DOS
- **Sega:** Master System, Genesis, Saturn, Dreamcast, Game Gear, SG-1000, Model 3, Naomi, Naomi 2, Atomiswave
- **Arcade:** MAME, FinalBurn Neo, FinalBurn Alpha, Raine
- **Retro Computers:** Amiga, Amstrad CPC, Commodore 64, Commodore 128, ZX Spectrum, MSX, MSX2, Sharp X68000, NEC PC-88
- **Classic Consoles:** Atari 2600-7800, Jaguar, Lynx, ColecoVision, Intellivision, Neo Geo, Neo Geo CD, TurboGrafx-16, PC Engine, 3DO, CD-i
- **Handheld/Mobile:** J2ME (Java ME)
- **Modern Platforms:** ScummVM, Windows Store games, Xbox 360 XBLA

### 💿 CHD Support Matrix
| Emulator            | CHD Support | Notes         |
|---------------------|-------------|---------------|
| 4DO                 | ✅ Added | 3DO           |
| Blastem             | ✅ Added | Sega Genesis  |
| CD-i Emulator       | ✅ Added | Philips CD-i  |
| Cxbx-Reloaded       | ✅ Added | Original Xbox |
| DOSBox and variants | ✅ Added | Microsoft DOS |
| FinalBurn Alpha     | ✅ Added | Arcade        |
| FinalBurn Neo       | ✅ Added | Arcade        |
| Gens                | ✅ Added | Sega Genesis  |
| Kega Fusion         | ✅ Added | Sega Genesis  |
| Mednafen            | ✅ Added | Multi-system  |
| Mesen               | ✅ Added | NES, SNES     |
| PCSX-Redux          | ✅ Added | PlayStation 1 |
| Raine               | ✅ Added | Arcade        |
| RPCS3               | ✅ Added | PlayStation 3 |
| Tsugaru             | ✅ Added | FM Towns      |
| Xemu                | ✅ Added | Original Xbox |
| Xenia               | ✅ Added | Xbox 360      |
| Yabause             | ✅ Added | Sega Saturn   |

---

## 📥 Installation

1. **Download:** Grab the latest release for your architecture (x64 or ARM64) from the [Releases Page](https://github.com/purelogiccode/SimpleLauncher/releases).
2. **Extract:** Unzip the contents into a **writable folder** (e.g., `C:\Games\SimpleLauncher`).
   * *Note: Do not install in `C:\Program Files` to avoid permission issues.*
3. **Run:** The single zip contains both apps next to each other — `SimpleLauncher.exe` (WPF) and `SimpleLauncher.Avalonia.exe` (Avalonia). Run whichever you prefer; they share the same content and settings files.
4. **Prerequisites:**
   * Install **Dokan** from [GitHub](https://github.com/dokan-dev/dokany/releases) for on-the-fly file mounting.
   * Ensure you have the [.NET 10 **Desktop** Runtime](https://dotnet.microsoft.com/download) installed (both apps are framework-dependent).
5. **Linux (beta):** download `release_{version}_linux-x64.zip` (or `linux-arm64`), install the [.NET 10 Runtime](https://dotnet.microsoft.com/download) and `libasound2` (optional: `libsndfile1` for FLAC/Ogg/Opus UI sounds), then run `./SimpleLauncher.Avalonia`. Mounting uses **FUSE 3** (`libfuse3` + `/dev/fuse`) instead of Dokan. Linux support is **beta** — issues are welcome on GitHub.

---

## 🚦 Getting Started

1. **Easy Mode:** Click the **Easy Mode** menu item to automatically download and configure emulators for your chosen systems.
2. **Add Games:** Place your ROMs and cover images in the folders designated during setup.
3. **Select System:** Use the visual **System Selection Screen** to pick your platform.
4. **Play:** Click a game cover to launch. Use a Gamepad (Xbox or PlayStation) to navigate the entire interface.

---

## 🌐 Localization

Simple Launcher is translated into **18 languages**:
* English, Spanish, French, German, Italian, Portuguese (BR), Russian, Chinese (Simplified), Japanese, Korean, Arabic, Bengali, Hindi, Indonesian, Dutch, Turkish, Urdu, and Vietnamese.

---

## 💻 Technical Specifications

* **Framework:** .NET 10 (WPF + Avalonia)
* **Language:** C# 14
* **Data Serialization:** Unified SQLite `settings.dat` (settings, favorites, play history, systems), MessagePack (RA/MAME/history databases) & XML (legacy migration)
* **Dependencies:** MahApps.Metro (UI), SharpDX (Input), SharpCompress (Archives), DokanNet (Mounting), NAudio 3 + NLayer (Audio; managed MP3/WAV on Linux/macOS), YamlDotNet, Tomlyn, PBPSharp — RetroAchievements hashing is delegated to the bundled `RetroAchievementsSharp` CLI tool (`tools\RetroAchievementsSharp\`)
* **Architecture:** Modular service-based architecture with dependency injection
* **Storage:** Unified SQLite `settings.dat` shared by both apps; legacy `settings.xml` / `favorites.dat` / `playhistory.dat` / `system.xml` are migrated automatically on first launch

---

## 🤝 Contributing & Support

* **Bug Reports:** Use the built-in **Support Window** to send detailed error reports directly to the developers.
* **Documentation:** [Documentation site](https://purelogiccode.github.io/SimpleLauncher/) and [GitHub Wiki](https://github.com/purelogiccode/SimpleLauncher/wiki) — guides, bundled-tool usage, and `parameters.md` with official emulator download links.
* **Donate:** If you find this project useful, consider [supporting the developer](https://www.purelogiccode.com/donate).

**⭐ If you like this project, please give us a star on GitHub! ⭐**

---

## 📜 License

This project is licensed under the GPLv3 License – see the [LICENSE](LICENSE.txt) file for details.

*Simple Launcher is an emulator frontend and does not provide ROMs, ISOs, or BIOS files.*