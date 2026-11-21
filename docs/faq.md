# FAQ

Short answers to the questions that come up most often. For step-by-step setup, see the
[Quickstart](03-quickstart.md). For how the app compares with other frontends, see
[Comparison](comparison.md). For data handling, see [Privacy](privacy.md).

## What is Simple Launcher?

An open-source emulator frontend for Windows, Linux and macOS. It organizes ROMs, downloads and
configures emulators through Easy Mode, launches games with gamepad or mouse/keyboard, and adds
RetroAchievements, play history, favorites, store-game scanning on Windows, and a set of bundled
conversion/mounting tools.

It is free software under the GPLv3. There are no ads and no account.

## Does it include ROMs, ISOs or BIOS files?

No. Simple Launcher does not provide games, BIOS files, or ROMs. Use your own legally obtained
dumps. Easy Mode can download emulators, cores and image packs, but not games.

## Which app does the Windows release install: WPF or Avalonia?

The release ships the **Avalonia app as `SimpleLauncher.exe`**. The original WPF app remains in
the source tree as a reference implementation but is no longer bundled or installed. Existing
WPF and Avalonia installs migrate to the new `SimpleLauncher.exe` through the updater, keeping
the same `settings.dat` and user data.

## What do I need installed?

- **Windows:** the [.NET 11 runtime](https://dotnet.microsoft.com/download). The
  **installer** downloads and installs it when missing (administrator approval is needed for
  that step only). The **portable ZIP** needs it already installed; the app itself needs no
  admin rights.
- **Linux portable (ZIP/tar.gz):** the .NET 11 runtime. The **AppImage** and **Flatpak** bundle it.
- **macOS:** nothing extra; the Apple Silicon DMG bundles the runtime.

Optional: **Dokan** on Windows for on-the-fly ZIP/ISO/Xbox-image mounting, and **FUSE 3**
(`libfuse3` + `/dev/fuse`) on Linux for ZIP/7z/RAR and Xbox-image mounts. Without them the app
falls back to extraction or conversion.

## How do I add my first system?

Launch the app with no systems configured and the first-run wizard opens:

1. **Welcome** - Next.
2. **Add System** - pick a system; Easy Mode downloads its emulator and core (and an optional
   image pack), then Add System.
3. **Finish** - the new system appears in the main window.

You can Skip the wizard and use **Easy Mode** from the menu later, or configure everything
manually in **Edit System** (Expert Mode).

## Where are my settings, favorites and saves?

Settings, favorites, play history and system definitions live in `settings.dat`:

- Windows: `%LocalAppData%\SimpleLauncher` (the installer default, and also the data folder) or
  next to the exe in portable mode
- Linux: `~/.local/share/SimpleLauncher`
- macOS: `~/Library/Application Support/SimpleLauncher`

Saves and save states belong to each emulator and stay in that emulator's own folders. Simple
Launcher does not upload them. See [Configuration](05-configuration.md) for the full data map.

## Does it work offline?

Yes for your local library: browsing, launching, favorites, play history and emulator
configuration work without a connection. Easy Mode catalog/downloads, cover-art lookups,
RetroAchievements and update checks need internet.

## Is my data private?

The app has no account, no ads, and does not sell data. It sends a small startup/launch ping,
automatic warning/error diagnostics, and feature-specific requests (Easy Mode, cover lookups,
RetroAchievements, support). The [Privacy Policy](privacy.md) lists exactly what is sent and how
to disable automatic bug reports.

## How do I update?

- **Windows and portable Linux:** use the built-in updater (**Check For Update**). It downloads
  and swaps the app, then relaunches it.
- **AppImage:** the AppImage replaces itself in place.
- **Flatpak:** update through your software center (or install a newer `.flatpak`).
- **macOS:** download the next DMG and drag the app to Applications; user data is kept.

## How do I uninstall?

- **Windows installer:** **Settings → Apps → Installed apps → Simple Launcher → Uninstall**, or
  the **Uninstall** shortcut in its Start Menu group. The uninstaller removes the program files
  but keeps `settings.dat`, `system.xml`, logs and game media. To remove everything, delete the
  install folder (default `%LocalAppData%\SimpleLauncher`) after uninstalling.
- **Portable Windows ZIP:** delete the extracted folder. In portable mode the data is inside it,
  so copy `settings.dat` / `system.xml` first if you want to keep it.
- **Linux/macOS:** delete the app, then remove `~/.local/share/SimpleLauncher` or
  `~/Library/Application Support/SimpleLauncher` to delete your settings.

## Why does Windows SmartScreen warn me?

The builds are not code-signed yet, so Windows shows "Unknown publisher" for the installer and
the apps. Download only from the
[official GitHub Releases](https://github.com/purelogiccode/SimpleLauncher/releases) page, then
choose **More info -> Run anyway** if you trust the source. Newer releases include
`SHA256SUMS.txt`; compare its hash with `Get-FileHash` (Windows) or `sha256sum -c` before running
the file. Do not use third-party download sites.

## Can I use it on a Steam Deck?

Yes - the Linux **AppImage** is the simplest option, or the **Flatpak**. Flatpak runs emulators
on the host through `flatpak-spawn`, but FUSE-based image mounting is not available inside the
sandbox, so mounts fall back to extraction/conversion. See
[Linux packages](19-linux-packages.md).

## Which systems are supported?

Over 100 systems, from Atari 2600 and NES through PlayStation 4, Xbox 360, arcade boards, retro
computers, ScummVM, and Windows store games. The full list and per-system emulator parameters are
in the [emulator parameter reference](18-emulator-parameters.md).

## How do I report a bug?

Use the in-app **Support Window** for a support request, or open an issue on
[GitHub](https://github.com/purelogiccode/SimpleLauncher/issues). Warning/error diagnostics are
sent automatically unless `SIMPLELAUNCHER_BUGREPORT_DISABLE=1` is set. Include the app version,
OS, and steps to reproduce; see [Privacy](privacy.md) for what the automatic report contains.

## Why is the macOS app unsigned?

There is no Apple Developer Program membership, so the DMG is ad-hoc signed only. On first launch,
right-click the app and choose **Open**, or run:

```bash
xattr -dr com.apple.quarantine "/Applications/Simple Launcher.app"
```

See [macOS package](20-macos-packages.md) for details.
