# How Simple Launcher compares

> **Last verified: 2026-11-20 (Simple Launcher installer); competitor data verified 2026-11-19.**
> Compared versions: Simple Launcher (this repository), LaunchBox 14.1, ES-DE 3.5.0,
> RetroBat 8.3.1.1, Playnite 10.65. Competitor features and prices change; check their sites
> before deciding.

Simple Launcher is an open-source emulator frontend that also imports PC store games. The other
projects in this table are good at different things, and this page says so. "Better" depends on
whether you want a cross-platform desktop launcher, a TV/arcade interface, or a PC game library.

## At a glance

| | **Simple Launcher** | **LaunchBox / Big Box** | **ES-DE** | **RetroBat** | **Playnite** |
|---|---|---|---|---|---|
| **Price and licence** | Free, GPLv3 | Free starter; Premium **$30** (1 year updates) or **$75** lifetime; Big Box requires Premium | Free desktop (MIT); Android release is paid | Free (donation-supported) | Free, MIT |
| **Platforms** | Windows x64/ARM64, Linux x64/ARM64, macOS 15+ Apple Silicon | Windows; Android is a separate app | Windows, Linux, macOS, Android | Windows 8.1/10/11 (64-bit) | Windows (Linux version announced, not in the current stable release) |
| **Emulator setup** | **Easy Mode** downloads emulator + core + image pack per system; Expert Mode for manual setups | Import wizards, emulator management, guided emulator installs | Preconfigured profiles; you install the emulators | Bundles and preconfigures emulators, cores, bezels and controllers | Emulator profiles and extensions; mostly manual setup |
| **PC storefront library** | Scans 11 stores on Windows (Steam, Epic, GOG, Microsoft Store, Amazon, EA, Battle.net, Humble, itch.io, Rockstar, Ubisoft) | Imports Steam, GOG, Epic, Amazon, EA, Ubisoft, Xbox; storefront plugins | Launches ports/store shortcuts; not a storefront library manager | Optional Steam/Epic/GOG/Amazon/EA scans | Best-in-class storefront imports and account linking |
| **RetroAchievements** | Built in: profile, hash-based filter, automatic credential injection (Windows) | Built in via bundled plugin: progress, badges, filters, Big Box views | Not built in (as of 3.5.0); achievements stay in the emulator | Built in: login, game indexing, hardcore, profile | Via third-party extension |
| **Interface** | Desktop grid/list, gamepad navigation, 3 built-in themes | Desktop UI + **Big Box 10-foot mode**, large theme/plugin ecosystem | Full-screen 10-foot UI, theme downloader | Full-screen ES-DE UI, themes, bezels, video previews | Desktop + full-screen mode, large theme/extension ecosystem |
| **Metadata and media** | Cover art + RetroAchievements data; no video/manual scraper | Games Database, EmuMovies videos/manuals, 3D boxes, Reader | Built-in scraper for images, videos and manuals | ScreenScraper and media scraping | IGDB metadata and extensions |
| **Extensibility** | No plugin API | Plugins and themes | Custom systems/scripts and themes | Config/scripts and themes | Large plugin/theme ecosystem |
| **Updates** | Built-in on Windows/portable Linux; AppImage self-updates; Flatpak via software center; macOS manual | Built-in | AppImage updater; Windows/macOS download and install | Built-in updater | Built-in |
| **Best for** | Cross-platform users who want one-click emulator setup, RetroAchievements, and store games in one desktop app | Windows couch/arcade setups that want polish and a big community | Cross-platform, hands-on retro libraries and Steam Deck | A Windows all-in-one retro console with minimal setup | A PC game library first, with emulation added on |

## Where Simple Launcher is stronger

- **Cross-platform desktop, including Linux ARM64 and Apple Silicon.** The same Avalonia app runs
  on Windows, Linux and macOS. ES-DE is also cross-platform (and adds Android); LaunchBox,
  RetroBat and Playnite are Windows-first.
