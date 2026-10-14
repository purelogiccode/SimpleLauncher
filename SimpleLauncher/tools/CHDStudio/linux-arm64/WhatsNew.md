---
title: What's New
nav_order: 15
---

# What's New

## 3.9.4 (2026-10-13)

### CD extraction writes cooked sectors again (#11)

*   **Extracted BINs are Redump-identical again.** The built-in reader called CHDSharp with `cooked:false`, writing full 2448-byte raw frames (data plus 96-byte subcode) instead of chdman's cooked `DataSize` bytes per frame. A single-track MODE2_RAW disc extracted as 546,178,176 bytes (SHA1 `072ea00f...`) instead of 524,759,424 bytes (SHA1 `9b4c1b51...`) while the CUE looked identical. Extraction now passes `cooked:true`, matching `chdman extractcd` byte-for-byte (subcode omitted, audio byte-swapped). Reproduced against *Armored Core (USA) (Rerelease)*.
*   **Verify passing for the bad rebuild is by design.** A CHD built from the oversized BIN is internally consistent (232,218 frames instead of 223,112; 267,908,519 bytes), so both `chdman verify` and CHDStudio Verify report success. Verify checks the header hashes, not Redump: the header SHA1 (`6dc714d9...`) and the file SHA1 (`3e1b20c5...`) differ from 7-Zip's file hash by design. Delete BINs/CHDs produced by the broken extraction and re-extract.

### Housekeeping

*   Package updates: Meziantou.Analyzer 3.0.299 → 3.0.303, Microsoft.NET.Test.Sdk 18.10.1 → 18.11.0, VideoGameFileSystemParser 1.3.0 → 1.4.0.
*   Test suite: new `ExtractCd_CookedMatchesOriginalBin_RawAddsSubcode` regression test; 1105 unit tests pass (`Category!=Integration`).

## 3.9.3 (2026-10-11)

### Cue WAVE tracks with an over-declared header now convert (#68232–#68237)

*   **A WAV whose header claims more audio than the file contains no longer fails the conversion.** Both encoders trust the data chunk's size field, so a file that is short of its declared length — the widely distributed Policenauts Saturn English patch ships exactly this shape, one CD frame (2,352 bytes) over-declared on all three discs — made `chdman` abort with `Error reading input file (...wav)` and the built-in CHDSharp fallback report `Unexpected end of file (...wav)`. The app now detects the over-declaration before encoding and clamps the data and RIFF sizes to the bytes actually present, so the conversion reads the audio that exists. The repair happens on a private copy; the user's files are never modified.
*   **Only the damaged WAV is copied.** The repaired work set is staged on the cue's own volume and references every other file (including multi-hundred-MB disc images) in place by relative path. When a same-volume location is unavailable it falls back to the full copy-based work directory, which repairs the WAVs there.
*   **Both encoders produce the same result for a repaired set**: a byte-identical CHD from `chdman` and the built-in CHDSharp encoder (verified against the real Policenauts disc 1 files).

### Housekeeping

*   `PbpFileIntegrationTests` now early-returns when its local samples are absent, matching every other integration group (§11.5); without samples the tests report as passed instead of failing on `Assert.NotEmpty()`.
*   MDSSharp published as **1.2.1** with repository metadata pointing at the renamed CHD Studio repository; no functional changes.
*   Package updates: Avalonia 12.1.3 → 12.1.4, SharpCompress 0.50.4 → 0.50.5, Meziantou.Analyzer 3.0.294 → 3.0.299, xunit.runner.visualstudio 4.0.0 → 4.0.1.
*   Test suite grew to **1133 tests** (1104 unit + 29 integration) across 58 test classes, including `WavHeaderSanitizer` unit coverage, the CueWorkDirectory repair paths and a real-`chdman` end-to-end regression test for the repaired cue set.

## 3.9.2 (2026-10-05)

### Duplicate outputs are skipped before conversion

*   **A duplicate input is stopped before any encoding starts.** When two inputs in one batch resolve to the same output CHD and the collision only becomes visible once an archive is extracted (its product is named after a file inside the archive), the duplicate is now skipped immediately; previously it was converted in full and the finished product discarded. The first product is kept exactly as before — the only difference is that no time is spent encoding something that will not be used.
*   **The duplicate notice can no longer become a bug report.** `"<name>.chd was already produced earlier in this batch; keeping the first one."` is now logged at information level instead of warning. Combined with the 3.9.1 exclusion pattern (kept as a safety net), this ends the false reports that 3.9.0 clients sent for every duplicated game in a batch.

### Reliability

