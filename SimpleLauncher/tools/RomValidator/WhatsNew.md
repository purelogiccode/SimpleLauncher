Release 2.9.2
2026-10-07

- Added a Donate button to the header (next to About) that opens the donation
  page at https://www.purelogiccode.com/donate in the default browser
- Version bumped to 2.9.2; the bundles remain single-file, framework-dependent
  executables named release_<version>_win-x64.zip and
  release_<version>_win-arm64.zip

Release 2.9.1
2026-10-07

- Fixed a race in the bug-report sink test that could make the release verification
  job fail intermittently
- The release workflow now checks out the full repository history before publishing,
  so GitHub can generate the release notes automatically
- Version bumped to 2.9.1; application behavior is unchanged from 2.9.0 and the
  bundles remain single-file, framework-dependent executables named
  release_<version>_win-x64.zip and release_<version>_win-arm64.zip

Release 2.9.0
2026-10-07

- Archive engine migrated from SharpSevenZip to SharpCompress, with the bundled
  7-Zip 26.03 executables (7za.exe / 7za_arm64.exe) as a fallback for archives
  SharpCompress cannot read or create
- RAR archives are converted to ZIP when internal filenames must be renamed
  (RAR creation is not supported); existing files are never overwritten during
  the conversion
- Internal archive renames now target the file whose hash matches the DAT entry
  instead of the first file in the archive, and folder structure is preserved
  when repacking
- Files locked by another process no longer generate bug reports; they are
  logged locally and handled with retries
- UTF-16/UTF-32 encoded XML DAT files are accepted again (binary detection now
  respects byte order marks)
- Disk-full errors while moving files stop the validation queue cleanly
- Release bundles are now single-file executables (RomValidator.exe) and include
  LICENSE.txt, ReadMe.md and WhatsNew.md alongside the bundled 7-Zip binaries
- Bundle names follow the update-checker convention: release_<version>_win-x64.zip
  and release_<version>_win-arm64.zip
- Documentation published to GitHub Pages and mirrored to the GitHub Wiki, with
  side navigation on both
- Continuous integration (build + tests on every push) and an automated release
  workflow (tag release_* or v* to publish the bundles)
