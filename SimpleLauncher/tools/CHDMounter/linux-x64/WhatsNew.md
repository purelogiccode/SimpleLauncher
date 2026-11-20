# What's New

## 1.4.0

### Added

- **CUE/BIN RAW 2448 (`cuebin2448`) for CD+G discs** - new virtual export mode that keeps
  the 96 bytes of subchannel data stored after each 2352-byte sector, producing full
  2448-byte CD frames. The generated CUE marks audio tracks as `CDG` and data tracks as
  `MODE1/2448`/`MODE2/2448`. The existing 2352/2048 export modes are unchanged.
- **38 console/format modes** - the new CD+G mode is available from the console dropdown
  and as the `cuebin2448` command-line alias.
- **Direct CHDSharp 1.6.3 dependency** - full-frame reads (sector data plus subcode) and
  the latest decoder fixes.
- **Repository automation** - Dependabot, CodeQL, dependency review, release/CI bundle
  validation, security policy and agent guidance.

### Changed

- The mount UI, command line and all three mount backends now share a disc-volume
  abstraction, so CHDMounter-specific virtual exports can be mounted alongside parsed
  file systems.
- NuGet dependencies updated: Avalonia 12.1.5, LibFuseSharp 1.0.1, Meziantou.Analyzer
  3.0.322, Microsoft.NET.Test.Sdk 18.11.0, System.Security.Cryptography.ProtectedData
  10.0.14 and winfsp.net 2.2.26286.
- GitHub Actions workflows updated to the current action major versions.

### Fixed

- **Mounting at an existing file** now reports a clear user error instead of an
  unhandled `IOException`; WinFsp preparation also rejects existing files.
- **Console dropdown labels** now show the format display name instead of the record's
  `ToString()` output.
- **Disposal and state handling** - parsed containers are disposed when mounting fails,
  the FUSE mounted flag is synchronized, screenshot bitmaps are disposed on exceptions,
  and `Path.Join` avoids silently dropping base folders.
- **Workflow validation and docs cleanup** - the reusable docs job no longer uses an
  unsupported `timeout-minutes`, and the docs build creates the NuGet cache folder that
  `setup-dotnet` expects during post-job cleanup.
- **Code scanning** - fixed the actionable CodeQL findings and scoped analysis to the
  shipping application; the open alert count is now zero.

### Bundles

| Bundle | Platform | Architecture |
|--------|----------|--------------|
| `release_1.4.0_win-x64.zip` | Windows | x64 |
| `release_1.4.0_win-arm64.zip` | Windows | ARM64 |
| `release_1.4.0_linux-x64.zip` | Linux | x64 |
| `release_1.4.0_linux-arm64.zip` | Linux | ARM64 |
| `release_1.4.0_osx-x64.zip` | macOS | Intel |
| `release_1.4.0_osx-arm64.zip` | macOS | Apple silicon |

Every bundle contains `CHDMounter` (`CHDMounter.exe` on Windows), `LICENSE.txt`, `README.md` and
`WhatsNew.md`, and requires the **.NET 10 runtime**.

## 1.3.1

### Fixed

- **WinFsp folder mounts failed and were unreadable** — WinFsp creates the folder mount
  point itself and rejects an existing directory with `STATUS_OBJECT_NAME_COLLISION`, yet
  the app pre-created it, so every elevated (cross-integrity) mount failed. The directory
  is no longer pre-created (only an empty leftover is cleaned up). The cross-integrity
  security descriptor was also ownerless, which Windows' `AccessCheck` rejects with
  "The security descriptor structure is invalid", leaving mounted drives unreadable; both
  builders now share a descriptor with owner/group and Full Access for SYSTEM,
  Administrators and Everyone.
