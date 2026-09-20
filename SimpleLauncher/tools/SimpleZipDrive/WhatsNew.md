# What's New

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
