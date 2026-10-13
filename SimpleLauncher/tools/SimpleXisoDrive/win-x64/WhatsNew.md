# What's New in 1.5.0 (since 1.4.0)

Prepared for the `release_1.5.0` tag. Framework-dependent `win-x64`/`win-arm64`,
`linux-x64`/`linux-arm64` and `osx-x64`/`osx-arm64` builds require the .NET 10.0 Runtime (the base
runtime; the Desktop Runtime also works but is not required).

## Startup experience

- **Update checks run immediately.** The GitHub release check runs at startup on both front ends,
  before argument handling and mounting. A newer release is offered through the native message box
  (Windows) or the console prompt (Linux/macOS); non-interactive runs print the version and URL
  instead of showing a dialog, so scripts never block.
- **Missing Dokan offers the download page.** When `dokan2.dll` (runtime) or `dokan2.sys` (driver)
  is missing, the Windows front end warns and offers to open the Dokan releases page. A missing
  runtime still exits with code 1; a missing driver warns and lets mounting proceed.
- **Help is instant and offline.** `-h`/`--help` prints the usage text and exits with code 0 before
  the update check, launch statistics, or the Dokan/FUSE probe.
- **Usage before the backend probe.** A run without arguments prints the usage text and the
  drag-and-drop hint even when Dokan/FUSE is not installed.

## Windows command line

- **Options may appear before or after the mount path**, matching the Unix front end
  (`SimpleXisoDrive.exe game.iso -d Z:` works).
- **A single image argument with options stays in drag-and-drop mode** (`game.iso -i`), with
  `--launch` implied.
- **Folder mount paths are validated up front**: a missing folder is reported before Dokan is
  invoked (drive letters are created by Dokan and are exempt).