*   **Screenshot capture no longer risks a disposed bitmap.** The F8 capture now hands the `RenderTargetBitmap` to a dedicated `SaveScreenshot` overload owned by `ScreenshotService`, instead of capturing it in a delegate that escaped the `using` block that disposes it.

### Housekeeping

*   MDSSharp cleanups: the `index00Tracks` null check is expressed as `index00Tracks?.Contains(track.Number) ?? pregapsInFile`, and `long` locals use `var` where the type is apparent.
*   PBPSharp and test-project cleanups of the same kind; the throughput test documents its deliberate captured-counter lambda with a `SuppressMessage`.

## 3.9.1 (2026-10-05)

### PBP extraction — PBPSharp 1.1.4

*   **PBPs whose compressed blocks were written without a final deflate block now extract.** Some authoring tools flush each PSAR block with `Z_SYNC_FLUSH` instead of finishing the stream, so the last deflate block never sets BFINAL. SharpZipLib's stream wrapper reported that as "Unexpected EOF" and the whole file failed with `DecompressionError`; the raw inflater is now driven directly and accepts a complete 16-sector block, while a block cut short still fails. Verified byte-identical against real *Brave Fencer Musashi* and *Ridge Racer* PBPs across compression levels (stored, default and maximum).
*   **A block cut short can no longer be accepted as a short block.** The decompressed output is bounded to exactly one 16-sector block, so a stream that ends before a full block, or expands past one, is rejected with `DecompressionError` instead of silently shifting every later block in the extracted image.

### Fewer false bug reports

*   **The by-design duplicate-product notice is no longer reported.** `<name>.chd was already produced earlier in this batch; keeping the first one.` appears when two inputs resolve to the same CHD (typically an archive whose contents were not known at the collision preflight); the first product is kept, as intended.
*   **CHDSharp device and permission errors are classified as environment conditions.** A drive that was unplugged, a failing disk, a full destination or denied access now logs `CHDSharp could not read or write the image: ...` and is excluded from bug reports; only genuine encoder defects still report.

### Release bundles for all six platforms