- **One-click per-system setup without a full bundle.** Easy Mode downloads the emulator, core and
  optional image pack, so you do not assemble RetroArch cores by hand as ES-DE, LaunchBox and
  Playnite typically expect. RetroBat also bundles everything, but only on Windows.
- **Both retro and PC libraries, free.** It scans 11 storefronts on Windows *and* manages emulated
  systems with one search, favorites list and play history - without a paid tier. LaunchBox and
  Playnite also combine both, but LaunchBox gates its TV mode behind Premium.
- **RetroAchievements is first-class on every platform** for profiles and hash-based filtering;
  automatic credential injection is Windows-only today. ES-DE has no built-in RA; Playnite
  needs an extension; LaunchBox and RetroBat are Windows-only.
- **Bundled runtime helpers** for CHD/Xbox/archive mounting, 7-Zip extraction and
  RetroAchievements hashing, without shipping a suite of optional GUI apps.
- **Lightweight, portable and GPLv3.** A portable ZIP or a per-user Windows installer, no account,
  no ads.

## Where the others are stronger

- **No Big Box-style TV mode.** Simple Launcher is a desktop window with gamepad navigation; it
  does not have a dedicated 10-foot/couch interface. LaunchBox Big Box and ES-DE are built for
  that.
- **No theme or plugin marketplace.** There are three built-in themes and no plugin API; LaunchBox
  and Playnite have large community ecosystems.
- **No video/manual scraper.** Cover art and RetroAchievements data are supported, but LaunchBox
  and ES-DE download videos, manuals and richer metadata.
- **Windows store scanning only.** On Linux and macOS the store scanner returns no results.
- **Smaller project and community.** LaunchBox, ES-DE, RetroBat and Playnite have years of
  community content, tutorials and testing.
- **macOS build is unsigned and not notarized.** Gatekeeper requires a right-click -> Open on
  first launch.
- **No winget or Store package yet.** The Windows installer is per user and bootstraps the
  .NET 11 Desktop Runtime; winget and MSIX are still planned. The portable ZIP still needs the
  runtime installed manually.
- **Flatpak is sandboxed.** FUSE-based ZIP/CHD/Xbox-image mounts are unavailable inside Flatpak,
  so the app falls back to extraction/conversion.

## Notes on the comparison

- LaunchBox's free tier is capable; Big Box, some theming options and some automation require
  Premium. The prices above are the Windows licence prices at the verification date.
- ES-DE's desktop releases are free and open source; the Android release is sold separately.
  RetroAchievements support has an open feature request, so achievements are handled by the
  emulator (for example RetroArch) rather than the frontend.
- RetroBat is an all-in-one Windows distribution: its value is the bundled, preconfigured
  emulator stack. It is not a cross-platform desktop app.
- Playnite's strength is PC storefront library management. Emulation and RetroAchievements come
  from profiles/extensions rather than the core app. A Linux version has been announced but is
  not in the current stable release.
- Feature counts and "systems supported" are deliberately omitted: each project counts
  differently, and the numbers change every release.

## Sources

- LaunchBox: [product site](https://www.launchbox-app.com/), [Premium pricing](https://www.launchbox-app.com/premium), [14.1 changelog](https://www.launchbox-app.com/about/changelog), [RetroAchievements guide](https://feedback.launchbox-app.com/help/articles/8691984-retroachievements-in-launchbox)
- ES-DE: [product site](https://es-de.org/), [user guide](https://gitlab.com/es-de/emulationstation-de/-/raw/master/USERGUIDE.md), [RetroAchievements feature request](https://gitlab.com/es-de/emulationstation-de/-/work_items/876)
- RetroBat: [product site](https://www.retrobat.org/), [wiki](https://wiki.retrobat.org/), [RetroAchievements setup](https://wiki.retrobat.org/advanced-features/retroachievements)
- Playnite: [product site](https://playnite.link/), [releases](https://github.com/JosefNemec/Playnite/releases)
- Simple Launcher: [README](../README.md), [supported systems and emulator parameters](18-emulator-parameters.md), [store scanning](10-game-scanning.md)