- **The administrator warning now covers `Z:` as well as `Z:\`.**

## Reliability

- **Read failures are I/O errors, not silent EOF.** A failed read now maps to `DokanResult.Error`
  on Windows and `-EIO` on Linux/macOS instead of returning a truncated result; short reads from
  the XISO layer are detected as truncation.
- **ZArchive reader access is serialized**, so Explorer browsing and an emulator reading the same
  mount cannot corrupt the archive reader state.
- **Case-sensitive entry caches** keep case-only-distinct names apart on case-sensitive file
  systems while the case-insensitive library lookup still serves any spelling on Windows.
- **The XISO explorer handle is released** when the volume descriptor cannot be read, and
  `ReaderOwningVfsVolume` disposal is idempotent.
- **Lookup misses are classified.** Paths that do not exist in the image (which Windows routinely
  probes, for example `\System Volume Information`) are logged at Debug level and are no longer
  forwarded to the bug report API.

## FuseSharp

- **The FUSE 3 mount layer is now a standalone library.** `FuseSharp` (NuGet package id
  `SimpleXisoDrive.FuseSharp`) contains the high-level mount, the platform interop and the
  availability probe, and has no dependency on `SimpleXisoDrive.Core`.
- **New `IFuseVolume`/`IFuseEntry` abstraction** with POSIX paths; `FuseFileSystem` consumes the
  interface, and the Unix app adapts the Xbox volume through `FuseVolumeAdapter`.
- **Direct use works without the app**: constructing `FuseFileSystem` registers the native library
  resolver itself.

## Logging, diagnostics and reporting

- **Expected user-setup and input conditions no longer auto-report**: missing Dokan/FUSE, missing
  image files, invalid or unsupported images, a failed `explorer.exe`/`xdg-open` launch and a
  failed FUSE session setup are logged below the bug-report threshold. Duplicate error reports
  were removed.
- **Launch statistics are tracked and awaited at shutdown** (up to 5 seconds), matching the bug
  report grace period, and `Process` handles from browser launches are disposed.

## Tests and documentation

- The suite grew to **476 tests**: 395 in `SimpleXisoDrive.Tests` and 81 in
  `SimpleXisoDrive.Unix.Tests`. New coverage includes the read-only access allow list (including
  the volume root), expected-condition logging, `XisoPathNotFound` classification, mount-path
  validation, deterministic FUSE probes, the `FuseVolumeAdapter`, and API failure handling.
- Documentation was refreshed for the new startup flow, CLI behavior and `FuseSharp`; both
  renderings (GitHub wiki and Pages) keep a shared side menu, now including the `FuseSharp` page.

# What's New in 1.4.0 (since 1.3.0)

Prepared for the `release_1.4.0` tag. Framework-dependent `win-x64`/`win-arm64`,
`linux-x64`/`linux-arm64` and `osx-x64`/`osx-arm64` builds require the .NET 10.0 Runtime (the base
runtime; the Desktop Runtime also works but is not required).

## Linux and macOS support

- **New FUSE 3 backend.** The project was split into a shared `SimpleXisoDrive.Core` library plus
  two light front ends: the existing Windows/Dokan app and a new `SimpleXisoDrive.Unix` app that
  mounts through the FUSE 3 high-level API (`libfuse3` on Linux, macFUSE on macOS). Native builds
  are produced for `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`.
- **Command-line mount.** `SimpleXisoDrive <image-file> [mount-path] [options]`. When the mount path
  is omitted, a temporary directory is created and printed; `fusermount3 -u` / `umount`, `Ctrl+C`,
  SIGTERM and SIGHUP all unmount cleanly.
- **Platform-correct ABI.** The FUSE operation table, `struct stat` and `struct statvfs` layouts are
  pinned for Linux x64/aarch64 and macOS, and the library is discovered in the standard locations
  (override with `SIMPLEXISODRIVE_FUSE_LIBRARY`).

## Virtual image.iso (`--image-iso`)

- **New `-i` / `--image-iso` option** adds a synthetic read-only `image.iso` file at the mount root
  while the normal tree stays browsable, so emulators that only accept a disc image (such as xemu)
  can open `<mount>\image.iso`.
- Plain ISO/XISO inputs and CISO `.cso` images (including split `.1.cso` sets) are served on demand:
  CISO blocks are decompressed through the XISOSharp block device, nothing is extracted.
- A ZArchive with a single embedded XISO is served from the embedded image on demand.
- A ZArchive directory tree is synthesized into an XISO **in memory**: the XDVDFS layout
  (volume descriptor, directory tables and file extents) is built with XISOSharp's public layout
  primitives and file data is read from the archive on demand. Nothing is extracted, the mount
  appears immediately, and the synthesized image is byte-identical to
  `XisoWriter.PackFromDirectory` output for the same tree.
- When the mounted image already contains a real `image.iso` file, that file is shown instead of the
  synthetic one.

## Xbox ISO CHD support

- **New `.chd` mounting.** Xbox ISO images stored as CHD are mounted read-only through the
  **CHDSharp** library: hunks are decompressed on demand (CHD V1–V5, all codecs) and the source file
  is never modified.
- **Xbox ISO only.** CD and GD-ROM CHDs are rejected up front, and the decompressed image must parse
  as XDVDFS; anything else fails with
  `"<path>" is not an Xbox ISO CHD (XDVDFS filesystem not found).`
- **`--image-iso` works for CHDs.** The virtual `image.iso` serves the decompressed Xbox image
  (a second independent CHD reader, so raw-image reads never race the volume stream).
- **Renamed CHDs still mount.** A valid CHD with any extension falls back to the CHD path when the
  XISO probe fails.
- The resolver recognizes `.chd` for extensionless paths and directory lookups.

## Reliability and review fixes

- **`image.iso` no longer inflates the reported volume size** for plain ISO/XISO/CISO, CHD and
  embedded-XISO mounts: the raw image is only added to the volume size for a synthesized ZArchive
  tree, where it is genuinely additional content.
- **Renamed files always mount.** `.iso` renamed to `.chd`/`.zar` (and the reverse) falls back to
  content detection instead of failing on the extension mismatch.
- **Bounded caches.** Volume entry and directory-listing caches stop growing at 4096 entries and 512
  listings, so a long-lived mount over a huge tree cannot consume memory without limit.
- **Clean ownership.** Identical-idempotent disposal across volumes and decorators; embedded-XISO
  probes never leak the archive reader; failed opens leave no stale Dokan handle context.
- **FUSE fixes.** macOS uses the exported `fuse_new_31` entry point (macFUSE's libfuse3 is built
  without ELF symbol versioning, so plain `fuse_new` is only a header macro); the `statfs` wake-up
  poke allocates a buffer that covers the macOS structure (2168 bytes vs 120 on Linux); temporary
  mount directories are removed on unmount; and option validation is case-insensitive like the
  Windows front end.
- **Windows update notification never blocks scripts.** The update message box is skipped when input
  or output is redirected or the process is non-interactive; the version and release URL are printed
  instead.
- **Truthful errors.** Locked or unreadable images surface the real I/O exception; only format
  failures are reported as "not a valid image".

## Logging, diagnostics and reporting

- All logging is routed through Serilog with a console level switch (`--debug`) and a rolling file
  sink; `Warning`-and-higher events are archived to `error.log` and forwarded to the bug report API
  with an 8-per-minute rate limit.
- In-flight bug reports are tracked and given a 5-second grace period at shutdown
  (`BugReport.WaitForPendingReportsAsync`), and all API clients (bug reports, launch statistics,
  update check) share one connection pool and TLS configuration.
- Launch statistics and the GitHub update check skip work when the API key is unavailable and never
  forward failures to the bug report API.

## Tests

- The suite grew from 89 to **336 tests**: 286 in `SimpleXisoDrive.Tests` and 50 in
  `SimpleXisoDrive.Unix.Tests`.
- `ChdVfsContainerTests` encodes real CHDs with the CHDSharp encoder and mounts them (rebuilt and
  standard layouts, trees, `--image-iso`, non-Xbox rejection, CD rejection, renamed fallbacks).
- `VirtualXisoImageSourceTests` proves the synthesized ZAR image is byte-identical to the XISOSharp
  whole-image writer for nested trees (sector-crossing files, empty files/directories) and validates
  unaligned reads; `ImageIsoVfsVolumeTests` and `StreamRawImageSourceTests` cover the decorator,
  size policy and stream reads.
- `VfsContainerTests` mounts `.iso`, `.cso`, embedded-XISO `.zar`, tree `.zar` and `.chd` inputs,
  including renamed files and the `--image-iso` option.
- The FUSE suite pins the Linux/macFUSE `fuse_operations` layouts and delegate conventions, covers
  resolver/library probing, mount arguments, path/time/directory helpers, POSIX errno values and
  case-insensitive option validation.

## Documentation

- Every page was refreshed for the cross-platform, CHD and `--image-iso` releases.
- Both renderings now have a side menu: `docs/_Sidebar.md` for the GitHub wiki, and
  `docs/_data/navigation.yml` + `docs/_layouts/default.html` for the published GitHub Pages site.
  The same Markdown serves both, with wiki-style links rewritten for the site at runtime.

# What's New in 1.3.0 (since 1.2.0)

Prepared for the `release_1.3.0` tag. Framework-dependent `win-x64` and `win-arm64` builds require
the .NET 10.0 Runtime (the base runtime; the Desktop Runtime also works but is not required).

This release adds ZArchive (`.zar`) mounting, CISO (`.cso`) image support, moves all XDVDFS parsing
to the **XISOSharp 1.2.0** library, modernizes ZArchive reads on **ZArchiveSharp 1.3.0**, and adds
CI plus a tag-driven release pipeline.

## Mounting

- **ZArchive support.** `.zar` files mount directly: directory-tree archives expose the stored game
  files, and an archive holding a single XISO image mounts that image. Content is read on demand
  through a zstd block cache; nothing is extracted to disk.
- **CISO support.** Compressed `.cso` images mount like plain ISOs, including split `.1.cso` part
  sets; the resolver recognizes `.cso` and treats a numbered part set as a single image.
- **Renamed archives still mount.** A valid ZArchive with any extension (including `.iso`) falls
  back to the archive path when the XISO probe fails.
- **ZArchiveSharp 1.3.0 reader API.** Node-handle enumeration (`TryGetDirEntry`), canonical names
  (`TryGetNodeName`), computed volume size (`TotalUncompressedSize`), seekable entry streams
  (`OpenRead`), shared-read opens, and typed `ZArchiveOpenFailure` reasons in error messages.

## XISO parsing

- **Parsing delegated to XISOSharp 1.2.0.** The in-repo parser (`IsoSt`, `VolumeDescriptor`,
  `FileEntry`, `XisoFsFileAttributes`) was removed. Path-based images use a keep-open `XisoExplorer`
  with a shared read/write handle; images embedded in archives use the `XisoReader` stream APIs.
- **Rebuilt sector-0 images** are detected alongside the standard sector-32 layouts (plain, GLOBAL,
  XGD3, XGD2 hybrid, XGD1), and the app reports the library's parsed attributes, size, and creation
  time.

## Reliability

- **Truthful I/O errors.** Locked or unreadable images now surface the real `IOException`; only
  format failures are reported as "not a valid image". ZArchive open failures keep their type
  (missing file, access denied/read error, invalid path, or bad format).
- **No handle leaks.** The embedded-ISO probe disposes the archive reader when the probe itself
  errors, and the probe stream on a failed validation.
- Directory walks use XISOSharp's hardened TOC traversal: cycle detection, per-table entry limits,
  and rejection of separator-bearing names.
- Reads are clamped to each entry's size, never the image length.
- Every Dokan operation still contains failures, logs them, and returns an error status instead of
  propagating.
- The shared console key-press watcher no longer swallows the key meant for an error prompt.
- The bug report / stats API key is no longer stored in plain text: it is double-encrypted in the
  assembly (AES-256-CBC over a SHA-256 XOR layer) and decrypted once at startup.

## Tests

- The suite grew from 43 to **89 tests**.
- `XisoVfsVolumeTreeTests` covers multi-file and nested images, empty files/directories, reads
  across sector boundaries, clamped reads, attribute mapping, descriptor FILETIMEs, and parallel
  reads in path and stream modes.
- `XboxIsoVfsDokanTests` exercises the Dokan operation layer directly with `MockDokanFileInfo` —
  volume information, `.`/`..` listings, wildcard filtering, `CreateFile`/`ReadFile` semantics, path
  normalization, and read-only denials — with no Dokan driver installed.
- `ResolveImagePathTests` and `VfsContainerTests` cover `.cso` resolution (including split sets),
  locked/missing images, and `ApiKeyProviderTests` verifies the decrypted key without storing it.
- `TestImageFactory` now builds arbitrary nested trees (with `TestImageEntry`) for standard
  sector-32 and rebuilt sector-0 layouts.

## CI and releases

- `.github/workflows/ci.yml` builds and tests every push/pull request to `master` on
  `windows-latest` and uploads TRX results plus Cobertura coverage.
- `.github/workflows/release.yml` runs on `release_*` tags: it verifies the tag against
  `<AssemblyVersion>`, runs the suite, publishes framework-dependent single-file executables for
  `win-x64` and `win-arm64`, packs each as `release_<version>_<rid>.zip` containing only
  `SimpleXisoDrive.exe`, and creates the GitHub release.
- The workflow can also be dispatched manually to produce the same zips as artifacts.

## Docs

- Updated `ReadMe.md` with a CI badge and the building guide; refreshed Architecture,
  Virtual File System, XDVDFS Format, Glossary, Testing, Troubleshooting, Contributing, and Release
  History for the library-based architecture and the new pipeline.
