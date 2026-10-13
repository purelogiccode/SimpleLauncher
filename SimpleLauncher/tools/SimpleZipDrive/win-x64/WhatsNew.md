# What's New

## 3.1.1

### Fixed
- **The Avalonia render-pass crash (bug reports #68051/#68052) is no longer reported as an application bug.** Applying a control template while a `TextBlock` containing inline controls was measured made Avalonia invalidate a visual during the render pass. The About window no longer embeds its links inline, and the framework exception is now classified as an environment condition so stale builds cannot flood the bug-report API with it.
- **Opening the browser no longer produces duplicate bug reports on systems with a broken URL association (bug reports #68197/#68198).** When shell execution failed (*"Class not registered"* - e.g. clicking a driver download link on a machine with a partially broken shell registration), the failure was reported twice: once as a warning carrying the exception and once as an error carrying only the message. On Windows the URL is now retried through the URL protocol handler (`rundll32 url.dll,FileProtocolHandler`), shell-launch failures are classified as environment conditions, and each failure reaches the API at most once (the in-app mirror is informational).

### Internal
- Dependency updates: Avalonia 12.1.4, SharpCompress 0.50.5, Meziantou.Analyzer 3.0.299 and xunit.runner.visualstudio 4.0.1; the separate Roslynator code-analysis and formatting packages were consolidated into the single `Roslynator.Analyzers` 5.0.1 package.
- Documentation refresh for the 3.1.1 release (version references and the packaging guide); the developer review notes file `Todo.md` was removed from the repository.
- The suite now contains **1,466 tests** and stays analyzer-warning-clean.

## 3.1.0

### Added
- **Linux and macOS support - one application, three filesystem backends.** The former Dokan and WinFsp executables are merged into a single cross-platform Avalonia app: **Dokan** (the Windows default) or **WinFsp** on Windows and **FUSE** (libfuse3 on Linux, macFUSE on macOS) everywhere else. Install either Windows driver and pick it in Settings (Auto prefers WinFsp when installed). Folder mounts work on every platform; drive-letter and cross-integrity mounts remain Windows-only. Six release bundles are produced: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.
- **Missing-driver warning with a download offer.** On an interactive start the app checks the driver required by the selected backend (Dokan/WinFsp on Windows, libfuse3/macFUSE elsewhere) and, when it is missing, shows a message box explaining what is needed and asking whether to open the matching download page.
- **The 7-Zip extraction fallback now works on every platform.** Instead of loading a native `7z.dll`, the app runs a bundled 7-Zip command-line binary - `7za.exe` on Windows, statically linked `7zzs` on Linux, universal `7zz` on macOS - streamed straight into the cache. Entry names are mapped through `7z l -slt`, so backslash-separated and wildcard-containing names (`[`, `]`) extract correctly; standard input is closed so a missing password can never hang a prompt.
- **Screenshot fallback location.** Pressing `F8` saves to the `Screenshot` folder next to the app; when that folder is read-only (e.g. Program Files), the image is saved to `%LOCALAPPDATA%\SimpleZipDrive\Screenshot` and the actual error is reported if both fail.

### Fixed
- **FUSE: unmounting while the session was still starting could deadlock the mount permanently.** The mount callback runs on the FUSE loop thread; it now requests a clean exit without waking the loop with a mount-point request (which could only be served by that same blocked thread). Unmount also no longer freezes the UI while waking the session, no longer reports success when the session is still alive, and a failed unmount says so instead of pretending the drive is gone.
- **Mount lifecycle races and leaks.** A mount aborted during startup now always disposes the archive core and file handle; `Dispose` no longer races a starting mount; failed mounts no longer leave stale `IsMounted`/`CurrentMountPoint`/`CurrentArchivePath` state; and a transiently failing FUSE loop can no longer be disposed underneath live callbacks.
- **Disk-cache extraction.** Extraction of a shared archive stream is now fully serialized, the 7-Zip fallback is attempted for *any* extraction exception (including corrupt data surfaced through a BCL frame), fallback results are cached under the same key every other path uses (no duplicate extraction per handle), a deleted temp directory is no longer recreated after shutdown, and a failed instance can no longer unregister another instance's protected temp directory.
- **Dialogs answer the keyboard again.** Message boxes now honour Enter (default button) and Escape (Cancel/No), exactly like the WPF `MessageBox` they replaced; concurrent password prompts are serialized instead of nesting dispatcher loops, and dialogs are owned by the active window (so a prompt opened from Settings is not stacked behind the main window).
- **Shutdown hardening.** The update prompt can no longer appear after shutdown began, the log writer's token source is no longer disposed while its background task is still using it, and the screenshot file-name helper no longer overwrites the 1000th same-millisecond capture.
- **Error reporting is narrower where it should be.** Only WinFsp assembly-load failures and missing *application* data/temp paths are treated as environment conditions; missing unrelated assemblies or paths are reported again instead of being silently suppressed. Missing/unsupported filesystem drivers (Dokan, WinFsp, libfuse3, macFUSE) and a backend that does not exist on the current OS are also environment conditions now, so the startup warning can no longer send a bogus *"libfuse3 not found: FUSE is only available on Linux and macOS"* report from a Windows machine (bug report #68046).
- **A settings file moved between platforms can no longer produce a nonsensical driver warning.** The startup driver check and the mount-backend selection normalize the persisted backend for the current platform first (FUSE → Dokan on Windows, Dokan/WinFsp → FUSE on Linux/macOS), matching the normalization already applied when settings are loaded.
- **`F1` now actually opens the About window.** The *Help → About* menu advertised `F1` through `InputGesture`, which is display-only in Avalonia; it is now a real `HotKey`.
- **Mount service no longer blocks the UI thread on unmount.** The backend facade now disposes the previous backend on a background thread (its driver grace delay used to freeze the interface for ~0.5 s after every unmount and failed mount) and publishes the new backend only after the old one has been unmounted and released, so a concurrent unmount can never target the wrong instance.
- **Packaging.** Linux/macOS bundles no longer contain the Windows-only `winfsp-msil.dll`; every bundle ships a combined `7zip-license.txt` covering both the Windows and Unix binaries; the Unix 7-Zip binaries are committed executable. The packaging script can now produce **all six bundles from any host** - on Windows it writes the Unix zips through `System.IO.Compression` and records the Unix executable bit (host byte + external attributes) so `unzip` restores `0755` on the apphost and the 7-Zip binary (verified end-to-end on a Linux VM). The host-byte patch walks the real central directory located through the end-of-central-directory record instead of scanning the file for the `PK\x01\x02` signature, so a signature-looking byte sequence inside compressed entry data can no longer be overwritten and corrupt a bundle.

### Changed
- **About window redesigned.** It now shows the application icon, the three-part release version (`3.1.0` instead of `3.1.0.0`), and platform-specific credits - WinFsp/DokanNet on Windows, FUSE (FuseSharp) on Linux/macOS, and SharpCompress everywhere - with cleanly spaced links (the old inline links rendered stray spaces such as *"WinFsp ,"* and *"GPLv3 ."*). The developer website (`www.purelogiccode.com`) is now a clickable link, and the dialog sizes itself to its content, removing the large empty gap above the Close button.
- **Donate button.** A *Donate* button on the main toolbar - on the Mount/Unmount row, at the far right - opens <https://www.purelogiccode.com/donate> (with a fallback dialog if the browser cannot be launched).
- **Windows now defaults to the Dokan backend.** New installs mount through Dokan; WinFsp remains fully supported - install it and select *WinFsp* (or *Auto*) in Settings. The Settings list now offers only the platform's backends (Dokan/WinFsp on Windows, FUSE on Linux/macOS); a settings file moved between platforms is normalized on load. The README and docs explain that either Windows driver can be used and how to install the one you want.
- The 7-Zip fallback no longer depends on the `SharpSevenZip` package; the vendored 7-Zip 26.03 binaries live under `SimpleZipDrive/7zip/` and ship one per runtime identifier. Note: the Windows `7za.exe` does not read RAR (SharpCompress remains the primary RAR reader; Linux/macOS `7zz` can also read RAR).
- Passwords for fallback extraction are passed as a command-line argument to the short-lived 7-Zip process (see [Security & Privacy](docs/security.md)); the process runs headless and exits as soon as the entry is extracted.

### Internal
- All 37 findings from the deep review that preceded this release are fixed; the suite grew to **1,454 tests** and stays analyzer-warning-clean.
- Documentation (`docs/`) is published both as the repository wiki (side menu in `docs/_Sidebar.md`) and as a GitHub Pages site (just-the-docs navigation via `docs/_config.yml`).

## 3.0.1

### Fixed
- **Dokan variant: mount-point failures are now detected by error status instead of error text (issue #67092).** The retry guard matched the English-only message *"Can't install"*, so on localized systems (DokanNet ships German, French, and Swedish resources) it silently broke and deterministic failures were pointlessly retried, adding ~3 s of latency per failed mount. Retries are now gated on `DokanException.ErrorStatus`: only the transient `Error` and `StartError` statuses are retried; driver-install, drive-letter/mount-point, and version failures fail immediately.
- **Dokan variant: a failed drive letter or mount folder now shows a dedicated *"Mount Point Unavailable"* dialog** instead of the misleading reinstall-driver dialog. It explains that the letter or folder may already be in use or that permission is missing, and suggests a different letter/folder or running as administrator.

### Internal
- Update checks now read the current version from the Core assembly (version-pinned to both app executables) instead of `Assembly.GetEntryAssembly()`, which under IDE test runners is the test host and made the "same version" tests spuriously notify the user when the runner version was below the latest release.
- Updated `Meziantou.Analyzer` to 3.0.259, `SharpSevenZip` to 2.0.128, `Microsoft.NET.Test.Sdk` to 18.10.1, and the GitHub Actions (checkout v5, setup-dotnet v5, upload-artifact v7, download-artifact v8); restored trailing newlines accidentally removed from six Core source files by the analyzer cleanup.

## 3.0.0

### Added
- **ZArchive (`.zar`) mount support with true Zstd seekable random access.** `.zar` containers are mounted via [ZArchiveSharp](https://github.com/purelogiccode/ZArchiveSharp), a pure C# implementation of the Zstd seekable format with no native dependencies. Every read decompresses only the 64 KiB zstd blocks it touches, so files are never extracted to memory or a temporary directory - seeks are immediate, and opening a 20 GB container costs the same as opening a 20 MB one. This also brings seekable-zstd support to the Xbox 360 scene, the main producer of this format (issue #5).
- **Xbox XISO disc image support (`.iso`, `.xiso`, `.cso`) via [XISOSharp](https://github.com/purelogiccode/XISOSharp).** Original Xbox and Xbox 360 game images mount directly, in all common disc layouts (RAW, rebuilt sector-0, GLOBAL/XGD2, XGD3, XGD2 Hybrid, XGD1). File data is read on demand straight from the disc sectors; compressed CISO `.cso` containers (single file or split `.1.cso` part sets) are decoded block-by-block per read. Generic ISO 9660/UDF data discs are rejected at mount time with a clear *"not a valid Xbox XISO disc image"* message.
- **Automated releases (GitHub Actions).** Every push/PR builds the solution warning-clean and runs the full test suite. The release workflow produces the four `release_<version>_<Variant>_win-<arch>.zip` bundles (Dokan/WinFsp x x64/ARM64) and waits for manual approval in a protected environment before creating the GitHub release - bundles can be downloaded and reviewed as workflow artifacts first.

### Changed
- **Archive detection, file dialogs, and error messages now include the new formats.** `.iso`, `.xiso`, and `.cso` map to the XISO reader; `.zar` maps to the ZArchive reader. Xbox image mounts are read-only and passwordless, like every other supported format.
- **Xbox image note:** because `.iso` support targets the Xbox XDVDFS filesystem only, mounting a regular (non-Xbox) ISO image now produces a specific format error instead of the generic "not a supported archive" rejection.

### Internal
- Added `XISOSharp` 1.2.0; updated `ZArchiveSharp` to 1.3.0.
- Updated `Meziantou.Analyzer` to 3.0.257 across all projects.
- New `XisoArchive` / `XisoArchiveEntry` / `XisoEntryStream` adapters with dedicated test coverage (adapter plus `ZipFileSystemCore` integration, path-backed, stream-backed, `.cso`, and cyclic-image coverage); the suite now runs **1,245 tests**.
- Pre-release hardening: ZArchive extended-length (`128+` character) names decode correctly, corrupt ZArchive blocks fail the entry instead of caching truncated data, cyclic/corrupt XISO directory tables terminate instead of hanging the mount, and a corrupt-image mount no longer leaks the image handle.
- Docs in `docs/` sync automatically to the repository wiki; `WhatsNew.md` is tracked in the repository again so it ships inside every bundle.

## 2.9.1

### Fixed
- **Outdated Dokan drivers are blocked with a clear dialog instead of crashing the app.** DokanNet 2.3 requires the `DokanRegisterWaitForFileSystemClosed` export, which only exists in dokan2.dll 2.3.0+. On older Dokan installations (2.2.x and earlier) every mount crashed the process with an uncatchable `EntryPointNotFoundException` (crash reports #66665–#66667). Mounts are now refused up front with a *"Dokan Driver Outdated"* dialog showing the installed and required versions and offering to open the Dokan download page.
- **WinFsp variant: clear error instead of a raw assembly-load crash when `winfsp-msil.dll` is missing.** If the interop library was removed beside the executable (typically by antivirus software) or the package was extracted incompletely, mounting previously failed with `Could not load file or assembly 'winfsp-msil'`. The app now verifies the interop assembly can be loaded before every mount and shows a plain-language dialog explaining how to restore the file. This failure is also recognized as an environment condition, so it no longer floods the bug-report API.
- **Dokan variant: architecture mismatches detected during the version check now show the correct *"Dokan Driver Incompatible"* dialog** instead of the outdated-driver dialog with a bogus "Installed version: 0.0.0".

### Changed
- **Dokan driver requirement documented:** Dokan v2 **2.3.0 or newer** (ReadMe, docs, and the new dialog all state this).

## 2.9.0

### Fixed
- **Halved peak memory during memory-cache decompression.** Decompression now streams directly into the final exact-size buffer instead of building a growing `MemoryStream` and copying it with `ToArray()`. A 424 MB entry now peaks at roughly one copy of the data (first-open working set ~460 MB, down from ~880 MB) — this addresses the memory-inflation follow-up reported in issue #10: the previous transient double copy made Task Manager show about twice the true footprint, and any second transient allocation could look like memory "doubling" between runs.
- **Bug-report filtering no longer masks potential real bugs.** Only canceled HTTP requests are treated as expected user errors; other network failures and `CryptographicException`s that do not originate from archive decryption (SharpCompress) are reported to the bug API again. The update check remains quiet on offline machines because it handles its own network errors.
- **Corrupt RAR archives are no longer misclassified as wrong-password prompts.** A "Unknown Rar Header" failure during an encrypted mount now routes to the corruption path instead of re-prompting for a password up to three times with a misleading message.
- **Duplicate log suppression restored to case-sensitive comparison**, so two distinct messages that differ only in case are both kept.
- **The archive open retry backoff no longer blocks the UI thread.** Waiting for a transiently locked archive file uses an awaited delay instead of `Thread.Sleep`.
- **WinFsp variant: fixed mount failure "The path is empty" in the packaged single-file build.** The winfsp-msil interop's static initializer calls `FileVersionInfo.GetVersionInfo(Assembly.Location)`, which is an empty string inside a single-file bundle. `winfsp-msil.dll` is now shipped as a real file beside the executable so the interop version check succeeds.
- **WinFsp variant: fixed cross-integrity folder mounts on fresh directories.** `IsDriveLetterMountPoint` misclassified every absolute folder path (`C:\...`) as a drive letter, so the mount-point directory was never created and mounting failed with `0xC0000034`. A bare `M` or `M:` is a drive letter; any longer path is a folder and is created on demand.

### Changed
- **Consistent packaging for both variants.** The Dokan and WinFsp executables ship with the native `7z.dll` / `7z_arm64.dll` fallback libraries (and `winfsp-msil.dll` for the WinFsp variant) as real files beside the executable, so the SevenZip extraction fallback is available in both.

### Changed
- **Update check now uses the canonical repository endpoint** (`https://github.com/purelogiccode/SimpleZipDrive`). The fallback endpoint pointing at the previous repository owner was removed now that the GitHub transfer is complete.
- **Framework-dependent single-file executables.** Releases are now built without the .NET runtime embedded — only the .NET Desktop Runtime and the filesystem driver (Dokan or WinFsp) remain prerequisites. Each package contains the single `exe`, the native `7z.dll` / `7z_arm64.dll` fallback libraries (and `winfsp-msil.dll` for the WinFsp variant) beside it, plus this README, the license, and these release notes.

### Internal
- ReDoS-safe match timeouts added to all regular expressions.
- Lock objects migrated to `System.Threading.Lock`.
- Solution reorganized with dedicated `Models/` and `Interfaces/` folders; `ErrorLogger` and `StoredEntryStream` split into per-type files; all analyzer warnings resolved.
- SharpCompress updated to 0.50.4 and SharpSevenZip to 2.0.115.

## 2.8.0

### Fixed
- **Shared, refcounted memory cache for decompressed entries with LRU eviction** (issue #10). Each entry is decompressed once and the buffer is shared by every open handle and on-demand read; buffers stay warm after the last handle closes and are evicted least-recently-used under memory pressure, falling back to the disk cache. This eliminated the one-full-decompression-per-open behavior (memory scaling as concurrent opens × file size) and the re-decompression-per-read path used by paging I/O.
- Graceful handling of entry-semaphore disposal during shutdown.
- WinFsp variant aligned with stable WinFsp 2.1 (the 2.2.x driver packages were beta releases).