*   **The Release workflow now publishes and attaches one zip per runtime identifier**: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64` — all framework-dependent single-file builds, so users install the .NET 10 Desktop Runtime on Windows and the .NET 10 runtime on Linux/macOS. Each zip carries only its own platform's tools (`7za`/`chdman` on Windows, `7zz` plus `7-Zip-License.txt` on Linux/macOS) and the platform's native Avalonia libraries, and the packaging script verifies every required file before zipping.
*   **Linux and macOS are no longer source-only.** They now get the same release downloads as Windows, matching the already cross-platform application.

### Housekeeping

*   Roslynator.Analyzers bumped 5.0.0 → 5.0.1.
*   Test suite grew to **1121 tests** (1093 unit + 28 integration), including regression coverage for unfinished deflate streams, truncated PBP blocks and a zero-length stored index entry.
*   PBPSharp published as **1.1.4**; the README, library reference, bug-reporting guide and troubleshooting guide document the new block handling and exclusions.

## 3.9.0 (2026-10-03)

### Renamed to CHD Studio

*   **The project, application and executable are now CHD Studio** (`CHDStudio`): the GitHub repository moved to <https://github.com/purelogiccode/CHDStudio> (the old URL redirects), the solution is `CHDStudio.sln`, the project folder is `CHDStudio/` and the Windows executable is `CHDStudio.exe`. The five NuGet libraries keep their names; their repository links point at the new URL from the next package release.
*   **Logs and screenshots now live under `%LocalAppData%\CHDStudio`** (old `%LocalAppData%\BatchConvertToCHD` logs are not migrated); the temporary work folder fallback is now `CHDStudio_Temp`.

### Avalonia becomes the application front end

*   **The Windows-only WPF front end is gone**; the cross-platform Avalonia app is now the base project and builds `CHDStudio.exe`. It runs on Windows, Linux and macOS; official release zips remain the two Windows architectures.
*   **The application project was renamed `BatchConvertToCHD.Avalonia` → `CHDStudio`**, so the folder, project and executable all carry the product name.
*   **Shared code and the bundled tools moved into `CHDStudio/`**, so the project no longer links files from another folder. Windows keeps the bundled `chdman`/`7za`/`CHDSharp`; Linux and macOS discover them (plus `ffmpeg` for MP3 tracks) on `PATH`.
*   **UI parity with the old WPF build** was restored: Alt+letter button mnemonics, click-to-sort grid columns with the original header tooltips, and accessibility names on the main controls.
*   **F8 screenshots are now window-scoped**: while the app window is focused, F8 captures it with `RenderTargetBitmap` on every platform (previously a system-wide hotkey captured the foreground window on Windows only). Screenshots are saved into the `screenshots` folder inside `%LocalAppData%\CHDStudio`, beside the `logs` folder, with a `screenshots` folder next to the app as fallback.
*   **The main window has a Donate button** (left of About) that opens the project's donation page, and the About window now scrolls so every acknowledgement is reachable on short screens.
*   **The Close button always closes the app again**: shutting down from inside the window's Closing event re-entered the close in a loop, so the window never actually closed while an operation was idle.
*   **The activity log is batched and capped**: log lines are flushed in batches instead of one dispatcher call per line, and the on-screen text is capped, so a very large log can no longer freeze the window. The Explorer tab now hides the log panel entirely instead of leaving a clipped strip of it over the window edge.
*   **The About window is complete**: it now carries the full third-party acknowledgements (CHDSharp, CHDMAN, Avalonia, the in-house image libraries, VideoGameFileSystemParser, SharpCompress, NAudio, Serilog and 7-Zip), a License section (GPL-3.0 with a link), the copyright line, and donate/release links.

### New CHD Explorer tab

*   **Browse a CHD like a disc**: pick a `.chd`, choose the file-system parser matching its console/system (35 formats, PlayStation auto-detection by default), and navigate the folder tree in a grid. Double-click a folder to open it, a file to extract and open it, or use Extract to save a folder to disk.
*   **Image Info**: an expander under the parser drop-down shows a read-only report built from the CHD header and map — version, hunk/unit sizes, logical size, image type (CD/GD-ROM/DVD/HDD/A-V), codecs, metadata tags, the CD/GD-ROM track table and the per-codec hunk distribution (the scan is capped at the first 1,000,000 hunks for very large images).
*   **Backed by VideoGameFileSystemParser 1.3.0** (<https://www.nuget.org/packages/VideoGameFileSystemParser>), rebuilt against CHDSharp 1.4.3 and published for this release.

### Laserdisc A/V support

*   **`.avi` laserdisc captures convert with `createld`**: the built-in CHDSharp encoder assembles every video frame into MAME's raw `chav` layout and compresses it with the `avhu` codec (delta-RLE Huffman video + per-channel mono FLAC audio), one frame per hunk, exactly like `chdman createld`; the Force CD/DVD overrides are ignored for an AVI.
*   **A/V CHDs extract back to AVI in-process**: the Extract tab gained a **Laserdisc (.avi)** format, and Auto-detection now reads the CHD's own flags — a CHD with no CD/DVD/HDD metadata is an A/V image and is extracted with CHDSharp's MAME-parity `ExtractLaserDisc` writer (YUY2 video + PCM audio). `chdman extractld`/`extractraw` remain the automatic fallback when the library cannot decode the CHD.

### Verification checksum reports

*   **Optional `<name>.checksums.txt` next to each verified CHD** (checkbox on the Verify tab): one whole-image SHA-1, CRC-32 and XXH3-64 plus per-track SHA-1, CRC-32 and XXH3-64 for CD/GD-ROM images. The whole-image hashes are computed over the decompressed image, so they match a hash of the extracted data rather than the CHD header's combined hash. The report follows the CHD into the Success folder, and a report that cannot be written is a warning — never a verification failure.

### Live tool output, progress and read-ahead

*   **Every `chdman` output line now reaches the activity log**, progress included, with completion lines ("Compression complete"/"Extraction complete"/"final ratio") marked with a check. The on-screen log caps a single line at 2,000 characters and appends at most 200 lines per flush, so a chatty tool cannot stall the UI, and the file sink rolls at 10 MB.
*   **The built-in CHDSharp encoder and reader log progress every 10%** for conversion (`ratio=`), verification, extraction and checksum hashing, so long library operations visibly keep running without one line per hunk.
*   **Extraction pre-decompresses 16 hunks in the background** (`ChdFile.ConfigureReadAhead`), so sequential reads overlap the disk writes on the single-file (DVD/HDD) and multi-track (CD/GDI) paths.

### Built-in CHD encoder — no CHDSharp CLI

*   **CHDSharp now runs in-process** (the NuGet library, not the bundled `CHDSharp.exe` CLI, which has been removed). On Linux and macOS it is the encoder, so those builds need no bundled native tool; on Windows the bundled `chdman` remains the primary encoder and the built-in CHDSharp is the automatic fallback. It mirrors chdman's commands and defaults (`createcd`/`createdvd`/`createhd`/`createraw`) and produces byte-identical CHDs.
*   **Conversion can no longer fail for a missing encoder**; a missing Windows `chdman.exe` only logs that the built-in encoder will be used. The status bar shows CHDSharp as always available.
*   **Official 7-Zip 26.03 console binaries for Linux and macOS are now bundled** (`7zz`, static builds on Linux and a universal binary on macOS) and copied next to the app for the matching runtime; `7-Zip-License.txt` is included. Windows keeps `7za.exe`.

### Reliability fixes

*   **CSO v2 / ZSO images decode correctly.** The reader applied CSO v1 index semantics to version 2: LZ4 blocks (high bit set) were returned as raw compressed bytes and stored or deflate blocks were pushed through the LZ4 decoder. Blocks are now classified the way the format defines them — compressed only when smaller than a full block, with the high bit selecting LZ4 over deflate — and a short final stored block is zero-padded instead of failing.
*   **A truncated CHD is never accepted as a success.** When `chdman` exited nonzero but left a non-empty file (the classic disk-full case), the app treated it as success and could delete the source. The output is now validated with the built-in reader first, and a partial file is discarded.
*   **Deleting originals never reaches outside the input folder.** A descriptor naming `..\..\other\game.bin` (or an absolute path) could make the batch delete files it never converted; referenced files outside the selected input folder are now kept and reported.
*   **Archived discs keep their folder structure.** Two same-named discs in different archive subfolders (`Disc1/game.cue`, `Disc2/game.cue`) both produced `game.chd`, and the second silently replaced the first; the archive's internal path is now preserved. The same-batch duplicate guard keeps the first product and reports any remaining collision.
*   **Extracting next to an existing disc set no longer replaces it.** The "extract into a subfolder instead of overwriting" rule now also covers cue/gdi sets (checking their BIN too) and applies to the `chdman` fallback, not just the built-in reader. Cancelling a fallback extraction now kills the `chdman` child instead of leaving it running.
*   **A PBP whose volume descriptor declares zero sectors** no longer extracts an empty BIN and reports success; every index entry is written when the size is unknown.
*   **Expected network and user conditions no longer file bug reports**: an update check that times out (now bounded to 30 seconds instead of the shared client's 100) and a CHD Explorer parse failure caused by choosing the wrong file-system parser are logged as network/user conditions instead of being sent to the bug tracker.
*   **PBP I/O failures carry the underlying OS error** (PBPSharp 1.1.3): an open or extraction that fails with `IoError` records the file, the OS message and whether it was a read or a write, so a locked file, a failing disk or a full drive is identifiable from the log and the bug report.
*   **CD-R/CD-RW MDS v2 descriptors** read their track lengths from the extra block like pressed CDs, and a `.ccd` claiming a huge `TocEntries` value can no longer make the parser allocate unbounded tracks.
*   **MP3 decoding on Linux/macOS is cancellable**: `ffmpeg` is awaited with the operation token and killed on cancel instead of blocking the window in an uncancellable `WaitForExit()`.
*   **Real `.mdx` containers are read**: the MDS v2 parser applied the 1 MB v1 descriptor cap before checking the version, so every genuine single-file MDX (which embeds the whole image) was rejected, and the whole file was loaded into memory. The header is now read first, the descriptor is decrypted/decompressed from a stream, and track data is decoded per footer: the footer's `track_data_length` (the sectors actually stored) is used instead of the extra block's logical length, so a pregap kept in the data file is no longer truncated, and a track split across several data files decodes every footer in order. The decoder also streams groups instead of buffering whole tracks, and a compressed descriptor that legitimately expands beyond the file size is no longer rejected.
*   **`chdman createcd` no longer receives an invalid `-us 2352` switch.** A cue referencing a `.raw` track made the Windows primary encoder exit with "Option '-us' not valid for this command" and silently fall back; chdman derives the unit size from the cue's track types, so the switch is now only passed to `createraw` — including when a `.raw` input is combined with Force CD or Force DVD, which previously still appended it to `createcd`/`createdvd`.
*   **The built-in encoder rejects inputs chdman refuses.** A `createdvd`/`createhd`/`createraw` input whose size is not a whole number of units is now rejected up front instead of dropping the partial trailing unit and reporting success (after which the source could be deleted).
*   **Explorer extraction never replaces existing files**: extracting an entry into a folder that already holds a file or folder of the same name now lands in a numbered subfolder, the same isolation the Extraction tab applies; a staged directory is renamed into place in one step when the destination is free, and a temp extraction opened in a viewer is deleted after the viewer exits instead of 30 seconds later.
*   **The same-batch duplicate-output guard now covers the built-in encoder too**, and a reservation is released when the final move fails, so a failed product cannot block a later input that resolves to the same CHD.
*   **Temp folders no longer litter every drive.** `GetBestTempDirectory` prefers the system temp folder whenever its path is safe for chdman and its volume has room, instead of always picking the roomiest drive and creating `CHDStudio_Temp` at its root; the drive-root fallback is only used when `%TEMP%` is unsafe or too small, and an emptied `CHDStudio_Temp` folder is removed after use and at startup (stale Explorer extractions older than a day are cleaned up as well).
*   **The speed card shows chdman's throughput.** The write-speed sampler measured the application's own process I/O, which stays near zero while the bundled `chdman` (a separate process) does the writing, so the card read `0.0 MB/s` for every chdman conversion and looked stalled. It now samples the `chdman` child process, while the in-process CHDSharp encoder keeps using the app's counter.
*   **The activity log keeps its mode instructions when the tab changes**: the clear is now synchronous, so the welcome/ready lines queued by the same selection change are no longer discarded.
*   **Completion dialogs are awaited**: the batch summary is shown before a close requested during the run is honoured, and the encoder notice is dismissed before the update prompt appears.
*   **Smaller fixes**: a file-system-watcher history clear is atomic with event recording; a Windows drive root is no longer trimmed to a drive-relative path when building a temp folder; the ISZ chunk table and PBP SFO string length are bounded before their `int` casts; a CCD `.img` with a partial trailing sector is rejected instead of silently dropped; archive bin sets are grouped case-sensitively on case-sensitive file systems; a bare `.bin` with a companion cue now converts through the cue; 7-Zip arguments are passed via `ArgumentList` so quotes in paths cannot inject switches; the bug-report throttle's safety-net timer can no longer clear the flag of a newer send; a corrupt CSO index, MDX descriptor or ISZ chunk table is rejected before a huge allocation.
*   **A laserdisc AVI that fails both encoders is no longer misdiagnosed**: the post-failure sector-alignment diagnostic exempts `.avi`, so the real encoder error is shown instead of "file size is not divisible by any standard sector size".
*   **A temp extraction opened in an external viewer is no longer deleted too early on Linux/macOS**: `xdg-open`/`open` exit as soon as they hand the file to the viewer, so a quick exit now gets the fixed grace delay instead of removing the file while the viewer is still starting.
*   **A CSO whose header declares zero uncompressed bytes** no longer "extracts" to an empty ISO and reports success; it is reported as an invalid header. A temp directory is only accepted as writable when it really is, and a descriptor's referenced-file deletion check compares paths case-sensitively on case-sensitive file systems.
*   **MDS v2 footers are read like the format reference**: a track whose footer count is zero but that carries a footer is decoded from that footer instead of being treated as uncompressed, and a later fragment of a split track that names no data file is reported instead of silently reading the first data file again.
*   **MDS v2 pregaps are resolved per track**: the footer's stored length tells whether each track's pregap is in the data file (`length` = missing, `length + pregap` = stored, the layout libMirage validates). An image that keeps one track's pregap and omits another's is now rebuilt selectively — only the missing pregaps are materialized as zeros — so every `INDEX 00`/`INDEX 01` lands at the LBA the descriptor names instead of the cue drifting after the first omitted pregap. The first track's pregap before LBA 0 is never materialized, matching libMirage's NULL pregap for track 1.

### Housekeeping

*   Version bumps: application 3.9.0; libraries CCDSharp 1.0.1, CSOSharp 1.0.1, ISZSharp 1.0.2, MDSSharp 1.2.0 (MDS v2/MDX), PBPSharp 1.1.3; Meziantou.Analyzer 3.0.294.
*   Release script strips native `.pdb` debug symbols from the zip and verifies the Avalonia native libraries are present.
*   Docs, AGENTS.md and CI updated for the new base project; tests now reference the Avalonia assembly.
*   Test suite grew to **1116 tests** (1088 unit + 28 integration), including new CSO v2 stored/LZ4/deflate, PBP zero-size, corrupt-CCD, MDX container, encoder divisibility, IoThroughputCounter, checksum-report, Image Info, CHDSharp-progress, MDS v2 per-track pregap and `createld` regression tests (with a committed laserdisc AVI fixture).
*   The `docs/` folder is the single source of truth for both documentation homes: GitHub Pages (Jekyll/just-the-docs navigation) and the GitHub wiki (`docs/_Sidebar.md` side menu, `index.md` → `Home.md`, `WhatsNew.md` synced from the repository root).

## 3.8.0 (2026-09-19)

### Multi-part RAR archives now convert end to end (#67305)

*   **A `.partNN.rar` set is decoded from its first volume**, whether the batch lists the first part or a later one. SharpCompress can only follow the whole set when it is opened by path through the first volume, so a later part is redirected to the first volume found beside it; when the first volume is missing the file is skipped with a message naming it instead of failing inside the decoder. This supersedes the 3.7.0 note that multi-part RAR still needed manual extraction.
*   **RAR sets renamed to `.001` are extracted by content** instead of being refused, because the extension hides what the content says.
*   **A set is offered once**: only the first volume stays in the batch, filtered both at the folder scan and again before the batch starts, so a multi-part RAR is no longer extracted once per volume.
*   **Malformed archives are classified as data, not app bugs**: SharpCompress decoder crashes (`NullReferenceException`, `ArgumentOutOfRangeException`, `IndexOutOfRangeException`) on corrupt data no longer trigger the temp-copy retry or an automatic bug report.
*   Volume discovery covers new-style `.partNN.rar`, numbered `.001` sets and old-style `.rar` + `.r00` volumes, and the disk-space preflight measures the whole set. A real WinRAR-built multi-volume fixture is committed so the path is tested without WinRAR at test time.

### Alcohol 120% support upgraded — MDSSharp 1.1.0

*   **The library was renamed `Alcohol120Sharp` → `MDSSharp`** and published as **MDSSharp 1.1.0** (<https://www.nuget.org/packages/MDSSharp>) with package metadata, a README and an icon matching the other embedded libraries.
*   **Pregaps are now described**: when the descriptor records pregap and track length, the generated cue carries `INDEX 00` if the data file already contains the pregap sectors, and pregaps the file does not contain are rebuilt as zeros so the cue can still express them without shifting the tracks that follow.
*   **Multi-file descriptors are joined** in track order before preparation, and the temp-space preflight accounts for the rebuilt or joined image.
*   **The descriptor is read more completely**: medium type (CD/CD-R/CD-RW/DVD/DVD-R), each track's extra block (pregap and length) and the footer data-file names (single-byte or UTF-16, `*.mdf` wildcards) are parsed, and the track mode now uses the low nibble of the mode byte exactly as libMirage's reverse engineering established.
*   A CD descriptor whose sectors are the cooked 2048 bytes is converted through a `MODE1/2048` cue instead of being treated as a DVD image.

### ISZ support upgraded, library renamed to ISZSharp

*   **Genuine UltraISO files now decode.** The ISZ library was checked against libMirage's ISZ filter and isz-tool, the two independent open-source readers, and now handles the real-file behaviours the published specification omits: the segment and chunk tables are de-obfuscated (XOR with `B6 8C A5 DE`, the complement of `IsZ!`) and bzip2 chunks get their stripped `BZh` header restored before decompression. Without those, a real `.isz` could not be decoded at all — the old test fixtures mirrored the same omission, so the suite passed while the reader could not open a genuine file.
*   **More layouts are read**: images whose header declares no chunk table (one raw run), and split images named `game.part01.isz`/`game.part02.isz` as well as the spec's `game.i01`/`game.i02`.
*   **UltraISO's checksum is validated** when the 64-byte header carries one; a mismatch is reported as damaged and the output deleted, like a size shortfall. A failed or cancelled decode now always deletes its partial output.
*   **A split image whose cut lands inside a chunk now decodes.** A later segment stores the tail of the straddling chunk (`left_size`) between its header and its chunk data; the reader started at the chunk data offset and skipped that tail, so a complete split image was rejected as truncated. Fixed in **ISZSharp 1.0.1**, and the split fixtures now use UltraISO's real segment layout, so the round-trip tests exercise it.
*   **The library now matches the other embedded packages' shape**: renamed `UltraIsoSharp` → `ISZSharp`, multi-targeting `net8.0;net9.0;net10.0`, shipping XML docs, a README and an icon, and published on NuGet (<https://www.nuget.org/packages/ISZSharp>) as **1.0.0**, then **1.0.1** with the split-boundary fix.

### Smaller fixes

*   Deleting originals for an Alcohol image now removes every data file the descriptor names and every volume of a split set, not just the first.
*   A multi-file `.mds` descriptor with one declared file missing is reported by name instead of silently falling back to the file that happens to be present, which could convert a truncated image.
*   A folder holding both `.part1.rar` and `.part01.rar` keeps the spelling whose next volume exists, so the set is still decodable.

### Housekeeping

*   Version bumps: application 3.8.0, MDSSharp 1.1.0, ISZSharp 1.0.1.
*   Library updates: Meziantou.Analyzer 3.0.264.
*   Test suite grew to **896 tests** (real multi-part RAR extraction, ISZ real-layout splits and checksums, MDS pregap/multi-file handling, RAR volume filtering).

---

## 3.7.2 (2026-09-16)

### Recovered images with Mode 2 or subchannel layouts now convert (#67139)

*   **A decoded ECM whose source was not a plain 2352-byte image is no longer reported as damaged.** Alcohol `.mdf` rips can store 2336-byte Mode 2 sectors, or 2448/2368-byte sectors carrying subchannel data, and all three were skipped with "the decoded image is not a whole number of 2352-byte CD sectors or 2048-byte data sectors" even though the ECM trailing checksum had already proved the file intact. The new `RecoveredImageClassifier` routes every recovered image (ECM, ISZ, archive, split set) by its actual layout: raw 2352-byte CD sectors get a generated cue as before, 2048-byte sectors convert as a DVD image as before, 2336 and 2324-byte Mode 2 sectors get a `MODE2/2336`/`MODE2/2324` cue (both round-trip losslessly through chdman 0.289), and 2448/2368-byte rips have their subchannel tail stripped to 2352 and are then sniffed and cued — the same strip the `.mds` path already used. Only a size that fits no standard layout is still reported as damaged.
*   The "not a whole number of 2352-byte CD sectors..." skip is now excluded from automatic bug reports, like the other user-data conditions.

### Housekeeping

*   Test suite grew to **851 tests** (recovered-image layout routing: 2336/2324 cues, 2448/2368 stripping, skip reasons).

---

## 3.7.1 (2026-09-16)

### MDS descriptors find renamed and nested data files (#66955, #66989)

*   **A `.mdf` no longer has to sit beside its `.mds` under the exact same name.** The descriptor's data file is now found when it was renamed with a decoration (`Game.mds` beside `Game (USA).mdf` — but never when the name continues with a letter or digit, so `Game 2` never matches `Game`), when it sits one folder down (only an unambiguous exact-name match, so a sibling game's image is never picked up), and split `.i00` sets are located by base name even when other sets share the folder. File names that differ only in Unicode composition (`Cafe\u0301` vs `Café`) are treated as equal.
*   **Ambiguity is refused, not guessed**: with `Game (Disc 1).mdf` and `Game (Disc 2).mdf` both beside `Game.mds`, the disc is skipped with the existing "data file was not found" message instead of converting the wrong disc.

### Transient network/NAS failures no longer abort a batch (#66854)

*   **Archive copies retry transient I/O errors** before the temp-copy fallback runs: a failed direct extraction (for example an SMB hiccup while streaming from a NAS) is demoted to a debug notice, and the automatic copy that precedes the fallback retries up to four times with increasing delays. Missing-source errors are never retried.
*   **Network-unavailability detection is locale-independent**: the Win32 error codes (bad netpath, unexpected network error, netname deleted, …) are now read from the exception's `HResult`, so non-English Windows builds (e.g. French `"Erreur réseau inattendue."`) are recognized without relying on localized message text.

### Startup crash suppressed: desktop composition disabled (#67014, #67084)

*   A user with desktop composition disabled (registry/DWM tweak or third-party theming tool) got a `COMException 0x80263001` from `WindowChromeWorker.DwmExtendFrameIntoClientArea` reported as an unhandled dispatcher exception. It is now suppressed like the other known-benign WPF-internal exceptions: the window simply runs without the glass frame effect.

### Bug-report noise reduction (#67027)

*   **"The output folder is not writable"** (root of `C:\`, `Program Files`, a read-only drive) is excluded from automatic reports — the batch-start probe already shows one actionable dialog and nothing was converted.

### Housekeeping

*   **Release zips are leaner**: the library `.xml` IntelliSense doc files (`CCDSharp.xml`, `CSOSharp.xml`, `PBPSharp.xml`) are no longer included — nothing at runtime uses them.
*   Library updates: Meziantou.Analyzer 3.0.259, Microsoft.NET.Test.Sdk 18.10.1.
*   Test suite grew to **842 tests** (decorated/ambiguous/subdirectory/split/Unicode `.mdf` lookup, transient network copy retries, SFO size bounds, the new bug-report exclusion).

---

## 3.7.0 (2026-09-09)

### Split archive volume sets now convert end to end

*   **Extract split 7-Zip and ZIP sets in-app** (`.7z.001`/`.zip.001` style): what used to be skipped with "extract the set manually" is now extracted with the bundled `7za.exe` and converted like any other input. The whole set is located from the first volume, disk space is checked against the *total* size of all volumes, and the extracted image is classified (cue/gdi/toc convert directly, bare images get the same recovery path as joined sets). Multi-part **RAR** still needs manual extraction, since the app does not carry RAR tooling.
*   Split-set skip notices (missing volume, no convertible image inside, missing `7za`) are user-data conditions and are excluded from automatic bug reports.

### Output folder convenience and resilience

*   **Output folder auto-fill**: selecting a source folder (browse or command line) now sets the **Output CHD** destination to the same folder when the output path is still empty. This is safe by construction — outputs are staged `<name>.chdtmp` and moved into place only on success, and existing CHDs are never inputs.
*   **Disconnected output drive detected up front**: a batch whose output folder sits on an unplugged USB stick or a dropped network drive now probes the folder once and reports a single actionable message, instead of failing every file with a confusing path error mid-batch.
*   **Temp-folder cleanup is best effort**: staged files carrying the read-only attribute (or locked by antivirus) no longer abort the batch or get reported as app bugs — attributes are cleared before deleting, directory read-only flags included, and a leftover temp folder is only logged as a warning.

### PBP extraction — PBPSharp 1.1.0

The bundled PBP library was aligned with all four reference implementations (popstation, PSX2PSP, iPoPS, pop-fe), so PBP files that earlier versions rejected now extract:

*   **pop-fe multi-disc PBPs no longer rejected**: pop-fe leaves the four "template" DWORDs of the `PSTITLEIMG000000` header zero (popstation/PSX2PSP write fixed magic values there). The header values are no longer validated — the disc position table, which is what actually locates the discs, is honoured regardless.
*   **PARAM.SFO is no longer required**: a missing or corrupt SFO leaves title/disc-id metadata empty instead of rejecting the whole file. None of the reference tools read the SFO when extracting disc images.
*   **zlib-wrapped PSAR blocks decompress**: blocks authored with `zlib.compress` (2-byte header + Adler-32) instead of raw deflate are retried as zlib streams after the raw inflate fails.
*   **pop-fe index layout supported**: the official 16-bit index entry (size `uint16` at bytes 4–5, stored-flag byte at 6, SHA-1 at 8–23) is read directly; tools that write the size as a 32-bit int (popstation/PSX2PSP/iPoPS) are still covered because bytes 6–7 stay zero there.
*   **Stored/uncompressed blocks flagged in the index** (pop-fe `--compression 0`) are copied verbatim instead of being inflated.
*   **Incompressible blocks accepted**: index entries a few bytes *larger* than the raw 16-sector block (deflate framing on incompressible data) are no longer rejected.
*   **64-bit offset math** for block reads, so multi-gigabyte files cannot overflow the 32-bit offset computation.
*   Hardened against corrupt indexes: an entry claiming to be a stored block larger than a full block is reported as corrupt data instead of overrunning the read buffer, and the split-set disk-space check now uses the whole volume set rather than the first volume alone.

### Bug-report filtering additions

*   chdman crashes during the **startup probe** (entry-point-not-found / illegal-instruction exit codes — an installation incompatibility, not app logic) are excluded.
*   "Output folder is not available" notices (unplugged drive) are excluded.

### Housekeeping

*   Version bumps: application 3.7.0, PBPSharp 1.1.0.
*   Library updates: Meziantou.Analyzer 3.0.231, Microsoft.NET.Test.Sdk 18.10.0.
*   Test suite grew to **835 tests** (split-volume-set extraction, PBP index/deflate variants, pop-fe multi-disc, SFO tolerance, bug-report exclusions).

---

## 3.6.0 (2026-09-07)

### Encoder resilience

*   **A batch runs on the CHDSharp fallback when `chdman` is missing or fails its startup probe**, instead of refusing to start. The startup dialog severity now reflects encoder priority (critical only when *both* encoders are unusable).

### PBP extraction fixes

*   **Corrupt deflate streams are classified as data errors**: `IndexOutOfRangeException` leaking from SharpZipLib's inflater is normalized to `PbpError.DecompressionError`, so a garbage PSAR block is reported as corrupt data instead of an app bug (regression-tested).
*   PBPSharp version bumps with the hardened block reader.

### Shutdown & noise

*   **Shutdown cancellation is treated as normal exit**: cancelling the window during startup no longer reports a `TaskCanceledException` bug report.
*   **Bug-report noise reduction**: encoder-missing notices, chdman fallback notices, CHD open/read failures, file-move failures, encoder start failures and low-disk-space warnings are excluded from automatic reports (low disk space is now informational, since the conversion proceeds).
*   **`0xC0000139` (entry point not found) crash decoding** added alongside the existing illegal-instruction mapping.

### Housekeeping

*   Library updates: NAudio 3.1.0, Meziantou.Analyzer 3.0.226; bundled 7-Zip binaries refreshed.
*   Application version bumped to 3.6.0; test suite at 820 tests.