- **FUSE layer is now a NuGet package** — the vendored `FuseSharp` sources were replaced by
  the [`LibFuseSharp`](https://github.com/purelogiccode/LibFuseSharp) 1.0.0 package
  (MIT, `net8.0`/`net9.0`/`net10.0`), including the mount-exit, `ENOTDIR` and quiet-probe
  improvements.

### Changed

- **Main window title is now just `CHDMounter`** (it no longer appends the mount core).
- **Help → Donate** menu item opens the donation page.
- **About → FuseSharp** now credits the standalone `LibFuseSharp` library.
- **README badges** expanded (platforms, architecture, mount cores, docs, community).

### Bundles

| Bundle | Platform | Architecture |
|--------|----------|--------------|
| `release_1.3.1_win-x64.zip` | Windows | x64 |
| `release_1.3.1_win-arm64.zip` | Windows | ARM64 |
| `release_1.3.1_linux-x64.zip` | Linux | x64 |
| `release_1.3.1_linux-arm64.zip` | Linux | ARM64 |
| `release_1.3.1_osx-x64.zip` | macOS | Intel |
| `release_1.3.1_osx-arm64.zip` | macOS | Apple silicon |

Every bundle contains `CHDMounter` (`CHDMounter.exe` on Windows), `LICENSE.txt`, `README.md` and
`WhatsNew.md`, and requires the **.NET 10 runtime**.

## 1.3.0

### Fixed

- **Settings and About windows were unresponsive** — every modal dialog was opened by
  blocking the UI thread on the dialog task, so the window rendered but never processed
  input: the settings check-boxes/radio buttons could not be toggled and the About links
  (and the update banner) did nothing. Dialogs are now awaited, so the UI stays responsive
  while a dialog is open.
- **Absolute Linux/macOS paths were dropped from the command line** — the parser treated
  any argument starting with `/` as a flag, so `/home/user/game.chd` was ignored and nothing
  mounted. Only the documented `/l`, `/a` and `/s:<alias>` flags are recognised now.
- **F8 screenshots in single-file builds** were written to the .NET extraction directory
  instead of the application folder; the screenshot folder is now resolved from the running
  executable's path.
- **SIGTERM/SIGHUP/Ctrl+C left the GUI running** after unmounting; the application now shuts
  down cleanly on these signals (documented in the usage guide).
- **Bug-report queue could stall** — a race in the queue hand-off could pin a thread at 100%
  CPU and stop all later reports from being sent.
- **Window could not be closed** if unmounting kept failing; the closing path now gives up
  after one attempt instead of retrying forever.

### Changed

- **One application instead of two** — the Dokan, WinFsp and Core projects are merged into a
  single `CHDMounter` application. There is now one download per platform/architecture
  (`release_<version>_<rid>.zip`) instead of separate Dokan and WinFsp bundles.
- **Cross-platform UI** — the WPF interface was ported to Avalonia with the same dark theme and
  layout, and the application now runs on Windows, Linux and macOS (x64 and ARM64).
- **Selectable mount core** — choose Dokan (default) or WinFsp on Windows in
  **Settings → Mount core**. Linux and macOS mount through FUSE 3 (libfuse3 / macFUSE) using the
  bundled `FuseSharp` library.
- **Per-platform settings** — settings stay DPAPI-encrypted on Windows and are stored as plain
  JSON on Linux/macOS.
- **VideoGameFileSystemParser is now a NuGet package** — the vendored parser project was removed
  from the solution; `CHDMounter` references the `VideoGameFileSystemParser` 1.4.0 package.

### Bundles

| Bundle | Platform | Architecture |
|--------|----------|--------------|
| `release_1.3.0_win-x64.zip` | Windows | x64 |
| `release_1.3.0_win-arm64.zip` | Windows | ARM64 |
| `release_1.3.0_linux-x64.zip` | Linux | x64 |
| `release_1.3.0_linux-arm64.zip` | Linux | ARM64 |
| `release_1.3.0_osx-x64.zip` | macOS | Intel |
| `release_1.3.0_osx-arm64.zip` | macOS | Apple silicon |

Every bundle contains `CHDMounter` (`CHDMounter.exe` on Windows), `LICENSE.txt`, `README.md` and
`WhatsNew.md`, and requires the **.NET 10 runtime**.

## 1.2.1

### Fixed

- **WinFsp variant could never mount** — the single-file release did not extract its content, so
  `Fsp.Interop.Api`'s static initializer failed with
  `The type initializer for 'Fsp.Interop.Api' threw an exception` even when WinFsp was installed
  (reported as bugs 67653–67656 and 67777/67778). The WinFsp bundle now extracts its content to
  disk so the WinFsp.NET bindings can locate `winfsp-msil.dll` and initialize correctly.
- **WinFsp detection is now a full compatibility check** — the app loads the native WinFsp DLL,
  resolves every entry point the bindings need, and verifies the native/managed version match
  *before* attempting a mount. Missing or outdated WinFsp now shows an actionable
  "install or repair WinFsp" dialog instead of failing mid-mount.
- **ARM64 WinFsp** — the native DLL probe now selects `winfsp-a64.dll` on ARM64 instead of
  looking for the x64 DLL and reporting WinFsp as missing.
- **Mount-point folder creation is retried** — a folder mount point that cannot be created no
  longer aborts the remaining drive-letter/folder candidates.
- **ISO 9660 / PC-FX multi-extent matching** — continuation records must match byte-for-byte
  (ordinal). Case-insensitive matching could merge two distinct Joliet names into one file.
- **HFS/HFS+ catalog deduplication** — deduplication is now ordinal, so HFSX volumes with
  names that differ only by case keep all entries.
- **VideoGameFileSystemParser public API** — `FileEntry.Extents` and `SectorReader.Tracks`
  are `List<T>` again (they had been changed to `IList<T>`, breaking external consumers).
- **Bug-report noise** — known test hosts (`testhost`, `vstest`, `ReSharperTestRunner`,
  `xunit.console`, NUnit, MSTest) can never file production bug reports, while the
  `CHDMounter_Tester` application keeps its diagnostics.
- **User-error handling** — missing drivers, wrong console type, busy mount points and similar
  environment problems are logged without filing a bug report; genuinely unexpected mount and
  unmount failures are still reported with full stack traces.

### Changed

- **Release bundles are framework-dependent** — the .NET runtime is no longer embedded.
  Each bundle contains a single `CHDMounter.exe` plus `LICENSE.txt`, `README.md` and
  `WhatsNew.md`, and requires the **.NET 10 Desktop Runtime**.
- **Update checker** now points at the canonical repository `purelogiccode/CHDMounter`.
- **The Tester application is no longer published** in releases.

### Bundles

| Bundle | Architecture | Driver |
|--------|--------------|--------|
| `release_1.2.1_Dokan_win-x64.zip` | x64 | [Dokan](https://github.com/dokan-dev/dokany/releases) |
| `release_1.2.1_Dokan_win-arm64.zip` | ARM64 | [Dokan](https://github.com/dokan-dev/dokany/releases) |
| `release_1.2.1_WinFsp_win-x64.zip` | x64 | [WinFsp](https://github.com/winfsp/winfsp/releases) |
| `release_1.2.1_WinFsp_win-arm64.zip` | ARM64 | [WinFsp](https://github.com/winfsp/winfsp/releases) |

## 1.2.0

- Added the WinFsp variant alongside the Dokan variant.
- 37 console/format modes, virtual CUE/BIN/ISO/WAV export, SingleFile ISO passthrough.
- Console selection is alias-only (e.g. `ps2`, `cuebin2352`).
- Vendored `VideoGameFileSystemParser` as a project reference.
- Added the CHDMounter Tester batch-parsing/benchmarking application.
- Bug-report API, logging, settings and mount diagnostics improvements.

## 1.1.0

- Added diagnostics logging, update checker and statistics reporting.
- Wildcard enumeration fixes on mounted virtual drives.
- Removed the CHDSharp dependency from the application (now via VideoGameFileSystemParser).
- README and release process improvements.

## 1.0.0

- First public release: mount CHD images as virtual read-only drives with Dokan.
- ISO 9660, UDF, XDVDFS, OperaFS, CD-i Green Book and HFS/HFS+ support.
